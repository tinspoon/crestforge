using UnityEngine;
using System.Collections.Generic;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Handles animation playback for units with custom 3D models.
    /// Supports both Animator (Mecanim) and Legacy Animation components.
    /// </summary>
    public class UnitAnimator : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public Animation legacyAnimation;

        [Header("Animation Names")]
        [Tooltip("Name of the idle animation clip")]
        public string idleClip = "CharacterArmature|Idle";

        [Tooltip("Name of the walk/move animation clip")]
        public string walkClip = "CharacterArmature|Walk";

        [Tooltip("Name of the attack animation clip")]
        public string attackClip = "CharacterArmature|Punch";

        [Tooltip("Name of the ability animation clip (falls back to attack if not found)")]
        public string abilityClip = "";

        [Tooltip("Name of the death animation clip (optional)")]
        public string deathClip = "CharacterArmature|Death";

        [Tooltip("Name of the hit/hurt animation clip (optional)")]
        public string hitClip = "CharacterArmature|Hit";

        [Tooltip("Name of the victory/celebration animation clip (optional)")]
        public string victoryClip = "CharacterArmature|Victory";

        [Header("Animation Speed")]
        [Tooltip("Speed multiplier for attack animation")]
        public float attackAnimSpeed = 1f;
        [Tooltip("Speed multiplier for hit animation (2.0 = twice as fast)")]
        public float hitAnimSpeed = 2f;

        // Animation parameter names
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Hit = Animator.StringToHash("Hit");
        private static readonly int Death = Animator.StringToHash("Death");
        private static readonly int Victory = Animator.StringToHash("Victory");

        // State tracking
        private bool isInitialized = false;
        private bool useLegacy = false;
        private Coroutine returnToIdleCoroutine = null;
        private bool isPlayingHit = false;
        public bool IsPlayingHit => isPlayingHit;
        private bool waitingForAttackEnd = false;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (isInitialized) return;

            // First check for Legacy Animation component
            legacyAnimation = GetComponent<Animation>();
            if (legacyAnimation == null)
            {
                legacyAnimation = GetComponentInChildren<Animation>();
            }

            if (legacyAnimation != null)
            {
                useLegacy = true;
                isInitialized = true;
                AutoDetectLegacyClips();
                return;
            }

            // Fall back to Animator
            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                {
                    animator = GetComponentInChildren<Animator>();
                }
            }

            if (animator != null)
            {
                isInitialized = true;
                AutoDetectAnimatorClips();
            }
        }

        private void Update()
        {
            // Frame-accurate attack completion: transition to idle the moment the clip finishes
            if (waitingForAttackEnd && !useLegacy && animator != null)
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.normalizedTime >= 0.95f)
                {
                    waitingForAttackEnd = false;
                    animator.speed = 1f;
                    ResetToIdle();
                }
            }
        }

        /// <summary>
        /// Auto-detect animation clip names from the Animator controller
        /// </summary>
        private void AutoDetectAnimatorClips()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            // Only auto-detect if using default clip names
            bool usingDefaults = idleClip.Contains("CharacterArmature");
            if (!usingDefaults) return;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

            string foundIdle = null;
            string foundWalk = null;
            string foundAttack = null;
            string foundAbility = null;
            string foundHit = null;
            string foundDeath = null;
            string foundVictory = null;

            foreach (var clip in clips)
            {
                string name = clip.name.ToLower();

                // Find idle (prefer IdleBattle over IdleNormal)
                if (name.Contains("idlebattle") || name.Contains("idle_battle"))
                {
                    foundIdle = clip.name;
                }
                else if (foundIdle == null && name.Contains("idle"))
                {
                    foundIdle = clip.name;
                }

                // Find walk/move
                if (name.Contains("walk") || name.Contains("movefwd") || name.Contains("move_fwd") || name.Contains("run"))
                {
                    foundWalk = clip.name;
                }

                // Find attack (prefer Attack01)
                if (name.Contains("attack01") || name.Contains("attack_01"))
                {
                    foundAttack = clip.name;
                }
                else if (foundAttack == null && name.Contains("attack"))
                {
                    foundAttack = clip.name;
                }

                // Find ability (Attack02 or spell/ability animations)
                if (name.Contains("attack02") || name.Contains("attack_02") || name.Contains("ability") || name.Contains("spell") || name.Contains("cast"))
                {
                    foundAbility = clip.name;
                }

                // Find hit
                if (name.Contains("gethit") || name.Contains("get_hit") || name.Contains("hit") || name.Contains("hurt"))
                {
                    foundHit = clip.name;
                }

                // Find death
                if (name.Contains("death") || name.Contains("die"))
                {
                    foundDeath = clip.name;
                }

                // Find victory/celebration
                if (name.Contains("victory") || name.Contains("celebrate") || name.Contains("win"))
                {
                    foundVictory = clip.name;
                }
            }

            // Apply found clips
            if (!string.IsNullOrEmpty(foundIdle)) idleClip = foundIdle;
            if (!string.IsNullOrEmpty(foundWalk)) walkClip = foundWalk;
            if (!string.IsNullOrEmpty(foundAttack)) attackClip = foundAttack;
            if (!string.IsNullOrEmpty(foundAbility)) abilityClip = foundAbility;
            if (!string.IsNullOrEmpty(foundHit)) hitClip = foundHit;
            if (!string.IsNullOrEmpty(foundDeath)) deathClip = foundDeath;
            if (!string.IsNullOrEmpty(foundVictory)) victoryClip = foundVictory;
        }

        /// <summary>
        /// Auto-detect animation clip names from Legacy Animation
        /// </summary>
        private void AutoDetectLegacyClips()
        {
            if (legacyAnimation == null) return;

            bool usingDefaults = idleClip.Contains("CharacterArmature");
            if (!usingDefaults) return;

            string foundIdle = null;
            string foundWalk = null;
            string foundAttack = null;
            string foundAbility = null;
            string foundHit = null;
            string foundDeath = null;
            string foundVictory = null;

            foreach (AnimationState state in legacyAnimation)
            {
                string name = state.name.ToLower();

                if (name.Contains("idlebattle") || name.Contains("idle_battle"))
                    foundIdle = state.name;
                else if (foundIdle == null && name.Contains("idle"))
                    foundIdle = state.name;

                if (name.Contains("walk") || name.Contains("movefwd") || name.Contains("run"))
                    foundWalk = state.name;

                if (name.Contains("attack01") || name.Contains("attack_01"))
                    foundAttack = state.name;
                else if (foundAttack == null && name.Contains("attack"))
                    foundAttack = state.name;

                // Look for Attack02 or secondary attack for abilities
                if (name.Contains("attack02") || name.Contains("attack_02") || name.Contains("ability") || name.Contains("spell") || name.Contains("cast"))
                    foundAbility = state.name;

                if (name.Contains("gethit") || name.Contains("hit") || name.Contains("hurt"))
                    foundHit = state.name;

                if (name.Contains("death") || name.Contains("die"))
                    foundDeath = state.name;

                if (name.Contains("victory") || name.Contains("celebrate") || name.Contains("win"))
                    foundVictory = state.name;
            }

            if (!string.IsNullOrEmpty(foundIdle)) idleClip = foundIdle;
            if (!string.IsNullOrEmpty(foundWalk)) walkClip = foundWalk;
            if (!string.IsNullOrEmpty(foundAttack)) attackClip = foundAttack;
            if (!string.IsNullOrEmpty(foundAbility)) abilityClip = foundAbility;
            if (!string.IsNullOrEmpty(foundHit)) hitClip = foundHit;
            if (!string.IsNullOrEmpty(foundDeath)) deathClip = foundDeath;
            if (!string.IsNullOrEmpty(foundVictory)) victoryClip = foundVictory;
        }

        /// <summary>
        /// Set up animator with a runtime controller if needed
        /// </summary>
        public void SetupAnimator(RuntimeAnimatorController controller)
        {
            Initialize();
            if (animator != null && controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }
        }

        /// <summary>
        /// Start moving animation
        /// </summary>
        public void StartMoving()
        {
            if (!isInitialized) return;

            if (useLegacy)
            {
                PlayClip(walkClip);
            }
            else if (animator != null)
            {
                PlayClip(walkClip);
            }
        }

        /// <summary>
        /// Stop moving, return to idle
        /// </summary>
        public void StopMoving()
        {
            if (!isInitialized) return;

            // Reset animation speed to normal
            ResetSpeed();

            if (useLegacy)
            {
                PlayClip(idleClip);
            }
            else if (animator != null)
            {
                // Always use PlayClip for state-based animation
                PlayClip(idleClip);
            }
        }

        /// <summary>
        /// Reset animation speed to normal
        /// </summary>
        public void ResetSpeed()
        {
            if (useLegacy && legacyAnimation != null)
            {
                // Reset all clip speeds
                foreach (AnimationState state in legacyAnimation)
                {
                    state.speed = 1f;
                }
            }
            else if (animator != null)
            {
                animator.speed = 1f;
            }
        }

        /// <summary>
        /// Play attack animation with dynamic speed based on unit's attack speed.
        /// Attack has highest priority and will interrupt any other animation.
        /// Plays exactly once and returns to idle.
        /// </summary>
        public void PlayAttack(float unitAttackSpeed = 0f)
        {
            if (!isInitialized) return;

            // Attack takes priority - interrupt any hit animation
            isPlayingHit = false;

            // Cancel any pending return-to-idle
            waitingForAttackEnd = false;
            if (returnToIdleCoroutine != null)
            {
                StopCoroutine(returnToIdleCoroutine);
                returnToIdleCoroutine = null;
            }

            // Calculate dynamic speed if unit attack speed is provided
            float speed = attackAnimSpeed;
            float clipDuration = GetClipDuration(attackClip);
            if (clipDuration <= 0) clipDuration = 0.5f; // Default assumption

            if (unitAttackSpeed > 0)
            {
                float attackInterval = 1f / unitAttackSpeed;
                // Scale animation to match the full attack interval
                // Server hit lands at 60% through, so animation peak should align with that
                speed = clipDuration / attackInterval;
                speed = Mathf.Clamp(speed, 0.5f, 6f); // Clamp to reasonable range
            }

            if (useLegacy)
            {
                float actualDuration = clipDuration / speed;
                PlayClipWithSpeed(attackClip, speed);
                returnToIdleCoroutine = StartCoroutine(ReturnToIdleAfterAttack(actualDuration));
            }
            else if (animator != null)
            {
                PlayClipWithSpeed(attackClip, speed);
                // Use Update() to watch normalizedTime for frame-accurate idle transition
                waitingForAttackEnd = true;
            }
        }

        /// <summary>
        /// Play ability animation (uses abilityClip if available, falls back to attackClip)
        /// Similar to PlayAttack but uses a different animation.
        /// </summary>
        public void PlayAbility(float unitAttackSpeed = 0f)
        {
            // Calculate duration from attack speed and delegate
            float duration = unitAttackSpeed > 0 ? 1f / unitAttackSpeed : 1f;
            PlayAbilityWithDuration(duration);
        }

        /// <summary>
        /// Play ability animation with a fixed duration (does not scale with attack speed)
        /// </summary>
        public void PlayAbilityWithDuration(float targetDuration)
        {
            if (!isInitialized) return;

            // Ability takes priority - interrupt any hit animation
            isPlayingHit = false;

            // Cancel any pending return-to-idle
            waitingForAttackEnd = false;
            if (returnToIdleCoroutine != null)
            {
                StopCoroutine(returnToIdleCoroutine);
                returnToIdleCoroutine = null;
            }

            // Use ability clip if available, otherwise fall back to attack
            string clipToUse = !string.IsNullOrEmpty(abilityClip) ? abilityClip : attackClip;

            // Calculate animation speed to match target duration
            float clipDuration = GetClipDuration(clipToUse);
            if (clipDuration <= 0) clipDuration = 0.5f;

            // Scale animation speed so clip plays in targetDuration
            float speed = clipDuration / targetDuration;
            speed = Mathf.Clamp(speed, 0.5f, 6f);

            if (useLegacy)
            {
                float actualDuration = clipDuration / speed;
                PlayClipWithSpeed(clipToUse, speed);
                returnToIdleCoroutine = StartCoroutine(ReturnToIdleAfterAttack(actualDuration));
            }
            else if (animator != null)
            {
                PlayClipWithSpeed(clipToUse, speed);
                waitingForAttackEnd = true;
            }
        }

        /// <summary>
        /// Coroutine to return to idle after attack animation completes
        /// </summary>
        private System.Collections.IEnumerator ReturnToIdleAfterAttack(float delay)
        {
            yield return new WaitForSeconds(delay);
            ResetToIdle();
            returnToIdleCoroutine = null;
        }

        /// <summary>
        /// Get the duration of an animation clip
        /// </summary>
        private float GetClipDuration(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return 0f;

            if (useLegacy && legacyAnimation != null)
            {
                AnimationClip clip = legacyAnimation.GetClip(clipName);
                if (clip != null)
                    return clip.length;
            }
            else if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip.name == clipName)
                        return clip.length;
                }
            }

            return 1f; // Default assumption
        }

        /// <summary>
        /// Play a clip with a specific speed multiplier
        /// </summary>
        private void PlayClipWithSpeed(string clipName, float speed)
        {
            if (string.IsNullOrEmpty(clipName)) return;

            if (useLegacy && legacyAnimation != null)
            {
                if (legacyAnimation.GetClip(clipName) != null)
                {
                    legacyAnimation[clipName].speed = speed;
                    legacyAnimation.CrossFade(clipName, 0.1f);
                }
            }
            else if (animator != null)
            {
                animator.speed = speed;

                // Try to play the state - state names may differ from clip names
                // First try the full clip name
                if (TryPlayState(clipName)) return;

                // Try extracting state name (e.g., "Attack01" from "Attack01_BowAndArrow")
                string stateName = ExtractStateName(clipName);
                if (!string.IsNullOrEmpty(stateName) && stateName != clipName)
                {
                    if (TryPlayState(stateName)) return;
                }

                // Try common variations
                if (clipName.ToLower().Contains("attack"))
                {
                    if (TryPlayState("Attack01")) return;
                    if (TryPlayState("Attack")) return;
                }
                else if (clipName.ToLower().Contains("idle"))
                {
                    if (TryPlayState("IdleBattle")) return;
                    if (TryPlayState("Idle")) return;
                }
                else if (clipName.ToLower().Contains("walk") || clipName.ToLower().Contains("move"))
                {
                    if (TryPlayState("MoveFWD")) return;
                    if (TryPlayState("Walk")) return;
                }

                Debug.LogWarning($"[UnitAnimator] Could not find state for clip: {clipName}");
            }
        }

        /// <summary>
        /// Try to play an animator state, returns true if successful
        /// </summary>
        private bool TryPlayState(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName)) return false;

            // Check if state exists by trying to get its hash
            int stateHash = Animator.StringToHash(stateName);
            if (animator.HasState(0, stateHash))
            {
                animator.Play(stateHash, 0, 0f);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Extract state name from clip name.
        /// Handles multiple patterns:
        /// - "Attack01_BowAndArrow" -> "Attack01" (action_weapon)
        /// - "RatAssassin_MoveFWD" -> "MoveFWD" (model_action)
        /// </summary>
        private string ExtractStateName(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return null;

            int underscoreIndex = clipName.IndexOf('_');
            if (underscoreIndex > 0)
            {
                string beforeUnderscore = clipName.Substring(0, underscoreIndex);
                string afterUnderscore = clipName.Substring(underscoreIndex + 1);

                // Check if the part after underscore looks like an action name
                string afterLower = afterUnderscore.ToLower();
                if (afterLower.Contains("idle") || afterLower.Contains("move") ||
                    afterLower.Contains("walk") || afterLower.Contains("run") ||
                    afterLower.Contains("attack") || afterLower.Contains("hit") ||
                    afterLower.Contains("die") || afterLower.Contains("death") ||
                    afterLower.Contains("fly"))
                {
                    // Pattern: ModelName_Action (e.g., RatAssassin_MoveFWD -> MoveFWD)
                    return afterUnderscore;
                }
                else
                {
                    // Pattern: Action_Weapon (e.g., Attack01_BowAndArrow -> Attack01)
                    return beforeUnderscore;
                }
            }
            return clipName;
        }

        /// <summary>
        /// Play hit/hurt animation, then return to idle.
        /// Hit animations are low priority and can be interrupted by attacks.
        /// </summary>
        public void PlayHit()
        {
            if (!isInitialized) return;

            // Cancel any pending return-to-idle from a previous hit
            if (returnToIdleCoroutine != null)
            {
                StopCoroutine(returnToIdleCoroutine);
                returnToIdleCoroutine = null;
            }

            isPlayingHit = true;
            float speed = hitAnimSpeed;

            if (useLegacy)
            {
                if (!string.IsNullOrEmpty(hitClip))
                {
                    PlayClipWithSpeed(hitClip, speed);
                    float duration = GetClipDuration(hitClip) / speed;
                    returnToIdleCoroutine = StartCoroutine(ReturnToIdleAfterDelay(duration));
                }
            }
            else if (animator != null)
            {
                if (HasParameter(Hit))
                {
                    animator.speed = speed;
                    // Reset trigger first to prevent queuing
                    animator.ResetTrigger(Hit);
                    animator.SetTrigger(Hit);
                    // Reset speed after a short delay
                    returnToIdleCoroutine = StartCoroutine(ResetSpeedAfterDelay(0.5f / speed));
                }
                else if (!string.IsNullOrEmpty(hitClip))
                {
                    PlayClipWithSpeed(hitClip, speed);
                    float duration = GetClipDuration(hitClip) / speed;
                    returnToIdleCoroutine = StartCoroutine(ReturnToIdleAfterDelay(duration));
                }
            }
        }

        /// <summary>
        /// Reset animator speed after a delay
        /// </summary>
        private System.Collections.IEnumerator ResetSpeedAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (animator != null)
            {
                animator.speed = 1f;
            }
            isPlayingHit = false;
            returnToIdleCoroutine = null;
        }

        /// <summary>
        /// Coroutine to return to idle after a delay
        /// </summary>
        private System.Collections.IEnumerator ReturnToIdleAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            isPlayingHit = false;
            // Play idle directly without resetting speed
            PlayClip(idleClip);
            returnToIdleCoroutine = null;
        }

        /// <summary>
        /// Play death animation
        /// </summary>
        public void PlayDeath()
        {
            if (!isInitialized) return;

            if (useLegacy)
            {
                if (!string.IsNullOrEmpty(deathClip))
                    PlayClip(deathClip);
            }
            else if (animator != null)
            {
                if (HasParameter(Death))
                {
                    animator.SetTrigger(Death);
                }
                else if (!string.IsNullOrEmpty(deathClip))
                {
                    PlayClip(deathClip);
                }
            }
        }

        /// <summary>
        /// Play victory/celebration animation (looping)
        /// </summary>
        public void PlayVictory()
        {
            if (!isInitialized) return;

            // Cancel any pending return-to-idle
            if (returnToIdleCoroutine != null)
            {
                StopCoroutine(returnToIdleCoroutine);
                returnToIdleCoroutine = null;
            }

            // Reset animation speed to normal (combat may have sped it up)
            ResetSpeed();

            if (useLegacy)
            {
                if (!string.IsNullOrEmpty(victoryClip) && legacyAnimation.GetClip(victoryClip) != null)
                {
                    legacyAnimation.CrossFade(victoryClip, 0.2f);
                }
                else
                {
                    PlayClip(idleClip);
                }
            }
            else if (animator != null)
            {
                // Try multiple approaches to play victory animation
                string stateName = ExtractStateName(victoryClip);

                // First try: Victory trigger parameter
                if (HasParameter(Victory))
                {
                    animator.SetTrigger(Victory);
                    return;
                }

                // Build list of state names to try - include variations with unit name prefix
                List<string> stateNamesToTry = new List<string>
                {
                    "Victory",                      // Common name added by our tool
                    victoryClip,                    // Full clip name: "Cactus_Victory"
                    stateName,                      // Extracted: "Victory"
                };

                // Add variations based on the detected victory clip name
                if (!string.IsNullOrEmpty(victoryClip))
                {
                    // Try without any prefix extraction
                    stateNamesToTry.Add(victoryClip.Replace("_", ""));

                    // If clip has underscore, try both parts
                    if (victoryClip.Contains("_"))
                    {
                        string[] parts = victoryClip.Split('_');
                        foreach (string part in parts)
                        {
                            if (part.ToLower().Contains("victory"))
                            {
                                stateNamesToTry.Add(part);
                            }
                        }
                    }
                }

                foreach (string tryName in stateNamesToTry)
                {
                    if (string.IsNullOrEmpty(tryName)) continue;

                    if (TryPlayState(tryName))
                    {
                        return;
                    }
                }

                // Try CrossFade with each name (might work even if HasState returns false)
                foreach (string tryName in stateNamesToTry)
                {
                    if (string.IsNullOrEmpty(tryName)) continue;

                    try
                    {
                        animator.CrossFade(tryName, 0.2f, 0);
                        return;
                    }
                    catch (System.Exception)
                    {
                        // Silently continue to next
                    }
                }

                // Fallback to idle
                PlayClip(idleClip);
            }
        }

        /// <summary>
        /// Play a specific clip by name
        /// </summary>
        public void PlayClip(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return;

            if (useLegacy && legacyAnimation != null)
            {
                // Legacy Animation playback
                if (legacyAnimation.GetClip(clipName) != null)
                {
                    legacyAnimation.CrossFade(clipName, 0.1f);
                }
                else
                {
                    Debug.LogWarning($"[UnitAnimator] Legacy clip not found: {clipName}");
                }
            }
            else if (animator != null)
            {
                // Mecanim playback - check if state exists before playing
                if (!TryPlayState(clipName))
                {
                    // Try extracting state name from clip name (e.g., "Idle_BattleIdle" -> "Idle")
                    string stateName = ExtractStateName(clipName);
                    if (!string.IsNullOrEmpty(stateName))
                    {
                        TryPlayState(stateName);
                    }
                }
            }
        }

        /// <summary>
        /// Check if animator has a parameter
        /// </summary>
        private bool HasParameter(int paramHash)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;

            foreach (var param in animator.parameters)
            {
                if (param.nameHash == paramHash)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Set animation speed multiplier
        /// </summary>
        public void SetSpeed(float speed)
        {
            if (animator != null)
            {
                animator.speed = speed;
            }
        }

        /// <summary>
        /// Force reset to idle animation - used when combat ends
        /// </summary>
        public void ResetToIdle()
        {
            if (!isInitialized) return;

            isPlayingHit = false;
            waitingForAttackEnd = false;
            ResetSpeed();

            // Force play idle animation immediately
            if (useLegacy && legacyAnimation != null)
            {
                if (legacyAnimation.GetClip(idleClip) != null)
                {
                    legacyAnimation.Stop();
                    legacyAnimation.Play(idleClip);
                }
            }
            else if (animator != null)
            {
                // Force immediate transition to idle - try multiple fallbacks
                if (!TryPlayState(idleClip))
                {
                    if (!TryPlayState("IdleBattle"))
                    {
                        TryPlayState("Idle");
                    }
                }
            }
        }
    }
}
