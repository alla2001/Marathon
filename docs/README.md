# Marathon Game - Setup & Usage Guide

## Overview
Interactive marathon game with three modes: **Rowing**, **Running**, and **Cycling**. The system consists of:
- **PC (Big Screen)** - Displays the 3D game view
- **Tablet** - Player registration and live metrics display

Both communicate via MQTT through a Node.js backend server.

---

## Prerequisites

### Required Software
- Node.js (v16 or higher)
- MQTT Broker (Mosquitto recommended)

### Network Requirements
- PC and Tablet must be on the same network
- MQTT broker accessible from both devices

---

## Backend Server Setup

### 1. Install Dependencies
```bash
cd backend
npm install
```

### 2. Configure MQTT Connection
Edit `backend/mqtt-settings.json`:
```json
{
  "broker": "mqtt://localhost:1883",
  "username": "",
  "password": ""
}
```

### 3. Configure Game Settings
Edit `backend/config.json`:
```json
{
  "rowing": {
    "routeDistance": 1600,
    "timeLimit": 300,
    "countdownSeconds": 5
  },
  "running": {
    "routeDistance": 1200,
    "timeLimit": 240,
    "countdownSeconds": 5
  },
  "cycling": {
    "routeDistance": 2000,
    "timeLimit": 360,
    "countdownSeconds": 5
  }
}
```

### 4. Start the Server
```bash
cd backend
node server.js
```

The server will:
- Connect to MQTT broker
- Load game configuration
- Broadcast config every 5 seconds
- Handle leaderboard requests

---

## PC (Big Screen) Setup


### Running
1. Start the backend server first
2. Launch the built executable
3. The game will auto-connect to MQTT

### Controls (Debug)
- Press **Shift+D** - Open debug menu

### Game Flow
1. **IDLE** - Waiting for player (shows idle screen with cycling instructions)
2. **COUNTDOWN** - 3, 2, 1, GO! (after tablet sends start)
3. **PLAYING** - Game in progress
4. **GAME_OVER** - Shows results (win/lose)

### Station Configuration
The PC identifies itself by station ID. Set in Unity:
1. Find `MQTTManager` in scene
2. Set `Station ID` (e.g., 1, 2, 3...)

Each station operates independently with its own leaderboard.

---

## Tablet Setup

### Debug 
 - 3 fingers tap twice

### Running
1. Ensure backend server is running
2. Ensure PC game is running
3. Launch tablet app
4. Connect to same network as PC


### Station Configuration
Match the tablet's station ID to the PC:
1. Find `MQTTManager` in scene
2. Set same `Station ID` as the paired PC

### User Flow

#### 1. Start Screen
- User enters their name/nickname
- Accepts terms and conditions (toggle)
- Username is validated against leaderboard:
  - **Green** = Username available
  - **Red** = Username already used
- Press **Start** when enabled

#### 2. During Game
- Shows live metrics:
  - Distance covered
  - Time remaining
  - Progress bar
- Motivational images appear at checkpoints
- Final 10-second countdown displays prominently

#### 3. Game Over
- **Win Screen** - If player reached finish line
- **Lose Screen** - If time ran out
- Shows final distance and time
- **Play Again** button to restart

### Language Toggle
- Press **العربية/English** button to switch languages
- All UI text switches between English and Arabic

---

## Game Modes

### Rowing
- Default mode
- Uses rowing machine input
- River/water environment

### Running
- Treadmill input
- City running environment

### Cycling
- Stationary bike input
- Cycling route environment

### Switching Modes
1. Open debug menu on Tablet (Shift+D / 3 fingers tap twice) 
2. Select game mode
3. Mode is saved and persists

Or configure in `backend/config.json` and restart.

---

## MQTT Topics

### Tablet to Game
- `marathon/station/{id}/TABLET_TO_GAME` - Start game command

### Game to Tablet
- `marathon/station/{id}/GAME_TO_TABLET` - Game data, countdown, game over

### Game State
- `marathon/station/{id}/GAME_STATE` - Current state (IDLE, PLAYING, etc.)

### Leaderboard
- `leaderboard/check-username` - Validate username
- `leaderboard/submit` - Submit score
- `leaderboard/top10/request` - Get top 10

### Config
- `marathon/config` - Game configuration broadcast

---

## Troubleshooting

### PC not receiving tablet commands
1. Check both are on same network
2. Verify MQTT broker is running
3. Confirm station IDs match
4. Check firewall allows MQTT port (1883)

### Tablet shows "Username already registered"
- Player must use a unique name
- Names are stored per game mode
- Clear `backend/leaderboard-{mode}.json` to reset

### Game stuck in IDLE
1. Check MQTT connection (debug menu)
2. Verify backend is broadcasting config
3. Restart backend server

### Camera not following player
- This is handled automatically
- If stuck, the game resets SplineFollower on game start

### Motivational images not showing
1. Check sprites are assigned in `GameHUDController`
2. Verify `numberOfCheckpoints` setting
3. Images appear at distance intervals

---

## File Locations

### Config Files
- `backend/config.json` - Game settings
- `backend/mqtt-settings.json` - MQTT connection

### Leaderboards
- `backend/leaderboard-rowing.json`
- `backend/leaderboard-running.json`
- `backend/leaderboard-cycling.json`

### Unity Scenes
- `Scenes/Game (Big Screen).unity` - PC game
- `Scenes/Tablet.unity` - Tablet app

---

## Quick Start Checklist

### First Time Setup
- [ ] Install Node.js
- [ ] Install/Start Mosquitto MQTT broker
- [ ] Run `npm install` in backend folder
- [ ] Configure `mqtt-settings.json`
- [ ] Build PC game
- [ ] Build Tablet app

### Each Session
- [ ] Start MQTT broker
- [ ] Start backend: `node server.js`
- [ ] Launch PC game
- [ ] Launch Tablet app
- [ ] Verify connection (check debug menu)

---

## Support
For issues or questions, check the debug logs:
- **PC**: Unity console or Player.log
- **Tablet**: Android logcat or Xcode console
- **Backend**: Terminal output
