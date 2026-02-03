/**
 * CrestForge WebSocket Server - Server-Authoritative Version
 * All game logic runs on the server
 */

const WebSocket = require('ws');
const { v4: uuidv4 } = require('uuid');
const { GameRoom } = require('./gameRoom');
const { GameConstants } = require('./gameData');

const PORT = process.env.PORT || 8080;

// ============================================
// Data Structures
// ============================================

const clients = new Map(); // clientId -> ClientInfo
const rooms = new Map();   // roomId -> GameRoom

function createClient(ws, id) {
    return {
        id,
        ws,
        name: `Player_${id.substring(0, 4)}`,
        roomId: null
    };
}

// ============================================
// WebSocket Server
// ============================================

const wss = new WebSocket.Server({ port: PORT });

console.log(`CrestForge Server (Authoritative) starting on port ${PORT}...`);

wss.on('connection', (ws) => {
    const clientId = uuidv4();
    const client = createClient(ws, clientId);
    clients.set(clientId, client);

    console.log(`Client connected: ${clientId}`);

    send(ws, {
        type: 'welcome',
        clientId: clientId,
        message: 'Connected to CrestForge server'
    });

    ws.on('message', (data) => {
        try {
            const rawMessage = data.toString();
            console.log(`[Server] Received message from ${clientId}: ${rawMessage.substring(0, 200)}`);
            const message = JSON.parse(rawMessage);
            handleMessage(client, message);
        } catch (err) {
            const rawMessage = data.toString();
            console.error('Error parsing message:', err);
            console.error('Raw message that failed:', rawMessage);
            send(ws, { type: 'error', message: 'Invalid message format' });
        }
    });

    ws.on('close', () => {
        console.log(`Client disconnected: ${clientId}`);
        handleDisconnect(client);
        clients.delete(clientId);
    });

    ws.on('error', (err) => {
        console.error(`WebSocket error for ${clientId}:`, err);
    });
});

// ============================================
// Message Handlers
// ============================================

function handleMessage(client, message) {
    console.log(`[${client.id.substring(0, 8)}] ${message.type}`);

    switch (message.type) {
        case 'setName':
            handleSetName(client, message);
            break;
        case 'createRoom':
            handleCreateRoom(client);
            break;
        case 'joinRoom':
            handleJoinRoom(client, message);
            break;
        case 'leaveRoom':
            handleLeaveRoom(client);
            break;
        case 'listRooms':
            handleListRooms(client);
            break;
        case 'ready':
            handleReady(client, message);
            break;
        case 'action':
            handleGameAction(client, message);
            break;
        case 'chat':
            handleChat(client, message);
            break;
        default:
            send(client.ws, { type: 'error', message: `Unknown message type: ${message.type}` });
    }
}

function handleSetName(client, message) {
    if (message.name && message.name.trim()) {
        client.name = message.name.trim().substring(0, 20);
        send(client.ws, { type: 'nameSet', name: client.name });
    }
}

function handleCreateRoom(client) {
    if (client.roomId) {
        handleLeaveRoom(client);
    }

    const roomId = generateRoomCode();
    const room = new GameRoom(roomId, client.id);
    room.addPlayer(client.id, client.name);
    rooms.set(roomId, room);

    client.roomId = roomId;

    console.log(`Room created: ${roomId} by ${client.name}`);

    send(client.ws, {
        type: 'roomCreated',
        roomId: roomId,
        room: {
            id: room.roomId,
            state: room.state,
            round: room.round,
            phase: room.phase,
            playerCount: room.players.size,
            maxPlayers: 8
        },
        players: Array.from(room.players.values()).map(p => ({
            id: p.clientId,
            name: p.name,
            boardIndex: p.boardIndex,
            isReady: p.isReady,
            health: p.health,
            level: p.level
        }))
    });
}

function handleJoinRoom(client, message) {
    const roomId = message.roomId?.toUpperCase();
    const room = rooms.get(roomId);

    if (!room) {
        send(client.ws, { type: 'error', message: 'Room not found' });
        return;
    }

    if (room.players.size >= 4) {
        send(client.ws, { type: 'error', message: 'Room is full' });
        return;
    }

    if (room.state !== 'waiting') {
        send(client.ws, { type: 'error', message: 'Game already in progress' });
        return;
    }

    if (client.roomId) {
        handleLeaveRoom(client);
    }

    room.addPlayer(client.id, client.name);
    client.roomId = roomId;

    console.log(`${client.name} joined room ${roomId}`);

    // Notify joining player with format client expects
    send(client.ws, {
        type: 'roomJoined',
        roomId: roomId,
        room: {
            id: room.roomId,
            state: room.state,
            round: room.round,
            phase: room.phase,
            playerCount: room.players.size,
            maxPlayers: 8
        },
        players: Array.from(room.players.values()).map(p => ({
            id: p.clientId,
            name: p.name,
            boardIndex: p.boardIndex,
            isReady: p.isReady,
            health: p.health,
            level: p.level
        }))
    });

    // Get the joining player's state for boardIndex
    const joiningPlayer = room.getPlayer(client.id);

    // Notify other players
    broadcastToRoom(room, {
        type: 'playerJoined',
        player: {
            id: client.id,
            name: client.name,
            boardIndex: joiningPlayer?.boardIndex ?? 0,
            isReady: false,
            health: 20,
            level: 1
        },
        players: Array.from(room.players.values()).map(p => ({
            id: p.clientId,
            name: p.name,
            boardIndex: p.boardIndex,
            isReady: p.isReady,
            health: p.health,
            level: p.level
        }))
    }, client.id);

    // Send updated state to all other players
    broadcastStateToRoom(room, client.id);
}

