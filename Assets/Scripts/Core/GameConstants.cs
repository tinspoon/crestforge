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
            public const int STARTING_GOLD = 4;
            public const int BASE_GOLD_PER_ROUND = 4;
            public const int BASE_GOLD_PER_TURN = BASE_GOLD_PER_ROUND;  // Alias
            public const int INTEREST_PER_5_GOLD = 1;
            public const int MAX_INTEREST = 3;           // At 15 gold
            public const int REROLL_COST = 2;
            public const int XP_COST = 4;                // Cost to buy XP
            public const int XP_PER_PURCHASE = 2;        // XP gained per purchase
            public const int SHOP_SIZE = 4;
            public const int CREST_TOKEN_COST = 3;       // Cost to buy crest token from shop
        }

        // ===========================================
        // LEVELING
        // ===========================================
        public static class Leveling
        {
            public const int MAX_LEVEL = 6;
            public const int FREE_XP_PER_ROUND = 2;

            // XP required to reach each level (cumulative)
            // Level: 1    2    3    4     5     6
            public static readonly int[] XP_REQUIRED = { 0, 0, 2, 6, 12, 24, 36 };

            // Units allowed on board at each level
            public static readonly int[] UNITS_PER_LEVEL = { 0, 1, 2, 3, 4, 5, 6 };
        }

        // ===========================================
        // SHOP ODDS - [level, cost-1] = percentage
        // Costs: 1, 2, 3, 4, 5
        // ===========================================
        public static class ShopOdds
        {
            // Odds for each unit cost tier by player level
            // Format: { 1-cost%, 2-cost%, 3-cost%, 4-cost%, 5-cost% }
            public static readonly int[,] UNIT_ODDS = {
                // Level 0 (unused)
                { 0, 0, 0, 0, 0 },
                // Level 1: Only 1-costs
                { 100, 0, 0, 0, 0 },
                // Level 2: Only 1-costs
                { 100, 0, 0, 0, 0 },
                // Level 3: 2-costs start appearing
                { 75, 25, 0, 0, 0 },
                // Level 4: 3-costs start appearing
                { 55, 30, 15, 0, 0 },
                // Level 5: 4-costs start appearing
                { 30, 40, 25, 5, 0 },
                // Level 6: 5-costs appear, best odds for high-cost
                { 15, 25, 30, 20, 10 },
            };

            // Chance for crest token to appear in shop (by level)
            // Higher at low levels to encourage early direction
            public static readonly int[] CREST_TOKEN_CHANCE = { 0, 20, 20, 15, 10, 0, 0 };
        }

        // ===========================================
        // UNITS
        // ===========================================
        public static class Units
        {
            public const int COPIES_PER_UNIT = 20;       // Copies of each unit in pool
            public const int POOL_SIZE = COPIES_PER_UNIT;    // Alias
            public const int MAX_STAR_LEVEL = 3;
            public const int UNITS_TO_2_STAR = 2;        // Pairs to upgrade
            public const int UNITS_TO_3_STAR = 4;        // 2 pairs (two 2-stars)

            // Stat multipliers by star level
            public static readonly float[] STAR_MULTIPLIERS = { 0f, 1.0f, 1.5f, 2.0f };

            // Unit counts by cost tier
            public const int TIER_1_COUNT = 10;
            public const int TIER_2_COUNT = 10;
            public const int TIER_3_COUNT = 8;
            public const int TIER_4_COUNT = 7;
            public const int TIER_5_COUNT = 5;
            public const int TOTAL_UNITS = 40;
        }

        // ===========================================
        // COMBAT
        // ===========================================
        public static class Combat
        {
            public const float TICK_RATE = 0.1f;
            public const float BASE_ATTACK_SPEED = 1.0f;
            public const float GLOBAL_ATTACK_SPEED_MULTIPLIER = 0.8f;
            public const float MOVE_COOLDOWN = 0.4f;
            public const float ATTACK_DELAY_AFTER_MOVE = 0.35f;
            public const int MANA_PER_ATTACK = 10;
            public const int MANA_PER_DAMAGE_TAKEN = 5;
            public const int MAX_MANA = 100;
        }

        // ===========================================
        // ROUNDS
        // ===========================================
        public static class Rounds
        {
            public const int TOTAL_ROUNDS = 14;
            public const int MAX_ROUNDS = TOTAL_ROUNDS;          // Alias for compatibility
            public const float PLANNING_PHASE_DURATION = 20f;    // Seconds
            public const float EARLY_PLANNING_DURATION = 35f;    // Rounds 1-3, extra time
            public const float COMBAT_PHASE_DURATION = 60f;      // Max combat time before draw
            public const float MAD_MERCHANT_DURATION = 45f;      // Time for merchant round
            public const float CREST_SELECTION_DURATION = 25f;   // Time to pick crest

            // Item selection rounds (0-indexed, add 1 for display round number)
            public const int ITEMS_PER_SELECTION = 3;            // Choose 1 of 3 items
            public const int ITEM_ROUND_2 = 7;                   // Round 8: PvELoot
            public const int ITEM_ROUND_3 = 11;                  // Round 12: PvEBoss

            // Round types by round number (0-indexed internally, 1-indexed for display)
            // See RoundType enum for values
            public static readonly RoundType[] ROUND_TYPES = {
                RoundType.PvEIntro,      // Round 1: Minor Crest + Item Anvil
                RoundType.PvP,           // Round 2
                RoundType.PvP,           // Round 3
                RoundType.MadMerchant,   // Round 4
                RoundType.PvP,           // Round 5
                RoundType.MajorCrest,    // Round 6
                RoundType.PvP,           // Round 7
                RoundType.PvELoot,       // Round 8: Random loot
                RoundType.PvP,           // Round 9
                RoundType.MadMerchant,   // Round 10
                RoundType.PvP,           // Round 11
                RoundType.PvEBoss,       // Round 12: Boss fight, item drop
                RoundType.PvP,           // Round 13
                RoundType.PvP,           // Round 14: Final
            };
        }

        // ===========================================
        // PLAYER
        // ===========================================
        public static class Player
        {
            public const int STARTING_HEALTH = 20;
            public const int STARTING_LEVEL = 1;
            public const int BENCH_SIZE = 7;

            // Damage calculation: BASE + (surviving enemy units * MULTIPLIER)
            public const int LOSS_DAMAGE_BASE = 1;
            public const int LOSS_DAMAGE_PER_UNIT = 1;
        }

        // ===========================================
        // MAD MERCHANT
        // ===========================================
        public static class MadMerchant
        {
            public const int TOTAL_OPTIONS = 12;
            public const int FIRST_PLACE_GOLD_BONUS = 3;
            public const int SECOND_PLACE_GOLD_BONUS = 2;
        }

        // ===========================================
        // ITEMS
        // ===========================================
        public static class Items
        {
            public const int MAX_PER_UNIT = 3;
            public const int MAX_INVENTORY = 10;         // Max items player can hold
            public const int ITEMS_FROM_ANVIL = 1;       // Round 1
            public const int ITEMS_FROM_MERCHANTS = 2;   // Rounds 4, 10 (if player chooses)
            public const int ITEMS_FROM_BOSS = 1;        // Round 12
            public const int ITEM_ANVIL_CHOICES = 3;     // Choose 1 of 3
        }

        // ===========================================
        // CRESTS
        // ===========================================
        public static class Crests
        {
            public const int MAX_MINOR_CRESTS = 1;       // 1 minor crest, upgradeable
            public const int MAX_MAJOR_CRESTS = 1;       // 1 major crest, not upgradeable
            public const int MINOR_SLOTS = MAX_MINOR_CRESTS;  // Alias
            public const int MAJOR_SLOTS = MAX_MAJOR_CRESTS;  // Alias
            public const int CREST_CHOICES = 3;          // Choose 1 of 3
        }

        // ===========================================
        // TRAITS
        // ===========================================
        public static class Traits
        {
            public const int SHARED_TRAIT_COUNT = 14;
            public const int UNIQUE_TRAIT_COUNT = 2;

            // Breakpoints for shared traits
            public static readonly int[] BREAKPOINTS = { 2, 4 };
        }
    }

    // ===========================================
    // ENUMS
    // ===========================================

    /// <summary>
    /// Types of rounds in the game
    /// </summary>
    public enum RoundType
    {
        PvP,            // Standard player vs player
        PvEIntro,       // Round 1: Tutorial PvE, grants Minor Crest + Item Anvil
        PvELoot,        // Mid-game PvE with random loot
        PvEBoss,        // Final PvE boss, guaranteed item
        MadMerchant,    // Carousel-style picking round
        MajorCrest,     // Choose major crest
    }
}
