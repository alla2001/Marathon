using UnityEngine;
using UnityEngine.UIElements;
using MarathonMQTT;

public class TabletMetricsController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    [Header("Game Settings")]
    [SerializeField] private float totalTime = 300f; // 5 minutes default
    [SerializeField] private float maxDistance = 1600f; // Total distance

    [Header("Game Mode Landing Images")]
    [SerializeField] private Texture2D rowingLandingImage;
    [SerializeField] private Texture2D runningLandingImage;
    [SerializeField] private Texture2D cyclingLandingImage;

    private Label gameModeTitle;
    private Image landingImage;
    private Label distanceValue;
    private Label timeValue;
    private VisualElement speedometerNeedle;
    private VisualElement progressFill;
    private VisualElement distanceMarkers;

    private float currentDistance = 0f;
    private float currentTime = 0f;
    private float timeRemaining = 300f;
    private string currentGameMode = "ROWING";

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

        // Get UI elements
        gameModeTitle = root.Q<Label>("GameModeTitle");
        distanceValue = root.Q<Label>("DistanceValue");
        timeValue = root.Q<Label>("TimeValue");
        speedometerNeedle = root.Q<VisualElement>("SpeedometerNeedle");
        progressFill = root.Q<VisualElement>("ProgressFill");
        distanceMarkers = root.Q<VisualElement>("DistanceMarkers");
        landingImage = root.Q<Image>("Landing-Image");

        // Subscribe to MQTT game data messages
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnGameDataReceived += OnGameDataReceived;
            MQTTManager.Instance.OnCountdownReceived += OnCountdownReceived;
            MQTTManager.Instance.OnGameStateReceived += OnGameStateReceived;
        }

        // Initialize display
        UpdateDisplay();

        // Load saved game mode and apply it
        string savedGameMode = PlayerPrefs.GetString("GameMode", "rowing");
        SetGameMode(savedGameMode);
        Debug.Log($"[Tablet Metrics] Loaded game mode from settings: {savedGameMode}");

        // Hide initially (show when game starts)
        HideMetrics();
    }

    private void OnDisable()
    {
        // Unsubscribe from MQTT events
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnGameDataReceived -= OnGameDataReceived;
            MQTTManager.Instance.OnCountdownReceived -= OnCountdownReceived;
            MQTTManager.Instance.OnGameStateReceived -= OnGameStateReceived;
        }
    }

    private void OnGameDataReceived(GameDataMessage msg)
    {
        // Update metrics from game data
        currentDistance = msg.currentDistance;
        currentTime = msg.currentTime;
        timeRemaining = totalTime - currentTime;

        // Update max distance from message if provided
        if (msg.totalDistance > 0 && msg.totalDistance != maxDistance)
        {
            maxDistance = msg.totalDistance;
            UpdateDistanceMarkers();
        }

        Debug.Log($"[Tablet Metrics] Received GAME_DATA - Distance: {currentDistance:F2}m/{maxDistance:F2}m, Time: {currentTime:F2}s, Remaining: {timeRemaining:F2}s");

        UpdateDisplay();
    }

    private void OnCountdownReceived(CountdownMessage msg)
    {
        // During countdown, you might want to show countdown overlay
        Debug.Log($"[Tablet Metrics] Countdown: {msg.countdownValue}");

        if (msg.countdownValue == 0)
        {
            // Game started, show metrics
            Debug.Log("[Tablet Metrics] Countdown finished (GO!), showing metrics...");
            ShowMetrics();
        }
    }

    private void OnGameStateReceived(GameStateMessage msg)
    {
        Debug.Log($"[Tablet Metrics] Game State: {msg.state}");

        switch (msg.state)
        {
            case "COUNTDOWN":
                // Prepare to show metrics
                Debug.Log("[Tablet Metrics] Game state COUNTDOWN");
                break;
            case "PLAYING":
                Debug.Log("[Tablet Metrics] Game state PLAYING, showing metrics...");
                ShowMetrics();
                break;
            case "FINISHED":
                Debug.Log("[Tablet Metrics] Game state FINISHED, hiding metrics...");
                HideMetrics();
                break;
        }
    }

    private void UpdateDisplay()
    {
        // Update distance
        if (distanceValue != null)
        {
            distanceValue.text = $"{Mathf.RoundToInt(currentDistance)} m";
        }

        // Update time remaining
        if (timeValue != null)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            timeValue.text = $"{minutes:00}:{seconds:00}";
        }

        // Update progress bar
        UpdateProgressBar();

        // Update speedometer needle rotation (if you add a needle visual element)
        UpdateSpeedometerNeedle();
    }

    private void UpdateProgressBar()
    {
        if (progressFill != null)
        {
            float progressPercent = Mathf.Clamp01(currentDistance / maxDistance) * 100f;
            progressFill.style.width = Length.Percent(progressPercent);
        }
    }

    private void UpdateDistanceMarkers()
    {
        if (distanceMarkers == null) return;

        var labels = distanceMarkers.Query<Label>().ToList();
        if (labels.Count == 0) return;

        int markerCount = labels.Count;
        for (int i = 0; i < markerCount; i++)
        {
            // Calculate distance for this marker (evenly spaced from 0 to maxDistance)
            float markerDistance = (maxDistance / (markerCount - 1)) * i;
            labels[i].text = $"{Mathf.RoundToInt(markerDistance)}m";
        }

        Debug.Log($"[Tablet Metrics] Updated {markerCount} distance markers for {maxDistance}m total");
    }

    private void UpdateSpeedometerNeedle()
    {
        if (speedometerNeedle != null)
        {
            // Calculate rotation based on distance (0 to maxDistance)
            // Speedometer typically goes from -90 degrees to +90 degrees (180 degree arc)
            float percentage = Mathf.Clamp01(currentDistance / maxDistance);
            float rotation = -90f + (percentage * 180f); // -90 to +90 degrees

            speedometerNeedle.style.rotate = new Rotate(new Angle(rotation, AngleUnit.Degree));
        }
    }

    public void ShowMetrics()
    {
        var root = uiDocument.rootVisualElement;
        if (root != null)
        {
            root.style.display = DisplayStyle.Flex;
            Debug.Log("[Tablet Metrics] Metrics screen SHOWN");
        }
    }

    public void HideMetrics()
    {
        var root = uiDocument.rootVisualElement;
        if (root != null)
        {
            root.style.display = DisplayStyle.None;
            Debug.Log("[Tablet Metrics] Metrics screen HIDDEN");
        }
    }

    public void SetGameMode(string mode)
    {
        currentGameMode = mode.ToUpper();
        if (gameModeTitle != null)
        {
            gameModeTitle.text = currentGameMode;
        }

        // Update landing image based on game mode
        UpdateLandingImage();
    }

    private void UpdateLandingImage()
    {
        if (landingImage == null) return;

        Texture2D newImage = null;
        switch (currentGameMode.ToLower())
        {
            case "rowing":
                newImage = rowingLandingImage;
                break;
            case "running":
                newImage = runningLandingImage;
                break;
            case "cycling":
                newImage = cyclingLandingImage;
                break;
            default:
                newImage = rowingLandingImage;
                break;
        }

        if (newImage != null)
        {
            landingImage.image = newImage;
            Debug.Log($"[Tablet Metrics] Updated landing image for mode: {currentGameMode}");
        }
    }

    public void SetTotalTime(float time)
    {
        totalTime = time;
    }

    // Manual update methods (if needed for testing)
    public void SetDistance(float distance)
    {
        currentDistance = distance;
        UpdateDisplay();
    }

    public void SetTime(float time)
    {
        currentTime = time;
        timeRemaining = totalTime - currentTime;
        UpdateDisplay();
    }
}