function handleLeaveRoom(client) {
    if (!client.roomId) return;

    const room = rooms.get(client.roomId);
    if (!room) {
        client.roomId = null;
        return;
    }

    const oldRoomId = client.roomId;
    room.removePlayer(client.id);
    client.roomId = null;

    console.log(`${client.name} left room ${oldRoomId}`);

    if (room.players.size > 0) {
        // Notify remaining players
        broadcastToRoom(room, {
            type: 'playerLeft',
            playerId: client.id,
            players: Array.from(room.players.values()).map(p => ({
                id: p.clientId,
                name: p.name,
                boardIndex: p.boardIndex,
                isReady: p.isReady,
                health: p.health,
                level: p.level
            }))
        });
        broadcastStateToRoom(room);

        // Notify new host if changed
        if (room.hostId !== client.id) {
            const newHost = clients.get(room.hostId);
            if (newHost) {
                send(newHost.ws, { type: 'becameHost' });
            }
        }
    } else {
        rooms.delete(oldRoomId);
        console.log(`Room ${oldRoomId} deleted (empty)`);
    }

    send(client.ws, { type: 'leftRoom' });
}

function handleListRooms(client) {
    const availableRooms = [];
    rooms.forEach((room, id) => {
        if (room.state === 'waiting' && room.players.size < 4) {
            availableRooms.push({
                id: room.roomId,
                playerCount: room.players.size,
                maxPlayers: 4,
                hostName: clients.get(room.hostId)?.name || 'Unknown'
            });
        }
    });

    send(client.ws, {
        type: 'roomList',
        rooms: availableRooms
    });
}

function handleReady(client, message) {
    const room = rooms.get(client.roomId);
    if (!room) return;

    const player = room.getPlayer(client.id);
    if (!player) return;

    player.isReady = message.ready !== false;

    broadcastToRoom(room, {
        type: 'playerReady',
        playerId: client.id,
        ready: player.isReady,
        players: Array.from(room.players.values()).map(p => ({
            id: p.clientId,
            name: p.name,
            boardIndex: p.boardIndex,
            isReady: p.isReady,
            health: p.health,
            level: p.level
        }))
    });

    broadcastStateToRoom(room);

    // Check if all players ready to start
    if (room.state === 'waiting') {
        checkStartGame(room);
    } else if (room.phase === 'planning') {
        checkAllReady(room);
    }
}

function handleGameAction(client, message) {
    const room = rooms.get(client.roomId);
    if (!room) {
        send(client.ws, { type: 'error', message: 'Not in a room' });
        return;
    }

    if (room.state !== 'playing') {
        send(client.ws, { type: 'error', message: 'Game not in progress' });
        return;
    }

    const result = room.handleAction(client.id, message.action);

    // Send result to acting player
    send(client.ws, {
        type: 'actionResult',
        action: message.action.type,
        ...result
    });

    if (result.success) {
        // Broadcast updated state to all players
        broadcastStateToRoom(room);

        // Check if action affects game flow
        if (message.action.type === 'ready' && result.allReady) {
            checkAllReady(room);
        }
    }
}

function handleChat(client, message) {
    const room = rooms.get(client.roomId);
    if (!room) return;

    broadcastToRoom(room, {
        type: 'chat',
        playerId: client.id,
        playerName: client.name,
        message: message.message?.substring(0, 200) || ''
    });
}

function handleDisconnect(client) {
    if (client.roomId) {
        handleLeaveRoom(client);
    }
}

// ============================================
// Game Flow
// ============================================

function checkStartGame(room) {
    if (room.state !== 'waiting') return;
    if (room.players.size < 2) return;

    const allReady = room.getAllPlayers().every(p => p.isReady);
    if (!allReady) return;

    startGame(room);
}

