# Gameplay Setup Guide

This guide explains how to set up the spline-based gameplay system with checkpoints and timer.

## Overview

The gameplay system consists of:
- **Spline Movement**: Player moves along a Dreamteck Spline based on speed from machine
- **Checkpoints**: Track progress along the course
- **Timer**: Countdown timer - game over if time runs out
- **Machine Input**: Speed data from workout machine via MQTT

## Components

### 1. SplinePlayerController
Main gameplay controller that handles:
- Movement along spline based on speed
- Checkpoint detection
- Timer countdown
- Game over conditions

### 2. Checkpoint
Place these along the spline to mark progress points.

### 3. MachineDataHandler
Receives speed data from workout machine via MQTT and feeds it to the player controller.

### 4. MainGameManager (Updated)
Now integrates with the spline controller to start/stop gameplay.

---

## Unity Scene Setup

### Step 1: Setup the Spline

1. **Create Dreamteck Spline**:
   - Add a Spline to your scene (GameObject → Dreamteck → Splines → Create Node)
   - Shape your spline to match your Riyadh Marathon course

2. **Add SplineFollower**:
   - Create a GameObject for the player (e.g., "Player")
   - Add Component → Dreamteck → Splines → Spline Follower
   - Assign your spline to the Spline Follower
   - Tag the GameObject as "Player"

3. **Add SplinePlayerController**:
   - Add the `SplinePlayerController` component to the same GameObject
   - The SplineFollower will be auto-detected

### Step 2: Create Checkpoints

1. **Create Checkpoint GameObject**:
   - Create an empty GameObject (e.g., "Checkpoint_1")
   - Add a Collider component (Box Collider or Trigger Collider)
   - Check "Is Trigger"
   - Position it along the spline

2. **Add Checkpoint Component**:
   - Add the `Checkpoint` script
   - Set the checkpoint index (0, 1, 2, etc.)
   - Optionally add visual elements (activeVisual, completedVisual)

3. **Duplicate for Multiple Checkpoints**:
   - Duplicate the checkpoint along the spline
   - Update the index for each (0, 1, 2, 3, ...)
   - The last checkpoint should be at the finish line

### Step 3: Configure SplinePlayerController

Select your Player GameObject and configure:

#### Spline Settings:
- **Spline Follower**: Auto-assigned (or drag your SplineFollower)
- **Max Speed**: 20 m/s (adjust as needed)
- **Speed Decay Rate**: 0.5 (how fast speed decreases)

#### Checkpoint Settings:
- **Checkpoints**: Drag all your checkpoint GameObjects into this array
- They should be in order (0, 1, 2, 3...)

#### Timer Settings:
- **Time Limit**: 300 seconds (5 minutes) - adjust as needed
- **Use Timer**: Checked ✓

#### References:
- **Game Manager**: Drag your MainGameManager GameObject
- **HUD Controller**: Drag your GameHUDController GameObject

### Step 4: Setup MachineDataHandler

1. **Create GameObject**:
   - Create empty GameObject called "MachineDataHandler"

2. **Add Component**:
   - Add the `MachineDataHandler` script

3. **Configure**:
   - **Player Controller**: Drag your Player GameObject (with SplinePlayerController)
   - **Game Mode**: "rowing" (will be set automatically by MainGameManager)
   - **Speed Multiplier**: 1.0 (adjust per game mode)
   - **Use Machine Data**: Unchecked for testing (check when machine is connected)

### Step 5: Update MainGameManager

Select your MainGameManager GameObject:

#### References Section:
- **Spline Controller**: Drag your Player GameObject (with SplinePlayerController)
- **Machine Handler**: Drag your MachineDataHandler GameObject
- **HUD Controller**: Drag your GameHUDController GameObject

---

## Testing

### Without Machine (Keyboard Testing)

1. **Run the game**
2. **Open Debug Menu**: Press Shift+D (3-finger double tap on device)
3. **Configure MQTT** and connect
4. **Start game** from Tablet (or use Test Start Game button)
5. **Press Spacebar** to simulate machine strokes/steps
6. Watch the player move along the spline

### With Machine (MQTT Data)

1. **Configure machine** to publish to: `marathon/station{ID}/machine/data`
2. **Message format**:
```json
{
  "messageType": "MACHINE_DATA",
  "speed": 5.2,
  "strokeRate": 28,
  "totalDistance": 150.5,
  "power": 120,
  "timestamp": 1234567890
}
```

3. **Enable machine input**:
   - In MachineDataHandler, check "Use Machine Data"
   - Machine speed will now control player movement

---

## Game Over Conditions

### Victory (Win):
- ✅ Player reaches all checkpoints before timer expires
- HUD shows final time and distance
- Game Over screen displays

### Defeat (Loss):
- ❌ Timer reaches 0:00 before completing all checkpoints
- HUD shows how many checkpoints were reached
- Game Over screen displays

---

## Machine Data Format

The treadmill publishes to: `treadmill_{deviceId}/pulse`

### Message Format (Real Treadmill):
```json
{
  "device": "treadmill_1",
  "sessionId": 46,
  "pulse": 23,
  "distance_m": 8.97,
  "speed_kmh": 3.42439,
  "dt_ms": 410,
  "ts_ms": 11250887
}
```

### Fields:
- `device`: Device identifier (string) - e.g., "treadmill_1"
- `sessionId`: Current session ID (int)
- `pulse`: Heart rate / pulse (int)
- `distance_m`: Total distance in meters (float)
- `speed_kmh`: Speed in km/h (float) - **automatically converted to m/s**
- `dt_ms`: Delta time in milliseconds (int)
- `ts_ms`: Timestamp in milliseconds (long)

### Configuration:
In MachineDataHandler:
- **Device ID**: Set to match your treadmill (e.g., "treadmill_1")
- **Use Machine Data**: Check this when connected to real machine
- The system automatically converts speed from km/h to m/s

---

## Troubleshooting

### Player not moving:
- Check that SplineFollower is assigned
- Check that machine data is being received (check console logs)
- Press Spacebar to test manual input
- Check "Use Machine Data" setting in MachineDataHandler

### Checkpoints not triggering:
- Ensure checkpoint colliders are set to "Is Trigger"
- Player GameObject must be tagged as "Player"
- Checkpoints must be in correct order in SplinePlayerController array
- Check checkpoint indices (should be 0, 1, 2, 3...)

### Timer not working:
- Check "Use Timer" is enabled in SplinePlayerController
- Verify Time Limit is set correctly (in seconds)
- Timer starts when countdown finishes (after "GO!")

### HUD not showing:
- GameHUDController reference must be assigned in both:
  - MainGameManager
  - SplinePlayerController
- HUD shows automatically when game starts (after countdown)

---

## Next Steps

1. **Fine-tune speed multipliers** based on actual machine data
2. **Add visual feedback** to checkpoints (particles, sound effects)
3. **Create finish line effects** when all checkpoints completed
4. **Test with actual workout machine** and adjust speed scaling

For MQTT setup, see `MULTI_STATION_SETUP.md`
