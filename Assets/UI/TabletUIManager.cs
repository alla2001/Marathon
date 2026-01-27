using UnityEngine;
using MarathonMQTT;

/// <summary>
/// Manages all UI screens on the Tablet
/// Handles transitions between: Start -> Metrics -> PlayAgain
/// </summary>
public class TabletUIManager : MonoBehaviour
{
    public static TabletUIManager Instance { get; private set; }

    [Header("UI Controllers")]
    [SerializeField] private TabletController startScreen;
    [SerializeField] private TabletMetricsController metricsScreen;
    [SerializeField] private TabletPlayAgainController playAgainScreen;
    [SerializeField] private DebugMenuController debugMenu;

    [Header("Current State")]
    private TabletScreenState currentState = TabletScreenState.START;

    public enum TabletScreenState
    {
        START,      // Tablet.uxml - name input, start button
        METRICS,    // TabletMetrics.uxml - during gameplay
        PLAY_AGAIN  // TabletPlayAgain.uxml - after game over
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Auto-find controllers if not assigned
        if (startScreen == null) startScreen = FindObjectOfType<TabletController>();
        if (metricsScreen == null) metricsScreen = FindObjectOfType<TabletMetricsController>();
        if (playAgainScreen == null) playAgainScreen = FindObjectOfType<TabletPlayAgainController>();
        if (debugMenu == null) debugMenu = FindObjectOfType<DebugMenuController>();

        // Subscribe to MQTT events
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnCountdownReceived += OnCountdownReceived;
            MQTTManager.Instance.OnGameDataReceived += OnGameDataReceived;
            MQTTManager.Instance.OnGameOverReceived += OnGameOverReceived;
            MQTTManager.Instance.OnGameStateReceived += OnGameStateReceived;
        }

        // Show start screen initially
        ShowStartScreen();
    }

    private void OnDestroy()
    {
        // Unsubscribe from MQTT events
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnCountdownReceived -= OnCountdownReceived;
            MQTTManager.Instance.OnGameDataReceived -= OnGameDataReceived;
            MQTTManager.Instance.OnGameOverReceived -= OnGameOverReceived;
            MQTTManager.Instance.OnGameStateReceived -= OnGameStateReceived;
        }
    }

    // MQTT Event Handlers
    private void OnCountdownReceived(CountdownMessage msg)
    {
        Debug.Log($"[TabletUI] Countdown: {msg.countdownValue}");

        if (msg.countdownValue == 0)
        {
            // Game starting - show metrics
            ShowMetricsScreen();
        }
    }

    private void OnGameDataReceived(GameDataMessage msg)
    {
        // Make sure we're showing metrics screen during gameplay
        if (currentState != TabletScreenState.METRICS)
        {
            ShowMetricsScreen();
        }
    }

    private void OnGameOverReceived(GameOverMessage msg)
    {
        Debug.Log("[TabletUI] Game Over - showing play again screen");
        ShowPlayAgainScreen();
    }

    private void OnGameStateReceived(GameStateMessage msg)
    {
        Debug.Log($"[TabletUI] Game State: {msg.state}");

        switch (msg.state)
        {
            case "IDLE":
                ShowStartScreen();
                break;
            case "COUNTDOWN":
                // Stay on current screen, metrics will show when countdown hits 0
                break;
            case "PLAYING":
                ShowMetricsScreen();
                break;
            case "FINISHED":
                ShowPlayAgainScreen();
                break;
        }
    }

    // Screen Management Methods
    public void ShowStartScreen()
    {
        Debug.Log("[TabletUI] Showing START screen");
        currentState = TabletScreenState.START;

        HideAllScreens();

        if (startScreen != null)
        {
            // Start screen is always visible in its UIDocument
            // Just make sure others are hidden
        }
    }

    public void ShowMetricsScreen()
    {
        Debug.Log("[TabletUI] Showing METRICS screen");
        currentState = TabletScreenState.METRICS;

        SyncLanguage();
        HideAllScreens();

        if (metricsScreen != null)
        {
            metricsScreen.ShowMetrics();
        }
    }

    public void ShowPlayAgainScreen()
    {
        Debug.Log("[TabletUI] Showing PLAY AGAIN screen");
        currentState = TabletScreenState.PLAY_AGAIN;

        SyncLanguage();
        HideAllScreens();

        if (playAgainScreen != null)
        {
            playAgainScreen.ShowPlayAgain();
        }
    }

    private void HideAllScreens()
    {
        // Hide all screens
        if (metricsScreen != null) metricsScreen.HideMetrics();
        if (playAgainScreen != null) playAgainScreen.HidePlayAgain();

        // Note: Start screen (Tablet.uxml) visibility is managed by TabletController
        // since it's the default state
    }

    public void OnGameStarted()
    {
        // Called when player clicks "Start Rowing!" button
        // Countdown will be received via MQTT, then metrics shown
        Debug.Log("[TabletUI] Game started - waiting for countdown");
    }

    public void OnPlayAgain()
    {
        // Called when player clicks "Play Again"
        ShowStartScreen();
    }

    public void OnGoHome()
    {
        // Called when player clicks "Go Home"
        ShowStartScreen();
    }

    /// <summary>
    /// Syncs language state from start screen to all other screens
    /// Called when transitioning screens to keep language consistent
    /// </summary>
    private void SyncLanguage()
    {
        if (startScreen == null) return;

        bool isArabic = startScreen.IsArabic;

        if (metricsScreen != null) metricsScreen.SetLanguage(isArabic);
        if (playAgainScreen != null) playAgainScreen.SetLanguage(isArabic);
    }

    // Public getters
    public TabletScreenState CurrentState => currentState;
}
