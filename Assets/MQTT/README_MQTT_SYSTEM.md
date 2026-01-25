# Riyadh Marathon MQTT Communication System

## Architecture Overview

This project consists of **3 main components** that communicate via MQTT:

1. **Tablet Project** (Unity) - User interface and control
2. **Main Game Project** (Unity) - Actual game simulation
3. **Leaderboard Backend** (Node.js) - Score storage and leaderboard

```
┌─────────────────┐         MQTT          ┌──────────────────┐
│  Tablet Project │ ◄──────────────────► │  Main Game       │
│  (UI/Control)   │                       │  Project         │
└─────────────────┘                       └──────────────────┘
         │                                         │
         │                                         │
         │              ┌──────────────────┐      │
         └─────────────►│  Leaderboard     │◄─────┘
                        │  Backend         │
                        │  (Node.js)       │
                        └──────────────────┘
```

---

## MQTT Communication Flow

### 1. Game Start Flow

```
Tablet                          Game
  │                              │
  │  START_GAME                  │
  │  {playerName, gameMode}      │
  │─────────────────────────────►│
  │                              │ (Start Countdown)
  │  COUNTDOWN {3}               │
  │◄─────────────────────────────│
  │  COUNTDOWN {2}               │
  │◄─────────────────────────────│
  │  COUNTDOWN {1}               │
  │◄─────────────────────────────│
  │  COUNTDOWN {0} (GO!)         │
  │◄─────────────────────────────│
  │                              │ (Game Starts)
  │  GAME_DATA (continuous)      │
  │◄─────────────────────────────│
```

### 2. During Gameplay

```
Tablet                          Game
  │                              │
  │  GAME_DATA                   │
  │  {distance, speed, time}     │
  │◄─────────────────────────────│ (Every 0.1s)
  │                              │
  │  GAME_STATE                  │
  │  {state: "PLAYING"}          │
  │◄─────────────────────────────│
```

### 3. Game End Flow

```
Tablet                          Game
  │                              │
  │  GAME_OVER                   │
  │  {finalDistance, finalTime}  │
  │◄─────────────────────────────│
  │                              │
  │ (Shows results screen)       │
```

---

## MQTT Topics

| Topic | Publisher | Subscriber | Purpose |
|-------|-----------|------------|---------|
| `marathon/tablet/command` | Tablet | Game | Send commands to game |
| `marathon/game/data` | Game | Tablet | Send game data updates |
| `marathon/game/state` | Game | Tablet | Send game state changes |
| `marathon/system/status` | Both | Both | System health checks |

---

## Message Types

### Tablet → Game Messages

#### START_GAME
```json
{
  "messageType": "START_GAME",
  "timestamp": 1234567890,
  "playerName": "John Doe",
  "gameMode": "rowing"
}
```

#### PAUSE_GAME
```json
{
  "messageType": "PAUSE_GAME",
  "timestamp": 1234567890
}
```

#### RESUME_GAME
```json
{
  "messageType": "RESUME_GAME",
  "timestamp": 1234567890
}
```

#### RESET_GAME
```json
{
  "messageType": "RESET_GAME",
  "timestamp": 1234567890
}
```

### Game → Tablet Messages

#### COUNTDOWN
```json
{
  "messageType": "COUNTDOWN",
  "timestamp": 1234567890,
  "countdownValue": 3
}
```

#### GAME_DATA
```json
{
  "messageType": "GAME_DATA",
  "timestamp": 1234567890,
  "currentDistance": 500.5,
  "currentSpeed": 5.2,
  "currentTime": 120.3,
  "progressPercent": 31.3
}
```

#### GAME_OVER
```json
{
  "messageType": "GAME_OVER",
  "timestamp": 1234567890,
  "finalDistance": 1600.0,
  "finalTime": 300.5,
  "completedCourse": true
}
```

#### GAME_STATE
```json
{
  "messageType": "GAME_STATE",
  "timestamp": 1234567890,
  "state": "PLAYING"
}
```

States: `IDLE`, `COUNTDOWN`, `PLAYING`, `PAUSED`, `FINISHED`

---

## Setup Instructions

### Prerequisites

1. **MQTT Broker** - Install Mosquitto or use a cloud MQTT broker
2. **M2Mqtt Unity Library** - Download from: https://github.com/eclipse/paho.mqtt.m2mqtt

### Installing M2Mqtt

1. Download M2Mqtt for Unity
2. Import `M2Mqtt.dll` into your Unity project's `Plugins` folder
3. Ensure both Tablet and Game projects have the DLL

### Tablet Project Setup

1. Create a new GameObject named "MQTTManager"
2. Add the `MQTTManager` script to it
3. Configure MQTT settings in Inspector:
   - Broker Address: `localhost` (or your broker IP)
   - Broker Port: `1883`
   - Client ID: `TabletClient`

4. Create a GameObject with `TabletController` script
5. Create a GameObject with `DebugMenuController` script
6. Assign references in Inspector

### Main Game Project Setup

