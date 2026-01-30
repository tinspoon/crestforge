using UnityEngine;
using System.Collections.Generic;

namespace Crestforge.UI
{
    /// <summary>
    /// Generates pixel art sprites for all unit types
    /// </summary>
    public static class UnitSpriteGenerator
    {
        private static Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        
        // Color palettes
        private static readonly Color HumanSkin = new Color(0.93f, 0.75f, 0.6f);
        private static readonly Color HumanArmor = new Color(0.6f, 0.6f, 0.7f);
        private static readonly Color UndeadBone = new Color(0.85f, 0.85f, 0.8f);
        private static readonly Color UndeadGlow = new Color(0.4f, 0.8f, 0.4f);
        private static readonly Color BeastFur = new Color(0.6f, 0.45f, 0.3f);
        private static readonly Color BeastDark = new Color(0.35f, 0.25f, 0.15f);
        private static readonly Color ElementalFire = new Color(1f, 0.5f, 0.2f);
        private static readonly Color ElementalBlue = new Color(0.3f, 0.6f, 1f);
        private static readonly Color DemonRed = new Color(0.8f, 0.2f, 0.2f);
        private static readonly Color DemonDark = new Color(0.3f, 0.1f, 0.15f);
        private static readonly Color FeyPurple = new Color(0.7f, 0.5f, 0.9f);
        private static readonly Color FeyGlow = new Color(0.9f, 0.8f, 1f);

        public static Sprite GetSprite(string unitId)
        {
            string key = unitId.ToLower().Replace(" ", "_").Replace("_", "");
            if (spriteCache.ContainsKey(key))
                return spriteCache[key];

            Sprite sprite = GenerateSprite(key);
            spriteCache[key] = sprite;
            return sprite;
        }

        private static Sprite GenerateSprite(string unitId)
        {
            int size = 16;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            switch (unitId)
            {
                case "footman": DrawWarrior(pixels, size, HumanSkin, HumanArmor); break;
                case "archer": DrawArcher(pixels, size, HumanSkin, new Color(0.4f, 0.5f, 0.3f)); break;
                case "skeleton": DrawSkeleton(pixels, size); break;
                case "wolf": DrawWolf(pixels, size, BeastFur, BeastDark); break;
                case "imp": DrawImp(pixels, size); break;
                case "sprite": DrawFairy(pixels, size); break;
                case "golem": DrawGolem(pixels, size); break;
                case "rat": DrawRat(pixels, size); break;
                case "knight": DrawKnight(pixels, size); break;
                case "crossbowman": DrawCrossbowman(pixels, size); break;
                case "ghoul": DrawGhoul(pixels, size); break;
                case "druid": DrawDruid(pixels, size); break;
                case "firemage": DrawFireMage(pixels, size); break;
                case "shadow": DrawShadow(pixels, size); break;
                case "satyr": DrawSatyr(pixels, size); break;
                case "houndmaster": DrawHoundMaster(pixels, size); break;
                case "paladin": DrawPaladin(pixels, size); break;
                case "warden": DrawWarden(pixels, size); break;
                case "necromancer": DrawNecromancer(pixels, size); break;
                case "alphawolf": DrawAlphaWolf(pixels, size); break;
                case "stormelemental": DrawStormElemental(pixels, size); break;
                case "succubus": DrawSuccubus(pixels, size); break;
                case "enchantress": DrawEnchantress(pixels, size); break;
                case "marksman": DrawMarksman(pixels, size); break;
                case "champion": DrawChampion(pixels, size); break;
                case "deathknight": DrawDeathKnight(pixels, size); break;
                case "phoenix": DrawPhoenix(pixels, size); break;
                case "demonlord": DrawDemonLord(pixels, size); break;
                case "archdruid": DrawArchdruid(pixels, size); break;
                case "archmage": DrawArchmage(pixels, size); break;
                case "lich": DrawLich(pixels, size); break;
                case "dragon": DrawDragon(pixels, size); break;
                default: DrawGeneric(pixels, size); break;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16);
        }

        private static void SetPixel(Color[] p, int s, int x, int y, Color c)
        {
            if (x >= 0 && x < s && y >= 0 && y < s) p[y * s + x] = c;
        }

        // ===== Unit Drawing Methods =====

