/**
 * GameRoom - Server-authoritative game state management
 */

const { v4: uuidv4 } = require('uuid');
const {
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
} = require('./gameData');

// Loot types for PvE enemies
const LootType = {
    None: 'none',
    CrestToken: 'crest_token',
    ItemAnvil: 'item_anvil',
    MixedLoot: 'mixed_loot', // Random loot - gold or unit
    LargeMixedLoot: 'large_mixed_loot' // Boss loot - more gold or better unit
};

class UnitPool {
    constructor() {
        this.pool = new Map(); // unitId -> count
        this.initialize();
    }

    initialize() {
        this.pool.clear();
        for (const unit of getAllUnits()) {
            this.pool.set(unit.unitId, GameConstants.Units.getPoolSize(unit.cost));
        }
    }

    getAvailable(unitId) {
        return this.pool.get(unitId) || 0;
    }

    takeUnit(unitId) {
        const count = this.pool.get(unitId) || 0;
        if (count > 0) {
            this.pool.set(unitId, count - 1);
            return true;
        }
        return false;
    }

    returnUnit(unitId, count = 1) {
        const current = this.pool.get(unitId) || 0;
        const template = UnitTemplates[unitId];
        const maxPool = template ? GameConstants.Units.getPoolSize(template.cost) : 8;
        this.pool.set(unitId, Math.min(maxPool, current + count));
    }

    rollUnit(playerLevel) {
        // Get odds for this level
        const odds = GameConstants.ShopOdds[playerLevel] || GameConstants.ShopOdds[1];

        // Roll for cost tier
        const roll = Math.random() * 100;
        let cumulative = 0;
        let costTier = 1;

        for (let i = 0; i < odds.length; i++) {
            cumulative += odds[i];
            if (roll < cumulative) {
                costTier = i + 1;
                break;
            }
        }

        // Get available units of this cost
        const unitsOfCost = getUnitsByCost(costTier).filter(u => this.getAvailable(u.unitId) > 0);

        if (unitsOfCost.length === 0) {
            // Try other tiers if this one is empty
            for (let c = 1; c <= 5; c++) {
                const fallback = getUnitsByCost(c).filter(u => this.getAvailable(u.unitId) > 0);
                if (fallback.length > 0) {
                    const selected = fallback[Math.floor(Math.random() * fallback.length)];
                    return selected;
                }
            }
            return null;
        }

        const selected = unitsOfCost[Math.floor(Math.random() * unitsOfCost.length)];
        return selected;
    }
}

class PlayerState {
    constructor(clientId, name, boardIndex = 0) {
        this.clientId = clientId;
        this.name = name;
        this.boardIndex = boardIndex; // Assigned board position (0-3)

        // Economy
        this.gold = GameConstants.Economy.STARTING_GOLD;
        this.health = GameConstants.Player.STARTING_HEALTH;
        this.maxHealth = GameConstants.Player.STARTING_HEALTH;
        this.level = GameConstants.Player.STARTING_LEVEL;
        this.xp = 0;

        // Streaks
        this.winStreak = 0;
        this.lossStreak = 0;

        // Board: 2D array [x][y], 7 wide x 4 tall
        this.board = [];
        for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
            this.board[x] = new Array(GameConstants.Grid.HEIGHT).fill(null);
        }

        // Bench: array of 9 slots
        this.bench = new Array(GameConstants.Player.BENCH_SIZE).fill(null);

        // Shop: array of 5 unit options
        this.shop = new Array(GameConstants.Economy.SHOP_SIZE).fill(null);
        this.shopLocked = false;

        // Free rerolls (earned from rewards, persists between phases)
        this.freeRerolls = 0;

        // Items: inventory of unequipped items (max 10)
        this.itemInventory = [];

        // Crests: active crests for the player
        this.crests = []; // { minor: CrestData, major: CrestData }
        this.minorCrests = []; // Up to 3 minor crests
        this.majorCrest = null;

        // Active traits (calculated from board units)
        this.activeTraits = {};

        // Pending selections (for consumables)
        this.pendingCrestSelection = []; // Array of crest options to choose from
        this.pendingItemSelection = []; // Array of item options to choose from
        this.pendingCrestReplacement = null; // { newCrest: CrestData } - when player needs to choose which crest to replace

        // Ready state
        this.isReady = false;

        // Eliminated
        this.isEliminated = false;

        // Pending loot from PvE combat (orbs to collect)
        this.pendingLoot = []; // { lootId, lootType, position }
    }

    // Calculate active traits from board units and minor crest
    calculateTraits() {
        const boardUnits = this.getBoardUnits();
        this.activeTraits = calculateActiveTraits(boardUnits);
        return this.activeTraits;
    }

    // Get all units on the board
    getBoardUnits() {
        const units = [];
        for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
            for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                if (this.board[x][y]) {
                    units.push(this.board[x][y]);
                }
            }
        }
        return units;
    }

    // Get combat-ready stats for a unit (with all bonuses applied)
    getUnitCombatStats(unit) {
        if (!unit) return null;

        // Start with star-scaled base stats
        let stats = { ...unit.currentStats };

        // Apply trait bonuses
        stats = applyTraitBonuses(stats, this.activeTraits, unit.traits);

        // Apply item bonuses
        if (unit.items && unit.items.length > 0) {
            stats = applyItemBonuses(stats, unit.items);
        }

        // Apply crest bonuses (both are team-wide now)
        const crests = [];
        if (this.majorCrest) crests.push(this.majorCrest);
        if (this.minorCrests && this.minorCrests.length > 0) {
            crests.push(...this.minorCrests);
        }
        stats = applyCrestBonuses(stats, crests);

        return stats;
    }

    getMaxUnits() {
        return GameConstants.Leveling.UNITS_PER_LEVEL[this.level] || 1;
    }

    getBoardUnitCount() {
        let count = 0;
        for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
            for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                if (this.board[x][y]) count++;
            }
        }
        return count;
    }

    getBenchUnitCount() {
        return this.bench.filter(u => u !== null).length;
    }

    findFirstEmptyBenchSlot() {
        for (let i = 0; i < this.bench.length; i++) {
            if (this.bench[i] === null) return i;
        }
        return -1;
    }

    canLevelUp() {
        if (this.level >= GameConstants.Player.MAX_LEVEL) return false;
        const required = GameConstants.Leveling.XP_REQUIRED[this.level + 1];
        return this.xp >= required;
    }

    calculateIncome() {
        let income = GameConstants.Economy.BASE_GOLD_PER_TURN;
        // Interest: 1 gold per 5 gold saved, max 3 (at 15 gold)
        const interest = Math.min(Math.floor(this.gold / 5), GameConstants.Economy.MAX_INTEREST);
        income += interest;
        // Win/loss streak bonus: +1 at 2, +2 at 4+
        const streak = Math.max(this.winStreak, this.lossStreak);
        if (streak >= 4) {
            income += GameConstants.Economy.STREAK_BONUS_AT_4;
        } else if (streak >= 2) {
            income += GameConstants.Economy.STREAK_BONUS_AT_2;
        }
        return income;
    }

    toJSON() {
        // Serialize board as flat list with coordinates (Unity JsonUtility doesn't handle 2D arrays)
        const boardUnits = [];
        for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
            for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                const unit = this.board[x][y];
                if (unit) {
                    boardUnits.push({
                        x: x,
                        y: y,
                        unit: unit.toJSON()
                    });
                }
            }
        }

        if (boardUnits.length > 0) {
            console.log(`[PlayerState.toJSON] ${this.name} has ${boardUnits.length} board units: ${boardUnits.map(b => `${b.unit.name}@(${b.x},${b.y})`).join(', ')}`);
        }

        // Serialize bench (1D array of units)
        const serializedBench = this.bench.map(unit => unit ? unit.toJSON() : null);

        // Serialize shop (1D array of shop units - just unit info, not full instances)
        const serializedShop = this.shop.map(unit => {
            if (!unit) return null;
            return {
                unitId: unit.unitId,
                name: unit.name,
                cost: unit.cost,
                traits: unit.traits
            };
        });

        // Calculate active traits before serializing
        this.calculateTraits();

        return {
            clientId: this.clientId,
            name: this.name,
            boardIndex: this.boardIndex,
            gold: this.gold,
            health: this.health,
            maxHealth: this.maxHealth,
            level: this.level,
            xp: this.xp,
            xpToNext: GameConstants.Leveling.XP_REQUIRED[this.level + 1] || 0,
            maxUnits: this.getMaxUnits(),
            winStreak: this.winStreak,
            lossStreak: this.lossStreak,
            boardUnits: boardUnits,
            bench: serializedBench,
            shop: serializedShop,
            shopLocked: this.shopLocked,
            freeRerolls: this.freeRerolls,
            isReady: this.isReady,
            isEliminated: this.isEliminated,
            // New fields
            itemInventory: this.itemInventory,
            minorCrests: this.minorCrests,
            majorCrest: this.majorCrest,
            activeTraits: this.serializeActiveTraits(),
            // Pending selections for consumables
            pendingCrestSelection: this.pendingCrestSelection,
            pendingItemSelection: this.pendingItemSelection,
            pendingCrestReplacement: this.pendingCrestReplacement
        };
    }

    serializeActiveTraits() {
        // Return as array for Unity JsonUtility compatibility
        const result = [];
        for (const [traitId, info] of Object.entries(this.activeTraits)) {
            result.push({
                traitId: traitId,
                count: info.count,
                tierCount: info.tier ? info.tier.count : 0,
                bonus: info.tier ? info.tier.bonus : null
            });
        }
        return result;
    }
}

class UnitInstance {
    constructor(template, starLevel = 1) {
        this.instanceId = uuidv4();
        this.unitId = template.unitId;
        this.name = template.name;
        this.cost = template.cost;
        this.traits = [...template.traits];
        this.starLevel = starLevel;
        this.baseStats = { ...template.stats };
        this.currentStats = getStarScaledStats(template.stats, starLevel);
        this.currentHealth = this.currentStats.health;
        this.currentMana = 0;

        // Items equipped on this unit (max 3)
        this.items = [];
    }

    toJSON() {
        return {
            instanceId: this.instanceId,
            unitId: this.unitId,
            name: this.name,
            cost: this.cost,
            traits: this.traits,
            starLevel: this.starLevel,
            currentStats: this.currentStats,
            currentHealth: this.currentHealth,
            currentMana: this.currentMana,
            items: this.items
        };
    }
}

// Combat event types
const CombatEventType = {
    COMBAT_START: 'combatStart',
    UNIT_MOVE: 'unitMove',
    UNIT_ATTACK: 'unitAttack',
    UNIT_DAMAGE: 'unitDamage',
    UNIT_DEATH: 'unitDeath',
    UNIT_ABILITY: 'unitAbility',
    UNIT_HEAL: 'unitHeal',
    UNIT_STATUS_EFFECT: 'unitStatusEffect',
    UNIT_BUFF: 'unitBuff',
    UNIT_SHIELD: 'unitShield',
    COMBAT_END: 'combatEnd'
};

// Default movement speed (tiles/sec) if unit doesn't have moveSpeed defined
const DEFAULT_MOVE_SPEED = 1.75;
// Point in attack animation when damage lands (0.0 = start, 1.0 = end)
const DEFAULT_HIT_POINT = 0.4;
// Number of ticks a unit can be stuck before re-evaluating target (10 ticks = 0.5s)
const STUCK_TICKS_BEFORE_RETARGET = 10;
// Fixed duration for ability animations (seconds) - does not scale with attack speed
const ABILITY_DURATION = 1.0;

class CombatUnit {
    constructor(unit, playerId, x, y, combatStats) {
        this.instanceId = unit.instanceId;
        this.unitId = unit.unitId;
        this.name = unit.name;
        this.playerId = playerId;
        this.x = x;
        this.y = y;
        this.stats = combatStats;
        this.currentHealth = combatStats.health;
        // Mana for abilities - starts at 0, fills from auto attacks
        this.maxMana = combatStats.maxMana || combatStats.mana || 40;
        this.currentMana = 0; // Always start at 0 mana
        this.manaPerAttack = GameConstants.Combat.MANA_PER_ATTACK;
        // No initial delay - attack immediately when in range, then at constant rate
        this.attackCooldown = 0;
        this.moveCooldown = 0; // Movement cooldown
        this.arrivalTick = 0; // Tick when unit finishes moving to current position (0 = already there)
        this.isDead = false;
        this.target = null;
        this.stuckTicks = 0; // Track how long unit has been unable to move toward target
        this.previousX = x; // Track previous position to avoid oscillation
        this.previousY = y;
        // Copy loot type(s) for PvE enemies
        this.lootType = unit.lootType || null;
        this.lootTypes = unit.lootTypes || null; // Array for multiple drops (boss)
        // Copy items for display
        this.items = unit.items || [];

        // --- New fields for expanded combat ---
        this.traits = unit.traits || [];
        this.template = UnitTemplates[unit.unitId] || null;
        this.ability = this.template ? this.template.ability || null : null;
        this.damageAffinity = this.template ? this.template.damageAffinity || 'physical' : 'physical';
        this.elementalAutoattacks = this.template ? this.template.elementalAutoattacks || false : false;
        this.attunedElement = null; // Set by simulator from per-game vars
        this.totalAttacks = 0; // For Wild ramping
        this.hasCharged = false; // For Cavalry first-contact stun
        this.firstAttackDone = false; // For Shadow first-attack bonus
        this.activeBuffs = []; // Temp stat buffs: [{ stat, value, remainingTicks, isPermanent }]
        this.shield = 0; // Absorbs damage before HP
        this.untargetableUntilTick = 0; // Shadow stealth / Griffin dive
        this.itemEffects = {}; // Extracted from items: { lifesteal, reflect, revive, hpRegen }
        this.hasRevived = false; // Guardian Angel one-shot
        this.bonusGold = 0; // Gold earned from on-kill effects (Chest Monster)
        this.enhancedAttacksRemaining = 0; // Flame Knight enhanced attacks
        this.enhancedAttackData = null; // Data for enhanced attacks (hitAdjacentForPercent, etc.)
        this.appliesBleedOnAttack = false; // Berserker Blood Frenzy
        this.bleedOnAttackDps = 0;
        this.bleedOnAttackDuration = 0;
    }

    // Check if unit has enough mana to cast ability
    canCastAbility() {
        return this.currentMana >= this.maxMana;
    }

    // Gain mana (from auto attacks)
    gainMana(amount) {
        this.currentMana = Math.min(this.currentMana + amount, this.maxMana * 2); // Cap at 2x max to prevent overflow
    }

    // Use ability and reset mana (keeping overfill)
    useAbility() {
        const overfill = Math.max(0, this.currentMana - this.maxMana);
        this.currentMana = overfill;
        return overfill;
    }
}

class CombatSimulator {
    constructor(player1Board, player2Board, player1State, player2State, options = {}) {
        this.units = [];
        this.events = [];
        this.pendingAttacks = []; // Attacks that have started but haven't landed yet
        this.tick = 0;
        this.maxTicks = 1200; // 60 seconds at 20 ticks/sec
        this.tickRate = 0.05; // 50ms per tick (20Hz)

        // Team traits for combat effects
        this.teamTraits = {
            player1: player1State.activeTraits || {},
            player2: player2State.activeTraits || {}
        };

        // Per-game variables (attuned element, blessed bonus, warlord enhancement)
        this.perGameVars = options.perGameVars || {};

        // Status effects: instanceId -> [{ type, remainingTicks, sourceId, dps, slowPercent }]
        this.statusEffects = new Map();

        // Initialize combat units from both players
        this.initializeUnits(player1Board, player1State, 'player1');
        this.initializeUnits(player2Board, player2State, 'player2');

        // Post-initialization pass: apply Legion adjacency bonuses and other init-time effects
        this.applyInitTimeEffects();
    }

