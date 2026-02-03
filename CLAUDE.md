# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CrestForge is a Unity-based auto-battler game with server-authoritative multiplayer. Players collect units, place them on a hex board, and watch them auto-battle against opponents.

## Architecture

### Client-Server Split

**Unity Client** (`Assets/Scripts/`):
- Handles visuals, UI, and user input
- Receives authoritative state from server via WebSocket
- Plays back combat events received from server

**Node.js Server** (`Server/`):
- Server-authoritative: all game logic runs here
- Manages rooms, player state, economy, shop, combat simulation
- `server.js` - WebSocket server, message routing, game flow
- `gameRoom.js` - Room state, player management, combat orchestration
- `gameData.js` - Game constants mirrored from Unity

### Key Unity Namespaces

- `Crestforge.Core` - GameConstants, enums, bootstrap
- `Crestforge.Networking` - ServerGameState, NetworkManager, NetworkMessages
- `Crestforge.Combat` - CombatManager, CombatSimulation, CombatUnit
- `Crestforge.Visuals` - BoardManager3D, HexBoard3D, UnitVisual3D, OpponentBoardVisualizer
- `Crestforge.Systems` - GameState, RoundManager, OpponentManager
- `Crestforge.Data` - UnitData, UnitInstance, ItemData, TraitData
- `Crestforge.UI` - All UI components

### Multiplayer Flow

1. Client connects via WebSocket to server
2. Players join room via 4-char code, mark ready
3. Server sends `gameState` with all player data
4. During planning: client sends actions (`buyUnit`, `placeUnit`, etc.), server validates and broadcasts state
5. Combat: server simulates combat, sends `combatStart` with pre-computed events
6. Client plays back combat events visually using `CombatPlayback` / `ServerCombatVisualizer`

### Board System

- 5x4 player grid (rows 0-3), mirrored 5x4 enemy grid (rows 4-7)
- `HexBoard3D` - Physical hex tile rendering, tracks all boards via `HexBoard3D.AllBoards`
- `BoardManager3D` - Main player board with drag/drop
- `OpponentBoardVisualizer` - Display-only boards for opponents
- `AwayBenchManager` - Shows visiting player's bench during PvP combat

### Combat Visualization (Multiplayer)

- Server pre-simulates combat and sends events (spawn, move, attack, damage, death)
- `ServerCombatVisualizer` plays events on the main board
- `CombatPlayback` handles event-by-event playback with timing
- During PvP, one player "hosts" (fights on their board), other is "away"

## Development Commands

### Server
```bash
cd Server
npm install          # Install dependencies
npm start            # Start server on port 8080
npm run dev          # Start with auto-reload (nodemon)
```

### Unity
Open project in Unity 2022.3+ with URP. Main scene is likely in `Assets/Scenes/`.

## Data Files

- Unit definitions: `Assets/Resources/ScriptableObjects/NewUnits/`
- Trait definitions: `Assets/Resources/ScriptableObjects/NewTraits/`
- Game constants: `Assets/Scripts/Core/GameConstants.cs` (also mirrored in `Server/gameData.js`)

## Key Classes for Common Tasks

| Task | Client | Server |
|------|--------|--------|
| Game constants | `GameConstants.cs` | `gameData.js` |
| Player state | `ServerGameState.cs` | `gameRoom.js` (Player class) |
| Combat simulation | `CombatSimulation.cs` | `gameRoom.js` (simulateCombat) |
| Board visuals | `BoardManager3D.cs`, `HexBoard3D.cs` | N/A |
| Network messages | `NetworkMessages.cs`, `NetworkManager.cs` | `server.js` |
| Combat playback | `CombatPlayback.cs`, `ServerCombatVisualizer.cs` | N/A |