        private static void DrawWarrior(Color[] p, int s, Color skin, Color armor)
        {
            // Head
            SetPixel(p, s, 7, 12, skin); SetPixel(p, s, 8, 12, skin);
            SetPixel(p, s, 7, 11, skin); SetPixel(p, s, 8, 11, skin);
            // Helmet
            SetPixel(p, s, 6, 13, armor); SetPixel(p, s, 7, 13, armor);
            SetPixel(p, s, 8, 13, armor); SetPixel(p, s, 9, 13, armor);
            // Body
            for (int y = 6; y <= 10; y++) { SetPixel(p, s, 7, y, armor); SetPixel(p, s, 8, y, armor); }
            // Arms & Legs
            SetPixel(p, s, 6, 9, armor); SetPixel(p, s, 9, 9, armor);
            SetPixel(p, s, 7, 5, armor); SetPixel(p, s, 8, 5, armor);
            SetPixel(p, s, 7, 4, skin); SetPixel(p, s, 8, 4, skin);
            // Sword
            SetPixel(p, s, 10, 10, Color.gray); SetPixel(p, s, 11, 11, Color.gray); SetPixel(p, s, 12, 12, Color.gray);
            // Shield
            SetPixel(p, s, 4, 9, new Color(0.5f, 0.4f, 0.3f)); SetPixel(p, s, 5, 9, new Color(0.5f, 0.4f, 0.3f));
            SetPixel(p, s, 4, 8, new Color(0.5f, 0.4f, 0.3f)); SetPixel(p, s, 5, 8, new Color(0.5f, 0.4f, 0.3f));
        }

        private static void DrawArcher(Color[] p, int s, Color skin, Color clothes)
        {
            SetPixel(p, s, 7, 12, skin); SetPixel(p, s, 8, 12, skin);
            SetPixel(p, s, 7, 11, skin); SetPixel(p, s, 8, 11, skin);
            for (int y = 6; y <= 10; y++) { SetPixel(p, s, 7, y, clothes); SetPixel(p, s, 8, y, clothes); }
            SetPixel(p, s, 7, 5, clothes); SetPixel(p, s, 8, 5, clothes);
            SetPixel(p, s, 7, 4, skin); SetPixel(p, s, 8, 4, skin);
            // Bow
            Color wood = new Color(0.5f, 0.35f, 0.2f);
            SetPixel(p, s, 10, 11, wood); SetPixel(p, s, 11, 10, wood);
            SetPixel(p, s, 11, 9, wood); SetPixel(p, s, 11, 8, wood); SetPixel(p, s, 10, 7, wood);
        }

        private static void DrawSkeleton(Color[] p, int s)
        {
            SetPixel(p, s, 7, 13, UndeadBone); SetPixel(p, s, 8, 13, UndeadBone);
            SetPixel(p, s, 7, 12, UndeadGlow); SetPixel(p, s, 8, 12, UndeadGlow); // Eyes
            for (int y = 6; y <= 11; y++) { SetPixel(p, s, 7, y, UndeadBone); SetPixel(p, s, 8, y, UndeadBone); }
            SetPixel(p, s, 6, 9, UndeadBone); SetPixel(p, s, 9, 9, UndeadBone); // Ribs
            SetPixel(p, s, 5, 8, UndeadBone); SetPixel(p, s, 10, 8, UndeadBone); // Arms
            SetPixel(p, s, 7, 5, UndeadBone); SetPixel(p, s, 8, 5, UndeadBone);
            SetPixel(p, s, 7, 4, UndeadBone); SetPixel(p, s, 8, 4, UndeadBone);
        }

        private static void DrawWolf(Color[] p, int s, Color fur, Color dark)
        {
            for (int x = 5; x <= 10; x++) { SetPixel(p, s, x, 8, fur); SetPixel(p, s, x, 7, fur); }
            SetPixel(p, s, 11, 9, fur); SetPixel(p, s, 12, 9, fur); SetPixel(p, s, 12, 10, fur); // Head
            SetPixel(p, s, 13, 9, dark); // Nose
            SetPixel(p, s, 11, 10, Color.yellow); // Eye
            SetPixel(p, s, 6, 6, dark); SetPixel(p, s, 9, 6, dark); // Legs
            SetPixel(p, s, 4, 9, fur); SetPixel(p, s, 3, 10, fur); // Tail
        }