    initializeUnits(board, playerState, teamId) {
        for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
            for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                const unit = board[x][y];
                if (unit) {
                    // Get combat stats with all bonuses applied
                    const combatStats = playerState.getUnitCombatStats(unit);

                    // Player1 stays at rows 0-3, Player2 is offset to rows 4-7 (mirrored so front rows face each other)
                    // Player2's y=0 (back row) goes to y=7, y=3 (front row) goes to y=4
                    const combatY = teamId === 'player2'
                        ? (GameConstants.Grid.HEIGHT * 2 - 1 - y)  // Maps 0->7, 1->6, 2->5, 3->4
                        : y;
                    const combatUnit = new CombatUnit(unit, teamId, x, combatY, combatStats);

                    // Set attuned element from per-game vars
                    if (this.perGameVars.attunedElement) {
                        combatUnit.attunedElement = this.perGameVars.attunedElement;
                    }

                    // Extract item combat effects
                    if (unit.items && unit.items.length > 0) {
                        for (const item of unit.items) {
                            const itemTemplate = ItemTemplates[item.itemId];
                            if (itemTemplate && itemTemplate.effects) {
                                if (itemTemplate.effects.lifesteal) {
                                    combatUnit.itemEffects.lifesteal = (combatUnit.itemEffects.lifesteal || 0) + itemTemplate.effects.lifesteal;
                                }
                                if (itemTemplate.effects.reflect) {
                                    combatUnit.itemEffects.reflect = (combatUnit.itemEffects.reflect || 0) + itemTemplate.effects.reflect;
                                }
                                if (itemTemplate.effects.revive) {
                                    combatUnit.itemEffects.revive = true;
                                }
                                if (itemTemplate.effects.hpRegen) {
                                    combatUnit.itemEffects.hpRegen = (combatUnit.itemEffects.hpRegen || 0) + itemTemplate.effects.hpRegen;
                                }
                            }
                        }
                    }

                    // Crest lifesteal (from major_bloodlust crest applied via stats)
                    if (combatStats.lifesteal) {
                        combatUnit.itemEffects.lifesteal = (combatUnit.itemEffects.lifesteal || 0) + combatStats.lifesteal;
                    }

                    this.units.push(combatUnit);
                }
            }
        }
    }

    // Post-init effects: Legion adjacency, Shadow stealth, Cavalry move bonus
    applyInitTimeEffects() {
        for (const unit of this.units) {
            const teamTraits = this.getTeamActiveTraits(unit);

            // Shadow: untargetable at combat start
            if (this.unitHasTrait(unit, 'shadow')) {
                const shadowTier = this.getTraitTier(unit, 'shadow');
                if (shadowTier && shadowTier.bonus && shadowTier.bonus.untargetable) {
                    const untargetableTicks = Math.ceil(shadowTier.bonus.untargetable / this.tickRate);
                    unit.untargetableUntilTick = untargetableTicks;
                }
            }

            // Cavalry: bonus move speed at combat start
            if (this.unitHasTrait(unit, 'cavalry')) {
                const cavTier = this.getTraitTier(unit, 'cavalry');
                if (cavTier && cavTier.bonus && cavTier.bonus.moveBonus) {
                    unit.stats.moveSpeed = (unit.stats.moveSpeed || 2) + cavTier.bonus.moveBonus;
                }
            }
        }

        // Legion: adjacent Legion units get AD bonus at combat start
        for (const unit of this.units) {
            if (!this.unitHasTrait(unit, 'legion')) continue;
            const legionTier = this.getTraitTier(unit, 'legion');
            if (!legionTier || !legionTier.bonus || !legionTier.bonus.adjAttackPercent) continue;

            const neighbors = this.getHexNeighbors(unit.x, unit.y);
            for (const neighbor of neighbors) {
                const adjUnit = this.getAliveUnits().find(u =>
                    u.x === neighbor.x && u.y === neighbor.y && u.playerId === unit.playerId
                );
                if (adjUnit && this.unitHasTrait(adjUnit, 'legion')) {
                    const bonusAD = Math.round(adjUnit.stats.attack * (legionTier.bonus.adjAttackPercent / 100));
                    adjUnit.stats.attack += bonusAD;
                    if (legionTier.bonus.adjArmor) {
                        adjUnit.stats.armor += legionTier.bonus.adjArmor;
                    }
                }
            }
        }
    }

    run() {
        // Record initial state
        this.events.push({
            type: CombatEventType.COMBAT_START,
            tick: 0,
            units: this.units.map(u => ({
                instanceId: u.instanceId,
                unitId: u.unitId,
                name: u.name,
                playerId: u.playerId,
                x: u.x,
                y: u.y,
                health: u.currentHealth,
                maxHealth: u.stats.health,
                mana: u.currentMana,
                maxMana: u.maxMana,
                stats: u.stats,
                items: u.items || []
            }))
        });

        // Debug: Log all units at combat start with full details
        console.log(`[COMBAT] === Combat Start (tickRate: ${this.tickRate}s) ===`);
        for (const u of this.units) {
            console.log(`[COMBAT]   ${u.unitId} (${u.playerId}) at COMBAT pos (${u.x},${u.y}) - HP:${u.currentHealth} ATK:${u.stats.attack} atkSpd:${u.stats.attackSpeed} range:${u.stats.range}`);
        }
        // Log distance between units
        if (this.units.length === 2) {
            const dist = this.getDistance(this.units[0], this.units[1]);
            console.log(`[COMBAT]   Initial distance: ${dist} tiles`);
        }

        // Run combat simulation
        while (this.tick < this.maxTicks) {
            this.tick++;

            // Process status effects (DoT damage, expiry)
            this.processStatusEffects();

            // Process buff expiry
            this.processBuffs();

            // Process aura effects (Nature heal, item regen) - gated 1/sec internally
            this.processAuraEffects();

            // Process pending attacks (damage lands, death, lifesteal, reflect)
            this.processPendingAttacks();

            // Snapshot positions at start of tick for simultaneous movement decisions
            const positionSnapshot = new Map();
            for (const unit of this.getAliveUnits()) {
                positionSnapshot.set(unit.instanceId, { x: unit.x, y: unit.y });
            }

            // Phase 1: Collect movement decisions (using snapshot positions)
            // Skip stunned/rooted units
            const pendingMoves = [];
            for (const unit of this.getAliveUnits()) {
                if (this.hasStatusEffect(unit, StatusEffects.STUN)) continue;
                if (this.hasStatusEffect(unit, StatusEffects.ROOT)) continue;
                const moveDecision = this.getUnitMoveDecision(unit, positionSnapshot);
                if (moveDecision) {
                    pendingMoves.push(moveDecision);
                }
            }

            // Phase 2: Apply all movements simultaneously
            for (const move of pendingMoves) {
                this.applyUnitMove(move.unit, move.newX, move.newY, move.target);
            }

            // Phase 3: Process attacks (after all movement is complete)
            // Skip stunned units
            for (const unit of this.getAliveUnits()) {
                if (this.hasStatusEffect(unit, StatusEffects.STUN)) continue;
                this.processUnitAttack(unit);
            }

            // Check for combat end (only if no pending attacks - let in-flight attacks resolve)
            const player1Units = this.getAliveUnits().filter(u => u.playerId === 'player1');
            const player2Units = this.getAliveUnits().filter(u => u.playerId === 'player2');

            if ((player1Units.length === 0 || player2Units.length === 0) && this.pendingAttacks.length === 0) {
                break;
            }
        }

        // Determine winner
        const player1Units = this.getAliveUnits().filter(u => u.playerId === 'player1');
        const player2Units = this.getAliveUnits().filter(u => u.playerId === 'player2');

        let winner = null;
        let remainingUnits = 0;

        if (player1Units.length > 0 && player2Units.length === 0) {
            winner = 'player1';
            remainingUnits = player1Units.length;
        } else if (player2Units.length > 0 && player1Units.length === 0) {
            winner = 'player2';
            remainingUnits = player2Units.length;
        } else if (player1Units.length > 0 && player2Units.length > 0) {
            // Timeout - compare total remaining health
            const p1Health = player1Units.reduce((sum, u) => sum + u.currentHealth, 0);
            const p2Health = player2Units.reduce((sum, u) => sum + u.currentHealth, 0);
            winner = p1Health >= p2Health ? 'player1' : 'player2';
            remainingUnits = winner === 'player1' ? player1Units.length : player2Units.length;
        }

        // Damage = 1 (base for losing) + number of surviving enemy units
        const damage = 1 + remainingUnits;

        this.events.push({
            type: CombatEventType.COMBAT_END,
            tick: this.tick,
            winner,
            remainingUnits,
            damage
        });

        console.log(`[COMBAT] === Combat End at tick ${this.tick} (${(this.tick * this.tickRate).toFixed(2)}s) - Winner: ${winner} ===`);

        return {
            winner,
            remainingUnits,
            damage,
            events: this.events,
            durationTicks: this.tick
        };
    }

    getAliveUnits() {
        return this.units.filter(u => !u.isDead);
    }

    getEnemyUnits(unit) {
        return this.getAliveUnits().filter(u => u.playerId !== unit.playerId);
    }

    getFriendlyUnits(unit) {
        return this.getAliveUnits().filter(u => u.playerId === unit.playerId);
    }

    // ========== TRAIT HELPERS ==========

    getTeamActiveTraits(unit) {
        return this.teamTraits[unit.playerId] || {};
    }

    getTraitTier(unit, traitId) {
        const teamTraits = this.getTeamActiveTraits(unit);
        const traitInfo = teamTraits[traitId];
        if (!traitInfo || !traitInfo.tier) return null;
        return traitInfo.tier;
    }

    unitHasTrait(unit, traitId) {
        return unit.traits && unit.traits.includes(traitId);
    }

    isUntargetable(unit) {
        return unit.untargetableUntilTick > this.tick;
    }

    // ========== DAMAGE CALCULATION ==========

    calculateDamage(attacker, target, rawDamage, damageType, options = {}) {
        let finalDamage = rawDamage;
        let isCrit = false;
        let resolvedDamageType = damageType;

        // Resolve 'attuned' damage type to actual element
        if (resolvedDamageType === 'attuned') {
            resolvedDamageType = attacker.attunedElement || 'arcane';
        }

        // Crit check
        const critChance = (attacker.stats.critChance || 0) / 100;
        if (options.guaranteedCrit || (critChance > 0 && Math.random() < critChance)) {
            isCrit = true;
            const critMultiplier = attacker.stats.critDamage || 1.5;
            finalDamage = Math.round(finalDamage * critMultiplier);
        }

        // Bonus damage % (Dragon trait)
        if (attacker.stats.bonusDamagePercent) {
            finalDamage = Math.round(finalDamage * (1 + attacker.stats.bonusDamagePercent / 100));
        }

        // Attuned global element bonus: when dealing attuned element damage, all allies get bonus
        if (this.perGameVars.attunedElement && resolvedDamageType === this.perGameVars.attunedElement) {
            const teamTraits = this.getTeamActiveTraits(attacker);
            const attunedTrait = teamTraits['attuned'];
            if (attunedTrait && attunedTrait.tier && attunedTrait.tier.bonus && attunedTrait.tier.bonus.globalElementPercent) {
                finalDamage = Math.round(finalDamage * (1 + attunedTrait.tier.bonus.globalElementPercent / 100));
            }
        }

        // Resistance
        if (resolvedDamageType === 'physical') {
            // Physical: armor reduction
            const armor = Math.max(0, target.stats.armor);
            const armorReduction = armor / (armor + 100);
            finalDamage = Math.round(finalDamage * (1 - armorReduction));
        } else {
            // Non-physical (fire, arcane, nature, shadow): magic resist reduction
            const mr = Math.max(0, target.stats.magicResist || 0);
            const mrReduction = mr / (mr + 100);
            finalDamage = Math.round(finalDamage * (1 - mrReduction));

            // Fire damage also checks fireResist (Dragon trait, %-based)
            if (resolvedDamageType === 'fire' && target.stats.fireResist) {
                finalDamage = Math.round(finalDamage * (1 - target.stats.fireResist / 100));
            }
        }

        // Damage reduction (Ironclad trait, %-based)
        if (target.stats.damageReduction) {
            finalDamage = Math.round(finalDamage * (1 - target.stats.damageReduction / 100));
        }

        // Minimum 1 damage
        finalDamage = Math.max(1, finalDamage);

        return { finalDamage, isCrit, damageType: resolvedDamageType };
    }

    // ========== STATUS EFFECTS ==========

    applyStatusEffect(unit, type, duration, sourceId, options = {}) {
        if (unit.isDead) return;

        let effects = this.statusEffects.get(unit.instanceId);
        if (!effects) {
            effects = [];
            this.statusEffects.set(unit.instanceId, effects);
        }

        const durationTicks = Math.ceil(duration / this.tickRate);

        // CC effects (stun/root/frost) refresh duration, don't stack
        if (type === StatusEffects.STUN || type === StatusEffects.ROOT || type === StatusEffects.FROST) {
            const existing = effects.find(e => e.type === type);
            if (existing) {
                existing.remainingTicks = Math.max(existing.remainingTicks, durationTicks);
                if (options.slowPercent) existing.slowPercent = options.slowPercent;
                return;
            }
        }

        // DoTs stack from different sources
        if (type === StatusEffects.BLEED || type === StatusEffects.BURN || type === StatusEffects.POISON) {
            const existing = effects.find(e => e.type === type && e.sourceId === sourceId);
            if (existing) {
                existing.remainingTicks = durationTicks;
                return;
            }
        }

        effects.push({
            type,
            remainingTicks: durationTicks,
            sourceId: sourceId || null,
            dps: options.dps || 0,
            slowPercent: options.slowPercent || 0,
            lastDotTick: 0 // Track last DoT tick to apply once per second
        });

        this.events.push({
            type: CombatEventType.UNIT_STATUS_EFFECT,
            tick: this.tick,
            instanceId: unit.instanceId,
            effect: type,
            duration: duration,
            sourceId: sourceId || null
        });
    }

    processStatusEffects() {
        for (const [instanceId, effects] of this.statusEffects.entries()) {
            const unit = this.getUnitByInstanceId(instanceId);
            if (!unit || unit.isDead) {
                this.statusEffects.delete(instanceId);
                continue;
            }

            const expiredIndices = [];
            for (let i = 0; i < effects.length; i++) {
                const effect = effects[i];
                effect.remainingTicks--;

                // Process DoT damage (once per second = every 20 ticks)
                if (effect.dps > 0 && (this.tick - effect.lastDotTick) >= 20) {
                    effect.lastDotTick = this.tick;
                    let dotDamageType = 'physical';
                    if (effect.type === StatusEffects.BURN) dotDamageType = 'fire';
                    else if (effect.type === StatusEffects.POISON) dotDamageType = 'nature';

                    const dotDamage = Math.round(effect.dps);
                    if (dotDamage > 0) {
                        unit.currentHealth -= dotDamage;

                        this.events.push({
                            type: CombatEventType.UNIT_DAMAGE,
                            tick: this.tick,
                            instanceId: unit.instanceId,
                            damage: dotDamage,
                            currentHealth: Math.max(0, unit.currentHealth),
                            maxHealth: unit.stats.health,
                            damageType: dotDamageType,
                            isDot: true
                        });

                        if (unit.currentHealth <= 0) {
                            this.handleDeath(unit, effect.sourceId ? this.getUnitByInstanceId(effect.sourceId) : null);
                        }
                    }
                }

                if (effect.remainingTicks <= 0) {
                    expiredIndices.push(i);
                }
            }

            // Remove expired effects (iterate in reverse)
            for (let i = expiredIndices.length - 1; i >= 0; i--) {
                effects.splice(expiredIndices[i], 1);
            }

            if (effects.length === 0) {
                this.statusEffects.delete(instanceId);
            }
        }
    }

    hasStatusEffect(unit, type) {
        const effects = this.statusEffects.get(unit.instanceId);
        if (!effects) return false;
        return effects.some(e => e.type === type);
    }

    getStatusEffect(unit, type) {
        const effects = this.statusEffects.get(unit.instanceId);
        if (!effects) return null;
        return effects.find(e => e.type === type);
    }

    getFrostSlowPercent(unit) {
        const frost = this.getStatusEffect(unit, StatusEffects.FROST);
        if (!frost) return 0;
        return frost.slowPercent || GameConstants.Combat.FROST_AS_SLOW;
    }

    // ========== BUFF SYSTEM ==========

    applyBuff(unit, stat, value, durationSeconds, isPermanent = false) {
        if (unit.isDead) return;

        // Apply the stat change immediately
        if (stat === 'attackSpeed') {
            unit.stats.attackSpeed = (unit.stats.attackSpeed || 0.5) + value;
        } else if (stat === 'attack') {
            unit.stats.attack += value;
        } else if (stat === 'armor') {
            unit.stats.armor += value;
        } else if (stat === 'magicResist') {
            unit.stats.magicResist += value;
        } else if (stat === 'critChance') {
            unit.stats.critChance = (unit.stats.critChance || 0) + value * 100;
        } else if (stat === 'damageReduction') {
            unit.stats.damageReduction = (unit.stats.damageReduction || 0) + value * 100;
        }

        if (!isPermanent) {
            const durationTicks = Math.ceil(durationSeconds / this.tickRate);
            unit.activeBuffs.push({ stat, value, remainingTicks: durationTicks });
        }

        this.events.push({
            type: CombatEventType.UNIT_BUFF,
            tick: this.tick,
            instanceId: unit.instanceId,
            stat,
            value,
            duration: isPermanent ? -1 : durationSeconds
        });
    }

    processBuffs() {
        for (const unit of this.getAliveUnits()) {
            const expiredIndices = [];
            for (let i = 0; i < unit.activeBuffs.length; i++) {
                const buff = unit.activeBuffs[i];
                buff.remainingTicks--;
                if (buff.remainingTicks <= 0) {
                    // Call onExpire callback if present
                    if (buff.onExpire) {
                        buff.onExpire();
                    }
                    // Reverse the stat change (skip special marker stats)
                    if (buff.stat.startsWith('_')) {
                        // Special marker buff, no stat to reverse
                    } else if (buff.stat === 'attackSpeed') {
                        unit.stats.attackSpeed = Math.max(0.1, unit.stats.attackSpeed - buff.value);
                    } else if (buff.stat === 'attack') {
                        unit.stats.attack = Math.max(0, unit.stats.attack - buff.value);
                    } else if (buff.stat === 'armor') {
                        unit.stats.armor = Math.max(0, unit.stats.armor - buff.value);
                    } else if (buff.stat === 'magicResist') {
                        unit.stats.magicResist = Math.max(0, unit.stats.magicResist - buff.value);
                    } else if (buff.stat === 'critChance') {
                        unit.stats.critChance = Math.max(0, (unit.stats.critChance || 0) - buff.value * 100);
                    } else if (buff.stat === 'damageReduction') {
                        unit.stats.damageReduction = Math.max(0, (unit.stats.damageReduction || 0) - buff.value * 100);
                    }
                    expiredIndices.push(i);
                }
            }
            for (let i = expiredIndices.length - 1; i >= 0; i--) {
                unit.activeBuffs.splice(expiredIndices[i], 1);
            }
        }
    }

    // ========== SHIELD SYSTEM ==========

    applyShield(unit, amount) {
        if (unit.isDead) return;
        unit.shield += amount;
        this.events.push({
            type: CombatEventType.UNIT_SHIELD,
            tick: this.tick,
            instanceId: unit.instanceId,
            shieldAmount: amount,
            currentShield: unit.shield
        });
    }

    // ========== AURA EFFECTS ==========

    processAuraEffects() {
        // Gate most effects to once per second (every 20 ticks)
        if (this.tick % 20 !== 0) return;

        for (const unit of this.getAliveUnits()) {
            // Nature trait: heal adjacent allies
            if (this.unitHasTrait(unit, 'nature')) {
                const natureTier = this.getTraitTier(unit, 'nature');
                if (natureTier && natureTier.bonus && natureTier.bonus.adjHealPerSec) {
                    const healPerSec = natureTier.bonus.adjHealPerSec;
                    const neighbors = this.getHexNeighbors(unit.x, unit.y);
                    for (const neighbor of neighbors) {
                        const adjUnit = this.getAliveUnits().find(u =>
                            u.x === neighbor.x && u.y === neighbor.y && u.playerId === unit.playerId && u !== unit
                        );
                        if (adjUnit && adjUnit.currentHealth < adjUnit.stats.health) {
                            this.healUnit(adjUnit, healPerSec, unit.instanceId);
                        }
                    }
                }
            }

            // Warmog's regen (item)
            if (unit.itemEffects.hpRegen && unit.currentHealth < unit.stats.health) {
                const regenAmount = Math.round(unit.stats.health * unit.itemEffects.hpRegen / 100);
                if (regenAmount > 0) {
                    this.healUnit(unit, regenAmount, unit.instanceId);
                }
            }
        }
    }

    healUnit(unit, amount, sourceId) {
        if (unit.isDead) return;
        const oldHealth = unit.currentHealth;
        unit.currentHealth = Math.min(unit.stats.health, unit.currentHealth + amount);
        const healed = unit.currentHealth - oldHealth;
        if (healed > 0) {
            this.events.push({
                type: CombatEventType.UNIT_HEAL,
                tick: this.tick,
                instanceId: unit.instanceId,
                healAmount: healed,
                currentHealth: unit.currentHealth,
                maxHealth: unit.stats.health,
                sourceId: sourceId || null
            });
        }
    }

    // ========== DEATH HANDLING ==========

    handleDeath(unit, killer) {
        if (unit.isDead) return; // Guard against re-entrance

        unit.isDead = true;

        // Guardian Angel revive check
        if (unit.itemEffects.revive && !unit.hasRevived) {
            unit.hasRevived = true;
            unit.isDead = false;
            unit.currentHealth = Math.round(unit.stats.health * 0.3);

            this.events.push({
                type: CombatEventType.UNIT_HEAL,
                tick: this.tick,
                instanceId: unit.instanceId,
                healAmount: unit.currentHealth,
                currentHealth: unit.currentHealth,
                maxHealth: unit.stats.health,
                sourceId: unit.instanceId
            });
            return;
        }

        const deathEvent = {
            type: CombatEventType.UNIT_DEATH,
            tick: this.tick,
            instanceId: unit.instanceId,
            killerId: killer ? killer.instanceId : null
        };

        console.log(`[COMBAT] Tick ${this.tick}: ${unit.unitId} DIED (killed by ${killer ? killer.unitId : 'unknown'})`);

        // Loot drops
        if (unit.lootTypes && unit.lootTypes.length > 0) {
            deathEvent.lootDrops = unit.lootTypes.map((lootType, index) => ({
                lootType,
                lootPosition: { x: unit.x, y: unit.y },
                lootId: uuidv4(),
                offsetIndex: index
            }));
        } else if (unit.lootType && unit.lootType !== LootType.None) {
            deathEvent.lootType = unit.lootType;
            deathEvent.lootPosition = { x: unit.x, y: unit.y };
            deathEvent.lootId = uuidv4();
        }
        this.events.push(deathEvent);

        // Process on-kill effects for killer
        if (killer && !killer.isDead && killer.ability && killer.ability.onKill) {
            this.processOnKillEffect(killer, unit);
        }

        // Chain on kill (Warlock)
        if (killer && !killer.isDead && killer.ability && killer.ability.chainsOnKill && killer.ability.chainDamage) {
            const enemies = this.getEnemyUnits(killer).filter(e => e !== unit);
            if (enemies.length > 0) {
                // Find nearest enemy to the killed unit
                let nearest = null;
                let nearestDist = Infinity;
                for (const enemy of enemies) {
                    const dist = this.getDistance(unit, enemy);
                    if (dist < nearestDist) {
                        nearestDist = dist;
                        nearest = enemy;
                    }
                }
                if (nearest) {
                    const chainResult = this.calculateDamage(killer, nearest, killer.ability.chainDamage, killer.ability.damageType);
                    nearest.currentHealth -= chainResult.finalDamage;
                    this.events.push({
                        type: CombatEventType.UNIT_DAMAGE,
                        tick: this.tick,
                        instanceId: nearest.instanceId,
                        damage: chainResult.finalDamage,
                        currentHealth: Math.max(0, nearest.currentHealth),
                        maxHealth: nearest.stats.health,
                        damageType: chainResult.damageType
                    });
                    if (nearest.currentHealth <= 0) {
                        this.handleDeath(nearest, killer);
                    }
                }
            }
        }

        // Volatile trait: explode on death
        if (this.unitHasTrait(unit, 'volatile')) {
            const volatileTier = this.getTraitTier(unit, 'volatile');
            if (volatileTier && volatileTier.bonus && volatileTier.bonus.explosionDamage) {
                const neighbors = this.getHexNeighbors(unit.x, unit.y);
                const enemies = this.getAliveUnits().filter(u => u.playerId !== unit.playerId);
                for (const enemy of enemies) {
                    if (neighbors.some(n => n.x === enemy.x && n.y === enemy.y)) {
                        const explosionResult = this.calculateDamage(unit, enemy, volatileTier.bonus.explosionDamage, 'fire');
                        enemy.currentHealth -= explosionResult.finalDamage;
                        this.events.push({
                            type: CombatEventType.UNIT_DAMAGE,
                            tick: this.tick,
                            instanceId: enemy.instanceId,
                            damage: explosionResult.finalDamage,
                            currentHealth: Math.max(0, enemy.currentHealth),
                            maxHealth: enemy.stats.health,
                            damageType: 'fire'
                        });
                        if (enemy.currentHealth <= 0) {
                            this.handleDeath(enemy, unit);
                        }
                    }
                }
            }
        }

        // Clear status effects for dead unit
        this.statusEffects.delete(unit.instanceId);
    }

    processOnKillEffect(killer, victim) {
        const onKill = killer.ability.onKill;
        if (!onKill) return;

        if (onKill.buff) {
            // Temporary stat buff (e.g., Grunt +20% AS for 5s)
            this.applyBuff(killer, onKill.buff, onKill.value, onKill.duration || 5);
        }
        if (onKill.permBuff) {
            // Permanent stat buff (e.g., Werewolf +30% AS permanent)
            this.applyBuff(killer, onKill.permBuff, onKill.value, 0, true);
        }
        if (onKill.gold) {
            killer.bonusGold += onKill.gold;
        }
        if (onKill.mana) {
            killer.currentMana = Math.min(killer.maxMana * 2, killer.currentMana + onKill.mana);
        }
        if (onKill.shield) {
            this.applyShield(killer, onKill.shield);
        }
    }

    // ========== ABILITY TARGETING ==========

    getAbilityTargets(attacker, ability) {
        const targeting = ability.targeting;
        const enemies = this.getEnemyUnits(attacker).filter(e => !this.isUntargetable(e));
        const allies = this.getFriendlyUnits(attacker);

        switch (targeting) {
            case 'currentTarget':
                return attacker.target && !attacker.target.isDead && !this.isUntargetable(attacker.target)
                    ? [attacker.target] : (enemies.length > 0 ? [enemies[0]] : []);

            case 'lowestHealthEnemy': {
                if (enemies.length === 0) return [];
                const sorted = [...enemies].sort((a, b) => a.currentHealth - b.currentHealth);
                return [sorted[0]];
            }

            case 'highestHealthEnemy': {
                if (enemies.length === 0) return [];
                const sorted = [...enemies].sort((a, b) => b.currentHealth - a.currentHealth);
                return [sorted[0]];
            }

            case 'farthestEnemy': {
                if (enemies.length === 0) return [];
                const sorted = [...enemies].sort((a, b) =>
                    this.getDistance(attacker, b) - this.getDistance(attacker, a)
                );
                return [sorted[0]];
            }

            case 'backlineEnemy': {
                if (enemies.length === 0) return [];
                // Backline = farthest row from attacker's side
                const isPlayer1 = attacker.playerId === 'player1';
                const sorted = [...enemies].sort((a, b) => {
                    const aBack = isPlayer1 ? b.y - a.y : a.y - b.y;
                    return aBack;
                });
                return [sorted[0]];
            }

            case 'lowestHealthAlly': {
                if (allies.length === 0) return [];
                const sorted = [...allies].sort((a, b) =>
                    (a.currentHealth / a.stats.health) - (b.currentHealth / b.stats.health)
                );
                return [sorted[0]];
            }

            case 'nearbyEnemies': {
                const radius = ability.radius || 1;
                return enemies.filter(e => this.getDistance(attacker, e) <= radius);
            }

            case 'adjacentEnemies': {
                const target = attacker.target && !attacker.target.isDead ? attacker.target : null;
                if (!target) return enemies.filter(e => this.getDistance(attacker, e) <= 1);
                // Get enemies adjacent to the target (including target)
                const neighbors = this.getHexNeighbors(target.x, target.y);
                const result = [target];
                for (const enemy of enemies) {
                    if (enemy !== target && neighbors.some(n => n.x === enemy.x && n.y === enemy.y)) {
                        result.push(enemy);
                    }
                }
                return result;
            }

            case 'allEnemies':
                return enemies;

            case 'allAllies':
                return allies;

            case 'randomEnemy': {
                const count = ability.projectileCount || 1;
                const targets = [];
                for (let i = 0; i < count; i++) {
                    if (enemies.length > 0) {
                        targets.push(enemies[Math.floor(Math.random() * enemies.length)]);
                    }
                }
                return targets;
            }

            case 'lineFromCaster': {
                const target = attacker.target && !attacker.target.isDead ? attacker.target : null;
                if (!target) return enemies.length > 0 ? [enemies[0]] : [];
                return this.getUnitsInLine(attacker, target, enemies, ability.pierces);
            }

            case 'self':
                return [attacker];

            default:
                // Fallback to current target
                return attacker.target && !attacker.target.isDead ? [attacker.target] : [];
        }
    }

    getUnitsInLine(source, target, candidates, pierces) {
        // Simple line: get all candidates between source and target (and beyond if pierces)
        const hit = [];
        const dx = target.x - source.x;
        const dy = target.y - source.y;
        const maxDist = pierces ? 8 : this.getDistance(source, target);

        for (const candidate of candidates) {
            const dist = this.getDistance(source, candidate);
            if (dist > maxDist) continue;

            // Check if candidate is roughly on the line from source to target
            const cdx = candidate.x - source.x;
            const cdy = candidate.y - source.y;

            // Simple proximity check: is the unit close to the line?
            if (dx === 0 && dy === 0) continue;
            const lineDist = Math.abs(cdx * dy - cdy * dx) / Math.sqrt(dx * dx + dy * dy);
            if (lineDist <= 1.0) {
                // Must be in the forward direction
                const dot = cdx * dx + cdy * dy;
                if (dot > 0) {
                    hit.push(candidate);
                }
            }
        }

        // Always include the primary target
        if (!hit.includes(target)) {
            hit.unshift(target);
        }

        return hit;
    }

    processUnit(unit) {
        // Reduce cooldowns
        if (unit.attackCooldown > 0) {
            unit.attackCooldown -= this.tickRate;
        }
        if (unit.moveCooldown > 0) {
            unit.moveCooldown -= this.tickRate;
        }

        // Find target (or re-evaluate if needed)
        if (!unit.target || unit.target.isDead) {
            unit.target = this.findTarget(unit);
            unit.stuckTicks = 0;
            // Reset previous position when getting new target - allows free movement toward new target
            unit.previousX = unit.x;
            unit.previousY = unit.y;
        } else {
            // Re-evaluate target if:
            // 1. Current target is not in range AND there's a closer enemy that IS in range
            // 2. Unit has been stuck for too long
            const distToCurrentTarget = this.getDistance(unit, unit.target);
            const range = unit.stats.range || 1;

            if (distToCurrentTarget > range) {
                // Current target not in range - check if there's an enemy we CAN attack right now
                const closestEnemy = this.findTarget(unit);
                if (closestEnemy && closestEnemy !== unit.target) {
                    const distToClosest = this.getDistance(unit, closestEnemy);
                    if (distToClosest <= range) {
                        // There's an adjacent enemy we can attack! Switch to them
                        console.log(`[COMBAT] Tick ${this.tick}: ${unit.unitId} re-targeting from ${unit.target.unitId} (dist=${distToCurrentTarget}) to adjacent ${closestEnemy.unitId} (dist=${distToClosest})`);
                        unit.target = closestEnemy;
                        unit.stuckTicks = 0;
                        unit.previousX = unit.x;
                        unit.previousY = unit.y;
                    }
                }
            }

            // Also re-target if stuck for too long
            if (unit.stuckTicks >= STUCK_TICKS_BEFORE_RETARGET) {
                const alternateTarget = this.findTarget(unit, unit.target);
                if (alternateTarget) {
                    console.log(`[COMBAT] Tick ${this.tick}: ${unit.unitId} re-targeting from ${unit.target.unitId} to ${alternateTarget.unitId} after being stuck`);
                    unit.target = alternateTarget;
                    unit.stuckTicks = 0;
                    unit.previousX = unit.x;
                    unit.previousY = unit.y;
                }
                // If no alternate target, keep current target and reset stuck counter to avoid spamming
                else {
                    unit.stuckTicks = 0;
                }
            }
        }

        if (!unit.target) return;

        const distance = this.getDistance(unit, unit.target);

        // Check if in range
        if (distance <= unit.stats.range) {
            // Only attack if:
            // 1. Attack cooldown is ready
            // 2. This unit has finished moving (arrived at its tile)
            // 3. Target has finished moving (arrived at their tile)
            // This prevents "drive-by" attacks while moving and hitting units mid-step
            const canAttack = unit.attackCooldown <= 0
                && unit.arrivalTick <= this.tick
                && unit.target.arrivalTick <= this.tick;

            if (canAttack) {
                this.performAttack(unit, unit.target);
                unit.attackCooldown = 1 / unit.stats.attackSpeed;
                unit.stuckTicks = 0; // Reset stuck counter on successful attack
            }
        } else {
            // Move towards target (only if move cooldown is ready)
            if (unit.moveCooldown <= 0) {
                this.moveTowardsTarget(unit, unit.target);
                // Calculate move cooldown from unit's moveSpeed (tiles/sec)
                const moveSpeed = unit.stats.moveSpeed || DEFAULT_MOVE_SPEED;
                unit.moveCooldown = 1 / moveSpeed;
            }
        }
    }

    // Get distance using snapshot positions (for simultaneous movement decisions)
    getDistanceWithSnapshot(unit, target, positionSnapshot) {
        const unitPos = positionSnapshot.get(unit.instanceId) || { x: unit.x, y: unit.y };
        const targetPos = positionSnapshot.get(target.instanceId) || { x: target.x, y: target.y };

        const cube1 = this.offsetToCube(unitPos.x, unitPos.y);
        const cube2 = this.offsetToCube(targetPos.x, targetPos.y);
        return (Math.abs(cube1.q - cube2.q) + Math.abs(cube1.r - cube2.r) + Math.abs(cube1.s - cube2.s)) / 2;
    }

    // Find target using snapshot positions
    findTargetWithSnapshot(unit, positionSnapshot, excludeTarget = null) {
        const enemies = this.getEnemyUnits(unit);
        if (enemies.length === 0) return null;

        const unitPos = positionSnapshot.get(unit.instanceId) || { x: unit.x, y: unit.y };

        let closest = null;
        let closestDist = Infinity;
        let closestXDiff = Infinity;

        for (const enemy of enemies) {
            if (excludeTarget && enemy === excludeTarget) continue;

            const dist = this.getDistanceWithSnapshot(unit, enemy, positionSnapshot);
            const enemyPos = positionSnapshot.get(enemy.instanceId) || { x: enemy.x, y: enemy.y };
            const xDiff = Math.abs(unitPos.x - enemyPos.x);

            if (dist < closestDist || (dist === closestDist && xDiff < closestXDiff)) {
                closestDist = dist;
                closestXDiff = xDiff;
                closest = enemy;
            }
        }

        return closest;
    }

    // Decide if unit should move this tick (using snapshot positions for fairness)
    getUnitMoveDecision(unit, positionSnapshot) {
        // Reduce cooldowns
        if (unit.attackCooldown > 0) {
            unit.attackCooldown -= this.tickRate;
        }
        if (unit.moveCooldown > 0) {
            unit.moveCooldown -= this.tickRate;
        }

        // Find target (or re-evaluate if needed) using snapshot positions
        if (!unit.target || unit.target.isDead) {
            unit.target = this.findTargetWithSnapshot(unit, positionSnapshot);
            unit.stuckTicks = 0;
            unit.previousX = unit.x;
            unit.previousY = unit.y;
        } else {
            // Re-evaluate target using snapshot positions
            const distToCurrentTarget = this.getDistanceWithSnapshot(unit, unit.target, positionSnapshot);
            const range = unit.stats.range || 1;

            if (distToCurrentTarget > range) {
                const closestEnemy = this.findTargetWithSnapshot(unit, positionSnapshot);
                if (closestEnemy && closestEnemy !== unit.target) {
                    const distToClosest = this.getDistanceWithSnapshot(unit, closestEnemy, positionSnapshot);
                    if (distToClosest <= range) {
                        console.log(`[COMBAT] Tick ${this.tick}: ${unit.unitId} re-targeting from ${unit.target.unitId} (dist=${distToCurrentTarget}) to adjacent ${closestEnemy.unitId} (dist=${distToClosest})`);
                        unit.target = closestEnemy;
                        unit.stuckTicks = 0;
                        unit.previousX = unit.x;
                        unit.previousY = unit.y;
                    }
                }
            }

            if (unit.stuckTicks >= STUCK_TICKS_BEFORE_RETARGET) {
                const alternateTarget = this.findTargetWithSnapshot(unit, positionSnapshot, unit.target);
                if (alternateTarget) {
                    console.log(`[COMBAT] Tick ${this.tick}: ${unit.unitId} re-targeting from ${unit.target.unitId} to ${alternateTarget.unitId} after being stuck`);
                    unit.target = alternateTarget;
                    unit.stuckTicks = 0;
                    unit.previousX = unit.x;
                    unit.previousY = unit.y;
                } else {
                    unit.stuckTicks = 0;
                }
            }
        }

        if (!unit.target) return null;

        // Use snapshot distance to decide if we need to move
        const distance = this.getDistanceWithSnapshot(unit, unit.target, positionSnapshot);

        // If in range, don't move (will attack in attack phase)
        if (distance <= unit.stats.range) {
            return null;
        }

        // Move towards target (only if move cooldown is ready)
        if (unit.moveCooldown <= 0) {
            const nextStep = this.findPathToTarget(unit, unit.target);
            if (nextStep) {
                return { unit, newX: nextStep.x, newY: nextStep.y, target: unit.target };
            }
            // Couldn't find path - increment stuck counter
            unit.stuckTicks++;
        }

        return null;
    }

    // Apply a movement decision
    applyUnitMove(unit, newX, newY, target) {
        // Check if destination is now occupied (by a unit that moved earlier this tick)
        if (this.isOccupied(newX, newY, unit)) {
            unit.stuckTicks++;
            return false;
        }

        const oldX = unit.x;
        const oldY = unit.y;

        unit.previousX = oldX;
        unit.previousY = oldY;
        unit.x = newX;
        unit.y = newY;

        let moveSpeed = unit.stats.moveSpeed || DEFAULT_MOVE_SPEED;
        // Frost slow affects movement speed
        const frostSlow = this.getFrostSlowPercent(unit);
        if (frostSlow > 0) {
            moveSpeed = moveSpeed * (1 - frostSlow);
        }
        const moveDuration = 1 / moveSpeed;
        const moveDurationTicks = Math.ceil(moveDuration / this.tickRate);
        unit.arrivalTick = this.tick + moveDurationTicks;
        unit.moveCooldown = moveDuration;

        console.log(`[COMBAT] Tick ${this.tick}: ${unit.unitId} (${unit.playerId}) moves (${oldX},${oldY}) -> (${newX},${newY}), arrives at tick ${unit.arrivalTick}, target at (${target.x},${target.y})`);

        this.events.push({
            type: CombatEventType.UNIT_MOVE,
            tick: this.tick,
            instanceId: unit.instanceId,
            x: newX,
            y: newY,
            duration: moveDuration
        });

        unit.stuckTicks = 0;
        return true;
    }

    // Process attacks for a unit (after all movement is complete)
    processUnitAttack(unit) {
        // Skip untargetable units (they can't attack while stealthed)
        if (this.isUntargetable(unit) && unit.totalAttacks === 0) return;

        if (!unit.target || unit.target.isDead || this.isUntargetable(unit.target)) {
            unit.target = this.findTarget(unit);
        }
        if (!unit.target) return;

        const distance = this.getDistance(unit, unit.target);

        if (distance <= unit.stats.range) {
            const isRanged = unit.stats.range > 1;
            // Ranged units can attack moving targets, melee units cannot
            const canAttack = unit.attackCooldown <= 0
                && unit.arrivalTick <= this.tick
                && (isRanged || unit.target.arrivalTick <= this.tick);

            if (canAttack) {
                // Calculate effective attack speed for cooldown
                let effectiveAS = unit.stats.attackSpeed;

                // Wild trait ramp
                if (this.unitHasTrait(unit, 'wild')) {
                    const wildTier = this.getTraitTier(unit, 'wild');
                    if (wildTier && wildTier.bonus) {
                        const rampBonus = Math.min(
                            unit.totalAttacks * (wildTier.bonus.asPerAttack || 0) / 100,
                            (wildTier.bonus.asMaxPercent || 50) / 100
                        );
                        effectiveAS = effectiveAS * (1 + rampBonus);
                    }
                }

                // Frost slow
                const frostSlow = this.getFrostSlowPercent(unit);
                if (frostSlow > 0) {
                    effectiveAS = effectiveAS * (1 - frostSlow);
                }

                // Check if unit can cast ability (mana is full)
                if (unit.canCastAbility()) {
                    const abilityDuration = this.performAbility(unit, unit.target);
                    unit.attackCooldown = Math.max(abilityDuration, 1 / effectiveAS);
                    unit.moveCooldown = abilityDuration;
                } else {
                    this.performAttack(unit, unit.target);
                    unit.attackCooldown = 1 / effectiveAS;
                }
                unit.stuckTicks = 0;
            }
        }
    }

    findTarget(unit, excludeTarget = null) {
        const enemies = this.getEnemyUnits(unit);
        if (enemies.length === 0) return null;

        // Find closest enemy (optionally excluding a specific target)
        // Tie-breaker: prefer enemies in the same column (more direct path)
        let closest = null;
        let closestDist = Infinity;
        let closestXDiff = Infinity; // For tie-breaking

        for (const enemy of enemies) {
            // Skip excluded target (used when re-evaluating due to being stuck)
            if (excludeTarget && enemy === excludeTarget) continue;
            // Skip untargetable enemies
            if (this.isUntargetable(enemy)) continue;

            const dist = this.getDistance(unit, enemy);
            const xDiff = Math.abs(unit.x - enemy.x); // For tie-breaking

            if (dist < closestDist || (dist === closestDist && xDiff < closestXDiff)) {
                closestDist = dist;
                closestXDiff = xDiff;
                closest = enemy;
            }
        }

        // If all enemies are untargetable, target the closest one anyway (they'll become targetable soon)
        if (!closest) {
            for (const enemy of enemies) {
                if (excludeTarget && enemy === excludeTarget) continue;
                const dist = this.getDistance(unit, enemy);
                const xDiff = Math.abs(unit.x - enemy.x);
                if (dist < closestDist || (dist === closestDist && xDiff < closestXDiff)) {
                    closestDist = dist;
                    closestXDiff = xDiff;
                    closest = enemy;
                }
            }
        }

        return closest;
    }

    getDistance(unit1, unit2) {
        // Hex distance for odd-row offset coordinates
        // Convert offset coords to cube coords, then calculate cube distance
        const cube1 = this.offsetToCube(unit1.x, unit1.y);
        const cube2 = this.offsetToCube(unit2.x, unit2.y);
        return (Math.abs(cube1.q - cube2.q) + Math.abs(cube1.r - cube2.r) + Math.abs(cube1.s - cube2.s)) / 2;
    }

    offsetToCube(x, y) {
        // Convert odd-row offset coordinates to cube coordinates
        const q = x - Math.floor((y - (y & 1)) / 2);
        const r = y;
        const s = -q - r;
        return { q, r, s };
    }

    // Get hex neighbors for a position (odd-row offset coordinates)
    getHexNeighbors(x, y) {
        // Neighbor offsets for odd-row offset coordinates (odd rows shifted right)
        const isOddRow = y & 1;
        if (isOddRow) {
            // Odd row: shifted right
            return [
                { x: x + 1, y: y },     // East
                { x: x + 1, y: y - 1 }, // NE
                { x: x, y: y - 1 },     // NW
                { x: x - 1, y: y },     // West
                { x: x, y: y + 1 },     // SW
                { x: x + 1, y: y + 1 }, // SE
            ];
        } else {
            // Even row: not shifted
            return [
                { x: x + 1, y: y },     // East
                { x: x, y: y - 1 },     // NE
                { x: x - 1, y: y - 1 }, // NW
                { x: x - 1, y: y },     // West
                { x: x - 1, y: y + 1 }, // SW
                { x: x, y: y + 1 },     // SE
            ];
        }
    }

    // A* pathfinding to find next step toward target
    findPathToTarget(unit, target) {
        const startX = unit.x;
        const startY = unit.y;
        const range = unit.stats.range || 1;

        const startDist = this.getDistance(unit, target);

        // Check if already in range
        if (startDist <= range) {
            return null; // No movement needed
        }

        // A* implementation
        const openSet = [{ x: startX, y: startY, g: 0, h: 0, f: 0, parent: null }];
        const closedSet = new Set();
        const gScores = new Map();
        gScores.set(`${startX},${startY}`, 0);

        while (openSet.length > 0) {
            // Get node with lowest f score
            openSet.sort((a, b) => a.f - b.f);
            const current = openSet.shift();
            const currentKey = `${current.x},${current.y}`;

            // Check if we've reached a position in range of target
            const distToTarget = this.getDistance({ x: current.x, y: current.y }, target);
            if (distToTarget <= range) {
                // Reconstruct path and return first step
                let node = current;
                while (node.parent && node.parent.parent) {
                    node = node.parent;
                }
                if (node.parent) {
                    return { x: node.x, y: node.y };
                }
                return null;
            }

            closedSet.add(currentKey);

            // Check all neighbors
            const neighbors = this.getHexNeighbors(current.x, current.y);
            for (const neighbor of neighbors) {
                const neighborKey = `${neighbor.x},${neighbor.y}`;

                // Skip if out of bounds (7 wide, 8 tall board)
                if (neighbor.x < 0 || neighbor.x >= 7 || neighbor.y < 0 || neighbor.y >= 8) {
                    continue;
                }

                // Skip if already evaluated
                if (closedSet.has(neighborKey)) {
                    continue;
                }

                // Skip if occupied (including target's position - we want to stop adjacent, not on top)
                if (this.isOccupied(neighbor.x, neighbor.y, unit)) {
                    continue;
                }

                const tentativeG = current.g + 1;
                const existingG = gScores.get(neighborKey);

                if (existingG === undefined || tentativeG < existingG) {
                    const h = this.getDistance({ x: neighbor.x, y: neighbor.y }, target);
                    // Small tie-breaker: prefer positions closer to target's x-coordinate (column)
                    // This creates a natural zigzag pattern for vertical movement instead of drifting left/right
                    const xDeviation = Math.abs(neighbor.x - target.x) * 0.01;
                    const newNode = {
                        x: neighbor.x,
                        y: neighbor.y,
                        g: tentativeG,
                        h: h,
                        f: tentativeG + h + xDeviation,
                        parent: current
                    };

                    gScores.set(neighborKey, tentativeG);

                    // Add to open set if not already there
                    const existingIndex = openSet.findIndex(n => n.x === neighbor.x && n.y === neighbor.y);
                    if (existingIndex >= 0) {
                        openSet[existingIndex] = newNode;
                    } else {
                        openSet.push(newNode);
                    }
                }
            }
        }

        // No path found
        return null;
    }

    moveTowardsTarget(unit, target) {
        // Use A* pathfinding to find the best next step
        const nextStep = this.findPathToTarget(unit, target);

        let newX = unit.x;
        let newY = unit.y;

        if (nextStep) {
            newX = nextStep.x;
            newY = nextStep.y;
        }

        if (newX !== unit.x || newY !== unit.y) {
            const oldX = unit.x;
            const oldY = unit.y;

            // Track previous position to avoid oscillation
            unit.previousX = oldX;
            unit.previousY = oldY;

            unit.x = newX;
            unit.y = newY;

            // Calculate how many ticks until the unit "arrives" at the new position
            // This equals the movement duration (1 / moveSpeed) converted to ticks
            const moveSpeed = unit.stats.moveSpeed || DEFAULT_MOVE_SPEED;
            const moveDuration = 1 / moveSpeed; // seconds
            const moveDurationTicks = Math.ceil(moveDuration / this.tickRate);
            unit.arrivalTick = this.tick + moveDurationTicks;

            console.log(`[COMBAT] Tick ${this.tick}: ${unit.unitId} (${unit.playerId}) moves (${oldX},${oldY}) -> (${newX},${newY}), arrives at tick ${unit.arrivalTick}, target at (${target.x},${target.y})`);

            this.events.push({
                type: CombatEventType.UNIT_MOVE,
                tick: this.tick,
                instanceId: unit.instanceId,
                x: newX,
                y: newY,
                // Send movement duration so client animation matches server timing
                duration: moveDuration
            });

            // Successfully moved - reset stuck counter
            unit.stuckTicks = 0;
            return true;
        }

        // Couldn't move - increment stuck counter
        unit.stuckTicks++;
        return false;
    }

    isOccupied(x, y, excludeUnit) {
        return this.getAliveUnits().some(u => u !== excludeUnit && u.x === x && u.y === y);
    }

    performAttack(attacker, target) {
        // Track attack count for Wild trait
        attacker.totalAttacks++;

        // Determine auto-attack damage type
        let autoAttackDamageType = 'physical';
        if (attacker.elementalAutoattacks) {
            autoAttackDamageType = attacker.damageAffinity || 'fire';
        }
        // Attuned units with elementalAutoattacks convert to attuned element
        if (attacker.elementalAutoattacks && this.unitHasTrait(attacker, 'attuned') && attacker.attunedElement) {
            autoAttackDamageType = attacker.attunedElement;
        }

        // Calculate damage using the central method
        const baseDamage = attacker.stats.attack;

        // Shadow first attack bonus
        let firstAttackMultiplier = 1;
        if (!attacker.firstAttackDone && this.unitHasTrait(attacker, 'shadow')) {
            const shadowTier = this.getTraitTier(attacker, 'shadow');
            if (shadowTier && shadowTier.bonus && shadowTier.bonus.firstAttackBonus) {
                firstAttackMultiplier = 1 + shadowTier.bonus.firstAttackBonus / 100;
            }
            attacker.firstAttackDone = true;
        }

        const rawDamage = Math.round(baseDamage * firstAttackMultiplier);
        const damageResult = this.calculateDamage(attacker, target, rawDamage, autoAttackDamageType);

        // Calculate effective attack speed (with Wild ramping and frost slow)
        let effectiveAS = attacker.stats.attackSpeed;

        // Wild trait: ramp AS with each attack
        if (this.unitHasTrait(attacker, 'wild')) {
            const wildTier = this.getTraitTier(attacker, 'wild');
            if (wildTier && wildTier.bonus) {
                const rampBonus = Math.min(
                    attacker.totalAttacks * (wildTier.bonus.asPerAttack || 0) / 100,
                    (wildTier.bonus.asMaxPercent || 50) / 100
                );
                effectiveAS = effectiveAS * (1 + rampBonus);
            }
        }

        // Frost slow
        const frostSlow = this.getFrostSlowPercent(attacker);
        if (frostSlow > 0) {
            effectiveAS = effectiveAS * (1 - frostSlow);
        }

        // Calculate when the attack will land (hit point in animation)
        const attackDuration = 1 / effectiveAS;
        const hitDelay = attackDuration * DEFAULT_HIT_POINT;
        const hitTick = this.tick + Math.ceil(hitDelay / this.tickRate);

        // Determine if this is a ranged attack (range > 1)
        const isRanged = attacker.stats.range > 1;

        console.log(`[COMBAT] Tick ${this.tick}: ${attacker.unitId} starts attack on ${target.unitId} (${isRanged ? 'ranged' : 'melee'}, ${damageResult.damageType}, ${damageResult.isCrit ? 'CRIT ' : ''}lands at tick ${hitTick})`);

        // Emit attack start event
        this.events.push({
            type: CombatEventType.UNIT_ATTACK,
            tick: this.tick,
            attackerId: attacker.instanceId,
            targetId: target.instanceId,
            damage: damageResult.finalDamage,
            hitTick,
            damageType: damageResult.damageType,
            isCrit: damageResult.isCrit
        });

        // Queue the attack to land later
        this.pendingAttacks.push({
            attackerId: attacker.instanceId,
            attackerUnitId: attacker.unitId,
            targetId: target.instanceId,
            targetUnitId: target.unitId,
            damage: damageResult.finalDamage,
            damageType: damageResult.damageType,
            isCrit: damageResult.isCrit,
            hitTick,
            isRanged
        });

        // Cavalry: first attack stun
        if (!attacker.hasCharged && this.unitHasTrait(attacker, 'cavalry')) {
            attacker.hasCharged = true;
            const cavTier = this.getTraitTier(attacker, 'cavalry');
            if (cavTier && cavTier.bonus) {
                if (cavTier.bonus.chargeStun) {
                    this.applyStatusEffect(target, StatusEffects.STUN, cavTier.bonus.chargeStun, attacker.instanceId);
                }
                // Charge damage bonus applied as extra pending attack
                if (cavTier.bonus.chargeDamageBonus) {
                    const bonusDamage = Math.round(damageResult.finalDamage * cavTier.bonus.chargeDamageBonus / 100);
                    if (bonusDamage > 0) {
                        this.pendingAttacks.push({
                            attackerId: attacker.instanceId,
                            attackerUnitId: attacker.unitId,
                            targetId: target.instanceId,
                            targetUnitId: target.unitId,
                            damage: bonusDamage,
                            damageType: damageResult.damageType,
                            hitTick,
                            isRanged
                        });
                    }
                }
            }
        }

        // Dragon tier 4: auto-attacks apply burn
        if (this.unitHasTrait(attacker, 'dragon')) {
            const dragonTier = this.getTraitTier(attacker, 'dragon');
            if (dragonTier && dragonTier.bonus && dragonTier.bonus.burnOnHit) {
                this.applyStatusEffect(target, StatusEffects.BURN, dragonTier.bonus.burnDuration || 3, attacker.instanceId, {
                    dps: dragonTier.bonus.burnOnHit / (dragonTier.bonus.burnDuration || 3)
                });
            }
        }

        // Cleave trait: splash to adjacent enemies
        if (this.unitHasTrait(attacker, 'cleave')) {
            const cleaveTier = this.getTraitTier(attacker, 'cleave');
            if (cleaveTier && cleaveTier.bonus && cleaveTier.bonus.cleavePercent) {
                const splashDamage = Math.round(damageResult.finalDamage * cleaveTier.bonus.cleavePercent / 100);
                if (splashDamage > 0) {
                    const neighbors = this.getHexNeighbors(target.x, target.y);
                    const enemies = this.getEnemyUnits(attacker);
                    for (const enemy of enemies) {
                        if (enemy !== target && neighbors.some(n => n.x === enemy.x && n.y === enemy.y)) {
                            this.pendingAttacks.push({
                                attackerId: attacker.instanceId,
                                attackerUnitId: attacker.unitId,
                                targetId: enemy.instanceId,
                                targetUnitId: enemy.unitId,
                                damage: splashDamage,
                                damageType: damageResult.damageType,
                                hitTick,
                                isRanged: true, // Splash always lands
                                isCleave: true
                            });
                        }
                    }
                }
            }
        }

        // Enhanced attacks (Flame Knight)
        if (attacker.enhancedAttacksRemaining > 0 && attacker.enhancedAttackData) {
            attacker.enhancedAttacksRemaining--;
            const enhData = attacker.enhancedAttackData;
            if (enhData.hitAdjacentForPercent) {
                const enhDamage = Math.round(attacker.stats.attack * enhData.hitAdjacentForPercent);
                const neighbors = this.getHexNeighbors(target.x, target.y);
                const enemies = this.getEnemyUnits(attacker);
                for (const enemy of enemies) {
                    if (enemy !== target && neighbors.some(n => n.x === enemy.x && n.y === enemy.y)) {
                        const enhResult = this.calculateDamage(attacker, enemy, enhDamage, 'fire');
                        this.pendingAttacks.push({
                            attackerId: attacker.instanceId,
                            attackerUnitId: attacker.unitId,
                            targetId: enemy.instanceId,
                            targetUnitId: enemy.unitId,
                            damage: enhResult.finalDamage,
                            damageType: 'fire',
                            hitTick,
                            isRanged: true
                        });
                    }
                }
            }
        }

        // Berserker Blood Frenzy: attacks apply bleed
        if (attacker.appliesBleedOnAttack && attacker.bleedOnAttackDps > 0) {
            this.applyStatusEffect(target, StatusEffects.BLEED, attacker.bleedOnAttackDuration, attacker.instanceId, {
                dps: attacker.bleedOnAttackDps
            });
        }

        // Warlord tier 4: attacks apply bleed
        if (this.unitHasTrait(attacker, 'warlord')) {
            const warlordTier = this.getTraitTier(attacker, 'warlord');
            // Tier 4 (count >= 4) adds bleed
            const teamTraits = this.getTeamActiveTraits(attacker);
            const warlordInfo = teamTraits['warlord'];
            if (warlordInfo && warlordInfo.count >= 4) {
                this.applyStatusEffect(target, StatusEffects.BLEED, 3, attacker.instanceId, {
                    dps: 10 // 30 over 3s
                });
            }
        }

        // Generate mana for attacker (on attack start, not on hit)
        attacker.gainMana(attacker.manaPerAttack);
    }

    performAbility(attacker, target) {
        // Use mana (resets to 0, or keeps overfill)
        attacker.useAbility();

        const ability = attacker.ability;

        // Abilities use a fixed duration (does not scale with attack speed)
        const hitDelay = ABILITY_DURATION * DEFAULT_HIT_POINT;
        const hitTick = this.tick + Math.ceil(hitDelay / this.tickRate);
        const isRanged = attacker.stats.range > 1;

        // No ability template: fallback to generic 3x attack damage
        if (!ability) {
            const fallbackDamage = attacker.stats.attack * 3;
            const fallbackResult = this.calculateDamage(attacker, target, fallbackDamage, 'physical');

            this.events.push({
                type: CombatEventType.UNIT_ABILITY,
                tick: this.tick,
                attackerId: attacker.instanceId,
                targetId: target.instanceId,
                damage: fallbackResult.finalDamage,
                hitTick,
                abilityName: 'default',
                abilityDuration: ABILITY_DURATION
            });
            this.pendingAttacks.push({
                attackerId: attacker.instanceId,
                attackerUnitId: attacker.unitId,
                targetId: target.instanceId,
                targetUnitId: target.unitId,
                damage: fallbackResult.finalDamage,
                damageType: 'physical',
                hitTick,
                isRanged,
                isAbility: true
            });
            return ABILITY_DURATION;
        }

        // Resolve damage type
        let damageType = ability.damageType || 'physical';
        if (damageType === 'attuned') {
            damageType = attacker.attunedElement || 'arcane';
        }

        // Calculate ability damage: baseDamage + adRatio * attack + apRatio * AP
        const rawAbilityDamage = (ability.baseDamage || 0)
            + (ability.adRatio || 0) * attacker.stats.attack
            + (ability.apRatio || 0) * (attacker.stats.abilityPower || 0);

        // Get targets
        const targets = this.getAbilityTargets(attacker, ability);
        const primaryTarget = targets.length > 0 ? targets[0] : target;

        // Emit ability event (for client animation)
        this.events.push({
            type: CombatEventType.UNIT_ABILITY,
            tick: this.tick,
            attackerId: attacker.instanceId,
            targetId: primaryTarget.instanceId,
            damage: Math.round(rawAbilityDamage),
            hitTick,
            abilityName: ability.name || 'ability',
            abilityDuration: ABILITY_DURATION,
            damageType
        });

        console.log(`[COMBAT] Tick ${this.tick}: ${attacker.unitId} casts ${ability.name} (type: ${ability.type}, targets: ${targets.length})`);

        // Teleport to target if ability has teleports flag
        if (ability.teleports && primaryTarget !== attacker) {
            // Move attacker adjacent to target
            const neighbors = this.getHexNeighbors(primaryTarget.x, primaryTarget.y);
            const emptyNeighbor = neighbors.find(n =>
                n.x >= 0 && n.x < 7 && n.y >= 0 && n.y < 8 &&
                !this.isOccupied(n.x, n.y, attacker)
            );
            if (emptyNeighbor) {
                attacker.x = emptyNeighbor.x;
                attacker.y = emptyNeighbor.y;
                this.events.push({
                    type: CombatEventType.UNIT_MOVE,
                    tick: this.tick,
                    instanceId: attacker.instanceId,
                    x: emptyNeighbor.x,
                    y: emptyNeighbor.y,
                    duration: 0.1 // Fast teleport
                });
            }
        }

        // Charge to target if ability has charges flag
        if (ability.charges && primaryTarget !== attacker) {
            const neighbors = this.getHexNeighbors(primaryTarget.x, primaryTarget.y);
            const emptyNeighbor = neighbors.find(n =>
                n.x >= 0 && n.x < 7 && n.y >= 0 && n.y < 8 &&
                !this.isOccupied(n.x, n.y, attacker)
            );
            if (emptyNeighbor) {
                attacker.x = emptyNeighbor.x;
                attacker.y = emptyNeighbor.y;
                this.events.push({
                    type: CombatEventType.UNIT_MOVE,
                    tick: this.tick,
                    instanceId: attacker.instanceId,
                    x: emptyNeighbor.x,
                    y: emptyNeighbor.y,
                    duration: 0.2 // Fast charge
                });
            }

            // Stun from ability's stun field (e.g., Drake)
            if (ability.stun && primaryTarget) {
                this.applyStatusEffect(primaryTarget, StatusEffects.STUN, ability.stun, attacker.instanceId);
            }
        }

        // Untargetable during (Griffin Sky Dive)
        if (ability.untargetableDuring) {
            attacker.untargetableUntilTick = this.tick + Math.ceil(ABILITY_DURATION / this.tickRate);
        }

        // Process by ability type
        switch (ability.type) {
            case 'damage':
            case 'areaDamage': {
                for (const t of targets) {
                    if (t.isDead) continue;
                    const dmgResult = this.calculateDamage(attacker, t, rawAbilityDamage, damageType, {
                        guaranteedCrit: ability.guaranteedCrit
                    });
                    this.pendingAttacks.push({
                        attackerId: attacker.instanceId,
                        attackerUnitId: attacker.unitId,
                        targetId: t.instanceId,
                        targetUnitId: t.unitId,
                        damage: dmgResult.finalDamage,
                        damageType: dmgResult.damageType,
                        isCrit: dmgResult.isCrit,
                        hitTick,
                        isRanged: true,
                        isAbility: true,
                        abilityRef: ability // For on-kill processing
                    });

                    // Apply status effect if specified
                    if (ability.effect && ability.effect !== 'none' && ability.effectDuration) {
                        const effectOptions = {};
                        if (ability.effectDps) effectOptions.dps = ability.effectDps;
                        if (ability.effectSlowPercent) effectOptions.slowPercent = ability.effectSlowPercent;
                        this.applyStatusEffect(t, ability.effect, ability.effectDuration, attacker.instanceId, effectOptions);
                    }

                    // Mana burn (Demon Hunter)
                    if (ability.manaBurn) {
                        const manaBeforeBurn = t.currentMana;
                        t.currentMana = Math.max(0, t.currentMana - ability.manaBurn);
                        // If target was at full mana, deal bonus damage
                        if (ability.fullManaBonusDamage && manaBeforeBurn >= t.maxMana) {
                            const bonusResult = this.calculateDamage(attacker, t, ability.fullManaBonusDamage, damageType);
                            this.pendingAttacks.push({
                                attackerId: attacker.instanceId,
                                attackerUnitId: attacker.unitId,
                                targetId: t.instanceId,
                                targetUnitId: t.unitId,
                                damage: bonusResult.finalDamage,
                                damageType: bonusResult.damageType,
                                hitTick: hitTick + 1,
                                isRanged: true,
                                isAbility: true
                            });
                        }
                    }
                }

                // Armor shred (Demon King)
                if (ability.armorShred && ability.armorShredDuration) {
                    for (const t of targets) {
                        if (t.isDead) continue;
                        const shredAmount = Math.round(t.stats.armor * ability.armorShred);
                        if (shredAmount > 0) {
                            t.stats.armor -= shredAmount;
                            t.activeBuffs.push({
                                stat: 'armor',
                                value: -shredAmount, // Negative = restore on expiry
                                remainingTicks: Math.ceil(ability.armorShredDuration / this.tickRate)
                            });
                        }
                    }
                }
                break;
            }

            case 'heal': {
                for (const t of targets) {
                    if (t.isDead) continue;
                    let healAmount = ability.baseHealing || 0;
                    // Double healing for below 50% HP (Druid Rejuvenation)
                    if (t.currentHealth < t.stats.health * 0.5) {
                        healAmount *= 2;
                    }
                    // Nature heal effectiveness bonus
                    const natureTier = this.getTraitTier(attacker, 'nature');
                    if (natureTier && natureTier.bonus && natureTier.bonus.healEffectiveness) {
                        healAmount = Math.round(healAmount * (1 + natureTier.bonus.healEffectiveness / 100));
                    }
                    this.healUnit(t, healAmount, attacker.instanceId);
                }
                break;
            }

            case 'healAndDamage': {
                // Heal adjacent allies
                if (ability.baseHealing) {
                    const allies = this.getFriendlyUnits(attacker);
                    const neighbors = this.getHexNeighbors(attacker.x, attacker.y);
                    for (const ally of allies) {
                        if (ally !== attacker && neighbors.some(n => n.x === ally.x && n.y === ally.y)) {
                            this.healUnit(ally, ability.baseHealing, attacker.instanceId);
                        }
                    }
                }
                // Damage targets
                for (const t of targets) {
                    if (t.isDead) continue;
                    const dmgResult = this.calculateDamage(attacker, t, rawAbilityDamage, damageType);
                    this.pendingAttacks.push({
                        attackerId: attacker.instanceId,
                        attackerUnitId: attacker.unitId,
                        targetId: t.instanceId,
                        targetUnitId: t.unitId,
                        damage: dmgResult.finalDamage,
                        damageType: dmgResult.damageType,
                        hitTick,
                        isRanged: true,
                        isAbility: true,
                        abilityRef: ability
                    });
                }
                break;
            }

            case 'damageAndHeal': {
                let totalDamageDealt = 0;
                for (const t of targets) {
                    if (t.isDead) continue;
                    const dmgResult = this.calculateDamage(attacker, t, rawAbilityDamage, damageType);
                    totalDamageDealt += dmgResult.finalDamage;
                    this.pendingAttacks.push({
                        attackerId: attacker.instanceId,
                        attackerUnitId: attacker.unitId,
                        targetId: t.instanceId,
                        targetUnitId: t.unitId,
                        damage: dmgResult.finalDamage,
                        damageType: dmgResult.damageType,
                        hitTick,
                        isRanged: true,
                        isAbility: true,
                        abilityRef: ability
                    });
                    if (ability.effect && ability.effect !== 'none' && ability.effectDuration) {
                        const effectOptions = {};
                        if (ability.effectDps) effectOptions.dps = ability.effectDps;
                        if (ability.effectSlowPercent) effectOptions.slowPercent = ability.effectSlowPercent;
                        this.applyStatusEffect(t, ability.effect, ability.effectDuration, attacker.instanceId, effectOptions);
                    }
                }
                // Self-heal based on damage dealt
                if (ability.selfHealPercent && totalDamageDealt > 0) {
                    const healAmount = Math.round(totalDamageDealt * ability.selfHealPercent);
                    this.healUnit(attacker, healAmount, attacker.instanceId);
                }
                // Heal nearby allies (Bishop Knight)
                if (ability.baseHealing && ability.radius) {
                    const allies = this.getFriendlyUnits(attacker);
                    for (const ally of allies) {
                        if (this.getDistance(attacker, ally) <= ability.radius) {
                            this.healUnit(ally, ability.baseHealing, attacker.instanceId);
                        }
                    }
                }
                break;
            }

            case 'healAndBuff': {
                for (const t of targets) {
                    if (t.isDead) continue;
                    if (ability.baseHealing) {
                        this.healUnit(t, ability.baseHealing, attacker.instanceId);
                    }
                    if (ability.buff) {
                        const duration = ability.duration || ability.buff.duration || 4;
                        for (const [stat, value] of Object.entries(ability.buff)) {
                            if (stat === 'duration') continue;
                            this.applyBuff(t, stat, value, duration);
                        }
                    }
                }
                break;
            }

            case 'damageAndBuff': {
                // Damage targets
                for (const t of targets) {
                    if (t.isDead || t === attacker) continue;
                    if (rawAbilityDamage > 0) {
                        const dmgResult = this.calculateDamage(attacker, t, rawAbilityDamage, damageType);
                        this.pendingAttacks.push({
                            attackerId: attacker.instanceId,
                            attackerUnitId: attacker.unitId,
                            targetId: t.instanceId,
                            targetUnitId: t.unitId,
                            damage: dmgResult.finalDamage,
                            damageType: dmgResult.damageType,
                            hitTick,
                            isRanged: true,
                            isAbility: true,
                            abilityRef: ability
                        });
                    }
                    if (ability.effect && ability.effect !== 'none' && ability.effectDuration) {
                        const effectOptions = {};
                        if (ability.effectDps) effectOptions.dps = ability.effectDps;
                        if (ability.effectSlowPercent) effectOptions.slowPercent = ability.effectSlowPercent;
                        if (ability.effectValue) effectOptions.value = ability.effectValue;
                        this.applyStatusEffect(t, ability.effect, ability.effectDuration, attacker.instanceId, effectOptions);
                    }
                }
                // Self buff (Knight Shield Wall)
                if (ability.buff && ability.targeting === 'self') {
                    const duration = ability.effectDuration || ability.buff.duration || 4;
                    if (ability.effect === 'damageReduction' && ability.effectValue) {
                        this.applyBuff(attacker, 'damageReduction', ability.effectValue, duration);
                    }
                }
                // Ally buff (Treeant Ancient Roots, Naga Wizard, Fat Dragon)
                if (ability.allyBuff) {
                    const allyDuration = ability.allyBuff.duration || ability.duration || 4;
                    const allies = this.getFriendlyUnits(attacker);
                    for (const ally of allies) {
                        for (const [stat, value] of Object.entries(ability.allyBuff)) {
                            if (stat === 'duration') continue;
                            if (stat === 'damagePercent') {
                                // Temporary bonus damage percent
                                this.applyBuff(ally, 'attack', Math.round(ally.stats.attack * value), allyDuration);
                            } else {
                                this.applyBuff(ally, stat, value, allyDuration);
                            }
                        }
                    }
                }
                break;
            }

            case 'buff': {
                // Self buff (Berserker Blood Frenzy, Flame Knight Blazing Cleave)
                if (ability.buff) {
                    const duration = ability.buff.duration || 4;
                    if (ability.buff.attackSpeed) {
                        this.applyBuff(attacker, 'attackSpeed', ability.buff.attackSpeed, duration);
                    }
                    // Berserker: attacks apply bleed during buff
                    if (ability.buff.appliesBleed) {
                        attacker.appliesBleedOnAttack = true;
                        attacker.bleedOnAttackDps = ability.buff.bleedDps || 20;
                        attacker.bleedOnAttackDuration = ability.buff.bleedDuration || 2;
                        // Clear after buff expires
                        const bleedBuffTicks = Math.ceil(duration / this.tickRate);
                        attacker.activeBuffs.push({
                            stat: '_bleedOnAttack',
                            value: 0,
                            remainingTicks: bleedBuffTicks,
                            onExpire: () => {
                                attacker.appliesBleedOnAttack = false;
                            }
                        });
                    }
                    // Flame Knight: enhanced attacks
                    if (ability.buff.enhancedAttacks) {
                        attacker.enhancedAttacksRemaining = ability.buff.enhancedAttacks;
                        attacker.enhancedAttackData = {
                            hitAdjacentForPercent: ability.buff.hitAdjacentForPercent || 1.5
                        };
                        // Apply effect to enhanced attacks
                        if (ability.effect === 'bleed' && ability.effectDuration) {
                            attacker.bleedOnAttackDps = ability.effectDps || 25;
                            attacker.bleedOnAttackDuration = ability.effectDuration;
                            attacker.appliesBleedOnAttack = true;
                        }
                    }
                }
                break;
            }

            case 'teamBuff': {
                // Buff all allies (Blacksmith Forge Weapons)
                if (ability.buff) {
                    const allies = this.getFriendlyUnits(attacker);
                    for (const ally of allies) {
                        for (const [stat, value] of Object.entries(ability.buff)) {
                            if (stat === 'duration') continue;
                            // Team buffs are permanent for rest of combat
                            this.applyBuff(ally, stat, value, 0, true);
                        }
                    }
                }
                break;
            }

            default: {
                // Unknown type: fallback to generic damage
                const fallbackDamage = attacker.stats.attack * 3;
                const fallbackResult = this.calculateDamage(attacker, target, fallbackDamage, 'physical');
                this.pendingAttacks.push({
                    attackerId: attacker.instanceId,
                    attackerUnitId: attacker.unitId,
                    targetId: target.instanceId,
                    targetUnitId: target.unitId,
                    damage: fallbackResult.finalDamage,
                    damageType: 'physical',
                    hitTick,
                    isRanged,
                    isAbility: true
                });
                break;
            }
        }

        return ABILITY_DURATION;
    }

    processPendingAttacks() {
        // Process attacks that should land this tick
        const attacksToProcess = this.pendingAttacks.filter(a => a.hitTick <= this.tick);
        this.pendingAttacks = this.pendingAttacks.filter(a => a.hitTick > this.tick);

        for (const attack of attacksToProcess) {
            const attacker = this.getUnitByInstanceId(attack.attackerId);
            const target = this.getUnitByInstanceId(attack.targetId);

            // Check if attack should land:
            // - Ranged attacks always land (projectile is in flight)
            // - Melee attacks only land if attacker is still alive
            const attackerAlive = attacker && !attacker.isDead;
            const shouldLand = attack.isRanged || attackerAlive;

            if (!shouldLand) {
                console.log(`[COMBAT] Tick ${this.tick}: ${attack.attackerUnitId}'s melee attack on ${attack.targetUnitId} interrupted (attacker died)`);
                continue;
            }

            // Check if target is still alive or untargetable
            if (!target || target.isDead) {
                console.log(`[COMBAT] Tick ${this.tick}: ${attack.attackerUnitId}'s attack on ${attack.targetUnitId} missed (target already dead)`);
                continue;
            }

            let damage = attack.damage;

            // Shield absorption
            if (target.shield > 0) {
                if (target.shield >= damage) {
                    target.shield -= damage;
                    this.events.push({
                        type: CombatEventType.UNIT_SHIELD,
                        tick: this.tick,
                        instanceId: target.instanceId,
                        shieldAmount: -damage,
                        currentShield: target.shield
                    });
                    damage = 0;
                } else {
                    damage -= target.shield;
                    this.events.push({
                        type: CombatEventType.UNIT_SHIELD,
                        tick: this.tick,
                        instanceId: target.instanceId,
                        shieldAmount: -target.shield,
                        currentShield: 0
                    });
                    target.shield = 0;
                }
            }

            if (damage <= 0) continue;

            // Apply damage
            const attackType = attack.isAbility ? 'ABILITY' : (attack.isCleave ? 'cleave' : 'attack');
            console.log(`[COMBAT] Tick ${this.tick}: ${attack.attackerUnitId}'s ${attackType} LANDS on ${attack.targetUnitId} for ${damage} damage${attack.isCrit ? ' (CRIT)' : ''}`);
            target.currentHealth -= damage;

            this.events.push({
                type: CombatEventType.UNIT_DAMAGE,
                tick: this.tick,
                instanceId: target.instanceId,
                damage: damage,
                currentHealth: Math.max(0, target.currentHealth),
                maxHealth: target.stats.health,
                damageType: attack.damageType || 'physical',
                isCrit: attack.isCrit || false,
                isDot: false
            });

            // Lifesteal: heal attacker for % of damage dealt
            if (attacker && !attacker.isDead && attacker.itemEffects.lifesteal && !attack.isCleave) {
                const healAmount = Math.round(damage * attacker.itemEffects.lifesteal / 100);
                if (healAmount > 0) {
                    this.healUnit(attacker, healAmount, attacker.instanceId);
                }
            }

            // Thornmail reflect: deal % of damage back to attacker
            if (target.itemEffects.reflect && attacker && !attacker.isDead && !attack.isAbility) {
                const reflectDamage = Math.round(damage * target.itemEffects.reflect / 100);
                if (reflectDamage > 0) {
                    attacker.currentHealth -= reflectDamage;
                    this.events.push({
                        type: CombatEventType.UNIT_DAMAGE,
                        tick: this.tick,
                        instanceId: attacker.instanceId,
                        damage: reflectDamage,
                        currentHealth: Math.max(0, attacker.currentHealth),
                        maxHealth: attacker.stats.health,
                        damageType: 'physical'
                    });
                    if (attacker.currentHealth <= 0) {
                        this.handleDeath(attacker, target);
                    }
                }
            }

            // Check for death
            if (target.currentHealth <= 0) {
                this.handleDeath(target, attacker);
            }
        }
    }

    getUnitByInstanceId(instanceId) {
        return this.units.find(u => u.instanceId === instanceId) || null;
    }
}

