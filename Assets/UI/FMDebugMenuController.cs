using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>
/// Debug menu for FM Tablet scene only.
/// Separate from DebugMenuController (which is for the main Tablet/PC game).
/// Provides MQTT connection settings and left/right tablet selection.
/// </summary>
public class FMDebugMenuController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    [Header("Gesture Settings")]
    [SerializeField] private int requiredFingers = 3;
    [SerializeField] private int requiredTaps = 2;
    [SerializeField] private float tapTimeWindow = 0.5f;
    [SerializeField] private float tapTimeout = 1f;

    private VisualElement debugRoot;
    private bool isDebugMenuVisible = false;

    // MQTT UI
    private TextField brokerAddressInput;
    private TextField brokerPortInput;
    private TextField usernameInput;
    private TextField passwordInput;
    private Label connectionStatus;
    private Button connectButton;
    private Button disconnectButton;
    private Button closeButton;

    // Tablet Side UI
    private Button leftButton;
    private Button rightButton;
    private Label selectedSideLabel;

    // Game Control
    private Button cancelGameButton;

    // Log Window
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
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("[FMDebugMenu] UIDocument not found!");
            return;
        }

        var root = uiDocument.rootVisualElement;
        debugRoot = root.Q<VisualElement>("DebugRoot");

        // MQTT elements
        brokerAddressInput = root.Q<TextField>("BrokerAddressInput");
        brokerPortInput = root.Q<TextField>("BrokerPortInput");
        usernameInput = root.Q<TextField>("UsernameInput");
        passwordInput = root.Q<TextField>("PasswordInput");
        connectionStatus = root.Q<Label>("ConnectionStatus");
        connectButton = root.Q<Button>("ConnectButton");
        disconnectButton = root.Q<Button>("DisconnectButton");
        closeButton = root.Q<Button>("CloseButton");

        // Tablet side elements
        leftButton = root.Q<Button>("LeftButton");
        rightButton = root.Q<Button>("RightButton");
        selectedSideLabel = root.Q<Label>("SelectedSideLabel");

        // Game control elements
        cancelGameButton = root.Q<Button>("CancelGameButton");

        // Log elements
        logContainer = root.Q<VisualElement>("LogContainer");
        logScrollView = root.Q<ScrollView>("LogScrollView");
        clearLogButton = root.Q<Button>("ClearLogButton");

        // Register callbacks
        if (connectButton != null) connectButton.clicked += OnConnectClicked;
        if (disconnectButton != null) disconnectButton.clicked += OnDisconnectClicked;
        if (closeButton != null) closeButton.clicked += CloseDebugMenu;
        if (leftButton != null) leftButton.clicked += () => SelectSide("left");
        if (rightButton != null) rightButton.clicked += () => SelectSide("right");
        if (cancelGameButton != null) cancelGameButton.clicked += OnCancelGameClicked;
        if (clearLogButton != null) clearLogButton.clicked += ClearLogs;

        Application.logMessageReceived += OnLogMessageReceived;

        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnConnected += OnMQTTConnected;
            MQTTManager.Instance.OnDisconnected += OnMQTTDisconnected;
            MQTTManager.Instance.OnConnectionFailed += OnMQTTConnectionFailed;
        }

        LoadSettings();
        UpdateConnectionStatus();
        UpdateSideButtonStyles();
        HideDebugMenu();
    }

    private void OnDisable()
    {
        if (connectButton != null) connectButton.clicked -= OnConnectClicked;
        if (disconnectButton != null) disconnectButton.clicked -= OnDisconnectClicked;
        if (closeButton != null) closeButton.clicked -= CloseDebugMenu;
        if (cancelGameButton != null) cancelGameButton.clicked -= OnCancelGameClicked;
        if (clearLogButton != null) clearLogButton.clicked -= ClearLogs;

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

    // ========================================
    // Gesture Detection (same pattern as main debug menu)
    // ========================================
    private void DetectGesture()
    {
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

        // Shift+D for editor testing
        if (Input.GetKeyDown(KeyCode.D) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            OnMultiFingerTap();
        }
    }

    private void OnMultiFingerTap()
    {
        float currentTime = Time.time;

        if (currentTime - lastTapTime <= tapTimeWindow)
        {
            currentTapCount++;
        }
        else
        {
            currentTapCount = 1;
        }

        lastTapTime = currentTime;

        if (currentTapCount >= requiredTaps)
        {
            ToggleDebugMenu();
            currentTapCount = 0;
        }

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

    // ========================================
    // Debug Menu Visibility
    // ========================================
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
        if (isDebugMenuVisible) HideDebugMenu();
        else ShowDebugMenu();
    }

    public void CloseDebugMenu()
    {
        SaveSettings();
        HideDebugMenu();
    }

    // ========================================
    // MQTT Connection
    // ========================================
    private void OnConnectClicked()
    {
        SaveSettings();

        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.SetBrokerAddress(brokerAddressInput.value);

            if (int.TryParse(brokerPortInput.value, out int port))
                MQTTManager.Instance.SetBrokerPort(port);

            MQTTManager.Instance.SetCredentials(usernameInput.value, passwordInput.value);
            MQTTManager.Instance.Connect();
        }
    }

    private void OnDisconnectClicked()
    {
        if (MQTTManager.Instance != null)
            MQTTManager.Instance.Disconnect();
    }

    private void OnMQTTConnected() => UpdateConnectionStatus();
    private void OnMQTTDisconnected() => UpdateConnectionStatus();
    private void OnMQTTConnectionFailed(string error)
    {
        UpdateConnectionStatus();
        Debug.LogError($"[FMDebugMenu] MQTT Connection failed: {error}");
    }

    private void UpdateConnectionStatus()
    {
        if (connectionStatus == null || MQTTManager.Instance == null) return;

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

    // ========================================
    // Cancel Game
    // ========================================
    private void OnCancelGameClicked()
    {
        var fmController = FindObjectOfType<TabletFMController>();
        if (fmController != null)
        {
            fmController.CancelGame();
            Debug.Log("[FMDebugMenu] Game cancelled - returned to start page");
        }
        else
        {
            Debug.LogWarning("[FMDebugMenu] TabletFMController not found");
        }
    }

    // ========================================
    // Tablet Side Selection
    // ========================================
    private void SelectSide(string side)
    {
        if (FMLeaderboardManager.Instance != null)
        {
            FMLeaderboardManager.Instance.SetTabletSide(side);
        }

        if (selectedSideLabel != null)
        {
            selectedSideLabel.text = side.ToUpper();
        }

        UpdateSideButtonStyles();
        SaveSettings();

        Debug.Log($"[FMDebugMenu] Tablet side set to: {side}");
    }

    private void UpdateSideButtonStyles()
    {
        string currentSide = FMLeaderboardManager.Instance != null
            ? FMLeaderboardManager.Instance.TabletSide
            : PlayerPrefs.GetString("FMTabletSide", "left");

        Color inactiveColor = new Color(0.3f, 0.3f, 0.3f);
        Color activeColor = new Color(1f, 0.55f, 0f);
        Color activeBorderColor = new Color(1f, 0.78f, 0f);

        if (leftButton != null)
        {
            bool isLeft = currentSide == "left";
            leftButton.style.backgroundColor = new StyleColor(isLeft ? activeColor : inactiveColor);
            leftButton.style.borderLeftColor = leftButton.style.borderRightColor =
                leftButton.style.borderTopColor = leftButton.style.borderBottomColor =
                new StyleColor(isLeft ? activeBorderColor : inactiveColor);
        }

        if (rightButton != null)
        {
            bool isRight = currentSide == "right";
            rightButton.style.backgroundColor = new StyleColor(isRight ? activeColor : inactiveColor);
            rightButton.style.borderLeftColor = rightButton.style.borderRightColor =
                rightButton.style.borderTopColor = rightButton.style.borderBottomColor =
                new StyleColor(isRight ? activeBorderColor : inactiveColor);
        }

        if (selectedSideLabel != null)
        {
            selectedSideLabel.text = currentSide.ToUpper();
        }
    }

    // ========================================
    // Settings
    // ========================================
    private void LoadSettings()
    {
        if (brokerAddressInput == null) return;

        if (MQTTManager.Instance != null)
        {
            brokerAddressInput.value = MQTTManager.Instance.BrokerAddress;
            brokerPortInput.value = MQTTManager.Instance.BrokerPort.ToString();
        }
        else
        {
            brokerAddressInput.value = PlayerPrefs.GetString("MQTT_BrokerAddress", "localhost");
            brokerPortInput.value = PlayerPrefs.GetInt("MQTT_BrokerPort", 1883).ToString();
        }

        if (usernameInput != null) usernameInput.value = PlayerPrefs.GetString("MQTT_Username", "");
        if (passwordInput != null) passwordInput.value = PlayerPrefs.GetString("MQTT_Password", "");
    }

    private void SaveSettings()
    {
        if (brokerAddressInput != null)
            PlayerPrefs.SetString("MQTT_BrokerAddress", brokerAddressInput.value);
        if (brokerPortInput != null && int.TryParse(brokerPortInput.value, out int port))
            PlayerPrefs.SetInt("MQTT_BrokerPort", port);
        if (usernameInput != null)
            PlayerPrefs.SetString("MQTT_Username", usernameInput.value);
        if (passwordInput != null)
            PlayerPrefs.SetString("MQTT_Password", passwordInput.value);
        PlayerPrefs.Save();
    }

    // ========================================
    // Log Window
    // ========================================
    private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        if (logContainer == null) return;

        var logEntry = new Label();
        logEntry.style.fontSize = 11;
        logEntry.style.marginBottom = 2;
        logEntry.style.whiteSpace = WhiteSpace.Normal;

        logEntry.style.color = type switch
        {
            LogType.Error or LogType.Exception => new StyleColor(new Color(1f, 0.4f, 0.4f)),
            LogType.Warning => new StyleColor(new Color(1f, 0.8f, 0.3f)),
            _ => new StyleColor(new Color(0.8f, 0.8f, 0.8f))
        };

        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        string prefix = type switch
        {
            LogType.Error => "[ERROR]",
            LogType.Exception => "[EXCEPTION]",
            LogType.Warning => "[WARN]",
            _ => "[INFO]"
        };
        logEntry.text = $"[{timestamp}] {prefix} {logString}";

        logContainer.Add(logEntry);

        if (logContainer.childCount > MAX_LOG_ENTRIES)
            logContainer.RemoveAt(0);

        if (logScrollView != null)
            logScrollView.scrollOffset = new Vector2(0, logScrollView.contentContainer.layout.height);
    }

    private void ClearLogs()
    {
        if (logContainer != null)
        {
            logContainer.Clear();
            Debug.Log("[FMDebugMenu] Logs cleared");
        }
    }
}
