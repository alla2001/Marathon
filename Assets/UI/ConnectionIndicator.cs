using UnityEngine;
using UnityEngine.UIElements;
using MarathonMQTT;
using System.Collections.Generic;

/// <summary>
/// Shows a connection status dot and FPS counter at the bottom of all UIDocuments in the scene.
/// Add this to any scene (Tablet or PC Game) to show MQTT connection state.
/// </summary>
public class ConnectionIndicator : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float fpsUpdateInterval = 0.5f;

    private List<VisualElement> dots = new List<VisualElement>();
    private List<Label> fpsLabels = new List<Label>();
    private bool lastConnectionState = false;
    private float fpsTimer = 0f;
    private int frameCount = 0;
    private float currentFps = 0f;

    private void Start()
    {
        // Wait a frame for all UIDocuments to initialize
        StartCoroutine(InjectAfterFrame());
    }

    private System.Collections.IEnumerator InjectAfterFrame()
    {
        yield return null; // Wait one frame

        InjectIntoAllDocuments();

        // Subscribe to connection events
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnConnected += UpdateDots;
            MQTTManager.Instance.OnDisconnected += UpdateDots;
        }

        UpdateDots();
    }

    private void OnDestroy()
    {
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnConnected -= UpdateDots;
            MQTTManager.Instance.OnDisconnected -= UpdateDots;
        }
    }

    private void InjectIntoAllDocuments()
    {
        dots.Clear();
        fpsLabels.Clear();

        var allDocs = FindObjectsOfType<UIDocument>();
        foreach (var doc in allDocs)
        {
            var root = doc.rootVisualElement;
            if (root == null) continue;

            // Skip if already has our indicator
            if (root.Q<VisualElement>("ConnectionIndicatorBar") != null) continue;

            // Create a bar at the bottom with dot + fps
            var bar = new VisualElement();
            bar.name = "ConnectionIndicatorBar";
            bar.style.position = Position.Absolute;
            bar.style.bottom = 10;
            bar.style.right = 10;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.pickingMode = PickingMode.Ignore;

            // FPS label
            var fpsLabel = new Label("0 FPS");
            fpsLabel.name = "FpsLabel";
            fpsLabel.style.fontSize = 14;
            fpsLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            fpsLabel.style.marginRight = 6;
            fpsLabel.pickingMode = PickingMode.Ignore;
            bar.Add(fpsLabel);
            fpsLabels.Add(fpsLabel);

            // Connection dot
            var dot = new VisualElement();
            dot.name = "ConnectionDot";
            dot.style.width = 12;
            dot.style.height = 12;
            dot.style.borderTopLeftRadius = 6;
            dot.style.borderTopRightRadius = 6;
            dot.style.borderBottomLeftRadius = 6;
            dot.style.borderBottomRightRadius = 6;
            dot.style.backgroundColor = new Color(1f, 0.3f, 0.3f, 1f);
            dot.pickingMode = PickingMode.Ignore;
            bar.Add(dot);
            dots.Add(dot);

            root.Add(bar);
        }

        Debug.Log($"[ConnectionIndicator] Injected into {dots.Count} UIDocuments");
    }

    private void Update()
    {
        // FPS calculation
        frameCount++;
        fpsTimer += Time.unscaledDeltaTime;

        if (fpsTimer >= fpsUpdateInterval)
        {
            currentFps = frameCount / fpsTimer;
            frameCount = 0;
            fpsTimer = 0f;

            string fpsText = $"{Mathf.RoundToInt(currentFps)} FPS";
            foreach (var label in fpsLabels)
            {
                if (label != null) label.text = fpsText;
            }
        }
    }

    private void UpdateDots()
    {
        bool connected = MQTTManager.Instance != null && MQTTManager.Instance.IsConnected;
        Color dotColor = connected
            ? new Color(0.3f, 0.9f, 0.3f, 1f)
            : new Color(1f, 0.3f, 0.3f, 1f);

        foreach (var dot in dots)
        {
            if (dot != null) dot.style.backgroundColor = dotColor;
        }

        if (connected != lastConnectionState)
        {
            Debug.Log($"[ConnectionIndicator] {(connected ? "CONNECTED" : "DISCONNECTED")}");
            lastConnectionState = connected;
        }
    }
}
