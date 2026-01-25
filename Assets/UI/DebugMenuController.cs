using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using MarathonMQTT;

public class DebugMenuController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private TabletController tabletController;
    [SerializeField] private TabletPlayAgainController playAgainController;
    [SerializeField] private TabletMetricsController metricsController;

    [Header("Gesture Settings")]
    [SerializeField] private int requiredFingers = 3;
    [SerializeField] private int requiredTaps = 2;
    [SerializeField] private float tapTimeWindow = 0.5f; // Time window for double tap
    [SerializeField] private float tapTimeout = 1f; // Reset counter after this time

    private VisualElement debugRoot;
    private bool isDebugMenuVisible = false;

    // MQTT UI Elements
    private TextField brokerAddressInput;
    private TextField brokerPortInput;
    private TextField stationIdInput;
    private TextField usernameInput;
    private TextField passwordInput;
    private Label connectionStatus;
    private Label discoveryStatus;
    private VisualElement discoveredBrokersList;
    private Button connectButton;
    private Button disconnectButton;
    private Button broadcastButton;
    private Button closeButton;

    // Game Mode UI Elements
    private Button rowingButton;
    private Button runningButton;
    private Button cyclingButton;
    private Label selectedModeLabel;
    private string currentGameMode = "rowing";

    // Game Control
    private Button stopGameButton;

    // Test Buttons
    private Button testStartButton;
    private Button testDataButton;
    private Button testGameOverButton;

    // Log Window Elements
    private VisualElement logContainer;
    private ScrollView logScrollView;
    private Button clearLogButton;
    private const int MAX_LOG_ENTRIES = 100;

    // Gesture detection
    private int currentTapCount = 0;
    private float lastTapTime = 0f;
    private Coroutine resetTapCoroutine;

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("UIDocument component not found!");
            return;
        }

        var root = uiDocument.rootVisualElement;
        debugRoot = root.Q<VisualElement>("DebugRoot");

        // Get MQTT UI elements
        brokerAddressInput = root.Q<TextField>("BrokerAddressInput");
        brokerPortInput = root.Q<TextField>("BrokerPortInput");
        stationIdInput = root.Q<TextField>("StationIdInput");
        usernameInput = root.Q<TextField>("UsernameInput");
        passwordInput = root.Q<TextField>("PasswordInput");
        connectionStatus = root.Q<Label>("ConnectionStatus");
        discoveryStatus = root.Q<Label>("DiscoveryStatus");
        discoveredBrokersList = root.Q<VisualElement>("DiscoveredBrokersList");
        connectButton = root.Q<Button>("ConnectButton");
        disconnectButton = root.Q<Button>("DisconnectButton");
        closeButton = root.Q<Button>("CloseButton");

        // Get Game Mode UI elements
        rowingButton = root.Q<Button>("RowingButton");
        runningButton = root.Q<Button>("RunningButton");
        cyclingButton = root.Q<Button>("CyclingButton");
        selectedModeLabel = root.Q<Label>("SelectedModeLabel");

        // Get Game Control buttons
        stopGameButton = root.Q<Button>("StopGameButton");

        // Get Test buttons
        testStartButton = root.Q<Button>("TestStartButton");
        testDataButton = root.Q<Button>("TestDataButton");
        testGameOverButton = root.Q<Button>("TestGameOverButton");

        // Get Log window elements
        logContainer = root.Q<VisualElement>("LogContainer");
        logScrollView = root.Q<ScrollView>("LogScrollView");
        clearLogButton = root.Q<Button>("ClearLogButton");

        // Register button callbacks
        if (connectButton != null) connectButton.clicked += OnConnectClicked;
        if (disconnectButton != null) disconnectButton.clicked += OnDisconnectClicked;
        if (closeButton != null) closeButton.clicked += CloseDebugMenu;

        if (rowingButton != null) rowingButton.clicked += () => SelectGameMode("rowing");
        if (runningButton != null) runningButton.clicked += () => SelectGameMode("running");
        if (cyclingButton != null) cyclingButton.clicked += () => SelectGameMode("cycling");

        if (stopGameButton != null) stopGameButton.clicked += OnStopGameClicked;

        if (testStartButton != null) testStartButton.clicked += OnTestStartGame;
        if (testDataButton != null) testDataButton.clicked += OnTestGameData;
        if (testGameOverButton != null) testGameOverButton.clicked += OnTestGameOver;

        if (clearLogButton != null) clearLogButton.clicked += ClearLogs;

        // Register Unity log callback
        Application.logMessageReceived += OnLogMessageReceived;

        // Subscribe to MQTT events
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnConnected += OnMQTTConnected;
            MQTTManager.Instance.OnDisconnected += OnMQTTDisconnected;
            MQTTManager.Instance.OnConnectionFailed += OnMQTTConnectionFailed;
        }

        // Initialize UI
        LoadSettings();
        UpdateConnectionStatus();
        HideDebugMenu();
    }

    private void OnDisable()
    {
        // Unregister callbacks
        if (connectButton != null) connectButton.clicked -= OnConnectClicked;
        if (disconnectButton != null) disconnectButton.clicked -= OnDisconnectClicked;
        if (closeButton != null) closeButton.clicked -= CloseDebugMenu;
        if (clearLogButton != null) clearLogButton.clicked -= ClearLogs;

        // Unregister Unity log callback
        Application.logMessageReceived -= OnLogMessageReceived;

        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnConnected -= OnMQTTConnected;
            MQTTManager.Instance.OnDisconnected -= OnMQTTDisconnected;
            MQTTManager.Instance.OnConnectionFailed -= OnMQTTConnectionFailed;
        }
    }

    private void Update()
    {
        DetectGesture();
    }

    private void DetectGesture()
    {
        // Check for multi-finger tap
        if (Input.touchCount == requiredFingers)
        {
            bool allTouchesBegan = true;
            foreach (Touch touch in Input.touches)
            {
                if (touch.phase != TouchPhase.Began)
                {
                    allTouchesBegan = false;
                    break;
                }
            }

            if (allTouchesBegan)
            {
                OnMultiFingerTap();
            }
        }

        // Fallback for testing in editor (Shift + D key)
        if (Input.GetKeyDown(KeyCode.D) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            OnMultiFingerTap();
        }
    }

    private void OnMultiFingerTap()
    {
        float currentTime = Time.time;

        // Check if this tap is within the time window
        if (currentTime - lastTapTime <= tapTimeWindow)
        {
            currentTapCount++;
        }
        else
        {
            // Reset if too much time has passed
            currentTapCount = 1;
        }

        lastTapTime = currentTime;

        Debug.Log($"Multi-finger tap detected! Count: {currentTapCount}/{requiredTaps}");

        // Check if we've reached the required number of taps
        if (currentTapCount >= requiredTaps)
        {
            ToggleDebugMenu();
            currentTapCount = 0;
        }

        // Reset tap count after timeout
        if (resetTapCoroutine != null)
        {
            StopCoroutine(resetTapCoroutine);
        }
        resetTapCoroutine = StartCoroutine(ResetTapCountAfterDelay());
    }

    private IEnumerator ResetTapCountAfterDelay()
    {
        yield return new WaitForSeconds(tapTimeout);
        currentTapCount = 0;
    }

    public void ShowDebugMenu()
    {
        if (debugRoot != null)
        {
            debugRoot.style.display = DisplayStyle.Flex;
            isDebugMenuVisible = true;
            LoadSettings();
            UpdateConnectionStatus();
        }
    }

    public void HideDebugMenu()
    {
        if (debugRoot != null)
        {
            debugRoot.style.display = DisplayStyle.None;
            isDebugMenuVisible = false;
        }
    }

    public void ToggleDebugMenu()
    {
        if (isDebugMenuVisible)
        {
            HideDebugMenu();
        }
        else
        {
            ShowDebugMenu();
        }
    }

    public void CloseDebugMenu()
    {
        SaveSettings();
        HideDebugMenu();
    }

    private void OnConnectClicked()
    {
        SaveSettings();

        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.SetBrokerAddress(brokerAddressInput.value);

            if (int.TryParse(brokerPortInput.value, out int port))
            {
                MQTTManager.Instance.SetBrokerPort(port);
            }

            if (int.TryParse(stationIdInput.value, out int stationId))
            {
                MQTTManager.Instance.SetStationId(stationId);
            }

            MQTTManager.Instance.SetCredentials(usernameInput.value, passwordInput.value);
            MQTTManager.Instance.Connect();
        }
    }

    private void OnDisconnectClicked()
    {
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.Disconnect();
        }
    }

    private void OnMQTTConnected()
    {
        UpdateConnectionStatus();
    }

    private void OnMQTTDisconnected()
    {
        UpdateConnectionStatus();
    }

    private void OnMQTTConnectionFailed(string error)
    {
        UpdateConnectionStatus();
        Debug.LogError($"MQTT Connection failed: {error}");
    }

    private void UpdateConnectionStatus()
    {
        if (connectionStatus == null || MQTTManager.Instance == null)
            return;

        if (MQTTManager.Instance.IsConnected)
        {
            connectionStatus.text = "Connected";
            connectionStatus.style.color = new StyleColor(new Color(0.2f, 0.8f, 0.2f));
        }
        else
        {
            connectionStatus.text = "Disconnected";
            connectionStatus.style.color = new StyleColor(new Color(0.8f, 0.4f, 0.4f));
        }
    }

    private void SelectGameMode(string mode)
    {
        currentGameMode = mode;
        selectedModeLabel.text = mode.ToUpper();

        // Update button styles
        UpdateGameModeButtonStyles();

        // Update TabletController content
        if (tabletController != null)
        {
            tabletController.SetGameMode(mode);
        }

        // Update TabletPlayAgainController content
        if (playAgainController != null)
        {
            playAgainController.SetGameMode(mode);
        }

        // Update TabletMetricsController content
        if (metricsController != null)
        {
            metricsController.SetGameMode(mode);
        }

        // Send game mode to Game PC via MQTT
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            var gameModeMessage = new GameModeMessage { gameMode = mode };
            MQTTManager.Instance.PublishToStation("TABLET_TO_GAME", gameModeMessage);
            Debug.Log($"[Debug Menu] Sent GAME_MODE message to Game PC: {mode}");
        }

        // Save settings when game mode changes
        SaveSettings();

        Debug.Log($"Game mode selected: {mode}");
    }

    private void UpdateGameModeButtonStyles()
    {
        // Reset all buttons
        rowingButton.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        rowingButton.style.borderLeftColor = rowingButton.style.borderRightColor = rowingButton.style.borderTopColor = rowingButton.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        runningButton.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        runningButton.style.borderLeftColor = runningButton.style.borderRightColor = runningButton.style.borderTopColor = runningButton.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        cyclingButton.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        cyclingButton.style.borderLeftColor = cyclingButton.style.borderRightColor = cyclingButton.style.borderTopColor = cyclingButton.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));

        // Highlight selected button
        Button selectedButton = currentGameMode switch
        {
            "rowing" => rowingButton,
            "running" => runningButton,
            "cycling" => cyclingButton,
            _ => rowingButton
        };

        selectedButton.style.backgroundColor = new StyleColor(new Color(1f, 0.55f, 0f));
        selectedButton.style.borderLeftColor = selectedButton.style.borderRightColor = selectedButton.style.borderTopColor = selectedButton.style.borderBottomColor = new StyleColor(new Color(1f, 0.78f, 0f));
    }

    private void LoadSettings()
    {
        // Check if UI elements are valid
        if (brokerAddressInput == null || brokerPortInput == null || stationIdInput == null)
        {
            Debug.LogWarning("[Debug Menu] UI elements not found yet, skipping LoadSettings");
            return;
        }

        // Load from MQTTManager if available (shows CURRENT settings)
        if (MQTTManager.Instance != null)
        {
            brokerAddressInput.value = MQTTManager.Instance.BrokerAddress;
            brokerPortInput.value = MQTTManager.Instance.BrokerPort.ToString();
            stationIdInput.value = MQTTManager.Instance.StationId.ToString();

            Debug.Log($"[Debug Menu] Loaded current settings - Broker: {MQTTManager.Instance.BrokerAddress}:{MQTTManager.Instance.BrokerPort}, Station ID: {MQTTManager.Instance.StationId}");
        }
        else
        {
            // Fallback to PlayerPrefs if MQTTManager not found
            brokerAddressInput.value = PlayerPrefs.GetString("MQTT_BrokerAddress", "localhost");
            brokerPortInput.value = PlayerPrefs.GetInt("MQTT_BrokerPort", 1883).ToString();
            stationIdInput.value = PlayerPrefs.GetInt("MQTT_StationId", 1).ToString();

            Debug.Log("[Debug Menu] MQTTManager not found, loaded from PlayerPrefs");
        }

        // Load other settings from PlayerPrefs
        if (usernameInput != null) usernameInput.value = PlayerPrefs.GetString("MQTT_Username", "");
        if (passwordInput != null) passwordInput.value = PlayerPrefs.GetString("MQTT_Password", "");

        currentGameMode = PlayerPrefs.GetString("GameMode", "rowing");
        if (selectedModeLabel != null) selectedModeLabel.text = currentGameMode.ToUpper();

        UpdateGameModeButtonStyles();

        // Update TabletController content with loaded game mode
        if (tabletController != null)
        {
            tabletController.SetGameMode(currentGameMode);
        }

        // Update TabletPlayAgainController content with loaded game mode
        if (playAgainController != null)
        {
            playAgainController.SetGameMode(currentGameMode);
        }

        // Update TabletMetricsController content with loaded game mode
        if (metricsController != null)
        {
            metricsController.SetGameMode(currentGameMode);
        }
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetString("MQTT_BrokerAddress", brokerAddressInput.value);
        if (int.TryParse(brokerPortInput.value, out int port))
        {
            PlayerPrefs.SetInt("MQTT_BrokerPort", port);
        }
        if (int.TryParse(stationIdInput.value, out int stationId))
        {
            PlayerPrefs.SetInt("MQTT_StationId", stationId);
        }
        PlayerPrefs.SetString("MQTT_Username", usernameInput.value);
        PlayerPrefs.SetString("MQTT_Password", passwordInput.value);
        PlayerPrefs.SetString("GameMode", currentGameMode);
        PlayerPrefs.Save();
    }

    // Game Control methods
    private void OnStopGameClicked()
    {
        Debug.Log("[Debug Menu] Stop Game button clicked - sending RESET_GAME");

        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            var resetMessage = new ResetGameMessage();
            MQTTManager.Instance.PublishToStation("TABLET_TO_GAME", resetMessage);
            Debug.Log("[Debug Menu] Sent RESET_GAME command to Game PC");
        }
        else
        {
            Debug.LogWarning("[Debug Menu] MQTT not connected - cannot send reset command");
        }
    }

    // Test command methods
    private void OnTestStartGame()
    {
        if (MQTTManager.Instance != null)
        {
            var msg = new StartGameMessage
            {
                playerName = "Test Player",
                gameMode = currentGameMode
            };
            MQTTManager.Instance.PublishToStation("TABLET_TO_GAME", msg);
            Debug.Log($"[Debug Menu] Sent test START_GAME message to station {MQTTManager.Instance.StationId}");
        }
    }

    private void OnTestGameData()
    {
        if (MQTTManager.Instance != null)
        {
            var msg = new GameDataMessage
            {
                currentDistance = 500f,
                currentSpeed = 5f,
                currentTime = 60f,
                progressPercent = 50f
            };
            MQTTManager.Instance.PublishToStation("GAME_TO_TABLET", msg);
            Debug.Log($"[Debug Menu] Sent test GAME_DATA message to station {MQTTManager.Instance.StationId}");
        }
    }

    private void OnTestGameOver()
    {
        if (MQTTManager.Instance != null)
        {
            var msg = new GameOverMessage
            {
                finalDistance = 1600f,
                finalTime = 300f,
                completedCourse = true
            };
            MQTTManager.Instance.PublishToStation("GAME_TO_TABLET", msg);
            Debug.Log($"[Debug Menu] Sent test GAME_OVER message to station {MQTTManager.Instance.StationId}");
        }
    }

    public string GetCurrentGameMode() => currentGameMode;

    // Log Window Methods
    private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        if (logContainer == null)
            return;

        // Create log entry
        var logEntry = new Label();
        logEntry.style.fontSize = 11;
        logEntry.style.marginBottom = 2;
        logEntry.style.whiteSpace = WhiteSpace.Normal;
        logEntry.style.color = GetLogColor(type);

        // Format log message with timestamp
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        string typePrefix = GetLogTypePrefix(type);
        logEntry.text = $"[{timestamp}] {typePrefix} {logString}";

        // Add to container
        logContainer.Add(logEntry);

        // Limit number of log entries
        if (logContainer.childCount > MAX_LOG_ENTRIES)
        {
            logContainer.RemoveAt(0);
        }

        // Auto-scroll to bottom
        if (logScrollView != null)
        {
            logScrollView.scrollOffset = new UnityEngine.Vector2(0, logScrollView.contentContainer.layout.height);
        }
    }

    private StyleColor GetLogColor(LogType type)
    {
        switch (type)
        {
            case LogType.Error:
            case LogType.Exception:
                return new StyleColor(new Color(1f, 0.4f, 0.4f));
            case LogType.Warning:
                return new StyleColor(new Color(1f, 0.8f, 0.3f));
            case LogType.Log:
                return new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            default:
                return new StyleColor(new Color(0.6f, 0.6f, 0.6f));
        }
    }

    private string GetLogTypePrefix(LogType type)
    {
        switch (type)
        {
            case LogType.Error:
                return "[ERROR]";
            case LogType.Exception:
                return "[EXCEPTION]";
            case LogType.Warning:
                return "[WARN]";
            case LogType.Log:
                return "[INFO]";
            default:
                return "[LOG]";
        }
    }

    private void ClearLogs()
    {
        if (logContainer != null)
        {
            logContainer.Clear();
            Debug.Log("[Debug Menu] Logs cleared");
        }
    }
}
