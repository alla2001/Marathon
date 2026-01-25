using UnityEngine;
using UnityEngine.UIElements;
using MarathonMQTT;
using System.Collections;

public class TabletController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private DebugMenuController debugMenu;
    [SerializeField] private Texture2D buttonActiveImage;

    [Header("Username Check Settings")]
    [SerializeField] private float usernameCheckDelay = 0.5f; // Debounce delay

    [Header("Rowing Mode Content")]
    [SerializeField] private Texture2D rowingLandingImage;
    [SerializeField] private string rowingDescriptionArabic = "جدّف عبر مسار افتراضي مستوحى من معالم الرياض";
    [SerializeField] private string rowingDescriptionEnglish = "Row through a virtual course inspired by Riyadh landmarks.";
    [SerializeField] private string rowingButtonText = "Start Rowing!";

    [Header("Running Mode Content")]
    [SerializeField] private Texture2D runningLandingImage;
    [SerializeField] private string runningDescriptionArabic = "اركض عبر مسار افتراضي مستوحى من معالم الرياض";
    [SerializeField] private string runningDescriptionEnglish = "Run through a virtual course inspired by Riyadh landmarks.";
    [SerializeField] private string runningButtonText = "Start Running!";

    [Header("Cycling Mode Content")]
    [SerializeField] private Texture2D cyclingLandingImage;
    [SerializeField] private string cyclingDescriptionArabic = "تسابق بدراجتك عبر مسار افتراضي مستوحى من معالم الرياض";
    [SerializeField] private string cyclingDescriptionEnglish = "Cycle through a virtual course inspired by Riyadh landmarks.";
    [SerializeField] private string cyclingButtonText = "Start Cycling!";

    private Button startButton;
    private Button languageButton;
    private TextField nameInput;
    private Toggle termsToggle;
    private Image landingImage;
    private Label descriptionLabel;

    private bool isGameActive = false;
    private StyleBackground originalButtonImage;
    private string currentGameMode = "rowing";

    // Username uniqueness tracking
    private bool isUsernameUnique = false;
    private bool isCheckingUsername = false;
    private string lastCheckedUsername = "";
    private Coroutine usernameCheckCoroutine;

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
        startButton = root.Q<Button>("StartButton");
        languageButton = root.Q<Button>("LanguageButton");
        nameInput = root.Q<TextField>("NameInput");
        termsToggle = root.Q<Toggle>("TermsToggle");
        landingImage = root.Q<Image>("Landing-Image");
        descriptionLabel = root.Q<Label>("DescriptionLine1");

        // Register button callbacks
        if (startButton != null)
        {
            // Store the original button image for later restoration
            originalButtonImage = startButton.style.backgroundImage;
            startButton.clicked += OnStartButtonClicked;
        }
        else
        {
            Debug.LogError("StartButton not found in UI!");
        }

        if (languageButton != null)
        {
            languageButton.clicked += OnLanguageButtonClicked;
        }

        // Register toggle callback
        if (termsToggle != null)
        {
            termsToggle.RegisterValueChangedCallback(evt => UpdateStartButtonState());
        }

        // Clear the default "Name" placeholder when clicked and track changes
        if (nameInput != null)
        {
            // Style the TextField to match the design
            StyleTextField(nameInput);

            nameInput.RegisterCallback<FocusInEvent>(evt =>
            {
                if (nameInput.value == "Name")
                {
                    nameInput.value = "";
                }
            });

            // Track name input changes - trigger username check with debounce
            nameInput.RegisterValueChangedCallback(evt => OnUsernameChanged(evt.newValue));
        }

        // Set initial button state (disabled)
        UpdateStartButtonState();

        // Subscribe to MQTT events
        if (MQTTManager.Instance != null)
        {
            // Register for connection event to subscribe when connected
            MQTTManager.Instance.OnConnected += OnMQTTConnected;

            // Register message handlers
            MQTTManager.Instance.OnCountdownReceived += OnCountdownReceived;
            MQTTManager.Instance.OnGameDataReceived += OnGameDataReceived;
            MQTTManager.Instance.OnGameOverReceived += OnGameOverReceived;
            MQTTManager.Instance.OnGameStateReceived += OnGameStateReceived;

            // If already connected, subscribe now
            if (MQTTManager.Instance.IsConnected)
            {
                SubscribeToTopics();
            }
        }
    }

    private void Start()
    {
        // Subscribe to LeaderboardManager events (in Start to ensure it's initialized)
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.OnUsernameCheckResult += OnUsernameCheckResult;
            Debug.Log("[TabletController] Subscribed to LeaderboardManager");
        }
        else
        {
            Debug.LogWarning("[TabletController] LeaderboardManager not found - username checks will assume unique");
        }

        // Load saved game mode and apply it
        string savedGameMode = PlayerPrefs.GetString("GameMode", "rowing");
        SetGameMode(savedGameMode);
        Debug.Log($"[TabletController] Loaded game mode from settings: {savedGameMode}");
    }

    private void OnDisable()
    {
        if (startButton != null)
        {
            startButton.clicked -= OnStartButtonClicked;
        }

        if (languageButton != null)
        {
            languageButton.clicked -= OnLanguageButtonClicked;
        }

        // Unsubscribe from LeaderboardManager events
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.OnUsernameCheckResult -= OnUsernameCheckResult;
        }

        // Unsubscribe from MQTT events
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnConnected -= OnMQTTConnected;
            MQTTManager.Instance.OnCountdownReceived -= OnCountdownReceived;
            MQTTManager.Instance.OnGameDataReceived -= OnGameDataReceived;
            MQTTManager.Instance.OnGameOverReceived -= OnGameOverReceived;
            MQTTManager.Instance.OnGameStateReceived -= OnGameStateReceived;
        }

        // Stop any pending username check
        if (usernameCheckCoroutine != null)
        {
            StopCoroutine(usernameCheckCoroutine);
        }
    }

    private void OnMQTTConnected()
    {
        Debug.Log("MQTT Connected! Subscribing to topics...");
        SubscribeToTopics();
    }

    private void SubscribeToTopics()
    {
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            MQTTManager.Instance.SubscribeToStation("GAME_TO_TABLET");
            MQTTManager.Instance.SubscribeToStation("GAME_STATE");
            Debug.Log($"[TabletController] Subscribed to station {MQTTManager.Instance.StationId} topics");
        }
    }

    private void OnStartButtonClicked()
    {
        // Only proceed if button is enabled (conditions met)
        if (!IsStartButtonEnabled())
        {
            Debug.Log("[TabletController] Start button clicked but conditions not met");
            return;
        }

        string playerName = nameInput?.value ?? "Player";

        Debug.Log($"Starting rowing game with player name: {playerName}");

        // Add your game start logic here
        StartRowingGame(playerName);
    }

    private bool IsStartButtonEnabled()
    {
        // Check if toggle is checked
        bool toggleChecked = termsToggle != null && termsToggle.value;

        // Check if name is valid (not empty, not placeholder, has actual content)
        string name = nameInput?.value ?? "";
        bool hasValidName = !string.IsNullOrWhiteSpace(name) && name != "Name" && name.Length > 0;

        // Check if username is unique (and not currently checking)
        bool usernameValid = hasValidName && isUsernameUnique && !isCheckingUsername;

        return toggleChecked && usernameValid;
    }

    // ========================================
    // Username Checking
    // ========================================
    private void OnUsernameChanged(string newUsername)
    {
        // Reset uniqueness when username changes
        if (newUsername != lastCheckedUsername)
        {
            isUsernameUnique = false;
            isCheckingUsername = true;
        }

        UpdateStartButtonState();

        // Cancel previous check
        if (usernameCheckCoroutine != null)
        {
            StopCoroutine(usernameCheckCoroutine);
        }

        // Start debounced check
        if (!string.IsNullOrWhiteSpace(newUsername) && newUsername != "Name")
        {
            usernameCheckCoroutine = StartCoroutine(DebouncedUsernameCheck(newUsername));
        }
        else
        {
            isCheckingUsername = false;
            isUsernameUnique = false;
            UpdateStartButtonState();
        }
    }

    private IEnumerator DebouncedUsernameCheck(string username)
    {
        // Wait for user to stop typing
        yield return new WaitForSeconds(usernameCheckDelay);

        // Check with LeaderboardManager
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.CheckUsername(username);
            Debug.Log($"[TabletController] Checking username: {username}");
        }
        else
        {
            // No LeaderboardManager, assume unique
            Debug.Log($"[TabletController] LeaderboardManager not available, assuming username '{username}' is unique");
            isUsernameUnique = true;
            isCheckingUsername = false;
            lastCheckedUsername = username;
            UpdateStartButtonState();
        }
    }

    private void OnUsernameCheckResult(bool isUnique, string username)
    {
        // Only process if this is for the current username
        string currentUsername = nameInput?.value ?? "";
        if (username != currentUsername)
        {
            Debug.Log($"[TabletController] Ignoring stale username check result for '{username}'");
            return;
        }

        isUsernameUnique = isUnique;
        isCheckingUsername = false;
        lastCheckedUsername = username;

        Debug.Log($"[TabletController] Username '{username}' is {(isUnique ? "UNIQUE" : "TAKEN")}");

        UpdateStartButtonState();
    }

    private void UpdateStartButtonState()
    {
        if (startButton == null) return;

        bool enabled = IsStartButtonEnabled();

        if (enabled)
        {
            // Set to active/enabled state
            if (buttonActiveImage != null)
            {
                startButton.style.backgroundImage = new StyleBackground(buttonActiveImage);
            }
            startButton.style.opacity = 1f;
            startButton.SetEnabled(true);
            Debug.Log("[TabletController] Start button ENABLED");
        }
        else
        {
            // Set to inactive/disabled state - restore original image
            startButton.style.backgroundImage = originalButtonImage;
            startButton.style.opacity = 0.5f;
            startButton.SetEnabled(false);
            Debug.Log("[TabletController] Start button DISABLED");
        }
    }

    private void OnLanguageButtonClicked()
    {
        Debug.Log("Language button clicked - Toggle between English/Arabic");

        // Add your language switching logic here
        ToggleLanguage();
    }

    private void StartRowingGame(string playerName)
    {
        Debug.Log($"Game started for player: {playerName}");

        // Send start game command via MQTT
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            string gameMode = "rowing"; // Default
            if (debugMenu != null)
            {
                gameMode = debugMenu.GetCurrentGameMode();
            }

            var startMessage = new StartGameMessage
            {
                playerName = playerName,
                gameMode = gameMode
            };

            MQTTManager.Instance.PublishToStation("TABLET_TO_GAME", startMessage);
            Debug.Log($"[TabletController] Sent START_GAME command to station {MQTTManager.Instance.StationId} for {playerName} in {gameMode} mode");

            isGameActive = true;

            // Hide start screen, show HUD (implement UI transitions here)
            // HideStartScreen();
            // ShowHUD();
        }
        else
        {
            Debug.LogWarning("MQTT not connected! Cannot start game.");
        }
    }

    private void ToggleLanguage()
    {
        // Implement language switching logic here
        var root = uiDocument.rootVisualElement;

        if (languageButton.text == "العربية")
        {
            languageButton.text = "English";
            // Switch UI to Arabic
        }
        else
        {
            languageButton.text = "العربية";
            // Switch UI to English
        }
    }

    public void SetGameMode(string mode)
    {
        currentGameMode = mode.ToLower();
        UpdateGameModeContent();
    }

    private void UpdateGameModeContent()
    {
        Texture2D newLandingImage = null;
        string newDescription = "";
        string newButtonText = "";

        switch (currentGameMode.ToLower())
        {
            case "rowing":
                newLandingImage = rowingLandingImage;
                newDescription = rowingDescriptionEnglish;
                newButtonText = rowingButtonText;
                break;
            case "running":
                newLandingImage = runningLandingImage;
                newDescription = runningDescriptionEnglish;
                newButtonText = runningButtonText;
                break;
            case "cycling":
                newLandingImage = cyclingLandingImage;
                newDescription = cyclingDescriptionEnglish;
                newButtonText = cyclingButtonText;
                break;
            default:
                newLandingImage = rowingLandingImage;
                newDescription = rowingDescriptionEnglish;
                newButtonText = rowingButtonText;
                break;
        }

        // Update landing image
        if (landingImage != null && newLandingImage != null)
        {
            landingImage.image = newLandingImage;
        }

        // Update description
        if (descriptionLabel != null)
        {
            descriptionLabel.text = newDescription;
        }

        // Update start button text
        if (startButton != null)
        {
            startButton.text = newButtonText;
        }

        Debug.Log($"[TabletController] Updated content for game mode: {currentGameMode}");
    }

    // MQTT Message Handlers
    private void OnCountdownReceived(CountdownMessage msg)
    {
        Debug.Log($"Countdown: {msg.countdownValue}");
        // TODO: Show countdown on tablet UI
        // Update countdown display
    }

    private void OnGameDataReceived(GameDataMessage msg)
    {
        Debug.Log($"Game Data - Distance: {msg.currentDistance}m, Time: {msg.currentTime}s, Speed: {msg.currentSpeed}m/s");
        // TODO: Update HUD with game data
        // This is where you'd update the GameHUDController with live data
    }

    private void OnGameOverReceived(GameOverMessage msg)
    {
        Debug.Log($"Game Over - Distance: {msg.finalDistance}m, Time: {msg.finalTime}s, Completed: {msg.completedCourse}");
        isGameActive = false;

        // Show game over screen with results
        GameOverController.ShowResults(msg.finalTime, msg.finalDistance);

        // TODO: Reset UI after a delay or user action
        // Invoke("ResetToStartScreen", 5f);
    }

    private void OnGameStateReceived(GameStateMessage msg)
    {
        Debug.Log($"Game State: {msg.state}");
        // Handle different game states (IDLE, COUNTDOWN, PLAYING, PAUSED, FINISHED)
    }

    private void ResetToStartScreen()
    {
        isGameActive = false;
        // Hide HUD and game over screen
        // Show start screen
        Debug.Log("Reset to start screen");
    }

    /// <summary>
    /// Styles the TextField to match the design (dark background with angled corners)
    /// </summary>
    private void StyleTextField(TextField textField)
    {
        // Style the main TextField container
        textField.style.backgroundColor = Color.clear; // Let background image show through
        textField.style.borderTopWidth = 0;
        textField.style.borderBottomWidth = 0;
        textField.style.borderLeftWidth = 0;
        textField.style.borderRightWidth = 0;

        // Get the internal input element and style it
        var textInput = textField.Q<VisualElement>("unity-text-input");
        if (textInput != null)
        {
            textInput.style.backgroundColor = Color.clear;
            textInput.style.borderTopWidth = 0;
            textInput.style.borderBottomWidth = 0;
            textInput.style.borderLeftWidth = 0;
            textInput.style.borderRightWidth = 0;
            textInput.style.paddingLeft = 25;
            textInput.style.paddingRight = 25;
            textInput.style.paddingTop = 0;
            textInput.style.paddingBottom = 0;
            textInput.style.marginLeft = 0;
            textInput.style.marginRight = 0;
            textInput.style.marginTop = 0;
            textInput.style.marginBottom = 0;
        }

        // Style the text element inside
        var textElement = textField.Q<TextElement>();
        if (textElement != null)
        {
            textElement.style.color = new Color(0.7f, 0.7f, 0.7f, 1f); // Light gray text
            textElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            textElement.style.fontSize = 35;
        }

        // Also try the label if it exists (some TextField implementations have this)
        var label = textField.Q<Label>(className: "unity-text-field__label");
        if (label != null)
        {
            label.style.display = DisplayStyle.None; // Hide label if not needed
        }
    }
}
