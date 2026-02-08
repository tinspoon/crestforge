/**
 * CrestForge WebSocket Server - Server-Authoritative Version
 * All game logic runs on the server
 */

const WebSocket = require('ws');
const { v4: uuidv4 } = require('uuid');
const { GameRoom, CombatSimulator, UnitInstance, PlayerState } = require('./gameRoom');
const { GameConstants, DamageTypes, PerGameVariables, UnitTemplates, TraitDefinitions, ItemTemplates, CrestTemplates, getStarScaledStats, calculateActiveTraits, applyTraitBonuses, applyItemBonuses, applyCrestBonuses, applyBlessedBonuses, rollPerGameVariables } = require('./gameData');

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
        case 'testCombat':
            handleTestCombat(client, message);
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

        // If merchant round is active and the disconnected player was the current picker,
        // immediately advance the merchant turn
        const merchantState = room.getMerchantState();
        if (merchantState && merchantState.needsSkip) {
            merchantState.needsSkip = false;
            console.log(`[handleLeaveRoom] Merchant picker disconnected, advancing turn`);
            // Clear existing merchant turn timer
            if (room.merchantTurnTimer) {
                clearTimeout(room.merchantTurnTimer);
                room.merchantTurnTimer = null;
            }
            const turnResult = room.advanceMerchantTurn();
            if (turnResult) {
                if (turnResult.allPicked) {
                    room.endMerchantRound();
                    broadcastMerchantEnd(room);
                    endMerchantRoundAndAdvance(room);
                } else {
                    broadcastMerchantTurnUpdate(room, turnResult);
                    startMerchantTurnTimer(room);
                }
            } else {
                // No more turns possible, end the round
                room.endMerchantRound();
                broadcastMerchantEnd(room);
                endMerchantRoundAndAdvance(room);
            }
        }
    } else {
        room.cleanupTimers();
        rooms.delete(oldRoomId);
        console.log(`Room ${oldRoomId} deleted (empty, timers cleaned up)`);
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
        // Special handling for merchant picks
        if (message.action.type === 'merchantPick') {
            // Broadcast the pick to all players
            broadcastMerchantPick(room, {
                optionId: result.optionId,
                pickedById: result.pickedById,
                pickedByName: result.pickedByName
            });

            // Advance to next turn
            const turnResult = room.advanceMerchantTurn();
            if (turnResult) {
                if (turnResult.allPicked) {
                    // All players have picked - end merchant round
                    room.endMerchantRound();
                    broadcastMerchantEnd(room);

                    // Use the proper advance function
                    endMerchantRoundAndAdvance(room);
                } else {
                    // Notify all players of the new picker
                    broadcastMerchantTurnUpdate(room, {
                        currentPickerId: turnResult.currentPickerId,
                        currentPickerName: turnResult.currentPickerName
                    });

                    // Start timer for next picker
                    startMerchantTurnTimer(room);
                }
            }
        }

        // Special handling for major crest selection during major_crest round
        if (message.action.type === 'selectMajorCrest' && room.getCurrentRoundType() === 'major_crest') {
            const player = room.getPlayer(client.id);
            console.log(`[handleGameAction] Major crest select: player=${player?.name}, result.success=${result.success}, result.crest=${result.crest?.name}`);

            if (player && result.crest) {
                // Broadcast the selection to all players
                broadcastMajorCrestSelect(room, {
                    playerId: client.id,
                    playerName: player.name,
                    crestId: result.crest.crestId,
                    crestName: result.crest.name
                });

                // Check if all players have selected
                const activePlayers = room.getActivePlayers();
                const selectedCount = activePlayers.filter(p => p.majorCrest != null).length;
                const allSelected = selectedCount === activePlayers.length;

                console.log(`[handleGameAction] Major crest selection: ${selectedCount}/${activePlayers.length} players selected`);

                if (allSelected) {
                    // All players have selected - end major crest round early
                    console.log(`[handleGameAction] All players selected, ending major crest round early`);
                    endMajorCrestRoundAndAdvance(room);
                }
            }
        }

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
// Test Combat Handler
// ============================================

function handleTestCombat(client, message) {
    const { teamA, teamB } = message;

    console.log(`[TestCombat] Received request from ${client.id}`);
    console.log(`[TestCombat] Team A: ${teamA?.units?.length || 0} units, Team B: ${teamB?.units?.length || 0} units`);

    try {
        // Build test boards from config
        const boardA = buildTestBoard(teamA);
        const boardB = buildTestBoard(teamB);

        // Build mock PlayerState objects with combat stats calculation
        const stateA = buildTestPlayerState('teamA', teamA, boardA);
        const stateB = buildTestPlayerState('teamB', teamB, boardB);

        // Log units for debugging
        console.log('[TestCombat] Team A units:');
        for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
            for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                const unit = boardA[x][y];
                if (unit) {
                    const stats = stateA.getUnitCombatStats(unit);
                    console.log(`  ${unit.name} (${unit.starLevel}*) at (${x},${y}) - HP:${stats.health} ATK:${stats.attack}`);
                }
            }
        }

        console.log('[TestCombat] Team B units:');
        for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
            for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                const unit = boardB[x][y];
                if (unit) {
                    const stats = stateB.getUnitCombatStats(unit);
                    console.log(`  ${unit.name} (${unit.starLevel}*) at (${x},${y}) - HP:${stats.health} ATK:${stats.attack}`);
                }
            }
        }

        // Run combat simulation (test combat uses fresh per-game variables)
        const { rollPerGameVariables } = require('./gameData');
        const simulator = new CombatSimulator(boardA, boardB, stateA, stateB, { perGameVars: rollPerGameVariables() });
        const result = simulator.run();

        console.log(`[TestCombat] Combat complete: ${result.winner || 'draw'} wins with ${result.remainingUnits} units, ${result.events.length} events`);

        // Determine winner string - handle draw case
        let winnerStr;
        if (result.winner === 'player1') {
            winnerStr = 'teamA';
        } else if (result.winner === 'player2') {
            winnerStr = 'teamB';
        } else {
            winnerStr = 'draw';
        }

        // Send result back
        send(client.ws, {
            type: 'testCombatResult',
            winner: winnerStr,
            remainingUnits: result.remainingUnits,
            damage: result.damage,
            events: result.events
        });
    } catch (error) {
        console.error('[TestCombat] Error:', error);
        send(client.ws, {
            type: 'error',
            message: `Combat test failed: ${error.message}`
        });
    }
}

