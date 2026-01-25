# Solution Summary: Multi-Station Network Setup

## Your Question:
> "How will tablet find game PC in local network when there are many game PCs and many tablets?"

## The Answer:

### ✅ Use ONE Central MQTT Broker + Station IDs

```
                   ┌──────────────────┐
                   │  MQTT Broker     │
                   │  192.168.1.100   │  ← One server for ALL stations
                   └────────┬─────────┘
                            │
      ┌─────────────────────┼─────────────────────┐
      │                     │                     │
Station 1 (ID=1)      Station 2 (ID=2)      Station 3 (ID=3)
Game PC + Tablet      Game PC + Tablet      Game PC + Tablet
      │                     │                     │
Topics:              Topics:              Topics:
marathon/station1/   marathon/station2/   marathon/station3/
```

## How It Works:

### 1. ONE Broker for Everyone
- Install Mosquitto on ONE server/PC
- All tablets and game PCs connect to it
- Broker IP: `192.168.1.100` (example)

### 2. Unique Station IDs
- Each station has a number: 1, 2, 3, 4...
- Tablet #1 and Game PC #1 both use: `Station ID = 1`
- Tablet #2 and Game PC #2 both use: `Station ID = 2`

### 3. Topic-Based Routing
- Station 1 publishes/subscribes to: `marathon/station1/*`
- Station 2 publishes/subscribes to: `marathon/station2/*`
- **No cross-talk!** Each station only hears its own messages

## Setup Steps:

### Step 1: Setup Central Broker (Once)
```
1. Choose one PC as broker server
2. Install Mosquitto
3. Set static IP: 192.168.1.100
4. Start: mosquitto -v
```

### Step 2: Configure Each Station (Per Station)
```
On both Game PC AND Tablet for Station 1:
┌────────────────────────────┐
│ Debug Menu (Shift+D)       │
├────────────────────────────┤
│ Broker: 192.168.1.100      │
│ Port: 1883                 │
│ ⚠️ Station ID: 1           │ ← IMPORTANT!
│ [Connect]                  │
└────────────────────────────┘

Repeat for Station 2 with ID=2, Station 3 with ID=3, etc.
```

### Step 3: Label Physically
Put stickers on each station:
```
┌─────────────┐
│ STATION 1   │  ← On tablet and game PC
└─────────────┘
```

## Why This Works:

✅ **Scalable**: Supports 10, 20, 50+ stations
✅ **No Confusion**: Each station isolated by ID
✅ **Centralized**: One broker easier to manage
✅ **Simple Network**: All devices see same broker

## Files Updated:

1. `MQTT/MQTTMessages.cs` - Added station-based topics
2. `MQTT/MQTTManager.cs` - Added Station ID support
3. `Ui/DebugMenu.uxml` - Added Station ID input field
4. `Ui/DebugMenuController.cs` - Station ID configuration
5. `MULTI_STATION_SETUP.md` - Complete guide

## Quick Test:

1. **Start broker:** `mosquitto -v`
2. **Configure Station 1:**
   - Game PC: Station ID = 1
   - Tablet: Station ID = 1
3. **Configure Station 2:**
   - Game PC: Station ID = 2
   - Tablet: Station ID = 2
4. **Test:** Start game on Station 1, verify Station 2 doesn't react

## Key Setting:

The **Station ID** field in Debug Menu is highlighted in yellow/orange with a warning icon (⚠️) because it's CRITICAL that both tablet and game have the SAME station ID!

---

**Result:** Clean, reliable multi-station operation with no cross-talk! 🎉
