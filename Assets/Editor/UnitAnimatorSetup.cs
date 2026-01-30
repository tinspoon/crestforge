#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

namespace Crestforge.Editor
{
    /// <summary>
    /// Tools for setting up unit animations
    /// </summary>
    public class UnitAnimatorSetup : EditorWindow
    {
        [MenuItem("Crestforge/Unit Models/Create Unit Animator Controller")]
        public static void CreateAnimatorController()
        {
            // Ensure folder exists
            string folderPath = "Assets/Animations";
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets", "Animations");

            string controllerPath = folderPath + "/UnitAnimatorController.controller";

            // Check if already exists
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                Debug.Log("UnitAnimatorController already exists at " + controllerPath);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                return;
            }

            // Create animator controller
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Add parameters
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            // Get the root state machine
            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

            // Create states (names match Quaternius clip names with CharacterArmature| prefix)
            AnimatorState idleState = rootStateMachine.AddState("CharacterArmature|Idle", new Vector3(250, 0, 0));
            AnimatorState walkState = rootStateMachine.AddState("CharacterArmature|Walk", new Vector3(250, 100, 0));
            AnimatorState attackState = rootStateMachine.AddState("CharacterArmature|Punch", new Vector3(500, 0, 0));
            AnimatorState hitState = rootStateMachine.AddState("CharacterArmature|Hit", new Vector3(500, 100, 0));
            AnimatorState deathState = rootStateMachine.AddState("CharacterArmature|Death", new Vector3(500, 200, 0));

            // Set default state
            rootStateMachine.defaultState = idleState;

            // Create transitions
            // Idle -> Walk (when IsMoving = true)
            AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            idleToWalk.duration = 0.1f;
            idleToWalk.hasExitTime = false;

            // Walk -> Idle (when IsMoving = false)
            AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            walkToIdle.duration = 0.1f;
            walkToIdle.hasExitTime = false;

            // Any -> Attack (on Attack trigger)
            AnimatorStateTransition anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAttack.duration = 0.1f;
            anyToAttack.hasExitTime = false;

            // Attack -> Idle (after animation)
            AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.9f;
            attackToIdle.duration = 0.1f;

            // Any -> Hit (on Hit trigger)
            AnimatorStateTransition anyToHit = rootStateMachine.AddAnyStateTransition(hitState);
            anyToHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            anyToHit.duration = 0.05f;
            anyToHit.hasExitTime = false;

            // Hit -> Idle (after animation)
            AnimatorStateTransition hitToIdle = hitState.AddTransition(idleState);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.9f;
            hitToIdle.duration = 0.1f;

            // Any -> Death (on Death trigger)
            AnimatorStateTransition anyToDeath = rootStateMachine.AddAnyStateTransition(deathState);
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");
            anyToDeath.duration = 0.1f;
            anyToDeath.hasExitTime = false;

            AssetDatabase.SaveAssets();
            Debug.Log("Created UnitAnimatorController at " + controllerPath);
            Selection.activeObject = controller;
        }

        [MenuItem("Crestforge/Unit Models/Setup Selected FBX Animations")]
        public static void SetupSelectedFBXAnimations()
        {
            Object[] selection = Selection.objects;
            int count = 0;

            foreach (Object obj in selection)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                // Set animation type to Generic (works with most models)
                if (importer.animationType != ModelImporterAnimationType.Generic)
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.SaveAndReimport();
                    count++;
                    Debug.Log($"Set {obj.name} to Generic animation type");
                }
            }

            if (count > 0)
            {
                Debug.Log($"Updated {count} FBX files to use Generic animation");
            }
            else
            {
                Debug.Log("No FBX files selected or all already set to Generic");
            }
        }

        [MenuItem("Crestforge/Unit Models/Assign Animator to Selected FBX")]
        public static void AssignAnimatorToSelected()
        {
            // Find the animator controller
            string controllerPath = "Assets/Animations/UnitAnimatorController.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (controller == null)
            {
                Debug.LogWarning("UnitAnimatorController not found. Create it first via Crestforge > Unit Models > Create Unit Animator Controller");
                return;
            }

            Object[] selection = Selection.objects;
            int count = 0;

            foreach (Object obj in selection)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                // Load the FBX as a GameObject
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // Check if it has an Animator
                Animator animator = prefab.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = prefab.GetComponentInChildren<Animator>();
                }

                if (animator != null)
                {
                    // We can't directly modify the FBX, but we can create an override
                    // For now, just log that they need to create a prefab variant
                    Debug.Log($"{obj.name}: Has Animator. To assign controller, create a Prefab Variant and assign the controller there.");
                    count++;
                }
            }

            if (count == 0)
            {
                Debug.Log("No FBX files with Animators selected");
            }
        }
    }
}
#endif
