using System;
using UnityEngine;

// MQTT Message Protocol for Tablet <-> Game Communication
// All messages are serialized to JSON for transmission

namespace MarathonMQTT
{
    // Base message class
    [Serializable]
    public class MQTTMessage
    {
        public string messageType;
        public long timestamp;

        public MQTTMessage(string type)
        {
            messageType = type;
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    // ========== TABLET -> GAME MESSAGES ==========

    [Serializable]
    public class StartGameMessage : MQTTMessage
    {
        public string playerName;
        public string gameMode; // "rowing", "running", "cycling"

        public StartGameMessage() : base("START_GAME") { }
    }

    [Serializable]
    public class PauseGameMessage : MQTTMessage
    {
        public PauseGameMessage() : base("PAUSE_GAME") { }
    }

    [Serializable]
    public class ResumeGameMessage : MQTTMessage
    {
        public ResumeGameMessage() : base("RESUME_GAME") { }
    }

    [Serializable]
    public class ResetGameMessage : MQTTMessage
    {
        public ResetGameMessage() : base("RESET_GAME") { }
    }

    [Serializable]
    public class GameModeMessage : MQTTMessage
    {
        public string gameMode; // "rowing", "running", "cycling"

        public GameModeMessage() : base("GAME_MODE") { }
    }

    // ========== GAME -> TABLET MESSAGES ==========

    [Serializable]
    public class CountdownMessage : MQTTMessage
    {
        public int countdownValue; // 3, 2, 1, 0 (0 = GO!)

        public CountdownMessage() : base("COUNTDOWN") { }
    }

    [Serializable]
    public class GameDataMessage : MQTTMessage
    {
        public float currentDistance; // in meters
        public float totalDistance; // total spline length in meters
        public float currentSpeed; // in m/s
        public float currentTime; // in seconds
        public float progressPercent; // 0-100

        public GameDataMessage() : base("GAME_DATA") { }
    }

    [Serializable]
    public class GameOverMessage : MQTTMessage
    {
        public float finalDistance;
        public float finalTime;
        public bool completedCourse;

        public GameOverMessage() : base("GAME_OVER") { }
    }

    [Serializable]
    public class GameStateMessage : MQTTMessage
    {
        public string state; // "IDLE", "COUNTDOWN", "PLAYING", "PAUSED", "FINISHED"

        public GameStateMessage() : base("GAME_STATE") { }
    }

    // ========== MACHINE -> GAME MESSAGES ==========

    [Serializable]
    public class MachineDataMessage : MQTTMessage
    {
        public float speed;           // Speed in m/s
        public float strokeRate;      // Strokes/steps per minute
        public float totalDistance;   // Total distance from machine
        public float power;           // Power output (if available)

        public MachineDataMessage() : base("MACHINE_DATA") { }
    }

    // ========== SERVER -> ALL MESSAGES ==========

    [Serializable]
    public class GameModeConfig
    {
        public float routeDistance;
        public float timeLimit;
        public int countdownSeconds;
        public float resultsDisplaySeconds;
        public float idleTimeoutSeconds;
        public string machineTopic;
    }

    [Serializable]
    public class GameModesConfig
    {
        public GameModeConfig rowing;
        public GameModeConfig running;
        public GameModeConfig cycling;
    }

    [Serializable]
    public class GameConfigMessage : MQTTMessage
    {
        public GameModesConfig gameModes;

        public GameConfigMessage() : base("GAME_CONFIG") { }
    }

    // ========== UTILITY CLASSES ==========

    public static class MessageSerializer
    {
        public static string Serialize<T>(T message) where T : MQTTMessage
        {
            return JsonUtility.ToJson(message);
        }

        public static T Deserialize<T>(string json) where T : MQTTMessage
        {
            return JsonUtility.FromJson<T>(json);
        }

        public static MQTTMessage DeserializeBase(string json)
        {
            return JsonUtility.FromJson<MQTTMessage>(json);
        }

        // Deserialize to the correct message type based on messageType field
        public static MQTTMessage DeserializeAuto(string json)
        {
            var baseMsg = DeserializeBase(json);
            if (baseMsg == null) return null;

            switch (baseMsg.messageType)
            {
                case "START_GAME":
                    return Deserialize<StartGameMessage>(json);
                case "PAUSE_GAME":
                    return Deserialize<PauseGameMessage>(json);
                case "RESUME_GAME":
                    return Deserialize<ResumeGameMessage>(json);
                case "RESET_GAME":
                    return Deserialize<ResetGameMessage>(json);
                case "GAME_MODE":
                    return Deserialize<GameModeMessage>(json);
                case "COUNTDOWN":
                    return Deserialize<CountdownMessage>(json);
                case "GAME_DATA":
                    return Deserialize<GameDataMessage>(json);
                case "GAME_OVER":
                    return Deserialize<GameOverMessage>(json);
                case "GAME_STATE":
                    return Deserialize<GameStateMessage>(json);
                case "MACHINE_DATA":
                    return Deserialize<MachineDataMessage>(json);
                case "GAME_CONFIG":
                    return Deserialize<GameConfigMessage>(json);
                default:
                    Debug.LogWarning($"Unknown message type: {baseMsg.messageType}");
                    return baseMsg;
            }
        }
    }

    // MQTT Topics
    public static class MQTTTopics
    {
        // Base topic prefix
        private const string BASE = "marathon";

        // Generate topics for a specific station
        public static string GetTabletToGameTopic(int stationId)
        {
            return $"{BASE}/station{stationId}/tablet/command";
        }

        public static string GetGameToTabletTopic(int stationId)
        {
            return $"{BASE}/station{stationId}/game/data";
        }

        public static string GetGameStateTopic(int stationId)
        {
            return $"{BASE}/station{stationId}/game/state";
        }

        public static string GetMachineToGameTopic(int stationId)
        {
            return $"{BASE}/station{stationId}/machine/data";
        }

        // Legacy support (for single station setup)
        public const string TABLET_TO_GAME = "marathon/tablet/command";
        public const string GAME_TO_TABLET = "marathon/game/data";
        public const string GAME_STATE = "marathon/game/state";
        public const string MACHINE_TO_GAME = "marathon/machine/data";

        // System topics (global)
        public const string SYSTEM_STATUS = "marathon/system/status";
        public const string STATION_DISCOVERY = "marathon/discovery";
    }
}
