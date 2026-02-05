/**
 * Game Constants and Unit Data for Server-Side Logic
 */

const GameConstants = {
    Grid: {
        WIDTH: 7,
        HEIGHT: 4
    },
    Player: {
        BENCH_SIZE: 7,
        STARTING_LEVEL: 1,
        MAX_LEVEL: 9
    },
    Economy: {
        STARTING_GOLD: 10,
        BASE_GOLD_PER_TURN: 5,
        MAX_INTEREST: 5,
        REROLL_COST: 2,
        XP_COST: 4,
        XP_PER_PURCHASE: 4,
        SHOP_SIZE: 5
    },
    Units: {
        POOL_SIZE: 20,
        MAX_STAR_LEVEL: 3
    },
    Leveling: {
        // Units allowed per level
        UNITS_PER_LEVEL: {
            1: 1, 2: 2, 3: 3, 4: 4, 5: 5, 6: 6, 7: 7, 8: 8, 9: 9
        },
        // XP required to reach each level
        XP_REQUIRED: {
            1: 0, 2: 2, 3: 6, 4: 10, 5: 20, 6: 36, 7: 56, 8: 80, 9: 100
        }
    },
    // Shop odds by player level (% for 1-cost, 2-cost, 3-cost, 4-cost, 5-cost)
    ShopOdds: {
        1: [100, 0, 0, 0, 0],
        2: [100, 0, 0, 0, 0],
        3: [75, 25, 0, 0, 0],
        4: [55, 30, 15, 0, 0],
        5: [45, 33, 20, 2, 0],
        6: [30, 40, 25, 5, 0],
        7: [20, 35, 30, 12, 3],
        8: [15, 25, 30, 22, 8],
        9: [10, 15, 25, 30, 20]
    },
    Rounds: {
        MAX_ROUNDS: 14,
        PLANNING_DURATION: 20,
        PVE_INTRO_PLANNING_DURATION: 5,
        COMBAT_MAX_DURATION: 60,
        RESULTS_DURATION: 3,
        // Round types: 'pve_intro', 'pvp', 'mad_merchant', 'major_crest', 'pve_loot', 'pve_boss'
        ROUND_TYPES: [
            'pve_intro',    // Round 1
            'pvp',          // Round 2
            'pvp',          // Round 3
            'mad_merchant', // Round 4
            'pvp',          // Round 5
            'major_crest',  // Round 6
            'pvp',          // Round 7
            'pve_loot',     // Round 8
            'pvp',          // Round 9
            'mad_merchant', // Round 10
            'pvp',          // Round 11
            'pve_boss',     // Round 12
            'pvp',          // Round 13
            'pvp'           // Round 14
        ]
    }
};

