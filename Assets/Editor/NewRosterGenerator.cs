using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Crestforge.Core;
using Crestforge.Data;

/// <summary>
/// Editor script to generate all 40 units and 16 traits for the new CrestForge roster.
/// Run from menu: Crestforge/Generate New Roster
/// </summary>
public class NewRosterGenerator : EditorWindow
{
    [MenuItem("Crestforge/Generate New Roster")]
    public static void ShowWindow()
    {
        GetWindow<NewRosterGenerator>("Roster Generator");
    }

    private bool generateTraits = true;
    private bool generateUnits = true;
    private bool deleteOldAssets = false;

    private void OnGUI()
    {
        GUILayout.Label("New Roster Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("This will generate ScriptableObjects for:", EditorStyles.label);
        GUILayout.Label("• 16 Traits (14 shared + 2 unique)", EditorStyles.label);
        GUILayout.Label("• 40 Units with stats and abilities", EditorStyles.label);
        GUILayout.Space(10);

        generateTraits = EditorGUILayout.Toggle("Generate Traits", generateTraits);
        generateUnits = EditorGUILayout.Toggle("Generate Units", generateUnits);
        deleteOldAssets = EditorGUILayout.Toggle("Delete Old Assets First", deleteOldAssets);

        GUILayout.Space(20);

        if (GUILayout.Button("Generate!", GUILayout.Height(40)))
        {
            Generate();
        }
    }

    private void Generate()
    {
        string traitPath = "Assets/Resources/ScriptableObjects/NewTraits";
        string unitPath = "Assets/Resources/ScriptableObjects/NewUnits";

        // Create directories if they don't exist
        if (!Directory.Exists(traitPath))
            Directory.CreateDirectory(traitPath);
        if (!Directory.Exists(unitPath))
            Directory.CreateDirectory(unitPath);

        Dictionary<string, TraitData> traits = new Dictionary<string, TraitData>();

        if (generateTraits)
        {
            if (deleteOldAssets)
            {
                DeleteAssetsInFolder(traitPath);
            }
            traits = GenerateTraits(traitPath);
            Debug.Log($"Generated {traits.Count} traits");
        }
        else
        {
            // Load existing traits
            traits = LoadExistingTraits(traitPath);
        }

        if (generateUnits)
        {
            if (deleteOldAssets)
            {
                DeleteAssetsInFolder(unitPath);
            }
            int unitCount = GenerateUnits(unitPath, traits);
            Debug.Log($"Generated {unitCount} units");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Complete", "Roster generation complete!", "OK");
    }

    private void DeleteAssetsInFolder(string path)
    {
        if (Directory.Exists(path))
        {
            string[] files = Directory.GetFiles(path, "*.asset");
            foreach (string file in files)
            {
                AssetDatabase.DeleteAsset(file);
            }
        }
    }

    private Dictionary<string, TraitData> LoadExistingTraits(string path)
    {
        var traits = new Dictionary<string, TraitData>();
        string[] guids = AssetDatabase.FindAssets("t:TraitData", new[] { path });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            TraitData trait = AssetDatabase.LoadAssetAtPath<TraitData>(assetPath);
            if (trait != null)
            {
                traits[trait.traitId] = trait;
            }
        }
        return traits;
    }

    #region Trait Generation

    private Dictionary<string, TraitData> GenerateTraits(string path)
    {
        var traits = new Dictionary<string, TraitData>();

        // Shared traits (2/4 breakpoints)
        CreateTrait(traits, path, "Attuned", "At game start, a random element pair is chosen. Attuned units deal that damage type and gain bonus damage.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("+15% damage with attuned element", TraitEffect.AbilityDamageBonus, 0.15f),
                CreateBonus("+30% damage with attuned element", TraitEffect.AbilityDamageBonus, 0.30f)
            });

        CreateTrait(traits, path, "Forged", "Forged units gain permanent stats for every round of combat they participate in.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("+2 AD, +2 AP per round", TraitEffect.None, 0, 0, 0, 2, 2),
                CreateBonus("+4 AD, +4 AP, +20 HP per round", TraitEffect.None, 0, 0, 0, 4, 4, 20)
            });