function buildTestBoard(teamConfig) {
    // Create empty board
    const board = [];
    for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
        board[x] = new Array(GameConstants.Grid.HEIGHT).fill(null);
    }

    if (!teamConfig || !teamConfig.units) return board;

    // Place units from config
    for (const unitConfig of teamConfig.units) {
        const template = UnitTemplates[unitConfig.unitId];
        if (!template) {
            console.warn(`[TestCombat] Unknown unit: ${unitConfig.unitId}`);
            continue;
        }

        // Create unit instance
        const unit = new UnitInstance(template, unitConfig.starLevel || 1);

        // Add items
        if (unitConfig.itemIds && unitConfig.itemIds.length > 0) {
            for (const itemId of unitConfig.itemIds) {
                if (itemId && ItemTemplates[itemId]) {
                    unit.items.push(ItemTemplates[itemId]);
                }
            }
        }

        // Place on board
        const x = unitConfig.boardX;
        const y = unitConfig.boardY;
        if (x >= 0 && x < GameConstants.Grid.WIDTH && y >= 0 && y < GameConstants.Grid.HEIGHT) {
            board[x][y] = unit;
        }
    }

    return board;
}

function buildTestPlayerState(teamId, teamConfig, board) {
    // Get all units from board
    const boardUnits = [];
    for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
        for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
            if (board[x][y]) {
                boardUnits.push(board[x][y]);
            }
        }
    }

    // Calculate active traits
    const activeTraits = calculateActiveTraits(boardUnits);

    // Get crests
    const minorCrests = [];
    if (teamConfig.minorCrestIds && teamConfig.minorCrestIds.length > 0) {
        for (const crestId of teamConfig.minorCrestIds) {
            if (crestId && CrestTemplates[crestId]) {
                minorCrests.push(CrestTemplates[crestId]);
            }
        }
    }
    const majorCrest = teamConfig.majorCrestId ? CrestTemplates[teamConfig.majorCrestId] : null;

    // Create a minimal PlayerState-like object with the necessary methods
    return {
        clientId: teamId,
        name: teamId,
        board: board,
        minorCrests: minorCrests,
        majorCrest: majorCrest,
        activeTraits: activeTraits,

        // Method to calculate combat stats for a unit
        getUnitCombatStats: function(unit) {
            if (!unit) return null;

            // Start with current stats (already star-scaled from UnitInstance constructor)
            let stats = { ...unit.currentStats };

            // Apply trait bonuses
            stats = applyTraitBonuses(stats, this.activeTraits, unit.traits);

            // Apply item bonuses
            if (unit.items && unit.items.length > 0) {
                stats = applyItemBonuses(stats, unit.items);
            }

            // Apply crest bonuses
            const crests = [];
            if (this.majorCrest) crests.push(this.majorCrest);
            if (this.minorCrests && this.minorCrests.length > 0) {
                crests.push(...this.minorCrests);
            }
            stats = applyCrestBonuses(stats, crests);

            return stats;
        }
    };
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

    // Register merchant callbacks
    room.setOnMerchantStart((r, merchantData) => {
        console.log(`[startGame] Merchant start callback triggered`);
        broadcastMerchantStart(r, merchantData);
    });

    room.setOnMerchantPick((r, pickData) => {
        console.log(`[startGame] Merchant pick callback triggered`);
        broadcastMerchantPick(r, pickData);
    });

    room.setOnMerchantTurnUpdate((r, turnData) => {
        console.log(`[startGame] Merchant turn update callback triggered`);
        broadcastMerchantTurnUpdate(r, turnData);
    });

    room.setOnMerchantEnd((r) => {
        console.log(`[startGame] Merchant end callback triggered`);
        broadcastMerchantEnd(r);
    });

    broadcastToRoom(room, {
        type: 'gameStart',
        round: room.round
    });

    broadcastStateToRoom(room);

    // Start phase timer updates
    startPhaseUpdates(room);

    // Check for special round types
    const roundType = room.getCurrentRoundType();
    if (roundType === 'mad_merchant') {
        console.log(`[startGame] Starting merchant round for round ${room.round}`);
        startMerchantRound(room);
    } else if (roundType === 'major_crest') {
        console.log(`[startGame] Starting major crest round for round ${room.round}`);
        startMajorCrestRound(room);
    }
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
    const roundType = room.getCurrentRoundType();
    console.log(`[startCombat] Starting combat for round ${room.round} (${roundType}), active players: ${room.getActivePlayers().map(p => p.name).join(', ')}`);

    room.startCombatPhase();

    console.log(`[startCombat] Combat phase started in room ${room.roomId}, combatResults: ${room.combatResults.length}, combatEvents: ${room.combatEvents.size}`);

    // Get all combat events for scouting/spectating
    const allCombatEvents = room.getAllCombatEvents();

    // Send combat events to ALL players (including eliminated ones, so they can see their death)
    // Use getAllPlayers() instead of getActivePlayers() to include recently eliminated players
    for (const player of room.getAllPlayers()) {
        const client = clients.get(player.clientId);
        if (!client) {
            console.log(`[startCombat] No client found for player ${player.name}`);
            continue;
        }

        const combatData = room.getCombatEventsForPlayer(player.clientId);
        console.log(`[startCombat] Sending combat to ${player.name} (eliminated=${player.isEliminated}): combatData=${combatData ? 'exists' : 'null'}, events=${combatData?.events?.length || 0}`);

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

    // Calculate when combat ends: real-time duration (ticks * 50ms) + small buffer
    const durations = room.combatResults.map(r => r.durationTicks || 0);
    const maxDurationTicks = durations.length > 0 ? Math.max(...durations) : 10;
    const realTimeDuration = maxDurationTicks * 0.05; // 50ms per tick
    const combatDuration = (realTimeDuration + 2) * 1000; // Add 2 seconds buffer

    console.log(`[startCombat] Combat: ${maxDurationTicks} ticks = ${realTimeDuration.toFixed(1)}s, waiting ${combatDuration}ms`);

    // Wait for combat animation to complete, then send results
    const gen = room.phaseGeneration;
    room.combatTimer = setTimeout(() => {
        room.combatTimer = null;
        if (room.phaseGeneration !== gen) return;
        sendCombatResults(room);
    }, combatDuration);
}

