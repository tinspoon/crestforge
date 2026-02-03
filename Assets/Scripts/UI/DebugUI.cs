using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Systems;
using Crestforge.Data;
using Crestforge.Combat;

namespace Crestforge.UI
{
    /// <summary>
    /// Debug UI - compact left sidebar layout
    /// </summary>
    public class DebugUI : MonoBehaviour
    {
        private GameState state;
        private Vector2 scrollPosition;
        
        private float sidebarWidth = 280f;
        private System.Action deferredAction;
        
        private void Start()
        {
            state = GameState.Instance;
        }

        private void OnGUI()
        {
            if (state == null) 
            {
                state = GameState.Instance;
                if (state == null) return;
            }

            if (deferredAction != null)
            {
                var action = deferredAction;
                deferredAction = null;
                action.Invoke();
            }

            GUILayout.BeginArea(new Rect(5, 5, sidebarWidth, Screen.height - 10));
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            
            DrawCompactUI();
            
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawCompactUI()
        {
            GUILayout.Box("CRESTFORGE", GUILayout.ExpandWidth(true));
            
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"R{state.round.currentRound}", GUILayout.Width(30));
            GUILayout.Label($"❤{state.player.health}", GUILayout.Width(45));
            GUILayout.Label($"💰{state.player.gold}", GUILayout.Width(45));
            GUILayout.Label($"Lv{state.player.level}", GUILayout.Width(35));
            GUILayout.Label($"👥{state.GetBoardUnitCount()}/{state.player.GetMaxUnits()}", GUILayout.Width(45));
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            switch (state.round.phase)
            {
                case GamePhase.CrestSelect:
                    DrawCrestSelection();
                    break;
                case GamePhase.ItemSelect:
                    DrawItemSelection();
                    break;
                case GamePhase.Planning:
                    DrawPlanningUI();
                    break;
                case GamePhase.Combat:
                    DrawCombatUI();
                    break;
                case GamePhase.Results:
                    GUILayout.Box("Battle Complete...", GUILayout.ExpandWidth(true));
                    break;
                case GamePhase.GameOver:
                    DrawGameOver();
                    break;
            }
        }

