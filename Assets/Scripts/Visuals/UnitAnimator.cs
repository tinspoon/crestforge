using UnityEngine;

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

        [Tooltip("Name of the death animation clip (optional)")]
        public string deathClip = "CharacterArmature|Death";

        [Tooltip("Name of the hit/hurt animation clip (optional)")]
        public string hitClip = "CharacterArmature|Hit";

        [Header("Animation Speed")]
        [Tooltip("Speed multiplier for attack animation")]
        public float attackAnimSpeed = 1f;

        // Animation parameter names
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Hit = Animator.StringToHash("Hit");
        private static readonly int Death = Animator.StringToHash("Death");

        // State tracking
        private bool isInitialized = false;
        private bool useLegacy = false;

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
            }
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
        /// Play attack animation with dynamic speed based on unit's attack speed
        /// </summary>
        public void PlayAttack(float unitAttackSpeed = 0f)
        {
            if (!isInitialized) return;

            // Calculate dynamic speed if unit attack speed is provided
            float speed = attackAnimSpeed;
            if (unitAttackSpeed > 0)
            {
                float clipDuration = GetClipDuration(attackClip);
                if (clipDuration > 0)
                {
                    float attackInterval = 1f / unitAttackSpeed;
                    // Scale animation to fit within 80% of attack interval (leave time for recovery)
                    speed = clipDuration / (attackInterval * 0.8f);
                    speed = Mathf.Clamp(speed, 0.5f, 4f); // Clamp to reasonable range
                }
            }

            if (useLegacy)
            {
                PlayClipWithSpeed(attackClip, speed);
            }
            else if (animator != null)
            {
                if (HasParameter(Attack))
                {
                    animator.speed = speed;
                    animator.SetTrigger(Attack);
                }
                else
                {
                    PlayClipWithSpeed(attackClip, speed);
                }
            }
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
                animator.Play(clipName, 0, 0f);
            }
        }

        /// <summary>
        /// Play hit/hurt animation
        /// </summary>
        public void PlayHit()
        {
            if (!isInitialized) return;

            if (useLegacy)
            {
                if (!string.IsNullOrEmpty(hitClip))
                    PlayClip(hitClip);
            }
            else if (animator != null)
            {
                if (HasParameter(Hit))
                {
                    animator.SetTrigger(Hit);
                }
                else if (!string.IsNullOrEmpty(hitClip))
                {
                    PlayClip(hitClip);
                }
            }
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
                // Mecanim playback - play state by name
                animator.Play(clipName, 0, 0f);
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
                // Force immediate transition to idle
                animator.Play(idleClip, 0, 0f);
            }
        }
    }
}
