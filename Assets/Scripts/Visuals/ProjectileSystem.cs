using UnityEngine;
using System.Collections;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Combat;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Handles projectile visuals for ranged attacks and abilities
    /// </summary>
    public class ProjectileSystem : MonoBehaviour
    {
        public static ProjectileSystem Instance { get; private set; }

        [Header("Projectile Settings")]
        public float projectileSpeed = 8f;
        public float arcHeight = 0.5f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// Fire a basic projectile from attacker to target
        /// </summary>
        public void FireProjectile(Vector3 from, Vector3 to, ProjectileType type, System.Action onHit = null)
        {
            // Play fire sound
            AudioManager.Instance?.PlayProjectileFire(type);
            
            StartCoroutine(ProjectileRoutine(from, to, type, onHit));
        }

        /// <summary>
        /// Fire projectile between units
        /// </summary>
        public void FireProjectile(UnitVisual3D attacker, UnitVisual3D target, ProjectileType type, System.Action onHit = null)
        {
            if (attacker == null || target == null) return;
            
            Vector3 from = attacker.transform.position + Vector3.up * 0.5f;
            Vector3 to = target.transform.position + Vector3.up * 0.3f;
            
            FireProjectile(from, to, type, onHit);
        }

        private IEnumerator ProjectileRoutine(Vector3 from, Vector3 to, ProjectileType type, System.Action onHit)
        {
            // Create projectile visual
            GameObject projectile = CreateProjectileVisual(type);
            projectile.transform.position = from;

            // Calculate travel
            float distance = Vector3.Distance(from, to);
            float duration = distance / projectileSpeed;
            float elapsed = 0f;

            // Create trail effect
            TrailRenderer trail = projectile.GetComponent<TrailRenderer>();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Lerp position with arc
                Vector3 pos = Vector3.Lerp(from, to, t);
                
                // Add arc (parabola)
                float arc = arcHeight * 4f * t * (1f - t);
                pos.y += arc;

                projectile.transform.position = pos;

                // Face direction of travel
                Vector3 direction = (to - from).normalized;
                if (direction.magnitude > 0.01f)
                {
                    projectile.transform.rotation = Quaternion.LookRotation(direction);
                }

                yield return null;
            }

            // Hit effect
            SpawnHitEffect(to, type);
            
            // Callback
            onHit?.Invoke();

            // Destroy projectile
            Destroy(projectile);
        }

        private GameObject CreateProjectileVisual(ProjectileType type)
        {
            GameObject projectile = new GameObject($"Projectile_{type}");

            switch (type)
            {
                case ProjectileType.Arrow:
                    CreateArrowVisual(projectile);
                    break;
                case ProjectileType.Fireball:
                    CreateFireballVisual(projectile);
                    break;
                case ProjectileType.IceShard:
                    CreateIceShardVisual(projectile);
                    break;
                case ProjectileType.Lightning:
                    CreateLightningVisual(projectile);
                    break;
                case ProjectileType.Holy:
                    CreateHolyVisual(projectile);
                    break;
                case ProjectileType.Shadow:
                    CreateShadowVisual(projectile);
                    break;
                default:
                    CreateDefaultVisual(projectile);
                    break;
            }

            return projectile;
        }

        private void CreateArrowVisual(GameObject projectile)
        {
            // Arrow shaft
            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.transform.SetParent(projectile.transform);
            shaft.transform.localPosition = Vector3.zero;
            shaft.transform.localRotation = Quaternion.Euler(90, 0, 0);
            shaft.transform.localScale = new Vector3(0.02f, 0.15f, 0.02f);
            Destroy(shaft.GetComponent<Collider>());
            shaft.GetComponent<Renderer>().material.color = new Color(0.4f, 0.3f, 0.2f);

            // Arrow head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.transform.SetParent(projectile.transform);
            head.transform.localPosition = new Vector3(0, 0, 0.12f);
            head.transform.localRotation = Quaternion.Euler(0, 0, 45);
            head.transform.localScale = new Vector3(0.04f, 0.04f, 0.06f);
            Destroy(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().material.color = new Color(0.5f, 0.5f, 0.5f);

            AddTrail(projectile, new Color(0.8f, 0.8f, 0.6f, 0.5f), 0.02f);
        }

        private void CreateFireballVisual(GameObject projectile)
        {
            // Core
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.transform.SetParent(projectile.transform);
            core.transform.localPosition = Vector3.zero;
            core.transform.localScale = Vector3.one * 0.15f;
            Destroy(core.GetComponent<Collider>());
            
            Material mat = core.GetComponent<Renderer>().material;
            mat.color = new Color(1f, 0.5f, 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0f) * 2f);

            // Outer glow
            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.transform.SetParent(projectile.transform);
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localScale = Vector3.one * 0.22f;
            Destroy(glow.GetComponent<Collider>());
            
            Material glowMat = glow.GetComponent<Renderer>().material;
            glowMat.color = new Color(1f, 0.8f, 0f, 0.5f);

            AddTrail(projectile, new Color(1f, 0.4f, 0f, 0.8f), 0.1f);
            
            // Add particle effect
            AddFireParticles(projectile);
        }

        private void CreateIceShardVisual(GameObject projectile)
        {
            // Shard
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.transform.SetParent(projectile.transform);
            shard.transform.localPosition = Vector3.zero;
            shard.transform.localRotation = Quaternion.Euler(0, 0, 45);
            shard.transform.localScale = new Vector3(0.06f, 0.06f, 0.2f);
            Destroy(shard.GetComponent<Collider>());
            
            Material mat = shard.GetComponent<Renderer>().material;
            mat.color = new Color(0.7f, 0.9f, 1f, 0.9f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.5f, 0.8f, 1f) * 1.5f);

            AddTrail(projectile, new Color(0.6f, 0.9f, 1f, 0.6f), 0.05f);
        }

        private void CreateLightningVisual(GameObject projectile)
        {
            // Energy ball
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.SetParent(projectile.transform);
            ball.transform.localPosition = Vector3.zero;
            ball.transform.localScale = Vector3.one * 0.1f;
            Destroy(ball.GetComponent<Collider>());
            
            Material mat = ball.GetComponent<Renderer>().material;
            mat.color = new Color(0.8f, 0.8f, 1f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.6f, 0.6f, 1f) * 3f);

            AddTrail(projectile, new Color(0.7f, 0.7f, 1f, 0.8f), 0.08f);
        }

        private void CreateHolyVisual(GameObject projectile)
        {
            // Holy orb
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.transform.SetParent(projectile.transform);
            orb.transform.localPosition = Vector3.zero;
            orb.transform.localScale = Vector3.one * 0.12f;
            Destroy(orb.GetComponent<Collider>());
            
            Material mat = orb.GetComponent<Renderer>().material;
            mat.color = new Color(1f, 1f, 0.8f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.6f) * 2f);

            AddTrail(projectile, new Color(1f, 1f, 0.7f, 0.6f), 0.06f);
        }

        private void CreateShadowVisual(GameObject projectile)
        {
            // Shadow orb
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.transform.SetParent(projectile.transform);
            orb.transform.localPosition = Vector3.zero;
            orb.transform.localScale = Vector3.one * 0.12f;
            Destroy(orb.GetComponent<Collider>());
            
            Material mat = orb.GetComponent<Renderer>().material;
            mat.color = new Color(0.2f, 0.1f, 0.3f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.4f, 0.1f, 0.5f) * 1.5f);

            AddTrail(projectile, new Color(0.3f, 0.1f, 0.4f, 0.7f), 0.08f);
        }

        private void CreateDefaultVisual(GameObject projectile)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(projectile.transform);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * 0.08f;
            Destroy(sphere.GetComponent<Collider>());
            sphere.GetComponent<Renderer>().material.color = Color.white;

            AddTrail(projectile, new Color(1f, 1f, 1f, 0.5f), 0.04f);
        }

        private void AddTrail(GameObject projectile, Color color, float width)
        {
            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.startWidth = width;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        private void AddFireParticles(GameObject parent)
        {
            GameObject particleObj = new GameObject("FireParticles");
            particleObj.transform.SetParent(parent.transform);
            particleObj.transform.localPosition = Vector3.zero;

            ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.3f;
            main.startSpeed = 0.5f;
            main.startSize = 0.05f;
            main.startColor = new Color(1f, 0.5f, 0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 30f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.6f, 0f), 0f), new GradientColorKey(new Color(1f, 0.2f, 0f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        }

        private void SpawnHitEffect(Vector3 position, ProjectileType type)
        {
            // Play hit sound
            AudioManager.Instance?.PlayProjectileHit(type);
            
            switch (type)
            {
                case ProjectileType.Fireball:
                    VFXSystem.Instance?.SpawnEffect(VFXType.FireExplosion, position);
                    break;
                case ProjectileType.IceShard:
                    VFXSystem.Instance?.SpawnEffect(VFXType.IceShatter, position);
                    break;
                case ProjectileType.Lightning:
                    VFXSystem.Instance?.SpawnEffect(VFXType.LightningStrike, position);
                    break;
                default:
                    VFXSystem.Instance?.SpawnEffect(VFXType.Impact, position);
                    break;
            }
        }
    }

    public enum ProjectileType
    {
        Arrow,
        Fireball,
        IceShard,
        Lightning,
        Holy,
        Shadow,
        Default
    }
}