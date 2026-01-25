# Quick Setup Guide - Riyadh Marathon MQTT System

## 5-Minute Setup

### Step 1: Install MQTT Broker (Choose One)

#### Option A: Mosquitto (Local)
```bash
# Windows (with Chocolatey)
choco install mosquitto

# Mac
brew install mosquitto

# Linux
sudo apt-get install mosquitto
```

#### Option B: HiveMQ Cloud (Free)
1. Go to https://www.hivemq.com/mqtt-cloud-broker/
2. Sign up for free tier
3. Get your broker URL and credentials

### Step 2: Install M2Mqtt in Unity

1. Download from: https://www.eclipse.org/paho/index.php?page=downloads.php
2. Or use NuGet: `M2MqttUnity`
3. Place DLL in: `Assets/Plugins/M2Mqtt/`
4. Copy the DLL to BOTH Tablet and Game projects

### Step 3: Setup Tablet Project

1. **Create Scene Hierarchy:**
   ```
   Canvas
   ├── Tablet (UIDocument)
   │   └── TabletController
   ├── GameHUD (UIDocument)
   │   └── GameHUDController
   ├── GameOverScreen (UIDocument)
   │   └── GameOverController
   └── DebugMenu (UIDocument)
       └── DebugMenuController

   Managers
   ├── MQTTManager
   └── RowingGameManager (optional for testing)
   ```

2. **Assign UXML Files:**
   - Tablet → `Tablet.uxml`
   - GameHUD → `GameHUD.uxml`
   - GameOverScreen → `GameOver.uxml`
   - DebugMenu → `DebugMenu.uxml`

3. **Configure MQTTManager:**
   - Broker Address: `localhost` (or HiveMQ URL)
   - Port: `1883` (or HiveMQ port)
   - Auto Connect: ✓

### Step 4: Setup Game Project

1. **Create Scene Hierarchy:**
   ```
   Managers
   ├── MQTTManager
   ├── MainGameManager
   └── GameInputController

   (Your game objects)
   ├── Rowing Machine Model
   ├── Environment
   └── Camera
   ```

2. **Configure MQTTManager:**
   - Same settings as Tablet project
   - Must use same broker!

3. **Configure MainGameManager:**
   - Total Distance: `1600`
   - Countdown Seconds: `3`
   - Game Mode Settings (adjust as needed)

### Step 5: Test the System

1. **Start MQTT Broker:**
   ```bash
   mosquitto -v
   ```

2. **Run Tablet Project:**
   - Press Play
   - Press `Shift + D` to open Debug Menu
   - Verify "Connected" status
   - Click "Test Start Game"

3. **Run Game Project:**
   - Press Play
   - Wait for START_GAME message
   - Press Space bar to simulate rowing
   - Watch distance increase

4. **Test Full Flow:**
   - In Tablet: Enter name, click "Start Rowing!"
   - Watch countdown on both projects
   - In Game: Press Space to row
   - In Tablet: Watch HUD update
   - In Game: Reach 1600m
   - In Tablet: See game over screen

### Step 6: Debug Menu (Tablet Only)

**Open:** 2x three-finger tap (or `Shift + D` in editor)

**Quick Test:**
1. Open Debug Menu
2. Verify MQTT connection
3. Select game mode
4. Click "Test Start Game"
5. Watch console for messages

---

## Common Issues & Fixes

| Issue | Fix |
|-------|-----|
| "Cannot connect to broker" | Start mosquitto with `mosquitto -v` |
| "MQTTManager not found" | Create GameObject with MQTTManager script |
| "Messages not received" | Check both projects use same broker |
| "DLL errors" | Place M2Mqtt.dll in `Assets/Plugins/` |
| "UI not showing" | Check UIDocument has correct UXML assigned |

---

## Quick MQTT Test (Terminal)

```bash
# Terminal 1: Subscribe to all messages
mosquitto_sub -h localhost -t "marathon/#" -v

# Terminal 2: Publish test message
mosquitto_pub -h localhost -t "marathon/tablet/command" -m '{"messageType":"START_GAME","timestamp":1234567890,"playerName":"Test","gameMode":"rowing"}'
```

---

## File Checklist

### Tablet Project Needs:
- ✓ MQTTManager.cs
- ✓ MQTTMessages.cs
- ✓ TabletController.cs
- ✓ GameHUDController.cs
- ✓ GameOverController.cs
- ✓ DebugMenuController.cs
- ✓ All .uxml files
- ✓ M2Mqtt.dll

### Game Project Needs:
- ✓ MQTTManager.cs
- ✓ MQTTMessages.cs
- ✓ MainGameManager.cs
- ✓ GameInputController.cs
- ✓ M2Mqtt.dll

---

## Next: Connect Hardware

Replace test input in `GameInputController.cs`:

```csharp
// Example for serial port input
void Update()
{
    if (SerialPort.IsOpen)
    {
        string data = SerialPort.ReadLine();
        float intensity = ParseIntensity(data);
        gameManager.PerformAction(intensity);
    }
}
```

---

**Ready to test!** 🚀

Run both projects, press Space in Game project to simulate rowing, and watch the Tablet HUD update in real-time.
