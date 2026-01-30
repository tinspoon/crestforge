using UnityEngine;
using System.Collections;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Handles visual effects like explosions, impacts, and ability effects
    /// </summary>
    public class VFXSystem : MonoBehaviour
    {
        public static VFXSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// Spawn a visual effect at position
        /// </summary>
        public void SpawnEffect(VFXType type, Vector3 position, float scale = 1f)
        {
            switch (type)
            {
                case VFXType.Impact:
                    CreateImpactEffect(position, scale);
                    break;
                case VFXType.FireExplosion:
                    CreateFireExplosion(position, scale);
                    break;
                case VFXType.IceShatter:
                    CreateIceShatter(position, scale);
                    break;
                case VFXType.LightningStrike:
                    CreateLightningStrike(position, scale);
                    break;
                case VFXType.Heal:
                    CreateHealEffect(position, scale);
                    break;
                case VFXType.Shield:
                    CreateShieldEffect(position, scale);
                    break;
                case VFXType.LevelUp:
                    CreateLevelUpEffect(position, scale);
                    break;
                case VFXType.Death:
                    CreateDeathEffect(position, scale);
                    break;
                case VFXType.Slash:
                    CreateSlashEffect(position, scale);
                    break;
                case VFXType.Buff:
                    CreateBuffEffect(position, scale);
                    break;
                case VFXType.Debuff:
                    CreateDebuffEffect(position, scale);
                    break;
                case VFXType.MeleeHit:
                    CreateMeleeHitEffect(position, scale);
                    break;
                case VFXType.ArrowHit:
                    CreateArrowHitEffect(position, scale);
                    break;
                case VFXType.MagicBurst:
                    CreateMagicBurstEffect(position, scale);
                    break;
                case VFXType.ManaReady:
                    CreateManaReadyEffect(position, scale);
                    break;
                case VFXType.SoulWisp:
                    CreateSoulWispEffect(position, scale);
                    break;
                case VFXType.Merge:
                    CreateMergeEffect(position, scale);
                    break;
            }
        }

        /// <summary>
        /// Spawn effect attached to a unit
        /// </summary>
        public void SpawnEffectOnUnit(VFXType type, UnitVisual3D unit, float duration = 1f)
        {
            if (unit == null) return;
            
            GameObject effect = CreateEffectObject(type, unit.transform.position);
            if (effect != null)
            {
                effect.transform.SetParent(unit.transform);
                effect.transform.localPosition = Vector3.up * 0.5f;
                Destroy(effect, duration);
            }
        }

        private GameObject CreateEffectObject(VFXType type, Vector3 position)
        {
            GameObject obj = new GameObject($"VFX_{type}");
            obj.transform.position = position;
            return obj;
        }

        // ============ EFFECT IMPLEMENTATIONS ============

        private void CreateImpactEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.Impact, position);

            ParticleSystem ps = CreateParticleSystem(obj);
            var main = ps.main;
            main.startLifetime = 0.3f;
            main.startSpeed = 2f * scale;
            main.startSize = 0.1f * scale;
            main.startColor = Color.white;
            main.duration = 0.1f;
            main.loop = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f * scale;

            SetupParticleRenderer(obj);
            FinalizeAndPlay(ps);
            Destroy(obj, 1f);
        }

        private void CreateFireExplosion(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.FireExplosion, position);

            // Main explosion particles
            ParticleSystem ps = CreateParticleSystem(obj);
            var main = ps.main;
            main.startLifetime = 0.5f;
            main.startSpeed = 3f * scale;
            main.startSize = 0.15f * scale;
            main.startColor = new Color(1f, 0.5f, 0f);
            main.duration = 0.2f;
            main.loop = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f * scale;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f), 
                    new GradientColorKey(new Color(1f, 0.3f, 0f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0.1f, 0f), 1f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1f, 0f), 
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f) 
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 2f));

            SetupParticleRenderer(obj);
            FinalizeAndPlay(ps);

            // Add flash
            StartCoroutine(FlashEffect(position, new Color(1f, 0.6f, 0f), 0.1f, scale));

            Destroy(obj, 1.5f);
        }

        private void CreateIceShatter(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.IceShatter, position);

            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.6f;
            main.startSpeed = 2f * scale;
            main.startSize = 0.08f * scale;
            main.startColor = new Color(0.7f, 0.9f, 1f);
            main.duration = 0.1f;
            main.loop = false;
            main.gravityModifier = 0.5f;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f * scale;

            SetupParticleRenderer(obj, new Color(0.6f, 0.9f, 1f));

            Destroy(obj, 1.5f);
        }

        private void CreateLightningStrike(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.LightningStrike, position);

            // Electric sparks
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.2f;
            main.startSpeed = 4f * scale;
            main.startSize = 0.05f * scale;
            main.startColor = new Color(0.8f, 0.8f, 1f);
            main.duration = 0.15f;
            main.loop = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f * scale;

            SetupParticleRenderer(obj, new Color(0.7f, 0.7f, 1f));

            // Add flash
            StartCoroutine(FlashEffect(position, new Color(0.8f, 0.8f, 1f), 0.05f, scale * 1.5f));

            Destroy(obj, 1f);
        }

        private void CreateHealEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.Heal, position);

            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1f;
            main.startSpeed = 0.5f * scale;
            main.startSize = 0.08f * scale;
            main.startColor = new Color(0.3f, 1f, 0.3f);
            main.duration = 0.5f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 20;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f * scale;
            shape.rotation = new Vector3(-90, 0, 0);

            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.y = 1f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.5f, 1f, 0.5f), 0f), new GradientColorKey(new Color(0.8f, 1f, 0.8f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            SetupParticleRenderer(obj, new Color(0.5f, 1f, 0.5f));

            Destroy(obj, 2f);
        }

        private void CreateShieldEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.Shield, position);

            // Shield bubble
            GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.transform.SetParent(obj.transform);
            bubble.transform.localPosition = Vector3.zero;
            bubble.transform.localScale = Vector3.one * 0.8f * scale;
            Destroy(bubble.GetComponent<Collider>());

            Material mat = bubble.GetComponent<Renderer>().material;
            mat.color = new Color(0.3f, 0.5f, 1f, 0.3f);
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = 3000;

            StartCoroutine(PulseAndFade(bubble, 0.5f));

            Destroy(obj, 0.6f);
        }

        private void CreateLevelUpEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.LevelUp, position);

            // Rising stars
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.5f;
            main.startSpeed = 1f * scale;
            main.startSize = 0.1f * scale;
            main.startColor = new Color(1f, 0.9f, 0.3f);
            main.duration = 0.5f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 30;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f * scale;
            shape.rotation = new Vector3(-90, 0, 0);

            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.y = 2f;

            SetupParticleRenderer(obj, new Color(1f, 0.9f, 0.3f));

            Destroy(obj, 2.5f);
        }

        private void CreateDeathEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.Death, position);

            // Soul particles rising
            ParticleSystem ps = CreateParticleSystem(obj);
            var main = ps.main;
            main.startLifetime = 1.5f;
            main.startSpeed = 0.5f * scale;
            main.startSize = 0.15f * scale;
            main.startColor = new Color(0.5f, 0.5f, 0.6f, 0.8f);
            main.duration = 0.3f;
            main.loop = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f * scale;

            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.y = 1f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.6f, 0.6f, 0.7f), 0f), new GradientColorKey(new Color(0.3f, 0.3f, 0.4f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            SetupParticleRenderer(obj);
            FinalizeAndPlay(ps);

            Destroy(obj, 2f);
        }

        private void CreateSlashEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.Slash, position);

            // Quick slash particles - stop auto-play to configure properly
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = 0.15f;
            main.startSpeed = 5f * scale;
            main.startSize = 0.03f * scale;
            main.startColor = Color.white;
            main.duration = 0.05f;
            main.loop = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.01f;

            SetupParticleRenderer(obj);

            ps.Play();
            Destroy(obj, 0.5f);
        }

        private void CreateBuffEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.Buff, position);

            // Upward sparkles
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 1f * scale;
            main.startSize = 0.06f * scale;
            main.startColor = new Color(0.3f, 0.8f, 1f);
            main.duration = 0.4f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 25;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f * scale;
            shape.rotation = new Vector3(-90, 0, 0);

            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.y = 1.5f;

            SetupParticleRenderer(obj, new Color(0.3f, 0.8f, 1f));

            Destroy(obj, 1.5f);
        }

        private void CreateDebuffEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.Debuff, position);

            // Downward dark particles
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.6f;
            main.startSpeed = 0.5f * scale;
            main.startSize = 0.08f * scale;
            main.startColor = new Color(0.5f, 0.2f, 0.5f);
            main.duration = 0.4f;
            main.loop = false;
            main.gravityModifier = 0.3f;

            var emission = ps.emission;
            emission.rateOverTime = 20;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f * scale;

            SetupParticleRenderer(obj, new Color(0.5f, 0.2f, 0.5f));

            Destroy(obj, 1.5f);
        }

        // ============ MEDIEVAL COMBAT EFFECTS ============

        private void CreateMeleeHitEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.MeleeHit, position);

            // Metal sparks for sword/weapon impact
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.25f;
            main.startSpeed = 3f * scale;
            main.startSize = 0.04f * scale;
            main.startColor = new Color(1f, 0.9f, 0.6f); // Golden sparks
            main.duration = 0.1f;
            main.loop = false;
            main.gravityModifier = 1f;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.05f * scale;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                    new GradientColorKey(new Color(1f, 0.6f, 0.2f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.2f, 0.1f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            SetupParticleRenderer(obj, new Color(1f, 0.9f, 0.6f));
            Destroy(obj, 0.8f);
        }

        private void CreateArrowHitEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.ArrowHit, position);

            // Wood/feather debris
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 2f * scale;
            main.startSize = 0.03f * scale;
            main.startColor = new Color(0.6f, 0.45f, 0.3f); // Wood brown
            main.duration = 0.1f;
            main.loop = false;
            main.gravityModifier = 0.8f;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 8) });
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.02f * scale;

            SetupParticleRenderer(obj, new Color(0.6f, 0.45f, 0.3f));

            // Small impact
            StartCoroutine(FlashEffect(position, new Color(0.9f, 0.8f, 0.6f), 0.05f, scale * 0.3f));

            Destroy(obj, 0.8f);
        }

        private void CreateMagicBurstEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.MagicBurst, position);

            // Arcane energy burst
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.6f;
            main.startSpeed = 2f * scale;
            main.startSize = 0.12f * scale;
            main.startColor = new Color(0.5f, 0.4f, 1f); // Arcane purple-blue
            main.duration = 0.2f;
            main.loop = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f * scale;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.7f, 0.6f, 1f), 0f),
                    new GradientColorKey(new Color(0.4f, 0.3f, 0.9f), 0.5f),
                    new GradientColorKey(new Color(0.2f, 0.1f, 0.5f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.7f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));

            SetupParticleRenderer(obj, new Color(0.5f, 0.4f, 1f));

            // Arcane flash
            StartCoroutine(FlashEffect(position, new Color(0.6f, 0.5f, 1f), 0.15f, scale * 0.8f));

            Destroy(obj, 1.2f);
        }

        private void CreateManaReadyEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.ManaReady, position);

            // Swirling mana particles
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 0.8f * scale;
            main.startSize = 0.06f * scale;
            main.startColor = new Color(0.3f, 0.6f, 1f); // Blue mana
            main.duration = 0.5f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 30;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f * scale;
            shape.rotation = new Vector3(-90, 0, 0);

            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.y = 1.5f;
            velocityOverLifetime.orbitalY = 2f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.4f, 0.7f, 1f), 0f),
                    new GradientColorKey(new Color(0.6f, 0.8f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.8f, 0.9f, 1f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            SetupParticleRenderer(obj, new Color(0.4f, 0.7f, 1f));

            Destroy(obj, 1.5f);
        }

        private void CreateSoulWispEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.SoulWisp, position);

            // Ghostly soul wisps rising (for undead/death themes)
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 2f;
            main.startSpeed = 0.3f * scale;
            main.startSize = 0.2f * scale;
            main.startColor = new Color(0.4f, 0.8f, 0.4f, 0.6f); // Ghostly green
            main.duration = 0.5f;
            main.loop = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 5) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f * scale;

            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.y = 0.8f;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.5f, 0.9f, 0.5f), 0f),
                    new GradientColorKey(new Color(0.3f, 0.7f, 0.4f), 0.5f),
                    new GradientColorKey(new Color(0.2f, 0.5f, 0.3f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.7f, 0f),
                    new GradientAlphaKey(0.5f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.5f));

            SetupParticleRenderer(obj, new Color(0.4f, 0.8f, 0.4f));

            Destroy(obj, 2.5f);
        }

        private void CreateMergeEffect(Vector3 position, float scale)
        {
            GameObject obj = CreateEffectObject(VFXType.Merge, position);

            // Star upgrade particles
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1f;
            main.startSpeed = 1.5f * scale;
            main.startSize = 0.1f * scale;
            main.startColor = new Color(1f, 0.85f, 0.3f); // Gold
            main.duration = 0.3f;
            main.loop = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f * scale;

            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.radial = 1f;
            velocityOverLifetime.y = 1f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 1f, 0.8f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0.3f),
                    new GradientColorKey(new Color(0.9f, 0.6f, 0.1f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            SetupParticleRenderer(obj, new Color(1f, 0.9f, 0.4f));

            // Bright flash
            StartCoroutine(FlashEffect(position, new Color(1f, 0.95f, 0.6f), 0.2f, scale * 1.2f));

            Destroy(obj, 1.5f);
        }

        // ============ HELPERS ============

        /// <summary>
        /// Create and configure a particle system, stopping auto-play to avoid warnings
        /// </summary>
        private ParticleSystem CreateParticleSystem(GameObject obj)
        {
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            // Stop immediately to prevent "duration while playing" warnings
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;

            return ps;
        }

        /// <summary>
        /// Finalize and start the particle system
        /// </summary>
        private void FinalizeAndPlay(ParticleSystem ps)
        {
            ps.Play();
        }

        private void SetupParticleRenderer(GameObject obj, Color? color = null)
        {
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.SetFloat("_Mode", 2); // Fade
                if (color.HasValue)
                {
                    mat.color = color.Value;
                }
                renderer.material = mat;
            }
        }

        private IEnumerator FlashEffect(Vector3 position, Color color, float duration, float size)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.transform.position = position;
            flash.transform.localScale = Vector3.one * size;
            Destroy(flash.GetComponent<Collider>());

            Material mat = flash.GetComponent<Renderer>().material;
            mat.color = color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 2f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = size * (1f + t * 0.5f);
                flash.transform.localScale = Vector3.one * scale;
                mat.color = new Color(color.r, color.g, color.b, 1f - t);
                yield return null;
            }

            Destroy(flash);
        }

        private IEnumerator PulseAndFade(GameObject obj, float duration)
        {
            float elapsed = 0f;
            Vector3 startScale = obj.transform.localScale;
            Material mat = obj.GetComponent<Renderer>().material;
            Color startColor = mat.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Pulse
                float pulse = 1f + Mathf.Sin(t * Mathf.PI * 4f) * 0.1f;
                obj.transform.localScale = startScale * pulse * (1f + t * 0.3f);
                
                // Fade
                mat.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - t));
                
                yield return null;
            }
        }
    }

    public enum VFXType
    {
        Impact,
        FireExplosion,
        IceShatter,
        LightningStrike,
        Heal,
        Shield,
        LevelUp,
        Death,
        Slash,
        Buff,
        Debuff,
        // Medieval combat effects
        MeleeHit,
        ArrowHit,
        MagicBurst,
        ManaReady,
        SoulWisp,
        Merge
    }
}