function startGame(room) {
    const success = room.startGame();
    if (!success) return;

    console.log(`Game started in room ${room.roomId}`);

    // Register phase callbacks
    room.setOnCombatStart((r) => {
        console.log(`[startGame] Combat start callback triggered for round ${r.round}`);
        startCombat(r);
    });

    room.setOnCombatEnd((r) => {
        console.log(`[startGame] Combat end callback triggered`);
        sendCombatResults(r);
    });

    room.setOnResultsEnd((r) => {
        console.log(`[startGame] Results end callback triggered`);
        endResults(r);
    });

    broadcastToRoom(room, {
        type: 'gameStart',
        round: room.round
    });

    broadcastStateToRoom(room);

    // Start phase timer updates
    startPhaseUpdates(room);
}

function checkAllReady(room) {
    if (room.phase !== 'planning') return;

    const allReady = room.getActivePlayers().every(p => p.isReady);
    if (!allReady) return;

    // All ready - start combat early
    room.stopPhaseTimer();
    startCombat(room);
}

function startCombat(room) {
    // This runs the combat simulation in gameRoom.startCombatPhase()
    room.startCombatPhase();

    console.log(`Combat started in room ${room.roomId}, round ${room.round}`);

    // Get all combat events for scouting/spectating
    const allCombatEvents = room.getAllCombatEvents();

    // Send combat events to each player
    for (const player of room.getActivePlayers()) {
        const client = clients.get(player.clientId);
        if (!client) {
            console.log(`[startCombat] No client found for player ${player.name}`);
            continue;
        }

        const combatData = room.getCombatEventsForPlayer(player.clientId);
        console.log(`[startCombat] Sending combat to ${player.name}: combatData=${combatData ? 'exists' : 'null'}, events=${combatData?.events?.length || 0}`);

        if (combatData) {
            const events = combatData.events || [];
            const BATCH_SIZE = 50; // Send events in batches to avoid message size issues

            // Send initial combatStart with first batch of events (without allCombatEvents to avoid size issues)
            const firstBatch = events.slice(0, BATCH_SIZE);
            const msg = {
                type: 'combatStart',
                round: room.round,
                matchups: room.matchups,
                combatEvents: firstBatch,
                myTeam: combatData.myTeam,
                opponentTeam: combatData.opponentTeam,
                totalEvents: events.length,
                batchIndex: 0
            };
            console.log(`[startCombat] Sending combatStart message with ${firstBatch.length}/${events.length} events`);
            send(client.ws, msg);

            // Send remaining events in batches
            for (let i = BATCH_SIZE; i < events.length; i += BATCH_SIZE) {
                const batch = events.slice(i, i + BATCH_SIZE);
                const batchMsg = {
                    type: 'combatEventsBatch',
                    round: room.round,
                    combatEvents: batch,
                    batchIndex: Math.floor(i / BATCH_SIZE),
                    isLast: i + BATCH_SIZE >= events.length
                };
                console.log(`[startCombat] Sending batch ${Math.floor(i / BATCH_SIZE)} with ${batch.length} events`);
                send(client.ws, batchMsg);
            }
        } else {
            // For PvE/special rounds, still send combatStart but with empty events
            console.log(`[startCombat] No combat data for ${player.name}, sending empty events`);
            send(client.ws, {
                type: 'combatStart',
                round: room.round,
                matchups: room.matchups || [],
                combatEvents: [],
                myTeam: 'player1',
                opponentTeam: 'pve'
            });
        }

        // Send allCombatEvents separately (excluding current player's events to reduce size)
        const otherCombatEvents = allCombatEvents.filter(e => e.playerId !== player.clientId);
        if (otherCombatEvents.length > 0) {
            const SCOUT_BATCH_SIZE = 50; // Same batch size as regular combat events

            // Send each player's events in batched messages to avoid size limits
            for (const entry of otherCombatEvents) {
                const events = entry.events || [];

                // Send first batch with metadata
                const firstBatch = events.slice(0, SCOUT_BATCH_SIZE);
                send(client.ws, {
                    type: 'scoutCombatEvents',
                    playerId: entry.playerId,
                    hostPlayerId: entry.hostPlayerId,
                    awayPlayerId: entry.awayPlayerId,
                    events: firstBatch,
                    totalEvents: events.length,
                    batchIndex: 0,
                    isLast: events.length <= SCOUT_BATCH_SIZE
                });

                // Send remaining events in batches
                for (let i = SCOUT_BATCH_SIZE; i < events.length; i += SCOUT_BATCH_SIZE) {
                    const batch = events.slice(i, i + SCOUT_BATCH_SIZE);
                    send(client.ws, {
                        type: 'scoutCombatEventsBatch',
                        playerId: entry.playerId,
                        events: batch,
                        batchIndex: Math.floor(i / SCOUT_BATCH_SIZE),
                        isLast: i + SCOUT_BATCH_SIZE >= events.length
                    });
                }
            }
        }
    }

    // Stop room's internal phase timer - we'll manage timing here
    room.stopPhaseTimer();

    // Calculate when combat ends based on simulation duration
    // Events are generated at 10 ticks per second
    const durations = room.combatResults.map(r => r.durationTicks || 0);
    const maxDuration = durations.length > 0 ? Math.max(...durations) / 10 : 1;
    const combatDuration = Math.min(maxDuration + 2, 60) * 1000; // Add 2 seconds buffer

    console.log(`[startCombat] Combat will end in ${combatDuration}ms`);

    // Wait for combat animation to complete, then send results
    setTimeout(() => {
        sendCombatResults(room);
    }, combatDuration);
}