// Unit templates - synced from Unity ScriptableObjects (NewUnits folder)
const UnitTemplates = {
    // ============================================
    // 1-COST UNITS (10 units)
    // ============================================
    'archer': {
        unitId: 'archer',
        name: 'Archer',
        cost: 1,
        traits: ['firstblood', 'fury'],
        stats: { health: 400, attack: 55, armor: 15, magicResist: 15, attackSpeed: 0.8, range: 4, mana: 50, moveSpeed: 1.75 }
    },
    'bat': {
        unitId: 'bat',
        name: 'Bat',
        cost: 1,
        traits: ['fury', 'momentum'],
        stats: { health: 400, attack: 55, armor: 15, magicResist: 15, attackSpeed: 0.9, range: 1, mana: 40, moveSpeed: 2.15 }
    },
    'blueslime': {
        unitId: 'blueslime',
        name: 'Blue Slime',
        cost: 1,
        traits: ['bruiser', 'mitigation'],
        stats: { health: 550, attack: 40, armor: 25, magicResist: 25, attackSpeed: 0.6, range: 1, mana: 60, moveSpeed: 1.75 }
    },
    'crawler': {
        unitId: 'crawler',
        name: 'Crawler',
        cost: 1,
        traits: ['scavenger', 'fury'],
        stats: { health: 420, attack: 50, armor: 25, magicResist: 15, attackSpeed: 0.75, range: 1, mana: 50, moveSpeed: 1.75 }
    },
    'greenspider': {
        unitId: 'greenspider',
        name: 'Green Spider',
        cost: 1,
        traits: ['reflective', 'mitigation'],
        stats: { health: 500, attack: 40, armor: 30, magicResist: 25, attackSpeed: 0.65, range: 1, mana: 55, moveSpeed: 1.75 }
    },
    'littledemon': {
        unitId: 'littledemon',
        name: 'Little Demon',
        cost: 1,
        traits: ['firstblood', 'overkill', 'attuned'],
        stats: { health: 400, attack: 60, armor: 15, magicResist: 20, attackSpeed: 0.8, range: 1, mana: 45, moveSpeed: 1.75 }
    },
    'mushroom': {
        unitId: 'mushroom',
        name: 'Mushroom',
        cost: 1,
        traits: ['invigorating', 'volatile'],
        stats: { health: 480, attack: 35, armor: 20, magicResist: 30, attackSpeed: 0.6, range: 1, mana: 60, moveSpeed: 1.75 }
    },
    'ratassassin': {
        unitId: 'ratassassin',
        name: 'Rat Assassin',
        cost: 1,
        traits: ['firstblood', 'momentum'],
        stats: { health: 380, attack: 65, armor: 15, magicResist: 15, attackSpeed: 0.85, range: 1, mana: 40, moveSpeed: 2.15 }
    },
    'redslime': {
        unitId: 'redslime',
        name: 'Red Slime',
        cost: 1,
        traits: ['scavenger', 'volatile'],
        stats: { health: 450, attack: 45, armor: 20, magicResist: 20, attackSpeed: 0.65, range: 1, mana: 50, moveSpeed: 1.75 }
    },
    'starfish': {
        unitId: 'starfish',
        name: 'Starfish',
        cost: 1,
        traits: ['invigorating', 'reflective'],
        stats: { health: 520, attack: 35, armor: 20, magicResist: 25, attackSpeed: 0.55, range: 1, mana: 65, moveSpeed: 1.75 }
    },

    // ============================================
    // 2-COST UNITS (10 units)
    // ============================================
    'battlebee': {
        unitId: 'battlebee',
        name: 'Battle Bee',
        cost: 2,
        traits: ['fury', 'cleave'],
        stats: { health: 480, attack: 60, armor: 20, magicResist: 20, attackSpeed: 0.85, range: 1, mana: 45, moveSpeed: 1.75 }
    },
    'blacksmith': {
        unitId: 'blacksmith',
        name: 'Blacksmith',
        cost: 2,
        traits: ['crestmaker'],
        stats: { health: 650, attack: 50, armor: 30, magicResist: 30, attackSpeed: 0.6, range: 1, mana: 80, moveSpeed: 1.75 }
    },
    'chestmonster': {
        unitId: 'chestmonster',
        name: 'Chest Monster',
        cost: 2,
        traits: ['treasure'],
        stats: { health: 600, attack: 55, armor: 35, magicResist: 25, attackSpeed: 0.6, range: 1, mana: 60, moveSpeed: 1.75 }
    },
    'crabmonster': {
        unitId: 'crabmonster',
        name: 'Crab Monster',
        cost: 2,
        traits: ['bruiser', 'reflective'],
        stats: { health: 700, attack: 50, armor: 45, magicResist: 25, attackSpeed: 0.55, range: 1, mana: 70, moveSpeed: 1.75 }
    },
    'evilplant': {
        unitId: 'evilplant',
        name: 'Evil Plant',
        cost: 2,
        traits: ['invigorating', 'volatile'],
        stats: { health: 580, attack: 50, armor: 25, magicResist: 30, attackSpeed: 0.6, range: 2, mana: 60, moveSpeed: 1.75 }
    },
    'fishman': {
        unitId: 'fishman',
        name: 'Fishman',
        cost: 2,
        traits: ['scavenger', 'cleave'],
        stats: { health: 600, attack: 55, armor: 30, magicResist: 20, attackSpeed: 0.7, range: 1, mana: 60, moveSpeed: 1.75 }
    },
    'flowermonster': {
        unitId: 'flowermonster',
        name: 'Flower Monster',
        cost: 2,
        traits: ['invigorating', 'gigamega'],
        stats: { health: 500, attack: 45, armor: 20, magicResist: 35, attackSpeed: 0.6, range: 3, mana: 80, moveSpeed: 1.75 }
    },
    'golem': {
        unitId: 'golem',
        name: 'Golem',
        cost: 2,
        traits: ['forged', 'bruiser'],
        stats: { health: 800, attack: 50, armor: 40, magicResist: 25, attackSpeed: 0.5, range: 1, mana: 75, moveSpeed: 1.75 }
    },
    'salamander': {
        unitId: 'salamander',
        name: 'Salamander',
        cost: 2,
        traits: ['forged', 'overkill'],
        stats: { health: 550, attack: 65, armor: 25, magicResist: 30, attackSpeed: 0.7, range: 2, mana: 55, moveSpeed: 1.75 }
    },
    'wormmonster': {
        unitId: 'wormmonster',
        name: 'Worm Monster',
        cost: 2,
        traits: ['bruiser', 'mitigation'],
        stats: { health: 750, attack: 45, armor: 35, magicResist: 30, attackSpeed: 0.5, range: 1, mana: 70, moveSpeed: 1.75 }
    },

    // ============================================
    // 3-COST UNITS (8 units)
    // ============================================
    'beholder': {
        unitId: 'beholder',
        name: 'Beholder',
        cost: 3,
        traits: ['gigamega', 'volatile'],
        stats: { health: 550, attack: 65, armor: 20, magicResist: 45, attackSpeed: 0.6, range: 4, mana: 70, moveSpeed: 1.75 }
    },
    'cleric': {
        unitId: 'cleric',
        name: 'Cleric',
        cost: 3,
        traits: ['invigorating', 'attuned'],
        stats: { health: 700, attack: 45, armor: 30, magicResist: 45, attackSpeed: 0.55, range: 3, mana: 100, moveSpeed: 1.75 }
    },
    'cyclops': {
        unitId: 'cyclops',
        name: 'Cyclops',
        cost: 3,
        traits: ['overkill', 'cleave'],
        stats: { health: 850, attack: 90, armor: 35, magicResist: 25, attackSpeed: 0.6, range: 1, mana: 70, moveSpeed: 1.75 }
    },
    'icegolem': {
        unitId: 'icegolem',
        name: 'Ice Golem',
        cost: 3,
        traits: ['bruiser', 'attuned'],
        stats: { health: 950, attack: 55, armor: 45, magicResist: 40, attackSpeed: 0.5, range: 1, mana: 80, moveSpeed: 1.75 }
    },
    'lizardwarrior': {
        unitId: 'lizardwarrior',
        name: 'Lizard Warrior',
        cost: 3,
        traits: ['cleave', 'forged'],
        stats: { health: 800, attack: 75, armor: 40, magicResist: 25, attackSpeed: 0.7, range: 1, mana: 65, moveSpeed: 1.75 }
    },
    'nagawizard': {
        unitId: 'nagawizard',
        name: 'Naga Wizard',
        cost: 3,
        traits: ['gigamega', 'scavenger', 'attuned'],
        stats: { health: 650, attack: 55, armor: 25, magicResist: 50, attackSpeed: 0.6, range: 4, mana: 90, moveSpeed: 1.75 }
    },
    'specter': {
        unitId: 'specter',
        name: 'Specter',
        cost: 3,
        traits: ['gigamega', 'reflective'],
        stats: { health: 600, attack: 60, armor: 20, magicResist: 45, attackSpeed: 0.65, range: 3, mana: 75, moveSpeed: 1.75 }
    },
    'werewolf': {
        unitId: 'werewolf',
        name: 'Werewolf',
        cost: 3,
        traits: ['momentum', 'firstblood'],
        stats: { health: 750, attack: 85, armor: 30, magicResist: 25, attackSpeed: 0.85, range: 1, mana: 55, moveSpeed: 1.75 }
    },

    // ============================================
    // 4-COST UNITS (8 units)
    // ============================================
    'bishopknight': {
        unitId: 'bishopknight',
        name: 'Bishop Knight',
        cost: 4,
        traits: ['mitigation', 'reflective'],
        stats: { health: 1100, attack: 70, armor: 60, magicResist: 50, attackSpeed: 0.5, range: 1, mana: 90, moveSpeed: 1.75 }
    },
    'blackknight': {
        unitId: 'blackknight',
        name: 'Black Knight',
        cost: 4,
        traits: ['forged', 'cleave'],
        stats: { health: 1000, attack: 95, armor: 55, magicResist: 35, attackSpeed: 0.65, range: 1, mana: 80, moveSpeed: 1.75 }
    },
    'bonedragon': {
        unitId: 'bonedragon',
        name: 'Bone Dragon',
        cost: 4,
        traits: ['volatile', 'attuned'],
        stats: { health: 900, attack: 80, armor: 35, magicResist: 40, attackSpeed: 0.6, range: 2, mana: 85, moveSpeed: 1.75 }
    },
    'eviloldmage': {
        unitId: 'eviloldmage',
        name: 'Evil Old Mage',
        cost: 4,
        traits: ['gigamega', 'scavenger'],
        stats: { health: 700, attack: 70, armor: 25, magicResist: 55, attackSpeed: 0.55, range: 4, mana: 95, moveSpeed: 1.75 }
    },
    'fatdragon': {
        unitId: 'fatdragon',
        name: 'Fat Dragon',
        cost: 4,
        traits: ['bruiser', 'cleave'],
        stats: { health: 1200, attack: 85, armor: 45, magicResist: 40, attackSpeed: 0.5, range: 1, mana: 85, moveSpeed: 1.75 }
    },
    'flyingdemon': {
        unitId: 'flyingdemon',
        name: 'Flying Demon',
        cost: 4,
        traits: ['momentum', 'overkill'],
        stats: { health: 850, attack: 100, armor: 30, magicResist: 35, attackSpeed: 0.75, range: 1, mana: 70, moveSpeed: 1.75 }
    },
    'orcwithmace': {
        unitId: 'orcwithmace',
        name: 'Orc with Mace',
        cost: 4,
        traits: ['firstblood', 'overkill'],
        stats: { health: 950, attack: 110, armor: 40, magicResist: 30, attackSpeed: 0.6, range: 1, mana: 75, moveSpeed: 1.75 }
    },

    // ============================================
    // 5-COST UNITS (5 units)
    // ============================================
    'castlemonster': {
        unitId: 'castlemonster',
        name: 'Castle Monster',
        cost: 5,
        traits: ['bruiser', 'forged'],
        stats: { health: 1500, attack: 80, armor: 60, magicResist: 50, attackSpeed: 0.4, range: 1, mana: 100, moveSpeed: 1.75 }
    },
    'demonking': {
        unitId: 'demonking',
        name: 'Demon King',
        cost: 5,
        traits: ['overkill', 'momentum'],
        stats: { health: 1100, attack: 120, armor: 45, magicResist: 45, attackSpeed: 0.65, range: 1, mana: 100, moveSpeed: 1.75 }
    },
    'flameknight': {
        unitId: 'flameknight',
        name: 'Flame Knight',
        cost: 5,
        traits: ['volatile', 'overkill'],
        stats: { health: 950, attack: 105, armor: 40, magicResist: 45, attackSpeed: 0.65, range: 1, mana: 85, moveSpeed: 1.75 }
    },
    'skeletonmage': {
        unitId: 'skeletonmage',
        name: 'Skeleton Mage',
        cost: 5,
        traits: ['gigamega', 'attuned'],
        stats: { health: 750, attack: 75, armor: 25, magicResist: 55, attackSpeed: 0.55, range: 4, mana: 120, moveSpeed: 1.75 }
    },
    'spikyshellturtle': {
        unitId: 'spikyshellturtle',
        name: 'Spiky Shell Turtle',
        cost: 5,
        traits: ['reflective', 'mitigation'],
        stats: { health: 1400, attack: 60, armor: 70, magicResist: 60, attackSpeed: 0.35, range: 1, mana: 90, moveSpeed: 1.75 }
    },

    // ============================================
    // PVE-ONLY UNITS (not available in shop)
    // ============================================
    'stingray': {
        unitId: 'stingray',
        name: 'Stingray',
        cost: 0, // Cost 0 = not available in shop
        traits: [],
        stats: { health: 80, attack: 15, armor: 0, magicResist: 0, attackSpeed: 0.6, range: 1, mana: 0, moveSpeed: 1.75 },
        isPvE: true
    },
    'cactus': {
        unitId: 'cactus',
        name: 'Cactus',
        cost: 0,
        traits: [],
        stats: { health: 100, attack: 10, armor: 5, magicResist: 0, attackSpeed: 0.5, range: 1, mana: 0, moveSpeed: 1.75 },
        isPvE: true
    }
};

