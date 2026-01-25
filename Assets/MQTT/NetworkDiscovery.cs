using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

public class NetworkDiscovery : MonoBehaviour
{
    public static NetworkDiscovery Instance { get; private set; }

    [Header("Discovery Settings")]
    [SerializeField] private int discoveryPort = 7777;
    [SerializeField] private int broadcastInterval = 2; // seconds
    [SerializeField] private string serviceIdentifier = "MARATHON_MQTT_BROKER";

    [Header("Discovery Mode")]
    [SerializeField] private bool isBroadcaster = false; // True for Game PC, False for Tablet

    private UdpClient udpClient;
    private bool isRunning = false;

    // Event when broker is discovered
    public event Action<string, int> OnBrokerDiscovered; // IP, Port

    private Dictionary<string, float> discoveredBrokers = new Dictionary<string, float>();
    private float lastBroadcastTime = 0f;

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
        if (isBroadcaster)
        {
            StartBroadcasting();
        }
        else
        {
            StartListening();
        }
    }

    private void Update()
    {
        if (isBroadcaster && isRunning)
        {
            // Broadcast periodically
            if (Time.time - lastBroadcastTime >= broadcastInterval)
            {
                BroadcastPresence();
                lastBroadcastTime = Time.time;
            }
        }

        // Clean up old discoveries (not seen in 10 seconds)
        CleanupOldDiscoveries();
    }

    // ========== BROADCASTER (Game PC) ==========

    public void StartBroadcasting()
    {
        try
        {
            udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            isRunning = true;

            Debug.Log($"[Discovery] Started broadcasting on port {discoveryPort}");
            BroadcastPresence(); // Send immediately
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Failed to start broadcasting: {e.Message}");
        }
    }

    private void BroadcastPresence()
    {
        try
        {
            string localIP = GetLocalIPAddress();
            string message = $"{serviceIdentifier}|{localIP}|1883"; // identifier|IP|MQTT_PORT

            byte[] data = Encoding.UTF8.GetBytes(message);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

            udpClient.Send(data, data.Length, endPoint);

            Debug.Log($"[Discovery] Broadcasted: {message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Broadcast failed: {e.Message}");
        }
    }

    // ========== LISTENER (Tablet) ==========

    public void StartListening()
    {
        try
        {
            udpClient = new UdpClient(discoveryPort);
            udpClient.EnableBroadcast = true;
            isRunning = true;

            udpClient.BeginReceive(ReceiveCallback, null);

            Debug.Log($"[Discovery] Started listening on port {discoveryPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Failed to start listening: {e.Message}");
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = udpClient.EndReceive(ar, ref remoteEndPoint);

            string message = Encoding.UTF8.GetString(data);
            ProcessDiscoveryMessage(message, remoteEndPoint.Address.ToString());

            // Continue listening
            if (isRunning)
            {
                udpClient.BeginReceive(ReceiveCallback, null);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Receive error: {e.Message}");

            // Try to continue listening
            if (isRunning)
            {
                try
                {
                    udpClient.BeginReceive(ReceiveCallback, null);
                }
                catch { }
            }
        }
    }

    private void ProcessDiscoveryMessage(string message, string senderIP)
    {
        try
        {
            string[] parts = message.Split('|');

            if (parts.Length >= 3 && parts[0] == serviceIdentifier)
            {
                string brokerIP = parts[1];
                int mqttPort = int.Parse(parts[2]);

                // Don't discover yourself
                if (IsLocalAddress(brokerIP))
                {
                    return;
                }

                // Check if this is a new discovery or update
                if (!discoveredBrokers.ContainsKey(brokerIP))
                {
                    Debug.Log($"[Discovery] Found MQTT Broker at {brokerIP}:{mqttPort}");
                    OnBrokerDiscovered?.Invoke(brokerIP, mqttPort);
                }

                // Update last seen time
                discoveredBrokers[brokerIP] = Time.time;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Failed to process message: {e.Message}");
        }
    }

    // ========== UTILITY METHODS ==========

    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Failed to get local IP: {e.Message}");
        }

        return "127.0.0.1";
    }

    private bool IsLocalAddress(string ip)
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var localIP in host.AddressList)
            {
                if (localIP.ToString() == ip)
                {
                    return true;
                }
            }
        }
        catch { }

        return ip == "127.0.0.1" || ip == "localhost";
    }

    private void CleanupOldDiscoveries()
    {
        List<string> toRemove = new List<string>();

        foreach (var kvp in discoveredBrokers)
        {
            if (Time.time - kvp.Value > 10f) // Not seen in 10 seconds
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var ip in toRemove)
        {
            discoveredBrokers.Remove(ip);
            Debug.Log($"[Discovery] Broker at {ip} timed out");
        }
    }

    public List<string> GetDiscoveredBrokers()
    {
        return new List<string>(discoveredBrokers.Keys);
    }

    private void OnApplicationQuit()
    {
        StopDiscovery();
    }

    private void OnDestroy()
    {
        StopDiscovery();
    }

    public void StopDiscovery()
    {
        isRunning = false;

        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch { }
        }

        Debug.Log("[Discovery] Stopped");
    }
}