        private void DrawCrestSelection()
        {
            GUILayout.Box("SELECT STARTING CREST", GUILayout.ExpandWidth(true));
            
            if (state.pendingCrestSelection == null) return;
            
            var crestsCopy = new List<CrestData>(state.pendingCrestSelection);
            foreach (var crest in crestsCopy)
            {
                if (crest == null) continue;
                GUI.backgroundColor = new Color(0.4f, 0.6f, 0.8f);
                if (GUILayout.Button($"{crest.crestName}\n{crest.description}", GUILayout.Height(55)))
                {
                    var selectedCrest = crest;
                    deferredAction = () => RoundManager.Instance.OnCrestSelected(selectedCrest);
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawItemSelection()
        {
            GUILayout.Box("SELECT AN ITEM", GUILayout.ExpandWidth(true));
            
            if (state.pendingItemSelection == null) return;
            
            var itemsCopy = new List<ItemData>(state.pendingItemSelection);
            foreach (var item in itemsCopy)
            {
                if (item == null) continue;
                GUI.backgroundColor = GetRarityColor(item.rarity);
                if (GUILayout.Button($"{item.itemName} [{item.rarity}]\n{item.description}", GUILayout.Height(45)))
                {
                    var selectedItem = item;
                    deferredAction = () => RoundManager.Instance.OnItemSelected(selectedItem);
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawPlanningUI()
        {
            // Timer & Combat button
            GUILayout.BeginHorizontal();
            GUILayout.Label($"⏱ {state.round.phaseTimer:F0}s", GUILayout.Width(50));
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("⚔ FIGHT!", GUILayout.Height(30)))
            {
                deferredAction = () => RoundManager.Instance.StartCombatPhase();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Shop
            GUILayout.Box("SHOP", GUILayout.ExpandWidth(true));
            
            if (state.shop == null || state.shop.availableUnits == null) return;
            
            var shopCopy = new List<UnitInstance>(state.shop.availableUnits);
            for (int i = 0; i < shopCopy.Count; i++)
            {
                var unit = shopCopy[i];
                if (unit == null)
                {
                    GUI.enabled = false;
                    GUILayout.Button("(sold)", GUILayout.Height(25));
                    GUI.enabled = true;
                    continue;
                }

                if (unit.template == null) continue;

                GUI.backgroundColor = GetCostColor(unit.template.cost);
                bool canAfford = state.player.gold >= unit.template.cost;
                GUI.enabled = canAfford;
                
                string traits = GetTraitString(unit);
                
                int index = i;
                if (GUILayout.Button($"${unit.template.cost} {unit.template.unitName} ({traits})", GUILayout.Height(28)))
                {
                    deferredAction = () => state.BuyUnit(index);
                }
                GUI.enabled = true;
            }
            GUI.backgroundColor = Color.white;

            // Shop buttons
            GUILayout.BeginHorizontal();
            GUI.enabled = state.player.gold >= GameConstants.Economy.REROLL_COST;
            if (GUILayout.Button($"🔄 ${GameConstants.Economy.REROLL_COST}"))
            {
                deferredAction = () => state.RerollShop();
            }
            GUI.enabled = state.player.gold >= GameConstants.Economy.XP_COST && state.player.level < GameConstants.Leveling.MAX_LEVEL;
            if (GUILayout.Button($"📈 ${GameConstants.Economy.XP_COST} XP"))
            {
                deferredAction = () => state.BuyXP();
            }
            GUI.enabled = true;
            
            string lockIcon = state.shop.isLocked ? "🔒" : "🔓";
            if (GUILayout.Button(lockIcon, GUILayout.Width(30)))
            {
                state.shop.isLocked = !state.shop.isLocked;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Bench
            int benchCount = state.GetBenchUnitCount();
            GUILayout.Box($"BENCH ({benchCount}/{GameConstants.Player.BENCH_SIZE})", GUILayout.ExpandWidth(true));

            if (benchCount > 0)
            {
                var benchUnits = state.GetBenchUnits();
                foreach (var unit in benchUnits)
                {
                    if (unit == null || unit.template == null) continue;
                    GUI.backgroundColor = GetCostColor(unit.template.cost);
                    string stars = new string('★', unit.starLevel);
                    if (GUILayout.Button($"{unit.template.unitName} {stars} → Board", GUILayout.Height(25)))
                    {
                        var unitToPlace = unit;
                        deferredAction = () => TryPlaceUnit(unitToPlace);
                    }
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUILayout.Label("(empty - buy units!)");
            }

            GUILayout.Space(5);

            // Board units
            GUILayout.Box("BOARD (click to bench)", GUILayout.ExpandWidth(true));
            
            bool hasUnits = false;
            if (state.playerBoard != null)
            {
                for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                {
                    for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                    {
                        var unit = state.playerBoard[x, y];
                        if (unit != null && unit.template != null)
                        {
                            hasUnits = true;
                            GUI.backgroundColor = GetCostColor(unit.template.cost);
                            string stars = new string('★', unit.starLevel);
                            if (GUILayout.Button($"{unit.template.unitName} {stars} → Bench", GUILayout.Height(22)))
                            {
                                var unitToReturn = unit;
                                deferredAction = () => state.ReturnToBench(unitToReturn);
                            }
                        }
                    }
                }
            }
            GUI.backgroundColor = Color.white;
            
            if (!hasUnits)
            {
                GUILayout.Label("(no units placed)");
            }

            GUILayout.Space(5);

            // Active traits
            if (state.activeTraits != null && state.activeTraits.Count > 0)
            {
                GUILayout.Box("TRAITS", GUILayout.ExpandWidth(true));
                var traitsCopy = new List<KeyValuePair<TraitData, int>>(state.activeTraits);
                foreach (var trait in traitsCopy)
                {
                    if (trait.Key == null) continue;
                    int tier = trait.Key.GetActiveTier(trait.Value);
                    string tierStr = tier >= 0 ? $"✓T{tier + 1}" : "";
                    GUILayout.Label($"{trait.Key.traitName}: {trait.Value} {tierStr}");
                }
            }

            // Crests
            if ((state.minorCrests != null && state.minorCrests.Count > 0) || 
                (state.majorCrests != null && state.majorCrests.Count > 0))
            {
                GUILayout.Box("CRESTS", GUILayout.ExpandWidth(true));
                if (state.minorCrests != null)
                {
                    for (int i = 0; i < state.minorCrests.Count; i++)
                    {
                        if (state.minorCrests[i] != null)
                            GUILayout.Label($"• {state.minorCrests[i].crestName}");
                    }
                }
                if (state.majorCrests != null)
                {
                    for (int i = 0; i < state.majorCrests.Count; i++)
                    {
                        if (state.majorCrests[i] != null)
                            GUILayout.Label($"★ {state.majorCrests[i].crestName}");
                    }
                }
            }

            // Items
            if (state.itemInventory != null && state.itemInventory.Count > 0)
            {
                GUILayout.Box("ITEMS", GUILayout.ExpandWidth(true));
                var itemsCopy = new List<ItemData>(state.itemInventory);
                foreach (var item in itemsCopy)
                {
                    if (item != null)
                        GUILayout.Label($"• {item.itemName}");
                }
            }
        }

        private void DrawCombatUI()
        {
            GUILayout.Box("⚔ COMBAT ⚔", GUILayout.ExpandWidth(true));
            
            var combat = CombatManager.Instance;
            if (combat != null && combat.allUnits != null)
            {
                GUILayout.Label($"Time: {combat.combatTime:F1}s");
                
                int playerAlive = 0;
                int enemyAlive = 0;
                
                var unitsCopy = new List<CombatUnit>(combat.allUnits);
                foreach (var unit in unitsCopy)
                {
                    if (unit != null && !unit.isDead)
                    {
                        if (unit.team == Team.Player) playerAlive++;
                        else enemyAlive++;
                    }
                }
                
                GUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f);
                GUILayout.Box($"YOU: {playerAlive}", GUILayout.Width(80));
                GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                GUILayout.Box($"ENEMY: {enemyAlive}", GUILayout.Width(80));
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("Watch the battle on the grid! →");
        }

        private void DrawGameOver()
        {
            if (state.player.health <= 0)
            {
                GUI.backgroundColor = new Color(0.5f, 0.2f, 0.2f);
                GUILayout.Box("💀 DEFEAT 💀", GUILayout.ExpandWidth(true));
                GUI.backgroundColor = Color.white;
                GUILayout.Label($"Made it to round {state.round.currentRound}");
            }
            else
            {
                GUI.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
                GUILayout.Box("🏆 VICTORY! 🏆", GUILayout.ExpandWidth(true));
                GUI.backgroundColor = Color.white;
                GUILayout.Label("All rounds completed!");
            }

            GUILayout.Space(10);
            
            if (GUILayout.Button("Play Again", GUILayout.Height(40)))
            {
                deferredAction = () => RoundManager.Instance.StartGame();
            }
        }

        private string GetTraitString(UnitInstance unit)
        {
            if (unit == null || unit.template == null || unit.template.traits == null)
                return "";
                
            string traits = "";
            foreach (var t in unit.template.traits)
            {
                if (t == null) continue;
                if (traits.Length > 0) traits += "/";
                traits += t.traitName.Substring(0, Mathf.Min(3, t.traitName.Length));
            }
            return traits;
        }

        private void TryPlaceUnit(UnitInstance unit)
        {
            if (state.playerBoard == null) return;
            
            for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    if (state.playerBoard[x, y] == null)
                    {
                        if (state.PlaceUnit(unit, x, y))
                        {
                            return;
                        }
                    }
                }
            }
        }

        private Color GetCostColor(int cost)
        {
            return cost switch
            {
                1 => new Color(0.6f, 0.6f, 0.6f),
                2 => new Color(0.3f, 0.7f, 0.3f),
                3 => new Color(0.3f, 0.5f, 0.9f),
                4 => new Color(0.7f, 0.3f, 0.7f),
                _ => Color.white
            };
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.7f, 0.7f, 0.7f),
                ItemRarity.Uncommon => new Color(0.3f, 0.7f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 0.9f),
                _ => Color.white
            };
        }
    }
}