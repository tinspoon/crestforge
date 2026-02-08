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
            public const int BASE_GOLD_PER_ROUND = 3;
            public const int BASE_GOLD_PER_TURN = BASE_GOLD_PER_ROUND;  // Alias
            public const int INTEREST_PER_5_GOLD = 1;
            public const int MAX_INTEREST = 3;           // Cap at 15 gold saved
            public const int REROLL_COST = 1;
            public const int XP_COST = 2;                // Cost to buy XP
            public const int XP_PER_PURCHASE = 2;        // XP gained per purchase
            public const int SHOP_SIZE = 4;

            // Win/lose streaks
            public const int STREAK_BONUS_AT_2 = 1;     // +1g at 2 streak
            public const int STREAK_BONUS_AT_4 = 2;     // +2g at 4+ streak
        }

        // ===========================================
        // LEVELING
        // ===========================================
        public static class Leveling
        {
            public const int MAX_LEVEL = 6;
            public const int FREE_XP_PER_ROUND = 1;     // Passive 1 XP per round

            // XP required to reach each level (cumulative)
            // Level:  1    2    3    4     5     6
            // To next: -    2    2    4     6    10
            public static readonly int[] XP_REQUIRED = { 0, 0, 2, 4, 8, 14, 24 };

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
                // Level 2: 2-costs start appearing
                { 80, 20, 0, 0, 0 },
                // Level 3: 3-costs start appearing
                { 60, 30, 10, 0, 0 },
                // Level 4: 4-costs start appearing
                { 35, 30, 25, 10, 0 },
                // Level 5: 5-costs appear
                { 20, 25, 25, 25, 5 },
                // Level 6: Best odds for high-cost
                { 10, 15, 25, 25, 25 },
            };
        }

        // ===========================================
        // UNITS
        // ===========================================
        public static class Units
        {
            public const int MAX_STAR_LEVEL = 3;
            public const int UNITS_TO_MERGE = 2;         // 2 copies to merge (pair system)

            // Pool sizes by tier
            public const int TIER_1_POOL = 8;
            public const int TIER_2_POOL = 8;
            public const int TIER_3_POOL = 7;
            public const int TIER_4_POOL = 6;
            public const int TIER_5_POOL = 6;

            // Stat multipliers by star level
            public static readonly float[] STAR_MULTIPLIERS = { 0f, 1.0f, 1.5f, 2.0f };

            // Unit counts by cost tier
            public const int TIER_1_COUNT = 12;
            public const int TIER_2_COUNT = 10;
            public const int TIER_3_COUNT = 9;
            public const int TIER_4_COUNT = 7;
            public const int TIER_5_COUNT = 5;
            public const int TOTAL_UNITS = 43;

            // Pool size lookup by tier (1-indexed)
            public static int GetPoolSize(int tier)
            {
                switch (tier)
                {
                    case 1: return TIER_1_POOL;
                    case 2: return TIER_2_POOL;
                    case 3: return TIER_3_POOL;
                    case 4: return TIER_4_POOL;
                    case 5: return TIER_5_POOL;
                    default: return TIER_1_POOL;
                }
            }
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

            // Status effect defaults
            public const float BLEED_TICK_RATE = 1f;     // Bleed ticks every 1s
            public const float BURN_TICK_RATE = 1f;      // Burn ticks every 1s
            public const float FROST_MOVE_SLOW = 0.3f;   // 30% move speed reduction
            public const float FROST_AS_SLOW = 0.3f;     // 30% attack speed reduction
        }

        // ===========================================
        // ROUNDS
        // ===========================================
        public static class Rounds
        {
            public const int TOTAL_ROUNDS = 14;
            public const int MAX_ROUNDS = TOTAL_ROUNDS;
            public const float PLANNING_PHASE_DURATION = 20f;
            public const float EARLY_PLANNING_DURATION = 35f;
            public const float COMBAT_PHASE_DURATION = 60f;
            public const float MAD_MERCHANT_DURATION = 45f;
            public const float CREST_SELECTION_DURATION = 25f;

            public const int ITEMS_PER_SELECTION = 3;
            public const int ITEM_ROUND_2 = 7;
            public const int ITEM_ROUND_3 = 11;

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
            public const int STARTING_HEALTH = 15;
            public const int STARTING_LEVEL = 1;
            public const int BENCH_SIZE = 7;

            // Damage calculation: 1 + surviving enemy units
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
            public const int MAX_INVENTORY = 10;
            public const int ITEMS_FROM_ANVIL = 1;
            public const int ITEMS_FROM_MERCHANTS = 2;
            public const int ITEMS_FROM_BOSS = 1;
            public const int ITEM_ANVIL_CHOICES = 3;
        }

        // ===========================================
        // CRESTS
        // ===========================================
        public static class Crests
        {
            public const int MAX_MINOR_CRESTS = 1;
            public const int MAX_MAJOR_CRESTS = 1;
            public const int MINOR_SLOTS = MAX_MINOR_CRESTS;
            public const int MAJOR_SLOTS = MAX_MAJOR_CRESTS;
            public const int CREST_CHOICES = 3;
        }

        // ===========================================
        // TRAITS
        // ===========================================
        public static class Traits
        {
            public const int SHARED_TRAIT_COUNT = 15;
            public const int UNIQUE_TRAIT_COUNT = 2;

            // Standard breakpoints for shared traits
            public static readonly int[] BREAKPOINTS = { 2, 4 };

            // Crestmaker crafting interval
            public const int CRESTMAKER_ROUNDS = 2;      // Minor crest token every 2 rounds
        }

        // ===========================================
        // SELL PRICES
        // ===========================================
        public static class Selling
        {
            // 1-star: sell for cost
            // 2-star: sell for (2 * cost) - 1
            // 3-star: sell for (4 * cost) - 1
            public static int GetSellPrice(int cost, int starLevel)
            {
                switch (starLevel)
                {
                    case 1: return cost;
                    case 2: return cost * 2 - 1;
                    case 3: return cost * 4 - 1;
                    default: return cost;
                }
            }
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