        CreateTrait(traits, path, "Scavenger", "After each round, chance to gain a random unit. Cost equals your level minus 1 (max 4).", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("50% chance to gain a unit", TraitEffect.None, 0.5f),
                CreateBonus("100% chance to gain a unit", TraitEffect.None, 1.0f)
            });

        CreateTrait(traits, path, "Invigorating", "Adjacent allies heal HP per second.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("Heal 3 HP/sec to adjacent allies", TraitEffect.Regeneration, 3f),
                CreateBonus("Heal 6 HP/sec to adjacent allies", TraitEffect.Regeneration, 6f)
            });

        CreateTrait(traits, path, "Reflective", "Reflective units reflect damage back to attackers.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("Reflect 15% of damage taken", TraitEffect.Thorns, 0.15f),
                CreateBonus("Reflect 30% of damage taken", TraitEffect.Thorns, 0.30f)
            });

        CreateTrait(traits, path, "Mitigation", "Mitigation units take reduced damage from all sources.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("Take 10% less damage", TraitEffect.DamageReductionLowHealth, 0, 0.10f),
                CreateBonus("Take 20% less damage", TraitEffect.DamageReductionLowHealth, 0, 0.20f)
            });

        CreateTrait(traits, path, "Bruiser", "Bruiser units gain bonus maximum HP.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("+150 bonus HP", TraitEffect.None, 0, 0, 0, 0, 0, 150),
                CreateBonus("+350 bonus HP", TraitEffect.None, 0, 0, 0, 0, 0, 350)
            });

        CreateTrait(traits, path, "Overkill", "When an Overkill unit kills an enemy, excess damage splashes to nearby enemy.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("50% of overkill damage splashes", TraitEffect.None, 0.5f),
                CreateBonus("100% of overkill damage splashes", TraitEffect.None, 1.0f)
            });

        CreateTrait(traits, path, "Gigamega", "Abilities cost more mana but deal increased damage.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("+20% mana cost, +25% ability damage", TraitEffect.AbilityDamageBonus, 0.25f, 0.20f),
                CreateBonus("+20% mana cost, +40% ability damage", TraitEffect.AbilityDamageBonus, 0.40f, 0.20f)
            });

        CreateTrait(traits, path, "FirstBlood", "First attack each combat deals bonus damage.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("First attack deals +50% damage", TraitEffect.None, 0.5f),
                CreateBonus("First attack deals +100% damage", TraitEffect.None, 1.0f)
            });

        CreateTrait(traits, path, "Momentum", "Each kill grants movement and attack speed, stacking up to 3 times.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("+10% speed per kill (max 30%)", TraitEffect.None, 0.10f),
                CreateBonus("+15% speed per kill (max 45%)", TraitEffect.None, 0.15f)
            });

        CreateTrait(traits, path, "Cleave", "Attacks hit enemies adjacent to target.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("Cleave for 25% damage", TraitEffect.None, 0.25f),
                CreateBonus("Cleave for 50% damage", TraitEffect.None, 0.50f)
            });

        CreateTrait(traits, path, "Fury", "Gain attack speed after every attack, stacking up to 15 times.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("+3% attack speed per attack", TraitEffect.None, 0.03f),
                CreateBonus("+5% attack speed per attack", TraitEffect.None, 0.05f)
            });

        CreateTrait(traits, path, "Volatile", "Volatile units explode on death, dealing damage to nearby enemies.", false,
            new int[] { 2, 4 },
            new TraitBonus[] {
                CreateBonus("Explode for 100 damage on death", TraitEffect.ExplodeOnDeath, 100f),
                CreateBonus("Explode for 250 damage on death", TraitEffect.ExplodeOnDeath, 250f)
            });

        // Unique traits (1 breakpoint)
        CreateTrait(traits, path, "Treasure", "After winning a round, gain a random reward: gold, item component, or consumable.", true,
            new int[] { 1 },
            new TraitBonus[] {
                CreateBonus("Gain random reward on win", TraitEffect.None, 1f)
            });

        CreateTrait(traits, path, "Crestmaker", "Crafts a crest token after 3 rounds. Crafts a major crest token after 6 rounds.", true,
            new int[] { 1 },
            new TraitBonus[] {
                CreateBonus("Craft crest tokens over time", TraitEffect.None, 3f, 6f)
            });

        return traits;
    }

    private void CreateTrait(Dictionary<string, TraitData> traits, string path, string name, string description, bool isUnique, int[] thresholds, TraitBonus[] bonuses)
    {
        TraitData trait = ScriptableObject.CreateInstance<TraitData>();
        trait.traitId = name.ToLower();
        trait.traitName = name;
        trait.description = description;
        trait.isUnique = isUnique;
        trait.tierThresholds = thresholds;
        trait.tierBonuses = bonuses;

        string assetPath = $"{path}/{name}.asset";
        AssetDatabase.CreateAsset(trait, assetPath);
        traits[trait.traitId] = trait;
    }

    private TraitBonus CreateBonus(string desc, TraitEffect effect, float val1 = 0, float val2 = 0, float val3 = 0, int bonusAtk = 0, int bonusAP = 0, int bonusHP = 0)
    {
        return new TraitBonus
        {
            bonusDescription = desc,
            specialEffect = effect,
            effectValue1 = val1,
            effectValue2 = val2,
            effectValue3 = val3,
            bonusAttack = bonusAtk,
            bonusHealth = bonusHP
        };
    }

    #endregion

    #region Unit Generation

    private int GenerateUnits(string path, Dictionary<string, TraitData> traits)
    {
        int count = 0;

        // TIER 1 (1-cost)
        CreateUnit(path, traits, "BlueSlime", "Blue Slime", 1, DamageType.Elemental, new[] { "Bruiser", "Mitigation" },
            new UnitStats { health = 550, attack = 40, armor = 25, magicResist = 25, attackSpeed = 0.6f, range = 1, maxMana = 60 },
            new AbilityData { abilityName = "Gel Shield", description = "Grants self a shield equal to 30% max HP for 4 seconds", type = AbilityType.Shield, baseShieldAmount = 165, duration = 4f, targeting = AbilityTargeting.Self });
        count++;

        CreateUnit(path, traits, "RedSlime", "Red Slime", 1, DamageType.Elemental, new[] { "Scavenger", "Volatile" },
            new UnitStats { health = 450, attack = 45, armor = 20, magicResist = 20, attackSpeed = 0.65f, range = 1, maxMana = 50 },
            new AbilityData { abilityName = "Volatile Burst", description = "Explodes dealing 150 damage to all adjacent enemies", type = AbilityType.AreaDamage, baseDamage = 150, damageType = DamageType.Elemental, radius = 1, targeting = AbilityTargeting.NearbyEnemies });
        count++;

        CreateUnit(path, traits, "Bat", "Bat", 1, DamageType.Physical, new[] { "Fury", "Momentum" },
            new UnitStats { health = 400, attack = 55, armor = 15, magicResist = 15, attackSpeed = 0.9f, range = 1, maxMana = 40 },
            new AbilityData { abilityName = "Sonic Screech", description = "Screeches dealing 100 damage to nearby enemies, reducing attack speed by 20%", type = AbilityType.AreaDamage, baseDamage = 100, damageType = DamageType.Physical, radius = 1, duration = 3f, targeting = AbilityTargeting.NearbyEnemies });
        count++;

        CreateUnit(path, traits, "Mushroom", "Mushroom", 1, DamageType.Elemental, new[] { "Invigorating", "Volatile" },
            new UnitStats { health = 480, attack = 35, armor = 20, magicResist = 30, attackSpeed = 0.6f, range = 2, maxMana = 60 },
            new AbilityData { abilityName = "Healing Spores", description = "Heals all adjacent allies for 120 HP over 4 seconds", type = AbilityType.Heal, baseHealing = 120, duration = 4f, targeting = AbilityTargeting.NearbyAllies });
        count++;

        CreateUnit(path, traits, "Crawler", "Crawler", 1, DamageType.Physical, new[] { "Scavenger", "Fury" },
            new UnitStats { health = 420, attack = 50, armor = 25, magicResist = 15, attackSpeed = 0.75f, range = 1, maxMana = 50 },
            new AbilityData { abilityName = "Frenzy", description = "Gains 50% attack speed for 5 seconds", type = AbilityType.Buff, attackSpeedBonus = 0.5f, duration = 5f, targeting = AbilityTargeting.Self });
        count++;

        CreateUnit(path, traits, "LittleDemon", "Little Demon", 1, DamageType.Dark, new[] { "FirstBlood", "Overkill", "Attuned" },
            new UnitStats { health = 400, attack = 60, armor = 15, magicResist = 20, attackSpeed = 0.8f, range = 2, maxMana = 45 },
            new AbilityData { abilityName = "Shadow Bolt", description = "Fires a dark bolt dealing 180 damage", type = AbilityType.Damage, baseDamage = 180, damageType = DamageType.Dark, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "GreenSpider", "Green Spider", 1, DamageType.Dark, new[] { "Reflective", "Mitigation" },
            new UnitStats { health = 500, attack = 40, armor = 30, magicResist = 25, attackSpeed = 0.65f, range = 1, maxMana = 55 },
            new AbilityData { abilityName = "Poison Fang", description = "Bites target dealing 80 damage + 100 poison over 4s", type = AbilityType.Damage, baseDamage = 180, damageType = DamageType.Dark, duration = 4f, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "Starfish", "Starfish", 1, DamageType.Elemental, new[] { "Invigorating", "Reflective" },
            new UnitStats { health = 520, attack = 35, armor = 20, magicResist = 25, attackSpeed = 0.55f, range = 1, maxMana = 65 },
            new AbilityData { abilityName = "Regeneration Wave", description = "Heals self and adjacent allies for 150 HP", type = AbilityType.Heal, baseHealing = 150, targeting = AbilityTargeting.NearbyAllies });
        count++;

        CreateUnit(path, traits, "RatAssassin", "Rat Assassin", 1, DamageType.Physical, new[] { "FirstBlood", "Momentum" },
            new UnitStats { health = 380, attack = 65, armor = 15, magicResist = 15, attackSpeed = 0.85f, range = 1, maxMana = 40 },
            new AbilityData { abilityName = "Backstab", description = "Dashes to furthest enemy dealing 200 damage", type = AbilityType.Movement, baseDamage = 200, damageType = DamageType.Physical, targeting = AbilityTargeting.BacklineEnemy });
        count++;

        CreateUnit(path, traits, "Archer", "Archer", 1, DamageType.Physical, new[] { "FirstBlood", "Fury" },
            new UnitStats { health = 400, attack = 55, armor = 15, magicResist = 15, attackSpeed = 0.8f, range = 4, maxMana = 50 },
            new AbilityData { abilityName = "Piercing Arrow", description = "Fires a powerful shot at lowest health enemy dealing 220 damage", type = AbilityType.Damage, baseDamage = 220, damageType = DamageType.Physical, targeting = AbilityTargeting.LowestHealthEnemy });
        count++;

        // TIER 2 (2-cost)
        CreateUnit(path, traits, "Fishman", "Fishman", 2, DamageType.Physical, new[] { "Scavenger", "Cleave" },
            new UnitStats { health = 600, attack = 55, armor = 30, magicResist = 20, attackSpeed = 0.7f, range = 1, maxMana = 60 },
            new AbilityData { abilityName = "Tidal Slash", description = "Slashes dealing 180 damage to up to 3 enemies", type = AbilityType.AreaDamage, baseDamage = 180, damageType = DamageType.Physical, projectileCount = 3, targeting = AbilityTargeting.NearbyEnemies });
        count++;

        CreateUnit(path, traits, "CrabMonster", "Crab Monster", 2, DamageType.Physical, new[] { "Bruiser", "Reflective" },
            new UnitStats { health = 700, attack = 50, armor = 45, magicResist = 25, attackSpeed = 0.55f, range = 1, maxMana = 70 },
            new AbilityData { abilityName = "Harden Shell", description = "Gains 80 armor and reflects 30% damage for 4 seconds", type = AbilityType.Buff, duration = 4f, targeting = AbilityTargeting.Self });
        count++;

        CreateUnit(path, traits, "Salamander", "Salamander", 2, DamageType.Elemental, new[] { "Forged", "Overkill" },
            new UnitStats { health = 550, attack = 65, armor = 25, magicResist = 30, attackSpeed = 0.7f, range = 2, maxMana = 55 },
            new AbilityData { abilityName = "Flame Breath", description = "Breathes fire dealing 200 damage in a cone", type = AbilityType.AreaDamage, baseDamage = 200, damageType = DamageType.Elemental, radius = 2, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "FlowerMonster", "Flower Monster", 2, DamageType.Elemental, new[] { "Invigorating", "Gigamega" },
            new UnitStats { health = 500, attack = 45, armor = 20, magicResist = 35, attackSpeed = 0.6f, range = 3, startingMana = 20, maxMana = 80 },
            new AbilityData { abilityName = "Nature's Bloom", description = "Heals lowest health ally for 350 HP", type = AbilityType.Heal, baseHealing = 350, targeting = AbilityTargeting.LowestHealthAlly });
        count++;

        CreateUnit(path, traits, "EvilPlant", "Evil Plant", 2, DamageType.Dark, new[] { "Invigorating", "Volatile" },
            new UnitStats { health = 580, attack = 50, armor = 25, magicResist = 30, attackSpeed = 0.6f, range = 2, maxMana = 60 },
            new AbilityData { abilityName = "Toxic Bloom", description = "Deals 150 damage to nearby enemies and heals self for 50%", type = AbilityType.DamageAndHeal, baseDamage = 150, baseHealing = 75, damageType = DamageType.Dark, radius = 1, targeting = AbilityTargeting.NearbyEnemies });
        count++;

        CreateUnit(path, traits, "WormMonster", "Worm Monster", 2, DamageType.Dark, new[] { "Bruiser", "Mitigation" },
            new UnitStats { health = 750, attack = 45, armor = 35, magicResist = 30, attackSpeed = 0.5f, range = 1, maxMana = 70 },
            new AbilityData { abilityName = "Burrow Strike", description = "Burrows underground for 1s, emerges at target dealing 200 damage", type = AbilityType.Movement, baseDamage = 200, damageType = DamageType.Dark, duration = 1f, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "BattleBee", "Battle Bee", 2, DamageType.Elemental, new[] { "Fury", "Cleave" },
            new UnitStats { health = 480, attack = 60, armor = 20, magicResist = 20, attackSpeed = 0.85f, range = 1, maxMana = 45 },
            new AbilityData { abilityName = "Swarm Strike", description = "Rapidly stings target 4 times for 60 damage each", type = AbilityType.Damage, baseDamage = 240, damageType = DamageType.Elemental, projectileCount = 4, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "Golem", "Golem", 2, DamageType.Physical, new[] { "Forged", "Bruiser" },
            new UnitStats { health = 800, attack = 50, armor = 40, magicResist = 25, attackSpeed = 0.5f, range = 1, maxMana = 75 },
            new AbilityData { abilityName = "Ground Slam", description = "Slams the ground dealing 220 damage to all adjacent enemies", type = AbilityType.AreaDamage, baseDamage = 220, damageType = DamageType.Physical, radius = 1, targeting = AbilityTargeting.NearbyEnemies });
        count++;

        CreateUnit(path, traits, "ChestMonster", "Chest Monster", 2, DamageType.Physical, new[] { "Treasure" },
            new UnitStats { health = 600, attack = 55, armor = 35, magicResist = 25, attackSpeed = 0.6f, range = 1, maxMana = 60 },
            new AbilityData { abilityName = "Gold Toss", description = "Throws coins dealing 250 damage. If kills, gain 1 gold", type = AbilityType.Damage, baseDamage = 250, damageType = DamageType.Physical, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "Blacksmith", "Blacksmith", 2, DamageType.Holy, new[] { "Crestmaker" },
            new UnitStats { health = 650, attack = 50, armor = 30, magicResist = 30, attackSpeed = 0.6f, range = 1, maxMana = 80 },
            new AbilityData { abilityName = "Blessed Weapons", description = "Buffs all allies' attack damage by 25% for 5 seconds", type = AbilityType.Buff, attackSpeedBonus = 0.25f, duration = 5f, targeting = AbilityTargeting.AllAllies });
        count++;

        // TIER 3 (3-cost)
        CreateUnit(path, traits, "LizardWarrior", "Lizard Warrior", 3, DamageType.Physical, new[] { "Cleave", "Forged" },
            new UnitStats { health = 800, attack = 75, armor = 40, magicResist = 25, attackSpeed = 0.7f, range = 1, maxMana = 65 },
            new AbilityData { abilityName = "Tail Sweep", description = "Deals 280 damage to adjacent enemies, knocking back 1 hex", type = AbilityType.AreaDamage, baseDamage = 280, damageType = DamageType.Physical, radius = 1, targeting = AbilityTargeting.NearbyEnemies });
        count++;

        CreateUnit(path, traits, "Werewolf", "Werewolf", 3, DamageType.Physical, new[] { "Momentum", "FirstBlood" },
            new UnitStats { health = 750, attack = 85, armor = 30, magicResist = 25, attackSpeed = 0.85f, range = 1, maxMana = 55 },
            new AbilityData { abilityName = "Savage Leap", description = "Leaps to target dealing 300 damage, gaining 40% attack speed for 4s", type = AbilityType.Movement, baseDamage = 300, damageType = DamageType.Physical, attackSpeedBonus = 0.4f, duration = 4f, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "IceGolem", "Ice Golem", 3, DamageType.Elemental, new[] { "Bruiser", "Attuned" },
            new UnitStats { health = 950, attack = 55, armor = 45, magicResist = 40, attackSpeed = 0.5f, range = 1, maxMana = 80 },
            new AbilityData { abilityName = "Frozen Aura", description = "Reduces nearby enemies' attack speed by 35% for 5 seconds", type = AbilityType.Debuff, duration = 5f, targeting = AbilityTargeting.NearbyEnemies });
        count++;

        CreateUnit(path, traits, "Cyclops", "Cyclops", 3, DamageType.Physical, new[] { "Overkill", "Cleave" },
            new UnitStats { health = 850, attack = 90, armor = 35, magicResist = 25, attackSpeed = 0.6f, range = 1, maxMana = 70 },
            new AbilityData { abilityName = "Boulder Hurl", description = "Hurls a boulder dealing 450 damage", type = AbilityType.Damage, baseDamage = 450, damageType = DamageType.Physical, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "Specter", "Specter", 3, DamageType.Dark, new[] { "Gigamega", "Reflective" },
            new UnitStats { health = 600, attack = 60, armor = 20, magicResist = 45, attackSpeed = 0.65f, range = 3, startingMana = 25, maxMana = 75 },
            new AbilityData { abilityName = "Soul Siphon", description = "Drains target dealing 300 damage, healing for 100%", type = AbilityType.DamageAndHeal, baseDamage = 300, baseHealing = 300, damageType = DamageType.Dark, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "NagaWizard", "Naga Wizard", 3, DamageType.Elemental, new[] { "Gigamega", "Scavenger", "Attuned" },
            new UnitStats { health = 650, attack = 55, armor = 25, magicResist = 50, attackSpeed = 0.6f, range = 4, startingMana = 30, maxMana = 90 },
            new AbilityData { abilityName = "Tidal Wave", description = "Summons a wave dealing 250 damage and STUNNING all enemies for 1.5s", type = AbilityType.AreaDamage, baseDamage = 250, damageType = DamageType.Elemental, duration = 1.5f, targeting = AbilityTargeting.AllEnemies });
        count++;

        CreateUnit(path, traits, "Beholder", "Beholder", 3, DamageType.Dark, new[] { "Gigamega", "Volatile" },
            new UnitStats { health = 550, attack = 65, armor = 20, magicResist = 45, attackSpeed = 0.6f, range = 4, startingMana = 20, maxMana = 70 },
            new AbilityData { abilityName = "Death Ray", description = "Fires a devastating beam dealing 500 damage", type = AbilityType.Damage, baseDamage = 500, damageType = DamageType.Dark, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "Cleric", "Cleric", 3, DamageType.Holy, new[] { "Invigorating", "Attuned" },
            new UnitStats { health = 700, attack = 45, armor = 30, magicResist = 45, attackSpeed = 0.55f, range = 3, startingMana = 30, maxMana = 100 },
            new AbilityData { abilityName = "Divine Radiance", description = "Heals all allies for 200 HP each", type = AbilityType.Heal, baseHealing = 200, targeting = AbilityTargeting.AllAllies });
        count++;

        // TIER 4 (4-cost)
        CreateUnit(path, traits, "BlackKnight", "Black Knight", 4, DamageType.Physical, new[] { "Forged", "Cleave" },
            new UnitStats { health = 1000, attack = 95, armor = 55, magicResist = 35, attackSpeed = 0.65f, range = 1, maxMana = 80 },
            new AbilityData { abilityName = "Executioner", description = "Deals 400 damage, +50% to targets below 50% HP", type = AbilityType.Damage, baseDamage = 400, damageType = DamageType.Physical, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "BishopKnight", "Bishop Knight", 4, DamageType.Physical, new[] { "Mitigation", "Reflective" },
            new UnitStats { health = 1100, attack = 70, armor = 60, magicResist = 50, attackSpeed = 0.5f, range = 1, maxMana = 90 },
            new AbilityData { abilityName = "Sacred Shield", description = "Grants lowest health ally 500 shield and 30 armor for 4s", type = AbilityType.Shield, baseShieldAmount = 500, duration = 4f, targeting = AbilityTargeting.LowestHealthAlly });
        count++;

        CreateUnit(path, traits, "BoneDragon", "Bone Dragon", 4, DamageType.Dark, new[] { "Volatile", "Attuned" },
            new UnitStats { health = 900, attack = 80, armor = 35, magicResist = 40, attackSpeed = 0.6f, range = 2, startingMana = 25, maxMana = 85 },
            new AbilityData { abilityName = "Necrotic Breath", description = "Breathes death in a cone dealing 400 damage, reducing healing 50%", type = AbilityType.AreaDamage, baseDamage = 400, damageType = DamageType.Dark, radius = 2, duration = 4f, targeting = AbilityTargeting.CurrentTarget });
        count++;

        CreateUnit(path, traits, "FlyingDemon", "Flying Demon", 4, DamageType.Dark, new[] { "Momentum", "Overkill" },
            new UnitStats { health = 850, attack = 100, armor = 30, magicResist = 35, attackSpeed = 0.75f, range = 1, maxMana = 70 },
            new AbilityData { abilityName = "Shadow Dive", description = "Dives onto furthest enemy dealing 450 damage, gaining 30% lifesteal", type = AbilityType.Movement, baseDamage = 450, damageType = DamageType.Dark, duration = 4f, targeting = AbilityTargeting.BacklineEnemy });
        count++;

        CreateUnit(path, traits, "FatDragon", "Fat Dragon", 4, DamageType.Elemental, new[] { "Bruiser", "Cleave" },
            new UnitStats { health = 1200, attack = 85, armor = 45, magicResist = 40, attackSpeed = 0.5f, range = 1, maxMana = 85 },
            new AbilityData { abilityName = "Belly Slam", description = "Crashes down dealing 350 damage, slowing 40% for 2s", type = AbilityType.AreaDamage, baseDamage = 350, damageType = DamageType.Elemental, radius = 1, duration = 2f, targeting = AbilityTargeting.NearbyEnemies });
        count++;

        CreateUnit(path, traits, "EvilOldMage", "Evil Old Mage", 4, DamageType.Dark, new[] { "Gigamega", "Scavenger" },
            new UnitStats { health = 700, attack = 70, armor = 25, magicResist = 55, attackSpeed = 0.55f, range = 4, startingMana = 35, maxMana = 95 },
            new AbilityData { abilityName = "Curse of Weakness", description = "Curses highest HP enemy, reducing damage 40% and armor 30 for 5s", type = AbilityType.Debuff, duration = 5f, targeting = AbilityTargeting.HighestHealthEnemy });
        count++;

        CreateUnit(path, traits, "OrcWithMace", "Orc with Mace", 4, DamageType.Physical, new[] { "FirstBlood", "Overkill" },
            new UnitStats { health = 950, attack = 110, armor = 40, magicResist = 30, attackSpeed = 0.6f, range = 1, maxMana = 75 },
            new AbilityData { abilityName = "Skull Crusher", description = "Devastating overhead strike dealing 550 damage", type = AbilityType.Damage, baseDamage = 550, damageType = DamageType.Physical, targeting = AbilityTargeting.CurrentTarget });
        count++;

        // TIER 5 (5-cost)
        CreateUnit(path, traits, "DemonKing", "Demon King", 5, DamageType.Dark, new[] { "Overkill", "Momentum" },
            new UnitStats { health = 1100, attack = 120, armor = 45, magicResist = 45, attackSpeed = 0.65f, range = 1, startingMana = 30, maxMana = 100 },
            new AbilityData { abilityName = "Infernal Storm", description = "Unleashes hellfire dealing 500 damage to ALL enemies + 200 burn", type = AbilityType.AreaDamage, baseDamage = 700, damageType = DamageType.Dark, duration = 4f, targeting = AbilityTargeting.AllEnemies });
        count++;

        CreateUnit(path, traits, "CastleMonster", "Castle Monster", 5, DamageType.Physical, new[] { "Bruiser", "Forged" },
            new UnitStats { health = 1500, attack = 80, armor = 60, magicResist = 50, attackSpeed = 0.4f, range = 1, maxMana = 100 },
            new AbilityData { abilityName = "Fortress Mode", description = "Gains 800 shield and taunts all enemies for 3 seconds", type = AbilityType.Shield, baseShieldAmount = 800, duration = 3f, targeting = AbilityTargeting.Self });
        count++;

        CreateUnit(path, traits, "SpikyShellTurtle", "Spiky Shell Turtle", 5, DamageType.Physical, new[] { "Reflective", "Mitigation" },
            new UnitStats { health = 1400, attack = 60, armor = 70, magicResist = 60, attackSpeed = 0.35f, range = 1, maxMana = 90 },
            new AbilityData { abilityName = "Impenetrable Shell", description = "80% damage reduction, reflects 100% damage for 4s, cannot attack", type = AbilityType.Buff, duration = 4f, targeting = AbilityTargeting.Self });
        count++;

        CreateUnit(path, traits, "FlameKnight", "Flame Knight", 5, DamageType.Elemental, new[] { "Volatile", "Overkill" },
            new UnitStats { health = 950, attack = 105, armor = 40, magicResist = 45, attackSpeed = 0.65f, range = 1, startingMana = 25, maxMana = 85 },
            new AbilityData { abilityName = "Inferno Blade", description = "Ignites blade dealing 600 damage to adjacent enemies, leaving fire", type = AbilityType.AreaDamage, baseDamage = 600, damageType = DamageType.Elemental, radius = 1, duration = 3f, targeting = AbilityTargeting.NearbyEnemies });
        count++;

        CreateUnit(path, traits, "SkeletonMage", "Skeleton Mage", 5, DamageType.Dark, new[] { "Gigamega", "Attuned" },
            new UnitStats { health = 750, attack = 75, armor = 25, magicResist = 55, attackSpeed = 0.55f, range = 4, startingMana = 40, maxMana = 120 },
            new AbilityData { abilityName = "Absolute Zero", description = "FREEZES ALL enemies for 2.5 seconds. Frozen take 20% bonus damage", type = AbilityType.Debuff, duration = 2.5f, targeting = AbilityTargeting.AllEnemies });
        count++;

        return count;
    }

    private void CreateUnit(string path, Dictionary<string, TraitData> traits, string id, string name, int cost, DamageType damageType, string[] traitNames, UnitStats stats, AbilityData ability)
    {
        UnitData unit = ScriptableObject.CreateInstance<UnitData>();
        unit.unitId = id.ToLower();
        unit.unitName = name;
        unit.cost = cost;
        unit.baseStats = stats;
        unit.ability = ability;
        unit.ability.damageType = damageType;

        // Assign traits
        List<TraitData> unitTraits = new List<TraitData>();
        foreach (string traitName in traitNames)
        {
            string key = traitName.ToLower();
            if (traits.ContainsKey(key))
            {
                unitTraits.Add(traits[key]);
            }
            else
            {
                Debug.LogWarning($"Trait '{traitName}' not found for unit '{name}'");
            }
        }
        unit.traits = unitTraits.ToArray();

        string assetPath = $"{path}/{id}.asset";
        AssetDatabase.CreateAsset(unit, assetPath);
    }

    #endregion
}
