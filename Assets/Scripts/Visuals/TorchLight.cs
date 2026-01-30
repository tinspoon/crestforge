using UnityEngine;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Adds a flickering torch light effect with optional flame particles.
    /// Attach to any torch or light source object in the scene.
    /// </summary>
    public class TorchLight : MonoBehaviour
    {
        [Header("Light Settings")]
        [Tooltip("Light color (default: warm orange)")]
        public Color lightColor = new Color(1f, 0.6f, 0.2f);

        [Tooltip("Base light intensity")]
        [Range(0.5f, 5f)]
        public float intensity = 1.2f;

        [Tooltip("Light range in units")]
        [Range(1f, 20f)]
        public float range = 5f;

        [Tooltip("Enable real-time shadows (performance cost)")]
        public bool castShadows = false;

        [Header("Flicker Settings")]
        [Tooltip("Enable light flickering")]
        public bool enableFlicker = true;

        [Tooltip("How much the intensity varies (0-1)")]
        [Range(0f, 0.5f)]
        public float flickerAmount = 0.15f;

        [Tooltip("How fast the light flickers")]
        [Range(1f, 20f)]
        public float flickerSpeed = 8f;

        [Header("Flame Particles")]
        [Tooltip("Create a simple flame particle effect")]
        public bool createFlameParticles = true;

        [Tooltip("Flame particle scale")]
        [Range(0.1f, 2f)]
        public float flameScale = 0.5f;

        // Components
        private Light pointLight;
        private ParticleSystem flameParticles;

        // Flicker state
        private float baseIntensity;
        private float flickerOffset;
        private float noiseTime;

        private void Awake()
        {
            SetupLight();

            if (createFlameParticles)
            {
                SetupFlameParticles();
            }
        }

        private void SetupLight()
        {
            // Use existing light or create new one
            pointLight = GetComponent<Light>();
            if (pointLight == null)
            {
                pointLight = gameObject.AddComponent<Light>();
            }

            pointLight.type = LightType.Point;
            pointLight.color = lightColor;
            pointLight.intensity = intensity;
            pointLight.range = range;
            pointLight.shadows = castShadows ? LightShadows.Soft : LightShadows.None;

            baseIntensity = intensity;
            flickerOffset = Random.Range(0f, 100f); // Randomize so multiple torches don't sync
        }

        private void SetupFlameParticles()
        {
            GameObject particleObj = new GameObject("FlameParticles");
            particleObj.transform.SetParent(transform);
            particleObj.transform.localPosition = Vector3.zero;

            flameParticles = particleObj.AddComponent<ParticleSystem>();

            // Stop to configure
            flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Main module
            var main = flameParticles.main;
            main.startLifetime = 0.5f;
            main.startSpeed = 0.8f;
            main.startSize = 0.15f * flameScale;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.8f, 0.2f, 1f),
                new Color(1f, 0.4f, 0.1f, 1f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 50;

            // Emission
            var emission = flameParticles.emission;
            emission.rateOverTime = 30f;

            // Shape - small cone pointing up
            var shape = flameParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.05f * flameScale;

            // Size over lifetime - shrink as they rise
            var sizeOverLifetime = flameParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(0.5f, 0.6f);
            sizeCurve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Color over lifetime - fade to transparent
            var colorOverLifetime = flameParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.5f),
                    new GradientColorKey(new Color(0.8f, 0.2f, 0.05f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // Velocity over lifetime - slight upward drift
            var velocityOverLifetime = flameParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.y = 0.5f;

            // Noise for flickering motion
            var noise = flameParticles.noise;
            noise.enabled = true;
            noise.strength = 0.2f;
            noise.frequency = 2f;
            noise.scrollSpeed = 1f;

            // Renderer settings
            var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.SetColor("_Color", Color.white);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Start the system
            flameParticles.Play();
        }

        private void Update()
        {
            if (enableFlicker && pointLight != null)
            {
                UpdateFlicker();
            }
        }

        private void UpdateFlicker()
        {
            noiseTime += Time.deltaTime * flickerSpeed;

            // Use multiple noise frequencies for natural-looking flicker
            float noise1 = Mathf.PerlinNoise(noiseTime + flickerOffset, 0f);
            float noise2 = Mathf.PerlinNoise(noiseTime * 2.3f + flickerOffset, 1f);
            float noise3 = Mathf.PerlinNoise(noiseTime * 5.7f + flickerOffset, 2f);

            // Combine noises with different weights
            float combinedNoise = noise1 * 0.5f + noise2 * 0.3f + noise3 * 0.2f;

            // Map to intensity range
            float flickerValue = (combinedNoise - 0.5f) * 2f * flickerAmount;
            pointLight.intensity = baseIntensity + (baseIntensity * flickerValue);

            // Subtle range variation
            pointLight.range = range + (range * flickerValue * 0.1f);
        }

        /// <summary>
        /// Update light settings at runtime
        /// </summary>
        public void SetIntensity(float newIntensity)
        {
            intensity = newIntensity;
            baseIntensity = newIntensity;
            if (pointLight != null)
            {
                pointLight.intensity = newIntensity;
            }
        }

        /// <summary>
        /// Turn the torch on or off
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (pointLight != null)
            {
                pointLight.enabled = enabled;
            }
            if (flameParticles != null)
            {
                if (enabled)
                    flameParticles.Play();
                else
                    flameParticles.Stop();
            }
        }

        private void OnValidate()
        {
            // Update light in editor when values change
            if (pointLight != null)
            {
                pointLight.color = lightColor;
                pointLight.intensity = intensity;
                pointLight.range = range;
                pointLight.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
                baseIntensity = intensity;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Show light range in editor
            Gizmos.color = new Color(lightColor.r, lightColor.g, lightColor.b, 0.3f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
