using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages FM leaderboard - separate from game leaderboard.
/// Supports left/right tablet side selection.
/// Does NOT touch any shared game code (MQTTManager is read-only).
/// </summary>
public class FMLeaderboardManager : MonoBehaviour
{
    public static FMLeaderboardManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float requestTimeout = 3f;
    [SerializeField] private string tabletSide = "left"; // "left" or "right"

    // Events
    public event Action<bool, string> OnUsernameCheckResult; // isUnique, username
    public event Action<bool, string> OnRegistrationResult; // success, message
    public event Action<string> OnGameStatusReceived; // status string
    public event Action<FMTop10Message> OnTop10Received; // top 10 data

    // Topics built from side
    private string checknameTopic;
    private string checknameResponseTopic;
    private string writeTopic = "MarathonFM/leaderboard/write";
    private string top10Topic = "MarathonFM/leaderboard/top10";
    private string nameTopic;
    private string statusTopic;

    // Pending requests
    private string pendingUsernameCheck = null;
    private Coroutine usernameCheckTimeoutCoroutine = null;

    public string TabletSide => tabletSide;

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
        // Load saved side preference
        tabletSide = PlayerPrefs.GetString("FMTabletSide", "left");

        BuildTopics();

        if (MQTTManager.Instance != null)
        {
            MQTTManager.Instance.OnConnected += OnMQTTConnected;
            MQTTManager.Instance.OnRawMessageReceived += OnRawMessageReceived;

            if (MQTTManager.Instance.IsConnected)
            {
                SubscribeToTopics();
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
        SubscribeToTopics();
    }

    private void BuildTopics()
    {
        checknameTopic = $"MarathonFM/leaderboard/{tabletSide}/checkname";
        checknameResponseTopic = $"MarathonFM/leaderboard/{tabletSide}/checkname/response";
        nameTopic = $"MarathonFM/{tabletSide}/name";
        statusTopic = $"MarathonFM/{tabletSide}/status";
    }

    private void SubscribeToTopics()
    {
        if (MQTTManager.Instance == null || !MQTTManager.Instance.IsConnected)
            return;

        MQTTManager.Instance.Subscribe(checknameResponseTopic);
        MQTTManager.Instance.Subscribe(top10Topic);
        MQTTManager.Instance.Subscribe(statusTopic);

        Debug.Log($"[FMLeaderboardManager] Subscribed (side: {tabletSide})");
    }

    private void UnsubscribeFromTopics()
    {
        if (MQTTManager.Instance == null || !MQTTManager.Instance.IsConnected)
            return;

        MQTTManager.Instance.Unsubscribe(checknameResponseTopic);
        MQTTManager.Instance.Unsubscribe(statusTopic);
        // Keep top10 subscribed — it's the same for both sides
    }

    /// <summary>
    /// Switch tablet side (left/right). Resubscribes to correct topics.
    /// </summary>
    public void SetTabletSide(string side)
    {
        side = side.ToLower();
        if (side != "left" && side != "right") return;
        if (side == tabletSide) return;

        UnsubscribeFromTopics();
        tabletSide = side;
        PlayerPrefs.SetString("FMTabletSide", side);
        PlayerPrefs.Save();
        BuildTopics();
        SubscribeToTopics();

        Debug.Log($"[FMLeaderboardManager] Switched to {tabletSide} tablet");
    }

    private void OnRawMessageReceived(string topic, string message)
    {
        if (topic == checknameResponseTopic)
        {
            HandleUsernameCheckResponse(message);
        }
        else if (topic == top10Topic)
        {
            HandleTop10(message);
        }
        else if (topic == statusTopic)
        {
            HandleGameStatus(message);
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
            Debug.Log($"[FMLeaderboardManager] MQTT not connected, assuming username '{username}' is unique");
            OnUsernameCheckResult?.Invoke(true, username);
            return;
        }

        pendingUsernameCheck = username;

        var request = new FMUsernameCheckRequest { username = username };
        string json = JsonUtility.ToJson(request);
        PublishRaw(checknameTopic, json);

        Debug.Log($"[FMLeaderboardManager] Checking username: {username} on {checknameTopic}");

        if (usernameCheckTimeoutCoroutine != null)
        {
            StopCoroutine(usernameCheckTimeoutCoroutine);
        }
        usernameCheckTimeoutCoroutine = StartCoroutine(UsernameCheckTimeout(username));
    }

    private IEnumerator UsernameCheckTimeout(string username)
    {
        yield return new WaitForSeconds(requestTimeout);

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

            if (pendingUsernameCheck == response.username)
            {
                if (usernameCheckTimeoutCoroutine != null)
                {
                    StopCoroutine(usernameCheckTimeoutCoroutine);
                    usernameCheckTimeoutCoroutine = null;
                }

                pendingUsernameCheck = null;

                Debug.Log($"[FMLeaderboardManager] Username '{response.username}' isUnique: {response.isUnique}");
                OnUsernameCheckResult?.Invoke(response.isUnique, response.username);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FMLeaderboardManager] Error parsing username check response: {ex.Message}");
        }
    }