function sendCombatResults(room) {
    console.log(`[sendCombatResults] Sending results for round ${room.round}`);

    broadcastToRoom(room, {
        type: 'combatEnd',
        results: room.combatResults
    });

    broadcastStateToRoom(room);

    // Move to results phase
    room.startResultsPhase();

    // Stop room's internal timer - we'll manage timing
    room.stopPhaseTimer();

    // After results, advance round
    const resultsTime = GameConstants.Rounds.RESULTS_DURATION * 1000;
    console.log(`[sendCombatResults] Results phase will end in ${resultsTime}ms`);

    setTimeout(() => {
        endResults(room);
    }, resultsTime);
}

function endResults(room) {
    // Check for game end
    const active = room.getActivePlayers();
    if (active.length <= 1) {
        endGame(room, active[0]?.clientId);
        return;
    }

    room.advanceRound();

    broadcastToRoom(room, {
        type: 'roundStart',
        round: room.round
    });

    broadcastStateToRoom(room);
}

function endGame(room, winnerId) {
    room.endGame(winnerId);

    const winner = room.getPlayer(winnerId);

    broadcastToRoom(room, {
        type: 'gameEnd',
        winnerId: winnerId,
        winnerName: winner?.name || 'Unknown'
    });

    console.log(`Game ended in room ${room.roomId}. Winner: ${winner?.name}`);

    // Reset room after delay
    setTimeout(() => {
        if (rooms.has(room.roomId)) {
            room.state = 'waiting';
            room.round = 1;
            room.phase = 'planning';
            for (const player of room.getAllPlayers()) {
                player.isReady = false;
            }
            broadcastStateToRoom(room);
        }
    }, 5000);
}

function startPhaseUpdates(room) {
    // Send periodic phase timer updates
    const updateInterval = setInterval(() => {
        if (!rooms.has(room.roomId) || room.state !== 'playing') {
            clearInterval(updateInterval);
            return;
        }

        broadcastToRoom(room, {
            type: 'phaseUpdate',
            phase: room.phase,
            timer: Math.ceil(room.phaseTimer),
            round: room.round
        });
    }, 1000);
}

// ============================================
// Utility Functions
// ============================================

function send(ws, data) {
    if (ws.readyState === WebSocket.OPEN) {
        const json = JSON.stringify(data);
        if (data.type === 'combatStart') {
            console.log(`[send] Sending combatStart message, length=${json.length} bytes`);
        }
        ws.send(json);
    } else {
        console.log(`[send] WebSocket not open (state=${ws.readyState}), cannot send ${data.type}`);
    }
}

function broadcastToRoom(room, data, excludeClientId = null) {
    for (const player of room.getAllPlayers()) {
        if (player.clientId !== excludeClientId) {
            const client = clients.get(player.clientId);
            if (client) {
                send(client.ws, data);
            }
        }
    }
}

function broadcastStateToRoom(room, excludeClientId = null) {
    console.log(`[broadcastStateToRoom] Broadcasting to ${room.players.size} players (excluding: ${excludeClientId || 'none'})`);
    for (const player of room.getAllPlayers()) {
        if (player.clientId !== excludeClientId) {
            const client = clients.get(player.clientId);
            if (client) {
                const state = room.getStateForPlayer(player.clientId);
                // Log each player's gold being sent
                console.log(`[broadcastStateToRoom] Sending state to ${player.name}:`);
                for (const p of state.players) {
                    console.log(`  - ${p.name}: gold=${p.gold}, health=${p.health}, boardUnits=${p.boardUnits?.length || 0}`);
                }
                send(client.ws, {
                    type: 'gameState',
                    state: state
                });
            }
        }
    }
}

function generateRoomCode() {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    let code = '';
    for (let i = 0; i < 4; i++) {
        code += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    if (rooms.has(code)) {
        return generateRoomCode();
    }
    return code;
}

// ============================================
// Server Info
// ============================================

console.log(`CrestForge Server running on ws://localhost:${PORT}`);
console.log('Server-authoritative mode: All game logic runs on server');
console.log('Waiting for connections...');
