using UnityEngine;
using UnityEngine.UIElements;
using MarathonMQTT;

public class TabletPlayAgainController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    [Header("Game Mode Landing Images")]
    [SerializeField] private Texture2D rowingLandingImage;
    [SerializeField] private Texture2D runningLandingImage;
    [SerializeField] private Texture2D cyclingLandingImage;

    private Label gameModeTitle;
    private Button playAgainButton;
    private Button goHomeButton;
    private Image landingImage;

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
        playAgainButton = root.Q<Button>("PlayAgainButton");
        goHomeButton = root.Q<Button>("GoHomeButton");
        landingImage = root.Q<Image>("Landing-Image");

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

        // Unsubscribe from MQTT
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnGameOverReceived -= OnGameOverReceived;
        }
    }

    private void OnGameOverReceived(GameOverMessage msg)
    {
        Debug.Log($"Game Over - Distance: {msg.finalDistance}m, Time: {msg.finalTime}s");

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
            Debug.Log($"[TabletPlayAgain] Updated landing image for mode: {currentGameMode}");
        }
    }
}
