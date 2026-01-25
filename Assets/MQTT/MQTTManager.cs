using System;
using System.Collections.Generic;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using MarathonMQTT;

public class MQTTManager : MonoBehaviour
{
    public static MQTTManager Instance { get; private set; }

    [Header("MQTT Broker Settings")]
    [SerializeField] private string brokerAddress = "localhost";
    [SerializeField] private int brokerPort = 1883;
    [SerializeField] private string clientId = "MarathonClient";
    [SerializeField] private string username = "";
    [SerializeField] private string password = "";

    [Header("Station Settings")]
    [SerializeField] private int stationId = 1; // Unique ID for this station
    [SerializeField] private bool useStationTopics = true; // Use station-specific topics

    [Header("Connection Settings")]
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private bool useEncryption = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private MqttClient mqttClient;
    private bool isConnected = false;
    private Queue<Action> mainThreadActions = new Queue<Action>();

    // Events for message handling
    public event Action<StartGameMessage> OnStartGameReceived;
    public event Action<PauseGameMessage> OnPauseGameReceived;
    public event Action<ResumeGameMessage> OnResumeGameReceived;
    public event Action<ResetGameMessage> OnResetGameReceived;
    public event Action<GameModeMessage> OnGameModeReceived;
    public event Action<CountdownMessage> OnCountdownReceived;
    public event Action<GameDataMessage> OnGameDataReceived;
    public event Action<GameOverMessage> OnGameOverReceived;
    public event Action<GameStateMessage> OnGameStateReceived;
    public event Action<MachineDataMessage> OnMachineDataReceived;
    public event Action<GameConfigMessage> OnGameConfigReceived;

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnConnectionFailed;

    // Raw message event for non-standard topics (like treadmill data)
    public event Action<string, string> OnRawMessageReceived; // topic, message

    private void Awake()
    {
        // Singleton pattern
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
        // Load settings from PlayerPrefs (allows persistence across restarts)
        LoadSettingsFromPlayerPrefs();

        if (autoConnect)
        {
            Connect();
        }
    }

    private void LoadSettingsFromPlayerPrefs()
    {
        // Check if we're on Game PC or Tablet based on prefix
        // Game PC uses "GamePC_MQTT_" prefix, Tablet uses "MQTT_" prefix

        // Try Game PC settings first
        if (PlayerPrefs.HasKey("GamePC_MQTT_BrokerAddress"))
        {
            brokerAddress = PlayerPrefs.GetString("GamePC_MQTT_BrokerAddress", brokerAddress);
            brokerPort = PlayerPrefs.GetInt("GamePC_MQTT_BrokerPort", brokerPort);
            stationId = PlayerPrefs.GetInt("GamePC_MQTT_StationId", stationId);
            username = PlayerPrefs.GetString("GamePC_MQTT_Username", username);
            password = PlayerPrefs.GetString("GamePC_MQTT_Password", password);
            LogDebug($"Loaded Game PC MQTT settings from PlayerPrefs - Broker: {brokerAddress}:{brokerPort}, Station: {stationId}");
        }
        // Then try Tablet settings
        else if (PlayerPrefs.HasKey("MQTT_BrokerAddress"))
        {
            brokerAddress = PlayerPrefs.GetString("MQTT_BrokerAddress", brokerAddress);
            brokerPort = PlayerPrefs.GetInt("MQTT_BrokerPort", brokerPort);
            stationId = PlayerPrefs.GetInt("MQTT_StationId", stationId);
            username = PlayerPrefs.GetString("MQTT_Username", username);
            password = PlayerPrefs.GetString("MQTT_Password", password);
            LogDebug($"Loaded Tablet MQTT settings from PlayerPrefs - Broker: {brokerAddress}:{brokerPort}, Station: {stationId}");
        }
        else
        {
            LogDebug("No saved MQTT settings found, using defaults from inspector");
        }
    }

