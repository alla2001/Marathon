using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>
/// Controller for the FM Registration Tablet
/// - User enters name
/// - Validates against FM leaderboard (different from game leaderboard)
/// - If username exists, can't press Start
/// - On Start, shows success popup
/// </summary>
public class TabletFMController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Texture2D buttonActiveImage;

    [Header("Username Check Settings")]
    [SerializeField] private float usernameCheckDelay = 0.5f;

    [Header("Popup Settings")]
    [SerializeField] private float popupDisplayDuration = 3f;
    [SerializeField] private string successMessageEnglish = "Success!";
    [SerializeField] private string successMessageArabic = "تم بنجاح!";
    [SerializeField] private string popupDescriptionEnglish = "User can go into the wheel and start the next steps";
    [SerializeField] private string popupDescriptionArabic = "يمكن للمشارك الدخول إلى العجلة والبدء باللعب.";
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

    private Button startButton;
    private Button languageButton;
    private TextField nameInput;
    private Toggle termsToggle;
    private VisualElement popup;
    private Label popupTitleLabel;
    private Label popupDescriptionLabel;
    private Label validationLabel;
    private Button popupCloseButton;
    private Label inputLabel;
    private Label termsLabel;
    private Label descriptionLabel;
    private VisualElement rootContainer;

    private StyleBackground originalButtonImage;
    private bool isArabic = false;

    // Username uniqueness tracking
    private bool isUsernameUnique = false;
    private bool isCheckingUsername = false;
    private string lastCheckedUsername = "";
    private Coroutine usernameCheckCoroutine;
    private Coroutine hidePopupCoroutine;

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("[TabletFMController] UIDocument component not found!");
            return;
        }

        var root = uiDocument.rootVisualElement;
        rootContainer = root.Q<VisualElement>("RootContainer");

        // Get UI elements
        startButton = root.Q<Button>("StartButton");
        languageButton = root.Q<Button>("LanguageButton");
        nameInput = root.Q<TextField>("NameInput");
        termsToggle = root.Q<Toggle>("TermsToggle");
        popup = root.Q<VisualElement>("Popup");

        // Find DescriptionLine1 labels (there are two - first is description, second is terms)
        var descriptionLabels = root.Query<Label>("DescriptionLine1").ToList();
        if (descriptionLabels.Count > 0)
        {
            descriptionLabel = descriptionLabels[0]; // First one is the main description
        }

        // Find labels in UserNameArea
        var userNameArea = root.Q<VisualElement>("UserNameArea");
        if (userNameArea != null)
        {
            var labels = userNameArea.Query<Label>().ToList();
            if (labels.Count > 0)
            {
                inputLabel = labels[0]; // First label is "Enter your name..."
            }
            if (labels.Count > 1)
            {
                validationLabel = labels[1]; // Second label is the validation message
            }
        }

        // Terms label is the second DescriptionLine1 (already queried above)
        if (descriptionLabels.Count > 1)
        {
            termsLabel = descriptionLabels[1]; // Second DescriptionLine1 is the terms
        }

        // Setup popup content
        SetupPopup();

        // Register button callbacks
        if (startButton != null)
        {
            originalButtonImage = startButton.style.backgroundImage;
            startButton.clicked += OnStartButtonClicked;
        }

        if (languageButton != null)
        {
            languageButton.clicked += OnLanguageButtonClicked;
        }

        if (termsToggle != null)
        {
            termsToggle.RegisterValueChangedCallback(evt => UpdateStartButtonState());
        }

        if (nameInput != null)
        {
            StyleTextField(nameInput);

            nameInput.RegisterCallback<FocusInEvent>(evt =>
            {
                // Clear placeholder in both languages
                if (nameInput.value == namePlaceholderEnglish || nameInput.value == namePlaceholderArabic)
                {
                    nameInput.value = "";
                }
            });

            nameInput.RegisterValueChangedCallback(evt => OnUsernameChanged(evt.newValue));
        }

        // Set initial button state
        UpdateStartButtonState();

        Debug.Log("[TabletFMController] Initialized");
    }

    private void Start()
    {
        // Subscribe to FMLeaderboardManager events
        if (FMLeaderboardManager.Instance != null)
        {
            FMLeaderboardManager.Instance.OnUsernameCheckResult += OnUsernameCheckResult;
            FMLeaderboardManager.Instance.OnRegistrationResult += OnRegistrationResult;
            Debug.Log("[TabletFMController] Subscribed to FMLeaderboardManager");
        }
        else
        {
            Debug.LogWarning("[TabletFMController] FMLeaderboardManager not found - username checks will assume unique");
        }
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

        if (popupCloseButton != null)
        {
            popupCloseButton.clicked -= HidePopup;
        }

        if (FMLeaderboardManager.Instance != null)
        {
            FMLeaderboardManager.Instance.OnUsernameCheckResult -= OnUsernameCheckResult;
            FMLeaderboardManager.Instance.OnRegistrationResult -= OnRegistrationResult;
        }

        if (usernameCheckCoroutine != null)
        {
            StopCoroutine(usernameCheckCoroutine);
        }

        if (hidePopupCoroutine != null)
        {
            StopCoroutine(hidePopupCoroutine);
        }
    }

    // ========================================
    // Popup Setup
    // ========================================
    private void SetupPopup()
    {
        if (popup == null)
        {
            Debug.LogWarning("[TabletFMController] Popup element not found!");
            return;
        }

        // Find existing elements in the popup by name (from UXML)
        popupTitleLabel = popup.Q<Label>("PopupTitle");
        popupDescriptionLabel = popup.Q<Label>("PopupDescription");
        popupCloseButton = popup.Q<Button>("PopupCloseButton");

        // Wire up close button
        if (popupCloseButton != null)
        {
            popupCloseButton.clicked += HidePopup;
        }

        // Ensure popup starts hidden
        popup.style.display = DisplayStyle.None;

        Debug.Log("[TabletFMController] Popup setup complete - using existing UXML popup");
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
        {
            StopCoroutine(usernameCheckCoroutine);
        }

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
            // No manager, assume unique
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
        if (username != currentUsername)
        {
            return;
        }

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
            // Default validation message
            validationLabel.text = isArabic ? validationDefaultArabic : validationDefaultEnglish;
            validationLabel.style.color = new Color(1f, 1f, 1f, 1f); // White
        }
        else if (isCheckingUsername)
        {
            validationLabel.text = isArabic ? checkingArabic : checkingEnglish;
            validationLabel.style.color = new Color(1f, 0.8f, 0.2f, 1f); // Yellow
        }
        else if (!isUsernameUnique)
        {
            validationLabel.text = isArabic ? usernameTakenMessageArabic : usernameTakenMessageEnglish;
            validationLabel.style.color = new Color(1f, 0.3f, 0.3f, 1f); // Red
        }
        else
        {
            validationLabel.text = isArabic ? usernameAvailableArabic : usernameAvailableEnglish;
            validationLabel.style.color = new Color(0.3f, 1f, 0.3f, 1f); // Green
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
            {
                startButton.style.backgroundImage = new StyleBackground(buttonActiveImage);
            }
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
    // Button Clicks
    // ========================================
    private void OnStartButtonClicked()
    {
        if (!IsStartButtonEnabled())
        {
            Debug.Log("[TabletFMController] Start button clicked but conditions not met");
            return;
        }

        string playerName = nameInput?.value ?? "";

        Debug.Log($"[TabletFMController] Registering user: {playerName}");

        // Register with FM leaderboard
        if (FMLeaderboardManager.Instance != null)
        {
            FMLeaderboardManager.Instance.RegisterUser(playerName);
        }

        // Show success popup immediately
        ShowSuccessPopup();
    }

    private void OnRegistrationResult(bool success, string message)
    {
        Debug.Log($"[TabletFMController] Registration result: {success} - {message}");
        // Popup is already shown, but we could update it here if needed
    }

    private void OnLanguageButtonClicked()
    {
        isArabic = !isArabic;
        ApplyLanguage();
        Debug.Log($"[TabletFMController] Language switched to {(isArabic ? "Arabic" : "English")}");
    }

    private void ApplyLanguage()
    {
        // Set text direction for RTL (Arabic) or LTR (English)
        var direction = isArabic ? FlexDirection.RowReverse : FlexDirection.Row;

        // Language button
        if (languageButton != null)
        {
            languageButton.text = isArabic ? "English" : "العربية";
        }

        // Main description
        if (descriptionLabel != null)
        {
            descriptionLabel.text = isArabic ? descriptionArabic : descriptionEnglish;
        }

        // Input label
        if (inputLabel != null)
        {
            inputLabel.text = isArabic ? inputLabelArabic : inputLabelEnglish;
            inputLabel.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        // Name input placeholder (only if it's still the placeholder)
        if (nameInput != null)
        {
            string currentValue = nameInput.value;
            if (currentValue == namePlaceholderEnglish || currentValue == namePlaceholderArabic || string.IsNullOrEmpty(currentValue))
            {
                nameInput.value = isArabic ? namePlaceholderArabic : namePlaceholderEnglish;
            }
            // Align text input
            var textElement = nameInput.Q<TextElement>();
            if (textElement != null)
            {
                textElement.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            }
        }

        // Start button
        if (startButton != null)
        {
            startButton.text = isArabic ? startButtonArabic : startButtonEnglish;
        }

        // Terms label
        if (termsLabel != null)
        {
            termsLabel.text = isArabic ? termsArabic : termsEnglish;
            termsLabel.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        // Validation label alignment
        if (validationLabel != null)
        {
            validationLabel.style.unityTextAlign = isArabic ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        // Update validation label text
        UpdateValidationLabel();
    }

    // ========================================
    // Popup
    // ========================================
    private void ShowSuccessPopup()
    {
        if (popup == null) return;

        // Update labels based on language
        if (popupTitleLabel != null)
        {
            popupTitleLabel.text = isArabic ? successMessageArabic : successMessageEnglish;
        }

        if (popupDescriptionLabel != null)
        {
            popupDescriptionLabel.text = isArabic ? popupDescriptionArabic : popupDescriptionEnglish;
        }

        // Position close button based on language (top-left for Arabic, top-right for English)
        if (popupCloseButton != null)
        {
            if (isArabic)
            {
                popupCloseButton.style.right = StyleKeyword.Auto;
                popupCloseButton.style.left = 15;
            }
            else
            {
                popupCloseButton.style.left = StyleKeyword.Auto;
                popupCloseButton.style.right = 15;
            }
        }

        // Show with fade animation
        popup.style.display = DisplayStyle.Flex;
        popup.style.opacity = 0;

        popup.style.transitionProperty = new StyleList<StylePropertyName>(
            new System.Collections.Generic.List<StylePropertyName> { new StylePropertyName("opacity") }
        );
        popup.style.transitionDuration = new StyleList<TimeValue>(
            new System.Collections.Generic.List<TimeValue> { new TimeValue(0.3f, TimeUnit.Second) }
        );

        popup.schedule.Execute(() =>
        {
            popup.style.opacity = 1;
        }).ExecuteLater(10);

        // Auto-hide after duration
        if (hidePopupCoroutine != null)
        {
            StopCoroutine(hidePopupCoroutine);
        }
        hidePopupCoroutine = StartCoroutine(HidePopupAfterDelay());

        // Reset the form
        ResetForm();

        Debug.Log("[TabletFMController] Showing success popup");
    }

    private IEnumerator HidePopupAfterDelay()
    {
        yield return new WaitForSeconds(popupDisplayDuration);
        HidePopup();
    }

    private void HidePopup()
    {
        if (popup == null) return;

        if (hidePopupCoroutine != null)
        {
            StopCoroutine(hidePopupCoroutine);
            hidePopupCoroutine = null;
        }

        // Fade out
        popup.style.opacity = 0;

        popup.schedule.Execute(() =>
        {
            popup.style.display = DisplayStyle.None;
        }).ExecuteLater(300);
    }

    private void ResetForm()
    {
        // Reset name input with correct language placeholder
        if (nameInput != null)
        {
            nameInput.value = isArabic ? namePlaceholderArabic : namePlaceholderEnglish;
        }

        // Reset toggle
        if (termsToggle != null)
        {
            termsToggle.value = false;
        }

        // Reset username check state
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
