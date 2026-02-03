using System.Collections.Generic;
using UnityEngine;

namespace Crestforge.Core
{
    /// <summary>
    /// All traits in the game.
    /// 14 shared traits with (2)/(4) breakpoints.
    /// 2 unique traits (one per unit).
    /// </summary>
    public enum TraitType
    {
        // === SHARED TRAITS (14) ===
        // Each has (2) and (4) breakpoints

        Attuned,        // Random element pair chosen at game start, bonus damage with that type
        Forged,         // Permanent stacking stats each round of combat
        Scavenger,      // Random unit after each round (cost = level-1, max 4)
        Invigorating,   // Adjacent allies heal X HP per second
        Reflective,     // X% damage reflected to attacker
        Mitigation,     // X% less damage taken
        Bruiser,        // Gain X bonus HP
        Overkill,       // Half of overkill damage dealt to nearby enemy
        Gigamega,       // Abilities cost 20% more mana but deal 25/40% more damage
        FirstBlood,     // First attack deals X bonus damage
        Momentum,       // Each kill grants +10/15% move and attack speed (max 3 stacks)
        Cleave,         // Attacks hit enemies adjacent to target
        Fury,           // Gain attack speed after every attack (max 15 stacks)
        Volatile,       // Summons explode on death dealing X damage

        // === UNIQUE TRAITS (2) ===
        // Only one unit has each, always active

        Treasure,       // After winning a round, gain random reward (gold, item component, consumable)
        Crestmaker,     // Crafts crest token after 3 rounds, major crest token after 6
    }

    /// <summary>
    /// Defines a bonus granted at a trait breakpoint.
    /// </summary>
    [System.Serializable]
    public class TraitBreakpoint
    {
        public int unitsRequired;       // How many units needed for this tier
        public string description;      // Human-readable description
        public float value1;            // Primary bonus value
        public float value2;            // Secondary bonus value (optional)
        public float value3;            // Tertiary bonus value (optional)
    }

    /// <summary>
    /// Complete definition of a trait including all breakpoints.
    /// </summary>
    [System.Serializable]
    public class TraitInfo
    {
        public TraitType traitType;
        public string displayName;
        public string description;
        public bool isUnique;           // If true, only one unit has this trait
        public Sprite icon;
        public List<TraitBreakpoint> breakpoints = new List<TraitBreakpoint>();
    }

    /// <summary>
    /// Static definitions for all traits.
    /// </summary>
    public static class TraitDefinitions
    {
        private static Dictionary<TraitType, TraitInfo> _traits;

        public static Dictionary<TraitType, TraitInfo> All
        {
            get
            {
                if (_traits == null)
                    InitializeTraits();
                return _traits;
            }
        }

        public static TraitInfo Get(TraitType type) => All[type];

        private static void InitializeTraits()
        {
            _traits = new Dictionary<TraitType, TraitInfo>();

            // === SHARED TRAITS ===

            _traits[TraitType.Attuned] = new TraitInfo
            {
                traitType = TraitType.Attuned,
                displayName = "Attuned",
                description = "At game start, a random pair of elements is chosen. Attuned units deal that damage type and gain bonus damage.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "+15% damage with attuned element", value1 = 0.15f },
                    new TraitBreakpoint { unitsRequired = 4, description = "+30% damage with attuned element", value1 = 0.30f }
                }
            };

            _traits[TraitType.Forged] = new TraitInfo
            {
                traitType = TraitType.Forged,
                displayName = "Forged",
                description = "Forged units gain permanent stats for every round of combat they participate in.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "+2 AD, +2 AP per round", value1 = 2f, value2 = 2f },
                    new TraitBreakpoint { unitsRequired = 4, description = "+4 AD, +4 AP, +20 HP per round", value1 = 4f, value2 = 4f, value3 = 20f }
                }
            };

