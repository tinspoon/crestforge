# Server-Authoritative Architecture Plan

## Overview
Move from client-side game logic to server-authoritative model where the server is the source of truth for all game state.

## Architecture

### Server Responsibilities
- **Game State**: Track all player state (gold, health, level, XP, board, bench)
- **Unit Pool**: Manage shared unit pool across all players
- **Shop**: Generate and manage shop for each player
- **Validation**: Validate all player actions before applying
- **Combat**: Run combat simulation and broadcast results
- **Round Management**: Control round timing and phase transitions

### Client Responsibilities
- **Rendering**: Display game state received from server
- **Input**: Send player actions to server
- **Animation**: Play combat animations based on server events
- **UI**: Show shop, stats, boards based on server state

## Message Protocol

### Client → Server Actions
```
{ type: "action", action: "buyUnit", shopIndex: 0 }
{ type: "action", action: "sellUnit", unitId: "..." }
{ type: "action", action: "placeUnit", unitId: "...", x: 2, y: 1 }
{ type: "action", action: "moveUnit", unitId: "...", x: 3, y: 2 }
{ type: "action", action: "benchUnit", unitId: "..." }
{ type: "action", action: "reroll" }
{ type: "action", action: "buyXP" }
{ type: "action", action: "toggleShopLock" }
{ type: "action", action: "ready" }
```

### Server → Client Updates
```
{ type: "gameState", state: { ...fullGameState } }
{ type: "playerUpdate", player: { gold, health, level, xp, ... } }
{ type: "shopUpdate", shop: [...units] }
{ type: "boardUpdate", playerId, board: [...], bench: [...] }
{ type: "phaseChange", phase: "planning|combat|results", timer: 30 }
{ type: "combatStart", matchups: [{p1, p2}, ...], boards: {...} }
{ type: "combatEvent", event: { type, data } }
{ type: "combatEnd", results: [{winner, loser, damage}, ...] }
{ type: "roundEnd", round: 5, standings: [...] }
{ type: "gameEnd", winner, standings: [...] }
```

## Implementation Phases

### Phase 1: Server Game State ✓
- [ ] Define unit data structures on server
- [ ] Create GameRoom class with full game state
- [ ] Implement unit pool management
- [ ] Implement shop generation

### Phase 2: Server Actions
- [ ] Implement buyUnit with validation
- [ ] Implement sellUnit
- [ ] Implement placeUnit/moveUnit/benchUnit
- [ ] Implement reroll and buyXP
- [ ] Implement unit merging (3 → star up)

### Phase 3: Round Management
- [ ] Server controls phase timing
- [ ] Planning phase with countdown
- [ ] Combat phase triggers
- [ ] Results phase and round advancement

### Phase 4: Combat Simulation
- [ ] Port combat logic to server (simplified)
- [ ] Combat event streaming to clients
- [ ] Damage calculation and health updates

### Phase 5: Client Refactor
- [ ] Remove local game logic
- [ ] Render based on server state
- [ ] Send actions instead of local mutations
- [ ] Handle server state updates

## Data Structures

### Server Player State
```javascript
{
  odId: "uuid",
  odname: "PlayerName",
  gold: 10,
  health: 20,
  level: 1,
  xp: 0,
  board: [[null, null, ...], ...],  // 7x4 grid
  bench: [null, null, ...],          // 9 slots
  shop: [{unitId, cost}, ...],       // 5 slots
  shopLocked: false,
  isReady: false
}
```

### Server Unit Instance
```javascript
{
  odinstanceId: "uuid",
  unitId: "warrior_1",        // Reference to unit template
  odstarLevel: 1,
  currentHealth: 100,
  boardX: 2,                  // -1 if on bench
  boardY: 1,
  benchSlot: -1               // -1 if on board
}
```