// ============================================
// TRAIT DEFINITIONS
// ============================================

const TraitDefinitions = {
    // ============================================
    // SHARED TRAITS (2/4 breakpoints)
    // ============================================
    'attuned': {
        traitId: 'attuned',
        name: 'Attuned',
        description: 'Attuned units deal bonus elemental damage',
        tiers: [
            { count: 2, bonus: { damagePercent: 15 } },
            { count: 4, bonus: { damagePercent: 30 } }
        ]
    },
    'forged': {
        traitId: 'forged',
        name: 'Forged',
        description: 'Gain permanent stats each combat round',
        tiers: [
            { count: 2, bonus: { stackingAD: 2, stackingAP: 2 } },
            { count: 4, bonus: { stackingAD: 4, stackingAP: 4, stackingHP: 20 } }
        ]
    },
    'scavenger': {
        traitId: 'scavenger',
        name: 'Scavenger',
        description: 'Gain a random unit after each round',
        tiers: [
            { count: 2, bonus: { unitChance: 50 } },
            { count: 4, bonus: { unitChance: 100 } }
        ]
    },
    'invigorating': {
        traitId: 'invigorating',
        name: 'Invigorating',
        description: 'Adjacent allies heal HP per second',
        tiers: [
            { count: 2, bonus: { healPerSecond: 3 } },
            { count: 4, bonus: { healPerSecond: 6 } }
        ]
    },
    'reflective': {
        traitId: 'reflective',
        name: 'Reflective',
        description: 'Reflect damage back to attacker',
        tiers: [
            { count: 2, bonus: { reflectPercent: 15 } },
            { count: 4, bonus: { reflectPercent: 30 } }
        ]
    },
    'mitigation': {
        traitId: 'mitigation',
        name: 'Mitigation',
        description: 'Take reduced damage from all sources',
        tiers: [
            { count: 2, bonus: { damageReduction: 10 } },
            { count: 4, bonus: { damageReduction: 20 } }
        ]
    },
    'bruiser': {
        traitId: 'bruiser',
        name: 'Bruiser',
        description: 'Gain bonus maximum HP',
        tiers: [
            { count: 2, bonus: { health: 150 } },
            { count: 4, bonus: { health: 350 } }
        ]
    },
    'overkill': {
        traitId: 'overkill',
        name: 'Overkill',
        description: 'Excess damage splashes to nearby enemy',
        tiers: [
            { count: 2, bonus: { overkillPercent: 50 } },
            { count: 4, bonus: { overkillPercent: 100 } }
        ]
    },
    'gigamega': {
        traitId: 'gigamega',
        name: 'Gigamega',
        description: 'Abilities cost more but deal more damage',
        tiers: [
            { count: 2, bonus: { manaCostPercent: 20, abilityDamagePercent: 25 } },
            { count: 4, bonus: { manaCostPercent: 20, abilityDamagePercent: 40 } }
        ]
    },
    'firstblood': {
        traitId: 'firstblood',
        name: 'First Blood',
        description: 'First attack deals bonus damage',
        tiers: [
            { count: 2, bonus: { firstAttackPercent: 50 } },
            { count: 4, bonus: { firstAttackPercent: 100 } }
        ]
    },
    'momentum': {
        traitId: 'momentum',
        name: 'Momentum',
        description: 'Kills grant speed (max 3 stacks)',
        tiers: [
            { count: 2, bonus: { speedPerKill: 10 } },
            { count: 4, bonus: { speedPerKill: 15 } }
        ]
    },
    'cleave': {
        traitId: 'cleave',
        name: 'Cleave',
        description: 'Attacks hit adjacent enemies',
        tiers: [
            { count: 2, bonus: { cleavePercent: 25 } },
            { count: 4, bonus: { cleavePercent: 50 } }
        ]
    },
    'fury': {
        traitId: 'fury',
        name: 'Fury',
        description: 'Gain attack speed per attack (max 15)',
        tiers: [
            { count: 2, bonus: { attackSpeedPerHit: 3 } },
            { count: 4, bonus: { attackSpeedPerHit: 5 } }
        ]
    },
    'volatile': {
        traitId: 'volatile',
        name: 'Volatile',
        description: 'Explode on death dealing damage',
        tiers: [
            { count: 2, bonus: { explosionDamage: 100 } },
            { count: 4, bonus: { explosionDamage: 250 } }
        ]
    },

    // ============================================
    // UNIQUE TRAITS (always active at 1)
    // ============================================
    'treasure': {
        traitId: 'treasure',
        name: 'Treasure',
        description: 'Gain random reward on victory',
        tiers: [
            { count: 1, bonus: { rewardOnWin: true } }
        ]
    },
    'crestmaker': {
        traitId: 'crestmaker',
        name: 'Crestmaker',
        description: 'Crafts crest tokens over time',
        tiers: [
            { count: 1, bonus: { craftCrest: true } }
        ]
    }
};

