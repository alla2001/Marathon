using UnityEngine;
using System.Collections;
using MarathonMQTT;

public class MainGameManager : MonoBehaviour
{
    public enum GameState
    {
        IDLE,
        COUNTDOWN,
        PLAYING,
        PAUSED,
        FINISHED
    }

    [Header("Game Settings")]
    [SerializeField] private float totalDistance = 1600f;
    [SerializeField] private int countdownSeconds = 3;
    [SerializeField] private float gameDataUpdateInterval = 0.1f; // Send data 10 times per second
    [SerializeField] private float autoResetDelay = 8f; // Auto-reset after game over
    [SerializeField] private float idleTimeoutSeconds = 10f; // Force reset if no movement for this long

    [Header("Game Mode Specific Settings")]
    [SerializeField] private float rowingDistancePerStroke = 5f;
    [SerializeField] private float runningSpeedMultiplier = 1f;
    [SerializeField] private float cyclingSpeedMultiplier = 1.5f;

    [Header("References")]
    [SerializeField] private SplinePlayerController splineController;
    [SerializeField] private MachineDataHandler machineHandler;
    [SerializeField] private GameHUDController hudController;
    [SerializeField] private GameOverController gameOverController;
    [SerializeField] private GamePCIdleController idleController;

    [Header("Mesh Generators (per game mode)")]
    [SerializeField] private GameObject rowingMeshGenerator;
    [SerializeField] private GameObject runningMeshGenerator;
    [SerializeField] private GameObject cyclingMeshGenerator;

    [Header("Finish Line")]
    [SerializeField] private Dreamteck.Splines.SplineFollower finishLineFollower;

    [Header("Spline Meshes to Clip (will be clipped to finish line distance)")]
    [SerializeField] private Dreamteck.Splines.SplineUser[] splineMeshesToClip;

    private GameState currentState = GameState.IDLE;
    private string currentGameMode = "rowing";
    private string playerName = "";

    private float currentDistance = 0f;
    private float currentSpeed = 0f;
    private float currentTime = 0f;

    private Coroutine gameDataCoroutine;
    private Coroutine autoResetCoroutine;
    private float lastMovementTime = 0f;

    private void Start()
    {
        // Subscribe to MQTT messages from tablet
        if (MQTTManager.Instance != null)
        {
            // Register for connection event to subscribe when connected
            MQTTManager.Instance.OnConnected += OnMQTTConnected;

            // Register message handlers
            MQTTManager.Instance.OnStartGameReceived += OnStartGameReceived;
            MQTTManager.Instance.OnPauseGameReceived += OnPauseGameReceived;
            MQTTManager.Instance.OnResumeGameReceived += OnResumeGameReceived;
            MQTTManager.Instance.OnResetGameReceived += OnResetGameReceived;
            MQTTManager.Instance.OnGameConfigReceived += OnGameConfigReceived;

            // If already connected, subscribe now
            if (MQTTManager.Instance.IsConnected)
            {
                SubscribeToTopics();
            }
        }
        else
        {
            Debug.LogWarning("MQTTManager not found! Make sure it exists in the scene.");
        }

        SetGameState(GameState.IDLE);

        // Set initial mesh generator state based on default game mode
        UpdateMeshGenerators(currentGameMode);
    }

    private void OnMQTTConnected()
    {
        Debug.Log("[MainGameManager] MQTT Connected! Subscribing to topics...");
        SubscribeToTopics();
    }

