using UnityEngine;

namespace Crestforge.Cosmetics
{
    /// <summary>
    /// Behavior component for placed scenery items
    /// Handles idle animations and combat reactions
    /// </summary>
    public class SceneryBehavior : MonoBehaviour
    {
        public SceneryItemData itemData;
        public PlacedScenery placedData;

        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float idleTimer;
        private bool isReacting;

        public void Initialize(SceneryItemData item, PlacedScenery placed)
        {
            itemData = item;
            placedData = placed;
            originalPosition = transform.position;
            originalRotation = transform.rotation;
        }

        private void Update()
        {
            if (itemData == null) return;

            // Idle animation (gentle bob or sway)
            if (itemData.hasIdleAnimation && !isReacting)
            {
                idleTimer += Time.deltaTime;
                float bob = Mathf.Sin(idleTimer * 2f) * 0.02f;
                transform.position = originalPosition + new Vector3(0, bob, 0);
            }
        }

        /// <summary>
        /// React to a combat event
        /// </summary>
        public void OnCombatEvent(CombatEventType eventType, Vector3 eventPosition)
        {
            if (!itemData.reactsToCombat) return;

            switch (eventType)
            {
                case CombatEventType.UnitKilled:
                    StartCoroutine(CheerReaction());
                    break;
                case CombatEventType.UnitDamaged:
                    // Subtle flinch toward the damage
                    StartCoroutine(FlinchReaction(eventPosition));
                    break;
                case CombatEventType.Victory:
                    StartCoroutine(VictoryReaction());
                    break;
                case CombatEventType.Defeat:
                    StartCoroutine(DefeatReaction());
                    break;
            }
        }

        private System.Collections.IEnumerator CheerReaction()
        {
            isReacting = true;

            // Quick hop
            float duration = 0.3f;
            float elapsed = 0f;
            Vector3 startPos = transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float hop = Mathf.Sin(t * Mathf.PI) * 0.15f;
                transform.position = startPos + new Vector3(0, hop, 0);
                yield return null;
            }

            transform.position = originalPosition;
            isReacting = false;
        }

        private System.Collections.IEnumerator FlinchReaction(Vector3 eventPos)
        {
            isReacting = true;

            // Lean away from event
            Vector3 awayDir = (transform.position - eventPos).normalized;
            awayDir.y = 0;

            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float lean = Mathf.Sin(t * Mathf.PI) * 5f;
                transform.rotation = originalRotation * Quaternion.Euler(awayDir.z * lean, 0, -awayDir.x * lean);
                yield return null;
            }

            transform.rotation = originalRotation;
            isReacting = false;
        }

        private System.Collections.IEnumerator VictoryReaction()
        {
            isReacting = true;

            // Multiple hops
            for (int i = 0; i < 3; i++)
            {
                float duration = 0.25f;
                float elapsed = 0f;
                Vector3 startPos = transform.position;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float hop = Mathf.Sin(t * Mathf.PI) * 0.2f;
                    transform.position = startPos + new Vector3(0, hop, 0);
                    yield return null;
                }

                yield return new WaitForSeconds(0.1f);
            }

            transform.position = originalPosition;
            isReacting = false;
        }

        private System.Collections.IEnumerator DefeatReaction()
        {
            isReacting = true;

            // Slump down
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float droop = Mathf.Sin(t * Mathf.PI * 0.5f) * 10f;
                transform.rotation = originalRotation * Quaternion.Euler(droop, 0, 0);
                yield return null;
            }

            // Slowly recover
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float droop = 10f * (1f - t);
                transform.rotation = originalRotation * Quaternion.Euler(droop, 0, 0);
                yield return null;
            }

            transform.rotation = originalRotation;
            isReacting = false;
        }
    }
}