// ============================================
// ITEM DEFINITIONS
// ============================================

const ItemTemplates = {
    // Basic Components
    'sword': {
        itemId: 'sword',
        name: 'Long Sword',
        description: '+15 Attack Damage',
        isComponent: true,
        stats: { attack: 15 }
    },
    'bow': {
        itemId: 'bow',
        name: 'Recurve Bow',
        description: '+15% Attack Speed',
        isComponent: true,
        stats: { attackSpeedPercent: 15 }
    },
    'rod': {
        itemId: 'rod',
        name: 'Needlessly Large Rod',
        description: '+15 Ability Power',
        isComponent: true,
        stats: { abilityPower: 15 }
    },
    'vest': {
        itemId: 'vest',
        name: 'Chain Vest',
        description: '+25 Armor',
        isComponent: true,
        stats: { armor: 25 }
    },
    'cloak': {
        itemId: 'cloak',
        name: 'Negatron Cloak',
        description: '+25 Magic Resist',
        isComponent: true,
        stats: { magicResist: 25 }
    },
    'belt': {
        itemId: 'belt',
        name: "Giant's Belt",
        description: '+200 Health',
        isComponent: true,
        stats: { health: 200 }
    },
    'tear': {
        itemId: 'tear',
        name: 'Tear of the Goddess',
        description: '+15 Starting Mana',
        isComponent: true,
        stats: { mana: 15 }
    },
    'glove': {
        itemId: 'glove',
        name: 'Sparring Gloves',
        description: '+10% Crit Chance',
        isComponent: true,
        stats: { critChance: 10 }
    },

    // Combined Items
    'bloodthirster': {
        itemId: 'bloodthirster',
        name: 'Bloodthirster',
        description: '+30 Attack, Lifesteal 25%',
        isComponent: false,
        recipe: ['sword', 'sword'],
        stats: { attack: 30 },
        effects: { lifesteal: 25 }
    },
    'rapidfire': {
        itemId: 'rapidfire',
        name: 'Rapid Firecannon',
        description: '+30% Attack Speed, +1 Range',
        isComponent: false,
        recipe: ['bow', 'bow'],
        stats: { attackSpeedPercent: 30, range: 1 }
    },
    'deathcap': {
        itemId: 'deathcap',
        name: "Rabadon's Deathcap",
        description: '+50 Ability Power',
        isComponent: false,
        recipe: ['rod', 'rod'],
        stats: { abilityPower: 50 }
    },
    'thornmail': {
        itemId: 'thornmail',
        name: 'Thornmail',
        description: '+50 Armor, Reflect 25% damage',
        isComponent: false,
        recipe: ['vest', 'vest'],
        stats: { armor: 50 },
        effects: { reflect: 25 }
    },
    'warmog': {
        itemId: 'warmog',
        name: "Warmog's Armor",
        description: '+500 Health, Regen 3% HP/sec',
        isComponent: false,
        recipe: ['belt', 'belt'],
        stats: { health: 500 },
        effects: { hpRegen: 3 }
    },
    'infinity': {
        itemId: 'infinity',
        name: 'Infinity Edge',
        description: '+25 Attack, +30% Crit Damage',
        isComponent: false,
        recipe: ['sword', 'glove'],
        stats: { attack: 25, critDamagePercent: 30 }
    },
    'guardian_angel': {
        itemId: 'guardian_angel',
        name: 'Guardian Angel',
        description: '+20 Armor, +20 MR, Revive once',
        isComponent: false,
        recipe: ['vest', 'cloak'],
        stats: { armor: 20, magicResist: 20 },
        effects: { revive: true }
    }
};

