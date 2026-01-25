using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using MarathonMQTT;

public class GamePCIdleController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    [Header("Sport Images (per game mode)")]
    [SerializeField] private Texture2D rowingSportImage;
    [SerializeField] private Texture2D runningSportImage;
    [SerializeField] private Texture2D cyclingSportImage;

    [Header("Countdown Images")]
    [SerializeField] private Texture2D countdown3Image;
    [SerializeField] private Texture2D countdown2Image;
    [SerializeField] private Texture2D countdown1Image;
    [SerializeField] private Texture2D countdownGoImage;

    [Header("Countdown 3 Instructions")]
    [SerializeField] private string instruction3Arabic = "خذ وضعيتك";
    [SerializeField] private string instruction3English = "Get In Position";

    [Header("Countdown 2 Instructions")]
    [SerializeField] private string instruction2Arabic = "استعد للسحب";
    [SerializeField] private string instruction2English = "Prepare To Pull";

    [Header("Countdown 1 Instructions")]
    [SerializeField] private string instruction1Arabic = "اسحب بقوة!";
    [SerializeField] private string instruction1English = "Pull Hard!";

    [Header("GO! Instructions")]
    [SerializeField] private string instructionGoArabic = "انطلق!";
    [SerializeField] private string instructionGoEnglish = "GO!";

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private float maxScale = 1.3f;
    [SerializeField] private float bounceScale = 0.95f;

    private VisualElement idleRoot;
    private VisualElement idleContent;
    private VisualElement countdownContent;
    private VisualElement countdownImageContainer;
    private Image countdownImage;
    private Image sportImage;
    private Label countdownNumberFallback;
    private Label instructionArabic;
    private Label instructionEnglish;
    private Label arabicTitle;
    private Label englishTitle;

    private bool isCountingDown = false;
    private Coroutine currentAnimation;

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
        idleRoot = root.Q<VisualElement>("IdleRoot");
        idleContent = root.Q<VisualElement>("MainContentArea");
        countdownContent = root.Q<VisualElement>("CountdownContent");
        countdownImageContainer = root.Q<VisualElement>("CountdownImageContainer");
        countdownImage = root.Q<Image>("CountdownImage");
        countdownNumberFallback = root.Q<Label>("CountdownNumberFallback");
        instructionArabic = root.Q<Label>("InstructionArabic");
        instructionEnglish = root.Q<Label>("InstructionEnglish");
        arabicTitle = root.Q<Label>("ArabicTitle");
        englishTitle = root.Q<Label>("EnglishTitle");
        sportImage = root.Q<Image>("SportImage");

        // Subscribe to MQTT messages
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnCountdownReceived += OnCountdownReceived;
            MQTTManager.Instance.OnGameStateReceived += OnGameStateReceived;
            MQTTManager.Instance.OnStartGameReceived += OnStartGameReceived;
        }

        // Load saved game mode and apply it
        string savedGameMode = PlayerPrefs.GetString("GamePC_GameMode", "rowing");
        UpdateGameMode(savedGameMode);
        Debug.Log($"[GamePC Idle] Loaded game mode from settings: {savedGameMode}");

        // Show idle state and screen initially
        ShowIdleState();
        ShowIdleScreen();
    }

    private void OnDisable()
    {
        // Stop any running animation
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        // Unsubscribe from MQTT events
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnCountdownReceived -= OnCountdownReceived;
            MQTTManager.Instance.OnGameStateReceived -= OnGameStateReceived;
            MQTTManager.Instance.OnStartGameReceived -= OnStartGameReceived;
        }
    }

    private void OnStartGameReceived(StartGameMessage msg)
    {
        Debug.Log($"[GamePC Idle] Game starting - mode: {msg.gameMode}");
        UpdateGameMode(msg.gameMode);
    }

    private void OnCountdownReceived(CountdownMessage msg)
    {
        Debug.Log($"[GamePC Idle] Countdown received: {msg.countdownValue}");

        if (msg.countdownValue > 0)
        {
            // Show countdown (3, 2, 1)
            ShowCountdownState(msg.countdownValue);
        }
        else if (msg.countdownValue == 0)
        {
            // Show "GO!"
            ShowCountdownState(0);

            Debug.Log("[GamePC Idle] GO! shown, hiding screen in 1 second...");

            // After brief delay, hide idle screen (game HUD will take over)
            StartCoroutine(HideAfterDelay(1f));
        }
    }

    private void OnGameStateReceived(GameStateMessage msg)
    {
        Debug.Log($"[GamePC Idle] Game State: {msg.state}");

        switch (msg.state)
        {
            case "IDLE":
                ShowIdleState();
                ShowIdleScreen();
                break;
            case "COUNTDOWN":
                // Countdown messages will handle showing countdown
                break;
            case "PLAYING":
                HideIdleScreen();
                break;
            case "FINISHED":
                HideIdleScreen();
                break;
        }
    }

    private void ShowIdleState()
    {
        // Show main content (waves, text)
        if (idleContent != null)
        {
            idleContent.style.display = DisplayStyle.Flex;
        }

        // Hide countdown
        if (countdownContent != null)
        {
            countdownContent.style.display = DisplayStyle.None;
        }

        isCountingDown = false;
    }

    private void ShowCountdownState(int number)
    {
        // Make sure the screen is visible
        ShowIdleScreen();

        // Hide main idle content (waves, text, sport image)
        if (idleContent != null)
        {
            // Hide the Waves and Text images
            var waves = idleContent.Q<Image>("Waves");
            var text = idleContent.Q<Image>("Text");
            if (waves != null) waves.style.display = DisplayStyle.None;
            if (text != null) text.style.display = DisplayStyle.None;
        }

        // Hide sport image during countdown
        if (sportImage != null)
        {
            sportImage.style.display = DisplayStyle.None;
        }

        // Show countdown content
        if (countdownContent != null)
        {
            countdownContent.style.display = DisplayStyle.Flex;
        }

        // Update instructions for this number
        UpdateInstructions(number);

        // Update countdown image
        UpdateCountdownImage(number);

        // Play animation
        PlayCountdownAnimation();

        isCountingDown = true;

        Debug.Log($"[GamePC Idle] Showing countdown: {number}");
    }

    private void UpdateInstructions(int number)
    {
        string arabic = "";
        string english = "";

        switch (number)
        {
            case 3:
                arabic = instruction3Arabic;
                english = instruction3English;
                break;
            case 2:
                arabic = instruction2Arabic;
                english = instruction2English;
                break;
            case 1:
                arabic = instruction1Arabic;
                english = instruction1English;
                break;
            case 0:
                arabic = instructionGoArabic;
                english = instructionGoEnglish;
                break;
        }

        if (instructionArabic != null)
        {
            instructionArabic.text = arabic;
        }

        if (instructionEnglish != null)
        {
            instructionEnglish.text = english;
        }
    }

    private void UpdateCountdownImage(int number)
    {
        Texture2D imageToShow = null;

        switch (number)
        {
            case 3:
                imageToShow = countdown3Image;
                break;
            case 2:
                imageToShow = countdown2Image;
                break;
            case 1:
                imageToShow = countdown1Image;
                break;
            case 0:
                imageToShow = countdownGoImage;
                break;
        }

        if (countdownImage != null)
        {
            if (imageToShow != null)
            {
                // Use image
                countdownImage.image = imageToShow;
                countdownImage.style.display = DisplayStyle.Flex;

                if (countdownNumberFallback != null)
                {
                    countdownNumberFallback.style.display = DisplayStyle.None;
                }
            }
            else
            {
                // Fallback to text
                countdownImage.style.display = DisplayStyle.None;

                if (countdownNumberFallback != null)
                {
                    countdownNumberFallback.style.display = DisplayStyle.Flex;
                    countdownNumberFallback.text = number == 0 ? "GO!" : number.ToString();
                }
            }
        }
    }

    private void PlayCountdownAnimation()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        currentAnimation = StartCoroutine(AnimateCountdown());
    }

    private IEnumerator AnimateCountdown()
    {
        if (countdownImageContainer == null) yield break;

        // Start small
        countdownImageContainer.style.scale = new StyleScale(new Scale(Vector3.zero));

        // Phase 1: Scale up with overshoot
        float elapsed = 0f;
        float phase1Duration = animationDuration * 0.6f;

        while (elapsed < phase1Duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / phase1Duration;

            // Ease out back curve for overshoot effect
            float scale = EaseOutBack(t) * maxScale;
            countdownImageContainer.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));

            yield return null;
        }

        // Phase 2: Settle to normal size with bounce
        elapsed = 0f;
        float phase2Duration = animationDuration * 0.4f;
        float startScale = maxScale;

        while (elapsed < phase2Duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / phase2Duration;

            // Bounce settle
            float scale = Mathf.Lerp(startScale, 1f, EaseOutBounce(t));
            countdownImageContainer.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));

            yield return null;
        }

        // Ensure final scale is exactly 1
        countdownImageContainer.style.scale = new StyleScale(new Scale(Vector3.one));

        currentAnimation = null;
    }

    // Easing function: Ease Out Back (overshoot)
    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // Easing function: Ease Out Bounce
    private float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
        {
            return n1 * t * t;
        }
        else if (t < 2f / d1)
        {
            return n1 * (t -= 1.5f / d1) * t + 0.75f;
        }
        else if (t < 2.5f / d1)
        {
            return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        }
        else
        {
            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }
    }

    private void UpdateGameMode(string mode)
    {
        string modeUpper = mode.ToUpper();
        Debug.Log($"[GamePC Idle] UpdateGameMode called with: {mode}");

        // Update sport image based on game mode
        if (sportImage != null)
        {
            Texture2D newImage = null;
            switch (modeUpper)
            {
                case "ROWING":
                    newImage = rowingSportImage;
                    break;
                case "RUNNING":
                    newImage = runningSportImage;
                    break;
                case "CYCLING":
                    newImage = cyclingSportImage;
                    break;
                default:
                    newImage = rowingSportImage;
                    break;
            }

            if (newImage != null)
            {
                sportImage.image = newImage;
                Debug.Log($"[GamePC Idle] Updated sport image for mode: {modeUpper}");
            }
            else
            {
                Debug.LogWarning($"[GamePC Idle] Sport image for mode '{modeUpper}' is NULL! Assign it in the inspector.");
            }
        }
        else
        {
            Debug.LogWarning("[GamePC Idle] sportImage UI element is NULL!");
        }

        // Update titles based on game mode
        if (arabicTitle != null && englishTitle != null)
        {
            switch (modeUpper)
            {
                case "ROWING":
                    arabicTitle.text = "تحدي التجديف";
                    englishTitle.text = "ROWING";
                    break;
                case "RUNNING":
                    arabicTitle.text = "تحدي الجري";
                    englishTitle.text = "RUNNING";
                    break;
                case "CYCLING":
                    arabicTitle.text = "تحدي الدراجة";
                    englishTitle.text = "CYCLING";
                    break;
                default:
                    arabicTitle.text = "تحدي التجديف";
                    englishTitle.text = "ROWING";
                    break;
            }
        }
    }

    public void ShowIdleScreen()
    {
        if (idleRoot != null)
        {
            idleRoot.style.display = DisplayStyle.Flex;
            Debug.Log("[GamePC Idle] Screen shown");
        }

        // Also show waves, text, and sport image when showing idle screen
        if (idleContent != null)
        {
            var waves = idleContent.Q<Image>("Waves");
            var text = idleContent.Q<Image>("Text");
            if (waves != null) waves.style.display = DisplayStyle.Flex;
            if (text != null) text.style.display = DisplayStyle.Flex;
        }

        // Show sport image
        if (sportImage != null)
        {
            sportImage.style.display = DisplayStyle.Flex;
        }
    }

    public void HideIdleScreen()
    {
        if (idleRoot != null)
        {
            idleRoot.style.display = DisplayStyle.None;
            Debug.Log("[GamePC Idle] Screen hidden");
        }
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideIdleScreen();
    }

    // Public methods for manual control
    public void SetGameMode(string mode)
    {
        UpdateGameMode(mode);
    }

    public void StartCountdown()
    {
        ShowCountdownState(3);
    }

    public void ShowIdle()
    {
        ShowIdleState();
        ShowIdleScreen();
    }

    // Test method for animation preview in editor
    [ContextMenu("Test Countdown 3")]
    public void TestCountdown3() => ShowCountdownState(3);

    [ContextMenu("Test Countdown 2")]
    public void TestCountdown2() => ShowCountdownState(2);

    [ContextMenu("Test Countdown 1")]
    public void TestCountdown1() => ShowCountdownState(1);

    [ContextMenu("Test Countdown GO")]
    public void TestCountdownGo() => ShowCountdownState(0);
}
