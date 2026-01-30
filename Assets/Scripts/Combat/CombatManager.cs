using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Hex;

namespace Crestforge.Combat
{
    /// <summary>
    /// Manages combat simulation between player and enemy units
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("Combat State")]
        public bool isInCombat;
        public float combatTime;
        public List<CombatUnit> allUnits = new List<CombatUnit>();

        [Header("Settings")]
        public float tickRate = 0.1f;
        private float tickTimer;

        [Header("Events")]
        public System.Action<CombatResult> OnCombatEnd;
        public System.Action<CombatUnit, CombatUnit, int> OnDamageDealt;
        public System.Action<CombatUnit> OnUnitDied;
        public System.Action<CombatUnit> OnAbilityCast;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Start a new combat between player and enemy boards
        /// </summary>
        public void StartCombat(UnitInstance[,] playerBoard, UnitInstance[,] enemyBoard,
            List<CrestData> playerCrests, List<CrestData> enemyCrests)
        {
            allUnits.Clear();
            combatTime = 0;
            isInCombat = true;
            tickTimer = 0;
            startupTimer = 0;

            // Create combat units for player (bottom half of battlefield)
            for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
            {
                for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                {
                    if (playerBoard[x, y] != null)
                    {
                        var combatUnit = new CombatUnit(playerBoard[x, y], Team.Player, new Vector2Int(x, y));
                        ApplyCrests(combatUnit, playerCrests);
                        allUnits.Add(combatUnit);
                    }
                }
            }

            // Create combat units for enemy (top half, mirrored)
            for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
            {
                for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                {
                    if (enemyBoard[x, y] != null)
                    {
                        // Mirror Y position
                        int mirroredY = GameConstants.Grid.TOTAL_BATTLEFIELD_HEIGHT - 1 - y;
                        var combatUnit = new CombatUnit(enemyBoard[x, y], Team.Enemy, new Vector2Int(x, mirroredY));
                        ApplyCrests(combatUnit, enemyCrests);
                        allUnits.Add(combatUnit);
                    }
                }
            }

            Debug.Log($"Combat started: {GetTeamUnits(Team.Player).Count} vs {GetTeamUnits(Team.Enemy).Count}");
        }

        // Startup delay to let visuals sync before combat begins
        private float combatStartDelay = 0.5f;
        private float startupTimer = 0f;

        private void Update()
        {
            if (!isInCombat) return;

            // Wait for startup delay before processing combat
            if (startupTimer < combatStartDelay)
            {
                startupTimer += Time.deltaTime;
                return;
            }

            tickTimer += Time.deltaTime;

            while (tickTimer >= tickRate)
            {
                tickTimer -= tickRate;
                ProcessTick();
            }

            combatTime += Time.deltaTime;
        }

        /// <summary>
        /// Process one combat tick
        /// </summary>
        private void ProcessTick()
        {
            // Reset tick flags
            foreach (var unit in allUnits)
            {
                unit.hasActedThisTick = false;
            }

            // Check win condition
            var playerUnits = GetAliveUnits(Team.Player);
            var enemyUnits = GetAliveUnits(Team.Enemy);

            if (playerUnits.Count == 0 || enemyUnits.Count == 0)
            {
                EndCombat(playerUnits.Count > 0);
                return;
            }

            // Process each unit
            foreach (var unit in allUnits)
            {
                if (unit.isDead || unit.hasActedThisTick) continue;

                // Process status effects
                ProcessStatusEffects(unit);
                if (unit.isDead) continue;

                // Reduce cooldowns
                unit.attackCooldown = Mathf.Max(0, unit.attackCooldown - tickRate);
                unit.moveCooldown = Mathf.Max(0, unit.moveCooldown - tickRate);

                // Find target if needed
                if (unit.target == null || unit.target.isDead)
                {
                    unit.target = FindTarget(unit);
                }

                if (unit.target == null) continue;

                // Calculate distance
                int distance = HexUtils.Distance(unit.position, unit.target.position);

                if (distance <= unit.stats.range)
                {
                    // In range - attack if ready
                    if (unit.attackCooldown <= 0)
                    {
                        PerformAttack(unit, unit.target);
                    }
                }
                else
                {
                    // Move towards target (if not on cooldown)
                    if (unit.moveCooldown <= 0)
                    {
                        MoveTowardsTarget(unit);
                        unit.moveCooldown = GameConstants.Combat.MOVE_COOLDOWN;

                        // Add attack delay after moving so visual can catch up
                        // This prevents attacks from appearing to happen at the old position
                        unit.attackCooldown = Mathf.Max(unit.attackCooldown, GameConstants.Combat.ATTACK_DELAY_AFTER_MOVE);
                    }
                }

                unit.hasActedThisTick = true;
            }
        }

        /// <summary>
        /// Find the closest enemy target
        /// </summary>
        private CombatUnit FindTarget(CombatUnit unit)
        {
            var enemies = GetAliveUnits(unit.team == Team.Player ? Team.Enemy : Team.Player);
            
            if (enemies.Count == 0) return null;

            CombatUnit closest = null;
            int closestDist = int.MaxValue;

            foreach (var enemy in enemies)
            {
                int dist = HexUtils.Distance(unit.position, enemy.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy;
                }
            }

            return closest;
        }

        /// <summary>
        /// Move unit towards its target
        /// </summary>
        private void MoveTowardsTarget(CombatUnit unit)
        {
            if (unit.target == null) return;

            // Get blocked positions
            var blocked = new HashSet<Vector2Int>();
            foreach (var other in allUnits)
            {
                if (!other.isDead && other != unit)
                {
                    blocked.Add(other.position);
                }
            }

            // Find adjacent position to target
            var targetNeighbors = HexUtils.GetValidNeighbors(
                unit.target.position, 
                GameConstants.Grid.WIDTH, 
                GameConstants.Grid.TOTAL_BATTLEFIELD_HEIGHT);

            Vector2Int? bestNeighbor = null;
            int bestDist = int.MaxValue;

            foreach (var neighbor in targetNeighbors)
            {
                if (!blocked.Contains(neighbor))
                {
                    int dist = HexUtils.Distance(unit.position, neighbor);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestNeighbor = neighbor;
                    }
                }
            }

            if (bestNeighbor.HasValue)
            {
                var path = HexUtils.FindPath(
                    unit.position, 
                    bestNeighbor.Value, 
                    blocked,
                    GameConstants.Grid.WIDTH,
                    GameConstants.Grid.TOTAL_BATTLEFIELD_HEIGHT);

                if (path.Count > 0)
                {
                    unit.position = path[0];
                }
            }
        }

        /// <summary>
        /// Perform an attack
        /// </summary>
        private void PerformAttack(CombatUnit attacker, CombatUnit target)
        {
            // Calculate damage
            int damage = attacker.stats.attack;

            // Apply armor reduction
            float armorReduction = target.stats.armor / (float)(target.stats.armor + 100);
            damage = Mathf.RoundToInt(damage * (1 - armorReduction));

            // Deal damage
            DealDamage(attacker, target, damage, DamageType.Physical);

            // Set cooldown (apply global speed multiplier)
            float effectiveAttackSpeed = attacker.stats.attackSpeed * GameConstants.Combat.GLOBAL_ATTACK_SPEED_MULTIPLIER;
            float attackInterval = 1f / effectiveAttackSpeed;
            attacker.attackCooldown = attackInterval;

            // Gain mana
            attacker.currentMana = Mathf.Min(
                attacker.currentMana + GameConstants.Combat.MANA_PER_ATTACK,
                attacker.stats.maxMana);

            // Check for ability cast
            if (attacker.currentMana >= attacker.stats.maxMana)
            {
                CastAbility(attacker);
            }

            // Apply on-hit effects from items
            ApplyOnHitEffects(attacker, target, damage);
        }

        /// <summary>
        /// Deal damage to a unit
        /// </summary>
        private void DealDamage(CombatUnit source, CombatUnit target, int amount, DamageType type)
        {
            // Apply magic resist for magic damage
            if (type == DamageType.Magic)
            {
                float mrReduction = target.stats.magicResist / (float)(target.stats.magicResist + 100);
                amount = Mathf.RoundToInt(amount * (1 - mrReduction));
            }

            // True damage bypasses all resistance
            // Poison and Fire could have special handling

            // Absorb with shield
            if (target.currentShield > 0)
            {
                int absorbed = Mathf.Min(target.currentShield, amount);
                target.currentShield -= absorbed;
                amount -= absorbed;
            }

            // Apply damage
            target.currentHealth = Mathf.Max(0, target.currentHealth - amount);

            // Gain mana from damage
            target.currentMana = Mathf.Min(
                target.currentMana + GameConstants.Combat.MANA_PER_DAMAGE_TAKEN,
                target.stats.maxMana);

            OnDamageDealt?.Invoke(source, target, amount);

            // Check death
            if (target.currentHealth <= 0)
            {
                KillUnit(target, source);
            }
        }

        /// <summary>
        /// Cast unit's ability
        /// </summary>
        private void CastAbility(CombatUnit caster)
        {
            var ability = caster.source.template.ability;
            if (ability == null) return;

            caster.currentMana = 0;
            OnAbilityCast?.Invoke(caster);

            // Handle ability based on type
            switch (ability.type)
            {
                case AbilityType.Damage:
                    CastDamageAbility(caster, ability);
                    break;
                case AbilityType.Heal:
                    CastHealAbility(caster, ability);
                    break;
                case AbilityType.Shield:
                    CastShieldAbility(caster, ability);
                    break;
                case AbilityType.AreaDamage:
                    CastAreaDamage(caster, ability);
                    break;
                case AbilityType.DamageAndHeal:
                    CastLifeDrainAbility(caster, ability);
                    break;
                case AbilityType.Buff:
                    CastBuffAbility(caster, ability);
                    break;
            }

            Debug.Log($"{caster.source.template.unitName} casts {ability.abilityName}!");
        }

        private void CastDamageAbility(CombatUnit caster, AbilityData ability)
        {
            var target = GetAbilityTarget(caster, ability.targeting);
            if (target == null) return;

            int damage = Mathf.RoundToInt(ability.baseDamage * GameConstants.Units.STAR_MULTIPLIERS[caster.source.starLevel]);
            DealDamage(caster, target, damage, ability.damageType);
        }

        private void CastHealAbility(CombatUnit caster, AbilityData ability)
        {
            var target = GetAbilityTarget(caster, ability.targeting);
            if (target == null) return;

            int heal = Mathf.RoundToInt(ability.baseHealing * GameConstants.Units.STAR_MULTIPLIERS[caster.source.starLevel]);
            target.currentHealth = Mathf.Min(target.currentHealth + heal, target.stats.health);
        }

        private void CastShieldAbility(CombatUnit caster, AbilityData ability)
        {
            var target = GetAbilityTarget(caster, ability.targeting);
            if (target == null) return;

            int shield = Mathf.RoundToInt(ability.baseShieldAmount * GameConstants.Units.STAR_MULTIPLIERS[caster.source.starLevel]);
            target.currentShield += shield;
        }

        private void CastAreaDamage(CombatUnit caster, AbilityData ability)
        {
            var center = caster.target?.position ?? caster.position;
            var enemies = GetAliveUnits(caster.team == Team.Player ? Team.Enemy : Team.Player);

            int damage = Mathf.RoundToInt(ability.baseDamage * GameConstants.Units.STAR_MULTIPLIERS[caster.source.starLevel]);

            foreach (var enemy in enemies)
            {
                if (HexUtils.Distance(center, enemy.position) <= ability.radius)
                {
                    DealDamage(caster, enemy, damage, ability.damageType);
                }
            }
        }

        private void CastLifeDrainAbility(CombatUnit caster, AbilityData ability)
        {
            var target = GetAbilityTarget(caster, ability.targeting);
            if (target == null) return;

            int damage = Mathf.RoundToInt(ability.baseDamage * GameConstants.Units.STAR_MULTIPLIERS[caster.source.starLevel]);
            DealDamage(caster, target, damage, ability.damageType);

            int heal = Mathf.RoundToInt(damage * 0.5f); // 50% lifesteal on ability
            caster.currentHealth = Mathf.Min(caster.currentHealth + heal, caster.stats.health);
        }

        private void CastBuffAbility(CombatUnit caster, AbilityData ability)
        {
            var target = GetAbilityTarget(caster, ability.targeting);
            if (target == null) return;

            // Apply attack speed buff as a status effect
            if (ability.attackSpeedBonus > 0)
            {
                float duration = ability.duration * GameConstants.Units.STAR_MULTIPLIERS[caster.source.starLevel];
                var buff = new CombatStatusEffect
                {
                    name = ability.abilityName,
                    duration = duration,
                    remainingDuration = duration,
                    attackSpeedBonus = ability.attackSpeedBonus
                };
                target.statusEffects.Add(buff);

                // Apply the attack speed bonus immediately
                target.stats.attackSpeed += ability.attackSpeedBonus;
            }
        }

        private CombatUnit GetAbilityTarget(CombatUnit caster, AbilityTargeting targeting)
        {
            var allies = GetAliveUnits(caster.team);
            var enemies = GetAliveUnits(caster.team == Team.Player ? Team.Enemy : Team.Player);

            switch (targeting)
            {
                case AbilityTargeting.CurrentTarget:
                    return caster.target;

                case AbilityTargeting.LowestHealthEnemy:
                    return GetLowestHealth(enemies);

                case AbilityTargeting.HighestHealthEnemy:
                    return GetHighestHealth(enemies);

                case AbilityTargeting.LowestHealthAlly:
                    return GetLowestHealth(allies);

                case AbilityTargeting.Self:
                    return caster;

                case AbilityTargeting.RandomEnemy:
                    return enemies.Count > 0 ? enemies[Random.Range(0, enemies.Count)] : null;

                default:
                    return caster.target;
            }
        }

        private CombatUnit GetLowestHealth(List<CombatUnit> units)
        {
            CombatUnit lowest = null;
            float lowestPercent = float.MaxValue;

            foreach (var unit in units)
            {
                float percent = (float)unit.currentHealth / unit.stats.health;
                if (percent < lowestPercent)
                {
                    lowestPercent = percent;
                    lowest = unit;
                }
            }

            return lowest;
        }

        private CombatUnit GetHighestHealth(List<CombatUnit> units)
        {
            CombatUnit highest = null;
            int highestHp = int.MinValue;

            foreach (var unit in units)
            {
                if (unit.currentHealth > highestHp)
                {
                    highestHp = unit.currentHealth;
                    highest = unit;
                }
            }

            return highest;
        }

        /// <summary>
        /// Apply on-hit effects from items and traits
        /// </summary>
        private void ApplyOnHitEffects(CombatUnit attacker, CombatUnit target, int damage)
        {
            foreach (var item in attacker.source.equippedItems)
            {
                switch (item.effect)
                {
                    case ItemEffect.Lifesteal:
                        int heal = Mathf.RoundToInt(damage * item.effectValue1);
                        attacker.currentHealth = Mathf.Min(attacker.currentHealth + heal, attacker.stats.health);
                        break;

                    case ItemEffect.Burn:
                        // Apply burn DoT
                        var burn = new CombatStatusEffect
                        {
                            name = "Burn",
                            duration = item.effectValue2,
                            remainingDuration = item.effectValue2,
                            damagePerTick = Mathf.RoundToInt(item.effectValue1 / (item.effectValue2 / tickRate)),
                            damageType = DamageType.Fire
                        };
                        target.statusEffects.Add(burn);
                        break;

                    case ItemEffect.CriticalStrike:
                        // Already handled in attack calculation
                        break;
                }
            }
        }

        /// <summary>
        /// Process status effects on a unit
        /// </summary>
        private void ProcessStatusEffects(CombatUnit unit)
        {
            for (int i = unit.statusEffects.Count - 1; i >= 0; i--)
            {
                var effect = unit.statusEffects[i];
                effect.remainingDuration -= tickRate;

                // Apply DoT
                if (effect.damagePerTick > 0)
                {
                    unit.currentHealth -= effect.damagePerTick;
                    if (unit.currentHealth <= 0)
                    {
                        KillUnit(unit, null);
                        return;
                    }
                }

                // Apply HoT
                if (effect.healPerTick > 0)
                {
                    unit.currentHealth = Mathf.Min(unit.currentHealth + effect.healPerTick, unit.stats.health);
                }

                // Remove expired effects
                if (effect.remainingDuration <= 0)
                {
                    // Remove attack speed bonus when buff expires
                    if (effect.attackSpeedBonus > 0)
                    {
                        unit.stats.attackSpeed -= effect.attackSpeedBonus;
                    }
                    unit.statusEffects.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Kill a unit
        /// </summary>
        private void KillUnit(CombatUnit unit, CombatUnit killer)
        {
            unit.isDead = true;
            unit.currentHealth = 0;
            
            OnUnitDied?.Invoke(unit);

            // Check for revive items
            foreach (var item in unit.source.equippedItems)
            {
                if (item.effect == ItemEffect.Revive && !unit.hasUsedRevive)
                {
                    unit.isDead = false;
                    unit.currentHealth = Mathf.RoundToInt(item.effectValue1);
                    unit.hasUsedRevive = true;
                    Debug.Log($"{unit.source.template.unitName} revived!");
                    return;
                }
            }

            Debug.Log($"{unit.source.template.unitName} died!");
        }

        /// <summary>
        /// Apply crest effects to a combat unit
        /// </summary>
        private void ApplyCrests(CombatUnit unit, List<CrestData> crests)
        {
            foreach (var crest in crests)
            {
                // Apply stat bonuses
                unit.stats.health += crest.bonusHealth;
                unit.stats.attack += crest.bonusAttack;
                unit.stats.armor += crest.bonusArmor;
                unit.stats.magicResist += crest.bonusMagicResist;
                unit.stats.attackSpeed += crest.bonusAttackSpeed;

                // Apply shield at combat start
                if (crest.effect == CrestEffect.AllUnitsShield)
                {
                    unit.currentShield += Mathf.RoundToInt(crest.effectValue1);
                }
            }

            // Update current health to match new max
            unit.currentHealth = unit.stats.health;
        }

        /// <summary>
        /// Get alive units of a team
        /// </summary>
        private List<CombatUnit> GetAliveUnits(Team team)
        {
            var result = new List<CombatUnit>();
            foreach (var unit in allUnits)
            {
                if (!unit.isDead && unit.team == team)
                {
                    result.Add(unit);
                }
            }
            return result;
        }

        /// <summary>
        /// Get all units of a team
        /// </summary>
        private List<CombatUnit> GetTeamUnits(Team team)
        {
            var result = new List<CombatUnit>();
            foreach (var unit in allUnits)
            {
                if (unit.team == team)
                {
                    result.Add(unit);
                }
            }
            return result;
        }

        /// <summary>
        /// End combat and return results
        /// </summary>
        private void EndCombat(bool playerWon)
        {
            isInCombat = false;

            var result = new CombatResult
            {
                victory = playerWon,
                duration = combatTime,
                playerSurvivors = GetAliveUnits(Team.Player).Count,
                enemySurvivors = GetAliveUnits(Team.Enemy).Count
            };

            // Calculate damage to player health
            if (!playerWon)
            {
                result.damageToPlayer = 2 + GetAliveUnits(Team.Enemy).Count;
            }

            Debug.Log($"Combat ended: {(playerWon ? "Victory" : "Defeat")} in {combatTime:F1}s");
            
            OnCombatEnd?.Invoke(result);
        }
    }

    /// <summary>
    /// Runtime combat unit state
    /// </summary>
    public class CombatUnit
    {
        public UnitInstance source;
        public Team team;
        public Vector2Int position;
        public UnitStats stats;
        
        public int currentHealth;
        public int currentMana;
        public int currentShield;
        
        public CombatUnit target;
        public float attackCooldown;
        public float moveCooldown;
        public bool hasActedThisTick;
        public bool isDead;
        public bool hasUsedRevive;
        
        public List<CombatStatusEffect> statusEffects = new List<CombatStatusEffect>();

        public CombatUnit(UnitInstance source, Team team, Vector2Int position)
        {
            this.source = source;
            this.team = team;
            this.position = position;
            this.stats = source.currentStats.Clone();
            this.currentHealth = stats.health;
            this.currentMana = stats.startingMana;
            this.currentShield = 0;
        }
    }

    public enum Team
    {
        Player,
        Enemy
    }

    public class CombatStatusEffect
    {
        public string name;
        public float duration;
        public float remainingDuration;
        public int damagePerTick;
        public int healPerTick;
        public DamageType damageType;
        public bool isStun;
        public float attackSpeedBonus; // For attack speed buffs
    }

    public class CombatResult
    {
        public bool victory;
        public float duration;
        public int playerSurvivors;
        public int enemySurvivors;
        public int damageToPlayer;
    }
}
