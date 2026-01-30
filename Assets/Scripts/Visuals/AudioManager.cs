using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Combat;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Manages all game audio - sound effects and music.
    /// Uses procedural sounds as placeholders until real audio is added.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Volume Settings")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float musicVolume = 0.5f;

        [Header("Audio Clips (Optional - uses procedural if empty)")]
        public AudioClip[] meleeSounds;
        public AudioClip[] rangedSounds;
        public AudioClip[] hitSounds;
        public AudioClip[] deathSounds;
        public AudioClip[] abilitySounds;
        public AudioClip[] uiClickSounds;
        public AudioClip[] uiHoverSounds;
        public AudioClip[] purchaseSounds;
        public AudioClip[] levelUpSounds;
        public AudioClip[] victorySounds;
        public AudioClip[] defeatSounds;
        public AudioClip backgroundMusic;

        [Header("Pitch Variation")]
        public float pitchVariation = 0.1f;

        // Audio sources
        private AudioSource sfxSource;
        private AudioSource musicSource;
        private List<AudioSource> pooledSources = new List<AudioSource>();
        private int poolSize = 10;

        // Procedural sound cache
        private Dictionary<string, AudioClip> proceduralClips = new Dictionary<string, AudioClip>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeAudio();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Subscribe to combat events
            SubscribeToCombatEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromCombatEvents();
        }

        private void InitializeAudio()
        {
            // Create main SFX source
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            // Create music source
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;

            // Create pooled sources for overlapping sounds
            for (int i = 0; i < poolSize; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                pooledSources.Add(source);
            }

            // Generate procedural sounds
            GenerateProceduralSounds();
        }

        private void SubscribeToCombatEvents()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnDamageDealt += HandleDamageDealt;
                CombatManager.Instance.OnUnitDied += HandleUnitDied;
                CombatManager.Instance.OnAbilityCast += HandleAbilityCast;
                CombatManager.Instance.OnCombatEnd += HandleCombatEnd;
            }
        }

        private void UnsubscribeFromCombatEvents()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnDamageDealt -= HandleDamageDealt;
                CombatManager.Instance.OnUnitDied -= HandleUnitDied;
                CombatManager.Instance.OnAbilityCast -= HandleAbilityCast;
                CombatManager.Instance.OnCombatEnd -= HandleCombatEnd;
            }
        }

        // ============ PUBLIC PLAY METHODS ============

        public void PlayMeleeAttack()
        {
            PlaySound(meleeSounds, "melee", 0.6f);
        }

        public void PlayRangedAttack()
        {
            PlaySound(rangedSounds, "ranged", 0.5f);
        }

        public void PlayHit()
        {
            PlaySound(hitSounds, "hit", 0.7f);
        }

        public void PlayDeath()
        {
            PlaySound(deathSounds, "death", 0.8f);
        }

        public void PlayAbility(string abilityType = "default")
        {
            string key = $"ability_{abilityType}";
            if (!proceduralClips.ContainsKey(key))
                key = "ability_default";
            PlaySound(abilitySounds, key, 0.7f);
        }

        public void PlayUIClick()
        {
            PlaySound(uiClickSounds, "ui_click", 0.5f);
        }

        public void PlayUIHover()
        {
            PlaySound(uiHoverSounds, "ui_hover", 0.3f);
        }

        public void PlayPurchase()
        {
            PlaySound(purchaseSounds, "purchase", 0.6f);
        }

        public void PlayLevelUp()
        {
            PlaySound(levelUpSounds, "levelup", 0.8f);
        }

        public void PlayVictory()
        {
            PlaySound(victorySounds, "victory", 1f);
        }

        public void PlayDefeat()
        {
            PlaySound(defeatSounds, "defeat", 1f);
        }

        public void PlayProjectileFire(ProjectileType type)
        {
            string key = type switch
            {
                ProjectileType.Arrow => "arrow_fire",
                ProjectileType.Fireball => "fire_cast",
                ProjectileType.IceShard => "ice_cast",
                ProjectileType.Lightning => "lightning_cast",
                _ => "ranged"
            };
            PlayProceduralSound(key, 0.5f);
        }

        public void PlayProjectileHit(ProjectileType type)
        {
            string key = type switch
            {
                ProjectileType.Arrow => "arrow_hit",
                ProjectileType.Fireball => "fire_explosion",
                ProjectileType.IceShard => "ice_shatter",
                ProjectileType.Lightning => "lightning_strike",
                _ => "hit"
            };
            PlayProceduralSound(key, 0.6f);
        }

        public void StartMusic()
        {
            if (backgroundMusic != null)
            {
                musicSource.clip = backgroundMusic;
                musicSource.volume = musicVolume * masterVolume;
                musicSource.Play();
            }
        }

        public void StopMusic()
        {
            musicSource.Stop();
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            musicSource.volume = musicVolume * masterVolume;
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            musicSource.volume = musicVolume * masterVolume;
        }

        // ============ COMBAT EVENT HANDLERS ============

        private void HandleDamageDealt(CombatUnit attacker, CombatUnit target, int damage)
        {
            // Play attack sound based on range
            if (attacker.stats.range > 1)
            {
                // Ranged attack - sound handled by projectile system
            }
            else
            {
                PlayMeleeAttack();
                // Delay hit sound slightly
                StartCoroutine(DelayedSound(() => PlayHit(), 0.1f));
            }
        }

        private void HandleUnitDied(CombatUnit unit)
        {
            PlayDeath();
        }

        private void HandleAbilityCast(CombatUnit caster)
        {
            if (caster.source.template.ability != null)
            {
                string abilityType = GetAbilityType(caster.source.template);
                PlayAbility(abilityType);
            }
        }

        private void HandleCombatEnd(CombatResult result)
        {
            if (result.victory)
                PlayVictory();
            else
                PlayDefeat();
        }

        private string GetAbilityType(UnitData template)
        {
            if (template == null || template.traits == null)
                return "default";

            foreach (var trait in template.traits)
            {
                if (trait == null) continue;
                string traitName = trait.traitName.ToLower();

                if (traitName.Contains("fire") || traitName.Contains("flame"))
                    return "fire";
                if (traitName.Contains("ice") || traitName.Contains("frost"))
                    return "ice";
                if (traitName.Contains("lightning") || traitName.Contains("storm"))
                    return "lightning";
                if (traitName.Contains("healer") || traitName.Contains("support"))
                    return "heal";
                if (traitName.Contains("shadow") || traitName.Contains("dark"))
                    return "shadow";
            }

            return "default";
        }

        private IEnumerator DelayedSound(System.Action playAction, float delay)
        {
            yield return new WaitForSeconds(delay);
            playAction?.Invoke();
        }

        // ============ SOUND PLAYING ============

        private void PlaySound(AudioClip[] clips, string proceduralKey, float volume)
        {
            AudioClip clip = null;

            // Try to use provided clips first
            if (clips != null && clips.Length > 0)
            {
                clip = clips[Random.Range(0, clips.Length)];
            }
            // Fall back to procedural
            else if (proceduralClips.ContainsKey(proceduralKey))
            {
                clip = proceduralClips[proceduralKey];
            }

            if (clip != null)
            {
                PlayClip(clip, volume);
            }
        }

        private void PlayProceduralSound(string key, float volume)
        {
            if (proceduralClips.ContainsKey(key))
            {
                PlayClip(proceduralClips[key], volume);
            }
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            // Find available pooled source
            AudioSource source = GetAvailableSource();
            
            source.clip = clip;
            source.volume = volume * sfxVolume * masterVolume;
            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            source.Play();
        }

        private AudioSource GetAvailableSource()
        {
            foreach (var source in pooledSources)
            {
                if (!source.isPlaying)
                    return source;
            }
            // All busy, use main source
            return sfxSource;
        }

        // ============ PROCEDURAL SOUND GENERATION ============

        private void GenerateProceduralSounds()
        {
            // Combat sounds
            proceduralClips["melee"] = GenerateMeleeSound();
            proceduralClips["ranged"] = GenerateRangedSound();
            proceduralClips["hit"] = GenerateHitSound();
            proceduralClips["death"] = GenerateDeathSound();

            // Projectile sounds
            proceduralClips["arrow_fire"] = GenerateArrowFireSound();
            proceduralClips["arrow_hit"] = GenerateArrowHitSound();
            proceduralClips["fire_cast"] = GenerateFireCastSound();
            proceduralClips["fire_explosion"] = GenerateFireExplosionSound();
            proceduralClips["ice_cast"] = GenerateIceCastSound();
            proceduralClips["ice_shatter"] = GenerateIceShatterSound();
            proceduralClips["lightning_cast"] = GenerateLightningCastSound();
            proceduralClips["lightning_strike"] = GenerateLightningStrikeSound();

            // Ability sounds
            proceduralClips["ability_default"] = GenerateAbilitySound();
            proceduralClips["ability_fire"] = GenerateFireCastSound();
            proceduralClips["ability_ice"] = GenerateIceCastSound();
            proceduralClips["ability_lightning"] = GenerateLightningCastSound();
            proceduralClips["ability_heal"] = GenerateHealSound();
            proceduralClips["ability_shadow"] = GenerateShadowSound();

            // UI sounds
            proceduralClips["ui_click"] = GenerateUIClickSound();
            proceduralClips["ui_hover"] = GenerateUIHoverSound();
            proceduralClips["purchase"] = GeneratePurchaseSound();
            proceduralClips["levelup"] = GenerateLevelUpSound();
            proceduralClips["victory"] = GenerateVictorySound();
            proceduralClips["defeat"] = GenerateDefeatSound();
        }

        // ============ SOUND GENERATORS ============

        private AudioClip GenerateMeleeSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.15f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 15f);
                
                // Swoosh + impact
                float swoosh = Mathf.Sin(i * 0.1f + t * 50f) * (1f - t);
                float impact = Mathf.Sin(i * 0.02f) * Mathf.Exp(-t * 20f);
                float noise = (Random.value - 0.5f) * 0.3f * envelope;
                
                data[i] = (swoosh * 0.4f + impact * 0.4f + noise) * envelope;
            }

            return CreateClip("melee", data, sampleRate);
        }

        private AudioClip GenerateRangedSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.2f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 8f);
                
                // Twang + whoosh
                float twang = Mathf.Sin(i * 0.15f) * Mathf.Exp(-t * 25f);
                float whoosh = (Random.value - 0.5f) * (1f - t) * 0.5f;
                
                data[i] = (twang * 0.6f + whoosh * 0.4f) * envelope;
            }

            return CreateClip("ranged", data, sampleRate);
        }

        private AudioClip GenerateHitSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.1f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 25f);
                
                // Thump
                float thump = Mathf.Sin(i * 0.03f + Mathf.Sin(i * 0.01f) * 2f);
                float noise = (Random.value - 0.5f) * 0.4f;
                
                data[i] = (thump * 0.6f + noise * 0.4f) * envelope;
            }

            return CreateClip("hit", data, sampleRate);
        }

        private AudioClip GenerateDeathSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.4f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 5f);
                
                // Descending tone + noise
                float freq = 200f * (1f - t * 0.7f);
                float tone = Mathf.Sin(i * freq / sampleRate * Mathf.PI * 2f);
                float noise = (Random.value - 0.5f) * 0.3f;
                
                data[i] = (tone * 0.5f + noise * 0.3f) * envelope;
            }

            return CreateClip("death", data, sampleRate);
        }

        private AudioClip GenerateArrowFireSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.12f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.1f ? t * 10f : Mathf.Exp(-(t - 0.1f) * 15f);
                
                float twang = Mathf.Sin(i * 0.2f) * Mathf.Exp(-t * 30f);
                float whoosh = (Random.value - 0.5f) * (1f - t);
                
                data[i] = (twang * 0.5f + whoosh * 0.3f) * envelope;
            }

            return CreateClip("arrow_fire", data, sampleRate);
        }

        private AudioClip GenerateArrowHitSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.08f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 35f);
                
                float thud = Mathf.Sin(i * 0.05f);
                float crack = (Random.value - 0.5f) * Mathf.Exp(-t * 50f);
                
                data[i] = (thud * 0.5f + crack * 0.5f) * envelope;
            }

            return CreateClip("arrow_hit", data, sampleRate);
        }

        private AudioClip GenerateFireCastSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.3f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.1f ? t * 10f : Mathf.Exp(-(t - 0.1f) * 5f);
                
                // Whoosh + crackle
                float whoosh = Mathf.Sin(i * 0.05f + Mathf.Sin(i * 0.01f) * 3f);
                float crackle = (Random.value - 0.5f) * Mathf.PerlinNoise(i * 0.01f, 0f);
                
                data[i] = (whoosh * 0.4f + crackle * 0.6f) * envelope;
            }

            return CreateClip("fire_cast", data, sampleRate);
        }

        private AudioClip GenerateFireExplosionSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.4f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.05f ? t * 20f : Mathf.Exp(-(t - 0.05f) * 6f);
                
                // Boom + crackle
                float boom = Mathf.Sin(i * 0.02f + Mathf.Sin(i * 0.005f) * 5f);
                float crackle = (Random.value - 0.5f) * 0.8f;
                
                data[i] = (boom * 0.5f + crackle * 0.5f) * envelope;
            }

            return CreateClip("fire_explosion", data, sampleRate);
        }

        private AudioClip GenerateIceCastSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.25f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.1f ? t * 10f : Mathf.Exp(-(t - 0.1f) * 6f);
                
                // Crystal chime
                float chime = Mathf.Sin(i * 0.3f) * Mathf.Sin(i * 0.15f);
                float shimmer = Mathf.Sin(i * 0.5f + Mathf.Sin(i * 0.2f) * 2f) * 0.3f;
                
                data[i] = (chime * 0.6f + shimmer * 0.4f) * envelope;
            }

            return CreateClip("ice_cast", data, sampleRate);
        }

        private AudioClip GenerateIceShatterSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.2f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 10f);
                
                // Shatter
                float crack = (Random.value - 0.5f) * Mathf.Exp(-t * 15f);
                float tinkle = Mathf.Sin(i * 0.4f + Random.value * 10f) * (1f - t);
                
                data[i] = (crack * 0.6f + tinkle * 0.4f) * envelope;
            }

            return CreateClip("ice_shatter", data, sampleRate);
        }

        private AudioClip GenerateLightningCastSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.15f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.05f ? t * 20f : Mathf.Exp(-(t - 0.05f) * 12f);
                
                // Electric buzz
                float buzz = Mathf.Sin(i * 0.4f) * Mathf.Sin(i * 0.05f);
                float crackle = (Random.value - 0.5f) * (Random.value > 0.7f ? 1f : 0.3f);
                
                data[i] = (buzz * 0.4f + crackle * 0.6f) * envelope;
            }

            return CreateClip("lightning_cast", data, sampleRate);
        }

        private AudioClip GenerateLightningStrikeSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.3f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.02f ? t * 50f : Mathf.Exp(-(t - 0.02f) * 8f);
                
                // Thunder crack
                float crack = (Random.value - 0.5f) * (t < 0.1f ? 1f : 0.5f);
                float rumble = Mathf.Sin(i * 0.01f + Mathf.Sin(i * 0.003f) * 5f) * (1f - t);
                
                data[i] = (crack * 0.7f + rumble * 0.3f) * envelope;
            }

            return CreateClip("lightning_strike", data, sampleRate);
        }

        private AudioClip GenerateAbilitySound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.3f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.1f ? t * 10f : Mathf.Exp(-(t - 0.1f) * 4f);
                
                // Magic whoosh
                float whoosh = Mathf.Sin(i * 0.1f + Mathf.Sin(i * 0.02f) * 3f);
                float shimmer = Mathf.Sin(i * 0.3f) * Mathf.Sin(i * 0.15f) * 0.5f;
                
                data[i] = (whoosh * 0.5f + shimmer * 0.5f) * envelope;
            }

            return CreateClip("ability", data, sampleRate);
        }

        private AudioClip GenerateHealSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.4f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.2f ? t * 5f : Mathf.Exp(-(t - 0.2f) * 3f);
                
                // Ascending chime
                float freq = 400f + t * 400f;
                float chime = Mathf.Sin(i * freq / sampleRate * Mathf.PI * 2f);
                float shimmer = Mathf.Sin(i * 0.2f) * 0.3f;
                
                data[i] = (chime * 0.6f + shimmer * 0.4f) * envelope;
            }

            return CreateClip("heal", data, sampleRate);
        }

        private AudioClip GenerateShadowSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.35f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.15f ? t * 6.67f : Mathf.Exp(-(t - 0.15f) * 4f);
                
                // Dark whoosh
                float dark = Mathf.Sin(i * 0.03f + Mathf.Sin(i * 0.01f) * 4f);
                float whisper = (Random.value - 0.5f) * 0.4f * Mathf.PerlinNoise(i * 0.005f, 0f);
                
                data[i] = (dark * 0.5f + whisper * 0.5f) * envelope;
            }

            return CreateClip("shadow", data, sampleRate);
        }

        private AudioClip GenerateUIClickSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.05f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 40f);
                
                float click = Mathf.Sin(i * 0.3f);
                
                data[i] = click * envelope;
            }

            return CreateClip("ui_click", data, sampleRate);
        }

        private AudioClip GenerateUIHoverSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.03f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 50f);
                
                float tick = Mathf.Sin(i * 0.5f);
                
                data[i] = tick * envelope * 0.5f;
            }

            return CreateClip("ui_hover", data, sampleRate);
        }

        private AudioClip GeneratePurchaseSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.2f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.1f ? t * 10f : Mathf.Exp(-(t - 0.1f) * 8f);
                
                // Coin clink
                float clink1 = Mathf.Sin(i * 0.4f) * Mathf.Exp(-t * 20f);
                float clink2 = Mathf.Sin(i * 0.35f) * Mathf.Exp(-(t - 0.05f) * 15f) * (t > 0.05f ? 1f : 0f);
                
                data[i] = (clink1 * 0.5f + clink2 * 0.5f) * envelope;
            }

            return CreateClip("purchase", data, sampleRate);
        }

        private AudioClip GenerateLevelUpSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.5f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.1f ? t * 10f : Mathf.Exp(-(t - 0.1f) * 3f);
                
                // Ascending fanfare
                float freq = 300f + t * 500f;
                float tone = Mathf.Sin(i * freq / sampleRate * Mathf.PI * 2f);
                float shimmer = Mathf.Sin(i * 0.25f) * 0.3f;
                
                data[i] = (tone * 0.6f + shimmer * 0.4f) * envelope;
            }

            return CreateClip("levelup", data, sampleRate);
        }

        private AudioClip GenerateVictorySound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.8f);
            float[] data = new float[samples];

            // Victory fanfare - three ascending notes
            float[] notes = { 261.63f, 329.63f, 392f }; // C, E, G
            float noteDuration = 0.25f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                int noteIndex = Mathf.Min((int)(t / noteDuration), 2);
                float noteT = (t - noteIndex * noteDuration) / noteDuration;
                float envelope = noteT < 0.1f ? noteT * 10f : Mathf.Exp(-(noteT - 0.1f) * 3f);
                
                float freq = notes[noteIndex];
                float tone = Mathf.Sin(i * freq / sampleRate * Mathf.PI * 2f);
                float harmony = Mathf.Sin(i * freq * 2f / sampleRate * Mathf.PI * 2f) * 0.3f;
                
                data[i] = (tone * 0.6f + harmony * 0.4f) * envelope * 0.8f;
            }

            return CreateClip("victory", data, sampleRate);
        }

        private AudioClip GenerateDefeatSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.6f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 3f);
                
                // Descending sad tone
                float freq = 300f * (1f - t * 0.5f);
                float tone = Mathf.Sin(i * freq / sampleRate * Mathf.PI * 2f);
                float minor = Mathf.Sin(i * freq * 1.2f / sampleRate * Mathf.PI * 2f) * 0.3f;
                
                data[i] = (tone * 0.6f + minor * 0.4f) * envelope * 0.6f;
            }

            return CreateClip("defeat", data, sampleRate);
        }

        private AudioClip CreateClip(string name, float[] data, int sampleRate)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}