        private static void DrawImp(Color[] p, int s)
        {
            SetPixel(p, s, 7, 10, DemonRed); SetPixel(p, s, 8, 10, DemonRed);
            SetPixel(p, s, 7, 9, DemonRed); SetPixel(p, s, 8, 9, DemonRed);
            SetPixel(p, s, 6, 11, DemonDark); SetPixel(p, s, 9, 11, DemonDark); // Horns
            SetPixel(p, s, 7, 10, Color.yellow); SetPixel(p, s, 8, 10, Color.yellow); // Eyes
            SetPixel(p, s, 7, 8, DemonRed); SetPixel(p, s, 8, 8, DemonRed);
            SetPixel(p, s, 5, 9, DemonDark); SetPixel(p, s, 10, 9, DemonDark); // Wings
            SetPixel(p, s, 4, 10, DemonDark); SetPixel(p, s, 11, 10, DemonDark);
        }

        private static void DrawFairy(Color[] p, int s)
        {
            SetPixel(p, s, 7, 9, FeyPurple); SetPixel(p, s, 8, 9, FeyPurple);
            SetPixel(p, s, 7, 8, FeyPurple); SetPixel(p, s, 8, 8, FeyPurple);
            SetPixel(p, s, 7, 10, FeyGlow); SetPixel(p, s, 8, 10, FeyGlow); // Head glow
            SetPixel(p, s, 5, 10, FeyGlow); SetPixel(p, s, 4, 11, FeyGlow); // Wings
            SetPixel(p, s, 10, 10, FeyGlow); SetPixel(p, s, 11, 11, FeyGlow);
        }

        private static void DrawGolem(Color[] p, int s)
        {
            Color stone = new Color(0.5f, 0.5f, 0.55f);
            for (int x = 5; x <= 10; x++) for (int y = 5; y <= 11; y++) SetPixel(p, s, x, y, stone);
            SetPixel(p, s, 6, 12, stone); SetPixel(p, s, 7, 12, stone);
            SetPixel(p, s, 8, 12, stone); SetPixel(p, s, 9, 12, stone);
            SetPixel(p, s, 7, 11, ElementalBlue); SetPixel(p, s, 8, 11, ElementalBlue); // Eyes
        }

        private static void DrawRat(Color[] p, int s)
        {
            Color fur = new Color(0.5f, 0.45f, 0.4f);
            for (int x = 6; x <= 10; x++) { SetPixel(p, s, x, 7, fur); SetPixel(p, s, x, 6, fur); }
            SetPixel(p, s, 11, 8, fur); SetPixel(p, s, 12, 8, Color.black); // Head/nose
            SetPixel(p, s, 11, 9, Color.red); // Eye
            SetPixel(p, s, 5, 7, fur); SetPixel(p, s, 4, 8, fur); // Tail
        }

        private static void DrawKnight(Color[] p, int s)
        {
            Color armor = new Color(0.7f, 0.7f, 0.75f);
            DrawWarrior(p, s, HumanSkin, armor);
            SetPixel(p, s, 7, 14, armor); SetPixel(p, s, 8, 14, armor); // Helmet plume
        }

        private static void DrawCrossbowman(Color[] p, int s)
        {
            DrawArcher(p, s, HumanSkin, new Color(0.5f, 0.4f, 0.3f));
            SetPixel(p, s, 11, 9, Color.gray); SetPixel(p, s, 12, 9, Color.gray); // Crossbow
            SetPixel(p, s, 11, 10, new Color(0.4f, 0.3f, 0.2f));
        }

        private static void DrawGhoul(Color[] p, int s)
        {
            Color skin = new Color(0.5f, 0.6f, 0.5f);
            SetPixel(p, s, 7, 11, skin); SetPixel(p, s, 8, 11, skin);
            SetPixel(p, s, 7, 10, UndeadGlow); SetPixel(p, s, 8, 10, UndeadGlow); // Eyes
            for (int y = 6; y <= 9; y++) { SetPixel(p, s, 7, y, skin); SetPixel(p, s, 8, y, skin); }
            SetPixel(p, s, 5, 9, skin); SetPixel(p, s, 4, 10, Color.white); // Claws
            SetPixel(p, s, 10, 9, skin); SetPixel(p, s, 11, 10, Color.white);
        }