    private void SubscribeToTopics()
    {
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            // Subscribe to tablet commands using station-specific topics
            MQTTManager.Instance.SubscribeToStation("TABLET_TO_GAME");

            // Subscribe to our own game data topic to receive countdown/state messages
            MQTTManager.Instance.SubscribeToStation("GAME_TO_TABLET");

            // Subscribe to game config broadcast
            MQTTManager.Instance.Subscribe("marathon/config/broadcast");

            Debug.Log($"[MainGameManager] Subscribed to station {MQTTManager.Instance.StationId} topics");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from MQTT events
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnConnected -= OnMQTTConnected;
            MQTTManager.Instance.OnStartGameReceived -= OnStartGameReceived;
            MQTTManager.Instance.OnPauseGameReceived -= OnPauseGameReceived;
            MQTTManager.Instance.OnResumeGameReceived -= OnResumeGameReceived;
            MQTTManager.Instance.OnResetGameReceived -= OnResetGameReceived;
            MQTTManager.Instance.OnGameConfigReceived -= OnGameConfigReceived;
        }
    }

    private void Update()
    {
        if (currentState == GameState.PLAYING)
        {
            // Update game time
            currentTime += Time.deltaTime;

            // NOTE: Distance and speed are now managed by SplinePlayerController
            // which syncs values back to this manager via SetDistance() and SetSpeed()

            // Track player movement for idle timeout
            if (currentSpeed > 0.1f)
            {
                lastMovementTime = Time.time;
            }
            else
            {
                // Check for idle timeout
                float idleTime = Time.time - lastMovementTime;
                if (idleTime >= idleTimeoutSeconds)
                {
                    Debug.Log($"[MainGameManager] Player idle for {idleTime:F1}s - forcing reset");
                    ResetGame();
                    return;
                }
            }

            // Check if finished
            if (currentDistance >= totalDistance)
            {
                FinishGame(true);
            }
        }
    }

    // MQTT Message Handlers
    private void OnStartGameReceived(StartGameMessage msg)
    {
        Debug.Log($"Received START_GAME command for player: {msg.playerName}, mode: {msg.gameMode}");
        StartGame(msg.playerName, msg.gameMode);
    }

    private void OnPauseGameReceived(PauseGameMessage msg)
    {
        Debug.Log("Received PAUSE_GAME command");
        PauseGame();
    }

    private void OnResumeGameReceived(ResumeGameMessage msg)
    {
        Debug.Log("Received RESUME_GAME command");
        ResumeGame();
    }

    private void OnResetGameReceived(ResetGameMessage msg)
    {
        Debug.Log("Received RESET_GAME command");
        ResetGame();
    }

    // Store received config for use
    private GameConfigMessage receivedConfig;

    private void OnGameConfigReceived(GameConfigMessage msg)
    {
        Debug.Log("[MainGameManager] Received GAME_CONFIG from server");

        if (msg.gameModes == null)
        {
            Debug.LogWarning("[MainGameManager] Game config has no game modes!");
            return;
        }

        // Store and apply config
        ApplyReceivedConfig(msg);
    }

    public void ApplyReceivedConfig(GameConfigMessage config)
    {
        receivedConfig = config;

        if (config?.gameModes == null) return;

        GameModeConfig modeConfig = null;
        switch (currentGameMode.ToLower())
        {
            case "rowing":
                modeConfig = config.gameModes.rowing;
                break;
            case "running":
                modeConfig = config.gameModes.running;
                break;
            case "cycling":
                modeConfig = config.gameModes.cycling;
                break;
        }

        if (modeConfig != null)
        {
            totalDistance = modeConfig.routeDistance;
            countdownSeconds = modeConfig.countdownSeconds;
            autoResetDelay = modeConfig.resultsDisplaySeconds;
            idleTimeoutSeconds = modeConfig.idleTimeoutSeconds;

            Debug.Log($"[MainGameManager] Config applied - Distance: {totalDistance}m, Countdown: {countdownSeconds}s, Results: {autoResetDelay}s, Idle timeout: {idleTimeoutSeconds}s");

            // Update HUD with new total distance
            if (hudController != null)
            {
                hudController.SetTotalDistance(totalDistance);
            }

            // Position finish line at the configured distance
            PositionFinishLine(totalDistance);
        }
    }

    // Game Control Methods
    public void StartGame(string player, string mode)
    {
        if (currentState != GameState.IDLE)
        {
            Debug.LogWarning("Game already in progress!");
            return;
        }

        playerName = player;
        currentGameMode = mode;
        currentDistance = 0f;
        currentSpeed = 0f;
        currentTime = 0f;
        lastMovementTime = Time.time; // Reset idle timer

        // Apply config for this game mode (updates totalDistance, positions finish line, etc.)
        if (receivedConfig != null)
        {
            ApplyReceivedConfig(receivedConfig);
        }
        else
        {
            // No config received, use default - still position finish line
            PositionFinishLine(totalDistance);
            if (hudController != null)
            {
                hudController.SetTotalDistance(totalDistance);
            }
        }

        // Update mesh generators based on game mode
        UpdateMeshGenerators(mode);

        Debug.Log($"Starting {mode} game for {player}");

        // Start countdown
        StartCoroutine(CountdownCoroutine());
    }

    public void UpdateMeshGenerators(string mode)
    {
        string modeLower = mode.ToLower();

        bool isRowing = modeLower == "rowing";
        bool isRunning = modeLower == "running";
        bool isCycling = modeLower == "cycling";

        // Enable rowing mesh generator only for rowing mode
        if (rowingMeshGenerator != null)
        {
            rowingMeshGenerator.SetActive(isRowing);
            Debug.Log($"[MainGameManager] Rowing mesh generator: {(isRowing ? "ENABLED" : "DISABLED")}");
        }

        // Enable running mesh generator only for running mode
        if (runningMeshGenerator != null)
        {
            runningMeshGenerator.SetActive(isRunning);
            Debug.Log($"[MainGameManager] Running mesh generator: {(isRunning ? "ENABLED" : "DISABLED")}");
        }

        // Enable cycling mesh generator only for cycling mode
        if (cyclingMeshGenerator != null)
        {
            cyclingMeshGenerator.SetActive(isCycling);
            Debug.Log($"[MainGameManager] Cycling mesh generator: {(isCycling ? "ENABLED" : "DISABLED")}");
        }
    }

    private IEnumerator CountdownCoroutine()
    {
        SetGameState(GameState.COUNTDOWN);

        // Send countdown messages
        for (int i = countdownSeconds; i > 0; i--)
        {
            SendCountdownMessage(i);
            yield return new WaitForSeconds(1f);
        }

        // Send GO! (0)
        SendCountdownMessage(0);
        yield return new WaitForSeconds(1f);

        // Start the game
        SetGameState(GameState.PLAYING);

        // Start spline controller
        if (splineController != null)
        {
            splineController.StartGameplay();
        }

        // Set machine handler game mode
        if (machineHandler != null)
        {
            machineHandler.SetGameMode(currentGameMode);
        }

        // Show HUD
        if (hudController != null)
        {
            hudController.ShowHUD();
            hudController.StartGame();
        }

        // Start sending game data updates
        if (gameDataCoroutine != null)
        {
            StopCoroutine(gameDataCoroutine);
        }
        gameDataCoroutine = StartCoroutine(SendGameDataCoroutine());
    }

    private IEnumerator SendGameDataCoroutine()
    {
        while (currentState == GameState.PLAYING)
        {
            SendGameDataMessage();
            yield return new WaitForSeconds(gameDataUpdateInterval);
        }
    }

    public void PauseGame()
    {
        if (currentState == GameState.PLAYING)
        {
            SetGameState(GameState.PAUSED);

            if (gameDataCoroutine != null)
            {
                StopCoroutine(gameDataCoroutine);
                gameDataCoroutine = null;
            }
        }
    }

    public void ResumeGame()
    {
        if (currentState == GameState.PAUSED)
        {
            SetGameState(GameState.PLAYING);

            if (gameDataCoroutine != null)
            {
                StopCoroutine(gameDataCoroutine);
            }
            gameDataCoroutine = StartCoroutine(SendGameDataCoroutine());
        }
    }

    public void ResetGame()
    {
        // Cancel any pending auto-reset
        if (autoResetCoroutine != null)
        {
            StopCoroutine(autoResetCoroutine);
            autoResetCoroutine = null;
        }

        if (gameDataCoroutine != null)
        {
            StopCoroutine(gameDataCoroutine);
            gameDataCoroutine = null;
        }

        // Stop and reset spline controller (resets camera/player position to start)
        if (splineController != null)
        {
            splineController.StopGameplay();
            splineController.ResetPosition();
        }

        // Hide HUD
        if (hudController != null)
        {
            hudController.HideHUD();
            hudController.StopGame();
        }

        // Hide Game Over screen
        if (gameOverController != null)
        {
            gameOverController.HideGameOver();
            Debug.Log("[MainGameManager] Hiding Game Over screen");
        }
        else
        {
            Debug.LogWarning("[MainGameManager] Cannot hide Game Over - gameOverController is NULL!");
        }

        // Show Idle screen
        if (idleController != null)
        {
            idleController.ShowIdle();
            Debug.Log("[MainGameManager] Showing Idle screen");
        }
        else
        {
            Debug.LogWarning("[MainGameManager] Cannot show Idle - idleController is NULL!");
        }

        currentDistance = 0f;
        currentSpeed = 0f;
        currentTime = 0f;
        SetGameState(GameState.IDLE);
    }

    private void FinishGame(bool completed)
    {
        SetGameState(GameState.FINISHED);

        if (gameDataCoroutine != null)
        {
            StopCoroutine(gameDataCoroutine);
            gameDataCoroutine = null;
        }

        // Stop spline controller
        if (splineController != null)
        {
            splineController.StopGameplay();
        }

        // Hide HUD
        if (hudController != null)
        {
            hudController.StopGame();
            hudController.HideHUD();
        }

        // Show Game Over screen (pass completed status for win/lose display)
        if (gameOverController != null)
        {
            gameOverController.ShowGameOver(currentTime, currentDistance, completed);
            Debug.Log($"[MainGameManager] Showing Game Over screen - Won: {completed}");
        }
        else
        {
            Debug.LogWarning("[MainGameManager] GameOverController reference is NULL!");
        }

        // Send game over message via MQTT
        SendGameOverMessage(completed);

        Debug.Log($"Game finished! Distance: {currentDistance}m, Time: {currentTime}s, Completed: {completed}");

        // Auto-reset after delay
        if (autoResetCoroutine != null)
        {
            StopCoroutine(autoResetCoroutine);
        }
        autoResetCoroutine = StartCoroutine(AutoResetCoroutine());
    }

    private IEnumerator AutoResetCoroutine()
    {
        Debug.Log($"[MainGameManager] Auto-reset in {autoResetDelay} seconds...");
        yield return new WaitForSeconds(autoResetDelay);

        autoResetCoroutine = null;
        Debug.Log("[MainGameManager] Auto-resetting game...");
        ResetGame();
    }

    // Game Mechanics
    public void PerformAction(float intensity = 1f)
    {
        if (currentState != GameState.PLAYING)
            return;

        float speedIncrease = 0f;

        switch (currentGameMode)
        {
            case "rowing":
                speedIncrease = rowingDistancePerStroke * intensity;
                break;
            case "running":
                speedIncrease = runningSpeedMultiplier * intensity;
                break;
            case "cycling":
                speedIncrease = cyclingSpeedMultiplier * intensity;
                break;
        }

        currentSpeed += speedIncrease;
        currentSpeed = Mathf.Min(currentSpeed, 20f); // Cap max speed

        Debug.Log($"{currentGameMode} action performed! Speed: {currentSpeed}m/s");
    }

    public void AddDistance(float distance)
    {
        currentDistance = Mathf.Min(currentDistance + distance, totalDistance);
    }

    // Method for SplinePlayerController to set distance directly
    public void SetDistance(float distance)
    {
        currentDistance = Mathf.Clamp(distance, 0f, totalDistance);
    }

    public void SetSpeed(float speed)
    {
        currentSpeed = speed;
    }

    public void SetTotalDistance(float distance)
    {
        totalDistance = distance;
        Debug.Log($"[MainGameManager] Total distance set to {totalDistance}m (from spline)");
    }

    /// <summary>
    /// Positions the finish line follower at the specified distance along the spline
    /// </summary>
    private void PositionFinishLine(float distance)
    {
        if (finishLineFollower == null)
        {
            Debug.LogWarning("[MainGameManager] Finish line follower not assigned!");
            return;
        }

        if (finishLineFollower.spline == null)
        {
            Debug.LogWarning("[MainGameManager] Finish line follower has no spline assigned!");
            return;
        }

        // Stop the follower from auto-moving
        finishLineFollower.follow = false;

        // Get total spline length
        double splineLength = finishLineFollower.spline.CalculateLength();

        if (splineLength <= 0)
        {
            Debug.LogError("[MainGameManager] Spline length is 0!");
            return;
        }

        // Clamp distance to spline length
        double clampedDistance = System.Math.Min(distance, splineLength);

        // Calculate percent and use SetPercent for accurate positioning
        double targetPercent = clampedDistance / splineLength;
        finishLineFollower.SetPercent(targetPercent);

        Debug.Log($"[MainGameManager] Finish line positioned at {clampedDistance}m ({targetPercent * 100:F1}%) of {splineLength:F1}m spline");

        // Clip all spline meshes to end at the finish line
        ClipSplineMeshes(targetPercent);
    }

    /// <summary>
    /// Sets the clipTo on all spline meshes to end at the specified percent
    /// </summary>
    private void ClipSplineMeshes(double clipToPercent)
    {
        if (splineMeshesToClip == null || splineMeshesToClip.Length == 0)
            return;

        foreach (var splineUser in splineMeshesToClip)
        {
            if (splineUser != null)
            {
                splineUser.clipTo = clipToPercent;
                Debug.Log($"[MainGameManager] Clipped {splineUser.name} to {clipToPercent * 100:F1}%");
            }
        }
    }

    // MQTT Sending Methods
    private void SendCountdownMessage(int value)
    {
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            var msg = new CountdownMessage
            {
                countdownValue = value
            };
            MQTTManager.Instance.PublishToStation("GAME_TO_TABLET", msg);
        }
    }

    private void SendGameDataMessage()
    {
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            var msg = new GameDataMessage
            {
                currentDistance = currentDistance,
                totalDistance = totalDistance,
                currentSpeed = currentSpeed,
                currentTime = currentTime,
                progressPercent = (currentDistance / totalDistance) * 100f
            };
            MQTTManager.Instance.PublishToStation("GAME_TO_TABLET", msg);
        }
    }

    private void SendGameOverMessage(bool completed)
    {
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            var msg = new GameOverMessage
            {
                finalDistance = currentDistance,
                finalTime = currentTime,
                completedCourse = completed
            };
            MQTTManager.Instance.PublishToStation("GAME_TO_TABLET", msg);
        }
    }

    private void SetGameState(GameState newState)
    {
        currentState = newState;

        // Send game state message
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            var msg = new GameStateMessage
            {
                state = newState.ToString()
            };
            MQTTManager.Instance.PublishToStation("GAME_STATE", msg);
        }

        Debug.Log($"Game State: {newState}");
    }

    // Public getters
    public GameState CurrentState => currentState;
    public float CurrentDistance => currentDistance;
    public float CurrentSpeed => currentSpeed;
    public float CurrentTime => currentTime;
    public string CurrentGameMode => currentGameMode;
    public string PlayerName => playerName;
}