class GameRoom {
    constructor(roomId, hostId) {
        this.roomId = roomId;
        this.hostId = hostId;

        // Players
        this.players = new Map(); // clientId -> PlayerState

        // Shared unit pool
        this.unitPool = new UnitPool();

        // Game state
        this.state = 'waiting'; // waiting, playing, finished
        this.phase = 'planning'; // planning, combat, results
        this.round = 1;
        this.phaseTimer = 0;
        this.phaseStartTime = null;

        // Combat state
        this.combatResults = [];
        this.matchups = [];
        this.combatEvents = new Map(); // clientId -> events for their match
        this.lastHostByPair = new Map(); // "player1Id-player2Id" -> lastHostId (for alternating home/away)

        // Timers
        this.timerInterval = null;          // Internal 100ms phase countdown
        this.phaseUpdateInterval = null;    // 1s broadcast interval (managed by server.js)
        this.combatTimer = null;            // setTimeout for combat duration wait
        this.resultsTimer = null;           // setTimeout for results duration wait
        this.resetTimer = null;             // setTimeout for room reset after game end
        this.advanceRoundTimer = null;      // setTimeout for merchant/crest advance delay
        this.merchantTurnTimer = null;      // setTimeout for merchant per-turn timer
        this.merchantSafetyTimer = null;    // setTimeout for merchant safety timeout
        this.majorCrestTimer = null;        // setTimeout for major crest round timer
        this.majorCrestStartTime = null;    // Date.now() when major crest round started

        // Generation counter - increments on every phase/round transition
        // Callbacks capture this value and bail if it has changed
        this.phaseGeneration = 0;

        // Mad Merchant state
        this.merchantState = null;
        // Structure when active:
        // {
        //   options: [{optionId, optionType, itemId, goldAmount, name, description, isPicked, pickedBy, pickedByName}],
        //   pickOrder: [{clientId, name, health}],
        //   currentPickerIndex: 0,
        //   currentPickerId: null,
        //   isActive: false
        // }
    }