        private static void DrawDruid(Color[] p, int s)
        {
            Color robe = new Color(0.3f, 0.5f, 0.3f);
            SetPixel(p, s, 7, 12, HumanSkin); SetPixel(p, s, 8, 12, HumanSkin);
            for (int y = 5; y <= 11; y++) { SetPixel(p, s, 7, y, robe); SetPixel(p, s, 8, y, robe); }
            SetPixel(p, s, 10, 10, new Color(0.4f, 0.3f, 0.2f)); // Staff
            SetPixel(p, s, 10, 11, new Color(0.4f, 0.3f, 0.2f));
            SetPixel(p, s, 10, 12, new Color(0.3f, 0.7f, 0.4f)); // Staff glow
        }

        private static void DrawFireMage(Color[] p, int s)
        {
            Color robe = ElementalFire;
            SetPixel(p, s, 7, 12, HumanSkin); SetPixel(p, s, 8, 12, HumanSkin);
            for (int y = 5; y <= 11; y++) { SetPixel(p, s, 7, y, robe); SetPixel(p, s, 8, y, robe); }
            SetPixel(p, s, 10, 11, ElementalFire); SetPixel(p, s, 10, 12, Color.yellow);
            SetPixel(p, s, 11, 13, new Color(1f, 0.8f, 0.2f)); // Fireball
        }

        private static void DrawShadow(Color[] p, int s)
        {
            Color shadow = new Color(0.15f, 0.1f, 0.2f);
            Color edge = new Color(0.3f, 0.2f, 0.4f);
            for (int y = 6; y <= 12; y++) { SetPixel(p, s, 7, y, shadow); SetPixel(p, s, 8, y, shadow); }
            SetPixel(p, s, 6, 10, edge); SetPixel(p, s, 9, 10, edge);
            SetPixel(p, s, 7, 11, Color.red); SetPixel(p, s, 8, 11, Color.red); // Eyes
        }

        private static void DrawSatyr(Color[] p, int s)
        {
            SetPixel(p, s, 7, 12, HumanSkin); SetPixel(p, s, 8, 12, HumanSkin);
            SetPixel(p, s, 6, 13, BeastFur); SetPixel(p, s, 9, 13, BeastFur); // Horns
            for (int y = 8; y <= 11; y++) { SetPixel(p, s, 7, y, BeastFur); SetPixel(p, s, 8, y, BeastFur); }
            SetPixel(p, s, 6, 6, BeastFur); SetPixel(p, s, 9, 6, BeastFur); // Hooves
        }

        private static void DrawHoundMaster(Color[] p, int s)
        {
            DrawArcher(p, s, HumanSkin, new Color(0.4f, 0.35f, 0.3f));
            SetPixel(p, s, 11, 8, new Color(0.3f, 0.2f, 0.15f)); // Whip
            SetPixel(p, s, 12, 7, new Color(0.3f, 0.2f, 0.15f));
        }

        private static void DrawPaladin(Color[] p, int s)
        {
            Color gold = new Color(0.9f, 0.85f, 0.5f);
            DrawWarrior(p, s, HumanSkin, gold);
            SetPixel(p, s, 6, 14, gold); SetPixel(p, s, 7, 14, gold); // Halo
            SetPixel(p, s, 8, 14, gold); SetPixel(p, s, 9, 14, gold);
        }

        private static void DrawWarden(Color[] p, int s)
        {
            Color armor = new Color(0.4f, 0.4f, 0.5f);
            DrawWarrior(p, s, HumanSkin, armor);
            SetPixel(p, s, 11, 11, Color.gray); SetPixel(p, s, 12, 12, Color.gray); // Greatsword
            SetPixel(p, s, 13, 13, Color.gray); SetPixel(p, s, 14, 14, Color.gray);
        }