// ============================================
// CREST DEFINITIONS
// ============================================

const CrestTemplates = {
    // Minor Crests - Team-wide passive stat bonuses
    'minor_might': {
        crestId: 'minor_might',
        name: 'Crest of Might',
        description: 'All units gain +10 attack damage',
        type: 'minor',
        teamBonus: { attack: 10 }
    },
    'minor_vitality': {
        crestId: 'minor_vitality',
        name: 'Crest of Vitality',
        description: 'All units gain +100 health',
        type: 'minor',
        teamBonus: { health: 100 }
    },
    'minor_swiftness': {
        crestId: 'minor_swiftness',
        name: 'Crest of Swiftness',
        description: 'All units gain +10% attack speed',
        type: 'minor',
        teamBonus: { attackSpeedPercent: 10 }
    },
    'minor_protection': {
        crestId: 'minor_protection',
        name: 'Crest of Protection',
        description: 'All units gain +15 armor',
        type: 'minor',
        teamBonus: { armor: 15 }
    },
    'minor_warding': {
        crestId: 'minor_warding',
        name: 'Crest of Warding',
        description: 'All units gain +15 magic resist',
        type: 'minor',
        teamBonus: { magicResist: 15 }
    },
    'minor_precision': {
        crestId: 'minor_precision',
        name: 'Crest of Precision',
        description: 'All units gain +10% crit chance',
        type: 'minor',
        teamBonus: { critChance: 10 }
    },
    'minor_sorcery': {
        crestId: 'minor_sorcery',
        name: 'Crest of Sorcery',
        description: 'All units gain +10 ability power',
        type: 'minor',
        teamBonus: { abilityPower: 10 }
    },

    // Major Crests - Team-wide bonuses
    'major_bloodlust': {
        crestId: 'major_bloodlust',
        name: 'Bloodlust Crest',
        description: 'All units gain 10% lifesteal',
        type: 'major',
        teamBonus: { lifesteal: 10 }
    },
    'major_fortitude': {
        crestId: 'major_fortitude',
        name: 'Fortitude Crest',
        description: 'All units gain +150 health',
        type: 'major',
        teamBonus: { health: 150 }
    },
    'major_haste': {
        crestId: 'major_haste',
        name: 'Haste Crest',
        description: 'All units gain +15% attack speed',
        type: 'major',
        teamBonus: { attackSpeedPercent: 15 }
    },
    'major_arcane': {
        crestId: 'major_arcane',
        name: 'Arcane Crest',
        description: 'All units gain +20 ability power',
        type: 'major',
        teamBonus: { abilityPower: 20 }
    },
    'major_iron': {
        crestId: 'major_iron',
        name: 'Iron Crest',
        description: 'All units gain +20 armor and MR',
        type: 'major',
        teamBonus: { armor: 20, magicResist: 20 }
    }
};

