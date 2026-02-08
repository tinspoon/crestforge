using UnityEngine;
using System.Collections.Generic;

namespace Crestforge.UI
{
    /// <summary>
    /// Loads pre-baked unit portraits from Resources.
    /// Portraits are generated in the Editor via Crestforge > Bake Unit Portraits.
    /// Falls back to pixel art sprites if no portrait exists.
    /// </summary>
    public static class UnitPortraitGenerator
    {
        // Cache loaded portraits
        private static Dictionary<string, Sprite> portraitCache = new Dictionary<string, Sprite>();

        // Track which units we've already tried to load (to avoid repeated failed lookups)
        private static HashSet<string> attemptedLoads = new HashSet<string>();

        private const string PORTRAIT_PATH = "UnitPortraits/";

        /// <summary>
        /// Get a portrait sprite for a unit.
        /// Returns pre-baked 3D portrait if available, otherwise falls back to pixel art.
        /// </summary>
        public static Sprite GetPortrait(string unitId, string unitName)
        {
            if (string.IsNullOrEmpty(unitId) && string.IsNullOrEmpty(unitName))
                return null;

            // Generate cache key from unit name (matches baked filename)
            string key = SanitizeKey(unitName ?? unitId);

            // Check cache first
            if (portraitCache.TryGetValue(key, out Sprite cached))
                return cached;

            // Try to load pre-baked portrait
            if (!attemptedLoads.Contains(key))
            {
                attemptedLoads.Add(key);

                string resourcePath = PORTRAIT_PATH + key + "_portrait";
                Sprite portrait = Resources.Load<Sprite>(resourcePath);

                if (portrait != null)
                {
                    portraitCache[key] = portrait;
                    return portrait;
                }
            }

            // Fall back to pixel art sprite
            Sprite pixelArt = UnitSpriteGenerator.GetSprite(unitId);
            if (pixelArt != null)
            {
                portraitCache[key] = pixelArt;
            }

            return pixelArt;
        }

        /// <summary>
        /// Check if a pre-baked portrait exists for this unit
        /// </summary>
        public static bool HasPortrait(string unitName)
        {
            if (string.IsNullOrEmpty(unitName))
                return false;

            string key = SanitizeKey(unitName);

            // Check cache
            if (portraitCache.ContainsKey(key))
                return true;

            // Try to load
            string resourcePath = PORTRAIT_PATH + key + "_portrait";
            return Resources.Load<Sprite>(resourcePath) != null;
        }

        /// <summary>
        /// Preload all portraits into cache (call during loading screen)
        /// </summary>
        public static void PreloadAll()
        {
            Sprite[] allPortraits = Resources.LoadAll<Sprite>("UnitPortraits");
            foreach (var portrait in allPortraits)
            {
                if (portrait.name.EndsWith("_portrait"))
                {
                    string key = portrait.name.Replace("_portrait", "");
                    portraitCache[key] = portrait;
                }
            }

            Debug.Log($"[UnitPortraitGenerator] Preloaded {portraitCache.Count} portraits");
        }

        /// <summary>
        /// Clear the cache (call on scene unload if needed)
        /// </summary>
        public static void ClearCache()
        {
            portraitCache.Clear();
            attemptedLoads.Clear();
        }

        /// <summary>
        /// Sanitize unit name to match baked filename format
        /// </summary>
        private static string SanitizeKey(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";

            return name.ToLower().Replace(" ", "_").Replace("-", "_");
        }
    }
}