function sendCombatResults(room) {
    // Guard against room being removed or game ending
    if (!rooms.has(room.roomId)) {
        console.log(`[sendCombatResults] Room ${room.roomId} no longer exists, skipping`);
        return;
    }
    if (room.state !== 'playing') {
        console.log(`[sendCombatResults] Room ${room.roomId} not in playing state (${room.state}), skipping`);
        return;
    }

    const roundType = room.getCurrentRoundType();
    console.log(`[sendCombatResults] Sending results for round ${room.round} (${roundType}), results count: ${room.combatResults.length}`);

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

    const gen = room.phaseGeneration;
    room.resultsTimer = setTimeout(() => {
        room.resultsTimer = null;
        if (room.phaseGeneration !== gen) return;
        endResults(room);
    }, resultsTime);
}

function endResults(room) {
    console.log(`[endResults] Called for room ${room.roomId}, round ${room.round}`);

    // Guard against room being removed or game ending
    if (!rooms.has(room.roomId)) {
        console.log(`[endResults] Room ${room.roomId} no longer exists, skipping`);
        return;
    }
    if (room.state !== 'playing') {
        console.log(`[endResults] Room ${room.roomId} not in playing state (${room.state}), skipping`);
        return;
    }

    // Check for game end
    const active = room.getActivePlayers();
    console.log(`[endResults] Active players: ${active.length} - ${active.map(p => `${p.name}(${p.health}hp)`).join(', ')}`);
    if (active.length <= 1) {
        console.log(`[endResults] Only ${active.length} player(s) left, ending game`);
        endGame(room, active[0]?.clientId);
        return;
    }

    room.advanceRound();
    console.log(`[endResults] Advanced to round ${room.round}`);

    broadcastToRoom(room, {
        type: 'roundStart',
        round: room.round
    });

    broadcastStateToRoom(room);

    // Check for special round types
    const roundType = room.getCurrentRoundType();
    if (roundType === 'mad_merchant') {
        console.log(`[endResults] Starting merchant round for round ${room.round}`);
        startMerchantRound(room);
    } else if (roundType === 'major_crest') {
        console.log(`[endResults] Starting major crest round for round ${room.round}`);
        startMajorCrestRound(room);
    }
}