1. Create a new GameObject named "MQTTManager"
2. Add the `MQTTManager` script to it
3. Configure MQTT settings (same broker as tablet)

4. Create a GameObject named "GameManager"
5. Add the `MainGameManager` script to it
6. Add the `GameInputController` script for testing

7. Configure game settings in Inspector

### MQTT Broker Setup (Mosquitto)

#### Windows:
```bash
# Install Mosquitto
choco install mosquitto

# Start broker
mosquitto -v -c mosquitto.conf
```

#### Linux/Mac:
```bash
# Install Mosquitto
sudo apt-get install mosquitto mosquitto-clients

# Start broker
mosquitto -v
```

#### Cloud Options:
- HiveMQ Cloud (free tier)
- CloudMQTT
- AWS IoT Core

---

## Debug Menu

### Accessing Debug Menu

**Gesture:** 2x three-finger tap on tablet screen

**Keyboard Shortcut (Editor):** `Shift + D`

### Debug Menu Features

1. **MQTT Settings**
   - Broker Address configuration
   - Port configuration
   - Username/Password (if required)
   - Connection status display
   - Connect/Disconnect buttons

2. **Game Mode Selection**
   - Rowing
   - Running
   - Cycling

3. **Test Commands**
   - Test Start Game
   - Test Game Data
   - Test Game Over

---

## Testing the System

### 1. Start MQTT Broker
```bash
mosquitto -v
```

### 2. Test with MQTT Client
```bash
# Subscribe to all topics
mosquitto_sub -t "marathon/#" -v

# Publish test message
mosquitto_pub -t "marathon/tablet/command" -m '{"messageType":"START_GAME","timestamp":1234567890,"playerName":"Test","gameMode":"rowing"}'
```

### 3. Test in Unity

#### Tablet Project:
1. Run the tablet scene
2. Open Debug Menu (Shift + D)
3. Verify MQTT connection
4. Select game mode
5. Click test buttons to send messages

#### Game Project:
1. Run the game scene
2. Press Space bar to simulate rowing/running/cycling
3. Watch console for MQTT messages
4. Verify game data is being sent

---

## Game Modes

### Rowing
- Distance per stroke: 5m
- Requires periodic "stroke" inputs
- Speed decreases over time (water resistance)

### Running
- Speed multiplier: 1x
- Continuous movement based on input
- Moderate speed decay

### Cycling
- Speed multiplier: 1.5x
- Fastest mode
- Lower speed decay

---

## Integration with Hardware

Replace the test input in `GameInputController.cs` with your hardware input:

```csharp
// Example: Rowing machine integration
public void OnRowingMachineStroke(float force)
{
    gameManager.PerformAction(force / maxForce);
}

// Example: Treadmill integration
public void OnTreadmillSpeed(float speed)
{
    gameManager.PerformAction(speed / maxSpeed);
}

// Example: Cycling machine integration
public void OnCyclingPedal(float rpm)
{
    gameManager.PerformAction(rpm / maxRPM);
}
```

---

## File Structure

```
Assets/
├── MQTT/
│   ├── MQTTManager.cs          # MQTT connection manager
│   ├── MQTTMessages.cs         # Message protocol definitions
│   └── README_MQTT_SYSTEM.md   # This file
├── Ui/
│   ├── Tablet.uxml             # Start screen UI
│   ├── TabletController.cs     # Start screen logic
│   ├── GameHUD.uxml            # In-game HUD UI
│   ├── GameHUDController.cs    # HUD logic
│   ├── GameOver.uxml           # Results screen UI
│   ├── GameOverController.cs   # Results logic
│   ├── DebugMenu.uxml          # Debug menu UI
│   └── DebugMenuController.cs  # Debug menu logic
├── MainGameManager.cs          # Main game logic (for Game project)
└── GameInputController.cs      # Input handler (for Game project)
```

---

## Troubleshooting

### MQTT Connection Issues

1. **Cannot connect to broker**
   - Verify broker is running: `mosquitto -v`
   - Check firewall settings
   - Verify broker address and port

2. **Messages not received**
   - Check topic subscriptions
   - Verify message format (must be valid JSON)
   - Check MQTT QoS settings

3. **Connection drops**
   - Check network stability
   - Increase keep-alive interval
   - Verify broker capacity

### Unity Issues

1. **M2Mqtt DLL errors**
   - Ensure DLL is in Plugins folder
   - Check .NET compatibility settings
   - Verify Unity API compatibility level

2. **UI not updating**
   - Check if MQTT events are subscribed
   - Verify messages are on main thread
   - Check UI element references

---

## Next Steps

1. **Leaderboard Backend**: Create Node.js server to receive scores
2. **Authentication**: Add player authentication system
3. **Analytics**: Track gameplay metrics
4. **Multiplayer**: Support multiple concurrent players
5. **Replay System**: Save and replay game sessions

---

## Support

For issues or questions, check the following:
- Unity Console logs
- MQTT broker logs
- Network connectivity
- Message formats in Debug Menu

---

**Generated for Riyadh Marathon Project**