    addPlayer(clientId, name) {
        if (this.players.size >= 4) return false;

        // Find the next available board index (0-3)
        const usedIndices = new Set();
        for (const player of this.players.values()) {
            usedIndices.add(player.boardIndex);
        }
        let boardIndex = 0;
        while (usedIndices.has(boardIndex) && boardIndex < 4) {
            boardIndex++;
        }

        const player = new PlayerState(clientId, name, boardIndex);
        this.players.set(clientId, player);
        return true;
    }

    removePlayer(clientId) {
        this.players.delete(clientId);

        // If host left, assign new host
        if (clientId === this.hostId && this.players.size > 0) {
            this.hostId = this.players.keys().next().value;
        }

        // If no players remain, clean up all timers
        if (this.players.size === 0) {
            this.incrementGeneration();
            this.cleanupTimers();
            return;
        }

        // If merchant round is active and this was the current picker, signal to skip their turn
        if (this.merchantState && this.merchantState.isActive &&
            this.merchantState.currentPickerId === clientId) {
            this.merchantState.needsSkip = true;
        }
    }

    /**
     * Clean up all timers and intervals to prevent leaks
     */
    cleanupTimers() {
        // Clear all setTimeout-based timers
        const timeoutFields = [
            'combatTimer', 'resultsTimer', 'resetTimer', 'advanceRoundTimer',
            'merchantTurnTimer', 'merchantSafetyTimer', 'majorCrestTimer'
        ];
        for (const field of timeoutFields) {
            if (this[field]) {
                clearTimeout(this[field]);
                this[field] = null;
            }
        }

        // Clear all setInterval-based timers
        const intervalFields = ['timerInterval', 'phaseUpdateInterval'];
        for (const field of intervalFields) {
            if (this[field]) {
                clearInterval(this[field]);
                this[field] = null;
            }
        }

        this.majorCrestStartTime = null;

        if (this.merchantState) {
            this.merchantState.isActive = false;
        }

        console.log(`[GameRoom] All timers cleaned up`);
    }