        private static void DrawNecromancer(Color[] p, int s)
        {
            Color robe = new Color(0.2f, 0.15f, 0.25f);
            SetPixel(p, s, 7, 12, new Color(0.7f, 0.7f, 0.75f)); SetPixel(p, s, 8, 12, new Color(0.7f, 0.7f, 0.75f));
            for (int y = 5; y <= 11; y++) { SetPixel(p, s, 7, y, robe); SetPixel(p, s, 8, y, robe); }
            SetPixel(p, s, 10, 11, UndeadGlow); SetPixel(p, s, 10, 12, UndeadGlow); // Staff glow
            SetPixel(p, s, 11, 13, UndeadBone); // Skull
        }

        private static void DrawAlphaWolf(Color[] p, int s)
        {
            Color fur = new Color(0.4f, 0.4f, 0.45f);
            DrawWolf(p, s, fur, new Color(0.2f, 0.2f, 0.25f));
            SetPixel(p, s, 10, 10, Color.white); SetPixel(p, s, 11, 11, Color.white); // Alpha marking
        }

        private static void DrawStormElemental(Color[] p, int s)
        {
            for (int y = 7; y <= 11; y++) { SetPixel(p, s, 7, y, ElementalBlue); SetPixel(p, s, 8, y, ElementalBlue); }
            SetPixel(p, s, 6, 9, ElementalBlue); SetPixel(p, s, 9, 9, ElementalBlue);
            SetPixel(p, s, 5, 12, Color.white); SetPixel(p, s, 10, 12, Color.white); // Lightning
            SetPixel(p, s, 4, 7, Color.white); SetPixel(p, s, 11, 7, Color.white);
        }

        private static void DrawSuccubus(Color[] p, int s)
        {
            Color skin = new Color(0.9f, 0.7f, 0.75f);
            SetPixel(p, s, 7, 11, skin); SetPixel(p, s, 8, 11, skin);
            for (int y = 6; y <= 10; y++) { SetPixel(p, s, 7, y, DemonRed); SetPixel(p, s, 8, y, DemonRed); }
            SetPixel(p, s, 5, 12, DemonDark); SetPixel(p, s, 10, 12, DemonDark); // Horns
            SetPixel(p, s, 4, 10, DemonDark); SetPixel(p, s, 11, 10, DemonDark); // Wings
            SetPixel(p, s, 5, 5, DemonDark); // Tail
        }

        private static void DrawEnchantress(Color[] p, int s)
        {
            SetPixel(p, s, 7, 11, new Color(0.85f, 0.8f, 0.9f)); SetPixel(p, s, 8, 11, new Color(0.85f, 0.8f, 0.9f));
            for (int y = 5; y <= 10; y++) { SetPixel(p, s, 7, y, FeyPurple); SetPixel(p, s, 8, y, FeyPurple); }
            SetPixel(p, s, 4, 11, FeyGlow); SetPixel(p, s, 5, 12, FeyGlow); // Wings
            SetPixel(p, s, 11, 11, FeyGlow); SetPixel(p, s, 10, 12, FeyGlow);
        }

        private static void DrawMarksman(Color[] p, int s)
        {
            DrawArcher(p, s, HumanSkin, new Color(0.3f, 0.35f, 0.3f));
            SetPixel(p, s, 10, 12, new Color(0.5f, 0.35f, 0.2f)); // Long bow
            SetPixel(p, s, 11, 11, new Color(0.5f, 0.35f, 0.2f));
            SetPixel(p, s, 10, 6, new Color(0.5f, 0.35f, 0.2f));
        }

        private static void DrawChampion(Color[] p, int s)
        {
            Color gold = new Color(0.8f, 0.6f, 0.2f);
            DrawWarrior(p, s, HumanSkin, gold);
            SetPixel(p, s, 5, 10, new Color(0.7f, 0.1f, 0.1f)); // Cape
            SetPixel(p, s, 5, 9, new Color(0.7f, 0.1f, 0.1f));
            SetPixel(p, s, 4, 8, new Color(0.7f, 0.1f, 0.1f));
        }

        private static void DrawDeathKnight(Color[] p, int s)
        {
            Color armor = new Color(0.2f, 0.2f, 0.25f);
            DrawWarrior(p, s, UndeadBone, armor);
            SetPixel(p, s, 5, 11, UndeadGlow); SetPixel(p, s, 10, 11, UndeadGlow); // Glow
        }

