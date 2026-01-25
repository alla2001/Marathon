# Tablet UI Setup Guide
## Complete Setup for All 3 Tablet Screens

---

## Overview

The Tablet has **3 main screens** that transition automatically:

1. **START Screen** (Tablet.uxml) - Name input + Start button
2. **METRICS Screen** (TabletMetrics.uxml) - Live game data with speedometer
3. **PLAY AGAIN Screen** (TabletPlayAgain.uxml) - Celebration + buttons

---

## Unity Scene Setup

### Step 1: Create UI GameObjects

In your Tablet Unity scene, create this hierarchy:

```
Canvas
├── MQTTManager
├── TabletUIManager
├── StartScreen (UIDocument)
│   └── TabletController
├── MetricsScreen (UIDocument)
│   └── TabletMetricsController
├── PlayAgainScreen (UIDocument)
│   └── TabletPlayAgainController
└── DebugMenu (UIDocument)
    └── DebugMenuController
```

### Step 2: Assign UXML Files

**StartScreen:**
- Add `UIDocument` component
- Source Asset: `Tablet.uxml`
- Panel Settings: Create or use existing

**MetricsScreen:**
- Add `UIDocument` component
- Source Asset: `TabletMetrics.uxml`
- Panel Settings: Same as above

**PlayAgainScreen:**
- Add `UIDocument` component
- Source Asset: `TabletPlayAgain.uxml`
- Panel Settings: Same as above

**DebugMenu:**
- Add `UIDocument` component
- Source Asset: `DebugMenu.uxml`
- Panel Settings: Same as above

### Step 3: Assign Scripts

**StartScreen GameObject:**
- Add `TabletController.cs` script

**MetricsScreen GameObject:**
- Add `TabletMetricsController.cs` script

**PlayAgainScreen GameObject:**
- Add `TabletPlayAgainController.cs` script

**DebugMenu GameObject:**
- Add `DebugMenuController.cs` script

**MQTTManager GameObject:**
- Add `MQTTManager.cs` script
- Configure:
  - Broker Address: `192.168.1.100` (your broker IP)
  - Broker Port: `1883`
  - Station ID: `1` (unique per station)
  - Use Station Topics: ✓
  - Auto Connect: ✓

**TabletUIManager GameObject:**
- Add `TabletUIManager.cs` script
- Assign references in Inspector:
  - Start Screen: Drag `StartScreen` GameObject
  - Metrics Screen: Drag `MetricsScreen` GameObject
  - Play Again Screen: Drag `PlayAgainScreen` GameObject
  - Debug Menu: Drag `DebugMenu` GameObject

---

## Screen Transitions

### Automatic Flow:

```
START SCREEN
    ↓ (Player clicks "Start Rowing!")
    ↓ Sends START_GAME via MQTT
    ↓
COUNTDOWN (3, 2, 1)
    ↓ (Countdown reaches 0)
    ↓
METRICS SCREEN
    ↓ (Receives GAME_DATA updates)
    ↓ (Displays distance & time)
    ↓
    ↓ (Game finishes)
    ↓ Receives GAME_OVER via MQTT
    ↓
PLAY AGAIN SCREEN
    ↓ (Player clicks "Play Again" or "Go Home")
    ↓ Sends RESET_GAME via MQTT
    ↓
START SCREEN
```

---

## Testing Each Screen

### Test Start Screen:
1. Run the scene
2. Should see START screen with name input
3. Enter name: "Test Player"
4. Click "Start Rowing!" (or other game mode)
5. Check console: "Sent START_GAME command via MQTT"

### Test Metrics Screen:
1. After clicking Start, countdown should begin
2. When countdown hits 0, METRICS screen should appear
3. Should show:
   - ROWING title (or current game mode)
   - Distance: 0 m (initially)
   - Time: 05:00 (or configured time)
4. As game PC sends updates, values should change

### Test Play Again Screen:
1. After game finishes (1600m reached or time expires)
2. PLAY AGAIN screen should appear
3. Should show:
   - ROWING title
   - "Congratulations!" text
   - Colorful confetti decorations
   - "Play Again" button (orange)
   - "Go Home" button (outline)
4. Click "Play Again" → returns to START screen
5. Click "Go Home" → returns to START screen