function endGame(room, winnerId) {
    console.log(`[endGame] Ending game in room ${room.roomId}, winner: ${winnerId}`);
    room.endGame(winnerId);

    const winner = room.getPlayer(winnerId);
    const allPlayers = room.getAllPlayers();

    console.log(`[endGame] Broadcasting gameEnd to ${allPlayers.length} players. Winner: ${winner?.name}`);
    broadcastToRoom(room, {
        type: 'gameEnd',
        winnerId: winnerId,
        winnerName: winner?.name || 'Unknown'
    });

    console.log(`[endGame] Game ended in room ${room.roomId}. Winner: ${winner?.name}`);

    // Reset room after delay
    const gen = room.phaseGeneration;
    room.resetTimer = setTimeout(() => {
        room.resetTimer = null;
        if (room.phaseGeneration !== gen) return;
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
    room.phaseUpdateInterval = setInterval(() => {
        if (!rooms.has(room.roomId) || room.state !== 'playing') {
            clearInterval(room.phaseUpdateInterval);
            room.phaseUpdateInterval = null;
            return;
        }

        // For major_crest rounds, calculate timer based on majorCrestTimer
        const roundType = room.getCurrentRoundType();
        let timer = Math.ceil(room.phaseTimer);

        if (roundType === 'major_crest' && room.majorCrestStartTime) {
            // Calculate remaining time from when major crest round started
            const elapsed = (Date.now() - room.majorCrestStartTime) / 1000;
            timer = Math.max(0, Math.ceil(20 - elapsed));
        }

        broadcastToRoom(room, {
            type: 'phaseUpdate',
            phase: room.phase,
            timer: timer,
            round: room.round
        });
    }, 1000);
}

// ============================================
// Mad Merchant Broadcasts
// ============================================

function broadcastMerchantStart(room, merchantData) {
    console.log(`[broadcastMerchantStart] Broadcasting to ${room.players.size} players`);
    broadcastToRoom(room, {
        type: 'merchantStart',
        options: merchantData.options,
        pickOrder: merchantData.pickOrder,
        currentPickerId: merchantData.currentPickerId,
        currentPickerName: merchantData.currentPickerName
    });
}

function broadcastMerchantPick(room, pickData) {
    console.log(`[broadcastMerchantPick] ${pickData.pickedByName} picked option ${pickData.optionId}`);
    broadcastToRoom(room, {
        type: 'merchantPick',
        optionId: pickData.optionId,
        pickedById: pickData.pickedById,
        pickedByName: pickData.pickedByName
    });
}

function broadcastMerchantTurnUpdate(room, turnData) {
    console.log(`[broadcastMerchantTurnUpdate] Current picker: ${turnData.currentPickerName}`);
    broadcastToRoom(room, {
        type: 'merchantTurnUpdate',
        currentPickerId: turnData.currentPickerId,
        currentPickerName: turnData.currentPickerName
    });
}

function broadcastMerchantEnd(room) {
    console.log(`[broadcastMerchantEnd] Merchant round ended`);
    broadcastToRoom(room, {
        type: 'merchantEnd'
    });
}

/**
 * Start the merchant round - called when planning phase starts for a mad_merchant round
 */
function startMerchantRound(room) {
    const merchantData = room.startMerchantRound();
    if (merchantData) {
        broadcastMerchantStart(room, merchantData);

        // Start turn timer for the first picker
        startMerchantTurnTimer(room);

        // Safety timeout: force-end merchant round after 90 seconds (max ~6 picks * 15s)
        // This prevents the round from getting stuck indefinitely
        const safetyGen = room.phaseGeneration;
        room.merchantSafetyTimer = setTimeout(() => {
            room.merchantSafetyTimer = null;
            if (room.phaseGeneration !== safetyGen) return;
            const state = room.getMerchantState();
            if (state && state.isActive) {
                console.log(`[startMerchantRound] Safety timeout hit - forcing merchant round end`);
                room.endMerchantRound();
                broadcastMerchantEnd(room);
                endMerchantRoundAndAdvance(room);
            }
        }, 90000);
    }
}

/**
 * Start a timer for the current merchant picker (15 seconds per pick)
 */
function startMerchantTurnTimer(room) {
    // Clear any existing timer
    if (room.merchantTurnTimer) {
        clearTimeout(room.merchantTurnTimer);
        room.merchantTurnTimer = null;
    }

    // Set 15 second timer for current picker
    const gen = room.phaseGeneration;
    room.merchantTurnTimer = setTimeout(() => {
        room.merchantTurnTimer = null;
        if (room.phaseGeneration !== gen) return;
        autoPickMerchant(room);
    }, 15000);
}

/**
 * Auto-pick for a player who took too long
 */
function autoPickMerchant(room) {
    const merchantState = room.getMerchantState();
    if (!merchantState || !merchantState.isActive) {
        return;
    }

    const currentPicker = room.getPlayer(merchantState.currentPickerId);
    console.log(`[autoPickMerchant] ${currentPicker?.name || 'Unknown'} (${merchantState.currentPickerId}) took too long, auto-picking`);

    // Find the first unpicked option (prefer gold)
    const unpicked = merchantState.options.filter(o => !o.isPicked);
    if (unpicked.length === 0) {
        console.log(`[autoPickMerchant] No unpicked options left, ending merchant round`);
        room.endMerchantRound();
        broadcastMerchantEnd(room);
        endMerchantRoundAndAdvance(room);
        return;
    }

    // Prefer gold options for auto-pick
    const goldOption = unpicked.find(o => o.optionType === 'gold');
    const optionToPick = goldOption || unpicked[0];

    // Simulate the pick - may fail if player disconnected
    const result = room.handleMerchantPick(merchantState.currentPickerId, optionToPick.optionId);
    if (result.success) {
        broadcastMerchantPick(room, {
            optionId: result.optionId,
            pickedById: result.pickedById,
            pickedByName: result.pickedByName
        });
        broadcastStateToRoom(room);
    } else {
        console.log(`[autoPickMerchant] Pick failed (player likely disconnected), skipping turn`);
    }

    // Always advance the turn, even if the pick failed (disconnected player)
    const turnResult = room.advanceMerchantTurn();
    if (turnResult) {
        if (turnResult.allPicked) {
            room.endMerchantRound();
            broadcastMerchantEnd(room);
            endMerchantRoundAndAdvance(room);
        } else {
            broadcastMerchantTurnUpdate(room, turnResult);
            startMerchantTurnTimer(room); // Start timer for next picker
        }
    } else {
        // advanceMerchantTurn returned null (merchant not active), force end
        console.log(`[autoPickMerchant] advanceMerchantTurn returned null, forcing end`);
        room.endMerchantRound();
        broadcastMerchantEnd(room);
        endMerchantRoundAndAdvance(room);
    }
}

/**
 * End the merchant round and advance to next round
 */
function endMerchantRoundAndAdvance(room) {
    // Clear turn timer
    if (room.merchantTurnTimer) {
        clearTimeout(room.merchantTurnTimer);
        room.merchantTurnTimer = null;
    }

    // Clear safety timer
    if (room.merchantSafetyTimer) {
        clearTimeout(room.merchantSafetyTimer);
        room.merchantSafetyTimer = null;
    }

    console.log(`[endMerchantRoundAndAdvance] Merchant round complete, advancing to next round`);

    const gen = room.phaseGeneration;
    room.advanceRoundTimer = setTimeout(() => {
        room.advanceRoundTimer = null;
        if (room.phaseGeneration !== gen) return;

        // Check for game end before advancing (a player could have been eliminated earlier)
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

        // Check for special round types in the new round
        const roundType = room.getCurrentRoundType();
        if (roundType === 'mad_merchant') {
            startMerchantRound(room);
        } else if (roundType === 'major_crest') {
            startMajorCrestRound(room);
        }
    }, 1000);
}

// ============================================
// Major Crest Round
// ============================================

/**
 * Start the major crest round - sends each player their crest options
 * Has a 20 second timer, then advances to next round
 */
function startMajorCrestRound(room) {
    const crestData = room.startMajorCrestRound();
    if (crestData) {
        broadcastMajorCrestStart(room, crestData);

        // Track when the round started for timer display
        room.majorCrestStartTime = Date.now();

        // Set up 20 second timer for major crest round
        const gen = room.phaseGeneration;
        room.majorCrestTimer = setTimeout(() => {
            room.majorCrestTimer = null;
            if (room.phaseGeneration !== gen) return;
            console.log(`[startMajorCrestRound] Timer expired, ending major crest round`);
            endMajorCrestRoundAndAdvance(room);
        }, 20000);
    }
}

/**
 * End the major crest round and advance to next round's planning phase
 * Skips combat and results phases entirely
 */
function endMajorCrestRoundAndAdvance(room) {
    // Clear timer if still running
    if (room.majorCrestTimer) {
        clearTimeout(room.majorCrestTimer);
        room.majorCrestTimer = null;
    }

    // Clear start time
    room.majorCrestStartTime = null;

    // Check if already ended (avoid double-processing)
    if (!room.majorCrestState || !room.majorCrestState.isActive) {
        return;
    }

    // Auto-assign random crests to players who haven't selected
    const autoAssigned = room.autoAssignMajorCrests();
    for (const assignment of autoAssigned) {
        broadcastMajorCrestSelect(room, assignment);
    }

    room.endMajorCrestRound();
    broadcastMajorCrestEnd(room);

    // Short delay then advance directly to next round's planning phase
    const gen = room.phaseGeneration;
    room.advanceRoundTimer = setTimeout(() => {
        room.advanceRoundTimer = null;
        if (room.phaseGeneration !== gen) return;

        // Check for game end
        const active = room.getActivePlayers();
        if (active.length <= 1) {
            endGame(room, active[0]?.clientId);
            return;
        }

        // Advance round and start planning phase directly (skip combat/results)
        room.advanceRound();

        broadcastToRoom(room, {
            type: 'roundStart',
            round: room.round
        });

        broadcastStateToRoom(room);

        // Check for special round types in the new round
        const roundType = room.getCurrentRoundType();
        if (roundType === 'mad_merchant') {
            console.log(`[endMajorCrestRoundAndAdvance] Starting merchant round for round ${room.round}`);
            startMerchantRound(room);
        } else if (roundType === 'major_crest') {
            console.log(`[endMajorCrestRoundAndAdvance] Starting major crest round for round ${room.round}`);
            startMajorCrestRound(room);
        }
        // For PvP/PvE rounds, the planning phase timer is already started by advanceRound
    }, 1000);
}

function broadcastMajorCrestStart(room, crestData) {
    console.log(`[broadcastMajorCrestStart] Sending crest options to ${room.players.size} players`);

    // Send each player their specific options
    for (const player of room.getActivePlayers()) {
        const client = clients.get(player.clientId);
        if (client) {
            const options = crestData.playerOptions[player.clientId] || [];
            send(client.ws, {
                type: 'majorCrestStart',
                options: options
            });
            console.log(`[broadcastMajorCrestStart] Sent ${options.length} options to ${player.name}`);
        }
    }
}

function broadcastMajorCrestSelect(room, selectData) {
    console.log(`[broadcastMajorCrestSelect] ${selectData.playerName} selected ${selectData.crestName}`);
    broadcastToRoom(room, {
        type: 'majorCrestSelect',
        playerId: selectData.playerId,
        playerName: selectData.playerName,
        crestId: selectData.crestId,
        crestName: selectData.crestName
    });
}

function broadcastMajorCrestEnd(room) {
    console.log(`[broadcastMajorCrestEnd] Major crest round ended`);
    broadcastToRoom(room, {
        type: 'majorCrestEnd'
    });
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
