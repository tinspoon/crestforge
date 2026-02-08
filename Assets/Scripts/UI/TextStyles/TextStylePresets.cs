using UnityEngine;
using TMPro;

namespace Crestforge.UI
{
    /// <summary>
    /// Central collection of text styles for the game.
    /// Assign this to TextStyleManager or access via Resources.
    /// </summary>
    [CreateAssetMenu(fileName = "TextStylePresets", menuName = "Crestforge/UI/Text Style Presets")]
    public class TextStylePresets : ScriptableObject
    {
        [Header("Primary Font")]
        [Tooltip("Main font used throughout the game")]
        public TMP_FontAsset primaryFont;

        [Header("Game Styles")]
        public TextStyle title;
        public TextStyle header;
        public TextStyle body;
        public TextStyle button;
        public TextStyle buttonSmall;

        [Header("Combat Styles")]
        public TextStyle damageNumber;
        public TextStyle healNumber;
        public TextStyle criticalHit;
        public TextStyle unitName;
        public TextStyle healthBar;

        [Header("Resource Styles")]
        public TextStyle gold;
        public TextStyle xp;
        public TextStyle level;

        [Header("Card/Shop Styles")]
        public TextStyle cardName;
        public TextStyle cardCost;
        public TextStyle cardStats;
        public TextStyle traitName;

        [Header("HUD Styles")]
        public TextStyle timer;
        public TextStyle roundNumber;
        public TextStyle playerName;
        public TextStyle notification;

        /// <summary>
        /// Get a style by name (for dynamic access)
        /// </summary>
        public TextStyle GetStyle(string styleName)
        {
            return styleName.ToLower() switch
            {
                "title" => title,
                "header" => header,
                "body" => body,
                "button" => button,
                "buttonsmall" => buttonSmall,
                "damage" or "damagenumber" => damageNumber,
                "heal" or "healnumber" => healNumber,
                "crit" or "critical" or "criticalhit" => criticalHit,
                "unitname" => unitName,
                "healthbar" => healthBar,
                "gold" => gold,
                "xp" => xp,
                "level" => level,
                "cardname" => cardName,
                "cardcost" => cardCost,
                "cardstats" => cardStats,
                "traitname" => traitName,
                "timer" => timer,
                "round" or "roundnumber" => roundNumber,
                "playername" => playerName,
                "notification" => notification,
                _ => body
            };
        }
    }
}
