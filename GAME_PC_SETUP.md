# Game PC Setup Guide
## Complete Setup for All Game PC Screens

---

## Overview

The Game PC has **3 display screens** that show automatically (no buttons):

1. **IDLE/COUNTDOWN Screen** (GamePCIdle.uxml) - Shows game mode or countdown
2. **HUD Screen** (GameHUD.uxml) - Live game display
3. **GAME OVER Screen** (GameOver.uxml) - Final results

---

## Unity Scene Setup

### Step 1: Create UI GameObjects

In your Game PC Unity scene, create this hierarchy:

```
Canvas
├── MQTTManager
├── IdleScreen (UIDocument)
│   └── GamePCIdleController
├── HUDScreen (UIDocument)
│   └── GameHUDController
├── GameOverScreen (UIDocument)
│   └── GameOverController
├── GameManager
│   ├── MainGameManager
│   └── GameInputController
```

### Step 2: Assign UXML Files

**IdleScreen:**
- Add `UIDocument` component
- Source Asset: `GamePCIdle.uxml`
- Panel Settings: Create or use existing

**HUDScreen:**
- Add `UIDocument` component
- Source Asset: `GameHUD.uxml`
- Panel Settings: Same as above

**GameOverScreen:**
- Add `UIDocument` component
- Source Asset: `GameOver.uxml`
- Panel Settings: Same as above

### Step 3: Assign Scripts

**IdleScreen GameObject:**
- Add `GamePCIdleController.cs` script

**HUDScreen GameObject:**
- Add `GameHUDController.cs` script

**GameOverScreen GameObject:**
- Add `GameOverController.cs` script

**MQTTManager GameObject:**
- Add `MQTTManager.cs` script
- Configure:
  - Broker Address: `192.168.1.100` (your broker IP)
  - Broker Port: `1883`
  - Station ID: `1` (MUST MATCH tablet!)
  - Use Station Topics: ✓
  - Auto Connect: ✓

**GameManager GameObject:**
- Add `MainGameManager.cs` script
- Add `GameInputController.cs` script
- Configure settings:
  - Total Distance: `1600` (meters)
  - Countdown Seconds: `3`
  - Game Data Update Interval: `0.1` (10 updates/sec)

---

## Screen Transitions

### Automatic Flow:

```
IDLE SCREEN
    ↓ (Receives START_GAME from tablet via MQTT)
    ↓
COUNTDOWN SCREEN (Same screen, different content)
    ↓ Shows: 3 → 2 → 1 → GO!
    ↓ (Countdown reaches 0)
    ↓
HUD SCREEN
    ↓ (Shows live distance, time, progress)
    ↓ (Player reaches finish or time expires)
    ↓
GAME OVER SCREEN
    ↓ (Shows final results)
    ↓ (After timeout or tablet sends RESET)
    ↓
IDLE SCREEN
```

---

## Idle/Countdown Screen Details

### IDLE STATE:
- Shows Riyadh Marathon logo
- Shows game mode title:
  - Arabic: "تحدي التجديف" (Rowing Challenge)
  - English: "ROWING"
- Decorative wavy orange/yellow/red stripes
- Small station indicator "S" in bottom right

### COUNTDOWN STATE:
- **Same screen**, different content!
- Shows motivational instructions in Arabic & English:
  ```
  Arabic:
  "السحبات القوية تقربك من الفوز"
  "اشعر بالإيقاع وواصل التحدي حتى النهاية"
  "جذف عبر الرياض بقوة وتحكم"

  English:
  "Strong Pulls Take You Closer To Victory."
  "Feel The Rhythm And Push Through The Course."
  "Row Through Riyadh With Strength And Control."
  ```
- Shows large countdown number:
  - `3` (3 seconds remaining)
  - `2` (2 seconds remaining)
  - `1` (1 second remaining)
  - `GO!` (game starts)

---

## Testing Each Screen

### Test Idle Screen:
1. Run the scene
2. Should see IDLE screen with "ROWING" title
3. Wavy stripes and Riyadh background
4. Station indicator "S" in corner

### Test Countdown:
1. From tablet, enter name and click "Start"
2. Game PC should switch to countdown
3. Should see instructions text
4. Should count: 3 → 2 → 1 → GO!
5. After "GO!", idle screen disappears

### Test HUD Screen:
1. After countdown finishes
2. HUD should appear showing:
   - Distance: 0 m
   - Time: 00:00
   - Progress bar empty
3. Press Space bar on Game PC to simulate rowing
4. Distance should increase
5. Time should count up
6. Progress bar should fill

