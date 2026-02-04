using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crestforge.Networking
{
    /// <summary>
    /// Base class for all network messages
    /// </summary>
    [Serializable]
    public class NetworkMessage
    {
        public string type;
    }

    // ============================================
    // Server -> Client Messages
    // ============================================

    [Serializable]
    public class WelcomeMessage : NetworkMessage
    {
        public string clientId;
        public string message;
    }

    [Serializable]
    public class ErrorMessage : NetworkMessage
    {
        public string message;
    }

    [Serializable]
    public class RoomCreatedMessage : NetworkMessage
    {
        public string roomId;
        public RoomInfo room;
    }

    [Serializable]
    public class RoomJoinedMessage : NetworkMessage
    {
        public string roomId;
        public RoomInfo room;
        public List<PlayerInfo> players;
    }

    [Serializable]
    public class PlayerJoinedMessage : NetworkMessage
    {
        public PlayerInfo player;
        public List<PlayerInfo> players;
    }

    [Serializable]
    public class PlayerLeftMessage : NetworkMessage
    {
        public string playerId;
        public List<PlayerInfo> players;
    }

    [Serializable]
    public class PlayerReadyMessage : NetworkMessage
    {
        public string playerId;
        public bool ready;
        public List<PlayerInfo> players;
    }

    [Serializable]
    public class GameStartMessage : NetworkMessage
    {
        public int round;
        public string phase;
        public List<PlayerInfo> players;
    }

    [Serializable]
    public class RoundStartMessage : NetworkMessage
    {
        public int round;
        public string phase;
        public List<PlayerInfo> players;
    }

    [Serializable]
    public class CombatStartMessage : NetworkMessage
    {
        public int round;
        public List<BoardStateInfo> boardStates;
    }

    [Serializable]
    public class GameEndMessage : NetworkMessage
    {
        public string winnerId;
        public string winnerName;
        public List<PlayerInfo> players;
    }

    [Serializable]
    public class OpponentBoardUpdateMessage : NetworkMessage
    {
        public string playerId;
        public string playerName;
        public BoardData board;
        public int health;
        public int level;
    }

    [Serializable]
    public class RoomListMessage : NetworkMessage
    {
        public List<RoomListItem> rooms;
    }

    [Serializable]
    public class PlayerEndedPlanningMessage : NetworkMessage
    {
        public string playerId;
        public string playerName;
    }

    [Serializable]
    public class CombatResultReceivedMessage : NetworkMessage
    {
        public string playerId;
        public string playerName;
        public string result;
        public int health;
        public int damage;
    }

    [Serializable]
    public class ChatMessage : NetworkMessage
    {
        public string playerId;
        public string playerName;
        public string message;
    }

    // ============================================
    // Client -> Server Messages
    // ============================================

    [Serializable]
    public class SetNameMessage : NetworkMessage
    {
        public string name;

        public SetNameMessage(string name)
        {
            this.type = "setName";
            this.name = name;
        }
    }

    [Serializable]
    public class CreateRoomMessage : NetworkMessage
    {
        public CreateRoomMessage()
        {
            this.type = "createRoom";
        }
    }

    [Serializable]
    public class JoinRoomMessage : NetworkMessage
    {
        public string roomId;

        public JoinRoomMessage(string roomId)
        {
            this.type = "joinRoom";
            this.roomId = roomId;
        }
    }

    [Serializable]
    public class LeaveRoomMessage : NetworkMessage
    {
        public LeaveRoomMessage()
        {
            this.type = "leaveRoom";
        }
    }

    [Serializable]
    public class ListRoomsMessage : NetworkMessage
    {
        public ListRoomsMessage()
        {
            this.type = "listRooms";
        }
    }

    [Serializable]
    public class ReadyMessage : NetworkMessage
    {
        public bool ready;

        public ReadyMessage(bool ready)
        {
            this.type = "ready";
            this.ready = ready;
        }
    }

    [Serializable]
    public class BoardUpdateMessage : NetworkMessage
    {
        public BoardData board;
        public int health;
        public int gold;
        public int level;

        public BoardUpdateMessage(BoardData board, int health, int gold, int level)
        {
            this.type = "boardUpdate";
            this.board = board;
            this.health = health;
            this.gold = gold;
            this.level = level;
        }
    }

    [Serializable]
    public class EndPlanningMessage : NetworkMessage
    {
        public EndPlanningMessage()
        {
            this.type = "endPlanning";
        }
    }

    [Serializable]
    public class CombatResultMessage : NetworkMessage
    {
        public string result;
        public int health;
        public int damage;

        public CombatResultMessage(string result, int health, int damage)
        {
            this.type = "combatResult";
            this.result = result;
            this.health = health;
            this.damage = damage;
        }
    }

    [Serializable]
    public class SendChatMessage : NetworkMessage
    {
        public string message;

        public SendChatMessage(string message)
        {
            this.type = "chat";
            this.message = message;
        }
    }

    // ============================================
    // Data Structures
    // ============================================

    [Serializable]
    public class PlayerInfo
    {
        public string id;
        public string name;
        public int boardIndex;
        public bool isReady;
        public int health;
        public int level;
    }

    [Serializable]
    public class RoomInfo
    {
        public string id;
        public string state;
        public int round;
        public string phase;
        public int playerCount;
        public int maxPlayers;
    }

    [Serializable]
    public class RoomListItem
    {
        public string id;
        public int playerCount;
        public int maxPlayers;
        public string hostName;
    }

    [Serializable]
    public class BoardStateInfo
    {
        public string playerId;
        public string playerName;
        public BoardData board;
        public int health;
        public int level;
    }

    [Serializable]
    public class BoardData
    {
        public List<UnitPlacement> units;
        public List<UnitPlacement> bench;
    }

    [Serializable]
    public class UnitPlacement
    {
        public int x;
        public int y;
        public string unitId;
        public int stars;
        public int currentHealth;
        public int maxHealth;

        public UnitPlacement() { }

        public UnitPlacement(int x, int y, string unitId, int stars, int currentHealth = -1, int maxHealth = -1)
        {
            this.x = x;
            this.y = y;
            this.unitId = unitId;
            this.stars = stars;
            this.currentHealth = currentHealth;
            this.maxHealth = maxHealth;
        }
    }

    // ============================================
    // Server-Authoritative Message Types
    // ============================================

    [Serializable]
    public class GameStateWrapper : NetworkMessage
    {
        public ServerGameStateData state;
    }

    [Serializable]
    public class PhaseUpdateMessage : NetworkMessage
    {
        public string phase;
        public float timer;
        public int round;
    }

    [Serializable]
    public class ActionResultMessage : NetworkMessage
    {
        public string action;
        public bool success;
        public string error;
    }

    [Serializable]
    public class ServerCombatStartMessage : NetworkMessage
    {
        public int round;
        public List<ServerMatchup> matchups;
        public List<ServerCombatEvent> combatEvents;
        public string myTeam;
        public string opponentTeam;
        public int totalEvents; // Total events across all batches
        public int batchIndex;  // Index of this batch (0 = first)
    }

    /// <summary>
    /// Entry for a player's combat events (for scouting other players' fights)
    /// </summary>
    [Serializable]
    public class AllCombatEventsEntry
    {
        public string playerId;
        public string hostPlayerId;
        public string awayPlayerId;
        public List<ServerCombatEvent> events;
    }

    /// <summary>
    /// Message containing combat events for a specific player (for scouting)
    /// </summary>
    [Serializable]
    public class ScoutCombatEventsMessage : NetworkMessage
    {
        public string playerId;
        public string hostPlayerId;
        public string awayPlayerId;
        public List<ServerCombatEvent> events;
        public int totalEvents;
        public int batchIndex;
        public bool isLast;
    }

    /// <summary>
    /// Batch message for scout combat events (for large event lists)
    /// </summary>
    [Serializable]
    public class ScoutCombatEventsBatchMessage : NetworkMessage
    {
        public string playerId;
        public List<ServerCombatEvent> events;
        public int batchIndex;
        public bool isLast;
    }

    [Serializable]
    public class ServerCombatEventsBatchMessage : NetworkMessage
    {
        public int round;
        public List<ServerCombatEvent> combatEvents;
        public int batchIndex;
        public bool isLast;
    }

    [Serializable]
    public class ServerCombatEndMessage : NetworkMessage
    {
        public List<ServerCombatResult> results;
    }

    // ============================================
    // Combat Event Types for Visualization
    // ============================================

    [Serializable]
    public class ServerCombatEvent
    {
        public string type; // combatStart, unitMove, unitAttack, unitDamage, unitDeath, combatEnd
        public int tick;

        // For combatStart - initial unit positions
        public List<ServerCombatUnit> units;

        // For unitMove
        public string instanceId;
        public int x;
        public int y;

        // For unitAttack
        public string attackerId;
        public string targetId;
        public int damage;
        public int hitTick; // Tick when the attack will land (for animation sync)

        // For unitDamage
        public int currentHealth;
        public int maxHealth;

        // For unitDeath
        public string killerId;
        public string lootType; // "crest_token" or "item_anvil" if unit drops loot
        public ServerPosition lootPosition; // Position where loot should spawn
        public string lootId; // Unique ID for collecting this loot

        // For combatEnd
        public string winner;
        public int remainingUnits;
    }

    [Serializable]
    public class ServerPosition
    {
        public int x;
        public int y;
    }

    [Serializable]
    public class ServerCombatUnit
    {
        public string instanceId;
        public string unitId;
        public string name;
        public string playerId;
        public int x;
        public int y;
        public int health;
        public int maxHealth;
        public ServerUnitStats stats;
        public List<ServerItemData> items;
    }
}
