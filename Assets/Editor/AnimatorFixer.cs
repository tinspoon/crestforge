using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

namespace Crestforge.Editor
{
    /// <summary>
    /// Fixes animator controllers that have automatic transitions from idle to attack
    /// </summary>
    public class AnimatorFixer : EditorWindow
    {
        [MenuItem("Crestforge/Remove ALL Animator Transitions")]
        public static void RemoveAllTransitions()
        {
            string[] searchPaths = new string[]
            {
                "Assets/RPGMonsterWave4",
                "Assets/RPG Monster Wave PBR",
                "Assets/RPGMonsterWave02PBR",
                "Assets/RPGMonsterWave03PBR",
                "Assets/RPGTinyHeroWavePBR"
            };

            int totalTransitionsRemoved = 0;
            int controllersModified = 0;

            foreach (string searchPath in searchPaths)
            {
                if (!AssetDatabase.IsValidFolder(searchPath))
                    continue;

                string[] guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { searchPath });

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

                    if (controller == null) continue;

                    int removed = RemoveAllTransitionsFromController(controller);
                    if (removed > 0)
                    {
                        controllersModified++;
                        totalTransitionsRemoved += removed;
                        EditorUtility.SetDirty(controller);
                    }
                }
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Animator Fixer",
                $"Removed ALL {totalTransitionsRemoved} transitions from {controllersModified} controllers.\n\n" +
                "Animations will now only play via direct code control (Animator.Play or CrossFade).",
                "OK");

