using UnityEngine;

namespace Crestforge.Cosmetics
{
    /// <summary>
    /// Animates a flag banner with a waving motion
    /// </summary>
    public class FlagAnimation : MonoBehaviour
    {
        [Header("References")]
        public Transform banner;

        [Header("Wave Settings")]
        public float waveSpeed = 2f;
        public float waveAmount = 5f;
        public float windStrength = 1f;

        private Vector3 originalBannerRotation;
        private float timeOffset;

        private void Start()
        {
            if (banner != null)
            {
                originalBannerRotation = banner.localEulerAngles;
            }
            timeOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (banner == null) return;

            // Create a gentle waving motion
            float time = Time.time * waveSpeed + timeOffset;

            // Primary wave
            float waveX = Mathf.Sin(time) * waveAmount * windStrength;
            // Secondary wave (faster, smaller)
            float waveZ = Mathf.Sin(time * 1.7f) * (waveAmount * 0.3f) * windStrength;

            banner.localRotation = Quaternion.Euler(
                originalBannerRotation.x + waveX,
                originalBannerRotation.y,
                originalBannerRotation.z + waveZ
            );
        }

        /// <summary>
        /// Increase wind temporarily (for dramatic moments)
        /// </summary>
        public void GustOfWind(float intensity = 2f, float duration = 1f)
        {
            StartCoroutine(WindGust(intensity, duration));
        }

        private System.Collections.IEnumerator WindGust(float intensity, float duration)
        {
            float originalStrength = windStrength;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Quick ramp up, slow decay
                if (t < 0.2f)
                {
                    windStrength = Mathf.Lerp(originalStrength, intensity, t / 0.2f);
                }
                else
                {
                    windStrength = Mathf.Lerp(intensity, originalStrength, (t - 0.2f) / 0.8f);
                }

                yield return null;
            }

            windStrength = originalStrength;
        }
    }
}
