/*
 * CRESTFORGE - TRAIT DEFINITIONS
 * 
 * This file documents all traits in the game.
 * Use the Unity Editor to create TraitData ScriptableObjects
 * based on these specifications.
 * 
 * Each unit has 2-3 traits (mix of Origins and Classes)
 */

/* ============================================
 * ORIGINS (6 total)
 * ============================================ */

/*
 * HUMAN
 * "Strength in unity"
 * Tiers: 2/4/6
 * Bonus: All Humans gain stats per Human on board
 *   (2) +5% Attack and Health per Human
 *   (4) +10% Attack and Health per Human
 *   (6) +15% Attack and Health per Human, +10 Armor
 */

/*
 * UNDEAD
 * "Death is only the beginning"
 * Tiers: 2/4/6
 * Bonus: Undead units heal when dealing damage and can cheat death
 *   (2) Undead heal for 15% of damage dealt
 *   (4) Undead heal for 25% of damage dealt
 *   (6) Undead heal for 35% of damage dealt, first Undead to die revives at 30% HP
 */

/*
 * BEAST
 * "The pack hunts as one"
 * Tiers: 2/4/6
 * Bonus: Beasts gain attack speed, increasing with more Beasts
 *   (2) Beasts gain +15% Attack Speed
 *   (4) Beasts gain +30% Attack Speed
 *   (6) Beasts gain +50% Attack Speed, attacks have 20% chance to strike twice
 */

/*
 * ELEMENTAL
 * "Primordial forces unleashed"
 * Tiers: 2/4
 * Bonus: Elementals deal bonus magic damage and reduce magic resist
 *   (2) Elemental attacks deal +20 bonus magic damage
 *   (4) Elemental attacks deal +40 bonus magic damage, reduce target MR by 20 for 3s
 */

/*
 * DEMON
 * "Power demands sacrifice"
 * Tiers: 2/4
 * Bonus: Demons deal massive damage but take damage themselves
 *   (2) Demons deal +30% damage, take 3% max HP per attack
 *   (4) Demons deal +50% damage, take 2% max HP per attack, heal to full on kill
 */

/*
 * FEY
 * "Now you see me..."
 * Tiers: 2/4
 * Bonus: Fey units have evasion and magic resistance
 *   (2) Fey have 20% chance to dodge attacks, +20 Magic Resist
 *   (4) Fey have 35% chance to dodge attacks, +40 Magic Resist, dodging grants 10 mana
 */


/* ============================================
 * CLASSES (8 total)
 * ============================================ */

/*
 * WARRIOR
 * "First into battle"
 * Tiers: 2/4/6
 * Bonus: Warriors gain armor and deal cleave damage
 *   (2) Warriors gain +25 Armor
 *   (4) Warriors gain +50 Armor, attacks cleave for 30% damage
 *   (6) Warriors gain +75 Armor, attacks cleave for 50% damage to adjacent enemies
 */

/*
 * RANGER
 * "Death from afar"
 * Tiers: 2/4
 * Bonus: Rangers gain attack damage and armor penetration
 *   (2) Rangers gain +20 Attack, attacks ignore 20% armor
 *   (4) Rangers gain +40 Attack, attacks ignore 40% armor, +20% Attack Speed
 */

/*
 * MAGE
 * "Knowledge is power"
 * Tiers: 2/4/6
 * Bonus: Mages gain ability power and mana
 *   (2) Mage abilities deal +20% damage, +15 starting mana
 *   (4) Mage abilities deal +40% damage, +30 starting mana
 *   (6) Mage abilities deal +70% damage, +50 starting mana, abilities cost 20% less mana
 */

/*
 * TANK
 * "Unbreakable"
 * Tiers: 2/4
 * Bonus: Tanks gain massive health and damage reduction
 *   (2) Tanks gain +300 Health, take 10% reduced damage
 *   (4) Tanks gain +600 Health, take 20% reduced damage, taunt nearby enemies for 2s at combat start
 */

/*
 * ASSASSIN
 * "Strike from the shadows"
 * Tiers: 2/4
 * Bonus: Assassins jump to backline and deal critical damage
 *   (2) Assassins jump to enemy backline at combat start, +25% crit chance
 *   (4) Assassins deal +75% crit damage, kills reset attack cooldown
 */

/*
 * SUPPORT
 * "Together we stand"
 * Tiers: 2/4
 * Bonus: Supports buff allies and heal
 *   (2) Supports heal the lowest health ally for 50 HP every 3s
 *   (4) Supports heal all allies for 30 HP every 3s, allies gain +15% Attack Speed
 */

/*
 * BERSERKER
 * "Pain fuels rage"
 * Tiers: 2
 * Bonus: Berserkers get stronger as they lose health
 *   (2) Berserkers gain +1% Attack Speed and +1% damage for each 1% missing health
 */

/*
 * SUMMONER
 * "Rise, my minions"
 * Tiers: 2/4
 * Bonus: Summoners create additional units
 *   (2) At combat start, Summoners create a 1-star copy of the cheapest unit on your board
 *   (4) Created units are 2-star, Summoner abilities summon an additional minion
 */


/* ============================================
 * TRAIT DISTRIBUTION ACROSS 32 UNITS
 * ============================================
 * 
 * Cost 1 (8 units):
 *   - Footman: Human, Warrior
 *   - Archer: Human, Ranger
 *   - Skeleton: Undead, Warrior
 *   - Wolf: Beast, Assassin
 *   - Imp: Demon, Mage
 *   - Sprite: Fey, Support
 *   - Golem: Elemental, Tank
 *   - Rat: Beast, Berserker
 * 
 * Cost 2 (8 units):
 *   - Knight: Human, Tank
 *   - Crossbowman: Human, Ranger
 *   - Ghoul: Undead, Assassin
 *   - Druid: Beast, Support, Summoner
 *   - Fire Mage: Elemental, Mage
 *   - Shadow: Undead, Assassin
 *   - Satyr: Fey, Berserker
 *   - Hound Master: Human, Beast, Summoner
 * 
 * Cost 3 (8 units):
 *   - Paladin: Human, Tank, Support
 *   - Warden: Human, Warrior, Tank
 *   - Necromancer: Undead, Mage, Summoner
 *   - Alpha Wolf: Beast, Warrior
 *   - Storm Elemental: Elemental, Mage
 *   - Succubus: Demon, Assassin
 *   - Enchantress: Fey, Mage, Support
 *   - Marksman: Human, Ranger
 * 
 * Cost 4 (8 units):
 *   - Champion: Human, Warrior, Berserker
 *   - Death Knight: Undead, Tank, Warrior
 *   - Phoenix: Elemental, Support
 *   - Demon Lord: Demon, Tank, Berserker
 *   - Archdruid: Beast, Fey, Summoner
 *   - Archmage: Human, Mage
 *   - Lich: Undead, Mage, Summoner
 *   - Dragon: Beast, Elemental, Warrior
 */