            Debug.Log($"[AnimatorFixer] Removed ALL {totalTransitionsRemoved} transitions from {controllersModified} controllers");
        }

        private static int RemoveAllTransitionsFromController(AnimatorController controller)
        {
            int removed = 0;

            foreach (var layer in controller.layers)
            {
                removed += RemoveAllTransitionsFromStateMachine(layer.stateMachine);
            }

            return removed;
        }

        private static int RemoveAllTransitionsFromStateMachine(AnimatorStateMachine stateMachine)
        {
            int removed = 0;

            // Remove transitions from all states
            foreach (var state in stateMachine.states)
            {
                AnimatorStateTransition[] transitions = state.state.transitions;
                foreach (var transition in transitions)
                {
                    state.state.RemoveTransition(transition);
                    removed++;
                }
            }

            // Remove Any State transitions
            AnimatorStateTransition[] anyStateTransitions = stateMachine.anyStateTransitions;
            foreach (var transition in anyStateTransitions)
            {
                stateMachine.RemoveAnyStateTransition(transition);
                removed++;
            }

            // Recursively handle sub-state machines
            foreach (var subMachine in stateMachine.stateMachines)
            {
                removed += RemoveAllTransitionsFromStateMachine(subMachine.stateMachine);
            }

            return removed;
        }

        [MenuItem("Crestforge/Fix Animator Transitions")]
        public static void FixAnimators()
        {
            // Find all animator controllers in the asset packs
            string[] searchPaths = new string[]
            {
                "Assets/RPGMonsterWave4",
                "Assets/RPG Monster Wave PBR",
                "Assets/RPGMonsterWave02PBR",
                "Assets/RPGMonsterWave03PBR",
                "Assets/RPGTinyHeroWavePBR"
            };

            List<string> fixedControllers = new List<string>();
            int totalTransitionsRemoved = 0;

            foreach (string searchPath in searchPaths)
            {
                if (!AssetDatabase.IsValidFolder(searchPath))
                    continue;

                string[] guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { searchPath });

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

                    if (controller == null) continue;

                    int removed = FixController(controller);
                    if (removed > 0)
                    {
                        fixedControllers.Add($"{controller.name}: {removed} transitions");
                        totalTransitionsRemoved += removed;
                        EditorUtility.SetDirty(controller);
                    }
                }
            }

            AssetDatabase.SaveAssets();

            string message = $"Fixed {fixedControllers.Count} animator controllers.\n" +
                           $"Removed {totalTransitionsRemoved} auto-transitions.\n\n";

            if (fixedControllers.Count > 0)
            {
                message += "Fixed controllers:\n" + string.Join("\n", fixedControllers.GetRange(0, Mathf.Min(10, fixedControllers.Count)));
                if (fixedControllers.Count > 10)
                    message += $"\n... and {fixedControllers.Count - 10} more";
            }

            EditorUtility.DisplayDialog("Animator Fixer", message, "OK");
            Debug.Log($"[AnimatorFixer] Complete - Fixed {fixedControllers.Count} controllers, removed {totalTransitionsRemoved} transitions");
        }

        private static int FixController(AnimatorController controller)
        {
            int transitionsRemoved = 0;

            foreach (var layer in controller.layers)
            {
                AnimatorStateMachine stateMachine = layer.stateMachine;
                transitionsRemoved += FixStateMachine(stateMachine);
            }

            return transitionsRemoved;
        }

        private static int FixStateMachine(AnimatorStateMachine stateMachine)
        {
            int removed = 0;

            foreach (var state in stateMachine.states)
            {
                AnimatorState animState = state.state;
                string stateName = animState.name.ToLower();

                // Check if this is an idle state
                bool isIdleState = stateName.Contains("idle") ||
                                   stateName.Contains("wait") ||
                                   stateName.Contains("stand");

                if (!isIdleState) continue;

                // Check transitions from this idle state
                List<AnimatorStateTransition> transitionsToRemove = new List<AnimatorStateTransition>();

                foreach (var transition in animState.transitions)
                {
                    if (transition.destinationState == null) continue;

                    string destName = transition.destinationState.name.ToLower();

                    // Check if transitioning to attack/combat state
                    bool isAttackDest = destName.Contains("attack") ||
                                        destName.Contains("hit") ||
                                        destName.Contains("slash") ||
                                        destName.Contains("strike") ||
                                        destName.Contains("cast") ||
                                        destName.Contains("shoot");

                    if (!isAttackDest) continue;

                    // Check if it's an automatic transition (has exit time and no conditions, or just no conditions)
                    bool isAutoTransition = transition.conditions.Length == 0;

                    if (isAutoTransition)
                    {
                        transitionsToRemove.Add(transition);
                        Debug.Log($"[AnimatorFixer] Removing auto-transition: {animState.name} -> {transition.destinationState.name}");
                    }
                }

                // Remove the problematic transitions
                foreach (var transition in transitionsToRemove)
                {
                    animState.RemoveTransition(transition);
                    removed++;
                }
            }

            // Recursively fix sub-state machines
            foreach (var subMachine in stateMachine.stateMachines)
            {
                removed += FixStateMachine(subMachine.stateMachine);
            }

            return removed;
        }

        [MenuItem("Crestforge/Restore Attack Transitions")]
        public static void RestoreAttackTransitions()
        {
            string[] searchPaths = new string[]
            {
                "Assets/RPGMonsterWave4",
                "Assets/RPG Monster Wave PBR",
                "Assets/RPGMonsterWave02PBR",
                "Assets/RPGMonsterWave03PBR",
                "Assets/RPGTinyHeroWavePBR"
            };

            int transitionsAdded = 0;
            int controllersModified = 0;

            foreach (string searchPath in searchPaths)
            {
                if (!AssetDatabase.IsValidFolder(searchPath))
                    continue;

                string[] guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { searchPath });

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

                    if (controller == null) continue;

                    int added = RestoreTransitionsForController(controller);
                    if (added > 0)
                    {
                        controllersModified++;
                        transitionsAdded += added;
                        EditorUtility.SetDirty(controller);
                    }
                }
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Animator Fixer",
                $"Restored {transitionsAdded} transitions in {controllersModified} controllers.\n\n" +
                "Added 'Attack' trigger parameter and transitions from idle to attack states.",
                "OK");

            Debug.Log($"[AnimatorFixer] Restored {transitionsAdded} transitions in {controllersModified} controllers");
        }

        private static int RestoreTransitionsForController(AnimatorController controller)
        {
            int added = 0;

            // Ensure "Attack" trigger parameter exists
            bool hasAttackParam = false;
            foreach (var param in controller.parameters)
            {
                if (param.name == "Attack" && param.type == AnimatorControllerParameterType.Trigger)
                {
                    hasAttackParam = true;
                    break;
                }
            }

            if (!hasAttackParam)
            {
                controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            }

            foreach (var layer in controller.layers)
            {
                added += RestoreTransitionsInStateMachine(layer.stateMachine, controller);
            }

            return added;
        }

        private static int RestoreTransitionsInStateMachine(AnimatorStateMachine stateMachine, AnimatorController controller)
        {
            int added = 0;

            // Find all idle and attack states
            List<AnimatorState> idleStates = new List<AnimatorState>();
            List<AnimatorState> attackStates = new List<AnimatorState>();

            foreach (var state in stateMachine.states)
            {
                string name = state.state.name.ToLower();

                // Find idle states (including IdleBattle, IdleNormal, etc.)
                if (name.Contains("idle"))
                {
                    idleStates.Add(state.state);
                }

                // Find attack states
                if (name.Contains("attack") || name.Contains("slash") || name.Contains("strike"))
                {
                    attackStates.Add(state.state);
                }
            }

            // Add transitions from ALL idle states to the first attack state
            if (attackStates.Count > 0)
            {
                AnimatorState attackState = attackStates[0]; // Use first attack state

                foreach (var idleState in idleStates)
                {
                    // Check if transition already exists with Attack condition
                    bool hasProperTransition = false;
                    foreach (var t in idleState.transitions)
                    {
                        if (t.destinationState == attackState)
                        {
                            // Check if it has the Attack condition
                            foreach (var cond in t.conditions)
                            {
                                if (cond.parameter == "Attack")
                                {
                                    hasProperTransition = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!hasProperTransition)
                    {
                        // Add transition with Attack trigger condition
                        AnimatorStateTransition transition = idleState.AddTransition(attackState);
                        transition.hasExitTime = false;
                        transition.duration = 0.1f;
                        transition.AddCondition(AnimatorConditionMode.If, 0, "Attack");

                        Debug.Log($"[AnimatorFixer] Added transition: {idleState.name} -> {attackState.name} in {controller.name}");
                        added++;
                    }
                }
            }

            // Recursively handle sub-state machines
            foreach (var subMachine in stateMachine.stateMachines)
            {
                added += RestoreTransitionsInStateMachine(subMachine.stateMachine, controller);
            }

            return added;
        }

        [MenuItem("Crestforge/Add Victory States")]
        public static void AddVictoryStates()
        {
            string[] searchPaths = new string[]
            {
                "Assets/RPGMonsterWave4",
                "Assets/RPG Monster Wave PBR",
                "Assets/RPGMonsterWave02PBR",
                "Assets/RPGMonsterWave03PBR",
                "Assets/RPGTinyHeroWavePBR",
                "Assets/RPG Monster DUO PBR Polyart"
            };

            int statesAdded = 0;
            int controllersModified = 0;

            foreach (string searchPath in searchPaths)
            {
                if (!AssetDatabase.IsValidFolder(searchPath))
                    continue;

                // Find all animator controllers
                string[] controllerGuids = AssetDatabase.FindAssets("t:AnimatorController", new[] { searchPath });

                foreach (string guid in controllerGuids)
                {
                    string controllerPath = AssetDatabase.GUIDToAssetPath(guid);
                    AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                    if (controller == null) continue;

                    // Get the folder containing this controller
                    string controllerFolder = System.IO.Path.GetDirectoryName(controllerPath);
                    string parentFolder = System.IO.Path.GetDirectoryName(controllerFolder);

                    // Search for victory animation clips in the same folder and parent folder
                    List<AnimationClip> victoryClips = FindVictoryClips(controllerFolder);
                    victoryClips.AddRange(FindVictoryClips(parentFolder));

                    if (victoryClips.Count == 0) continue;

                    // Check if controller already has a Victory state
                    bool hasVictoryState = false;
                    AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
                    foreach (var state in stateMachine.states)
                    {
                        if (state.state.name.ToLower().Contains("victory"))
                        {
                            hasVictoryState = true;
                            break;
                        }
                    }

                    if (hasVictoryState)
                    {
                        Debug.Log($"[AnimatorFixer] {controller.name} already has Victory state");
                        continue;
                    }

                    // Add the first victory clip as a state
                    AnimationClip victoryClip = victoryClips[0];
                    AnimatorState victoryState = stateMachine.AddState("Victory");
                    victoryState.motion = victoryClip;

                    // Make it loop
                    var settings = AnimationUtility.GetAnimationClipSettings(victoryClip);
                    settings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(victoryClip, settings);

                    Debug.Log($"[AnimatorFixer] Added Victory state to {controller.name} using clip: {victoryClip.name}");

                    EditorUtility.SetDirty(controller);
                    statesAdded++;
                    controllersModified++;
                }
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Add Victory States",
                $"Added {statesAdded} Victory states to {controllersModified} animator controllers.",
                "OK");

            Debug.Log($"[AnimatorFixer] Added {statesAdded} Victory states to {controllersModified} controllers");
        }

        private static List<AnimationClip> FindVictoryClips(string folder)
        {
            List<AnimationClip> clips = new List<AnimationClip>();

            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                return clips;

            // Search recursively for FBX files with "victory" in the name
            string[] fbxGuids = AssetDatabase.FindAssets("victory", new[] { folder });

            foreach (string guid in fbxGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Load all sub-assets (animation clips are sub-assets of FBX files)
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in subAssets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        clips.Add(clip);
                    }
                }
            }

            return clips;
        }

        [MenuItem("Crestforge/List Animator Issues")]
        public static void ListIssues()
        {
            string[] searchPaths = new string[]
            {
                "Assets/RPGMonsterWave4",
                "Assets/RPG Monster Wave PBR",
                "Assets/RPGMonsterWave02PBR",
                "Assets/RPGMonsterWave03PBR",
                "Assets/RPGTinyHeroWavePBR"
            };

            Debug.Log("[AnimatorFixer] ========== Scanning for animation issues ==========");

            int issueCount = 0;

            foreach (string searchPath in searchPaths)
            {
                if (!AssetDatabase.IsValidFolder(searchPath))
                    continue;

                string[] guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { searchPath });

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

                    if (controller == null) continue;

                    foreach (var layer in controller.layers)
                    {
                        foreach (var state in layer.stateMachine.states)
                        {
                            string stateName = state.state.name.ToLower();
                            bool isIdleState = stateName.Contains("idle") || stateName.Contains("wait");

                            if (!isIdleState) continue;

                            foreach (var transition in state.state.transitions)
                            {
                                if (transition.destinationState == null) continue;

                                string destName = transition.destinationState.name.ToLower();
                                bool isAttackDest = destName.Contains("attack") || destName.Contains("hit");

                                if (isAttackDest && transition.conditions.Length == 0)
                                {
                                    Debug.LogWarning($"[AnimatorFixer] Issue in {controller.name}: " +
                                                   $"{state.state.name} -> {transition.destinationState.name} " +
                                                   $"(no conditions, hasExitTime={transition.hasExitTime})");
                                    issueCount++;
                                }
                            }
                        }
                    }
                }
            }

            Debug.Log($"[AnimatorFixer] ========== Found {issueCount} issues ==========");
        }
    }
}
