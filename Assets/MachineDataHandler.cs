using UnityEngine;
using MarathonMQTT;
using System;

/// <summary>
/// Receives machine data from MQTT and feeds it to the SplinePlayerController
/// Subscribes to: treadmill_{deviceId}/pulse
/// </summary>
public class MachineDataHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplinePlayerController playerController;

    [Header("Machine Settings")]
    [SerializeField] private string deviceId = "treadmill_1"; // Device ID for this station
    [SerializeField] private string gameMode = "rowing";
    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField] private bool useMachineData = false; // Enable when machine is connected

    private string currentTopic = "";

    private void Start()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<SplinePlayerController>();
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

        // Subscribe to treadmill topic: treadmill_{deviceId}/pulse
        currentTopic = $"{deviceId}/pulse";
        MQTTManager.Instance.Subscribe(currentTopic);

        // Also subscribe to the custom event for processing these messages
        MQTTManager.Instance.OnRawMessageReceived += OnRawMachineMessage;

        Debug.Log($"[MachineDataHandler] Subscribed to {currentTopic}");
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

        // Treadmill sends speed as integer (km/h * 100), so divide by 100 first
        float speedKmh = data.speed_kmh / 100f;

        // Convert speed from km/h to m/s (divide by 3.6)
        float speedMs = speedKmh / 3.6f;

        // Apply speed multiplier based on game mode
        float adjustedSpeed = speedMs * speedMultiplier;

        // Update player controller with speed
        playerController.SetMachineSpeed(adjustedSpeed);

        // Use treadmill's distance directly (more accurate than calculating from speed)
        playerController.SetMachineDistance(data.distance_m);

        // Log for debugging
        Debug.Log($"[MachineDataHandler] Device: {data.device}, Speed: {speedMs:F2} m/s ({speedKmh:F2} km/h), Distance: {data.distance_m:F2}m, Pulse: {data.pulse}");
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
    }

    public void EnableMachineInput(bool enable)
    {
        useMachineData = enable;
        Debug.Log($"[MachineDataHandler] Machine input {(enable ? "enabled" : "disabled")}");
    }

    public void SetDeviceId(string id)
    {
        deviceId = id;
        Debug.Log($"[MachineDataHandler] Device ID set to: {deviceId}");
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
