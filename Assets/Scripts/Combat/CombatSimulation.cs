using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Hex;

namespace Crestforge.Combat
{
    /// <summary>
    /// Standalone combat simulation that can run independently.
    /// Used for opponent board battles in multi-board PvP.
    /// Uses a seeded RNG for deterministic results - the same seed will always
    /// produce the same combat outcome, ensuring consistency across clients.
    /// </summary>
    public class CombatSimulation
    {
        // Combat state
        public bool isInCombat;
        public float combatTime;
        public List<CombatUnit> allUnits = new List<CombatUnit>();

        // Settings
        public float tickRate = 0.1f;
        private float tickTimer;
        private float startupTimer;
        private float combatStartDelay = 0.5f;

        // Identification
        public string ownerId;
        public string ownerName;

        // Seeded random for deterministic combat
        private System.Random seededRandom;
        private int combatSeed;

        // Events
        public System.Action<CombatSimulation, CombatResult> OnCombatEnd;
        public System.Action<CombatUnit, CombatUnit, int> OnDamageDealt;
        public System.Action<CombatUnit> OnUnitDied;
        public System.Action<CombatUnit> OnAbilityCast;
        public System.Action<CombatUnit, Vector2Int> OnUnitMoved;

        public CombatSimulation(string ownerId, string ownerName, int seed = -1)
        {
            this.ownerId = ownerId;
            this.ownerName = ownerName;

            // Use provided seed or generate one based on owner ID hash
            this.combatSeed = seed >= 0 ? seed : ownerId.GetHashCode();
            this.seededRandom = new System.Random(combatSeed);
        }

        /// <summary>
        /// Get the seed used for this combat (for debugging/replay)
        /// </summary>
        public int CombatSeed => combatSeed;

        /// <summary>
        /// Set a specific seed for deterministic combat replay
        /// Must be called before StartCombat()
        /// </summary>
        public void SetSeed(int seed)
        {
            this.combatSeed = seed;
            this.seededRandom = new System.Random(seed);
        }

        /// <summary>
        /// Get a random integer in range [0, maxExclusive) using seeded RNG
        /// </summary>
        private int GetRandomInt(int maxExclusive)
        {
            return seededRandom.Next(maxExclusive);
        }

        /// <summary>
        /// Get a random float in range [0, 1) using seeded RNG
        /// </summary>
        private float GetRandomFloat()
        {
            return (float)seededRandom.NextDouble();
        }

        /// <summary>
        /// Start a new combat between owner and enemy boards
        /// </summary>
        public void StartCombat(UnitInstance[,] ownerBoard, UnitInstance[,] enemyBoard,
            List<CrestData> ownerCrests, List<CrestData> enemyCrests)
        {
            allUnits.Clear();
            combatTime = 0;
            isInCombat = true;
            tickTimer = 0;
            startupTimer = 0;

            // Create combat units for owner (bottom half of battlefield)
            for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
            {
                for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                {
                    if (ownerBoard[x, y] != null)
                    {
                        var combatUnit = new CombatUnit(ownerBoard[x, y], Team.Player, new Vector2Int(x, y));
                        ApplyCrests(combatUnit, ownerCrests);
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

            Debug.Log($"[{ownerName}] Combat started: {GetTeamUnits(Team.Player).Count} vs {GetTeamUnits(Team.Enemy).Count}");
        }

        /// <summary>
        /// Update combat simulation - call this every frame
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!isInCombat) return;

            // Wait for startup delay before processing combat
            if (startupTimer < combatStartDelay)
            {
                startupTimer += deltaTime;
                return;
            }

            tickTimer += deltaTime;

            while (tickTimer >= tickRate)
            {
                tickTimer -= tickRate;
                ProcessTick();
            }

            combatTime += deltaTime;
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
                        var oldPos = unit.position;
                        MoveTowardsTarget(unit);
                        if (unit.position != oldPos)
                        {
                            OnUnitMoved?.Invoke(unit, unit.position);
                        }
                        unit.moveCooldown = GameConstants.Combat.MOVE_COOLDOWN;
                        unit.attackCooldown = Mathf.Max(unit.attackCooldown, GameConstants.Combat.ATTACK_DELAY_AFTER_MOVE);
                    }
                }

