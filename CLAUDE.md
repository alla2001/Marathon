# Marathon Project

## Architecture Overview
This is a Unity project with a Node.js backend, communicating via MQTT. There are **3 separate apps** that share the same Unity project but run independently:

### 1. PC Game (Big Screen)
- Scene: `Scenes/Game (Big Screen).unity`
- Main scripts: `MainGameManager.cs`, `SplinePlayerController.cs`, `MachineDataHandler.cs`
- UI controllers: `GameHUDController.cs`, `GameOverController.cs`, `GamePCIdleController.cs`, `GamePCDebugMenuController.cs`
- Backend: `backend/server.js` (leaderboard, config, position tracking)
- Supports 3 game modes: rowing, running, cycling

### 2. Tablet (Player Tablet)
- Scene: `Scenes/Tablet.unity`
- Main scripts: `TabletController.cs`, `TabletMetricsController.cs`, `TabletPlayAgainController.cs`
- UI manager: `TabletUIManager.cs`
- Debug menu: `DebugMenuController.cs`
- Leaderboard: `LeaderboardManager.cs`
- Backend: `backend/server.js` (same as PC Game)
- Supports 3 game modes: rowing, running, cycling

### 3. FM Tablet (separate registration tablet)
- Scene: `Scenes/Tablet FM.unity`
- Main scripts: `TabletFMController.cs`
- Leaderboard: `FMLeaderboardManager.cs` (separate from game LeaderboardManager)
- Debug menu: `FMDebugMenuController.cs` (separate from game DebugMenuController)
- Backend: `backend/fm-server.js` (separate process from server.js)
- Single game mode, supports left/right tablet selection
- MQTT topics use `MarathonFM/` prefix (completely separate from game topics)

## Critical Rules
- **NEVER modify shared code** (like MQTTManager) when working on FM tablet features
- **FM and Game systems are fully isolated** — different leaderboard managers, different debug menus, different backends, different MQTT topic prefixes
- The 3 apps must never break each other

## MQTT Topic Namespaces
- Game topics: `leaderboard/`, `marathon/`, station-based (`station{id}/`)
- FM topics: `MarathonFM/leaderboard/`, `MarathonFM/left/`, `MarathonFM/right/`

## FM Tablet MQTT Topics
| Topic | Direction | Purpose |
|-------|-----------|---------|
| `MarathonFM/leaderboard/write` | Tablet -> Backend | Submit entry {username, score, distance, time} |
| `MarathonFM/leaderboard/top10` | Backend -> All | Broadcast top 10 every 5s |
| `MarathonFM/leaderboard/left/checkname` | Left Tablet -> Backend | Check username |
| `MarathonFM/leaderboard/right/checkname` | Right Tablet -> Backend | Check username |
| `MarathonFM/leaderboard/left/checkname/response` | Backend -> Left | Username check result |
| `MarathonFM/leaderboard/right/checkname/response` | Backend -> Right | Username check result |
| `MarathonFM/left/name` | Tablet -> Game | Send registered username |
| `MarathonFM/right/name` | Tablet -> Game | Send registered username |
| `MarathonFM/left/status` | Game -> Tablet | Game status |
| `MarathonFM/right/status` | Game -> Tablet | Game status |

## FM Debug Menu UXML Elements
The FM debug menu expects these named elements (similar to main debug but with LeftButton/RightButton instead of game modes):
- `DebugRoot`, `BrokerAddressInput`, `BrokerPortInput`, `StationIdInput`
- `UsernameInput`, `PasswordInput`, `ConnectionStatus`
- `ConnectButton`, `DisconnectButton`, `CloseButton`
- `LeftButton`, `RightButton`, `SelectedSideLabel`
- `LogContainer`, `LogScrollView`, `ClearLogButton`

## Machine Data
- Speed comes as actual km/h (e.g. 18.08), NOT multiplied by 100
- Distance is calculated locally from speed * deltaTime (not from machine's distance_m)
- `MachineDataHandler.ResetDistance()` called on game start

## Backends
- `backend/server.js` — Main game leaderboard (rowing/running/cycling), config broadcasting, position tracking
- `backend/fm-server.js` — FM leaderboard (single flat array), username checking per side, top 10 broadcasting
- Both use `backend/mqtt-settings.json` for broker connection (different clientIds)

## Win/Lose Labels (TabletPlayAgain)
- Uses 4 separate labels: `WinLabelEng`, `WinLabelArb`, `LoseLabelEng`, `LoseLabelArb`
- Script toggles visibility based on language (no runtime font switching)
- Each label has its own font set in UXML/UI Builder
