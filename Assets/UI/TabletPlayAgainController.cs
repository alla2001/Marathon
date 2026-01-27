using UnityEngine;
using UnityEngine.UIElements;
using MarathonMQTT;
using System.Collections.Generic;

public class TabletPlayAgainController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    [Header("Game Mode Landing Images")]
    [SerializeField] private Texture2D rowingLandingImage;
    [SerializeField] private Texture2D runningLandingImage;
    [SerializeField] private Texture2D cyclingLandingImage;

    [Header("Arabic Landing Images (optional, leave empty to use same)")]
    [SerializeField] private Texture2D rowingLandingImageArabic;
    [SerializeField] private Texture2D runningLandingImageArabic;
    [SerializeField] private Texture2D cyclingLandingImageArabic;

    private Label gameModeTitle;
    private Button playAgainButton;
    private Button goHomeButton;
    private Button languageButton;
    private Image landingImage;

    // Win/Lose UI elements (lists to support multiple elements with same name)
    private List<VisualElement> winContainers = new List<VisualElement>();
    private List<VisualElement> loseContainers = new List<VisualElement>();
    private List<Label> resultTimeLabels = new List<Label>();
    private List<Label> resultDistanceLabels = new List<Label>();

    private string currentGameMode = "ROWING";
    private bool lastGameWon = false;
    private bool isArabic = false;

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
        playAgainButton = root.Q<Button>("PlayAgainButton");
        goHomeButton = root.Q<Button>("GoHomeButton");
        landingImage = root.Q<Image>("Landing-Image");
        languageButton = root.Q<Button>("LanguageButton");

        // Get ALL Win/Lose containers with any of these names
        winContainers = FindAllElementsWithNames(root, "WinContainer", "WinScreen", "Victory", "Win", "WinPanel");
        loseContainers = FindAllElementsWithNames(root, "LoseContainer", "LoseScreen", "GameOver", "Lose", "LosePanel", "Defeat");

        // Get result labels from all containers
        foreach (var container in winContainers)
        {
            var timeLabel = container.Q<Label>("TimeResult") ?? container.Q<Label>("WinTimeResult");
            var distLabel = container.Q<Label>("DistanceResult") ?? container.Q<Label>("WinDistanceResult");
            if (timeLabel != null) resultTimeLabels.Add(timeLabel);
            if (distLabel != null) resultDistanceLabels.Add(distLabel);
        }
        foreach (var container in loseContainers)
        {
            var timeLabel = container.Q<Label>("TimeResult") ?? container.Q<Label>("LoseTimeResult");
            var distLabel = container.Q<Label>("DistanceResult") ?? container.Q<Label>("LoseDistanceResult");
            if (timeLabel != null && !resultTimeLabels.Contains(timeLabel)) resultTimeLabels.Add(timeLabel);
            if (distLabel != null && !resultDistanceLabels.Contains(distLabel)) resultDistanceLabels.Add(distLabel);
        }
        // Also check root level
        var rootTimeLabel = root.Q<Label>("TimeResult");
        var rootDistLabel = root.Q<Label>("DistanceResult");
        if (rootTimeLabel != null && !resultTimeLabels.Contains(rootTimeLabel)) resultTimeLabels.Add(rootTimeLabel);
        if (rootDistLabel != null && !resultDistanceLabels.Contains(rootDistLabel)) resultDistanceLabels.Add(rootDistLabel);

        // Hide all containers initially
        foreach (var container in winContainers) container.style.display = DisplayStyle.None;
        foreach (var container in loseContainers) container.style.display = DisplayStyle.None;

        Debug.Log($"[TabletPlayAgain] Found {winContainers.Count} win containers, {loseContainers.Count} lose containers");

        // Register button callbacks
        if (playAgainButton != null)
        {
            playAgainButton.clicked += OnPlayAgainClicked;
        }
        else
        {
            Debug.LogError("PlayAgainButton not found in UI!");
        }

        if (goHomeButton != null)
        {
            goHomeButton.clicked += OnGoHomeClicked;
        }
        else
        {
            Debug.LogError("GoHomeButton not found in UI!");
        }

        // Register language button
        if (languageButton != null)
        {
            languageButton.clicked += OnLanguageButtonClicked;
        }

        // Subscribe to MQTT events
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnGameOverReceived += OnGameOverReceived;
        }

        // Load saved game mode and apply it
        string savedGameMode = PlayerPrefs.GetString("GameMode", "rowing");
        SetGameMode(savedGameMode);
        Debug.Log($"[TabletPlayAgain] Loaded game mode from settings: {savedGameMode}");

        // Hide initially
        HidePlayAgain();
    }

    private void OnDisable()
    {
        // Unregister button callbacks
        if (playAgainButton != null)
        {
            playAgainButton.clicked -= OnPlayAgainClicked;
        }

        if (goHomeButton != null)
        {
            goHomeButton.clicked -= OnGoHomeClicked;
        }

        if (languageButton != null)
        {
            languageButton.clicked -= OnLanguageButtonClicked;
        }

        // Unsubscribe from MQTT
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnGameOverReceived -= OnGameOverReceived;
        }
    }

    private void OnGameOverReceived(GameOverMessage msg)
    {
        Debug.Log($"Game Over - Distance: {msg.finalDistance}m, Time: {msg.finalTime}s, Won: {msg.completedCourse}");

        lastGameWon = msg.completedCourse;

        // Show appropriate win/lose containers (all of them)
        foreach (var container in winContainers) container.style.display = lastGameWon ? DisplayStyle.Flex : DisplayStyle.None;
        foreach (var container in loseContainers) container.style.display = lastGameWon ? DisplayStyle.None : DisplayStyle.Flex;

        // Update all result labels
        int minutes = Mathf.FloorToInt(msg.finalTime / 60f);
        int seconds = Mathf.FloorToInt(msg.finalTime % 60f);
        string timeText = $"{minutes:00}:{seconds:00}";
        string distText = $"{Mathf.RoundToInt(msg.finalDistance)} m";

        foreach (var label in resultTimeLabels) label.text = timeText;
        foreach (var label in resultDistanceLabels) label.text = distText;

        // Show play again screen
        ShowPlayAgain();
    }

    private void OnPlayAgainClicked()
    {
        Debug.Log("Play Again button clicked!");

        // Send reset command to game
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            var resetMessage = new ResetGameMessage();
            MQTTManager.Instance.PublishToStation("TABLET_TO_GAME", resetMessage);
            Debug.Log("Sent RESET_GAME command via MQTT");
        }

        // Notify UI Manager
        if (TabletUIManager.Instance != null)
        {
            TabletUIManager.Instance.OnPlayAgain();
        }
    }

    private void OnGoHomeClicked()
    {
        Debug.Log("Go Home button clicked!");

        // Reset game state
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            var resetMessage = new ResetGameMessage();
            MQTTManager.Instance.PublishToStation("TABLET_TO_GAME", resetMessage);
        }

        // Notify UI Manager
        if (TabletUIManager.Instance != null)
        {
            TabletUIManager.Instance.OnGoHome();
        }
    }

    public void ShowPlayAgain()
    {
        var root = uiDocument.rootVisualElement;
        if (root != null)
        {
            root.style.display = DisplayStyle.Flex;
            Debug.Log("Showing Play Again screen");
        }
    }

    public void HidePlayAgain()
    {
        var root = uiDocument.rootVisualElement;
        if (root != null)
        {
            root.style.display = DisplayStyle.None;
            Debug.Log("Hiding Play Again screen");
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

        Texture2D newImage = currentGameMode.ToLower() switch
        {
            "rowing" => (isArabic && rowingLandingImageArabic != null) ? rowingLandingImageArabic : rowingLandingImage,
            "running" => (isArabic && runningLandingImageArabic != null) ? runningLandingImageArabic : runningLandingImage,
            "cycling" => (isArabic && cyclingLandingImageArabic != null) ? cyclingLandingImageArabic : cyclingLandingImage,
            _ => (isArabic && rowingLandingImageArabic != null) ? rowingLandingImageArabic : rowingLandingImage
        };

        if (newImage != null)
        {
            landingImage.image = newImage;
            Debug.Log($"[TabletPlayAgain] Updated landing image for mode: {currentGameMode} (arabic: {isArabic})");
        }
    }

    private void OnLanguageButtonClicked()
    {
        isArabic = !isArabic;
        ApplyLanguage();
        Debug.Log($"[TabletPlayAgain] Language switched to {(isArabic ? "Arabic" : "English")}");
    }

    public void SetLanguage(bool arabic)
    {
        isArabic = arabic;
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        // Language button
        if (languageButton != null)
        {
            languageButton.text = isArabic ? "English" : "العربية";
        }

        // Update landing image for current language
        UpdateLandingImage();
    }

    /// <summary>
    /// Finds the first existing VisualElement from a list of possible names
    /// </summary>
    private VisualElement FindFirstElement(VisualElement parent, params string[] names)
    {
        foreach (string name in names)
        {
            var element = parent.Q<VisualElement>(name);
            if (element != null)
            {
                Debug.Log($"[TabletPlayAgain] Found element with name: {name}");
                return element;
            }
        }
        return null;
    }

    /// <summary>
    /// Finds ALL VisualElements that match any of the given names
    /// </summary>
    private List<VisualElement> FindAllElementsWithNames(VisualElement parent, params string[] names)
    {
        var results = new List<VisualElement>();
        foreach (string name in names)
        {
            var elements = parent.Query<VisualElement>(name).ToList();
            foreach (var element in elements)
            {
                if (!results.Contains(element))
                {
                    results.Add(element);
                    Debug.Log($"[TabletPlayAgain] Found element: {name}");
                }
            }
        }
        return results;
    }
}