### Test Game Over Screen:
1. After reaching 1600m or time expires
2. Game Over screen should appear
3. Should show final time and distance
4. Should show leaderboard message
5. **NO BUTTONS** (it's display only)

---

## MQTT Messages Received by Game PC

### From Tablet:

```javascript
// When player clicks "Start Rowing!"
START_GAME {
  playerName: "John",
  gameMode: "rowing"
}

// When player clicks "Play Again"
RESET_GAME {}
```

### Sent to Tablet:

```javascript
// During countdown
COUNTDOWN {
  countdownValue: 3  // then 2, 1, 0
}

// During gameplay (10x per second)
GAME_DATA {
  currentDistance: 510,
  currentSpeed: 5.2,
  currentTime: 68,
  progressPercent: 31.8
}

// When finished
GAME_OVER {
  finalDistance: 1600,
  finalTime: 215,
  completedCourse: true
}

// State changes
GAME_STATE {
  state: "PLAYING"  // or IDLE, COUNTDOWN, PAUSED, FINISHED
}
```

---

## Customization

### Change Game Mode Instructions:

Edit `GamePCIdle.uxml` to change the motivational text for different modes.

For RUNNING:
```
"الجري السريع يقربك من الفوز"
"Quick Steps Bring You Closer To Victory"
```

For CYCLING:
```
"الدواسة القوية تقربك من الفوز"
"Strong Pedaling Brings You Closer To Victory"
```

### Change Background Image:

Replace the `BackgroundImage` element in `GamePCIdle.uxml` with actual Riyadh cityscape image.

### Change Total Distance:

In `MainGameManager` Inspector:
- Total Distance: `1600` (default)
- Can change to 800, 2000, etc.

---

## Input Simulation (For Testing)

The `GameInputController` allows keyboard testing:

- **Space Bar:** Simulate rowing stroke / running step / cycling pedal
- **P Key:** Pause/Resume game
- **R Key:** Reset game

This is for testing only. Replace with actual hardware input later.

---

## Files Reference

### UXML Files (UI Layout):
```
Assets/Ui/
├── GamePCIdle.uxml          # Idle + Countdown display
├── GameHUD.uxml             # During gameplay HUD
└── GameOver.uxml            # Final results display
```

### Controller Files (Logic):
```
Assets/Ui/
├── GamePCIdleController.cs      # Idle/countdown logic
├── GameHUDController.cs         # HUD logic
└── GameOverController.cs        # Game over logic
```

### Game Logic Files:
```
Assets/
├── MainGameManager.cs           # Main game state + MQTT sender
└── GameInputController.cs       # Input handling (Space, P, R keys)
```

### MQTT Files (Shared):
```
Assets/MQTT/
├── MQTTManager.cs           # MQTT connection
├── MQTTMessages.cs          # Message definitions
└── NetworkDiscovery.cs      # Auto-discovery (optional)
```

---

## Troubleshooting

### Idle screen doesn't show:
- Check UIDocument has `GamePCIdle.uxml` assigned
- Check Panel Settings is assigned
- Check `GamePCIdleController` is attached
- Look for errors in Console

### Countdown doesn't appear:
- Verify `MQTTManager` is connected (check logs)
- Verify Station ID matches Tablet
- Check tablet is sending START_GAME message
- Look for "Countdown: 3" in Console

### HUD doesn't show after countdown:
- Check `GameHUDController` is working
- Verify MainGameManager is running
- Check for GAME_DATA messages in Console

### Game Over screen doesn't appear:
- Check MainGameManager is detecting finish
- Verify GAME_OVER message is sent
- Check GameOverController subscriptions

### MQTT not receiving messages:
- Verify Mosquitto broker is running
- Check broker IP and port in MQTTManager
- Verify Station ID matches Tablet exactly
- Try `mosquitto_sub -t "marathon/#" -v` to monitor

---

## Complete Checklist

Before running:

- [ ] MQTTManager GameObject created and configured
- [ ] Broker Address and Station ID set (must match Tablet!)
- [ ] All 3 screen GameObjects created
- [ ] All UXML files assigned to UIDocuments
- [ ] All controller scripts attached
- [ ] MainGameManager configured
- [ ] GameInputController attached for testing
- [ ] Mosquitto broker running on network
- [ ] Tablet has same Station ID
- [ ] Tested in Play mode

---

## Quick Start Test

```bash
# Terminal 1: Start MQTT broker
mosquitto -v

# Terminal 2: Monitor all messages
mosquitto_sub -t "marathon/#" -v

# Terminal 3: Simulate tablet start command
mosquitto_pub -t "marathon/station1/tablet/command" -m '{"messageType":"START_GAME","timestamp":1234567890,"playerName":"Test","gameMode":"rowing"}'
```

---

## Screen State Summary

| State | Screen | Content | Input |
|-------|--------|---------|-------|
| **IDLE** | GamePCIdle.uxml | Game mode title | None - display only |
| **COUNTDOWN** | GamePCIdle.uxml | Instructions + Number | None - display only |
| **PLAYING** | GameHUD.uxml | Distance, Time, Progress | Space = Row/Run/Cycle |
| **FINISHED** | GameOver.uxml | Final results | None - display only |

---

**Game PC is now fully configured!** It will automatically display the right screen based on MQTT messages from the tablet. 🎮
