using UnityEngine;
using MarathonMQTT;
using System;

/// <summary>
/// Receives machine data from MQTT and feeds it to the SplinePlayerController
/// Topic format: {machineTopic}/pulse
/// Defaults based on game mode if machineTopic is empty:
///   - rowing: rowing_{stationId}/pulse
///   - running: treadmill_{stationId}/pulse
///   - cycling: cycle_{stationId}/pulse
/// </summary>
public class MachineDataHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplinePlayerController playerController;
    [SerializeField] private MainGameManager gameManager;

    [Header("Machine Settings")]
    [SerializeField] private string customMachineTopic = ""; // Custom topic override (leave empty for auto)
    [SerializeField] private string gameMode = "rowing";
    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField] private bool useMachineData = false; // Enable when machine is connected

    private string currentTopic = "";
    private int stationId = 1;
    private float localDistance = 0f; // Distance calculated locally from speed

    private void Start()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<SplinePlayerController>();
        }
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<MainGameManager>();
        }

        // Subscribe to the machine topic
        if (MQTTManager.Instance != null)
        {
            if (MQTTManager.Instance.IsConnected)
            {
                SubscribeToMachineTopic();
            }
            else
            {
                MQTTManager.Instance.OnConnected += OnMQTTConnected;
            }
        }
    }

    private void OnDestroy()
    {
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnConnected -= OnMQTTConnected;
            MQTTManager.Instance.OnRawMessageReceived -= OnRawMachineMessage;
        }
    }

    private void OnMQTTConnected()
    {
        SubscribeToMachineTopic();
    }

    private void SubscribeToMachineTopic()
    {
        if (MQTTManager.Instance == null || !MQTTManager.Instance.IsConnected)
            return;

        // Get station ID from MQTTManager
        stationId = MQTTManager.Instance.StationId;

        // Unsubscribe from old topic if exists
        if (!string.IsNullOrEmpty(currentTopic))
        {
            MQTTManager.Instance.Unsubscribe(currentTopic);
        }

        // Determine topic: use custom if set, otherwise use default based on game mode
        string baseTopic = GetMachineTopic();
        currentTopic = $"{baseTopic}/pulse";

        MQTTManager.Instance.Subscribe(currentTopic);

        // Register for raw messages if not already
        MQTTManager.Instance.OnRawMessageReceived -= OnRawMachineMessage;
        MQTTManager.Instance.OnRawMessageReceived += OnRawMachineMessage;

        Debug.Log($"[MachineDataHandler] Subscribed to {currentTopic} (mode: {gameMode}, station: {stationId})");
    }

    /// <summary>
    /// Gets the machine topic based on custom setting or game mode default
    /// </summary>
    private string GetMachineTopic()
    {
        // If custom topic is set, use it
        if (!string.IsNullOrWhiteSpace(customMachineTopic))
        {
            return customMachineTopic;
        }

        // Otherwise use default based on game mode
        return gameMode.ToLower() switch
        {
            "rowing" => $"rowing_{stationId}",
            "running" => $"treadmill_{stationId}",
            "cycling" => $"cycle_{stationId}",
            _ => $"treadmill_{stationId}"
        };
    }

    /// <summary>
    /// Gets the current topic being used
    /// </summary>
    public string GetCurrentTopic()
    {
        return currentTopic;
    }

    private void OnRawMachineMessage(string topic, string message)
    {
        // Only process messages from our machine topic
        if (topic != currentTopic || !useMachineData)
            return;

        try
        {
            // Parse the treadmill message
            var machineData = JsonUtility.FromJson<TreadmillPulseMessage>(message);
            ProcessMachineData(machineData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MachineDataHandler] Failed to parse message: {ex.Message}");
        }
    }

    private void ProcessMachineData(TreadmillPulseMessage data)
    {
        if (playerController == null)
            return;

        // Speed comes in as km/h (e.g. 18.08)
        float speedKmh = data.speed_kmh;

        // Convert speed from km/h to m/s (divide by 3.6)
        float speedMs = speedKmh / 3.6f;

        // Apply speed multiplier based on game mode
        float adjustedSpeed = speedMs * speedMultiplier;

        // Reset idle timer if machine is reporting non-zero speed
        if (gameManager != null && speedKmh > 0f)
        {
            gameManager.OnMachineDataReceived();
        }

        // Calculate distance locally from speed and delta time
        float deltaSeconds = data.dt_ms / 1000f;
        if (deltaSeconds > 0f && adjustedSpeed > 0f)
        {
            localDistance += adjustedSpeed * deltaSeconds;
        }

        // Update player controller with speed and locally calculated distance
        playerController.SetMachineSpeed(adjustedSpeed);
        playerController.SetMachineDistance(localDistance);

        // Log for debugging
        Debug.Log($"[MachineDataHandler] Device: {data.device}, Speed: {speedMs:F2} m/s ({speedKmh:F2} km/h), LocalDist: {localDistance:F2}m, Pulse: {data.pulse}");
    }

    // For manual testing without machine
    private void Update()
    {
        if (!useMachineData && Input.GetKeyDown(KeyCode.Space))
        {
            // Simulate machine stroke/step
            if (playerController != null)
            {
                playerController.AddSpeedImpulse(2f);
            }
        }
    }

    public void SetGameMode(string mode)
    {
        gameMode = mode;

        // Adjust speed multiplier based on game mode
        speedMultiplier = mode switch
        {
            "rowing" => 1f,
            "running" => 1.2f,
            "cycling" => 1.5f,
            _ => 1f
        };

        // Resubscribe to topic (will use new default based on game mode if no custom topic)
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            SubscribeToMachineTopic();
        }
    }

    public void EnableMachineInput(bool enable)
    {
        useMachineData = enable;
        Debug.Log($"[MachineDataHandler] Machine input {(enable ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Resets the locally calculated distance (call when starting a new game)
    /// </summary>
    public void ResetDistance()
    {
        localDistance = 0f;
        Debug.Log("[MachineDataHandler] Local distance reset");
    }

    /// <summary>
    /// Sets a custom machine topic. If empty, will use default based on game mode.
    /// </summary>
    public void SetCustomTopic(string topic)
    {
        customMachineTopic = topic;
        Debug.Log($"[MachineDataHandler] Custom topic set to: {(string.IsNullOrEmpty(topic) ? "(auto)" : topic)}");

        // Resubscribe with new topic
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            SubscribeToMachineTopic();
        }
    }

    /// <summary>
    /// Gets the custom machine topic (empty means using default)
    /// </summary>
    public string GetCustomTopic()
    {
        return customMachineTopic;
    }
}

/// <summary>
/// Message format from treadmill device
/// Topic: treadmill_{deviceId}/pulse
/// </summary>
[Serializable]
public class TreadmillPulseMessage
{
    public string device;        // Device ID (e.g., "treadmill_1")
    public int sessionId;        // Current session ID
    public int pulse;            // Heart rate / pulse
    public float distance_m;     // Distance in meters
    public float speed_kmh;      // Speed in km/h
    public int dt_ms;            // Delta time in milliseconds
    public long ts_ms;           // Timestamp in milliseconds
}