        private static void DrawPhoenix(Color[] p, int s)
        {
            Color orange = new Color(1f, 0.6f, 0.2f);
            Color yellow = new Color(1f, 0.9f, 0.3f);
            SetPixel(p, s, 7, 9, orange); SetPixel(p, s, 8, 9, orange);
            SetPixel(p, s, 7, 8, yellow); SetPixel(p, s, 8, 8, yellow);
            SetPixel(p, s, 4, 11, yellow); SetPixel(p, s, 5, 10, orange); // Wings
            SetPixel(p, s, 11, 11, yellow); SetPixel(p, s, 10, 10, orange);
            SetPixel(p, s, 6, 6, orange); SetPixel(p, s, 9, 6, orange); // Tail
        }

        private static void DrawDemonLord(Color[] p, int s)
        {
            for (int x = 5; x <= 10; x++) for (int y = 5; y <= 10; y++) SetPixel(p, s, x, y, DemonRed);
            SetPixel(p, s, 7, 11, DemonRed); SetPixel(p, s, 8, 11, DemonRed);
            SetPixel(p, s, 4, 13, DemonDark); SetPixel(p, s, 3, 14, DemonDark); // Big horns
            SetPixel(p, s, 11, 13, DemonDark); SetPixel(p, s, 12, 14, DemonDark);
            SetPixel(p, s, 7, 10, Color.yellow); SetPixel(p, s, 8, 10, Color.yellow); // Eyes
        }

        private static void DrawArchdruid(Color[] p, int s)
        {
            DrawDruid(p, s);
            SetPixel(p, s, 5, 13, BeastFur); SetPixel(p, s, 4, 14, BeastFur); // Antlers
            SetPixel(p, s, 10, 13, BeastFur); SetPixel(p, s, 11, 14, BeastFur);
        }

        private static void DrawArchmage(Color[] p, int s)
        {
            Color robe = new Color(0.3f, 0.3f, 0.6f);
            SetPixel(p, s, 7, 12, HumanSkin); SetPixel(p, s, 8, 12, HumanSkin);
            for (int y = 5; y <= 11; y++) { SetPixel(p, s, 7, y, robe); SetPixel(p, s, 8, y, robe); }
            SetPixel(p, s, 7, 13, robe); SetPixel(p, s, 8, 13, robe); // Hat
            SetPixel(p, s, 7, 14, robe);
            SetPixel(p, s, 10, 12, ElementalBlue); // Staff glow
        }

        private static void DrawLich(Color[] p, int s)
        {
            DrawSkeleton(p, s);
            SetPixel(p, s, 6, 14, new Color(0.6f, 0.5f, 0.2f)); // Crown
            SetPixel(p, s, 7, 14, new Color(0.6f, 0.5f, 0.2f));
            SetPixel(p, s, 8, 14, new Color(0.6f, 0.5f, 0.2f));
            SetPixel(p, s, 9, 14, new Color(0.6f, 0.5f, 0.2f));
            SetPixel(p, s, 10, 12, new Color(0.5f, 0.2f, 0.7f)); // Purple staff
        }

        private static void DrawDragon(Color[] p, int s)
        {
            Color scales = new Color(0.2f, 0.5f, 0.3f);
            for (int x = 4; x <= 11; x++) { SetPixel(p, s, x, 8, scales); SetPixel(p, s, x, 7, scales); }
            SetPixel(p, s, 12, 9, scales); SetPixel(p, s, 13, 9, scales); // Head
            SetPixel(p, s, 14, 9, ElementalFire); // Fire breath
            SetPixel(p, s, 6, 10, scales); SetPixel(p, s, 5, 11, scales); // Wings
            SetPixel(p, s, 9, 10, scales); SetPixel(p, s, 10, 11, scales);
            SetPixel(p, s, 3, 9, scales); SetPixel(p, s, 2, 10, scales); // Tail
        }

        private static void DrawGeneric(Color[] p, int s)
        {
            for (int x = 6; x <= 9; x++) for (int y = 6; y <= 11; y++) SetPixel(p, s, x, y, Color.gray);
            SetPixel(p, s, 7, 10, Color.white); SetPixel(p, s, 8, 10, Color.white); // Eyes
        }

        public static void ClearCache() { spriteCache.Clear(); }
    }
}