# Riyadh Marathon - UI Architecture & Flow

## Device Separation

### 🖥️ GAME PC (Display Only - No Touch Input)
**Purpose:** Visual display for spectators/players

**UI Screens:**
1. **GamePCIdle.uxml** - Idle & Countdown screen
   - **IDLE STATE:** Shows game mode title (ROWING/RUNNING/CYCLING)
   - **COUNTDOWN STATE:** Shows motivational text + countdown number (3, 2, 1, GO!)
   - Decorative wavy stripes and Riyadh cityscape background
   - **NO BUTTONS** - read-only display
   - Receives COUNTDOWN messages via MQTT

2. **GameHUD.uxml** - During gameplay
   - Shows distance, time, progress bar
   - **NO BUTTONS** - read-only display
   - Updates via MQTT from MainGameManager

3. **GameOver.uxml** - After game finishes
   - Shows final time and distance
   - Shows leaderboard message
   - **NO BUTTONS** - read-only display
   - Displays results sent via MQTT

---

### 📱 TABLET (Interactive - Touch Input)
**Purpose:** Player control interface

**UI Screens:**
1. **Tablet.uxml** - START SCREEN ✓
   - Player enters name
   - Selects/shows game mode
   - "Start Rowing!" button
   - Sends START_GAME via MQTT

2. **TabletMetrics.uxml** - DURING GAMEPLAY (NEW)
   - Shows speedometer with distance
   - Shows time remaining
   - **NO BUTTONS** - just metrics display
   - Receives real-time updates via MQTT

3. **TabletPlayAgain.uxml** - AFTER GAME (TO BE CREATED)
   - Shows final results
   - "Play Again" button
   - "View Leaderboard" button
   - Handles restart logic

---

## Complete Game Flow

```
TABLET                          GAME PC
┌─────────────────────┐         ┌─────────────────────┐
│ START SCREEN        │         │ IDLE SCREEN         │
│ (Tablet.uxml)       │         │ (GamePCIdle.uxml)   │
│                     │         │                     │
│ [Player enters name]│         │ "ROWING"            │
│ [Click Start]       │         │ Wavy stripes        │
└──────────┬──────────┘         └─────────────────────┘
           │
           │ MQTT: START_GAME
           ├────────────────────────►
           │                          ┌─────────────────────┐
           │                          │ COUNTDOWN SCREEN    │
           │                          │ (Same: GamePCIdle)  │
           │                          │                     │
           │                          │ Instructions text   │
           │                          │ "3" → "2" → "1"     │
           │ MQTT: COUNTDOWN          │ → "GO!"             │
           │◄─────────────────────────┤                     │
┌──────────┴──────────┐              └──────────┬──────────┘
│ METRICS SCREEN      │                         │
│ (TabletMetrics.uxml)│                         │
│                     │                         │
│ 🌡 Speedometer: 510m│         ┌───────────────▼─────────┐
│ ⏱ Time: 03:52       │         │ GAME HUD                │
│                     │         │ (GameHUD.uxml)          │
│                     │         │                         │
│                     │         │ Distance: 510m          │
│                     │ MQTT    │ Time: 01:08             │
│ (Receives updates)  │◄────────┤ Progress: ████░░        │
│                     │         │                         │
│                     │         │ (Player rows/runs)      │
└─────────────────────┘         └─────────────────────────┘
           │                              │
           │                              │ (Reaches finish)
           │                              │
           │ MQTT: GAME_OVER              │
           │◄─────────────────────────────┤
           │                              │
┌──────────▼──────────┐         ┌────────▼────────────────┐
│ PLAY AGAIN SCREEN   │         │ GAME OVER SCREEN        │
│ (TabletPlayAgain)   │         │ (GameOver.uxml)         │
│                     │         │                         │
│ Congratulations!    │         │ Time: 03:35             │
│ Confetti 🎊         │         │ Distance: 1600m         │
│                     │         │                         │
│ [Play Again]        │         │ Check Leaderboard       │
│ [Go Home]           │         │                         │
└─────────────────────┘         └─────────────────────────┘
```

---

## MQTT Message Flow

### 1. Game Start
```
Tablet → Game PC
Topic: marathon/station{ID}/tablet/command
Message: START_GAME
{
  playerName: "John",
  gameMode: "rowing"
}
```

