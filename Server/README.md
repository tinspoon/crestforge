# CrestForge Multiplayer Server

WebSocket server for CrestForge multiplayer functionality.

## Quick Start

1. **Install dependencies:**
   ```bash
   cd Server
   npm install
   ```

2. **Start the server:**
   ```bash
   npm start
   ```
   Or with auto-reload during development:
   ```bash
   npm run dev
   ```

3. **Test with browser client:**
   - Open `test-client.html` in your browser
   - Open it again in a second browser window
   - Create a room in one window, join from the other

## Server Configuration

- Default port: `8080`
- Set `PORT` environment variable to change

## Message Protocol

### Client → Server

| Message Type | Description | Payload |
|-------------|-------------|---------|
| `setName` | Set player display name | `{ name: string }` |
| `createRoom` | Create a new game room | - |
| `joinRoom` | Join existing room | `{ roomId: string }` |
| `leaveRoom` | Leave current room | - |
| `listRooms` | Get available rooms | - |
| `ready` | Toggle ready status | `{ ready: boolean }` |
| `boardUpdate` | Send board state | `{ board: object, health: number, gold: number, level: number }` |
| `endPlanning` | Signal planning phase complete | - |
| `combatResult` | Report combat outcome | `{ result: string, health: number, damage: number }` |
| `chat` | Send chat message | `{ message: string }` |

### Server → Client

| Message Type | Description |
|-------------|-------------|
| `welcome` | Connection confirmed, includes clientId |
| `roomCreated` | Room successfully created |
| `roomJoined` | Successfully joined room |
| `leftRoom` | Left room confirmation |
| `roomList` | List of available rooms |
| `playerJoined` | Another player joined room |
| `playerLeft` | Player left room |
| `playerReady` | Player ready status changed |
| `gameStart` | Game is starting |
| `roundStart` | New round beginning |
| `combatStart` | Combat phase starting |
| `gameEnd` | Game finished, includes winner |
| `opponentBoardUpdate` | Opponent's board state (for scouting) |
| `error` | Error message |

## Game Flow

1. Players connect and set names
2. One player creates a room, others join with room code
3. All players mark ready → game starts
4. **Planning Phase:** Players set up boards, send `boardUpdate`
5. Players send `endPlanning` when done
6. **Combat Phase:** Server sends `combatStart` with all board states
7. Clients simulate combat locally, send `combatResult`
8. Server advances to next round or ends game
9. Repeat until one player remains

## Room Codes

- 4 character alphanumeric codes (e.g., "ABCD")
- Easy to share verbally
- Case-insensitive
