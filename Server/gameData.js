/**
 * Game Constants and Unit Data for Server-Side Logic
 * All game logic is server-authoritative — this is the source of truth.
 */

const GameConstants = {
    Grid: {
        WIDTH: 5,
        HEIGHT: 4
    },
    Player: {
        BENCH_SIZE: 7,
        STARTING_LEVEL: 1,
        MAX_LEVEL: 6,
        STARTING_HEALTH: 15,
        LOSS_DAMAGE_BASE: 1,        // Damage = 1 + surviving enemy units
        LOSS_DAMAGE_PER_UNIT: 1
    },
    Economy: {
        STARTING_GOLD: 4,
        BASE_GOLD_PER_TURN: 3,
        INTEREST_PER_5_GOLD: 1,
        MAX_INTEREST: 3,            // Cap at 15g saved
        REROLL_COST: 1,
        XP_COST: 2,
        XP_PER_PURCHASE: 2,
        SHOP_SIZE: 4,
        STREAK_BONUS_AT_2: 1,      // +1g at 2 win/loss streak
        STREAK_BONUS_AT_4: 2       // +2g at 4+ streak
    },
    Units: {
        MAX_STAR_LEVEL: 3,
        UNITS_TO_MERGE: 2,         // 2 copies to merge
        // Pool sizes by tier
        POOL_SIZES: { 1: 8, 2: 8, 3: 7, 4: 6, 5: 6 },
        getPoolSize(tier) {
            return this.POOL_SIZES[tier] || 8;
        }
    },
    Leveling: {
        FREE_XP_PER_ROUND: 1,      // Passive 1 XP per round
        UNITS_PER_LEVEL: { 1: 1, 2: 2, 3: 3, 4: 4, 5: 5, 6: 6 },
        // XP required to reach each level (cumulative)
        // To-next: -, 2, 2, 4, 6, 10
        XP_REQUIRED: { 1: 0, 2: 2, 3: 4, 4: 8, 5: 14, 6: 24 }
    },
    ShopOdds: {
        1: [100, 0, 0, 0, 0],
        2: [80, 20, 0, 0, 0],
        3: [60, 30, 10, 0, 0],
        4: [35, 30, 25, 10, 0],
        5: [20, 25, 25, 25, 5],
        6: [10, 15, 25, 25, 25]
    },
    Rounds: {
        MAX_ROUNDS: 14,
        PLANNING_DURATION: 20,
        PVE_INTRO_PLANNING_DURATION: 5,
        COMBAT_MAX_DURATION: 60,
        RESULTS_DURATION: 3,
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
    },
    Combat: {
        TICK_RATE: 0.1,
        BASE_ATTACK_SPEED: 1.0,
        GLOBAL_ATTACK_SPEED_MULTIPLIER: 0.8,
        MOVE_COOLDOWN: 0.4,
        ATTACK_DELAY_AFTER_MOVE: 0.35,
        MANA_PER_ATTACK: 10,
        MANA_PER_DAMAGE_TAKEN: 5,
        MAX_MANA: 100,
        BLEED_TICK_RATE: 1,
        BURN_TICK_RATE: 1,
        FROST_MOVE_SLOW: 0.3,
        FROST_AS_SLOW: 0.3
    },
    Selling: {
        // 1-star: cost, 2-star: 2*cost - 1, 3-star: 4*cost - 1
        getSellPrice(cost, starLevel) {
            switch (starLevel) {
                case 1: return cost;
                case 2: return cost * 2 - 1;
                case 3: return cost * 4 - 1;
                default: return cost;
            }
        }
    },
    Traits: {
        CRESTMAKER_ROUNDS: 2       // Minor crest token every 2 rounds
    }
};

// ============================================
// DAMAGE TYPES
// ============================================
const DamageTypes = {
    PHYSICAL: 'physical',
    FIRE: 'fire',
    ARCANE: 'arcane',
    NATURE: 'nature',
    SHADOW: 'shadow'
};

// Attuned can only roll elemental types (not Physical)
const ATTUNED_ELEMENTS = ['fire', 'arcane', 'nature', 'shadow'];

// ============================================
// STATUS EFFECTS
// ============================================
const StatusEffects = {
    BLEED: 'bleed',     // Physical DoT
    FROST: 'frost',     // Move + AS slow
    BURN: 'burn',       // Fire DoT
    POISON: 'poison',   // Nature DoT
    STUN: 'stun',
    ROOT: 'root'
};

// ============================================
// PER-GAME VARIABLES
// ============================================
const PerGameVariables = {
    // Blessed bonus options (rolled once per game)
    BLESSED_BONUSES: [
        { id: 'life', name: 'Life', desc: 'All allies gain 8% omnivamp', tier2Desc: 'All allies gain 15% omnivamp' },
        { id: 'mana', name: 'Mana', desc: 'All allies start with +15 mana', tier2Desc: 'All allies start with +30 mana' },
        { id: 'unity', name: 'Unity', desc: 'All allies deal +3% damage per ally', tier2Desc: 'All allies deal +5% damage per ally' },
        { id: 'devotion', name: 'Devotion', desc: 'Blessed individual buffs doubled', tier2Desc: 'Blessed individual buffs tripled' },
        { id: 'aegis', name: 'Aegis', desc: 'All allies start with 100 HP shield', tier2Desc: 'All allies start with 200 HP shield' },
        { id: 'vigor', name: 'Vigor', desc: 'All allies regen 3 HP/s', tier2Desc: 'All allies regen 6 HP/s' },
        { id: 'fortune', name: 'Fortune', desc: '+1 gold per round', tier2Desc: '+2 gold per round' }
    ],
    // Warlord enhancement options (rolled once per game)
    WARLORD_ENHANCEMENTS: [
        { id: 'bloodlust', name: 'Bloodlust', desc: 'Warlord units gain 10% lifesteal', tier2Desc: '20% lifesteal' },
        { id: 'precision', name: 'Precision', desc: 'Warlord units gain +15% crit chance', tier2Desc: '+25% crit chance' },
        { id: 'frenzy', name: 'Frenzy', desc: 'Warlord units gain +15% attack speed', tier2Desc: '+25% attack speed' },
        { id: 'brutality', name: 'Brutality', desc: 'Warlord units gain +15 attack damage', tier2Desc: '+30 attack damage' },
        { id: 'shatter', name: 'Shatter', desc: 'Warlord attacks reduce armor by 20%', tier2Desc: 'Reduce armor by 35%' }
    ]
};