### 2. Countdown
```
Game PC → Tablet
Topic: marathon/station{ID}/game/data
Message: COUNTDOWN
{
  countdownValue: 3, 2, 1, 0
}
```

### 3. During Game (10 times per second)
```
Game PC → Tablet
Topic: marathon/station{ID}/game/data
Message: GAME_DATA
{
  currentDistance: 510,
  currentSpeed: 5.2,
  currentTime: 68,
  progressPercent: 31.8
}
```

### 4. Game End
```
Game PC → Tablet
Topic: marathon/station{ID}/game/data
Message: GAME_OVER
{
  finalDistance: 1600,
  finalTime: 215,
  completedCourse: true
}
```

---

## File Organization

### Tablet Project Files:
```
Assets/Ui/
├── Tablet.uxml                  # Start screen
├── TabletController.cs          # Start screen logic
├── TabletMetrics.uxml           # Metrics during game (NEW)
├── TabletMetricsController.cs   # Metrics logic (NEW)
├── TabletPlayAgain.uxml         # Play again screen (TO CREATE)
├── TabletPlayAgainController.cs # Play again logic (TO CREATE)
├── DebugMenu.uxml               # Debug settings
└── DebugMenuController.cs       # Debug logic
```

### Game PC Project Files:
```
Assets/Ui/
├── GamePCIdle.uxml              # Idle + Countdown screen
├── GamePCIdleController.cs      # Idle/countdown logic
├── GameHUD.uxml                 # Display during game
├── GameHUDController.cs         # HUD logic
├── GameOver.uxml                # Display after game (NO BUTTONS)
└── GameOverController.cs        # Game over logic (NO BUTTONS)

Assets/
├── MainGameManager.cs           # Main game logic + MQTT
└── GameInputController.cs       # Input handling
```

### Shared Files (Both Projects):
```
Assets/MQTT/
├── MQTTManager.cs               # MQTT connection
├── MQTTMessages.cs              # Message definitions
└── NetworkDiscovery.cs          # Auto-discovery
```

---

## Screen States

### Tablet States:
1. **START** - Tablet.uxml (visible)
2. **COUNTDOWN** - TabletMetrics.uxml (show countdown overlay)
3. **PLAYING** - TabletMetrics.uxml (receiving updates)
4. **FINISHED** - TabletPlayAgain.uxml (with buttons)

### Game PC States:
1. **IDLE** - GamePCIdle.uxml (shows game mode title)
2. **COUNTDOWN** - GamePCIdle.uxml (shows instructions + countdown number: 3, 2, 1, GO!)
3. **PLAYING** - GameHUD.uxml (receiving local game data)
4. **FINISHED** - GameOver.uxml (no buttons)

---

## Key Points

✅ **Game PC = Display Only**
   - No buttons
   - No touch input
   - Just shows what's happening

✅ **Tablet = Control Interface**
   - All buttons
   - All user interaction
   - Controls game flow

✅ **MQTT = Communication**
   - Tablet sends commands
   - Game PC sends updates
   - Both use same Station ID

✅ **Station ID = Pairing**
   - Each station has unique ID
   - Tablet #1 + Game PC #1 = Station ID 1
   - No cross-talk between stations

---

## Next Steps

1. ✅ Tablet start screen (Tablet.uxml)
2. ✅ Tablet metrics screen (TabletMetrics.uxml)
3. ⏳ Tablet play again screen (TabletPlayAgain.uxml) - AWAITING IMAGE
4. ✅ Game PC HUD (GameHUD.uxml)
5. ✅ Game PC game over (GameOver.uxml)
6. ✅ MQTT communication system
7. ✅ Multi-station support

---

## Testing Flow

1. **Start both projects**
   - Game PC shows logo/idle
   - Tablet shows start screen

2. **On Tablet:**
   - Enter name
   - Press "Start Rowing!"

3. **Both screens update:**
   - Tablet switches to metrics screen
   - Game PC shows countdown
   - Game PC shows HUD

4. **Game PC:**
   - Press Space to simulate rowing

5. **Tablet updates automatically:**
   - Distance increases
   - Time counts up/down
   - Speedometer moves

6. **On finish:**
   - Game PC shows game over screen
   - Tablet shows play again screen

7. **On Tablet:**
   - Press "Play Again" to restart

Perfect separation of concerns! 🎯
