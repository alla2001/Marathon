using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Manages leaderboard communication via MQTT
/// Handles username checking, score submission, and top 10 retrieval
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float requestTimeout = 3f; // Assume success if no response

    // Events
    public event Action<bool, string> OnUsernameCheckResult; // isUnique, username
    public event Action<bool, string> OnScoreSubmitResult; // success, message
    public event Action<LeaderboardEntry[]> OnTop10Received;

    // Topics
    private const string TOPIC_CHECK_USERNAME = "leaderboard/check-username";
    private const string TOPIC_SUBMIT = "leaderboard/submit";
    private const string TOPIC_TOP10_REQUEST = "leaderboard/top10/request";

    private string checkUsernameResponseTopic;
    private string submitResponseTopic;
    private string top10ResponseTopic;

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
        checkUsernameResponseTopic = $"leaderboard/check-username/response/{stationId}";
        submitResponseTopic = $"leaderboard/submit/response/{stationId}";
        top10ResponseTopic = $"leaderboard/top10/response/{stationId}";

        // Subscribe to response topics
        MQTTManager.Instance.Subscribe(checkUsernameResponseTopic);
        MQTTManager.Instance.Subscribe(submitResponseTopic);
        MQTTManager.Instance.Subscribe(top10ResponseTopic);

        Debug.Log($"[LeaderboardManager] Subscribed to response topics for station {stationId}");
    }

    private void OnRawMessageReceived(string topic, string message)
    {
        if (topic == checkUsernameResponseTopic)
        {
            HandleUsernameCheckResponse(message);
        }
        else if (topic == submitResponseTopic)
        {
            HandleSubmitResponse(message);
        }
        else if (topic == top10ResponseTopic)
        {
            HandleTop10Response(message);
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
            Debug.Log($"[LeaderboardManager] MQTT not connected, assuming username '{username}' is unique");
            OnUsernameCheckResult?.Invoke(true, username);
            return;
        }

        pendingUsernameCheck = username;

        // Send request
        var request = new UsernameCheckRequest
        {
            username = username,
            stationId = MQTTManager.Instance.StationId
        };

        string json = JsonUtility.ToJson(request);
        MQTTManager.Instance.Subscribe(checkUsernameResponseTopic); // Ensure subscribed
        PublishRaw(TOPIC_CHECK_USERNAME, json);

        Debug.Log($"[LeaderboardManager] Checking username: {username}");

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
            Debug.Log($"[LeaderboardManager] Username check timeout, assuming '{username}' is unique");
            pendingUsernameCheck = null;
            OnUsernameCheckResult?.Invoke(true, username);
        }
    }

    private void HandleUsernameCheckResponse(string message)
    {
        try
        {
            var response = JsonUtility.FromJson<UsernameCheckResponse>(message);

            // Only process if this is the username we're waiting for
            if (pendingUsernameCheck == response.username)
            {
                if (usernameCheckTimeoutCoroutine != null)
                {
                    StopCoroutine(usernameCheckTimeoutCoroutine);
                    usernameCheckTimeoutCoroutine = null;
                }

                pendingUsernameCheck = null;

                Debug.Log($"[LeaderboardManager] Username '{response.username}' isUnique: {response.isUnique}");
                OnUsernameCheckResult?.Invoke(response.isUnique, response.username);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LeaderboardManager] Error parsing username check response: {ex.Message}");
        }
    }

    // ========================================
    // Submit Score
    // ========================================
    public void SubmitScore(string username, int score, float distance, float time)
    {
        if (MQTTManager.Instance == null || !MQTTManager.Instance.IsConnected)
        {
            Debug.LogWarning("[LeaderboardManager] Cannot submit score - MQTT not connected");
            OnScoreSubmitResult?.Invoke(false, "Not connected");
            return;
        }

        var request = new ScoreSubmitRequest
        {
            username = username,
            score = score,
            distance = distance,
            time = time,
            stationId = MQTTManager.Instance.StationId
        };

        string json = JsonUtility.ToJson(request);
        PublishRaw(TOPIC_SUBMIT, json);

        Debug.Log($"[LeaderboardManager] Submitting score for {username}: {score}");
    }

    private void HandleSubmitResponse(string message)
    {
        try
        {
            var response = JsonUtility.FromJson<ScoreSubmitResponse>(message);
            Debug.Log($"[LeaderboardManager] Submit response: {response.message}");
            OnScoreSubmitResult?.Invoke(response.success, response.message);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LeaderboardManager] Error parsing submit response: {ex.Message}");
        }
    }

    // ========================================
    // Top 10
    // ========================================
    public void RequestTop10()
    {
        if (MQTTManager.Instance == null || !MQTTManager.Instance.IsConnected)
        {
            Debug.LogWarning("[LeaderboardManager] Cannot request top 10 - MQTT not connected");
            OnTop10Received?.Invoke(new LeaderboardEntry[0]);
            return;
        }

        var request = new Top10Request
        {
            stationId = MQTTManager.Instance.StationId
        };

        string json = JsonUtility.ToJson(request);
        PublishRaw(TOPIC_TOP10_REQUEST, json);

        Debug.Log("[LeaderboardManager] Requesting top 10");
    }

    private void HandleTop10Response(string message)
    {
        try
        {
            var response = JsonUtility.FromJson<Top10Response>(message);
            Debug.Log($"[LeaderboardManager] Received top 10 ({response.entries?.Length ?? 0} entries)");
            OnTop10Received?.Invoke(response.entries ?? new LeaderboardEntry[0]);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LeaderboardManager] Error parsing top 10 response: {ex.Message}");
        }
    }

    // ========================================
    // Helper
    // ========================================
    private void PublishRaw(string topic, string json)
    {
        if (MQTTManager.Instance != null && MQTTManager.Instance.IsConnected)
        {
            MQTTManager.Instance.PublishRaw(topic, json);
        }
    }
}

// ========================================
// Message Types
// ========================================
[Serializable]
public class UsernameCheckRequest
{
    public string username;
    public int stationId;
}

[Serializable]
public class UsernameCheckResponse
{
    public bool success;
    public string username;
    public bool isUnique;
    public bool exists;
    public int stationId;
}

[Serializable]
public class ScoreSubmitRequest
{
    public string username;
    public int score;
    public float distance;
    public float time;
    public int stationId;
}

[Serializable]
public class ScoreSubmitResponse
{
    public bool success;
    public string message;
    public string username;
    public int score;
    public int stationId;
}

[Serializable]
public class Top10Request
{
    public int stationId;
}

[Serializable]
public class Top10Response
{
    public bool success;
    public LeaderboardEntry[] entries;
    public int stationId;
}

[Serializable]
public class LeaderboardEntry
{
    public int rank;
    public string username;
    public int score;
    public float distance;
    public float time;
}