                unit.hasActedThisTick = true;
            }
        }

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

        private void MoveTowardsTarget(CombatUnit unit)
        {
            if (unit.target == null) return;

            var blocked = new HashSet<Vector2Int>();
            foreach (var other in allUnits)
            {
                if (!other.isDead && other != unit)
                {
                    blocked.Add(other.position);
                }
            }

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

        private void PerformAttack(CombatUnit attacker, CombatUnit target)
        {
            int damage = attacker.stats.attack;
            float armorReduction = target.stats.armor / (float)(target.stats.armor + 100);
            damage = Mathf.RoundToInt(damage * (1 - armorReduction));

            DealDamage(attacker, target, damage, DamageType.Physical);

            float effectiveAttackSpeed = attacker.stats.attackSpeed * GameConstants.Combat.GLOBAL_ATTACK_SPEED_MULTIPLIER;
            float attackInterval = 1f / effectiveAttackSpeed;
            attacker.attackCooldown = attackInterval;

            attacker.currentMana = Mathf.Min(
                attacker.currentMana + GameConstants.Combat.MANA_PER_ATTACK,
                attacker.stats.maxMana);

            if (attacker.currentMana >= attacker.stats.maxMana)
            {
                CastAbility(attacker);
            }

            ApplyOnHitEffects(attacker, target, damage);
        }

        private void DealDamage(CombatUnit source, CombatUnit target, int amount, DamageType type)
        {
            if (type != DamageType.Physical)
            {
                float mrReduction = target.stats.magicResist / (float)(target.stats.magicResist + 100);
                amount = Mathf.RoundToInt(amount * (1 - mrReduction));
            }

            if (target.currentShield > 0)
            {
                int absorbed = Mathf.Min(target.currentShield, amount);
                target.currentShield -= absorbed;
                amount -= absorbed;
            }

            target.currentHealth = Mathf.Max(0, target.currentHealth - amount);

            target.currentMana = Mathf.Min(
                target.currentMana + GameConstants.Combat.MANA_PER_DAMAGE_TAKEN,
                target.stats.maxMana);

            OnDamageDealt?.Invoke(source, target, amount);

            if (target.currentHealth <= 0)
            {
                KillUnit(target, source);
            }
        }

        private void CastAbility(CombatUnit caster)
        {
            var ability = caster.source.template.ability;
            if (ability == null) return;

            caster.currentMana = 0;
            OnAbilityCast?.Invoke(caster);

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

            int heal = Mathf.RoundToInt(damage * 0.5f);
            caster.currentHealth = Mathf.Min(caster.currentHealth + heal, caster.stats.health);
        }

        private void CastBuffAbility(CombatUnit caster, AbilityData ability)
        {
            var target = GetAbilityTarget(caster, ability.targeting);
            if (target == null) return;

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
                    return enemies.Count > 0 ? enemies[GetRandomInt(enemies.Count)] : null;
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
                }
            }
        }

        private void ProcessStatusEffects(CombatUnit unit)
        {
            for (int i = unit.statusEffects.Count - 1; i >= 0; i--)
            {
                var effect = unit.statusEffects[i];
                effect.remainingDuration -= tickRate;

                if (effect.damagePerTick > 0)
                {
                    unit.currentHealth -= effect.damagePerTick;
                    if (unit.currentHealth <= 0)
                    {
                        KillUnit(unit, null);
                        return;
                    }
                }

                if (effect.healPerTick > 0)
                {
                    unit.currentHealth = Mathf.Min(unit.currentHealth + effect.healPerTick, unit.stats.health);
                }

                if (effect.remainingDuration <= 0)
                {
                    if (effect.attackSpeedBonus > 0)
                    {
                        unit.stats.attackSpeed -= effect.attackSpeedBonus;
                    }
                    unit.statusEffects.RemoveAt(i);
                }
            }
        }

        private void KillUnit(CombatUnit unit, CombatUnit killer)
        {
            unit.isDead = true;
            unit.currentHealth = 0;

            OnUnitDied?.Invoke(unit);

            foreach (var item in unit.source.equippedItems)
            {
                if (item.effect == ItemEffect.Revive && !unit.hasUsedRevive)
                {
                    unit.isDead = false;
                    unit.currentHealth = Mathf.RoundToInt(item.effectValue1);
                    unit.hasUsedRevive = true;
                    return;
                }
            }
        }

        private void ApplyCrests(CombatUnit unit, List<CrestData> crests)
        {
            if (crests == null) return;

            foreach (var crest in crests)
            {
                unit.stats.health += crest.bonusHealth;
                unit.stats.attack += crest.bonusAttack;
                unit.stats.armor += crest.bonusArmor;
                unit.stats.magicResist += crest.bonusMagicResist;
                unit.stats.attackSpeed += crest.bonusAttackSpeed;

                if (crest.effect == CrestEffect.AllUnitsShield)
                {
                    unit.currentShield += Mathf.RoundToInt(crest.effectValue1);
                }
            }

            unit.currentHealth = unit.stats.health;
        }

        public List<CombatUnit> GetAliveUnits(Team team)
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

        private void EndCombat(bool ownerWon)
        {
            isInCombat = false;

            var result = new CombatResult
            {
                victory = ownerWon,
                duration = combatTime,
                playerSurvivors = GetAliveUnits(Team.Player).Count,
                enemySurvivors = GetAliveUnits(Team.Enemy).Count
            };

            if (!ownerWon)
            {
                result.damageToPlayer = 2 + GetAliveUnits(Team.Enemy).Count;
            }

            Debug.Log($"[{ownerName}] Combat ended: {(ownerWon ? "Victory" : "Defeat")} in {combatTime:F1}s");

            OnCombatEnd?.Invoke(this, result);
        }

        /// <summary>
        /// Force end combat early (e.g., when player combat ends)
        /// </summary>
        public void ForceEndCombat()
        {
            if (isInCombat)
            {
                isInCombat = false;
                var playerUnits = GetAliveUnits(Team.Player);
                EndCombat(playerUnits.Count > 0);
            }
        }
    }
}