// ============================================
// UTILITY FUNCTIONS
// ============================================

// Get all units as array
function getAllUnits() {
    return Object.values(UnitTemplates);
}

// Get units by cost
function getUnitsByCost(cost) {
    return getAllUnits().filter(u => u.cost === cost);
}

// Calculate star-scaled stats
function getStarScaledStats(baseStats, starLevel) {
    const multiplier = Math.pow(1.8, starLevel - 1);
    return {
        health: Math.round(baseStats.health * multiplier),
        attack: Math.round(baseStats.attack * multiplier),
        armor: baseStats.armor + (starLevel - 1) * 5,
        magicResist: baseStats.magicResist + (starLevel - 1) * 5,
        attackSpeed: baseStats.attackSpeed,
        range: baseStats.range,
        mana: baseStats.mana,
        moveSpeed: baseStats.moveSpeed
    };
}

// Calculate active traits for a set of units
// Note: Minor crests now provide stat bonuses rather than trait bonuses
function calculateActiveTraits(units) {
    const traitCounts = {};

    // Count traits from all units
    for (const unit of units) {
        if (!unit) continue;

        // Count base traits from unit template
        if (unit.traits) {
            for (const traitId of unit.traits) {
                traitCounts[traitId] = (traitCounts[traitId] || 0) + 1;
            }
        }
    }

    // Determine active trait tiers (include ALL traits for UI display)
    const activeTraits = {};
    for (const [traitId, count] of Object.entries(traitCounts)) {
        const traitDef = TraitDefinitions[traitId];
        if (!traitDef) continue;

        // Find highest tier achieved (null if not yet activated)
        let activeTier = null;
        for (const tier of traitDef.tiers) {
            if (count >= tier.count) {
                activeTier = tier;
            }
        }

        // Include ALL traits so UI can show inactive ones (e.g., "1/2")
        activeTraits[traitId] = {
            count,
            tier: activeTier,
            definition: traitDef
        };
    }

    return activeTraits;
}