    /**
     * Increment the phase generation counter.
     * Returns the new generation value so callers can capture it in closures.
     */
    incrementGeneration() {
        this.phaseGeneration++;
        return this.phaseGeneration;
    }

    getPlayer(clientId) {
        return this.players.get(clientId);
    }

    getAllPlayers() {
        return Array.from(this.players.values());
    }

    getActivePlayers() {
        return this.getAllPlayers().filter(p => !p.isEliminated);
    }

    // ========== GAME FLOW ==========

    startGame() {
        console.log(`[GameRoom] startGame called. Players: ${this.players.size}`);

        if (this.players.size < 2) {
            console.log(`[GameRoom] Not enough players to start game`);
            return false;
        }

        this.state = 'playing';
        this.round = 1;
        this.unitPool.initialize();
        this.perGameVars = rollPerGameVariables();
        console.log(`[GameRoom] Unit pool initialized, per-game vars: attuned=${this.perGameVars.attunedElement}, blessed=${this.perGameVars.blessedBonus?.id}, warlord=${this.perGameVars.warlordEnhancement?.id}`);

        // Initialize all players
        for (const player of this.players.values()) {
            console.log(`[GameRoom] Initializing player: ${player.name} (boardIndex: ${player.boardIndex})`);
            player.gold = GameConstants.Economy.STARTING_GOLD;
            console.log(`[GameRoom] ${player.name} starting gold set to: ${player.gold}`);
            player.health = GameConstants.Player.STARTING_HEALTH;
            player.level = GameConstants.Player.STARTING_LEVEL;
            player.xp = 0;
            player.isReady = false;
            player.isEliminated = false;

            // Clear board and bench
            for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
                player.board[x] = new Array(GameConstants.Grid.HEIGHT).fill(null);
            }
            player.bench = new Array(GameConstants.Player.BENCH_SIZE).fill(null);

            // Give starting unit
            console.log(`[GameRoom] Giving starting unit to ${player.name}...`);
            this.giveStartingUnit(player);

            // Log board state after giving unit
            let boardUnitCount = 0;
            for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
                for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                    if (player.board[x][y]) boardUnitCount++;
                }
            }
            console.log(`[GameRoom] ${player.name} now has ${boardUnitCount} units on board`);

            // Generate initial shop
            this.refreshShop(player);
        }

        this.startPlanningPhase();
        console.log(`[GameRoom] Game started successfully`);
        return true;
    }

    giveStartingUnit(player) {
        console.log(`[GameRoom] giveStartingUnit called for ${player.name}`);

        const oneCostUnits = getUnitsByCost(1);
        console.log(`[GameRoom] Found ${oneCostUnits.length} 1-cost units: ${oneCostUnits.map(u => u.unitId).join(', ')}`);

        if (oneCostUnits.length === 0) {
            console.log(`[GameRoom] ERROR: No 1-cost units available for starting unit!`);
            return;
        }

        const template = oneCostUnits[Math.floor(Math.random() * oneCostUnits.length)];
        console.log(`[GameRoom] Selected template: ${template.unitId} - ${template.name}`);

        const unit = new UnitInstance(template, 1);
        console.log(`[GameRoom] Created unit instance: ${unit.instanceId} - ${unit.name} (${unit.unitId})`);

        // Place in center-front of board
        const centerX = Math.floor(GameConstants.Grid.WIDTH / 2);
        player.board[centerX][0] = unit;

        console.log(`[GameRoom] Placed unit at (${centerX}, 0). Board slot value: ${player.board[centerX][0] ? 'set' : 'null'}`);

        // Take from pool
        this.unitPool.takeUnit(template.unitId);
        console.log(`[GameRoom] Successfully gave starting unit ${unit.name} (${unit.unitId}) to ${player.name}`);
    }

    refreshShop(player) {
        if (player.shopLocked) return;

        // Return old shop units to pool
        for (const unit of player.shop) {
            if (unit) {
                this.unitPool.returnUnit(unit.unitId);
            }
        }

        // Generate new shop
        player.shop = [];
        for (let i = 0; i < GameConstants.Economy.SHOP_SIZE; i++) {
            const template = this.unitPool.rollUnit(player.level);
            if (template) {
                this.unitPool.takeUnit(template.unitId);
                player.shop.push({
                    unitId: template.unitId,
                    name: template.name,
                    cost: template.cost,
                    traits: template.traits,
                    stats: template.stats
                });
            } else {
                player.shop.push(null);
            }
        }
    }

    getCurrentRoundType() {
        const roundIndex = this.round - 1;
        if (roundIndex >= 0 && roundIndex < GameConstants.Rounds.ROUND_TYPES.length) {
            return GameConstants.Rounds.ROUND_TYPES[roundIndex];
        }
        return 'pvp';
    }

    startPlanningPhase() {
        this.incrementGeneration();
        this.phase = 'planning';

        // Stop any existing timer from previous phase
        this.stopPhaseTimer();

        // Use shorter timer for intro PvE round
        const roundType = this.getCurrentRoundType();
        if (roundType === 'pve_intro') {
            this.phaseTimer = GameConstants.Rounds.PVE_INTRO_PLANNING_DURATION;
        } else if (roundType === 'mad_merchant') {
            // Merchant rounds have shorter timer - 30 seconds for turn-based picking
            this.phaseTimer = 30;
        } else if (roundType === 'major_crest') {
            // Major crest rounds have a 15 second timer
            this.phaseTimer = 15;
        } else {
            this.phaseTimer = GameConstants.Rounds.PLANNING_DURATION;
        }

        console.log(`[GameRoom] Starting planning phase for round ${this.round} (${roundType}), timer: ${this.phaseTimer}s`);
        this.phaseStartTime = Date.now();

        // Major crest and mad merchant rounds don't use normal planning phase mechanics
        if (roundType === 'major_crest' || roundType === 'mad_merchant') {
            // Don't give income, passive XP, refresh shop, or start planning timer
            return;
        }

        // Check for pending merges (units bought during combat that can now merge with board units)
        for (const player of this.getActivePlayers()) {
            this.checkAllMerges(player);
        }

        // Give income and passive XP
        console.log(`[GameRoom] startPlanningPhase - giving income and XP to ${this.getActivePlayers().length} players`);
        for (const player of this.getActivePlayers()) {
            const income = player.calculateIncome();
            const oldGold = player.gold;
            player.gold += income;

            // Grant passive XP
            const freeXp = GameConstants.Leveling.FREE_XP_PER_ROUND;
            const oldXp = player.xp;
            const oldLevel = player.level;
            player.xp += freeXp;
            while (player.canLevelUp()) {
                player.level++;
            }
            if (player.level > oldLevel) {
                console.log(`[GameRoom] ${player.name}: LEVELED UP ${oldLevel} -> ${player.level} (xp: ${oldXp} + ${freeXp} = ${player.xp})`);
            }

            console.log(`[GameRoom] ${player.name}: gold ${oldGold} + ${income} = ${player.gold}, xp ${oldXp} + ${freeXp} = ${player.xp} (lv${player.level})`);
            player.isReady = false;

            // Refresh shop if not locked
            if (!player.shopLocked) {
                this.refreshShop(player);
            }
        }

        // Start timer
        this.startPhaseTimer();
    }

    startCombatPhase() {
        this.incrementGeneration();

        // Stop any lingering planning phase timer to prevent leaks
        this.stopPhaseTimer();

        this.phase = 'combat';
        this.phaseTimer = GameConstants.Rounds.COMBAT_MAX_DURATION;
        this.phaseStartTime = Date.now();

        // Check round type - PvE rounds are auto-wins in multiplayer for now
        const roundType = this.getCurrentRoundType();
        console.log(`[GameRoom] Starting combat phase for round ${this.round} (${roundType})`);

        if (roundType === 'pve_intro' || roundType === 'pve_loot' || roundType === 'pve_boss') {
            // PvE rounds: Generate enemies and run combat for each player
            const activePlayers = this.getActivePlayers();
            console.log(`[GameRoom] PvE round (${roundType}) - ${activePlayers.length} active players: ${activePlayers.map(p => `${p.name}(${p.health}hp)`).join(', ')}`);
            this.combatResults = [];
            this.combatEvents.clear();
            this.matchups = [];

            // Generate PvE enemy board
            const pveBoard = this.generatePvEBoard(roundType);
            const pveState = this.createPvEPlayerState(pveBoard);

            // Run combat for each player against the PvE enemies
            for (const player of activePlayers) {
                console.log(`[GameRoom] Processing PvE combat for ${player.name} (clientId: ${player.clientId})`);
                player.calculateTraits();

                // Count units on each board
                let playerUnitCount = 0;
                let pveUnitCount = 0;
                for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
                    for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                        if (player.board[x][y]) playerUnitCount++;
                        if (pveBoard[x][y]) pveUnitCount++;
                    }
                }
                console.log(`[GameRoom] PvE Combat: ${player.name} has ${playerUnitCount} units, PvE has ${pveUnitCount} units`);

                // Run combat simulation
                const simulator = new CombatSimulator(
                    player.board,
                    pveBoard,
                    player,
                    pveState,
                    { perGameVars: this.perGameVars || {} }
                );
                const result = simulator.run();
                console.log(`[GameRoom] PvE Combat result: ${result.events.length} events, winner=${result.winner}, duration=${result.durationTicks} ticks`);

                // Determine winner
                const playerWon = result.winner === 'player1';
                const winnerId = playerWon ? player.clientId : 'pve_enemy';
                const loserId = playerWon ? 'pve_enemy' : player.clientId;

                // Store combat result
                const combatResult = {
                    player1: player.clientId,
                    player2: 'pve_enemy',
                    winnerId,
                    loserId,
                    damage: result.damage,
                    durationTicks: result.durationTicks
                };
                this.combatResults.push(combatResult);

                // Store combat events for this player
                this.combatEvents.set(player.clientId, {
                    matchup: { player1: player.clientId, player2: 'pve_enemy' },
                    events: result.events,
                    myTeam: 'player1',
                    opponentTeam: 'player2'
                });
                console.log(`[GameRoom] Stored ${result.events.length} combat events for ${player.name} (clientId: ${player.clientId})`);

                // Extract loot drops from combat events and add to player's pending loot
                for (const event of result.events) {
                    if (event.type === CombatEventType.UNIT_DEATH) {
                        // Handle single loot drop (legacy format)
                        if (event.lootType && event.lootId) {
                            player.pendingLoot.push({
                                lootId: event.lootId,
                                lootType: event.lootType,
                                position: event.lootPosition || { x: 3, y: 2 }
                            });
                            console.log(`[GameRoom] Loot drop for ${player.name}: ${event.lootType} (id: ${event.lootId})`);
                        }
                        // Handle multiple loot drops (boss format)
                        if (event.lootDrops && event.lootDrops.length > 0) {
                            for (const drop of event.lootDrops) {
                                player.pendingLoot.push({
                                    lootId: drop.lootId,
                                    lootType: drop.lootType,
                                    position: drop.lootPosition || { x: 3, y: 2 }
                                });
                                console.log(`[GameRoom] Loot drop for ${player.name}: ${drop.lootType} (id: ${drop.lootId})`);
                            }
                        }
                    }
                }

                // Apply damage if player lost
                if (!playerWon) {
                    player.health -= result.damage;
                    if (player.health <= 0) {
                        player.health = 0;
                        player.isEliminated = true;
                    }
                    player.lossStreak++;
                    player.winStreak = 0;
                } else {
                    player.winStreak++;
                    player.lossStreak = 0;
                }

                console.log(`[GameRoom] ${player.name} vs PvE: ${playerWon ? 'WIN' : 'LOSS'}, damage=${result.damage}`);
            }

            console.log(`[GameRoom] PvE combat complete. combatResults: ${this.combatResults.length}, combatEvents stored for: ${[...this.combatEvents.keys()].join(', ')}`);

            // Set combat timer based on longest combat
            let maxDuration = 0;
            for (const result of this.combatResults) {
                maxDuration = Math.max(maxDuration, result.durationTicks || 0);
            }
            this.phaseTimer = Math.min(maxDuration / 10 + 2, GameConstants.Rounds.COMBAT_MAX_DURATION);
            return;
        }

        if (roundType === 'mad_merchant' || roundType === 'major_crest') {
            // Special rounds: Skip combat entirely, just do planning
            console.log(`[GameRoom] Special round (${roundType}) - skipping combat`);
            this.combatResults = [];
            this.combatEvents.clear();
            this.matchups = [];
            this.phaseTimer = 1; // Very short, go straight to results
            return;
        }

        // Generate matchups for PvP rounds
        this.generateMatchups();

        // Reset ready state
        for (const player of this.players.values()) {
            player.isReady = false;
        }

        // Run combat simulations for each matchup
        this.combatResults = [];
        this.combatEvents.clear();

        for (const matchup of this.matchups) {
            const player1 = this.getPlayer(matchup.player1);
            const player2 = this.getPlayer(matchup.player2);

            if (!player1 || !player2) continue;

            // Calculate traits for both players before combat
            player1.calculateTraits();
            player2.calculateTraits();

            // Debug: Log matchup details
            console.log(`[MATCHUP] Round ${this.round}: ${player1.name} (player1) vs ${player2.name} (player2)`);
            console.log(`[MATCHUP] Host: ${matchup.hostPlayerId === matchup.player1 ? player1.name : player2.name}`);

            // Log board positions for player1
            console.log(`[MATCHUP] ${player1.name}'s board:`);
            for (let y = 0; y < 4; y++) {
                for (let x = 0; x < 5; x++) {
                    const unit = player1.board[x][y];
                    if (unit) {
                        const stats = player1.getUnitCombatStats(unit);
                        console.log(`[MATCHUP]   ${unit.name} at (${x},${y}) - HP:${stats.health} ATK:${stats.attack} ASPD:${stats.attackSpeed} RNG:${stats.range}`);
                    }
                }
            }

            // Log board positions for player2
            console.log(`[MATCHUP] ${player2.name}'s board:`);
            for (let y = 0; y < 4; y++) {
                for (let x = 0; x < 5; x++) {
                    const unit = player2.board[x][y];
                    if (unit) {
                        const stats = player2.getUnitCombatStats(unit);
                        console.log(`[MATCHUP]   ${unit.name} at (${x},${y}) - HP:${stats.health} ATK:${stats.attack} ASPD:${stats.attackSpeed} RNG:${stats.range}`);
                    }
                }
            }

            // Run combat simulation
            const simulator = new CombatSimulator(
                player1.board,
                player2.board,
                player1,
                player2,
                { perGameVars: this.perGameVars || {} }
            );
            const result = simulator.run();

            // Debug: Log result
            console.log(`[MATCHUP] Result: ${result.winner} wins in ${result.durationTicks} ticks`);

            // Map winner back to actual player IDs
            const winnerId = result.winner === 'player1' ? matchup.player1 : matchup.player2;
            const loserId = result.winner === 'player1' ? matchup.player2 : matchup.player1;

            // Store combat result
            const combatResult = {
                player1: matchup.player1,
                player2: matchup.player2,
                winnerId,
                loserId,
                damage: result.damage,
                durationTicks: result.durationTicks
            };
            this.combatResults.push(combatResult);

            // Store combat events for both players (with correct perspective)
            this.combatEvents.set(matchup.player1, {
                matchup,
                events: result.events,
                myTeam: 'player1',
                opponentTeam: 'player2'
            });
            this.combatEvents.set(matchup.player2, {
                matchup,
                events: result.events,
                myTeam: 'player2',
                opponentTeam: 'player1'
            });

            // Apply damage to loser (unless ghost match)
            if (!matchup.isGhost) {
                const loser = this.getPlayer(loserId);
                if (loser) {
                    loser.health -= result.damage;
                    if (loser.health <= 0) {
                        loser.health = 0;
                        loser.isEliminated = true;
                    }
                }
            }

            // Update win/loss streaks
            const winner = this.getPlayer(winnerId);
            const loserPlayer = this.getPlayer(loserId);

            if (winner && !matchup.isGhost) {
                winner.winStreak++;
                winner.lossStreak = 0;
            }
            if (loserPlayer && !matchup.isGhost) {
                loserPlayer.lossStreak++;
                loserPlayer.winStreak = 0;
            }
        }

        // Calculate maximum combat duration for timer
        let maxDuration = 0;
        for (const result of this.combatResults) {
            maxDuration = Math.max(maxDuration, result.durationTicks);
        }
        // Convert ticks to seconds (10 ticks per second)
        this.phaseTimer = Math.min(maxDuration / 10 + 2, GameConstants.Rounds.COMBAT_MAX_DURATION);
    }

    getCombatEventsForPlayer(clientId) {
        return this.combatEvents.get(clientId) || null;
    }

    /**
     * Get all combat events for all players (for scouting/spectating)
     * Returns an array of combat event entries (Unity JsonUtility compatible format)
     */
    getAllCombatEvents() {
        const allEvents = [];
        for (const [playerId, eventData] of this.combatEvents) {
            allEvents.push({
                playerId: playerId,
                events: eventData.events,
                hostPlayerId: eventData.matchup?.hostPlayerId || eventData.matchup?.player1,
                awayPlayerId: eventData.matchup?.player2 !== eventData.matchup?.hostPlayerId
                    ? eventData.matchup?.player2
                    : eventData.matchup?.player1
            });
        }
        return allEvents;
    }

    generateMatchups() {
        const active = this.getActivePlayers();
        this.matchups = [];

        // Helper to get alternating host for a pair of players
        // Returns the host player ID
        const getAlternatingHost = (playerA, playerB) => {
            // Create a consistent key for this pair (sorted to be order-independent)
            const pairKey = [playerA, playerB].sort().join('-');
            const lastHost = this.lastHostByPair.get(pairKey);

            // Alternate: if last host was playerA, now it's playerB, and vice versa
            let newHost;
            if (lastHost === playerA) {
                newHost = playerB;
            } else if (lastHost === playerB) {
                newHost = playerA;
            } else {
                // First time this pair fights - randomly pick
                newHost = Math.random() < 0.5 ? playerA : playerB;
            }

            // Store for next time
            this.lastHostByPair.set(pairKey, newHost);
            return newHost;
        };

        // Helper to create matchup with host as player1 (so host units are at rows 0-3)
        const createMatchup = (playerA, playerB, hostId, isGhost = false) => {
            // Host should always be player1 so their units are on the near side (rows 0-3)
            // Away player is player2 with units on far side (rows 4-7)
            if (hostId === playerA) {
                return { player1: playerA, player2: playerB, hostPlayerId: hostId, isGhost };
            } else {
                return { player1: playerB, player2: playerA, hostPlayerId: hostId, isGhost };
            }
        };

        if (active.length === 2) {
            // 2 players: fight each other with alternating host
            const hostId = getAlternatingHost(active[0].clientId, active[1].clientId);
            this.matchups.push(createMatchup(active[0].clientId, active[1].clientId, hostId));
        } else if (active.length === 3) {
            // 3 players: round robin, one fights ghost
            const host1 = getAlternatingHost(active[0].clientId, active[1].clientId);
            this.matchups.push(createMatchup(active[0].clientId, active[1].clientId, host1));
            // Ghost match - always on the player who's fighting the ghost
            this.matchups.push(createMatchup(active[2].clientId, active[0].clientId, active[2].clientId, true));
        } else if (active.length === 4) {
            // 4 players: two matches
            // Shuffle and pair
            const shuffled = [...active].sort(() => Math.random() - 0.5);
            const host1 = getAlternatingHost(shuffled[0].clientId, shuffled[1].clientId);
            const host2 = getAlternatingHost(shuffled[2].clientId, shuffled[3].clientId);
            this.matchups.push(createMatchup(shuffled[0].clientId, shuffled[1].clientId, host1));
            this.matchups.push(createMatchup(shuffled[2].clientId, shuffled[3].clientId, host2));
        }
    }

    // ========== PVE ENEMY GENERATION ==========

    generatePvEBoard(roundType) {
        // Create empty board
        const board = [];
        for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
            board[x] = new Array(GameConstants.Grid.HEIGHT).fill(null);
        }

        // Get 1-cost units for PvE enemies
        const oneCostUnits = getUnitsByCost(1);
        if (oneCostUnits.length === 0) {
            console.log('[GameRoom] No 1-cost units available for PvE!');
            return board;
        }

        if (roundType === 'pve_intro') {
            // Intro wave: Use Stingray and Cactus PvE enemies
            const stingrayTemplate = UnitTemplates['stingray'];
            const cactusTemplate = UnitTemplates['cactus'];

            if (!stingrayTemplate || !cactusTemplate) {
                console.log('[GameRoom] PvE units not found, falling back to random 1-cost');
                const unit1Template = oneCostUnits[Math.floor(Math.random() * oneCostUnits.length)];
                const unit2Template = oneCostUnits[Math.floor(Math.random() * oneCostUnits.length)];
                const unit1 = new UnitInstance(unit1Template, 1);
                unit1.lootType = LootType.CrestToken;
                const unit2 = new UnitInstance(unit2Template, 1);
                unit2.lootType = LootType.ItemAnvil;
                board[2][2] = unit1;
                board[4][2] = unit2;
            } else {
                // Create PvE units with their template stats
                const unit1 = new UnitInstance(stingrayTemplate, 1);
                unit1.name = 'Stingray';
                unit1.lootType = LootType.CrestToken; // Drops crest token

                const unit2 = new UnitInstance(cactusTemplate, 1);
                unit2.name = 'Cactus';
                unit2.lootType = LootType.ItemAnvil; // Drops item anvil

                board[2][2] = unit1;
                board[4][2] = unit2;

                console.log(`[GameRoom] Generated PvE Intro: Stingray at (2,2) with CrestToken, Cactus at (4,2) with ItemAnvil`);
            }
        } else if (roundType === 'pve_loot') {
            // Loot wave: 2 cactus + 1 stingray with specific loot drops
            const stingrayTemplate = UnitTemplates['stingray'];
            const cactusTemplate = UnitTemplates['cactus'];

            if (!stingrayTemplate || !cactusTemplate) {
                console.log('[GameRoom] PvE units not found for loot round, using fallback');
                // Fallback to random units
                for (let i = 0; i < 3; i++) {
                    const template = oneCostUnits[Math.floor(Math.random() * oneCostUnits.length)];
                    const unit = new UnitInstance(template, 1);
                    unit.currentStats.health = 300;
                    unit.currentStats.attack = 40;
                    unit.currentHealth = 300;
                    if (i === 0) unit.lootType = LootType.ItemAnvil;
                    else if (i === 1) unit.lootType = LootType.CrestToken;
                    else unit.lootType = LootType.MixedLoot;
                    board[1 + i * 2][2] = unit;
                }
            } else {
                // Cactus 1 - drops Item Anvil
                const cactus1 = new UnitInstance(cactusTemplate, 1);
                cactus1.name = 'Cactus';
                cactus1.currentStats.health = 300;
                cactus1.currentStats.attack = 40;
                cactus1.currentHealth = 300;
                cactus1.lootType = LootType.ItemAnvil;
                board[1][2] = cactus1;

                // Cactus 2 - drops Crest Token
                const cactus2 = new UnitInstance(cactusTemplate, 1);
                cactus2.name = 'Cactus';
                cactus2.currentStats.health = 300;
                cactus2.currentStats.attack = 40;
                cactus2.currentHealth = 300;
                cactus2.lootType = LootType.CrestToken;
                board[3][2] = cactus2;

                // Stingray - drops Loot Choice (4 gold or random 4-cost unit)
                const stingray = new UnitInstance(stingrayTemplate, 1);
                stingray.name = 'Stingray';
                stingray.currentStats.health = 400;
                stingray.currentStats.attack = 50;
                stingray.currentHealth = 400;
                stingray.lootType = LootType.MixedLoot;
                board[2][3] = stingray;

                console.log(`[GameRoom] Generated PvE Loot: 2 Cactus (ItemAnvil, CrestToken) + 1 Stingray (MixedLoot)`);
            }
        } else if (roundType === 'pve_boss') {
            // Boss wave: Single boss that drops 3 loot orbs
            const stingrayTemplate = UnitTemplates['stingray'];
            const bossTemplate = stingrayTemplate || oneCostUnits[Math.floor(Math.random() * oneCostUnits.length)];

            const boss = new UnitInstance(bossTemplate, 2); // 2-star boss (not too hard)
            boss.name = 'Boss';
            // Buff stats slightly for a boss feel
            boss.currentStats.health = 600;
            boss.currentStats.attack = 60;
            boss.currentHealth = 600;
            // Boss drops 3 loot orbs: ItemAnvil, CrestToken, and LargeMixedLoot
            boss.lootTypes = [LootType.ItemAnvil, LootType.CrestToken, LootType.LargeMixedLoot];
            board[3][2] = boss;

            console.log(`[GameRoom] Generated PvE Boss: 1 boss (drops ItemAnvil, CrestToken, LargeMixedLoot)`);
        }

        return board;
    }

    createPvEPlayerState(board) {
        // Create a minimal player state for PvE enemies
        return {
            clientId: 'pve_enemy',
            name: 'PvE Enemies',
            board: board,
            activeTraits: {},
            getUnitCombatStats: (unit) => {
                // PvE units use their current stats directly
                return { ...unit.currentStats };
            },
            calculateTraits: () => {}
        };
    }

    startResultsPhase() {
        this.incrementGeneration();
        this.phase = 'results';
        this.phaseTimer = GameConstants.Rounds.RESULTS_DURATION;
        this.phaseStartTime = Date.now();
    }

    advanceRound() {
        this.incrementGeneration();
        this.round++;

        // Check for game end - only when 1 or fewer players remain
        const active = this.getActivePlayers();
        if (active.length <= 1) {
            this.endGame(active[0]?.clientId);
            return;
        }

        // Game continues with PvP rounds beyond the scheduled rounds until someone wins
        // (getCurrentRoundType() returns 'pvp' for rounds beyond the schedule)

        this.startPlanningPhase();
    }

    endGame(winnerId) {
        this.incrementGeneration();
        this.state = 'finished';
        this.phase = 'gameOver';
        this.cleanupTimers();
    }

    startPhaseTimer() {
        this.stopPhaseTimer();

        this.timerInterval = setInterval(() => {
            // Don't update timer for major_crest rounds (timer managed in server.js)
            const roundType = this.getCurrentRoundType();
            if (roundType === 'major_crest') {
                return;
            }

            const elapsed = (Date.now() - this.phaseStartTime) / 1000;
            this.phaseTimer = Math.max(0, this.getPhaseMaxTime() - elapsed);

            if (this.phaseTimer <= 0) {
                this.onPhaseTimerEnd();
            }
        }, 100);
    }

    stopPhaseTimer() {
        if (this.timerInterval) {
            clearInterval(this.timerInterval);
            this.timerInterval = null;
        }
    }

    getPhaseMaxTime() {
        switch (this.phase) {
            case 'planning':
                const roundType = this.getCurrentRoundType();
                if (roundType === 'pve_intro') {
                    return GameConstants.Rounds.PVE_INTRO_PLANNING_DURATION;
                }
                if (roundType === 'major_crest') {
                    return 20; // 20 second timer for major crest selection
                }
                return GameConstants.Rounds.PLANNING_DURATION;
            case 'combat': return GameConstants.Rounds.COMBAT_MAX_DURATION;
            case 'results': return GameConstants.Rounds.RESULTS_DURATION;
            default: return 30;
        }
    }

    onPhaseTimerEnd() {
        console.log(`[GameRoom] Phase timer ended for phase: ${this.phase}`);
        const roundType = this.getCurrentRoundType();

        switch (this.phase) {
            case 'planning':
                // Mad Merchant rounds don't advance on timer - they wait for all picks
                if (roundType === 'mad_merchant') {
                    console.log(`[GameRoom] Mad Merchant round - stopping leaked planning timer`);
                    this.stopPhaseTimer();
                    return;
                }

                // Call the callback to let server.js handle combat start
                if (this.onCombatStartCallback) {
                    this.onCombatStartCallback(this);
                } else {
                    // Fallback if no callback registered
                    this.startCombatPhase();
                }
                break;
            case 'combat':
                // Force end combat
                if (this.onCombatEndCallback) {
                    this.onCombatEndCallback(this);
                } else {
                    this.startResultsPhase();
                }
                break;
            case 'results':
                if (this.onResultsEndCallback) {
                    this.onResultsEndCallback(this);
                } else {
                    this.advanceRound();
                }
                break;
        }
    }

    // Callback setters for server.js to hook into phase changes
    setOnCombatStart(callback) {
        this.onCombatStartCallback = callback;
    }

    setOnCombatEnd(callback) {
        this.onCombatEndCallback = callback;
    }

    setOnResultsEnd(callback) {
        this.onResultsEndCallback = callback;
    }

    setOnMerchantStart(callback) {
        this.onMerchantStartCallback = callback;
    }

    setOnMerchantPick(callback) {
        this.onMerchantPickCallback = callback;
    }

    setOnMerchantTurnUpdate(callback) {
        this.onMerchantTurnUpdateCallback = callback;
    }

    setOnMerchantEnd(callback) {
        this.onMerchantEndCallback = callback;
    }

    // ========== PLAYER ACTIONS ==========

    handleAction(clientId, action) {
        console.log(`[GameRoom] handleAction from ${clientId}: type=${action?.type}, phase=${this.phase}`);
        const player = this.getPlayer(clientId);
        if (!player || player.isEliminated) {
            return { success: false, error: 'Invalid player' };
        }

        // Actions that are always allowed (any phase)
        switch (action.type) {
            case 'moveBenchUnit':
                return this.moveBenchUnit(player, action.instanceId, action.targetSlot);
            case 'buyUnit':
                return this.buyUnit(player, action.shopIndex);
            case 'reroll':
                return this.reroll(player);
            case 'buyXP':
                return this.buyXP(player);
            case 'collectLoot':
                return this.collectLoot(player, action.lootId);
            // Item and consumable actions - allowed in any phase
            case 'equipItem':
                return this.equipItem(player, action.itemIndex, action.instanceId);
            case 'unequipItem':
                return this.unequipItem(player, action.instanceId, action.itemSlot);
            case 'combineItems':
                return this.combineItems(player, action.itemIndex1, action.itemIndex2);
            case 'useConsumable':
                return this.useConsumable(player, action.itemIndex);
            case 'selectCrestChoice':
                return this.selectCrestChoice(player, action.choiceIndex);
            case 'selectItemChoice':
                return this.selectItemChoice(player, action.choiceIndex);
            case 'replaceCrest':
                return this.replaceCrest(player, action.replaceIndex);
            case 'merchantPick':
                // Merchant pick is allowed during the merchant round
                if (this.getCurrentRoundType() !== 'mad_merchant') {
                    return { success: false, error: 'Not a merchant round' };
                }
                return this.handleMerchantPick(clientId, action.optionId);
        }

        // Sell unit - bench units can be sold any time, board units only during planning
        if (action.type === 'sellUnit') {
            return this.sellUnit(player, action.instanceId, this.phase !== 'planning');
        }

        // All other actions require planning phase
        if (this.phase !== 'planning') {
            return { success: false, error: 'Not in planning phase' };
        }

        switch (action.type) {
            case 'placeUnit':
                return this.placeUnit(player, action.instanceId, action.x, action.y);
            case 'benchUnit':
                return this.benchUnit(player, action.instanceId, action.targetSlot);
            case 'toggleShopLock':
                return this.toggleShopLock(player);
            case 'ready':
                return this.setReady(player, action.ready);
            case 'selectMinorCrest':
                return this.selectMinorCrest(player, action.crestId, action.instanceId);
            case 'selectMajorCrest':
                // Use handleMajorCrestSelect during major_crest rounds for tracking
                if (this.getCurrentRoundType() === 'major_crest' && this.majorCrestState?.isActive) {
                    return this.handleMajorCrestSelect(player.clientId, action.crestId);
                }
                return this.selectMajorCrest(player, action.crestId);
            default:
                return { success: false, error: 'Unknown action' };
        }
    }

    buyUnit(player, shopIndex) {
        if (shopIndex < 0 || shopIndex >= player.shop.length) {
            return { success: false, error: 'Invalid shop index' };
        }

        const shopUnit = player.shop[shopIndex];
        if (!shopUnit) {
            return { success: false, error: 'No unit in that slot' };
        }

        if (player.gold < shopUnit.cost) {
            return { success: false, error: 'Not enough gold' };
        }

        const benchSlot = player.findFirstEmptyBenchSlot();
        if (benchSlot === -1) {
            return { success: false, error: 'Bench is full' };
        }

        // Create unit instance
        const template = UnitTemplates[shopUnit.unitId];
        if (!template) {
            return { success: false, error: 'Unit not found' };
        }

        const unit = new UnitInstance(template, 1);

        // Deduct gold
        player.gold -= shopUnit.cost;

        // Add to bench
        player.bench[benchSlot] = unit;

        // Remove from shop
        player.shop[shopIndex] = null;

        // Check for merge
        const mergeResult = this.checkAndMerge(player, unit);

        return {
            success: true,
            unit: unit.toJSON(),
            benchSlot,
            merged: mergeResult.merged,
            mergedUnit: mergeResult.mergedUnit
        };
    }

    sellUnit(player, instanceId, benchOnly = false) {
        // Find unit on board or bench
        let unit = null;
        let location = null;

        // Check board (skip if benchOnly - can only sell bench units during combat/results)
        if (!benchOnly) {
            for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
                for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                    if (player.board[x][y]?.instanceId === instanceId) {
                        unit = player.board[x][y];
                        location = { type: 'board', x, y };
                        break;
                    }
                }
                if (unit) break;
            }
        }

        // Check bench
        if (!unit) {
            for (let i = 0; i < player.bench.length; i++) {
                if (player.bench[i]?.instanceId === instanceId) {
                    unit = player.bench[i];
                    location = { type: 'bench', slot: i };
                    break;
                }
            }
        }

        if (!unit) {
            if (benchOnly) {
                return { success: false, error: 'Can only sell bench units during combat' };
            }
            return { success: false, error: 'Unit not found' };
        }

        // Unequip items and return to inventory
        const returnedItems = [];
        const droppedItems = [];
        if (unit.items && unit.items.length > 0) {
            for (const item of unit.items) {
                if (player.itemInventory.length < 10) {
                    player.itemInventory.push(item);
                    returnedItems.push(item);
                    console.log(`[GameRoom] ${player.name} recovered ${item.name} from sold unit`);
                } else {
                    // Inventory full - item is lost
                    droppedItems.push(item);
                    console.log(`[GameRoom] ${player.name} lost ${item.name} (inventory full)`);
                }
            }
        }

        // Calculate sell value: 1-star=cost, 2-star=2*cost-1, 3-star=4*cost-1
        const sellValue = GameConstants.Selling.getSellPrice(unit.cost, unit.starLevel);
        player.gold += sellValue;

        // Remove unit
        if (location.type === 'board') {
            player.board[location.x][location.y] = null;
        } else {
            player.bench[location.slot] = null;
        }

        // Return to pool: 1 for 1-star, 2 for 2-star, 4 for 3-star (2-copy merge)
        const unitsToReturn = Math.pow(2, unit.starLevel - 1);
        this.unitPool.returnUnit(unit.unitId, unitsToReturn);

        return {
            success: true,
            goldGained: sellValue,
            returnedItems: returnedItems,
            droppedItems: droppedItems
        };
    }

    placeUnit(player, instanceId, x, y) {
        if (x < 0 || x >= GameConstants.Grid.WIDTH || y < 0 || y >= GameConstants.Grid.HEIGHT) {
            return { success: false, error: 'Invalid position' };
        }

        // Find unit
        let unit = null;
        let fromBench = -1;
        let fromBoard = null;

        for (let i = 0; i < player.bench.length; i++) {
            if (player.bench[i]?.instanceId === instanceId) {
                unit = player.bench[i];
                fromBench = i;
                break;
            }
        }

        if (!unit) {
            for (let bx = 0; bx < GameConstants.Grid.WIDTH; bx++) {
                for (let by = 0; by < GameConstants.Grid.HEIGHT; by++) {
                    if (player.board[bx][by]?.instanceId === instanceId) {
                        unit = player.board[bx][by];
                        fromBoard = { x: bx, y: by };
                        break;
                    }
                }
                if (unit) break;
            }
        }

        if (!unit) {
            return { success: false, error: 'Unit not found' };
        }

        // Check if placing from bench and board is full
        if (fromBench >= 0 && !player.board[x][y]) {
            if (player.getBoardUnitCount() >= player.getMaxUnits()) {
                return { success: false, error: 'Board is full' };
            }
        }

        // Handle swap if target has a unit
        const targetUnit = player.board[x][y];

        // Remove from source
        if (fromBench >= 0) {
            player.bench[fromBench] = null;
        } else if (fromBoard) {
            player.board[fromBoard.x][fromBoard.y] = null;
        }

        // Place unit
        player.board[x][y] = unit;

        // Handle swapped unit
        if (targetUnit) {
            if (fromBench >= 0) {
                player.bench[fromBench] = targetUnit;
            } else if (fromBoard) {
                player.board[fromBoard.x][fromBoard.y] = targetUnit;
            }
        }

        return { success: true, swapped: !!targetUnit };
    }

    benchUnit(player, instanceId, targetSlot = -1) {
        // Find unit on board
        let unit = null;
        let fromBoard = null;

        for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
            for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                if (player.board[x][y]?.instanceId === instanceId) {
                    unit = player.board[x][y];
                    fromBoard = { x, y };
                    break;
                }
            }
            if (unit) break;
        }

        if (!unit) {
            return { success: false, error: 'Unit not found on board' };
        }

        // If targetSlot specified, use it (allows swapping with bench unit)
        let benchSlot = targetSlot;
        if (benchSlot < 0 || benchSlot >= player.bench.length) {
            // Find first empty bench slot
            benchSlot = player.findFirstEmptyBenchSlot();
            if (benchSlot === -1) {
                return { success: false, error: 'Bench is full' };
            }
        }

        // Check if there's a unit in the target bench slot (swap case)
        const benchUnit = player.bench[benchSlot];

        // Move board unit to bench
        player.board[fromBoard.x][fromBoard.y] = benchUnit; // null if empty, or swapped unit
        player.bench[benchSlot] = unit;

        return { success: true, benchSlot, swapped: !!benchUnit };
    }

    moveBenchUnit(player, instanceId, targetSlot) {
        // Find unit on bench
        let unit = null;
        let fromSlot = -1;

        for (let i = 0; i < player.bench.length; i++) {
            if (player.bench[i]?.instanceId === instanceId) {
                unit = player.bench[i];
                fromSlot = i;
                break;
            }
        }

        if (!unit) {
            return { success: false, error: 'Unit not found on bench' };
        }

        if (targetSlot < 0 || targetSlot >= player.bench.length) {
            return { success: false, error: 'Invalid target slot' };
        }

        if (targetSlot === fromSlot) {
            return { success: true }; // No-op, same slot
        }

        // Swap or move
        const targetUnit = player.bench[targetSlot];
        player.bench[targetSlot] = unit;
        player.bench[fromSlot] = targetUnit; // null if empty, or the swapped unit

        return { success: true, swapped: !!targetUnit };
    }

    reroll(player) {
        // Use free rerolls first
        if (player.freeRerolls > 0) {
            player.freeRerolls--;
            player.shopLocked = false;
            this.refreshShop(player);
            console.log(`[GameRoom] ${player.name} used free reroll (${player.freeRerolls} remaining)`);
            return { success: true, shop: player.shop, freeRerolls: player.freeRerolls, usedFreeReroll: true };
        }

        // Otherwise spend gold
        if (player.gold < GameConstants.Economy.REROLL_COST) {
            return { success: false, error: 'Not enough gold' };
        }

        player.gold -= GameConstants.Economy.REROLL_COST;
        player.shopLocked = false;
        this.refreshShop(player);

        return { success: true, shop: player.shop, freeRerolls: player.freeRerolls };
    }

    /**
     * Grant free rerolls to a player (from rewards like mixed loot or merchant)
     */
    grantFreeRerolls(player, count) {
        player.freeRerolls += count;
        console.log(`[GameRoom] ${player.name} gained ${count} free rerolls (total: ${player.freeRerolls})`);
        return player.freeRerolls;
    }

    buyXP(player) {
        if (player.gold < GameConstants.Economy.XP_COST) {
            return { success: false, error: 'Not enough gold' };
        }

        if (player.level >= GameConstants.Player.MAX_LEVEL) {
            return { success: false, error: 'Already max level' };
        }

        player.gold -= GameConstants.Economy.XP_COST;
        player.xp += GameConstants.Economy.XP_PER_PURCHASE;

        // Check for level up
        let leveled = false;
        while (player.canLevelUp()) {
            player.level++;
            leveled = true;
        }

        return { success: true, xp: player.xp, level: player.level, leveled };
    }

    collectLoot(player, lootId) {
        // Find the loot in player's pending loot
        const lootIndex = player.pendingLoot.findIndex(l => l.lootId === lootId);
        if (lootIndex === -1) {
            return { success: false, error: 'Loot not found' };
        }

        const loot = player.pendingLoot[lootIndex];

        // Check if player has room in inventory (max 10 items)
        if (player.itemInventory.length >= 10) {
            return { success: false, error: 'Inventory full' };
        }

        // Create consumable item based on loot type
        let item = null;
        if (loot.lootType === LootType.CrestToken) {
            item = {
                itemId: 'crest_token',
                itemName: 'Crest Token',
                description: 'Use to select a Minor Crest for your team.',
                rarity: 'rare',
                isConsumable: true,
                effect: 'consumable_crest_token'
            };
        } else if (loot.lootType === LootType.ItemAnvil) {
            item = {
                itemId: 'item_anvil',
                itemName: 'Item Anvil',
                description: 'Use to forge a new item for your units.',
                rarity: 'rare',
                isConsumable: true,
                effect: 'consumable_item_anvil'
            };
        } else if (loot.lootType === LootType.MixedLoot) {
            // Random loot orb - 33% gold, 33% unit, 33% free rerolls
            const roll = Math.random();

            if (roll < 0.33) {
                // Give 4 gold
                player.gold += 4;
                player.pendingLoot.splice(lootIndex, 1);
                console.log(`[GameRoom] ${player.name} collected random loot: 4 gold`);
                return {
                    success: true,
                    lootId,
                    rewardType: 'gold',
                    goldAmount: 4
                };
            } else if (roll < 0.66) {
                // Give a random 4-cost unit
                const fourCostUnits = getUnitsByCost(4);
                if (fourCostUnits.length === 0) {
                    // Fallback to gold if no 4-cost units available
                    player.gold += 4;
                    player.pendingLoot.splice(lootIndex, 1);
                    console.log(`[GameRoom] ${player.name} collected random loot: 4 gold (no 4-cost units available)`);
                    return {
                        success: true,
                        lootId,
                        rewardType: 'gold',
                        goldAmount: 4
                    };
                }

                const randomUnit = fourCostUnits[Math.floor(Math.random() * fourCostUnits.length)];

                // Find empty bench slot
                const emptySlot = player.bench.findIndex(u => u === null);
                if (emptySlot === -1) {
                    // Bench full - give gold instead
                    player.gold += 4;
                    player.pendingLoot.splice(lootIndex, 1);
                    console.log(`[GameRoom] ${player.name} collected random loot: 4 gold (bench full)`);
                    return {
                        success: true,
                        lootId,
                        rewardType: 'gold',
                        goldAmount: 4
                    };
                }

                // Create unit and add to bench
                const newUnit = new UnitInstance(randomUnit, 1);
                player.bench[emptySlot] = newUnit;

                // Check for merges
                this.checkAllMerges(player);

                player.pendingLoot.splice(lootIndex, 1);
                console.log(`[GameRoom] ${player.name} collected random loot: ${randomUnit.name} (4-cost unit)`);
                return {
                    success: true,
                    lootId,
                    rewardType: 'unit',
                    unitId: randomUnit.unitId,
                    unitName: randomUnit.name,
                    benchSlot: emptySlot
                };
            } else {
                // Give 2 free rerolls
                this.grantFreeRerolls(player, 2);
                player.pendingLoot.splice(lootIndex, 1);
                console.log(`[GameRoom] ${player.name} collected random loot: 2 free rerolls`);
                return {
                    success: true,
                    lootId,
                    rewardType: 'rerolls',
                    rerollCount: 2
                };
            }
        } else if (loot.lootType === LootType.LargeMixedLoot) {
            // Boss loot orb - 50/50 chance of more gold (6) or a 5-cost unit
            const isGold = Math.random() < 0.5;

            if (isGold) {
                // Give 6 gold (more than regular mixed loot)
                player.gold += 6;
                player.pendingLoot.splice(lootIndex, 1);
                console.log(`[GameRoom] ${player.name} collected boss loot: 6 gold`);
                return {
                    success: true,
                    lootId,
                    rewardType: 'gold',
                    goldAmount: 6
                };
            } else {
                // Give a random 5-cost unit (better than regular mixed loot)
                const fiveCostUnits = getUnitsByCost(5);
                if (fiveCostUnits.length === 0) {
                    // Fallback to gold if no 5-cost units available
                    player.gold += 6;
                    player.pendingLoot.splice(lootIndex, 1);
                    console.log(`[GameRoom] ${player.name} collected boss loot: 6 gold (no 5-cost units available)`);
                    return {
                        success: true,
                        lootId,
                        rewardType: 'gold',
                        goldAmount: 6
                    };
                }

                const randomUnit = fiveCostUnits[Math.floor(Math.random() * fiveCostUnits.length)];

                // Find empty bench slot
                const emptySlot = player.bench.findIndex(u => u === null);
                if (emptySlot === -1) {
                    // Bench full - give gold instead
                    player.gold += 6;
                    player.pendingLoot.splice(lootIndex, 1);
                    console.log(`[GameRoom] ${player.name} collected boss loot: 6 gold (bench full)`);
                    return {
                        success: true,
                        lootId,
                        rewardType: 'gold',
                        goldAmount: 6
                    };
                }

                // Create unit and add to bench
                const newUnit = new UnitInstance(randomUnit, 1);
                player.bench[emptySlot] = newUnit;

                // Check for merges
                this.checkAllMerges(player);

                player.pendingLoot.splice(lootIndex, 1);
                console.log(`[GameRoom] ${player.name} collected boss loot: ${randomUnit.name} (5-cost unit)`);
                return {
                    success: true,
                    lootId,
                    rewardType: 'unit',
                    unitId: randomUnit.unitId,
                    unitName: randomUnit.name,
                    benchSlot: emptySlot
                };
            }
        }

        if (!item) {
            return { success: false, error: 'Unknown loot type' };
        }

        // Add to inventory and remove from pending loot
        player.itemInventory.push(item);
        player.pendingLoot.splice(lootIndex, 1);

        console.log(`[GameRoom] ${player.name} collected ${item.itemName}`);

        return { success: true, item, lootId };
    }

    toggleShopLock(player) {
        player.shopLocked = !player.shopLocked;
        return { success: true, locked: player.shopLocked };
    }

    setReady(player, ready) {
        player.isReady = ready;

        // Check if all players ready
        const allReady = this.getActivePlayers().every(p => p.isReady);

        return { success: true, allReady };
    }

    checkAndMerge(player, newUnit) {
        // Find matching units (same unitId and star level)
        const matches = [];

        // Check bench
        for (let i = 0; i < player.bench.length; i++) {
            const u = player.bench[i];
            if (u && u !== newUnit && u.unitId === newUnit.unitId && u.starLevel === newUnit.starLevel) {
                matches.push({ unit: u, location: { type: 'bench', slot: i } });
            }
        }

        // Check board - only during planning phase
        // During combat/results, board units are locked and shouldn't merge
        if (this.phase === 'planning') {
            for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
                for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                    const u = player.board[x][y];
                    if (u && u !== newUnit && u.unitId === newUnit.unitId && u.starLevel === newUnit.starLevel) {
                        matches.push({ unit: u, location: { type: 'board', x, y } });
                    }
                }
            }
        }

        if (matches.length >= 1 && newUnit.starLevel < GameConstants.Units.MAX_STAR_LEVEL) {
            // Merge! Prioritize keeping the unit that's on the board

            // Check if newUnit is on board
            let newUnitOnBoard = false;
            let newUnitBoardPos = null;
            for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
                for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                    if (player.board[x][y] === newUnit) {
                        newUnitOnBoard = true;
                        newUnitBoardPos = { x, y };
                        break;
                    }
                }
                if (newUnitOnBoard) break;
            }

            // Find if any match is on board
            const boardMatch = matches.find(m => m.location.type === 'board');

            // Decide which unit to keep: prioritize board units
            let keepUnit, removeUnit;
            if (boardMatch) {
                // Keep the board match, remove newUnit
                keepUnit = boardMatch.unit;
                removeUnit = newUnit;
            } else if (newUnitOnBoard) {
                // Keep newUnit (on board), remove the bench match
                keepUnit = newUnit;
                removeUnit = matches[0].unit;
                // Remove the match from its location
                if (matches[0].location.type === 'bench') {
                    player.bench[matches[0].location.slot] = null;
                }
            } else {
                // Both on bench, keep first match
                keepUnit = matches[0].unit;
                removeUnit = newUnit;
            }

            // Remove the unit that's not being kept
            for (let i = 0; i < player.bench.length; i++) {
                if (player.bench[i] === removeUnit) {
                    player.bench[i] = null;
                    break;
                }
            }
            for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
                for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                    if (player.board[x][y] === removeUnit) {
                        player.board[x][y] = null;
                        break;
                    }
                }
            }

            // Upgrade kept unit
            keepUnit.starLevel++;
            keepUnit.currentStats = getStarScaledStats(keepUnit.baseStats, keepUnit.starLevel);
            keepUnit.currentHealth = keepUnit.currentStats.health;

            // Recursively check for more merges
            this.checkAndMerge(player, keepUnit);

            return { merged: true, mergedUnit: keepUnit.toJSON() };
        }

        return { merged: false };
    }

    /**
     * Check all units for possible merges.
     * Called at the start of planning phase to handle units bought during combat
     * that can now merge with board units.
     */
    checkAllMerges(player) {
        let mergesOccurred = true;
        let totalMerges = 0;

        // Keep checking until no more merges happen (handles chain merges like 1->2->3 star)
        while (mergesOccurred) {
            mergesOccurred = false;

            // Check each bench unit for possible merges
            for (let i = 0; i < player.bench.length; i++) {
                const unit = player.bench[i];
                if (unit) {
                    const result = this.checkAndMerge(player, unit);
                    if (result.merged) {
                        mergesOccurred = true;
                        totalMerges++;
                        console.log(`[GameRoom] Post-combat merge: ${unit.name} upgraded`);
                    }
                }
            }

            // Also check board units (in case two board units can merge with a bench unit)
            for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
                for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                    const unit = player.board[x][y];
                    if (unit) {
                        const result = this.checkAndMerge(player, unit);
                        if (result.merged) {
                            mergesOccurred = true;
                            totalMerges++;
                            console.log(`[GameRoom] Post-combat merge: ${unit.name} upgraded`);
                        }
                    }
                }
            }
        }

        if (totalMerges > 0) {
            console.log(`[GameRoom] ${player.name}: ${totalMerges} post-combat merge(s) completed`);
        }
    }

    // ========== ITEM ACTIONS ==========

    equipItem(player, itemIndex, instanceId) {
        // Validate item index
        if (itemIndex < 0 || itemIndex >= player.itemInventory.length) {
            return { success: false, error: 'Invalid item index' };
        }

        const item = player.itemInventory[itemIndex];
        if (!item) {
            return { success: false, error: 'No item at that index' };
        }

        // Find the unit
        const unit = this.findUnit(player, instanceId);
        if (!unit) {
            return { success: false, error: 'Unit not found' };
        }

        // Check if unit can hold more items (max 3)
        if (unit.items.length >= 3) {
            return { success: false, error: 'Unit already has max items' };
        }

        // Remove from inventory and add to unit
        player.itemInventory.splice(itemIndex, 1);
        unit.items.push(item);

        return { success: true, item, unitId: instanceId };
    }

    unequipItem(player, instanceId, itemSlot) {
        // Find the unit
        const unit = this.findUnit(player, instanceId);
        if (!unit) {
            return { success: false, error: 'Unit not found' };
        }

        // Validate item slot
        if (itemSlot < 0 || itemSlot >= unit.items.length) {
            return { success: false, error: 'Invalid item slot' };
        }

        // Check if inventory has room (max 10)
        if (player.itemInventory.length >= 10) {
            return { success: false, error: 'Item inventory is full' };
        }

        // Remove from unit and add to inventory
        const item = unit.items.splice(itemSlot, 1)[0];
        player.itemInventory.push(item);

        return { success: true, item, unitId: instanceId };
    }

    combineItems(player, itemIndex1, itemIndex2) {
        // Validate indices
        if (itemIndex1 < 0 || itemIndex1 >= player.itemInventory.length ||
            itemIndex2 < 0 || itemIndex2 >= player.itemInventory.length ||
            itemIndex1 === itemIndex2) {
            return { success: false, error: 'Invalid item indices' };
        }

        const item1 = player.itemInventory[itemIndex1];
        const item2 = player.itemInventory[itemIndex2];

        if (!item1 || !item2) {
            return { success: false, error: 'Items not found' };
        }

        // Check if both are components (not already combined)
        if (item1.isCombined || item2.isCombined) {
            return { success: false, error: 'Cannot combine already-combined items' };
        }

        // Find matching recipe
        const recipeKey = [item1.itemId, item2.itemId].sort().join('+');
        let combinedItem = null;

        for (const [id, template] of Object.entries(ItemTemplates)) {
            if (template.recipe) {
                const templateKey = [...template.recipe].sort().join('+');
                if (templateKey === recipeKey) {
                    combinedItem = {
                        itemId: id,
                        name: template.name,
                        stats: template.stats,
                        isCombined: true,
                        recipe: template.recipe
                    };
                    break;
                }
            }
        }

        if (!combinedItem) {
            return { success: false, error: 'No valid recipe for these items' };
        }

        // Remove components (remove higher index first to avoid shifting)
        const higher = Math.max(itemIndex1, itemIndex2);
        const lower = Math.min(itemIndex1, itemIndex2);
        player.itemInventory.splice(higher, 1);
        player.itemInventory.splice(lower, 1);

        // Add combined item
        player.itemInventory.push(combinedItem);

        return { success: true, combinedItem };
    }

    // ========== CREST ACTIONS ==========

    selectMinorCrest(player, crestId, instanceId) {
        // Validate crest exists and is minor
        const crestTemplate = CrestTemplates[crestId];
        if (!crestTemplate) {
            return { success: false, error: 'Crest not found' };
        }
        if (crestTemplate.type !== 'minor') {
            return { success: false, error: 'Not a minor crest' };
        }

        // Check if player already has max minor crests (3)
        if (player.minorCrests.length >= 3) {
            return { success: false, error: 'Already have maximum minor crests (3)' };
        }

        // Add to player's minor crests (team-wide bonus)
        const newCrest = {
            crestId: crestId,
            name: crestTemplate.name,
            description: crestTemplate.description,
            type: crestTemplate.type,
            teamBonus: crestTemplate.teamBonus, // Passive stat bonuses for all units
            stats: crestTemplate.stats,
            effect: crestTemplate.effect
        };
        player.minorCrests.push(newCrest);

        return { success: true, crest: newCrest };
    }

    selectMajorCrest(player, crestId) {
        // Validate crest exists and is major
        const crestTemplate = CrestTemplates[crestId];
        if (!crestTemplate) {
            return { success: false, error: 'Crest not found' };
        }
        if (crestTemplate.type !== 'major') {
            return { success: false, error: 'Not a major crest' };
        }

        // Set player's major crest with all template properties
        player.majorCrest = {
            crestId: crestId,
            name: crestTemplate.name,
            description: crestTemplate.description,
            type: crestTemplate.type,
            teamBonus: crestTemplate.teamBonus, // Important for stat bonuses
            stats: crestTemplate.stats,
            effect: crestTemplate.effect
        };

        return { success: true, crest: player.majorCrest };
    }

    findUnit(player, instanceId) {
        // Check board
        for (let x = 0; x < GameConstants.Grid.WIDTH; x++) {
            for (let y = 0; y < GameConstants.Grid.HEIGHT; y++) {
                if (player.board[x][y]?.instanceId === instanceId) {
                    return player.board[x][y];
                }
            }
        }

        // Check bench
        for (let i = 0; i < player.bench.length; i++) {
            if (player.bench[i]?.instanceId === instanceId) {
                return player.bench[i];
            }
        }

        return null;
    }

    // ========== CONSUMABLE ACTIONS ==========

    useConsumable(player, itemIndex) {
        if (itemIndex < 0 || itemIndex >= player.itemInventory.length) {
            return { success: false, error: 'Invalid item index' };
        }

        const item = player.itemInventory[itemIndex];
        if (!item) {
            return { success: false, error: 'No item at that index' };
        }

        // Check if it's a consumable
        if (item.itemId === 'crest_token') {
            // Generate crest selection (3 random minor crests)
            player.pendingCrestSelection = this.generateCrestChoices(3, 'minor');
            // Remove the consumable from inventory
            player.itemInventory.splice(itemIndex, 1);
            console.log(`[GameRoom] Player used Crest Token, generated ${player.pendingCrestSelection.length} choices`);
            return { success: true, selectionType: 'crest', choices: player.pendingCrestSelection };
        }
        else if (item.itemId === 'item_anvil') {
            // Generate item selection (3 random items)
            player.pendingItemSelection = this.generateItemChoices(3);
            // Remove the consumable from inventory
            player.itemInventory.splice(itemIndex, 1);
            console.log(`[GameRoom] Player used Item Anvil, generated ${player.pendingItemSelection.length} choices`);
            return { success: true, selectionType: 'item', choices: player.pendingItemSelection };
        }

        return { success: false, error: 'Item is not a consumable' };
    }

    generateCrestChoices(count, type) {
        const choices = [];
        const availableCrests = Object.values(CrestTemplates).filter(c => c.type === type);

        // Shuffle and pick
        const shuffled = [...availableCrests].sort(() => Math.random() - 0.5);
        for (let i = 0; i < Math.min(count, shuffled.length); i++) {
            const crest = shuffled[i];
            choices.push({
                crestId: crest.crestId,
                name: crest.name,
                description: crest.description,
                type: crest.type,
                grantsTrait: crest.grantsTrait,
                teamBonus: crest.teamBonus
            });
        }
        return choices;
    }

    generateItemChoices(count) {
        const choices = [];
        const availableItems = Object.values(ItemTemplates).filter(i => !i.isComponent);

        // Weighted selection based on rarity
        const weighted = [];
        for (const item of availableItems) {
            const weight = item.rarity === 'common' ? 3 : item.rarity === 'uncommon' ? 2 : 1;
            for (let i = 0; i < weight; i++) {
                weighted.push(item);
            }
        }

        // Pick unique items
        const selected = new Set();
        while (choices.length < count && weighted.length > 0) {
            const index = Math.floor(Math.random() * weighted.length);
            const item = weighted[index];
            if (!selected.has(item.itemId)) {
                selected.add(item.itemId);
                choices.push({
                    itemId: item.itemId,
                    name: item.name,
                    description: item.description,
                    rarity: item.rarity,
                    stats: item.stats,
                    isCombined: item.isCombined || false
                });
            }
            // Remove all instances of this item from weighted pool
            weighted.splice(index, 1);
        }
        return choices;
    }

    selectCrestChoice(player, choiceIndex) {
        if (!player.pendingCrestSelection || player.pendingCrestSelection.length === 0) {
            return { success: false, error: 'No pending crest selection' };
        }

        if (choiceIndex < 0 || choiceIndex >= player.pendingCrestSelection.length) {
            return { success: false, error: 'Invalid choice index' };
        }

        const selectedCrest = player.pendingCrestSelection[choiceIndex];

        // Use addOrUpgradeCrest to handle ranking
        const result = this.addOrUpgradeCrest(player, selectedCrest);

        // Clear pending selection
        player.pendingCrestSelection = [];

        if (result.success) {
            console.log(`[GameRoom] Player ${result.upgraded ? 'upgraded' : 'selected'} crest: ${result.crest.name} (rank ${result.crest.rank})`);
            return { success: true, crest: result.crest, upgraded: result.upgraded };
        }

        if (result.needsReplacement) {
            // Store the pending crest for replacement selection
            player.pendingCrestReplacement = { newCrest: result.newCrest };
            console.log(`[GameRoom] Player needs to choose which crest to replace for ${result.newCrest.name}`);
            return { success: true, needsReplacement: true, newCrest: result.newCrest };
        }

        return result;
    }

    /**
     * Add a new minor crest or upgrade an existing one
     * @param {PlayerState} player - The player
     * @param {Object} crestTemplate - The crest template to add/upgrade
     * @param {boolean} allowReplacement - If true, returns needsReplacement instead of failing at max
     * @returns {Object} Result with success, crest, upgraded flag, or needsReplacement
     */
    addOrUpgradeCrest(player, crestTemplate, allowReplacement = true) {
        // Check for existing crest with same ID
        const existing = player.minorCrests.find(c => c.crestId === crestTemplate.crestId);

        if (existing) {
            if (existing.rank >= 3) {
                return { success: false, error: 'Crest already at max rank' };
            }
            existing.rank++;
            console.log(`[GameRoom] ${player.name} upgraded ${existing.name} to rank ${existing.rank}`);
            return { success: true, upgraded: true, crest: existing };
        }

        // Check slot limit (max 3 unique crests)
        if (player.minorCrests.length >= 3) {
            if (allowReplacement) {
                // Return special status indicating player needs to choose which crest to replace
                const newCrest = {
                    crestId: crestTemplate.crestId,
                    name: crestTemplate.name,
                    description: crestTemplate.description,
                    type: crestTemplate.type || 'minor',
                    rank: 1,
                    teamBonus: { ...crestTemplate.teamBonus }
                };
                return { success: false, needsReplacement: true, newCrest: newCrest };
            }
            return { success: false, error: 'Already have maximum minor crests (3)' };
        }

        // Add new crest at rank 1
        const newCrest = {
            crestId: crestTemplate.crestId,
            name: crestTemplate.name,
            description: crestTemplate.description,
            type: crestTemplate.type || 'minor',
            rank: 1,
            teamBonus: { ...crestTemplate.teamBonus }
        };
        player.minorCrests.push(newCrest);
        return { success: true, upgraded: false, crest: newCrest };
    }

    /**
     * Replace an existing minor crest with a new one (when at max capacity)
     * @param {PlayerState} player - The player
     * @param {number} replaceIndex - Index of the crest to replace (0-2)
     * @returns {Object} Result with success and the new crest
     */
    replaceCrest(player, replaceIndex) {
        if (!player.pendingCrestReplacement) {
            return { success: false, error: 'No pending crest replacement' };
        }

        if (replaceIndex < 0 || replaceIndex >= player.minorCrests.length) {
            return { success: false, error: 'Invalid crest index' };
        }

        const oldCrest = player.minorCrests[replaceIndex];
        const newCrest = player.pendingCrestReplacement.newCrest;

        // Replace the crest
        player.minorCrests[replaceIndex] = newCrest;
        player.pendingCrestReplacement = null;

        console.log(`[GameRoom] ${player.name} replaced ${oldCrest.name} with ${newCrest.name}`);
        return { success: true, oldCrest: oldCrest, newCrest: newCrest };
    }

    selectItemChoice(player, choiceIndex) {
        if (!player.pendingItemSelection || player.pendingItemSelection.length === 0) {
            return { success: false, error: 'No pending item selection' };
        }

        if (choiceIndex < 0 || choiceIndex >= player.pendingItemSelection.length) {
            return { success: false, error: 'Invalid choice index' };
        }

        const selectedItem = player.pendingItemSelection[choiceIndex];

        // Add to inventory (if not full)
        if (player.itemInventory.length >= 10) {
            return { success: false, error: 'Item inventory is full' };
        }

        player.itemInventory.push({
            itemId: selectedItem.itemId,
            name: selectedItem.name,
            description: selectedItem.description,
            rarity: selectedItem.rarity,
            stats: selectedItem.stats,
            isCombined: selectedItem.isCombined || false
        });

        // Clear pending selection
        player.pendingItemSelection = [];

        console.log(`[GameRoom] Player selected item: ${selectedItem.name}`);
        return { success: true, item: selectedItem };
    }

    // ========== MAD MERCHANT ==========

    /**
     * Generate merchant options for the Mad Merchant round
     * Creates 6 pairs (4 players pick 1 each, 2 pairs left unpicked)
     *
     * Pair Types:
     * - unit_item: Random unit (2-4 cost) + Random item
     * - crest_rerolls: Specific minor crest + 3 free rerolls
     * - gold_item: 5-8 gold + Random item
     * - double_item: Random item + Random item
     * - unit_crest: Random unit (2-4 cost) + Specific minor crest
     * - item_crest: Random item + Specific minor crest
     */
    generateMerchantOptions() {
        const options = [];

        // Define 6 pair types (shuffle to randomize)
        const pairTypes = ['unit_item', 'crest_rerolls', 'gold_item', 'double_item', 'unit_crest', 'item_crest'];
        const shuffledTypes = [...pairTypes].sort(() => Math.random() - 0.5);

        for (let i = 0; i < 6; i++) {
            const pairType = shuffledTypes[i];
            const pair = this.generateMerchantPair(pairType, i);
            options.push(pair);
        }

        return options;
    }

    /**
     * Generate a single merchant pair with two rewards
     */
    generateMerchantPair(pairType, index) {
        const pair = {
            optionId: `pair_${index}`,
            pairType: pairType,
            rewardA: this.generateMerchantReward(pairType, 'A'),
            rewardB: this.generateMerchantReward(pairType, 'B'),
            isPicked: false,
            pickedBy: null,
            pickedByName: null
        };
        return pair;
    }

    /**
     * Generate a single reward based on pair type and slot (A or B)
     */
    generateMerchantReward(pairType, slot) {
        switch (pairType) {
            case 'unit_item':
                if (slot === 'A') {
                    return this.generateUnitReward(2, 4); // 2-4 cost unit
                } else {
                    return this.generateItemReward();
                }

            case 'crest_rerolls':
                if (slot === 'A') {
                    return this.generateCrestReward();
                } else {
                    return { type: 'rerolls', rerollCount: 3, name: '3 Free Rerolls', description: 'Gain 3 free shop rerolls' };
                }

            case 'gold_item':
                if (slot === 'A') {
                    const goldAmount = Math.floor(Math.random() * 4) + 5; // 5-8 gold
                    return { type: 'gold', goldAmount, name: `${goldAmount} Gold`, description: `Gain ${goldAmount} gold` };
                } else {
                    return this.generateItemReward();
                }

            case 'double_item':
                return this.generateItemReward(); // Both A and B are items

            case 'unit_crest':
                if (slot === 'A') {
                    return this.generateUnitReward(2, 4); // 2-4 cost unit
                } else {
                    return this.generateCrestReward();
                }

            case 'item_crest':
                if (slot === 'A') {
                    return this.generateItemReward();
                } else {
                    return this.generateCrestReward();
                }

            default:
                return this.generateItemReward();
        }
    }

    /**
     * Generate a unit reward for merchant
     */
    generateUnitReward(minCost, maxCost) {
        // Get units in cost range
        const eligibleUnits = [];
        for (let cost = minCost; cost <= maxCost; cost++) {
            eligibleUnits.push(...getUnitsByCost(cost));
        }

        if (eligibleUnits.length === 0) {
            // Fallback to gold if no units available
            return { type: 'gold', goldAmount: 4, name: '4 Gold', description: 'Gain 4 gold' };
        }

        const unit = eligibleUnits[Math.floor(Math.random() * eligibleUnits.length)];
        return {
            type: 'unit',
            unitId: unit.unitId,
            name: unit.name,
            cost: unit.cost,
            description: `${unit.cost}-cost ${unit.traits.join('/')} unit`
        };
    }

    /**
     * Generate an item reward for merchant
     */
    generateItemReward() {
        const availableItems = Object.values(ItemTemplates).filter(i => !i.isComponent);
        if (availableItems.length === 0) {
            return { type: 'gold', goldAmount: 3, name: '3 Gold', description: 'Gain 3 gold' };
        }

        const item = availableItems[Math.floor(Math.random() * availableItems.length)];
        return {
            type: 'item',
            itemId: item.itemId,
            name: item.name,
            description: item.description || '',
            rarity: item.rarity || 'common',
            stats: item.stats || {}
        };
    }

    /**
     * Generate a crest reward for merchant
     */
    generateCrestReward() {
        const minorCrests = Object.values(CrestTemplates).filter(c => c.type === 'minor');
        if (minorCrests.length === 0) {
            return { type: 'rerolls', rerollCount: 2, name: '2 Free Rerolls', description: 'Gain 2 free shop rerolls' };
        }

        const crest = minorCrests[Math.floor(Math.random() * minorCrests.length)];
        return {
            type: 'crest',
            crestId: crest.crestId,
            name: crest.name,
            description: crest.description,
            teamBonus: crest.teamBonus
        };
    }

    /**
     * Determine pick order for Mad Merchant (lowest health first)
     */
    determineMerchantPickOrder() {
        const activePlayers = this.getActivePlayers();

        // Sort by health (lowest first), then by board index (tie-breaker)
        return activePlayers
            .map(p => ({
                clientId: p.clientId,
                name: p.name,
                health: p.health,
                boardIndex: p.boardIndex
            }))
            .sort((a, b) => {
                if (a.health !== b.health) {
                    return a.health - b.health; // Lowest health first
                }
                return a.boardIndex - b.boardIndex; // Tie-breaker
            });
    }

    /**
     * Start the Mad Merchant round
     */
    startMerchantRound() {
        console.log(`[GameRoom] Starting Mad Merchant round`);

        const options = this.generateMerchantOptions();
        const pickOrder = this.determineMerchantPickOrder();

        this.merchantState = {
            options: options,
            pickOrder: pickOrder,
            currentPickerIndex: 0,
            currentPickerId: pickOrder.length > 0 ? pickOrder[0].clientId : null,
            isActive: true
        };

        console.log(`[GameRoom] Merchant options: ${options.length}, pick order: ${pickOrder.map(p => p.name).join(' -> ')}`);

        // Return the merchant start data for broadcasting
        return {
            options: options,
            pickOrder: pickOrder,
            currentPickerId: this.merchantState.currentPickerId,
            currentPickerName: pickOrder.length > 0 ? pickOrder[0].name : null
        };
    }

    /**
     * Handle a player picking a merchant pair
     */
    handleMerchantPick(clientId, optionId) {
        if (!this.merchantState || !this.merchantState.isActive) {
            return { success: false, error: 'Merchant round not active' };
        }

        // Verify it's this player's turn
        if (this.merchantState.currentPickerId !== clientId) {
            return { success: false, error: 'Not your turn to pick' };
        }

        // Find the option (pair)
        const option = this.merchantState.options.find(o => o.optionId === optionId);
        if (!option) {
            return { success: false, error: 'Invalid option' };
        }

        if (option.isPicked) {
            return { success: false, error: 'Option already picked' };
        }

        const player = this.getPlayer(clientId);
        if (!player) {
            return { success: false, error: 'Player not found' };
        }

        // Mark option as picked
        option.isPicked = true;
        option.pickedBy = clientId;
        option.pickedByName = player.name;

        // Apply both rewards from the pair
        const rewardsApplied = [];
        if (option.rewardA) {
            const resultA = this.applyMerchantReward(player, option.rewardA);
            rewardsApplied.push({ reward: option.rewardA, result: resultA });
        }
        if (option.rewardB) {
            const resultB = this.applyMerchantReward(player, option.rewardB);
            rewardsApplied.push({ reward: option.rewardB, result: resultB });
        }

        console.log(`[GameRoom] ${player.name} picked pair ${option.pairType}: ${option.rewardA?.name || 'N/A'} + ${option.rewardB?.name || 'N/A'}`);

        // Return success with pick info for broadcasting
        return {
            success: true,
            optionId: optionId,
            pickedById: clientId,
            pickedByName: player.name,
            pairType: option.pairType,
            rewardsApplied: rewardsApplied
        };
    }

    /**
     * Apply a single merchant reward to a player
     */
    applyMerchantReward(player, reward) {
        if (!reward) return { success: false };

        switch (reward.type) {
            case 'item':
                // Add item to player's inventory
                const itemTemplate = ItemTemplates[reward.itemId];
                if (itemTemplate && player.itemInventory.length < 10) {
                    player.itemInventory.push({
                        itemId: itemTemplate.itemId,
                        name: itemTemplate.name,
                        description: itemTemplate.description,
                        rarity: itemTemplate.rarity || 'common',
                        stats: itemTemplate.stats,
                        isCombined: !itemTemplate.isComponent
                    });
                    console.log(`[GameRoom] ${player.name} received item: ${itemTemplate.name}`);
                    return { success: true, type: 'item', name: itemTemplate.name };
                }
                return { success: false, error: 'Inventory full' };

            case 'unit':
                // Add unit to player's bench
                const unitTemplate = UnitTemplates[reward.unitId];
                if (!unitTemplate) {
                    // Fallback: try to find by iterating
                    for (const [id, template] of Object.entries(UnitTemplates)) {
                        if (id === reward.unitId || template.unitId === reward.unitId) {
                            return this.applyUnitReward(player, template);
                        }
                    }
                    return { success: false, error: 'Unit not found' };
                }
                return this.applyUnitReward(player, unitTemplate);

            case 'gold':
                player.gold += reward.goldAmount;
                console.log(`[GameRoom] ${player.name} received ${reward.goldAmount} gold`);
                return { success: true, type: 'gold', amount: reward.goldAmount };

            case 'rerolls':
                this.grantFreeRerolls(player, reward.rerollCount);
                return { success: true, type: 'rerolls', count: reward.rerollCount };

            case 'crest':
                // Apply crest directly (uses addOrUpgradeCrest for ranking)
                const crestTemplate = {
                    crestId: reward.crestId,
                    name: reward.name,
                    description: reward.description,
                    type: 'minor',
                    teamBonus: reward.teamBonus
                };
                const crestResult = this.addOrUpgradeCrest(player, crestTemplate);
                if (crestResult.success) {
                    console.log(`[GameRoom] ${player.name} received crest: ${reward.name} (rank ${crestResult.crest.rank})`);
                    return { success: true, type: 'crest', crest: crestResult.crest, upgraded: crestResult.upgraded };
                }
                if (crestResult.needsReplacement) {
                    // Store the pending crest for replacement selection
                    player.pendingCrestReplacement = { newCrest: crestResult.newCrest };
                    console.log(`[GameRoom] ${player.name} needs to choose which crest to replace for ${reward.name}`);
                    return { success: true, type: 'crest_pending', needsReplacement: true, newCrest: crestResult.newCrest };
                }
                return crestResult;

            default:
                console.log(`[GameRoom] Unknown reward type: ${reward.type}`);
                return { success: false, error: 'Unknown reward type' };
        }
    }

    /**
     * Apply a unit reward to a player (helper for merchant)
     */
    applyUnitReward(player, unitTemplate) {
        const emptySlot = player.bench.findIndex(u => u === null);
        if (emptySlot === -1) {
            // Bench full - give gold equivalent instead
            const goldValue = unitTemplate.cost * 2;
            player.gold += goldValue;
            console.log(`[GameRoom] ${player.name} received ${goldValue} gold (bench full, couldn't add ${unitTemplate.name})`);
            return { success: true, type: 'gold', amount: goldValue, reason: 'bench_full' };
        }

        const newUnit = new UnitInstance(unitTemplate, 1);
        player.bench[emptySlot] = newUnit;

        // Check for merges
        this.checkAllMerges(player);

        console.log(`[GameRoom] ${player.name} received unit: ${unitTemplate.name}`);
        return { success: true, type: 'unit', name: unitTemplate.name, benchSlot: emptySlot };
    }

    /**
     * Advance to the next picker in the merchant round
     */
    advanceMerchantTurn() {
        if (!this.merchantState || !this.merchantState.isActive) {
            return null;
        }

        this.merchantState.currentPickerIndex++;

        // Skip disconnected players (no longer in room)
        while (this.merchantState.currentPickerIndex < this.merchantState.pickOrder.length) {
            const candidate = this.merchantState.pickOrder[this.merchantState.currentPickerIndex];
            if (this.players.has(candidate.clientId)) {
                break; // This player is still connected
            }
            console.log(`[GameRoom] Skipping disconnected player ${candidate.name} in merchant pick order`);
            this.merchantState.currentPickerIndex++;
        }

        // Check if all players have picked (or been skipped)
        if (this.merchantState.currentPickerIndex >= this.merchantState.pickOrder.length) {
            console.log(`[GameRoom] All players have picked, ending merchant round`);
            return { allPicked: true };
        }

        const nextPicker = this.merchantState.pickOrder[this.merchantState.currentPickerIndex];
        this.merchantState.currentPickerId = nextPicker.clientId;
        this.merchantState.needsSkip = false; // Reset skip flag for new picker

        console.log(`[GameRoom] Merchant turn advanced to: ${nextPicker.name}`);

        return {
            allPicked: false,
            currentPickerId: nextPicker.clientId,
            currentPickerName: nextPicker.name
        };
    }

    /**
     * End the merchant round
     */
    endMerchantRound() {
        if (this.merchantState) {
            this.merchantState.isActive = false;
            console.log(`[GameRoom] Merchant round ended`);
        }
        this.merchantState = null;
    }

    /**
     * Get current merchant state for broadcasting
     */
    getMerchantState() {
        if (!this.merchantState) return null;

        return {
            options: this.merchantState.options,
            pickOrder: this.merchantState.pickOrder,
            currentPickerId: this.merchantState.currentPickerId,
            currentPickerIndex: this.merchantState.currentPickerIndex,
            isActive: this.merchantState.isActive
        };
    }

    // ========== MAJOR CREST ROUND ==========

    /**
     * Start the Major Crest round - each player gets 3 random options
     */
    startMajorCrestRound() {
        console.log(`[GameRoom] Starting Major Crest round`);

        // Get all major crests
        const majorCrests = Object.values(CrestTemplates).filter(c => c.type === 'major');

        // Generate options for each player
        const playerOptions = {};
        for (const player of this.getActivePlayers()) {
            // Shuffle and pick 3 random major crests for this player
            const shuffled = [...majorCrests].sort(() => Math.random() - 0.5);
            const options = shuffled.slice(0, 3).map(c => ({
                crestId: c.crestId,
                name: c.name,
                description: c.description,
                teamBonus: c.teamBonus
            }));
            playerOptions[player.clientId] = options;
        }

        this.majorCrestState = {
            isActive: true,
            playerSelections: new Map(), // Track who has selected
            playerOptions: playerOptions // Store options for auto-assign
        };

        console.log(`[GameRoom] Generated major crest options for ${Object.keys(playerOptions).length} players`);

        return {
            playerOptions: playerOptions
        };
    }

    /**
     * Handle a player selecting their major crest
     */
    handleMajorCrestSelect(clientId, crestId) {
        if (!this.majorCrestState || !this.majorCrestState.isActive) {
            return { success: false, error: 'Major crest round not active' };
        }

        // Check if player already selected
        if (this.majorCrestState.playerSelections.has(clientId)) {
            return { success: false, error: 'Already selected a crest' };
        }

        const player = this.getPlayer(clientId);
        if (!player) {
            return { success: false, error: 'Player not found' };
        }

        // Validate crest exists and is major
        const crestTemplate = CrestTemplates[crestId];
        if (!crestTemplate || crestTemplate.type !== 'major') {
            return { success: false, error: 'Invalid major crest' };
        }

        // Set player's major crest
        player.majorCrest = {
            crestId: crestId,
            name: crestTemplate.name,
            description: crestTemplate.description,
            type: crestTemplate.type,
            teamBonus: crestTemplate.teamBonus
        };

        // Track selection
        this.majorCrestState.playerSelections.set(clientId, crestId);

        console.log(`[GameRoom] ${player.name} selected major crest: ${crestTemplate.name}`);

        // Check if all players have selected
        const activePlayers = this.getActivePlayers();
        const allSelected = activePlayers.every(p => this.majorCrestState.playerSelections.has(p.clientId));

        return {
            success: true,
            crest: player.majorCrest,
            allSelected: allSelected
        };
    }

    /**
     * Auto-assign random major crests to players who haven't selected
     * Returns array of auto-assigned players for broadcasting
     */
    autoAssignMajorCrests() {
        if (!this.majorCrestState || !this.majorCrestState.playerOptions) {
            return [];
        }

        const autoAssigned = [];
        for (const player of this.getActivePlayers()) {
            // Skip players who already selected
            if (this.majorCrestState.playerSelections.has(player.clientId)) {
                continue;
            }

            // Get this player's options
            const options = this.majorCrestState.playerOptions[player.clientId];
            if (!options || options.length === 0) {
                continue;
            }

            // Pick a random option
            const randomIndex = Math.floor(Math.random() * options.length);
            const selectedCrest = options[randomIndex];

            // Assign the crest
            player.majorCrest = {
                crestId: selectedCrest.crestId,
                name: selectedCrest.name,
                description: selectedCrest.description,
                type: 'major',
                teamBonus: selectedCrest.teamBonus
            };

            this.majorCrestState.playerSelections.set(player.clientId, selectedCrest.crestId);

            console.log(`[GameRoom] Auto-assigned ${selectedCrest.name} to ${player.name}`);
            autoAssigned.push({
                playerId: player.clientId,
                playerName: player.name,
                crestId: selectedCrest.crestId,
                crestName: selectedCrest.name
            });
        }

        return autoAssigned;
    }

    /**
     * End the major crest round
     */
    endMajorCrestRound() {
        if (this.majorCrestState) {
            this.majorCrestState.isActive = false;
            console.log(`[GameRoom] Major crest round ended`);
        }
        this.majorCrestState = null;
    }

    // ========== SERIALIZATION ==========

    getFullState() {
        // Calculate timer - for major_crest rounds, use majorCrestStartTime
        let timer = Math.ceil(this.phaseTimer);
        const roundType = this.getCurrentRoundType();
        if (roundType === 'major_crest' && this.majorCrestStartTime) {
            const elapsed = (Date.now() - this.majorCrestStartTime) / 1000;
            timer = Math.max(0, Math.ceil(20 - elapsed));
        }

        return {
            roomId: this.roomId,
            hostId: this.hostId,
            state: this.state,
            phase: this.phase,
            round: this.round,
            phaseTimer: timer,
            players: this.getAllPlayers().map(p => p.toJSON()),
            matchups: this.matchups
        };
    }

    getStateForPlayer(clientId) {
        const fullState = this.getFullState();

        // Hide other players' shops
        fullState.players = fullState.players.map(p => {
            if (p.clientId !== clientId) {
                return {
                    ...p,
                    shop: null, // Don't reveal opponent's shop
                    gold: p.gold // Can see opponent's gold
                };
            }
            return p;
        });

        return fullState;
    }
}

module.exports = { GameRoom, PlayerState, UnitInstance, UnitPool, CombatSimulator, CombatEventType };
