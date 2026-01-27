# Marathon Game - Quick Start Guide

## Starting Up (In Order)

### Step 1: Start MQTT Broker
```
mosquitto
```

### Step 2: Start Backend Server
```
cd backend
node server.js
```
Wait for: `"MQTT Connected"` and `"Broadcasting config..."`

### Step 3: Launch PC Game
- Double-click the game executable
- Wait for idle screen to appear

### Step 4: Launch Tablet App
- Open app on tablet
- Should show start screen with name input

---

## Playing a Game

### On Tablet:
1. Enter player name
2. Check the terms checkbox
3. Wait for green "Username available"
4. Tap **Start**

### On PC:
1. Countdown appears: 3, 2, 1, GO!
2. Game begins automatically
3. Player uses equipment (rower/treadmill/bike)

### Game End:
- **Win**: Reached finish line in time
- **Lose**: Time ran out
- Tap **Play Again** on tablet for next player

---

## Common Issues

| Problem | Solution |
|---------|----------|
| Tablet won't connect | Check same WiFi network |
| Start button disabled | Username taken or terms not checked |
| Game not starting | Restart backend server |
| Camera stuck | Press Shift+D, click "Reset Game" |

---

## Emergency Reset

1. Close PC game
2. Stop backend (Ctrl+C)
3. Start backend again
4. Restart PC game
5. Restart tablet app
