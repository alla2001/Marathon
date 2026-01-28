using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>
/// Controller for the FM Registration Tablet
/// Flow:
/// 1. User enters name, presses Play -> register in leaderboard (don't send name on MQTT yet)
/// 2. Show "Success!" popup for 4 seconds
/// 3. Show "Are you Ready?" popup with Start Game button
/// 4. User presses Start Game -> send name on MQTT -> show "Please Wait!"
/// 5. Wait for "Game Idle" status -> hide popup, reset form
/// </summary>
public class TabletFMController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Texture2D buttonActiveImage;

    [Header("Username Check Settings")]
    [SerializeField] private float usernameCheckDelay = 0.5f;

    [Header("Popup Settings")]
    [SerializeField] private float successDisplayDuration = 4f;
    [SerializeField] private float stopButtonDelay = 15f;
    [SerializeField] private string usernameTakenMessageEnglish = "Username already registered";
    [SerializeField] private string usernameTakenMessageArabic = "اسم المستخدم مسجل مسبقاً";

    [Header("Localization - English")]
    [SerializeField] private string descriptionEnglish = "Row through a virtual course inspired by Riyadh landmarks.\nStay on the orange ribbon path and reach the finish line before the timer ends.";
    [SerializeField] private string inputLabelEnglish = "Enter your name or nickname";
    [SerializeField] private string namePlaceholderEnglish = "Name";
    [SerializeField] private string validationDefaultEnglish = "Username must be at least 15 characters and contain only letters";
    [SerializeField] private string usernameAvailableEnglish = "Username available";
    [SerializeField] private string checkingEnglish = "Checking...";
    [SerializeField] private string startButtonEnglish = "Start";
    [SerializeField] private string termsEnglish = "You assume all risks arising from the use, handling, or presence of sporting equipment at the event and agree to indemnify and hold harmless the event organizers, venue, sponsors, and app providers from any related claims or liabilities, except where prohibited by law.";

    [Header("Localization - Arabic")]
    [SerializeField] private string descriptionArabic = "جدّف عبر مسار افتراضي مستوحى من معالم الرياض.\nابقَ على المسار البرتقالي وصل إلى خط النهاية قبل انتهاء الوقت.";
    [SerializeField] private string inputLabelArabic = "أدخل اسمك أو لقبك";
    [SerializeField] private string namePlaceholderArabic = "الاسم";
    [SerializeField] private string validationDefaultArabic = "يجب أن يكون اسم المستخدم مكوّناً من 15 حرفاً على الأقل وأن يحتوي على حروف فقط.";
    [SerializeField] private string usernameAvailableArabic = "اسم المستخدم متاح";
    [SerializeField] private string checkingArabic = "جاري التحقق...";
    [SerializeField] private string startButtonArabic = "ابدأ";
    [SerializeField] private string termsArabic = "أنت تتحمل كامل المسؤولية عن جميع المخاطر الناشئة عن استخدام أو التعامل مع أو التواجد بالقرب من المعدات الرياضية في الفعالية، وتوافق على تعويض وإبراء ذمة منظمي الفعالية والمكان والرعاة ومزوّدي التطبيقات من أي مطالبات أو مسؤوليات ذات صلة، وذلك بالقدر الذي يسمح به القانون، ما لم يكن ذلك محظوراً بموجب القانون";

    // Form UI
    private Button startButton;
    private Button languageButton;
    private TextField nameInput;
    private Toggle termsToggle;
    private Label validationLabel;
    private Label inputLabel;
    private Label termsLabel;
    private Label descriptionLabel;
    private VisualElement rootContainer;

    // Popup containers
    private VisualElement popup;
    private VisualElement successPanel;
    private VisualElement areYouReadyPanel;
    private VisualElement gameInProgressPanel;
    private Button startGameButton; // Button inside "Are you Ready?" panel
    private Button stopGameButton;  // Button inside "gameinprogress" panel

    private StyleBackground originalButtonImage;
    private bool isArabic = false;

    // Username uniqueness tracking
    private bool isUsernameUnique = false;
    private bool isCheckingUsername = false;
    private string lastCheckedUsername = "";
    private Coroutine usernameCheckCoroutine;
    private Coroutine popupFlowCoroutine;
    private Coroutine stopButtonDelayCoroutine;

    // Stored player name for the popup flow
    private string pendingPlayerName = "";

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("[TabletFMController] UIDocument component not found!");
            return;
        }

        var root = uiDocument.rootVisualElement;
        rootContainer = root.Q<VisualElement>("RootContainer");

        // Form elements
        startButton = root.Q<Button>("StartButton");
        languageButton = root.Q<Button>("LanguageButton");
        nameInput = root.Q<TextField>("NameInput");
        termsToggle = root.Q<Toggle>("TermsToggle");

        // Find DescriptionLine1 labels
        var descriptionLabels = root.Query<Label>("DescriptionLine1").ToList();
        if (descriptionLabels.Count > 0)
            descriptionLabel = descriptionLabels[0];
        if (descriptionLabels.Count > 1)
            termsLabel = descriptionLabels[1];

        // Find labels in UserNameArea
        var userNameArea = root.Q<VisualElement>("UserNameArea");
        if (userNameArea != null)
        {
            var labels = userNameArea.Query<Label>().ToList();
            if (labels.Count > 0) inputLabel = labels[0];
            if (labels.Count > 1) validationLabel = labels[1];
        }

        // Popup elements
        popup = root.Q<VisualElement>("Popup");
        successPanel = root.Q<VisualElement>("Success");
        areYouReadyPanel = root.Q<VisualElement>("areyouready");
        gameInProgressPanel = root.Q<VisualElement>("gameinprogress");

        // Find Start Game button inside areyouready panel
        if (areYouReadyPanel != null)
        {
            startGameButton = areYouReadyPanel.Q<Button>();
            if (startGameButton != null)
            {
                startGameButton.clicked += OnStartGameClicked;
            }
        }

        // Find Stop Game button inside gameinprogress panel
        if (gameInProgressPanel != null)
        {
            stopGameButton = gameInProgressPanel.Q<Button>("StopGameButton");
            if (stopGameButton != null)
            {
                stopGameButton.clicked += CancelGame;
            }
        }

        // Hide popup and all panels initially
        HideAllPopupPanels();
        if (popup != null) popup.style.display = DisplayStyle.None;

        // Register form callbacks
        if (startButton != null)
        {
            originalButtonImage = startButton.style.backgroundImage;
            startButton.clicked += OnPlayButtonClicked;
        }

        if (languageButton != null)
            languageButton.clicked += OnLanguageButtonClicked;

        if (termsToggle != null)
            termsToggle.RegisterValueChangedCallback(evt => UpdateStartButtonState());

        if (nameInput != null)
        {
            StyleTextField(nameInput);

            nameInput.RegisterCallback<FocusInEvent>(evt =>
            {
                if (nameInput.value == namePlaceholderEnglish || nameInput.value == namePlaceholderArabic)
                    nameInput.value = "";
            });

            nameInput.RegisterValueChangedCallback(evt => OnUsernameChanged(evt.newValue));
        }

        UpdateStartButtonState();
        Debug.Log("[TabletFMController] Initialized");
    }

    private void Start()
    {
        if (FMLeaderboardManager.Instance != null)
        {
            FMLeaderboardManager.Instance.OnUsernameCheckResult += OnUsernameCheckResult;
            FMLeaderboardManager.Instance.OnRegistrationResult += OnRegistrationResult;
            FMLeaderboardManager.Instance.OnGameStatusReceived += OnGameStatusReceived;
            Debug.Log("[TabletFMController] Subscribed to FMLeaderboardManager");
        }
        else
        {
            Debug.LogWarning("[TabletFMController] FMLeaderboardManager not found");
        }
    }

    private void OnDisable()
    {
        if (startButton != null) startButton.clicked -= OnPlayButtonClicked;
        if (languageButton != null) languageButton.clicked -= OnLanguageButtonClicked;
        if (startGameButton != null) startGameButton.clicked -= OnStartGameClicked;
        if (stopGameButton != null) stopGameButton.clicked -= CancelGame;

        if (FMLeaderboardManager.Instance != null)
        {
            FMLeaderboardManager.Instance.OnUsernameCheckResult -= OnUsernameCheckResult;
            FMLeaderboardManager.Instance.OnRegistrationResult -= OnRegistrationResult;
            FMLeaderboardManager.Instance.OnGameStatusReceived -= OnGameStatusReceived;
        }

        if (usernameCheckCoroutine != null) StopCoroutine(usernameCheckCoroutine);
        if (popupFlowCoroutine != null) StopCoroutine(popupFlowCoroutine);
        if (stopButtonDelayCoroutine != null) StopCoroutine(stopButtonDelayCoroutine);
    }

    // ========================================
    // Username Checking
    // ========================================
    private void OnUsernameChanged(string newUsername)
    {
        if (newUsername != lastCheckedUsername)
        {
            isUsernameUnique = false;
            isCheckingUsername = true;
        }

        UpdateStartButtonState();
        UpdateValidationLabel();

        if (usernameCheckCoroutine != null)
            StopCoroutine(usernameCheckCoroutine);

        if (!string.IsNullOrWhiteSpace(newUsername) && newUsername != "Name")
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
        yield return new WaitForSeconds(usernameCheckDelay);

        if (FMLeaderboardManager.Instance != null)
        {
            FMLeaderboardManager.Instance.CheckUsername(username);
            Debug.Log($"[TabletFMController] Checking FM username: {username}");
        }
        else
        {
            isUsernameUnique = true;
            isCheckingUsername = false;
            lastCheckedUsername = username;
            UpdateStartButtonState();
            UpdateValidationLabel();
        }
    }

    private void OnUsernameCheckResult(bool isUnique, string username)
    {
        string currentUsername = nameInput?.value ?? "";
        if (username != currentUsername) return;

        isUsernameUnique = isUnique;
        isCheckingUsername = false;
        lastCheckedUsername = username;

        Debug.Log($"[TabletFMController] FM Username '{username}' is {(isUnique ? "UNIQUE" : "TAKEN")}");

        UpdateStartButtonState();
        UpdateValidationLabel();
    }

    private void UpdateValidationLabel()
    {
        if (validationLabel == null) return;

        string currentUsername = nameInput?.value ?? "";
        bool isPlaceholder = currentUsername == namePlaceholderEnglish || currentUsername == namePlaceholderArabic;

        if (string.IsNullOrWhiteSpace(currentUsername) || isPlaceholder)
        {
            validationLabel.text = isArabic ? validationDefaultArabic : validationDefaultEnglish;
            validationLabel.style.color = new Color(1f, 1f, 1f, 1f);
        }
        else if (isCheckingUsername)
        {
            validationLabel.text = isArabic ? checkingArabic : checkingEnglish;
            validationLabel.style.color = new Color(1f, 0.8f, 0.2f, 1f);
        }
        else if (!isUsernameUnique)
        {
            validationLabel.text = isArabic ? usernameTakenMessageArabic : usernameTakenMessageEnglish;
            validationLabel.style.color = new Color(1f, 0.3f, 0.3f, 1f);
        }
        else
        {
            validationLabel.text = isArabic ? usernameAvailableArabic : usernameAvailableEnglish;
            validationLabel.style.color = new Color(0.3f, 1f, 0.3f, 1f);
        }
    }

    // ========================================
    // Button State
    // ========================================
    private bool IsStartButtonEnabled()
    {
        bool toggleChecked = termsToggle != null && termsToggle.value;
        string name = nameInput?.value ?? "";
        bool hasValidName = !string.IsNullOrWhiteSpace(name) && name != "Name" && name.Length > 0;
        bool usernameValid = hasValidName && isUsernameUnique && !isCheckingUsername;

        return toggleChecked && usernameValid;
    }

    private void UpdateStartButtonState()
    {
        if (startButton == null) return;

        bool enabled = IsStartButtonEnabled();

        if (enabled)
        {
            if (buttonActiveImage != null)
                startButton.style.backgroundImage = new StyleBackground(buttonActiveImage);
            startButton.style.opacity = 1f;
            startButton.SetEnabled(true);
        }
        else
        {
            startButton.style.backgroundImage = originalButtonImage;
            startButton.style.opacity = 0.5f;
            startButton.SetEnabled(false);
        }
    }

    // ========================================
    // Play Button (Step 1: Register, show Success)
    // ========================================
    private void OnPlayButtonClicked()
    {
        if (!IsStartButtonEnabled())
        {
            Debug.Log("[TabletFMController] Play button clicked but conditions not met");
            return;
        }

        pendingPlayerName = nameInput?.value ?? "";

        Debug.Log($"[TabletFMController] Registering user: {pendingPlayerName}");

        // Register in leaderboard (store name) but DON'T send on MQTT yet
        if (FMLeaderboardManager.Instance != null)
        {
            FMLeaderboardManager.Instance.RegisterUser(pendingPlayerName);
        }

        // Reset the form immediately
        ResetForm();

        // Start the popup flow: Success -> Are you Ready? -> Please Wait
        if (popupFlowCoroutine != null) StopCoroutine(popupFlowCoroutine);
        popupFlowCoroutine = StartCoroutine(PopupFlowCoroutine());
    }

    // ========================================
    // Popup Flow
    // ========================================
    private IEnumerator PopupFlowCoroutine()
    {
        // Step 1: Show Success for 4 seconds
        ShowPopupPanel(successPanel);
        Debug.Log("[TabletFMController] Showing Success popup");

        yield return new WaitForSeconds(successDisplayDuration);

        // Step 2: Show "Are you Ready?" and wait for Start Game button
        ShowPopupPanel(areYouReadyPanel);
        Debug.Log("[TabletFMController] Showing Are You Ready popup");

        // Flow continues when OnStartGameClicked is called
    }

    // ========================================
    // Start Game Button (Step 2: Send name, show Please Wait)
    // ========================================
    private void OnStartGameClicked()
    {
        Debug.Log($"[TabletFMController] Start Game clicked, sending name: {pendingPlayerName}");

        // NOW send the name on MQTT
        if (FMLeaderboardManager.Instance != null && !string.IsNullOrEmpty(pendingPlayerName))
        {
            FMLeaderboardManager.Instance.SendUsername(pendingPlayerName);
        }

        // Show "Please Wait!" popup
        ShowPopupPanel(gameInProgressPanel);

        // Hide stop button for the first N seconds
        if (stopGameButton != null)
            stopGameButton.style.display = DisplayStyle.None;
        if (stopButtonDelayCoroutine != null)
            StopCoroutine(stopButtonDelayCoroutine);
        stopButtonDelayCoroutine = StartCoroutine(ShowStopButtonAfterDelay());

        Debug.Log("[TabletFMController] Showing Please Wait popup");

        // Flow continues when OnGameStatusReceived fires "Game Idle"
    }

    private IEnumerator ShowStopButtonAfterDelay()
    {
        yield return new WaitForSeconds(stopButtonDelay);
        if (stopGameButton != null)
            stopGameButton.style.display = DisplayStyle.Flex;
        stopButtonDelayCoroutine = null;
    }

    // ========================================
    // Game Status (Step 3: Game Idle -> hide popup)
    // ========================================
    private void OnGameStatusReceived(string status)
    {
        Debug.Log($"[TabletFMController] Game status received: {status}");

        if (status == "GameIdle")
        {
            // Game finished, hide popup and return to form
            HidePopup();
            pendingPlayerName = "";
            Debug.Log("[TabletFMController] Game Idle received, returning to form");
        }
    }

    private void OnRegistrationResult(bool success, string message)
    {
        Debug.Log($"[TabletFMController] Registration result: {success} - {message}");
    }

    // ========================================
    // Popup Helpers
    // ========================================
    private void ShowPopupPanel(VisualElement panel)
    {
        if (popup == null) return;

        // Hide all panels first
        HideAllPopupPanels();

        // Show the popup container
        popup.style.display = DisplayStyle.Flex;
        popup.style.opacity = 1;

        // Show the requested panel
        if (panel != null)
            panel.style.display = DisplayStyle.Flex;
    }

    private void HideAllPopupPanels()
    {
        if (successPanel != null) successPanel.style.display = DisplayStyle.None;
        if (areYouReadyPanel != null) areYouReadyPanel.style.display = DisplayStyle.None;
        if (gameInProgressPanel != null) gameInProgressPanel.style.display = DisplayStyle.None;
    }

    private void HidePopup()
    {
        if (popup == null) return;

        if (popupFlowCoroutine != null)
        {
            StopCoroutine(popupFlowCoroutine);
            popupFlowCoroutine = null;
        }

        if (stopButtonDelayCoroutine != null)
        {
            StopCoroutine(stopButtonDelayCoroutine);
            stopButtonDelayCoroutine = null;
        }

        HideAllPopupPanels();
        popup.style.display = DisplayStyle.None;
    }

    // ========================================
    // Cancel Game (called from debug menu)
    // ========================================
    public void CancelGame()
    {
        Debug.Log("[TabletFMController] Game cancelled, broadcasting stop game");

        if (FMLeaderboardManager.Instance != null)
        {
            FMLeaderboardManager.Instance.SendStopGame();
        }

        HidePopup();
        pendingPlayerName = "";
        ResetForm();
    }

    // ========================================
    // Language
    // ========================================
    private void OnLanguageButtonClicked()
    {
        isArabic = !isArabic;
        ApplyLanguage();
        Debug.Log($"[TabletFMController] Language switched to {(isArabic ? "Arabic" : "English")}");
    }

    private void ApplyLanguage()
    {
        if (languageButton != null)
            languageButton.text = isArabic ? "English" : "العربية";

        if (descriptionLabel != null)
            descriptionLabel.text = isArabic ? descriptionArabic : descriptionEnglish;

        if (inputLabel != null)
        {
            inputLabel.text = isArabic ? inputLabelArabic : inputLabelEnglish;
            inputLabel.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        if (nameInput != null)
        {
            string currentValue = nameInput.value;
            if (currentValue == namePlaceholderEnglish || currentValue == namePlaceholderArabic || string.IsNullOrEmpty(currentValue))
                nameInput.value = isArabic ? namePlaceholderArabic : namePlaceholderEnglish;

            var textElement = nameInput.Q<TextElement>();
            if (textElement != null)
                textElement.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        if (startButton != null)
            startButton.text = isArabic ? startButtonArabic : startButtonEnglish;

        if (termsLabel != null)
        {
            termsLabel.text = isArabic ? termsArabic : termsEnglish;
            termsLabel.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        if (validationLabel != null)
            validationLabel.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;

        UpdateValidationLabel();
    }

    // ========================================
    // Form Reset
    // ========================================
    private void ResetForm()
    {
        if (nameInput != null)
            nameInput.value = isArabic ? namePlaceholderArabic : namePlaceholderEnglish;

        if (termsToggle != null)
            termsToggle.value = false;

        isUsernameUnique = false;
        isCheckingUsername = false;
        lastCheckedUsername = "";

        UpdateStartButtonState();
        UpdateValidationLabel();
    }

    // ========================================
    // TextField Styling
    // ========================================
    private void StyleTextField(TextField textField)
    {
        textField.style.backgroundColor = Color.clear;
        textField.style.borderTopWidth = 0;
        textField.style.borderBottomWidth = 0;
        textField.style.borderLeftWidth = 0;
        textField.style.borderRightWidth = 0;

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
        }

        var textElement = textField.Q<TextElement>();
        if (textElement != null)
        {
            textElement.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            textElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            textElement.style.fontSize = 35;
        }
    }
}
