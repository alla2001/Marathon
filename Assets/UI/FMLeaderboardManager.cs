using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Manages FM (registration) leaderboard - separate from game leaderboard
/// Used by Tablet FM for username validation
/// </summary>
public class FMLeaderboardManager : MonoBehaviour
{
    public static FMLeaderboardManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float requestTimeout = 3f;

    // Events
    public event Action<bool, string> OnUsernameCheckResult; // isUnique, username
    public event Action<bool, string> OnRegistrationResult; // success, message

    // Topics - different from game leaderboard
    private const string TOPIC_CHECK_USERNAME = "fm-leaderboard/check-username";
    private const string TOPIC_REGISTER = "fm-leaderboard/register";

    private string checkUsernameResponseTopic;
    private string registerResponseTopic;

    // Pending requests
    private string pendingUsernameCheck = null;
    private Coroutine usernameCheckTimeoutCoroutine = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnConnected += OnMQTTConnected;
            MQTTManager.Instance.OnRawMessageReceived += OnRawMessageReceived;

            if (MQTTManager.Instance.IsConnected)
            {
                SubscribeToResponseTopics();
            }
        }
    }

    private void OnDestroy()
    {
        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnConnected -= OnMQTTConnected;
            MQTTManager.Instance.OnRawMessageReceived -= OnRawMessageReceived;
        }
    }

    private void OnMQTTConnected()
    {
        SubscribeToResponseTopics();
    }

    private void SubscribeToResponseTopics()
    {
        if (MQTTManager.Instance == null || !MQTTManager.Instance.IsConnected)
            return;

        int stationId = MQTTManager.Instance.StationId;

        // Build station-specific response topics
        checkUsernameResponseTopic = $"fm-leaderboard/check-username/response/{stationId}";
        registerResponseTopic = $"fm-leaderboard/register/response/{stationId}";

        // Subscribe to response topics
        MQTTManager.Instance.Subscribe(checkUsernameResponseTopic);
        MQTTManager.Instance.Subscribe(registerResponseTopic);

        Debug.Log($"[FMLeaderboardManager] Subscribed to FM response topics for station {stationId}");
    }

    private void OnRawMessageReceived(string topic, string message)
    {
        if (topic == checkUsernameResponseTopic)
        {
            HandleUsernameCheckResponse(message);
        }
        else if (topic == registerResponseTopic)
        {
            HandleRegisterResponse(message);
        }
    }

    // ========================================
    // Username Check
    // ========================================
    public void CheckUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || username == "Name")
        {
            OnUsernameCheckResult?.Invoke(false, username);
            return;
        }

        if (MQTTManager.Instance == null || !MQTTManager.Instance.IsConnected)
        {
            // Backend not available, assume unique
            Debug.Log($"[FMLeaderboardManager] MQTT not connected, assuming username '{username}' is unique");
            OnUsernameCheckResult?.Invoke(true, username);
            return;
        }

        pendingUsernameCheck = username;

        // Send request
        var request = new FMUsernameCheckRequest
        {
            username = username,
            stationId = MQTTManager.Instance.StationId
        };

        string json = JsonUtility.ToJson(request);
        MQTTManager.Instance.Subscribe(checkUsernameResponseTopic);
        PublishRaw(TOPIC_CHECK_USERNAME, json);

        Debug.Log($"[FMLeaderboardManager] Checking FM username: {username}");

        // Start timeout coroutine
        if (usernameCheckTimeoutCoroutine != null)
        {
            StopCoroutine(usernameCheckTimeoutCoroutine);
        }
        usernameCheckTimeoutCoroutine = StartCoroutine(UsernameCheckTimeout(username));
    }

    private IEnumerator UsernameCheckTimeout(string username)
    {
        yield return new WaitForSeconds(requestTimeout);

        // If we still have a pending check for this username, assume unique
        if (pendingUsernameCheck == username)
        {
            Debug.Log($"[FMLeaderboardManager] Username check timeout, assuming '{username}' is unique");
            pendingUsernameCheck = null;
            OnUsernameCheckResult?.Invoke(true, username);
        }
    }

    private void HandleUsernameCheckResponse(string message)
    {
        try
        {
            var response = JsonUtility.FromJson<FMUsernameCheckResponse>(message);

            // Only process if this is the username we're waiting for
            if (pendingUsernameCheck == response.username)
            {
                if (usernameCheckTimeoutCoroutine != null)
                {
                    StopCoroutine(usernameCheckTimeoutCoroutine);
                    usernameCheckTimeoutCoroutine = null;
                }

                pendingUsernameCheck = null;

                Debug.Log($"[FMLeaderboardManager] FM Username '{response.username}' isUnique: {response.isUnique}");
                OnUsernameCheckResult?.Invoke(response.isUnique, response.username);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FMLeaderboardManager] Error parsing username check response: {ex.Message}");
        }
    }

    // ========================================
    // Register User
    // ========================================
    public void RegisterUser(string username)
    {
        if (MQTTManager.Instance == null || !MQTTManager.Instance.IsConnected)
        {
            Debug.LogWarning("[FMLeaderboardManager] Cannot register - MQTT not connected");
            // Still fire success for now since user said no storage
            OnRegistrationResult?.Invoke(true, "Registered (offline)");
            return;
        }

        var request = new FMRegisterRequest
        {
            username = username,
            stationId = MQTTManager.Instance.StationId,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        string json = JsonUtility.ToJson(request);
        PublishRaw(TOPIC_REGISTER, json);

        Debug.Log($"[FMLeaderboardManager] Registering FM user: {username}");

        // For now, assume success immediately (user said no storage)
        OnRegistrationResult?.Invoke(true, "Registration sent");
    }

    private void HandleRegisterResponse(string message)
    {
        try
        {
            var response = JsonUtility.FromJson<FMRegisterResponse>(message);
            Debug.Log($"[FMLeaderboardManager] Register response: {response.message}");
            OnRegistrationResult?.Invoke(response.success, response.message);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FMLeaderboardManager] Error parsing register response: {ex.Message}");
        }
    }

    private void PublishRaw(string topic, string json)
    {
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            MQTTManager.Instance.PublishRaw(topic, json);
        }
    }
}

// ========================================
// FM Message Types
// ========================================
[Serializable]
public class FMUsernameCheckRequest
{
    public string username;
    public int stationId;
}

[Serializable]
public class FMUsernameCheckResponse
{
    public bool success;
    public string username;
    public bool isUnique;
    public bool exists;
    public int stationId;
}

[Serializable]
public class FMRegisterRequest
{
    public string username;
    public int stationId;
    public string timestamp;
}

[Serializable]
public class FMRegisterResponse
{
    public bool success;
    public string message;
    public string username;
    public int stationId;
}