### Test Debug Menu:
1. Press `Shift + D` (editor) or 2x 3-finger tap (tablet)
2. Debug menu should overlay current screen
3. Shows current MQTT settings
4. Can change Station ID, Broker, etc.
5. Click X to close

---

## Files Reference

### UXML Files (UI Layout):
```
Assets/Ui/
├── Tablet.uxml              # Start screen
├── TabletMetrics.uxml       # Metrics during game
├── TabletPlayAgain.uxml     # Play again celebration
└── DebugMenu.uxml           # Debug settings
```

### Controller Files (Logic):
```
Assets/Ui/
├── TabletController.cs           # Start screen logic
├── TabletMetricsController.cs    # Metrics logic
├── TabletPlayAgainController.cs  # Play again logic
├── DebugMenuController.cs        # Debug menu logic
└── TabletUIManager.cs            # Master screen controller
```

### MQTT Files (Shared):
```
Assets/MQTT/
├── MQTTManager.cs           # MQTT connection
├── MQTTMessages.cs          # Message definitions
└── NetworkDiscovery.cs      # Auto-discovery (optional)
```

---

## MQTT Messages Used by Tablet

### Outgoing (Tablet → Game):
```javascript
// When "Start Rowing!" clicked
START_GAME {
  playerName: "John",
  gameMode: "rowing"
}

// When "Play Again" clicked
RESET_GAME {}

// When "Go Home" clicked
RESET_GAME {}
```

### Incoming (Game → Tablet):
```javascript
// During countdown
COUNTDOWN {
  countdownValue: 3 // then 2, 1, 0
}

// During gameplay (10x per second)
GAME_DATA {
  currentDistance: 510,
  currentSpeed: 5.2,
  currentTime: 68,
  progressPercent: 31.8
}

// When game finishes
GAME_OVER {
  finalDistance: 1600,
  finalTime: 215,
  completedCourse: true
}

// State changes
GAME_STATE {
  state: "PLAYING" // or IDLE, COUNTDOWN, PAUSED, FINISHED
}
```

---

## Customization

### Change Game Mode Title:
In `TabletUIManager`, after showing metrics:
```csharp
metricsScreen.SetGameMode("RUNNING"); // or "CYCLING", "ROWING"
playAgainScreen.SetGameMode("RUNNING");
```

### Change Total Time:
In `TabletMetricsController` Inspector:
- Total Time: `300` (5 minutes in seconds)

### Adjust Colors:
Edit the `.uxml` files and change RGB values:
- Orange: `rgb(255, 140, 0)`
- Dark background: `rgb(45, 45, 45)`
- Border: `rgb(255, 140, 0)` to `rgb(200, 80, 0)` gradient

---

## Troubleshooting

### Screen doesn't appear:
- Check UIDocument has correct UXML assigned
- Check Panel Settings is assigned
- Check `TabletUIManager` has all references assigned
- Look for errors in Console

### Transitions don't work:
- Verify `MQTTManager` is connected (check Debug Menu)
- Verify Station ID matches Game PC
- Check Console for MQTT messages
- Verify `TabletUIManager` subscriptions in code

### Buttons don't work:
- Check button names in UXML match controller code
- Check button callbacks are registered in `OnEnable()`
- Look for null reference errors in Console

### MQTT not connecting:
- Verify Mosquitto broker is running
- Check broker IP and port in Debug Menu
- Verify firewall allows port 1883
- Try `mosquitto_sub -t "marathon/#" -v` to test broker

---

## Complete Checklist

Before running:

- [ ] MQTTManager GameObject created
- [ ] MQTTManager configured with broker IP and Station ID
- [ ] TabletUIManager GameObject created
- [ ] All 3 screen GameObjects created
- [ ] All UXML files assigned to UIDocuments
- [ ] All controller scripts attached
- [ ] TabletUIManager references assigned in Inspector
- [ ] Mosquitto broker running on network
- [ ] Game PC has same Station ID configured
- [ ] Tested in Play mode

---

## Quick Start Commands

```bash
# Start MQTT broker
mosquitto -v

# Test MQTT connection
mosquitto_sub -t "marathon/#" -v

# Test sending message
mosquitto_pub -t "marathon/station1/tablet/command" -m '{"messageType":"START_GAME","playerName":"Test","gameMode":"rowing"}'
```

---

**You're all set!** The tablet will now automatically transition between screens based on game state. 🎯