// Apply trait bonuses to unit stats
function applyTraitBonuses(stats, activeTraits, unitTraits) {
    const modifiedStats = { ...stats };

    for (const [traitId, traitInfo] of Object.entries(activeTraits)) {
        // Skip traits that haven't reached a tier yet
        if (!traitInfo.tier) continue;

        const bonus = traitInfo.tier.bonus;

        // Only apply trait bonus if unit has the trait (for most traits)
        const unitHasTrait = unitTraits.includes(traitId);

        // Bruiser: flat health bonus
        if (bonus.health && unitHasTrait) {
            modifiedStats.health += bonus.health;
        }

        // Health percentage bonus
        if (bonus.healthPercent && unitHasTrait) {
            modifiedStats.health = Math.round(modifiedStats.health * (1 + bonus.healthPercent / 100));
        }

        // Attack percentage bonus
        if (bonus.attackPercent && unitHasTrait) {
            modifiedStats.attack = Math.round(modifiedStats.attack * (1 + bonus.attackPercent / 100));
        }

        // Attack speed percentage bonus
        if (bonus.attackSpeedPercent && unitHasTrait) {
            modifiedStats.attackSpeed *= (1 + bonus.attackSpeedPercent / 100);
        }

        // Flat armor bonus
        if (bonus.armor && unitHasTrait) {
            modifiedStats.armor += bonus.armor;
        }

        // Flat magic resist bonus
        if (bonus.magicResist && unitHasTrait) {
            modifiedStats.magicResist += bonus.magicResist;
        }

        // Damage reduction (mitigation trait)
        if (bonus.damageReduction && unitHasTrait) {
            modifiedStats.damageReduction = (modifiedStats.damageReduction || 0) + bonus.damageReduction;
        }

        // All stats percentage bonus (Dragon trait)
        if (bonus.allStatsPercent && unitHasTrait) {
            const mult = 1 + bonus.allStatsPercent / 100;
            modifiedStats.health = Math.round(modifiedStats.health * mult);
            modifiedStats.attack = Math.round(modifiedStats.attack * mult);
            modifiedStats.armor = Math.round(modifiedStats.armor * mult);
            modifiedStats.magicResist = Math.round(modifiedStats.magicResist * mult);
        }
    }

    return modifiedStats;
}