// ============================================
// UNIT TEMPLATES - 43 units
// ============================================
const UnitTemplates = {
    // ============================================
    // TIER 1 (12 units, cost 1, pool 8)
    // ============================================
    'footman': {
        unitId: 'footman', name: 'Footman', cost: 1,
        traits: ['vanguard', 'legion'],
        damageAffinity: 'physical', elementalAutoattacks: false,
        stats: { health: 550, attack: 45, abilityPower: 0, armor: 30, magicResist: 20, attackSpeed: 0.7, range: 1, mana: 40, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Shield Bash', desc: 'Stun target 1s, deal 150% AD Physical', type: 'damage', damageType: 'physical', baseDamage: 0, adRatio: 1.5, apRatio: 0, effect: 'stun', effectDuration: 1, targeting: 'currentTarget' }
    },
    'duelist': {
        unitId: 'duelist', name: 'Duelist', cost: 1,
        traits: ['warlord', 'forged'],
        damageAffinity: 'physical', elementalAutoattacks: false,
        stats: { health: 500, attack: 55, abilityPower: 0, armor: 20, magicResist: 15, attackSpeed: 0.75, range: 1, mana: 40, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Twin Slash', desc: 'Deal 200% AD Physical. On kill: +20% AS for 5s', type: 'damage', damageType: 'physical', baseDamage: 0, adRatio: 2.0, apRatio: 0, effect: 'none', targeting: 'currentTarget', onKill: { buff: 'attackSpeed', value: 0.2, duration: 5 } }
    },
    'skeletonwarrior': {
        unitId: 'skeletonwarrior', name: 'Skeleton Warrior', cost: 1,
        traits: ['scavenger', 'forged'],
        damageAffinity: 'physical', elementalAutoattacks: false,
        stats: { health: 480, attack: 50, abilityPower: 0, armor: 25, magicResist: 15, attackSpeed: 0.7, range: 1, mana: 40, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Bone Shatter', desc: 'Deal 180% AD Physical + Bleed 50 over 3s', type: 'damage', damageType: 'physical', baseDamage: 0, adRatio: 1.8, apRatio: 0, effect: 'bleed', effectDuration: 3, effectDps: 17, targeting: 'currentTarget' }
    },
    'archer': {
        unitId: 'archer', name: 'Archer', cost: 1,
        traits: ['legion', 'attuned'],
        damageAffinity: 'physical', elementalAutoattacks: false,
        stats: { health: 380, attack: 55, abilityPower: 0, armor: 15, magicResist: 15, attackSpeed: 0.8, range: 4, mana: 30, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Piercing Shot', desc: 'Line pierce dealing 200% AD (Attuned element)', type: 'damage', damageType: 'attuned', baseDamage: 0, adRatio: 2.0, apRatio: 0, effect: 'none', targeting: 'lineFromCaster', pierces: true }
    },
    'elfranger': {
        unitId: 'elfranger', name: 'Elf Ranger', cost: 1,
        traits: ['nature', 'attuned'],
        damageAffinity: 'arcane', elementalAutoattacks: false,
        stats: { health: 370, attack: 50, abilityPower: 20, armor: 15, magicResist: 20, attackSpeed: 0.8, range: 4, mana: 40, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Frost Arrow', desc: 'Deal 180% AD Arcane + Frost 30% AS slow 3s', type: 'damage', damageType: 'arcane', baseDamage: 0, adRatio: 1.8, apRatio: 0, effect: 'frost', effectDuration: 3, effectSlowPercent: 0.3, targeting: 'currentTarget' }
    },
    'ratassassin': {
        unitId: 'ratassassin', name: 'Rat Assassin', cost: 1,
        traits: ['shadow', 'scavenger'],
        damageAffinity: 'shadow', elementalAutoattacks: false,
        stats: { health: 350, attack: 60, abilityPower: 0, armor: 15, magicResist: 15, attackSpeed: 0.85, range: 1, mana: 30, moveSpeed: 2.25, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Backstab', desc: 'Teleport behind target, 250% AD Shadow, guaranteed crit', type: 'damage', damageType: 'shadow', baseDamage: 0, adRatio: 2.5, apRatio: 0, effect: 'none', targeting: 'currentTarget', teleports: true, guaranteedCrit: true }
    },
    'bat': {
        unitId: 'bat', name: 'Bat', cost: 1,
        traits: ['wild', 'shadow'],
        damageAffinity: 'shadow', elementalAutoattacks: false,
        stats: { health: 380, attack: 50, abilityPower: 15, armor: 15, magicResist: 15, attackSpeed: 0.9, range: 1, mana: 30, moveSpeed: 2.25, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Sonic Screech', desc: 'AoE 100 Shadow + Frost 20% slow 2s', type: 'areaDamage', damageType: 'shadow', baseDamage: 100, adRatio: 0, apRatio: 0, effect: 'frost', effectDuration: 2, effectSlowPercent: 0.2, radius: 1, targeting: 'nearbyEnemies' }
    },
    'crawler': {
        unitId: 'crawler', name: 'Crawler', cost: 1,
        traits: ['ironclad', 'wild'],
        damageAffinity: 'nature', elementalAutoattacks: false,
        stats: { health: 450, attack: 48, abilityPower: 0, armor: 30, magicResist: 15, attackSpeed: 0.7, range: 1, mana: 40, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Burrow Strike', desc: 'Emerge under target, 200% AD Nature, stun 0.5s', type: 'damage', damageType: 'nature', baseDamage: 0, adRatio: 2.0, apRatio: 0, effect: 'stun', effectDuration: 0.5, targeting: 'currentTarget', teleports: true }
    },
    'redslime': {
        unitId: 'redslime', name: 'Red Slime', cost: 1,
        traits: ['scavenger', 'volatile'],
        damageAffinity: 'fire', elementalAutoattacks: false,
        stats: { health: 420, attack: 40, abilityPower: 20, armor: 20, magicResist: 20, attackSpeed: 0.65, range: 1, mana: 40, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Volatile Spit', desc: '150 Fire + burning area 2s', type: 'areaDamage', damageType: 'fire', baseDamage: 150, adRatio: 0, apRatio: 0, effect: 'burn', effectDuration: 2, effectDps: 30, radius: 1, targeting: 'currentTarget' }
    },
    'mushroom': {
        unitId: 'mushroom', name: 'Mushroom', cost: 1,
        traits: ['nature', 'volatile'],
        damageAffinity: 'nature', elementalAutoattacks: false,
        stats: { health: 460, attack: 30, abilityPower: 25, armor: 20, magicResist: 30, attackSpeed: 0.55, range: 1, mana: 50, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Spore Cloud', desc: 'Heal adjacent allies 100 HP + 80 Nature dmg adj enemies', type: 'healAndDamage', damageType: 'nature', baseDamage: 80, baseHealing: 100, adRatio: 0, apRatio: 0, effect: 'none', radius: 1, targeting: 'nearbyEnemies' }
    },
    'littledemon': {
        unitId: 'littledemon', name: 'Little Demon', cost: 1,
        traits: ['attuned', 'volatile'],
        damageAffinity: 'fire', elementalAutoattacks: true,
        stats: { health: 380, attack: 55, abilityPower: 25, armor: 15, magicResist: 20, attackSpeed: 0.75, range: 1, mana: 30, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Hellfire Bolt', desc: '250 Fire to lowest HP enemy', type: 'damage', damageType: 'fire', baseDamage: 250, adRatio: 0, apRatio: 0, effect: 'none', targeting: 'lowestHealthEnemy' }
    },
    'cleric': {
        unitId: 'cleric', name: 'Cleric', cost: 1,
        traits: ['blessed', 'ironclad'],
        damageAffinity: 'arcane', elementalAutoattacks: false,
        stats: { health: 500, attack: 35, abilityPower: 20, armor: 25, magicResist: 30, attackSpeed: 0.55, range: 3, mana: 50, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        isBlessed: true, blessedStat: 'attackDamage', blessedValue: 5,
        ability: { name: 'Divine Light', desc: 'Heal lowest ally 200 HP + 10 Armor/MR 4s', type: 'healAndBuff', damageType: 'arcane', baseDamage: 0, baseHealing: 200, adRatio: 0, apRatio: 0, effect: 'none', duration: 4, targeting: 'lowestHealthAlly', buff: { armor: 10, magicResist: 10 } }
    },

    // ============================================
    // TIER 2 (10 units, cost 2, pool 8)
    // ============================================
    'knight': {
        unitId: 'knight', name: 'Knight', cost: 2,
        traits: ['legion', 'ironclad'],
        damageAffinity: 'physical', elementalAutoattacks: false,
        stats: { health: 700, attack: 55, abilityPower: 0, armor: 40, magicResist: 25, attackSpeed: 0.6, range: 1, mana: 50, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Shield Wall', desc: '30% DR 3s, next attack 200% AD Physical', type: 'damageAndBuff', damageType: 'physical', baseDamage: 0, adRatio: 2.0, apRatio: 0, effect: 'damageReduction', effectDuration: 3, effectValue: 0.3, targeting: 'self' }
    },
    'dryad': {
        unitId: 'dryad', name: 'Dryad', cost: 2,
        traits: ['blessed', 'nature'],
        damageAffinity: 'nature', elementalAutoattacks: false,
        stats: { health: 580, attack: 40, abilityPower: 30, armor: 20, magicResist: 35, attackSpeed: 0.6, range: 3, mana: 50, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        isBlessed: true, blessedStat: 'health', blessedValue: 100,
        ability: { name: "Nature's Embrace", desc: 'Heal ally 250 HP + 15% AS 4s', type: 'healAndBuff', damageType: 'nature', baseDamage: 0, baseHealing: 250, adRatio: 0, apRatio: 0, effect: 'none', duration: 4, targeting: 'lowestHealthAlly', buff: { attackSpeed: 0.15 } }
    },
    'mage': {
        unitId: 'mage', name: 'Mage', cost: 2,
        traits: ['blessed', 'attuned'],
        damageAffinity: 'arcane', elementalAutoattacks: true,
        stats: { health: 450, attack: 40, abilityPower: 40, armor: 15, magicResist: 30, attackSpeed: 0.6, range: 4, mana: 40, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        isBlessed: true, blessedStat: 'abilityPower', blessedValue: 5,
        ability: { name: 'Arcane Barrage', desc: '3 missiles at random enemies, 120 Arcane each', type: 'damage', damageType: 'arcane', baseDamage: 120, adRatio: 0, apRatio: 0, effect: 'none', projectileCount: 3, targeting: 'randomEnemy' }
    },
    'specter': {
        unitId: 'specter', name: 'Specter', cost: 2,
        traits: ['cleave', 'shadow'],
        damageAffinity: 'physical', elementalAutoattacks: false,
        stats: { health: 550, attack: 65, abilityPower: 0, armor: 25, magicResist: 20, attackSpeed: 0.7, range: 1, mana: 40, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Phantom Rend', desc: '150 Physical cone + Bleed 40 over 3s', type: 'areaDamage', damageType: 'physical', baseDamage: 150, adRatio: 0, apRatio: 0, effect: 'bleed', effectDuration: 3, effectDps: 13, radius: 1, targeting: 'adjacentEnemies' }
    },
    'battlebee': {
        unitId: 'battlebee', name: 'Battle Bee', cost: 2,
        traits: ['cavalry', 'wild'],
        damageAffinity: 'nature', elementalAutoattacks: false,
        stats: { health: 500, attack: 55, abilityPower: 0, armor: 20, magicResist: 20, attackSpeed: 0.8, range: 1, mana: 30, moveSpeed: 2.25, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Stinger Dive', desc: 'Charge distant enemy, 200% AD Nature + poison 60 over 3s', type: 'damage', damageType: 'nature', baseDamage: 0, adRatio: 2.0, apRatio: 0, effect: 'poison', effectDuration: 3, effectDps: 20, targeting: 'farthestEnemy', charges: true }
    },
    'golem': {
        unitId: 'golem', name: 'Golem', cost: 2,
        traits: ['vanguard', 'ironclad'],
        damageAffinity: 'arcane', elementalAutoattacks: false,
        stats: { health: 800, attack: 45, abilityPower: 20, armor: 40, magicResist: 25, attackSpeed: 0.5, range: 1, mana: 60, moveSpeed: 1.75, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Arcane Slam', desc: '180 Arcane to target + adj + 20% move slow 2s', type: 'areaDamage', damageType: 'arcane', baseDamage: 180, adRatio: 0, apRatio: 0, effect: 'frost', effectDuration: 2, effectSlowPercent: 0.2, radius: 1, targeting: 'adjacentEnemies' }
    },
    'salamander': {
        unitId: 'salamander', name: 'Salamander', cost: 2,
        traits: ['dragon', 'volatile'],
        damageAffinity: 'fire', elementalAutoattacks: true,
        stats: { health: 520, attack: 60, abilityPower: 25, armor: 25, magicResist: 30, attackSpeed: 0.65, range: 2, mana: 40, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Fire Breath', desc: 'Cone 200 Fire', type: 'areaDamage', damageType: 'fire', baseDamage: 200, adRatio: 0, apRatio: 0, effect: 'none', radius: 2, targeting: 'adjacentEnemies' }
    },
    'fishman': {
        unitId: 'fishman', name: 'Fishman', cost: 2,
        traits: ['scavenger', 'cleave'],
        damageAffinity: 'nature', elementalAutoattacks: false,
        stats: { health: 600, attack: 55, abilityPower: 0, armor: 25, magicResist: 20, attackSpeed: 0.7, range: 1, mana: 40, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Tidal Slash', desc: '180% AD Nature target+adj, self-heal 50% of damage', type: 'damageAndHeal', damageType: 'nature', baseDamage: 0, adRatio: 1.8, apRatio: 0, effect: 'none', radius: 1, targeting: 'adjacentEnemies', selfHealPercent: 0.5 }
    },
    'chestmonster': {
        unitId: 'chestmonster', name: 'Chest Monster', cost: 2,
        traits: ['treasure', 'scavenger'],
        damageAffinity: 'arcane', elementalAutoattacks: false,
        stats: { health: 600, attack: 50, abilityPower: 15, armor: 35, magicResist: 25, attackSpeed: 0.6, range: 1, mana: 40, moveSpeed: 1.75, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Mimic Crunch', desc: '200 Arcane, on kill gain 1g', type: 'damage', damageType: 'arcane', baseDamage: 200, adRatio: 0, apRatio: 0, effect: 'none', targeting: 'currentTarget', onKill: { gold: 1 } }
    },
    'blacksmith': {
        unitId: 'blacksmith', name: 'Blacksmith', cost: 2,
        traits: ['crestmaker', 'forged'],
        damageAffinity: 'fire', elementalAutoattacks: false,
        stats: { health: 650, attack: 45, abilityPower: 0, armor: 30, magicResist: 25, attackSpeed: 0.55, range: 1, mana: 60, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Forge Weapons', desc: 'All allies +15 AD rest of combat', type: 'teamBuff', damageType: 'fire', baseDamage: 0, adRatio: 0, apRatio: 0, effect: 'none', targeting: 'allAllies', buff: { attack: 15 } }
    },

    // ============================================
    // TIER 3 (9 units, cost 3, pool 7)
    // ============================================
    'horseman': {
        unitId: 'horseman', name: 'Horseman', cost: 3,
        traits: ['warlord', 'cavalry'],
        damageAffinity: 'physical', elementalAutoattacks: false,
        stats: { health: 750, attack: 75, abilityPower: 0, armor: 35, magicResist: 25, attackSpeed: 0.7, range: 1, mana: 40, moveSpeed: 2.5, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Lance Charge', desc: 'Charge farthest, 250% AD Physical, stun 1s', type: 'damage', damageType: 'physical', baseDamage: 0, adRatio: 2.5, apRatio: 0, effect: 'stun', effectDuration: 1, targeting: 'farthestEnemy', charges: true }
    },
    'druid': {
        unitId: 'druid', name: 'Druid', cost: 3,
        traits: ['blessed', 'nature'],
        damageAffinity: 'nature', elementalAutoattacks: false,
        stats: { health: 700, attack: 40, abilityPower: 40, armor: 25, magicResist: 40, attackSpeed: 0.55, range: 3, mana: 60, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        isBlessed: true, blessedStat: 'attackSpeed', blessedValue: 0.1,
        ability: { name: 'Rejuvenation', desc: 'All allies heal 120 HP over 4s, below 50% double', type: 'heal', damageType: 'nature', baseDamage: 0, baseHealing: 120, adRatio: 0, apRatio: 0, effect: 'none', duration: 4, targeting: 'allAllies' }
    },
    'deathknight': {
        unitId: 'deathknight', name: 'Death Knight', cost: 3,
        traits: ['ironclad', 'forged'],
        damageAffinity: 'shadow', elementalAutoattacks: false,
        stats: { health: 850, attack: 70, abilityPower: 0, armor: 45, magicResist: 30, attackSpeed: 0.65, range: 1, mana: 50, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Soul Rend', desc: '250% AD Shadow, self-heal 30%', type: 'damageAndHeal', damageType: 'shadow', baseDamage: 0, adRatio: 2.5, apRatio: 0, effect: 'none', targeting: 'currentTarget', selfHealPercent: 0.3 }
    },
    'evilmage': {
        unitId: 'evilmage', name: 'Evil Mage', cost: 3,
        traits: ['attuned', 'volatile'],
        damageAffinity: 'fire', elementalAutoattacks: false,
        stats: { health: 600, attack: 50, abilityPower: 45, armor: 20, magicResist: 35, attackSpeed: 0.6, range: 3, mana: 50, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Chaos Bolt', desc: '300 Fire to highest HP. On kill: chains 150 to nearest', type: 'damage', damageType: 'fire', baseDamage: 300, adRatio: 0, apRatio: 0, effect: 'none', targeting: 'highestHealthEnemy', chainsOnKill: true, chainDamage: 150 }
    },
    'orc': {
        unitId: 'orc', name: 'Orc', cost: 3,
        traits: ['cleave', 'wild'],
        damageAffinity: 'physical', elementalAutoattacks: false,
        stats: { health: 800, attack: 80, abilityPower: 0, armor: 30, magicResist: 20, attackSpeed: 0.75, range: 1, mana: 40, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Blood Frenzy', desc: '+50% AS 4s, attacks apply Bleed 40 over 2s', type: 'buff', damageType: 'physical', baseDamage: 0, adRatio: 0, apRatio: 0, effect: 'none', targeting: 'self', buff: { attackSpeed: 0.5, duration: 4, appliesBleed: true, bleedDps: 20, bleedDuration: 2 } }
    },
    'cyclops': {
        unitId: 'cyclops', name: 'Cyclops', cost: 3,
        traits: ['warlord', 'vanguard'],
        damageAffinity: 'physical', elementalAutoattacks: false,
        stats: { health: 950, attack: 85, abilityPower: 0, armor: 35, magicResist: 25, attackSpeed: 0.55, range: 1, mana: 50, moveSpeed: 1.75, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Boulder Toss', desc: '350 Physical, stun 1s', type: 'damage', damageType: 'physical', baseDamage: 350, adRatio: 0, apRatio: 0, effect: 'stun', effectDuration: 1, targeting: 'currentTarget' }
    },
    'werewolf': {
        unitId: 'werewolf', name: 'Werewolf', cost: 3,
        traits: ['shadow', 'wild'],
        damageAffinity: 'shadow', elementalAutoattacks: false,
        stats: { health: 700, attack: 80, abilityPower: 0, armor: 25, magicResist: 25, attackSpeed: 0.85, range: 1, mana: 40, moveSpeed: 2.25, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Savage Lunge', desc: 'Leap lowest HP, 280% AD Shadow, on kill +30% AS permanent', type: 'damage', damageType: 'shadow', baseDamage: 0, adRatio: 2.8, apRatio: 0, effect: 'none', targeting: 'lowestHealthEnemy', teleports: true, onKill: { permBuff: 'attackSpeed', value: 0.3 } }
    },
    'nagawizard': {
        unitId: 'nagawizard', name: 'Naga Wizard', cost: 3,
        traits: ['blessed', 'shadow'],
        damageAffinity: 'arcane', elementalAutoattacks: false,
        stats: { health: 650, attack: 50, abilityPower: 40, armor: 20, magicResist: 40, attackSpeed: 0.6, range: 3, mana: 60, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        isBlessed: true, blessedStat: 'critChance', blessedValue: 0.1,
        ability: { name: 'Mystic Vortex', desc: '150 Arcane DPS nearby 3s, allies +10% crit', type: 'areaDamage', damageType: 'arcane', baseDamage: 150, adRatio: 0, apRatio: 0, effect: 'none', duration: 3, radius: 2, targeting: 'nearbyEnemies', allyBuff: { critChance: 0.1 } }
    },
    'deathrider': {
        unitId: 'deathrider', name: 'Death Rider', cost: 3,
        traits: ['cavalry', 'warlord'],
        damageAffinity: 'shadow', elementalAutoattacks: false,
        stats: { health: 780, attack: 70, abilityPower: 0, armor: 30, magicResist: 30, attackSpeed: 0.7, range: 1, mana: 40, moveSpeed: 2.5, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Deathly Charge', desc: 'Charge through line, 200% AD Shadow, Frost 30% slow 2s', type: 'damage', damageType: 'shadow', baseDamage: 0, adRatio: 2.0, apRatio: 0, effect: 'frost', effectDuration: 2, effectSlowPercent: 0.3, targeting: 'farthestEnemy', charges: true, pierces: true }
    },

    // ============================================
    // TIER 4 (7 units, cost 4, pool 6)
    // ============================================
    'griffin': {
        unitId: 'griffin', name: 'Griffin', cost: 4,
        traits: ['cavalry', 'legion'],
        damageAffinity: 'arcane', elementalAutoattacks: false,
        stats: { health: 900, attack: 80, abilityPower: 30, armor: 35, magicResist: 35, attackSpeed: 0.7, range: 1, mana: 50, moveSpeed: 2.5, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Sky Dive', desc: 'Leap backline, 300 Arcane, stun 1s, untargetable during', type: 'damage', damageType: 'arcane', baseDamage: 300, adRatio: 0, apRatio: 0, effect: 'stun', effectDuration: 1, targeting: 'backlineEnemy', teleports: true, untargetableDuring: true }
    },
    'demonhunter': {
        unitId: 'demonhunter', name: 'Demon Hunter', cost: 4,
        traits: ['nature', 'attuned'],
        damageAffinity: 'shadow', elementalAutoattacks: false,
        stats: { health: 800, attack: 85, abilityPower: 20, armor: 30, magicResist: 30, attackSpeed: 0.75, range: 1, mana: 50, moveSpeed: 2.25, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Mana Burn', desc: '250% AD Shadow, burn 30 mana, full mana = +150 bonus', type: 'damage', damageType: 'shadow', baseDamage: 0, adRatio: 2.5, apRatio: 0, effect: 'none', targeting: 'currentTarget', manaBurn: 30, fullManaBonusDamage: 150 }
    },
    'champion': {
        unitId: 'champion', name: 'Champion', cost: 4,
        traits: ['warlord', 'legion'],
        damageAffinity: 'physical', elementalAutoattacks: false,
        stats: { health: 950, attack: 90, abilityPower: 0, armor: 40, magicResist: 25, attackSpeed: 0.65, range: 1, mana: 50, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Rally Strike', desc: '200% AD Physical + Bleed 60 over 3s, allies within 2 hex gain +15% AS 3s', type: 'damageAndBuff', damageType: 'physical', baseDamage: 0, adRatio: 2.0, apRatio: 0, effect: 'bleed', effectDuration: 3, effectDps: 20, targeting: 'currentTarget', allyBuff: { attackSpeed: 0.15, duration: 3 } }
    },
    'treeant': {
        unitId: 'treeant', name: 'Treeant', cost: 4,
        traits: ['blessed', 'vanguard'],
        damageAffinity: 'nature', elementalAutoattacks: false,
        stats: { health: 1200, attack: 55, abilityPower: 30, armor: 50, magicResist: 40, attackSpeed: 0.4, range: 1, mana: 60, moveSpeed: 1.75, critChance: 0, critDamage: 1.5 },
        isBlessed: true, blessedStat: 'armor', blessedValue: 10,
        ability: { name: 'Ancient Roots', desc: 'Root 2 enemies 1.5s, 150 Nature, allies +20 Armor 4s', type: 'damageAndBuff', damageType: 'nature', baseDamage: 150, adRatio: 0, apRatio: 0, effect: 'root', effectDuration: 1.5, radius: 2, targeting: 'nearbyEnemies', allyBuff: { armor: 20, duration: 4 } }
    },
    'blackknight': {
        unitId: 'blackknight', name: 'Black Knight', cost: 4,
        traits: ['forged', 'cleave'],
        damageAffinity: 'shadow', elementalAutoattacks: false,
        stats: { health: 1000, attack: 95, abilityPower: 0, armor: 45, magicResist: 30, attackSpeed: 0.65, range: 1, mana: 50, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Dark Cleave', desc: '250% AD Shadow target+adj, Bleed 80 over 3s', type: 'areaDamage', damageType: 'shadow', baseDamage: 0, adRatio: 2.5, apRatio: 0, effect: 'bleed', effectDuration: 3, effectDps: 27, radius: 1, targeting: 'adjacentEnemies' }
    },
    'flyingdemon': {
        unitId: 'flyingdemon', name: 'Flying Demon', cost: 4,
        traits: ['dragon', 'shadow'],
        damageAffinity: 'shadow', elementalAutoattacks: true,
        stats: { health: 850, attack: 90, abilityPower: 30, armor: 25, magicResist: 30, attackSpeed: 0.75, range: 1, mana: 50, moveSpeed: 2.25, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Shadow Dive', desc: 'Teleport lowest HP, 350 Shadow, on kill mana = 50', type: 'damage', damageType: 'shadow', baseDamage: 350, adRatio: 0, apRatio: 0, effect: 'none', targeting: 'lowestHealthEnemy', teleports: true, onKill: { mana: 50 } }
    },
    'bishopknight': {
        unitId: 'bishopknight', name: 'Bishop Knight', cost: 4,
        traits: ['blessed', 'legion'],
        damageAffinity: 'arcane', elementalAutoattacks: false,
        stats: { health: 1050, attack: 60, abilityPower: 35, armor: 45, magicResist: 50, attackSpeed: 0.5, range: 1, mana: 60, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        isBlessed: true, blessedStat: 'magicResist', blessedValue: 10,
        ability: { name: 'Holy Judgment', desc: '300 Arcane, allies within 2 hex heal 150', type: 'damageAndHeal', damageType: 'arcane', baseDamage: 300, baseHealing: 150, adRatio: 0, apRatio: 0, effect: 'none', radius: 2, targeting: 'currentTarget' }
    },

    // ============================================
    // TIER 5 (5 units, cost 5, pool 6)
    // ============================================
    'shadowknight': {
        unitId: 'shadowknight', name: 'Shadow Knight', cost: 5,
        traits: ['shadow', 'ironclad'],
        damageAffinity: 'shadow', elementalAutoattacks: false,
        stats: { health: 1100, attack: 100, abilityPower: 0, armor: 50, magicResist: 40, attackSpeed: 0.6, range: 1, mana: 60, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Dark Verdict', desc: '300% AD Shadow + 200 HP shield 4s. On kill: +25% AS permanent', type: 'damageAndBuff', damageType: 'shadow', baseDamage: 0, adRatio: 3.0, apRatio: 0, effect: 'none', targeting: 'currentTarget', onKill: { permBuff: 'attackSpeed', value: 0.25 }, selfShield: 200, shieldDuration: 4 }
    },
    'lich': {
        unitId: 'lich', name: 'Lich', cost: 5,
        traits: ['dragon', 'attuned'],
        damageAffinity: 'shadow', elementalAutoattacks: true,
        stats: { health: 800, attack: 70, abilityPower: 60, armor: 25, magicResist: 50, attackSpeed: 0.55, range: 4, mana: 60, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Death Coil', desc: '400 Shadow, on kill 200 HP shield, Frost 40% AS slow 3s', type: 'damage', damageType: 'shadow', baseDamage: 400, adRatio: 0, apRatio: 0, effect: 'frost', effectDuration: 3, effectSlowPercent: 0.4, targeting: 'highestHealthEnemy', onKill: { shield: 200 } }
    },
    'demonking': {
        unitId: 'demonking', name: 'Demon King', cost: 5,
        traits: ['dragon', 'volatile'],
        damageAffinity: 'fire', elementalAutoattacks: true,
        stats: { health: 1050, attack: 95, abilityPower: 50, armor: 35, magicResist: 40, attackSpeed: 0.6, range: 1, mana: 60, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Infernal Storm', desc: 'All enemies 200 Fire + Burn 100 over 3s + 20% armor shred 4s', type: 'areaDamage', damageType: 'fire', baseDamage: 200, adRatio: 0, apRatio: 0, effect: 'burn', effectDuration: 3, effectDps: 33, targeting: 'allEnemies', armorShred: 0.2, armorShredDuration: 4 }
    },
    'titandrake': {
        unitId: 'titandrake', name: 'Titan Drake', cost: 5,
        traits: ['wild', 'vanguard'],
        damageAffinity: 'nature', elementalAutoattacks: false,
        stats: { health: 1400, attack: 80, abilityPower: 35, armor: 50, magicResist: 40, attackSpeed: 0.45, range: 1, mana: 60, moveSpeed: 1.75, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Earthquake', desc: '250 Nature all enemies, Frost 30% slow 2s, allies +15% dmg 4s', type: 'areaDamage', damageType: 'nature', baseDamage: 250, adRatio: 0, apRatio: 0, effect: 'frost', effectDuration: 2, effectSlowPercent: 0.3, targeting: 'allEnemies', allyBuff: { damagePercent: 0.15, duration: 4 } }
    },
    'flameknight': {
        unitId: 'flameknight', name: 'Flame Knight', cost: 5,
        traits: ['forged', 'cleave'],
        damageAffinity: 'fire', elementalAutoattacks: true,
        stats: { health: 950, attack: 105, abilityPower: 30, armor: 35, magicResist: 35, attackSpeed: 0.7, range: 1, mana: 60, moveSpeed: 2, critChance: 0, critDamage: 1.5 },
        ability: { name: 'Blazing Cleave', desc: 'Next 3 attacks hit adj for 150% as Fire, Bleed 50 over 2s', type: 'buff', damageType: 'fire', baseDamage: 0, adRatio: 1.5, apRatio: 0, effect: 'bleed', effectDuration: 2, effectDps: 25, targeting: 'self', buff: { enhancedAttacks: 3, hitAdjacentForPercent: 1.5 } }
    },

    // ============================================
    // PVE-ONLY UNITS (not available in shop)
    // ============================================
    'stingray': {
        unitId: 'stingray', name: 'Stingray', cost: 0,
        traits: [],
        damageAffinity: 'physical', elementalAutoattacks: false,
        stats: { health: 80, attack: 15, abilityPower: 0, armor: 0, magicResist: 0, attackSpeed: 0.6, range: 1, mana: 0, moveSpeed: 1.75, critChance: 0, critDamage: 1.5 },
        isPvE: true
    },
    'cactus': {
        unitId: 'cactus', name: 'Cactus', cost: 0,
        traits: [],
        damageAffinity: 'nature', elementalAutoattacks: false,
        stats: { health: 100, attack: 10, abilityPower: 0, armor: 5, magicResist: 0, attackSpeed: 0.5, range: 1, mana: 0, moveSpeed: 1.75, critChance: 0, critDamage: 1.5 },
        isPvE: true
    }
};

// ============================================
// TRAIT DEFINITIONS - 15 shared + 2 unique
// ============================================
const TraitDefinitions = {
    // === SHARED TRAITS (2/4 breakpoints) ===
    'vanguard': {
        traitId: 'vanguard', name: 'Vanguard',
        description: 'Vanguard units gain bonus health and armor',
        units: ['footman', 'golem', 'cyclops', 'treeant', 'titandrake'],
        tiers: [
            { count: 2, bonus: { health: 150 }, desc: '+150 HP' },
            { count: 4, bonus: { health: 400, armor: 15 }, desc: '+400 HP, +15 Armor' }
        ]
    },
    'legion': {
        traitId: 'legion', name: 'Legion',
        description: 'Adjacent Legion units gain bonus attack damage',
        units: ['footman', 'archer', 'knight', 'griffin', 'champion', 'bishopknight'],
        tiers: [
            { count: 2, bonus: { adjAttackPercent: 15 }, desc: '+15% AD to adjacent Legion' },
            { count: 4, bonus: { adjAttackPercent: 30, adjArmor: 5 }, desc: '+30% AD, +5 Armor to adjacent Legion' }
        ]
    },
    'wild': {
        traitId: 'wild', name: 'Wild',
        description: 'Wild units gain attack speed with each attack (ramping)',
        units: ['bat', 'crawler', 'battlebee', 'orc', 'werewolf', 'titandrake'],
        tiers: [
            { count: 2, bonus: { asPerAttack: 3, asMaxPercent: 30 }, desc: '+3% AS per attack, max +30%' },
            { count: 4, bonus: { asPerAttack: 5, asMaxPercent: 50 }, desc: '+5% AS per attack, max +50%' }
        ]
    },
    'shadow': {
        traitId: 'shadow', name: 'Shadow',
        description: 'Shadow units gain crit chance and stealth on combat start',
        units: ['ratassassin', 'bat', 'specter', 'werewolf', 'nagawizard', 'flyingdemon', 'shadowknight'],
        tiers: [
            { count: 2, bonus: { critChance: 15, firstAttackBonus: 50, untargetable: 1 }, desc: '+15% crit, first attack +50% dmg, untargetable 1s' },
            { count: 4, bonus: { critChance: 25, firstAttackBonus: 100, untargetable: 1.5 }, desc: '+25% crit, first attack +100% dmg, untargetable 1.5s' }
        ]
    },
    'ironclad': {
        traitId: 'ironclad', name: 'Ironclad',
        description: 'Ironclad units take reduced damage from all sources',
        units: ['crawler', 'cleric', 'knight', 'golem', 'deathknight', 'shadowknight'],
        tiers: [
            { count: 2, bonus: { damageReduction: 10 }, desc: '10% damage reduction' },
            { count: 4, bonus: { damageReduction: 20 }, desc: '20% damage reduction' }
        ]
    },
    'cleave': {
        traitId: 'cleave', name: 'Cleave',
        description: 'Cleave units\' attacks splash to adjacent enemies',
        units: ['specter', 'fishman', 'orc', 'blackknight', 'flameknight'],
        tiers: [
            { count: 2, bonus: { cleavePercent: 25 }, desc: '25% splash to adjacent' },
            { count: 4, bonus: { cleavePercent: 50 }, desc: '50% splash to adjacent' }
        ]
    },
    'cavalry': {
        traitId: 'cavalry', name: 'Cavalry',
        description: 'Cavalry units charge into combat, stunning their first target',
        units: ['battlebee', 'horseman', 'deathrider', 'griffin'],
        tiers: [
            { count: 2, bonus: { moveBonus: 1, chargeStun: 0.5 }, desc: '+1 move, charge stun 0.5s' },
            { count: 4, bonus: { moveBonus: 2, chargeStun: 1, chargeDamageBonus: 30 }, desc: '+2 move, charge stun 1s, +30% charge dmg' }
        ]
    },
    'dragon': {
        traitId: 'dragon', name: 'Dragon',
        description: 'Dragon units deal bonus damage and resist fire',
        units: ['salamander', 'flyingdemon', 'lich', 'demonking'],
        tiers: [
            { count: 2, bonus: { damagePercent: 15, fireResist: 30 }, desc: '+15% dmg, 30% Fire resist' },
            { count: 4, bonus: { damagePercent: 30, fireResist: 50, burnOnHit: 40, burnDuration: 3 }, desc: '+30% dmg, 50% Fire resist, attacks Burn 40/3s' }
        ]
    },
    'volatile': {
        traitId: 'volatile', name: 'Volatile',
        description: 'Volatile units explode on death dealing fire damage',
        units: ['redslime', 'mushroom', 'littledemon', 'salamander', 'evilmage', 'demonking'],
        tiers: [
            { count: 2, bonus: { explosionDamage: 100 }, desc: 'On death: 100 Fire to adjacent' },
            { count: 4, bonus: { explosionDamage: 250 }, desc: 'On death: 250 Fire to adjacent' }
        ]
    },
    'nature': {
        traitId: 'nature', name: 'Nature',
        description: 'Nature units heal adjacent allies over time',
        units: ['elfranger', 'mushroom', 'dryad', 'druid', 'demonhunter'],
        tiers: [
            { count: 2, bonus: { adjHealPerSec: 3 }, desc: 'Adjacent allies heal 3 HP/s' },
            { count: 4, bonus: { adjHealPerSec: 6, healEffectiveness: 20 }, desc: 'Adjacent allies heal 6 HP/s, +20% heal effectiveness' }
        ]
    },
    'attuned': {
        traitId: 'attuned', name: 'Attuned',
        description: 'A random element is chosen each game. Attuned units convert and boost that damage type for ALL allies.',
        units: ['archer', 'elfranger', 'littledemon', 'mage', 'evilmage', 'demonhunter', 'lich'],
        tiers: [
            { count: 2, bonus: { globalElementPercent: 15 }, desc: '+15% global Attuned element damage, Attuned units convert' },
            { count: 4, bonus: { globalElementPercent: 30 }, desc: '+30% global Attuned element damage, Attuned units convert' }
        ]
    },
    'blessed': {
        traitId: 'blessed', name: 'Blessed',
        description: 'Each Blessed unit provides a unique stat buff to your team (always active). At 2/4: per-game bonus.',
        units: ['cleric', 'dryad', 'mage', 'druid', 'nagawizard', 'treeant', 'bishopknight'],
        isAlwaysActive: true,
        tiers: [
            { count: 2, desc: 'Per-game Blessed bonus activates' },
            { count: 4, desc: 'Enhanced per-game Blessed bonus' }
        ]
    },
    'warlord': {
        traitId: 'warlord', name: 'Warlord',
        description: 'A random Physical enhancement is chosen each game for Warlord units.',
        units: ['duelist', 'horseman', 'cyclops', 'deathrider', 'champion'],
        tiers: [
            { count: 2, desc: 'Per-game Warlord enhancement activates' },
            { count: 4, desc: 'Enhanced Warlord effect + Warlord attacks apply Bleed 30/3s' }
        ]
    },
    'forged': {
        traitId: 'forged', name: 'Forged',
        description: 'Forged units gain permanent stats after each combat round',
        units: ['duelist', 'skeletonwarrior', 'blacksmith', 'deathknight', 'blackknight', 'flameknight'],
        tiers: [
            { count: 2, bonus: { stackAD: 3, stackAP: 3, stackHP: 15 }, desc: '+3 AD/AP, +15 HP per round (Forged units)' },
            { count: 4, bonus: { stackAD: 2, stackAP: 2, stackHP: 10, forgedDouble: true }, desc: 'ALL units +2/2/10 per round, Forged units double' }
        ]
    },
    'scavenger': {
        traitId: 'scavenger', name: 'Scavenger',
        description: 'After each round, receive a guaranteed unit',
        units: ['skeletonwarrior', 'ratassassin', 'redslime', 'fishman', 'chestmonster'],
        tiers: [
            { count: 2, bonus: { unitCostRange: [1, 2] }, desc: 'Gain a random 1-2 cost unit' },
            { count: 4, bonus: { unitCostRange: [3, 4], bonusGold: 1 }, desc: 'Gain a random 3-4 cost unit + 1g' }
        ]
    },

    // === UNIQUE TRAITS ===
    'treasure': {
        traitId: 'treasure', name: 'Treasure',
        description: 'On victory: receive random loot',
        isUnique: true,
        units: ['chestmonster'],
        tiers: [
            { count: 1, desc: 'Win: random reward (gold, reroll token, unit, or crest token)' }
        ],
        lootPool: [
            { type: 'gold', value: 2, weight: 30 },
            { type: 'rerollToken', value: 1, weight: 25 },
            { type: 'unit12', value: 1, weight: 20 },   // Random 1-2 cost unit
            { type: 'unit34', value: 1, weight: 15 },   // Random 3-4 cost unit
            { type: 'unit5', value: 1, weight: 5 },     // Random 5 cost unit
            { type: 'crestToken', value: 1, weight: 5 }  // Minor crest token
        ]
    },
    'crestmaker': {
        traitId: 'crestmaker', name: 'Crestmaker',
        description: 'Crafts a minor crest token every 2 rounds',
        isUnique: true,
        units: ['blacksmith'],
        tiers: [
            { count: 1, desc: 'Minor crest token every 2 rounds' }
        ]
    }
};

// ============================================
// ITEM DEFINITIONS
// ============================================
const ItemTemplates = {
    'sword': {
        itemId: 'sword', name: 'Long Sword',
        description: '+15 Attack Damage',
        isComponent: true, stats: { attack: 15 }
    },
    'bow': {
        itemId: 'bow', name: 'Recurve Bow',
        description: '+15% Attack Speed',
        isComponent: true, stats: { attackSpeedPercent: 15 }
    },
    'rod': {
        itemId: 'rod', name: 'Needlessly Large Rod',
        description: '+15 Ability Power',
        isComponent: true, stats: { abilityPower: 15 }
    },
    'vest': {
        itemId: 'vest', name: 'Chain Vest',
        description: '+25 Armor',
        isComponent: true, stats: { armor: 25 }
    },
    'cloak': {
        itemId: 'cloak', name: 'Negatron Cloak',
        description: '+25 Magic Resist',
        isComponent: true, stats: { magicResist: 25 }
    },
    'belt': {
        itemId: 'belt', name: "Giant's Belt",
        description: '+200 Health',
        isComponent: true, stats: { health: 200 }
    },
    'tear': {
        itemId: 'tear', name: 'Tear of the Goddess',
        description: '+15 Starting Mana',
        isComponent: true, stats: { mana: 15 }
    },
    'glove': {
        itemId: 'glove', name: 'Sparring Gloves',
        description: '+10% Crit Chance',
        isComponent: true, stats: { critChance: 10 }
    },
    'bloodthirster': {
        itemId: 'bloodthirster', name: 'Bloodthirster',
        description: '+30 Attack, Lifesteal 25%',
        isComponent: false, recipe: ['sword', 'sword'],
        stats: { attack: 30 }, effects: { lifesteal: 25 }
    },
    'rapidfire': {
        itemId: 'rapidfire', name: 'Rapid Firecannon',
        description: '+30% Attack Speed, +1 Range',
        isComponent: false, recipe: ['bow', 'bow'],
        stats: { attackSpeedPercent: 30, range: 1 }
    },
    'deathcap': {
        itemId: 'deathcap', name: "Rabadon's Deathcap",
        description: '+50 Ability Power',
        isComponent: false, recipe: ['rod', 'rod'],
        stats: { abilityPower: 50 }
    },
    'thornmail': {
        itemId: 'thornmail', name: 'Thornmail',
        description: '+50 Armor, Reflect 25% damage',
        isComponent: false, recipe: ['vest', 'vest'],
        stats: { armor: 50 }, effects: { reflect: 25 }
    },
    'warmog': {
        itemId: 'warmog', name: "Warmog's Armor",
        description: '+500 Health, Regen 3% HP/sec',
        isComponent: false, recipe: ['belt', 'belt'],
        stats: { health: 500 }, effects: { hpRegen: 3 }
    },
    'infinity': {
        itemId: 'infinity', name: 'Infinity Edge',
        description: '+25 Attack, +30% Crit Damage',
        isComponent: false, recipe: ['sword', 'glove'],
        stats: { attack: 25, critDamagePercent: 30 }
    },
    'guardian_angel': {
        itemId: 'guardian_angel', name: 'Guardian Angel',
        description: '+20 Armor, +20 MR, Revive once',
        isComponent: false, recipe: ['vest', 'cloak'],
        stats: { armor: 20, magicResist: 20 }, effects: { revive: true }
    }
};

// ============================================
// CREST DEFINITIONS
// ============================================
const CrestTemplates = {
    'minor_might': { crestId: 'minor_might', name: 'Crest of Might', description: 'All units gain +10 attack damage', type: 'minor', teamBonus: { attack: 10 } },
    'minor_vitality': { crestId: 'minor_vitality', name: 'Crest of Vitality', description: 'All units gain +100 health', type: 'minor', teamBonus: { health: 100 } },
    'minor_swiftness': { crestId: 'minor_swiftness', name: 'Crest of Swiftness', description: 'All units gain +10% attack speed', type: 'minor', teamBonus: { attackSpeedPercent: 10 } },
    'minor_protection': { crestId: 'minor_protection', name: 'Crest of Protection', description: 'All units gain +15 armor', type: 'minor', teamBonus: { armor: 15 } },
    'minor_warding': { crestId: 'minor_warding', name: 'Crest of Warding', description: 'All units gain +15 magic resist', type: 'minor', teamBonus: { magicResist: 15 } },
    'minor_precision': { crestId: 'minor_precision', name: 'Crest of Precision', description: 'All units gain +10% crit chance', type: 'minor', teamBonus: { critChance: 10 } },
    'minor_sorcery': { crestId: 'minor_sorcery', name: 'Crest of Sorcery', description: 'All units gain +10 ability power', type: 'minor', teamBonus: { abilityPower: 10 } },
    'major_bloodlust': { crestId: 'major_bloodlust', name: 'Bloodlust Crest', description: 'All units gain 10% lifesteal', type: 'major', teamBonus: { lifesteal: 10 } },
    'major_fortitude': { crestId: 'major_fortitude', name: 'Fortitude Crest', description: 'All units gain +150 health', type: 'major', teamBonus: { health: 150 } },
    'major_haste': { crestId: 'major_haste', name: 'Haste Crest', description: 'All units gain +15% attack speed', type: 'major', teamBonus: { attackSpeedPercent: 15 } },
    'major_arcane': { crestId: 'major_arcane', name: 'Arcane Crest', description: 'All units gain +20 ability power', type: 'major', teamBonus: { abilityPower: 20 } },
    'major_iron': { crestId: 'major_iron', name: 'Iron Crest', description: 'All units gain +20 armor and MR', type: 'major', teamBonus: { armor: 20, magicResist: 20 } }
};

// ============================================
// UTILITY FUNCTIONS
// ============================================

function getAllUnits() {
    return Object.values(UnitTemplates).filter(u => u.cost > 0);
}

function getUnitsByCost(cost) {
    return getAllUnits().filter(u => u.cost === cost);
}

function getStarScaledStats(baseStats, starLevel) {
    const multipliers = { 1: 1.0, 2: 1.5, 3: 2.0 };
    const mult = multipliers[starLevel] || 1.0;
    return {
        health: Math.round(baseStats.health * mult),
        attack: Math.round(baseStats.attack * mult),
        abilityPower: Math.round((baseStats.abilityPower || 0) * mult),
        armor: baseStats.armor,
        magicResist: baseStats.magicResist,
        attackSpeed: baseStats.attackSpeed,
        range: baseStats.range,
        mana: baseStats.mana,
        moveSpeed: baseStats.moveSpeed,
        critChance: baseStats.critChance || 0,
        critDamage: baseStats.critDamage || 1.5
    };
}

function calculateActiveTraits(units) {
    const traitCounts = {};

    for (const unit of units) {
        if (!unit) continue;
        if (unit.traits) {
            for (const traitId of unit.traits) {
                traitCounts[traitId] = (traitCounts[traitId] || 0) + 1;
            }
        }
    }

    const activeTraits = {};
    for (const [traitId, count] of Object.entries(traitCounts)) {
        const traitDef = TraitDefinitions[traitId];
        if (!traitDef) continue;

        let activeTier = null;
        for (const tier of traitDef.tiers) {
            if (count >= tier.count) {
                activeTier = tier;
            }
        }

        activeTraits[traitId] = {
            count,
            tier: activeTier,
            definition: traitDef
        };
    }

    return activeTraits;
}

function applyTraitBonuses(stats, activeTraits, unitTraits) {
    const modifiedStats = { ...stats };

    for (const [traitId, traitInfo] of Object.entries(activeTraits)) {
        if (!traitInfo.tier) continue;

        const bonus = traitInfo.tier.bonus;
        if (!bonus) continue;

        const unitHasTrait = unitTraits.includes(traitId);

        // Vanguard: health + armor
        if (bonus.health && unitHasTrait) modifiedStats.health += bonus.health;
        if (bonus.armor && unitHasTrait) modifiedStats.armor += bonus.armor;

        // Ironclad: damage reduction
        if (bonus.damageReduction && unitHasTrait) {
            modifiedStats.damageReduction = (modifiedStats.damageReduction || 0) + bonus.damageReduction;
        }

        // Shadow: crit chance
        if (bonus.critChance && unitHasTrait) {
            modifiedStats.critChance = (modifiedStats.critChance || 0) + bonus.critChance;
        }

        // Dragon: damage percent + fire resist
        if (bonus.damagePercent && unitHasTrait) {
            modifiedStats.bonusDamagePercent = (modifiedStats.bonusDamagePercent || 0) + bonus.damagePercent;
        }
        if (bonus.fireResist && unitHasTrait) {
            modifiedStats.fireResist = (modifiedStats.fireResist || 0) + bonus.fireResist;
        }
    }

    return modifiedStats;
}

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
        if (item.stats.abilityPower) modifiedStats.abilityPower = (modifiedStats.abilityPower || 0) + item.stats.abilityPower;
        if (item.stats.attackSpeedPercent) {
            modifiedStats.attackSpeed *= (1 + item.stats.attackSpeedPercent / 100);
        }
        if (item.stats.critChance) {
            modifiedStats.critChance = (modifiedStats.critChance || 0) + item.stats.critChance;
        }
    }

    return modifiedStats;
}

function applyCrestBonuses(stats, crests) {
    const modifiedStats = { ...stats };

    for (const crest of crests) {
        if (!crest || !crest.teamBonus) continue;

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

// Apply Blessed individual buffs (always active when unit is on board)
function applyBlessedBonuses(stats, blessedUnitsOnBoard) {
    const modifiedStats = { ...stats };

    for (const unit of blessedUnitsOnBoard) {
        if (!unit || !unit.isBlessed) continue;
        const template = UnitTemplates[unit.unitId] || unit;
        if (!template.isBlessed) continue;

        switch (template.blessedStat) {
            case 'attackDamage': modifiedStats.attack += template.blessedValue; break;
            case 'health': modifiedStats.health += template.blessedValue; break;
            case 'abilityPower': modifiedStats.abilityPower = (modifiedStats.abilityPower || 0) + template.blessedValue; break;
            case 'attackSpeed': modifiedStats.attackSpeed *= (1 + template.blessedValue); break;
            case 'critChance': modifiedStats.critChance = (modifiedStats.critChance || 0) + template.blessedValue * 100; break;
            case 'armor': modifiedStats.armor += template.blessedValue; break;
            case 'magicResist': modifiedStats.magicResist += template.blessedValue; break;
        }
    }

    return modifiedStats;
}

// Roll per-game variables at game start
function rollPerGameVariables() {
    return {
        attunedElement: ATTUNED_ELEMENTS[Math.floor(Math.random() * ATTUNED_ELEMENTS.length)],
        blessedBonus: PerGameVariables.BLESSED_BONUSES[Math.floor(Math.random() * PerGameVariables.BLESSED_BONUSES.length)],
        warlordEnhancement: PerGameVariables.WARLORD_ENHANCEMENTS[Math.floor(Math.random() * PerGameVariables.WARLORD_ENHANCEMENTS.length)]
    };
}

module.exports = {
    GameConstants,
    DamageTypes,
    StatusEffects,
    ATTUNED_ELEMENTS,
    PerGameVariables,
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
    applyCrestBonuses,
    applyBlessedBonuses,
    rollPerGameVariables
};