            _traits[TraitType.Scavenger] = new TraitInfo
            {
                traitType = TraitType.Scavenger,
                displayName = "Scavenger",
                description = "After each round, gain a random unit. Cost equals your level minus 1 (max 4-cost).",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "50% chance to gain a unit", value1 = 0.5f },
                    new TraitBreakpoint { unitsRequired = 4, description = "100% chance to gain a unit", value1 = 1.0f }
                }
            };

            _traits[TraitType.Invigorating] = new TraitInfo
            {
                traitType = TraitType.Invigorating,
                displayName = "Invigorating",
                description = "Adjacent allies heal HP per second. Stacks with multiple Invigorating units.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "Heal 3 HP/sec to adjacent allies", value1 = 3f },
                    new TraitBreakpoint { unitsRequired = 4, description = "Heal 6 HP/sec to adjacent allies", value1 = 6f }
                }
            };

            _traits[TraitType.Reflective] = new TraitInfo
            {
                traitType = TraitType.Reflective,
                displayName = "Reflective",
                description = "Reflective units reflect a portion of damage taken back to the attacker.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "Reflect 15% of damage taken", value1 = 0.15f },
                    new TraitBreakpoint { unitsRequired = 4, description = "Reflect 30% of damage taken", value1 = 0.30f }
                }
            };

            _traits[TraitType.Mitigation] = new TraitInfo
            {
                traitType = TraitType.Mitigation,
                displayName = "Mitigation",
                description = "Mitigation units take reduced damage from all sources.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "Take 10% less damage", value1 = 0.10f },
                    new TraitBreakpoint { unitsRequired = 4, description = "Take 20% less damage", value1 = 0.20f }
                }
            };

            _traits[TraitType.Bruiser] = new TraitInfo
            {
                traitType = TraitType.Bruiser,
                displayName = "Bruiser",
                description = "Bruiser units gain bonus maximum HP.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "+150 bonus HP", value1 = 150f },
                    new TraitBreakpoint { unitsRequired = 4, description = "+350 bonus HP", value1 = 350f }
                }
            };

            _traits[TraitType.Overkill] = new TraitInfo
            {
                traitType = TraitType.Overkill,
                displayName = "Overkill",
                description = "When an Overkill unit kills an enemy, excess damage is dealt to a nearby enemy.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "50% of overkill damage splashes", value1 = 0.5f },
                    new TraitBreakpoint { unitsRequired = 4, description = "100% of overkill damage splashes", value1 = 1.0f }
                }
            };

            _traits[TraitType.Gigamega] = new TraitInfo
            {
                traitType = TraitType.Gigamega,
                displayName = "Gigamega",
                description = "Gigamega units' abilities cost more mana but deal increased damage.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "+20% mana cost, +25% ability damage", value1 = 0.20f, value2 = 0.25f },
                    new TraitBreakpoint { unitsRequired = 4, description = "+20% mana cost, +40% ability damage", value1 = 0.20f, value2 = 0.40f }
                }
            };

            _traits[TraitType.FirstBlood] = new TraitInfo
            {
                traitType = TraitType.FirstBlood,
                displayName = "First Blood",
                description = "First Blood units deal bonus damage on their first attack each combat.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "First attack deals +50% damage", value1 = 0.5f },
                    new TraitBreakpoint { unitsRequired = 4, description = "First attack deals +100% damage", value1 = 1.0f }
                }
            };

            _traits[TraitType.Momentum] = new TraitInfo
            {
                traitType = TraitType.Momentum,
                displayName = "Momentum",
                description = "Each kill grants movement and attack speed, stacking up to 3 times.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "+10% speed per kill (max 30%)", value1 = 0.10f },
                    new TraitBreakpoint { unitsRequired = 4, description = "+15% speed per kill (max 45%)", value1 = 0.15f }
                }
            };

            _traits[TraitType.Cleave] = new TraitInfo
            {
                traitType = TraitType.Cleave,
                displayName = "Cleave",
                description = "Cleave units' attacks hit enemies adjacent to their target.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "Cleave for 25% damage", value1 = 0.25f },
                    new TraitBreakpoint { unitsRequired = 4, description = "Cleave for 50% damage", value1 = 0.50f }
                }
            };

            _traits[TraitType.Fury] = new TraitInfo
            {
                traitType = TraitType.Fury,
                displayName = "Fury",
                description = "Fury units gain attack speed after every attack, stacking up to 15 times.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "+3% attack speed per attack", value1 = 0.03f },
                    new TraitBreakpoint { unitsRequired = 4, description = "+5% attack speed per attack", value1 = 0.05f }
                }
            };

            _traits[TraitType.Volatile] = new TraitInfo
            {
                traitType = TraitType.Volatile,
                displayName = "Volatile",
                description = "Volatile units explode on death, dealing damage to nearby enemies.",
                isUnique = false,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 2, description = "Explode for 100 damage on death", value1 = 100f },
                    new TraitBreakpoint { unitsRequired = 4, description = "Explode for 250 damage on death", value1 = 250f }
                }
            };

            // === UNIQUE TRAITS ===

            _traits[TraitType.Treasure] = new TraitInfo
            {
                traitType = TraitType.Treasure,
                displayName = "Treasure",
                description = "After winning a round, gain a random reward: gold, item component, or consumable.",
                isUnique = true,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 1, description = "Gain random reward on win", value1 = 1f }
                }
            };

            _traits[TraitType.Crestmaker] = new TraitInfo
            {
                traitType = TraitType.Crestmaker,
                displayName = "Crestmaker",
                description = "Crafts a crest token after 3 rounds of combat. Crafts a major crest token after 6 rounds.",
                isUnique = true,
                breakpoints = new List<TraitBreakpoint>
                {
                    new TraitBreakpoint { unitsRequired = 1, description = "Craft crest tokens over time", value1 = 3f, value2 = 6f }
                }
            };
        }

        /// <summary>
        /// Gets the active bonus for a trait based on how many units have it.
        /// Returns null if the breakpoint is not met.
        /// </summary>
        public static TraitBreakpoint GetActiveBonus(TraitType type, int unitCount)
        {
            var trait = Get(type);
            TraitBreakpoint activeBonus = null;

            foreach (var bonus in trait.breakpoints)
            {
                if (unitCount >= bonus.unitsRequired)
                    activeBonus = bonus;
            }

            return activeBonus;
        }

        /// <summary>
        /// Gets all shared traits (non-unique).
        /// </summary>
        public static IEnumerable<TraitInfo> GetSharedTraits()
        {
            foreach (var trait in All.Values)
            {
                if (!trait.isUnique)
                    yield return trait;
            }
        }

        /// <summary>
        /// Gets all unique traits.
        /// </summary>
        public static IEnumerable<TraitInfo> GetUniqueTraits()
        {
            foreach (var trait in All.Values)
            {
                if (trait.isUnique)
                    yield return trait;
            }
        }
    }
}