// Apply item bonuses to unit stats
function applyItemBonuses(stats, items) {
    const modifiedStats = { ...stats };

    for (const item of items) {
        if (!item || !item.stats) continue;

        if (item.stats.attack) modifiedStats.attack += item.stats.attack;
        if (item.stats.health) modifiedStats.health += item.stats.health;
        if (item.stats.armor) modifiedStats.armor += item.stats.armor;
        if (item.stats.magicResist) modifiedStats.magicResist += item.stats.magicResist;
        if (item.stats.mana) modifiedStats.mana += item.stats.mana;
        if (item.stats.range) modifiedStats.range += item.stats.range;
        if (item.stats.attackSpeedPercent) {
            modifiedStats.attackSpeed *= (1 + item.stats.attackSpeedPercent / 100);
        }
    }

    return modifiedStats;
}

// Apply crest bonuses to unit stats
// Crests can have rank 1-3 with multipliers: 1x, 1.5x, 2x
function applyCrestBonuses(stats, crests) {
    const modifiedStats = { ...stats };

    for (const crest of crests) {
        if (!crest || !crest.teamBonus) continue;

        // Rank multiplier: rank 1 = 1x, rank 2 = 1.5x, rank 3 = 2x
        const rank = crest.rank || 1;
        const rankMultiplier = rank === 3 ? 2 : (rank === 2 ? 1.5 : 1);

        const bonus = crest.teamBonus;
        if (bonus.health) modifiedStats.health += Math.round(bonus.health * rankMultiplier);
        if (bonus.attack) modifiedStats.attack += Math.round(bonus.attack * rankMultiplier);
        if (bonus.armor) modifiedStats.armor += Math.round(bonus.armor * rankMultiplier);
        if (bonus.magicResist) modifiedStats.magicResist += Math.round(bonus.magicResist * rankMultiplier);
        if (bonus.attackSpeedPercent) {
            modifiedStats.attackSpeed *= (1 + (bonus.attackSpeedPercent * rankMultiplier) / 100);
        }
        if (bonus.critChance) {
            modifiedStats.critChance = (modifiedStats.critChance || 0) + bonus.critChance * rankMultiplier;
        }
        if (bonus.abilityPower) {
            modifiedStats.abilityPower = (modifiedStats.abilityPower || 0) + Math.round(bonus.abilityPower * rankMultiplier);
        }
        if (bonus.lifesteal) {
            modifiedStats.lifesteal = (modifiedStats.lifesteal || 0) + bonus.lifesteal * rankMultiplier;
        }
    }

    return modifiedStats;
}

module.exports = {
    GameConstants,
    UnitTemplates,
    TraitDefinitions,
    ItemTemplates,
    CrestTemplates,
    getAllUnits,
    getUnitsByCost,
    getStarScaledStats,
    calculateActiveTraits,
    applyTraitBonuses,
    applyItemBonuses,
    applyCrestBonuses
};
