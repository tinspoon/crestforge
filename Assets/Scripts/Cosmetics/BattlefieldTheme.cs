using UnityEngine;
using System.Collections.Generic;

namespace Crestforge.Cosmetics
{
    /// <summary>
    /// Defines a complete battlefield visual theme that can be swapped at runtime.
    /// Themes change the look of the battlefield while preserving game mechanics.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBattlefieldTheme", menuName = "Crestforge/Battlefield Theme")]
    public class BattlefieldTheme : ScriptableObject
    {
        [Header("Identity")]
        public string themeId;
        public string themeName;
        [TextArea(2, 4)]
        public string description;
        public Sprite previewImage;

        [Header("Unlock")]
        public ThemeUnlockType unlockType = ThemeUnlockType.Default;
        public string unlockRequirement;  // Achievement ID, level, etc.
        public int purchasePrice;         // If purchasable

        [Header("Ground")]
        [Tooltip("Skip spawning any ground plane for this theme.")]
        public bool skipGround = false;
        [Tooltip("Prefab for the ground plane under the hex grid. If null, uses procedural ground.")]
        public GameObject groundPrefab;
        [Tooltip("Material to apply to procedural ground if no prefab.")]
        public Material groundMaterial;
        [Tooltip("Ground color if using procedural generation.")]
        public Color groundColor = new Color(0.3f, 0.5f, 0.2f);
        [Tooltip("Size of the ground plane.")]
        public Vector2 groundSize = new Vector2(12f, 10f);

        [Header("Hex Tiles")]
        [Tooltip("Material for player-side hex tiles. If null, uses default.")]
        public Material playerHexMaterial;
        [Tooltip("Material for enemy-side hex tiles. If null, uses default.")]
        public Material enemyHexMaterial;
        [Tooltip("Player hex color if using procedural materials.")]
        public Color playerHexColor = new Color(0.35f, 0.55f, 0.25f);
        [Tooltip("Enemy hex color if using procedural materials.")]
        public Color enemyHexColor = new Color(0.45f, 0.55f, 0.30f);

        [Header("Bench")]
        [Tooltip("Skip spawning any bench visual for this theme.")]
        public bool skipBench = false;
        [Tooltip("Prefab for the bench area visual. Spawned behind player hexes.")]
        public GameObject benchPrefab;
        [Tooltip("Offset from default bench position.")]
        public Vector3 benchOffset = Vector3.zero;
        [Tooltip("Scale multiplier for bench prefab.")]
        public float benchScale = 1f;

        [Header("Environment Zones")]
        [Tooltip("Objects to place in each zone around the battlefield.")]
        public List<ThemeZonePlacement> zonePlacements = new List<ThemeZonePlacement>();

        [Header("Ambient Effects")]
        [Tooltip("Particle system prefab for ambient effects (leaves, fireflies, etc.)")]
        public GameObject ambientParticlesPrefab;
        [Tooltip("Position offset for ambient particles.")]
        public Vector3 ambientParticlesOffset = new Vector3(0, 2f, 0);

        [Header("Audio")]
        [Tooltip("Ambient audio clip for this theme.")]
        public AudioClip ambientAudio;
        [Range(0f, 1f)]
        public float ambientVolume = 0.3f;

        /// <summary>
        /// Get all placements for a specific zone
        /// </summary>
        public List<ThemeZonePlacement> GetPlacementsForZone(BattlefieldZone zone)
        {
            return zonePlacements.FindAll(p => p.zone == zone);
        }
    }

    /// <summary>
    /// How a theme is unlocked
    /// </summary>
    public enum ThemeUnlockType
    {
        Default,        // Everyone has it
        Achievement,    // Unlock via achievement
        Progression,    // Unlock at certain level/wins
        Purchase,       // Buy with premium currency
        Event,          // Limited time event
        Exclusive       // Special/promotional
    }

    /// <summary>
    /// Zones around the battlefield where decorations can be placed
    /// </summary>
    public enum BattlefieldZone
    {
        Ground,             // Under/around the hex grid
        BackLeft,           // Behind player, left corner
        BackRight,          // Behind player, right corner
        BackCenter,         // Behind player, center
        SideLeft,           // Left side of battlefield
        SideRight,          // Right side of battlefield
        FrontLeft,          // Enemy side, left
        FrontRight,         // Enemy side, right
        FrontCenter,        // Enemy side, center (careful - units fight here)
        BenchArea,          // Around the bench
        Surrounding         // Far perimeter decorations
    }

    /// <summary>
    /// A single object placement within a theme zone
    /// </summary>
    [System.Serializable]
    public class ThemeZonePlacement
    {
        public BattlefieldZone zone;
        [Tooltip("Prefab to spawn. If null, uses procedural generation based on objectType.")]
        public GameObject prefab;
        [Tooltip("Type of object for procedural generation if no prefab.")]
        public ProceduralObjectType objectType = ProceduralObjectType.None;
        [Tooltip("Local position offset within the zone.")]
        public Vector3 positionOffset;
        [Tooltip("Rotation in euler angles.")]
        public Vector3 rotation;
        [Tooltip("Scale multiplier.")]
        public Vector3 scale = Vector3.one;
        [Tooltip("Random position variance for natural look.")]
        public float positionVariance = 0f;
        [Tooltip("Random rotation variance (Y axis).")]
        public float rotationVariance = 0f;
        [Tooltip("Random scale variance.")]
        public float scaleVariance = 0f;
    }

    /// <summary>
    /// Types of procedurally generated objects (for themes without prefabs)
    /// </summary>
    public enum ProceduralObjectType
    {
        None,
        Tree,
        Bush,
        Rock,
        Flower,
        Grass,
        Fence,
        Torch,
        Pillar,
        Crate,
        Barrel,
        Flag
    }
}
