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

    [Header("Arabic Landing Images (optional, leave empty to use same)")]
    [SerializeField] private Texture2D rowingLandingImageArabic;
    [SerializeField] private Texture2D runningLandingImageArabic;
    [SerializeField] private Texture2D cyclingLandingImageArabic;

    [Header("Localization - English")]
    [SerializeField] private string inputLabelEnglish = "Enter your name or nickname";
    [SerializeField] private string namePlaceholderEnglish = "Name";
    [SerializeField] private string validationDefaultEnglish = "Username must be at least 15 characters and contain only letters";
    [SerializeField] private string usernameAvailableEnglish = "Username available";
    [SerializeField] private string usernameTakenEnglish = "Username already taken";
    [SerializeField] private string checkingEnglish = "Checking...";
    [SerializeField] private string termsEnglish = "You assume all risks arising from the use, handling, or presence of sporting equipment at the event and agree to indemnify and hold harmless the event organizers, venue, sponsors, and app providers from any related claims or liabilities, except where prohibited by law.";

    [Header("Localization - Arabic")]
    [SerializeField] private string rowingButtonTextArabic = "!ابدأ التجديف";
    [SerializeField] private string runningButtonTextArabic = "!ابدأ الجري";
    [SerializeField] private string cyclingButtonTextArabic = "!ابدأ ركوب الدراجة";
    [SerializeField] private string inputLabelArabic = "أدخل اسمك أو لقبك";
    [SerializeField] private string namePlaceholderArabic = "الاسم";
    [SerializeField] private string validationDefaultArabic = "يجب أن يكون اسم المستخدم مكوّناً من 15 حرفاً على الأقل وأن يحتوي على حروف فقط.";
    [SerializeField] private string usernameAvailableArabic = "اسم المستخدم متاح";
    [SerializeField] private string usernameTakenArabic = "اسم المستخدم مستخدم بالفعل";
    [SerializeField] private string checkingArabic = "جاري التحقق...";
    [SerializeField] private string termsArabic = "أنت تتحمل كامل المسؤولية عن جميع المخاطر الناشئة عن استخدام أو التعامل مع أو التواجد بالقرب من المعدات الرياضية في الفعالية، وتوافق على تعويض وإبراء ذمة منظمي الفعالية والمكان والرعاة ومزوّدي التطبيقات من أي مطالبات أو مسؤوليات ذات صلة، وذلك بالقدر الذي يسمح به القانون، ما لم يكن ذلك محظوراً بموجب القانون";

    private Button startButton;
    private Button languageButton;
    private TextField nameInput;
    private Toggle termsToggle;
    private Image landingImage;
    private Label descriptionLabel;
    private Label inputLabel;
    private Label validationLabel;
    private Label termsLabel;
    private VisualElement rulesRow; // "rules" container: toggle + terms text, flip for RTL
    private VisualElement headerRow; // "Header" container: logo + language button, flip for RTL

    private bool isGameActive = false;
    private bool isArabic = false;
    private StyleBackground originalButtonImage;
    private string currentGameMode = "rowing";

    // Username uniqueness tracking
    private bool isUsernameUnique = false;
    private bool isCheckingUsername = false;
    private string lastCheckedUsername = "";
    private Coroutine usernameCheckCoroutine;
    private Coroutine usernameCheckSafetyTimeout;

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

        // Find main description label
        descriptionLabel = root.Q<Label>("DescriptionLine1");

        // Find terms label (named DescriptionLine1Checkmark inside "rules" container)
        termsLabel = root.Q<Label>("DescriptionLine1Checkmark");

        // Find the "rules" row container (toggle + terms) and "Header" row (logo + lang button)
        rulesRow = root.Q<VisualElement>("rules");
        headerRow = root.Q<VisualElement>("Header");

        // Find labels in UserNameArea by name "InputLabel"
        var userNameArea = root.Q<VisualElement>("UserNameArea");
        if (userNameArea != null)
        {
            var inputLabels = userNameArea.Query<Label>("InputLabel").ToList();
            if (inputLabels.Count > 0)
            {
                inputLabel = inputLabels[0]; // "Enter your name or nickname"
            }
            if (inputLabels.Count > 1)
            {
                validationLabel = inputLabels[1]; // Validation message
            }
        }

        Debug.Log($"[TabletController] descriptionLabel={descriptionLabel != null}, termsLabel={termsLabel != null}, inputLabel={inputLabel != null}, validationLabel={validationLabel != null}, rulesRow={rulesRow != null}, headerRow={headerRow != null}");

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
                if (nameInput.value == namePlaceholderEnglish || nameInput.value == namePlaceholderArabic)
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
        if (usernameCheckSafetyTimeout != null)
        {
            StopCoroutine(usernameCheckSafetyTimeout);
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
        bool isPlaceholder = name == namePlaceholderEnglish || name == namePlaceholderArabic;
        bool hasValidName = !string.IsNullOrWhiteSpace(name) && !isPlaceholder && name.Length > 0;

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
        UpdateValidationLabel();

        // Cancel previous check
        if (usernameCheckCoroutine != null)
        {
            StopCoroutine(usernameCheckCoroutine);
        }

        // Start debounced check (ignore both English and Arabic placeholders)
        if (!string.IsNullOrWhiteSpace(newUsername) && newUsername != namePlaceholderEnglish && newUsername != namePlaceholderArabic)
        {
            usernameCheckCoroutine = StartCoroutine(DebouncedUsernameCheck(newUsername));
        }
        else
        {
            isCheckingUsername = false;
            isUsernameUnique = false;
            UpdateStartButtonState();
            UpdateValidationLabel();
        }
    }

    private IEnumerator DebouncedUsernameCheck(string username)
    {
        // Wait for user to stop typing
        yield return new WaitForSeconds(usernameCheckDelay);

        // Check with LeaderboardManager
        if (LeaderboardManager.Instance != null)
        {
            // Ensure we're subscribed to the result event (may not have been available in Start)
            LeaderboardManager.Instance.OnUsernameCheckResult -= OnUsernameCheckResult;
            LeaderboardManager.Instance.OnUsernameCheckResult += OnUsernameCheckResult;

            LeaderboardManager.Instance.CheckUsername(username);
            Debug.Log($"[TabletController] Checking username: {username}");

            // Start safety timeout in case the callback never fires
            if (usernameCheckSafetyTimeout != null)
            {
                StopCoroutine(usernameCheckSafetyTimeout);
            }
            usernameCheckSafetyTimeout = StartCoroutine(UsernameCheckSafetyTimeout(username));
        }
        else
        {
            // No LeaderboardManager, assume unique
            Debug.Log($"[TabletController] LeaderboardManager not available, assuming username '{username}' is unique");
            isUsernameUnique = true;
            isCheckingUsername = false;
            lastCheckedUsername = username;
            UpdateStartButtonState();
            UpdateValidationLabel();
        }
    }

    private IEnumerator UsernameCheckSafetyTimeout(string username)
    {
        yield return new WaitForSeconds(5f);

        // If still checking the same username after 5s, assume unique
        string currentUsername = nameInput?.value ?? "";
        if (isCheckingUsername && currentUsername == username)
        {
            Debug.LogWarning($"[TabletController] Username check safety timeout for '{username}', assuming unique");
            isUsernameUnique = true;
            isCheckingUsername = false;
            lastCheckedUsername = username;
            UpdateStartButtonState();
            UpdateValidationLabel();
        }
    }

    private void OnUsernameCheckResult(bool isUnique, string username)
    {
        // Cancel safety timeout
        if (usernameCheckSafetyTimeout != null)
        {
            StopCoroutine(usernameCheckSafetyTimeout);
            usernameCheckSafetyTimeout = null;
        }

        // Only process if this is for the current username
        string currentUsername = nameInput?.value ?? "";
        if (username != currentUsername)
        {
            Debug.Log($"[TabletController] Ignoring stale username check result for '{username}' (current: '{currentUsername}')");
            return;
        }

        isUsernameUnique = isUnique;
        isCheckingUsername = false;
        lastCheckedUsername = username;

        Debug.Log($"[TabletController] Username '{username}' is {(isUnique ? "UNIQUE" : "TAKEN")}");

        UpdateStartButtonState();
        UpdateValidationLabel();
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
        isArabic = !isArabic;
        ApplyLanguage();
        Debug.Log($"[TabletController] Language switched to {(isArabic ? "Arabic" : "English")}");
    }

    private void ApplyLanguage()
    {
        // Language button
        if (languageButton != null)
        {
            languageButton.text = isArabic ? "English" : "العربية";
        }

        // Flip header row (logo + language button) for RTL
        if (headerRow != null)
        {
            headerRow.style.flexDirection = isArabic ? FlexDirection.RowReverse : FlexDirection.Row;
        }

        // Main description (mode-dependent)
        if (descriptionLabel != null)
        {
            descriptionLabel.text = GetDescriptionForMode();
            descriptionLabel.style.unityTextAlign = isArabic ? TextAnchor.MiddleCenter : TextAnchor.MiddleCenter;
        }

        // Landing image (switch to Arabic version if provided)
        UpdateGameModeContent();

        // Input label
        if (inputLabel != null)
        {
            inputLabel.text = isArabic ? inputLabelArabic : inputLabelEnglish;
            inputLabel.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        // Name input placeholder (only if still placeholder)
        if (nameInput != null)
        {
            string currentValue = nameInput.value;
            if (currentValue == namePlaceholderEnglish || currentValue == namePlaceholderArabic || string.IsNullOrEmpty(currentValue))
            {
                nameInput.value = isArabic ? namePlaceholderArabic : namePlaceholderEnglish;
            }
            // Align text input for RTL
            var textElement = nameInput.Q<TextElement>();
            if (textElement != null)
            {
                textElement.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            }
        }

        // Start button text (mode-dependent)
        if (startButton != null)
        {
            startButton.text = GetButtonTextForMode();
        }

        // Terms label (DescriptionLine1Checkmark)
        if (termsLabel != null)
        {
            termsLabel.text = isArabic ? termsArabic : termsEnglish;
            termsLabel.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        // Flip the "rules" row (toggle + terms label) for RTL
        if (rulesRow != null)
        {
            rulesRow.style.flexDirection = isArabic ? FlexDirection.RowReverse : FlexDirection.Row;
        }

        // Flip toggle margin (margin-right in LTR, margin-left in RTL)
        if (termsToggle != null)
        {
            termsToggle.style.marginRight = isArabic ? 0 : 20;
            termsToggle.style.marginLeft = isArabic ? 20 : 0;
        }

        // Validation label alignment
        if (validationLabel != null)
        {
            validationLabel.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        // Update validation text
        UpdateValidationLabel();
    }

    private string GetDescriptionForMode()
    {
        return currentGameMode.ToLower() switch
        {
            "rowing" => isArabic ? rowingDescriptionArabic : rowingDescriptionEnglish,
            "running" => isArabic ? runningDescriptionArabic : runningDescriptionEnglish,
            "cycling" => isArabic ? cyclingDescriptionArabic : cyclingDescriptionEnglish,
            _ => isArabic ? rowingDescriptionArabic : rowingDescriptionEnglish
        };
    }

    private string GetButtonTextForMode()
    {
        return currentGameMode.ToLower() switch
        {
            "rowing" => isArabic ? rowingButtonTextArabic : rowingButtonText,
            "running" => isArabic ? runningButtonTextArabic : runningButtonText,
            "cycling" => isArabic ? cyclingButtonTextArabic : cyclingButtonText,
            _ => isArabic ? rowingButtonTextArabic : rowingButtonText
        };
    }

    private void UpdateValidationLabel()
    {
        if (validationLabel == null) return;

        string currentUsername = nameInput?.value ?? "";
        bool isPlaceholder = currentUsername == namePlaceholderEnglish || currentUsername == namePlaceholderArabic;

        Color tintColor;

        if (string.IsNullOrWhiteSpace(currentUsername) || isPlaceholder)
        {
            validationLabel.text = isArabic ? validationDefaultArabic : validationDefaultEnglish;
            validationLabel.style.color = new Color(1f, 1f, 1f, 1f);
            tintColor = Color.white; // Default/neutral
        }
        else if (isCheckingUsername)
        {
            validationLabel.text = isArabic ? checkingArabic : checkingEnglish;
            validationLabel.style.color = new Color(1f, 0.8f, 0.2f, 1f); // Yellow
            tintColor = new Color(1f, 0.9f, 0.5f, 1f); // Light yellow tint
        }
        else if (!isUsernameUnique)
        {
            validationLabel.text = isArabic ? usernameTakenArabic : usernameTakenEnglish;
            validationLabel.style.color = new Color(1f, 0.3f, 0.3f, 1f); // Red
            tintColor = new Color(1f, 0.6f, 0.6f, 1f); // Light red tint
        }
        else
        {
            validationLabel.text = isArabic ? usernameAvailableArabic : usernameAvailableEnglish;
            validationLabel.style.color = new Color(0.3f, 1f, 0.3f, 1f); // Green
            tintColor = new Color(0.6f, 1f, 0.6f, 1f); // Light green tint
        }

        // Update input field background tint
        if (nameInput != null)
        {
            nameInput.style.unityBackgroundImageTintColor = tintColor;
        }
    }

    public void SetGameMode(string mode)
    {
        currentGameMode = mode.ToLower();
        UpdateGameModeContent();
    }

    /// <summary>
    /// Gets current language state so other screens can sync
    /// </summary>
    public bool IsArabic => isArabic;

    /// <summary>
    /// Sets language from external source (e.g. TabletUIManager syncing screens)
    /// </summary>
    public void SetLanguage(bool arabic)
    {
        isArabic = arabic;
        ApplyLanguage();
    }

    private void UpdateGameModeContent()
    {
        // Pick landing image based on mode AND language
        Texture2D newLandingImage = currentGameMode.ToLower() switch
        {
            "rowing" => (isArabic && rowingLandingImageArabic != null) ? rowingLandingImageArabic : rowingLandingImage,
            "running" => (isArabic && runningLandingImageArabic != null) ? runningLandingImageArabic : runningLandingImage,
            "cycling" => (isArabic && cyclingLandingImageArabic != null) ? cyclingLandingImageArabic : cyclingLandingImage,
            _ => (isArabic && rowingLandingImageArabic != null) ? rowingLandingImageArabic : rowingLandingImage
        };

        // Update landing image
        if (landingImage != null && newLandingImage != null)
        {
            landingImage.image = newLandingImage;
        }

        // Update description (language-aware)
        if (descriptionLabel != null)
        {
            descriptionLabel.text = GetDescriptionForMode();
        }

        // Update start button text (language-aware)
        if (startButton != null)
        {
            startButton.text = GetButtonTextForMode();
        }

        Debug.Log($"[TabletController] Updated content for game mode: {currentGameMode} (arabic: {isArabic})");
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