    // ========================================
    // Write Entry
    // ========================================
    public void WriteEntry(string username, float distance, float time)
    {
        if (MQTTManager.Instance == null || !MQTTManager.Instance.IsConnected)
        {
            Debug.LogWarning("[FMLeaderboardManager] Cannot write - MQTT not connected");
            OnRegistrationResult?.Invoke(true, "Registered (offline)");
            return;
        }

        var request = new FMWriteRequest
        {
            username = username,
            distance = distance,
            time = time
        };

        string json = JsonUtility.ToJson(request);
        PublishRaw(writeTopic, json);

        Debug.Log($"[FMLeaderboardManager] Writing entry: {username} distance={distance} time={time}");
        OnRegistrationResult?.Invoke(true, "Entry submitted");
    }

    /// <summary>
    /// Register user (write with 0 distance/time, updated later by game)
    /// </summary>
    public void RegisterUser(string username)
    {
        WriteEntry(username, 0f, 0f);
    }

    // ========================================
    // Send Username to Game
    // ========================================
    public void SendUsername(string username)
    {
        if (MQTTManager.Instance == null || !MQTTManager.Instance.IsConnected)
        {
            Debug.LogWarning("[FMLeaderboardManager] Cannot send username - MQTT not connected");
            return;
        }

        PublishRaw(nameTopic, username);

        Debug.Log($"[FMLeaderboardManager] Sent username '{username}' on {nameTopic}");
    }

    // ========================================
    // Top 10
    // ========================================
    private void HandleTop10(string message)
    {
        try
        {
            var top10 = JsonUtility.FromJson<FMTop10Message>(message);
            OnTop10Received?.Invoke(top10);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FMLeaderboardManager] Error parsing top 10: {ex.Message}");
        }
    }

    // ========================================
    // Game Status
    // ========================================
    private void HandleGameStatus(string message)
    {
        // Status comes as raw string: "Game Idle" or "Game Active"
        string status = message.Trim().Trim('"');
        Debug.Log($"[FMLeaderboardManager] Game status: {status}");
        OnGameStatusReceived?.Invoke(status);
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
}

[Serializable]
public class FMUsernameCheckResponse
{
    public bool success;
    public string username;
    public bool isUnique;
    public bool exists;
}

[Serializable]
public class FMWriteRequest
{
    public string username;
    public float distance;
    public float time;
}


[Serializable]
public class FMTop10Message
{
    public string messageType;
    public long timestamp;
    public float totalDistances;
    public List<FMTop10Entry> leaderboard;
}

[Serializable]
public class FMTop10Entry
{
    public int rank;
    public string username;
    public int score;
    public float distance;
    public float time;
}
