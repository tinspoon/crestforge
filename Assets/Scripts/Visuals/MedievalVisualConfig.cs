using UnityEngine;
using Crestforge.Core;
using Crestforge.Data;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Central configuration for medieval-themed visual styles.
    /// Provides color palettes and material creation for units and board.
    /// </summary>
    public static class MedievalVisualConfig
    {
        // Cached shader references
        private static Shader _toonShader;
        private static Shader _toonTransparentShader;

        public static Shader ToonShader
        {
            get
            {
                if (_toonShader == null)
                {
                    _toonShader = Shader.Find("Crestforge/MedievalToon");
                    if (_toonShader == null)
                        _toonShader = Shader.Find("Standard");
                }
                return _toonShader;
            }
        }

        public static Shader ToonTransparentShader
        {
            get
            {
                if (_toonTransparentShader == null)
                {
                    _toonTransparentShader = Shader.Find("Crestforge/MedievalToonTransparent");
                    if (_toonTransparentShader == null)
                        _toonTransparentShader = Shader.Find("Transparent/Diffuse");
                }
                return _toonTransparentShader;
            }
        }

        // ==========================================
        // ORIGIN COLOR PALETTES (Medieval themed)
        // ==========================================

        public static class OriginColors
        {
            // Human - Heraldic blue and gold, knightly
            public static readonly ColorPalette Human = new ColorPalette(
                primary: new Color(0.2f, 0.35f, 0.6f),      // Royal blue
                secondary: new Color(0.85f, 0.7f, 0.3f),    // Gold trim
                accent: new Color(0.9f, 0.9f, 0.85f),       // Silver/white
                shadow: new Color(0.1f, 0.15f, 0.3f),       // Dark blue
                glow: new Color(0.4f, 0.6f, 1f)             // Light blue glow
            );

            // Undead - Sickly green, bone white, decay
            public static readonly ColorPalette Undead = new ColorPalette(
                primary: new Color(0.5f, 0.55f, 0.4f),      // Putrid green-gray
                secondary: new Color(0.85f, 0.8f, 0.7f),    // Bone white
                accent: new Color(0.3f, 0.5f, 0.3f),        // Ghostly green
                shadow: new Color(0.15f, 0.18f, 0.12f),     // Dark rot
                glow: new Color(0.4f, 0.9f, 0.4f)           // Necromantic green
            );

            // Beast - Natural browns, tans, earthy
            public static readonly ColorPalette Beast = new ColorPalette(
                primary: new Color(0.55f, 0.4f, 0.25f),     // Fur brown
                secondary: new Color(0.75f, 0.6f, 0.4f),    // Tan
                accent: new Color(0.3f, 0.25f, 0.15f),      // Dark brown
                shadow: new Color(0.2f, 0.15f, 0.1f),       // Deep brown
                glow: new Color(0.9f, 0.7f, 0.3f)           // Amber eyes
            );

            // Elemental - Varies by element, default to arcane purple/blue
            public static readonly ColorPalette Elemental = new ColorPalette(
                primary: new Color(0.4f, 0.5f, 0.7f),       // Storm gray-blue
                secondary: new Color(0.8f, 0.6f, 0.2f),     // Fire orange
                accent: new Color(0.3f, 0.7f, 0.9f),        // Lightning blue
                shadow: new Color(0.2f, 0.2f, 0.35f),       // Deep purple
                glow: new Color(0.6f, 0.8f, 1f)             // Arcane blue
            );

            // Demon - Crimson red, black, hellfire
            public static readonly ColorPalette Demon = new ColorPalette(
                primary: new Color(0.6f, 0.15f, 0.15f),     // Blood red
                secondary: new Color(0.15f, 0.1f, 0.1f),    // Charred black
                accent: new Color(0.9f, 0.4f, 0.1f),        // Hellfire orange
                shadow: new Color(0.25f, 0.05f, 0.05f),     // Deep crimson
                glow: new Color(1f, 0.3f, 0.1f)             // Fire glow
            );

            // Fey - Ethereal purple, moonlight silver
            public static readonly ColorPalette Fey = new ColorPalette(
                primary: new Color(0.5f, 0.4f, 0.65f),      // Twilight purple
                secondary: new Color(0.8f, 0.85f, 0.9f),    // Moonlight silver
                accent: new Color(0.6f, 0.8f, 0.6f),        // Forest green
                shadow: new Color(0.25f, 0.2f, 0.35f),      // Deep violet
                glow: new Color(0.7f, 0.6f, 1f)             // Fairy glow
            );
        }

        // ==========================================
        // CLASS ACCENT MODIFIERS
        // ==========================================

        public static class ClassAccents
        {
            public static readonly Color Warrior = new Color(0.7f, 0.7f, 0.75f);    // Steel
            public static readonly Color Ranger = new Color(0.4f, 0.5f, 0.3f);      // Forest green
            public static readonly Color Mage = new Color(0.5f, 0.4f, 0.8f);        // Arcane purple
            public static readonly Color Tank = new Color(0.5f, 0.5f, 0.55f);       // Iron gray
            public static readonly Color Assassin = new Color(0.2f, 0.2f, 0.25f);   // Shadow black
            public static readonly Color Support = new Color(0.9f, 0.85f, 0.6f);    // Holy gold
            public static readonly Color Berserker = new Color(0.7f, 0.25f, 0.2f);  // Rage red
            public static readonly Color Summoner = new Color(0.4f, 0.3f, 0.5f);    // Mystic purple
        }

        // ==========================================
        // COST TIER COLORS (Rarity indicator)
        // ==========================================

        public static class TierColors
        {
            public static readonly Color Tier1 = new Color(0.6f, 0.6f, 0.55f);      // Common gray
            public static readonly Color Tier2 = new Color(0.4f, 0.6f, 0.4f);       // Uncommon green
            public static readonly Color Tier3 = new Color(0.35f, 0.5f, 0.75f);     // Rare blue
            public static readonly Color Tier4 = new Color(0.6f, 0.4f, 0.7f);       // Epic purple
            public static readonly Color Tier5 = new Color(0.85f, 0.65f, 0.2f);     // Legendary gold
        }

        // ==========================================
        // BOARD COLORS (Merge Tactics grass style)
        // ==========================================

        public static class BoardColors
        {
            // Player side - Bright grass green
            public static readonly Color PlayerTileBase = new Color(0.45f, 0.65f, 0.32f);
            public static readonly Color PlayerTileLight = new Color(0.5f, 0.7f, 0.38f);
            public static readonly Color PlayerTileDark = new Color(0.35f, 0.55f, 0.25f);
            public static readonly Color PlayerHighlight = new Color(0.55f, 0.75f, 0.4f);

            // Enemy side - Slightly different shade of grass (darker/cooler)
            public static readonly Color EnemyTileBase = new Color(0.4f, 0.58f, 0.32f);
            public static readonly Color EnemyTileLight = new Color(0.45f, 0.63f, 0.38f);
            public static readonly Color EnemyTileDark = new Color(0.32f, 0.48f, 0.25f);
            public static readonly Color EnemyHighlight = new Color(0.6f, 0.4f, 0.35f);

            // Hex outline color
            public static readonly Color HexOutline = new Color(0.3f, 0.5f, 0.22f);

            // Dividing line
            public static readonly Color Divider = new Color(0.3f, 0.45f, 0.2f);
        }

        // ==========================================
        // MATERIAL CREATION
        // ==========================================

        /// <summary>
        /// Create a toon material with the given color
        /// </summary>
        public static Material CreateToonMaterial(Color mainColor, Color? shadowColor = null, float rimIntensity = 0.4f)
        {
            Material mat = new Material(ToonShader);

            mat.SetColor("_MainColor", mainColor);
            mat.SetColor("_ShadowColor", shadowColor ?? Color.Lerp(mainColor, Color.black, 0.5f));
            mat.SetFloat("_ShadowThreshold", 0.5f);
            mat.SetFloat("_ShadowSoftness", 0.05f);

            mat.SetColor("_RimColor", Color.Lerp(mainColor, Color.white, 0.5f));
            mat.SetFloat("_RimPower", 3f);
            mat.SetFloat("_RimIntensity", rimIntensity);

            mat.SetColor("_SpecularColor", Color.white);
            mat.SetFloat("_SpecularIntensity", 0.2f);
            mat.SetFloat("_SpecularSize", 0.05f);

            mat.SetColor("_OutlineColor", Color.Lerp(mainColor, Color.black, 0.7f));
            mat.SetFloat("_OutlineWidth", 0.008f);

            mat.SetColor("_EmissionColor", Color.black);
            mat.SetFloat("_EmissionIntensity", 0f);

            return mat;
        }

        /// <summary>
        /// Create a metallic toon material (for weapons, armor)
        /// </summary>
        public static Material CreateMetallicMaterial(Color mainColor)
        {
            Material mat = CreateToonMaterial(mainColor);
            mat.SetFloat("_SpecularIntensity", 0.5f);
            mat.SetFloat("_SpecularSize", 0.15f);
            mat.SetColor("_RimColor", Color.white);
            mat.SetFloat("_RimIntensity", 0.6f);
            return mat;
        }

        /// <summary>
        /// Create a glowing material (for magic effects)
        /// </summary>
        public static Material CreateGlowMaterial(Color mainColor, Color glowColor, float intensity = 1f)
        {
            Material mat = CreateToonMaterial(mainColor);
            mat.SetColor("_EmissionColor", glowColor);
            mat.SetFloat("_EmissionIntensity", intensity);
            mat.SetFloat("_RimIntensity", 0.8f);
            mat.SetColor("_RimColor", glowColor);
            return mat;
        }

        /// <summary>
        /// Get color palette for a unit based on its origin
        /// </summary>
        public static ColorPalette GetPaletteForUnit(UnitData template)
        {
            if (template == null || template.traits == null)
                return OriginColors.Human;

            foreach (var trait in template.traits)
            {
                if (trait == null) continue;

                string name = trait.traitName.ToLower();

                if (name == "human") return OriginColors.Human;
                if (name == "undead") return OriginColors.Undead;
                if (name == "beast") return OriginColors.Beast;
                if (name == "elemental") return OriginColors.Elemental;
                if (name == "demon") return OriginColors.Demon;
                if (name == "fey") return OriginColors.Fey;
            }

            return OriginColors.Human;
        }

        /// <summary>
        /// Get tier color based on unit cost
        /// </summary>
        public static Color GetTierColor(int cost)
        {
            return cost switch
            {
                1 => TierColors.Tier1,
                2 => TierColors.Tier2,
                3 => TierColors.Tier3,
                4 => TierColors.Tier4,
                5 => TierColors.Tier5,
                _ => TierColors.Tier1
            };
        }

        /// <summary>
        /// Get class accent color for weapons/accessories
        /// </summary>
        public static Color GetClassAccent(UnitData template)
        {
            if (template == null || template.traits == null)
                return ClassAccents.Warrior;

            foreach (var trait in template.traits)
            {
                if (trait == null) continue;

                string name = trait.traitName.ToLower();

                if (name == "warrior") return ClassAccents.Warrior;
                if (name == "ranger") return ClassAccents.Ranger;
                if (name == "mage") return ClassAccents.Mage;
                if (name == "tank") return ClassAccents.Tank;
                if (name == "assassin") return ClassAccents.Assassin;
                if (name == "support") return ClassAccents.Support;
                if (name == "berserker") return ClassAccents.Berserker;
                if (name == "summoner") return ClassAccents.Summoner;
            }

            return ClassAccents.Warrior;
        }
    }

    /// <summary>
    /// Color palette for unit origins
    /// </summary>
    public class ColorPalette
    {
        public Color Primary { get; }
        public Color Secondary { get; }
        public Color Accent { get; }
        public Color Shadow { get; }
        public Color Glow { get; }

        public ColorPalette(Color primary, Color secondary, Color accent, Color shadow, Color glow)
        {
            Primary = primary;
            Secondary = secondary;
            Accent = accent;
            Shadow = shadow;
            Glow = glow;
        }

        /// <summary>
        /// Blend this palette with a tier color
        /// </summary>
        public Color GetBlendedPrimary(int cost)
        {
            Color tierColor = MedievalVisualConfig.GetTierColor(cost);
            return Color.Lerp(Primary, tierColor, 0.3f);
        }
    }
}
