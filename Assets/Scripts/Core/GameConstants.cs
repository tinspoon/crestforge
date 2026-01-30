using UnityEngine;

namespace Crestforge.Core
{
    /// <summary>
    /// Central configuration for all game constants.
    /// Adjust these values to balance the game.
    /// </summary>
    public static class GameConstants
    {
        // ===========================================
        // GRID & BOARD
        // ===========================================
        public static class Grid
        {
            public const int WIDTH = 5;
            public const int HEIGHT = 4;
            public const float HEX_SIZE = 1f;
            public const int TOTAL_BATTLEFIELD_HEIGHT = 8; // Player + Enemy grids
        }

        // ===========================================
        // ECONOMY
        // ===========================================
        public static class Economy
        {
            public const int BASE_GOLD_PER_TURN = 5;
            public const int INTEREST_PER_5_GOLD = 1;
            public const int MAX_INTEREST = 4;
            public const int REROLL_COST = 1;
            public const int XP_COST = 4;
            public const int XP_PER_PURCHASE = 4;
            public const int SHOP_SIZE = 4;
        }

        // ===========================================
        // LEVELING
        // ===========================================
        public static class Leveling
        {
            public const int MAX_LEVEL = 6;
            public static readonly int[] XP_REQUIRED = { 0, 0, 2, 6, 10, 20, 36 };
            public static readonly int[] UNITS_PER_LEVEL = { 0, 1, 2, 3, 4, 5, 6 };
        }

        // ===========================================
        // SHOP ODDS - [level, cost-1] = percentage
        // ===========================================
        public static class ShopOdds
        {
            public static readonly int[,] UNIT_ODDS = {
                { 0, 0, 0, 0 },
                { 100, 0, 0, 0 },
                { 75, 25, 0, 0 },
                { 55, 30, 15, 0 },
                { 40, 35, 20, 5 },
                { 25, 35, 30, 10 },
                { 15, 25, 35, 25 },
            };
        }

        // ===========================================
        // UNITS
        // ===========================================
        public static class Units
        {
            public const int POOL_SIZE = 6;
            public const int MAX_STAR_LEVEL = 4;
            public const int UNITS_TO_MERGE = 2;
            public static readonly float[] STAR_MULTIPLIERS = { 0f, 1.0f, 1.8f, 3.2f, 5.5f };
        }

        // ===========================================
        // COMBAT
        // ===========================================
        public static class Combat
        {
            public const float TICK_RATE = 0.1f;
            public const float BASE_ATTACK_SPEED = 1.0f;
            public const float GLOBAL_ATTACK_SPEED_MULTIPLIER = 0.8f; // Slows all attacks by 20%
            public const float MOVE_COOLDOWN = 0.4f; // Time between unit movements
            public const float ATTACK_DELAY_AFTER_MOVE = 0.35f; // Delay before attacking after moving (lets visual catch up)
            public const int MANA_PER_ATTACK = 10;
            public const int MANA_PER_DAMAGE_TAKEN = 5;
            public const int MAX_MANA = 100;
        }

        // ===========================================
        // ROUNDS
        // ===========================================
        public static class Rounds
        {
            public const int MIN_ROUNDS = 5;
            public const int MAX_ROUNDS = 15;
            public const int ITEM_ROUND_1 = 0;
            public const int ITEM_ROUND_2 = 4;
            public const int ITEM_ROUND_3 = 8;
            public const int ITEMS_PER_SELECTION = 5;
            public const int STARTING_ITEMS = 2;
            public const int ROUND_4_ITEMS = 2;
            public const int ROUND_8_ITEMS = 1;
        }

        // ===========================================
        // PLAYER
        // ===========================================
        public static class Player
        {
            public const int STARTING_HEALTH = 100;
            public const int STARTING_GOLD = 10;
            public const int STARTING_LEVEL = 1;
            public const int BENCH_SIZE = 8;
        }

        // ===========================================
        // ITEMS
        // ===========================================
        public static class Items
        {
            public const int MAX_PER_UNIT = 2;
        }

        // ===========================================
        // CRESTS
        // ===========================================
        public static class Crests
        {
            public const int MINOR_SLOTS = 2;
            public const int MAJOR_SLOTS = 1;
            public const int STARTING_MINOR_CRESTS = 1;
        }
    }
}