    private void Update()
    {
        // Execute queued actions on main thread
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue()?.Invoke();
            }
        }
    }

    public void Connect()
    {
        try
        {
            // Disconnect first if already connected (allows re-connection and re-subscription)
            if (isConnected)
            {
                LogDebug("Already connected. Disconnecting to reconnect...");
                Disconnect();
            }

            LogDebug($"Connecting to MQTT broker at {brokerAddress}:{brokerPort}");

            // Create MQTT client
            mqttClient = new MqttClient(brokerAddress, brokerPort, useEncryption, null, null, MqttSslProtocols.None);

            // Register callbacks
            mqttClient.MqttMsgPublishReceived += OnMessageReceived;
            mqttClient.ConnectionClosed += OnConnectionClosed;

            // Generate unique client ID if needed
            string uniqueClientId = string.IsNullOrEmpty(clientId) ? Guid.NewGuid().ToString() : clientId + "_" + Guid.NewGuid().ToString().Substring(0, 8);

            // Connect
            if (string.IsNullOrEmpty(username))
            {
                mqttClient.Connect(uniqueClientId);
            }
            else
            {
                mqttClient.Connect(uniqueClientId, username, password);
            }

            isConnected = true;
            LogDebug("Successfully connected to MQTT broker");

            QueueMainThreadAction(() => OnConnected?.Invoke());
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to connect to MQTT broker: {ex.Message}");
            QueueMainThreadAction(() => OnConnectionFailed?.Invoke(ex.Message));
        }
    }

    public void Disconnect()
    {
        if (mqttClient != null && isConnected)
        {
            mqttClient.Disconnect();
            isConnected = false;
            LogDebug("Disconnected from MQTT broker");
        }
    }

    public void Subscribe(string topic)
    {
        if (!isConnected)
        {
            LogDebug("Cannot subscribe: Not connected to MQTT broker");
            return;
        }

        mqttClient.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
        LogDebug($"Subscribed to topic: {topic}");
    }

    public void Unsubscribe(string topic)
    {
        if (!isConnected)
        {
            return;
        }

        mqttClient.Unsubscribe(new string[] { topic });
        LogDebug($"Unsubscribed from topic: {topic}");
    }

    public void Publish(string topic, MQTTMessage message)
    {
        if (!isConnected)
        {
            LogDebug("Cannot publish: Not connected to MQTT broker");
            return;
        }

        string json = MessageSerializer.Serialize(message);
        mqttClient.Publish(topic, System.Text.Encoding.UTF8.GetBytes(json), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);

        LogDebug($"Published to {topic}: {message.messageType}");
    }

    public void PublishRaw(string topic, string json)
    {
        if (!isConnected)
        {
            LogDebug("Cannot publish: Not connected to MQTT broker");
            return;
        }

        mqttClient.Publish(topic, System.Text.Encoding.UTF8.GetBytes(json), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
        LogDebug($"Published raw to {topic}");
    }

    private void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string topic = e.Topic;
        string message = System.Text.Encoding.UTF8.GetString(e.Message);

        LogDebug($"Received message on {topic}: {message}");

        // Fire raw message event first (for non-standard topics like treadmill)
        QueueMainThreadAction(() => OnRawMessageReceived?.Invoke(topic, message));

        // Parse and dispatch message on main thread (for standard MarathonMQTT messages)
        QueueMainThreadAction(() => ProcessMessage(topic, message));
    }

    private void ProcessMessage(string topic, string messageJson)
    {
        try
        {
            MQTTMessage baseMessage = MessageSerializer.DeserializeAuto(messageJson);

            if (baseMessage == null)
            {
                LogDebug("Failed to deserialize message");
                return;
            }

            // Dispatch to appropriate event based on message type
            switch (baseMessage.messageType)
            {
                case "START_GAME":
                    OnStartGameReceived?.Invoke((StartGameMessage)baseMessage);
                    break;
                case "PAUSE_GAME":
                    OnPauseGameReceived?.Invoke((PauseGameMessage)baseMessage);
                    break;
                case "RESUME_GAME":
                    OnResumeGameReceived?.Invoke((ResumeGameMessage)baseMessage);
                    break;
                case "RESET_GAME":
                    OnResetGameReceived?.Invoke((ResetGameMessage)baseMessage);
                    break;
                case "GAME_MODE":
                    OnGameModeReceived?.Invoke((GameModeMessage)baseMessage);
                    break;
                case "COUNTDOWN":
                    OnCountdownReceived?.Invoke((CountdownMessage)baseMessage);
                    break;
                case "GAME_DATA":
                    OnGameDataReceived?.Invoke((GameDataMessage)baseMessage);
                    break;
                case "GAME_OVER":
                    OnGameOverReceived?.Invoke((GameOverMessage)baseMessage);
                    break;
                case "GAME_STATE":
                    OnGameStateReceived?.Invoke((GameStateMessage)baseMessage);
                    break;
                case "MACHINE_DATA":
                    OnMachineDataReceived?.Invoke((MachineDataMessage)baseMessage);
                    break;
                case "GAME_CONFIG":
                    OnGameConfigReceived?.Invoke((GameConfigMessage)baseMessage);
                    break;
                default:
                    LogDebug($"Unknown message type: {baseMessage.messageType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Error processing message: {ex.Message}");
        }
    }

    private void OnConnectionClosed(object sender, EventArgs e)
    {
        isConnected = false;
        LogDebug("Connection to MQTT broker closed");
        QueueMainThreadAction(() => OnDisconnected?.Invoke());
    }

    private void QueueMainThreadAction(Action action)
    {
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue(action);
        }
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    // Public getters and setters for configuration
    public void SetBrokerAddress(string address)
    {
        brokerAddress = address;
    }

    public void SetBrokerPort(int port)
    {
        brokerPort = port;
    }

    public void SetCredentials(string user, string pass)
    {
        username = user;
        password = pass;
    }

    public bool IsConnected => isConnected;
    public string BrokerAddress => brokerAddress;
    public int BrokerPort => brokerPort;
    public int StationId => stationId;
    public bool UseStationTopics => useStationTopics;

    public void SetStationId(int id)
    {
        stationId = id;
        LogDebug($"Station ID set to: {stationId}");
    }

    // Helper methods for station-aware publishing/subscribing
    public void SubscribeToStation(string baseTopicMethod)
    {
        string topic = useStationTopics ?
            GetStationTopic(baseTopicMethod) :
            baseTopicMethod;
        Subscribe(topic);
    }

    public void PublishToStation(string baseTopicMethod, MQTTMessage message)
    {
        string topic = useStationTopics ?
            GetStationTopic(baseTopicMethod) :
            baseTopicMethod;
        Publish(topic, message);
    }

    private string GetStationTopic(string baseTopicMethod)
    {
        // Convert method name to topic
        string topic;
        switch (baseTopicMethod)
        {
            case "TABLET_TO_GAME":
                topic = MQTTTopics.GetTabletToGameTopic(stationId);
                break;
            case "GAME_TO_TABLET":
                topic = MQTTTopics.GetGameToTabletTopic(stationId);
                break;
            case "GAME_STATE":
                topic = MQTTTopics.GetGameStateTopic(stationId);
                break;
            case "MACHINE_TO_GAME":
                topic = MQTTTopics.GetMachineToGameTopic(stationId);
                break;
            default:
                topic = baseTopicMethod;
                break;
        }

        LogDebug($"GetStationTopic: {baseTopicMethod} -> {topic}");
        return topic;
    }

    private void LogDebug(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[MQTT] {message}");
        }
    }
}
