using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Combat;

namespace Crestforge.UI
{
    /// <summary>
    /// Visualizes combat with damage numbers, attack lines, and effects
    /// </summary>
    public class CombatVisualizer : MonoBehaviour
    {
        public static CombatVisualizer Instance { get; private set; }

        [Header("Settings")]
        public float damageNumberDuration = 1f;
        public float damageNumberSpeed = 1.5f;

        [Header("Colors")]
        public Color damageColor = Color.white;
        public Color healColor = new Color(0.3f, 1f, 0.3f);
        public Color abilityColor = new Color(0.3f, 0.7f, 1f);

        private List<FloatingText> floatingTexts = new List<FloatingText>();
        private List<VisualEffect> effects = new List<VisualEffect>();

        private CombatManager combatManager;
        private HexGridRenderer hexGrid;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            combatManager = CombatManager.Instance;
            hexGrid = FindAnyObjectByType<HexGridRenderer>();

            if (combatManager != null)
            {
                combatManager.OnDamageDealt += HandleDamage;
                combatManager.OnUnitDied += HandleDeath;
                combatManager.OnAbilityCast += HandleAbility;
            }
        }

        private void OnDestroy()
        {
            if (combatManager != null)
            {
                combatManager.OnDamageDealt -= HandleDamage;
                combatManager.OnUnitDied -= HandleDeath;
                combatManager.OnAbilityCast -= HandleAbility;
            }
        }

        private void Update()
        {
            // Update floating texts
            for (int i = floatingTexts.Count - 1; i >= 0; i--)
            {
                var ft = floatingTexts[i];
                ft.timer -= Time.deltaTime;

                if (ft.timer <= 0 || ft.obj == null)
                {
                    if (ft.obj != null) Destroy(ft.obj);
                    floatingTexts.RemoveAt(i);
                    continue;
                }

                ft.obj.transform.position += ft.velocity * Time.deltaTime;
                ft.velocity *= 0.95f;

                float alpha = ft.timer / damageNumberDuration;
                var tm = ft.obj.GetComponent<TextMesh>();
                if (tm != null)
                {
                    Color c = tm.color;
                    c.a = alpha;
                    tm.color = c;
                }
            }

            // Update effects
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var e = effects[i];
                e.timer -= Time.deltaTime;

                if (e.timer <= 0 || e.obj == null)
                {
                    if (e.obj != null) Destroy(e.obj);
                    effects.RemoveAt(i);
                    continue;
                }

                e.obj.transform.position += e.velocity * Time.deltaTime;
                
                if (e.scaleUp)
                {
                    float t = 1f - (e.timer / e.duration);
                    e.obj.transform.localScale = Vector3.Lerp(e.startScale, e.endScale, t);
                }

                var sr = e.obj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    float alpha = e.timer / e.duration;
                    Color c = sr.color;
                    c.a = alpha * e.startAlpha;
                    sr.color = c;
                }
            }
        }

        private void HandleDamage(CombatUnit source, CombatUnit target, int damage)
        {
            Vector3 pos = GetWorldPos(target);
            // Note: Damage numbers are spawned by BoardManager3D.PlayDamage to avoid duplicates
            SpawnHitSparks(pos);

            if (source != null)
            {
                SpawnAttackLine(GetWorldPos(source), pos, source.team == Team.Player);
            }
        }

        private void HandleDeath(CombatUnit unit)
        {
            Vector3 pos = GetWorldPos(unit);
            SpawnText(pos, "💀", Color.red, 1.5f);
            SpawnDeathRing(pos);
        }

        private void HandleAbility(CombatUnit caster)
        {
            Vector3 pos = GetWorldPos(caster);
            string name = caster.source.template.ability?.abilityName ?? "Ability";
            SpawnText(pos + Vector3.up * 0.4f, name, abilityColor, 1f);
            SpawnAbilityGlow(pos, caster.team == Team.Player);
        }

        private void SpawnDamageNumber(Vector3 pos, int amount)
        {
            SpawnText(pos, amount.ToString(), damageColor, 1.2f);
        }

        private void SpawnText(Vector3 pos, string text, Color color, float scale)
        {
            GameObject obj = new GameObject("Text");
            obj.transform.position = pos + new Vector3(Random.Range(-0.1f, 0.1f), 0.1f, -3);

            TextMesh tm = obj.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 32;
            tm.characterSize = 0.05f * scale;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.fontStyle = FontStyle.Bold;
            obj.GetComponent<MeshRenderer>().sortingOrder = 100;

            // Shadow
            GameObject shadow = new GameObject("Shadow");
            shadow.transform.parent = obj.transform;
            shadow.transform.localPosition = new Vector3(0.015f, -0.015f, 0.01f);
            TextMesh stm = shadow.AddComponent<TextMesh>();
            stm.text = text;
            stm.fontSize = 32;
            stm.characterSize = 0.05f * scale;
            stm.anchor = TextAnchor.MiddleCenter;
            stm.alignment = TextAlignment.Center;
            stm.color = new Color(0, 0, 0, 0.7f);
            stm.fontStyle = FontStyle.Bold;
            shadow.GetComponent<MeshRenderer>().sortingOrder = 99;

            floatingTexts.Add(new FloatingText
            {
                obj = obj,
                timer = damageNumberDuration,
                velocity = new Vector3(Random.Range(-0.2f, 0.2f), damageNumberSpeed, 0)
            });
        }

        private void SpawnHitSparks(Vector3 pos)
        {
            for (int i = 0; i < 4; i++)
            {
                GameObject obj = new GameObject("Spark");
                obj.transform.position = pos + new Vector3(0, 0, -2);
                
                SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCircle(4);
                sr.color = Color.white;
                sr.sortingOrder = 90;

                effects.Add(new VisualEffect
                {
                    obj = obj,
                    timer = 0.25f,
                    duration = 0.25f,
                    velocity = new Vector3(Random.Range(-2f, 2f), Random.Range(-1f, 2f), 0),
                    startScale = Vector3.one * 0.15f,
                    endScale = Vector3.one * 0.05f,
                    scaleUp = true,
                    startAlpha = 1f
                });
            }
        }

        private void SpawnAttackLine(Vector3 from, Vector3 to, bool isPlayer)
        {
            GameObject obj = new GameObject("AttackLine");
            LineRenderer lr = obj.AddComponent<LineRenderer>();
            
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(from.x, from.y, -2));
            lr.SetPosition(1, new Vector3(to.x, to.y, -2));
            lr.startWidth = 0.04f;
            lr.endWidth = 0.01f;
            
            Color c = isPlayer ? new Color(0.4f, 0.7f, 1f, 0.9f) : new Color(1f, 0.4f, 0.4f, 0.9f);
            lr.startColor = c;
            lr.endColor = new Color(c.r, c.g, c.b, 0.3f);
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = 50;

            effects.Add(new VisualEffect
            {
                obj = obj,
                timer = 0.12f,
                duration = 0.12f,
                velocity = Vector3.zero,
                startAlpha = 1f
            });
        }

        private void SpawnDeathRing(Vector3 pos)
        {
            GameObject obj = new GameObject("DeathRing");
            obj.transform.position = pos + new Vector3(0, 0, -1);
            
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateRing(32);
            sr.color = new Color(0.9f, 0.2f, 0.2f, 0.8f);
            sr.sortingOrder = 85;

            effects.Add(new VisualEffect
            {
                obj = obj,
                timer = 0.4f,
                duration = 0.4f,
                velocity = Vector3.zero,
                startScale = Vector3.one * 0.2f,
                endScale = Vector3.one * 1.2f,
                scaleUp = true,
                startAlpha = 0.8f
            });
        }

        private void SpawnAbilityGlow(Vector3 pos, bool isPlayer)
        {
            GameObject obj = new GameObject("AbilityGlow");
            obj.transform.position = pos + new Vector3(0, 0, -0.5f);
            
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircle(32);
            sr.color = isPlayer ? new Color(0.3f, 0.6f, 1f, 0.5f) : new Color(1f, 0.3f, 0.5f, 0.5f);
            sr.sortingOrder = 8;

            effects.Add(new VisualEffect
            {
                obj = obj,
                timer = 0.35f,
                duration = 0.35f,
                velocity = Vector3.zero,
                startScale = Vector3.one * 0.4f,
                endScale = Vector3.one * 1f,
                scaleUp = true,
                startAlpha = 0.6f
            });
        }

        private Vector3 GetWorldPos(CombatUnit unit)
        {
            if (hexGrid == null) return Vector3.zero;
            float hexSize = hexGrid.hexSize;
            Vector2 offset = hexGrid.gridOffset;
            int x = unit.position.x;
            int y = unit.position.y;
            float xOff = (y % 2 == 1) ? hexSize * 0.5f : 0;
            return new Vector3(x * hexSize + xOff + offset.x, y * hexSize * 0.866f + offset.y, 0);
        }

        private Sprite CreateCircle(int size)
        {
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size / 2f - 1;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = Vector2.Distance(new Vector2(x, y), c) < r ? Color.white : Color.clear;
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private Sprite CreateRing(int size)
        {
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float outer = size / 2f - 1;
            float inner = outer - 3;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    px[y * size + x] = (d < outer && d > inner) ? Color.white : Color.clear;
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private class FloatingText
        {
            public GameObject obj;
            public float timer;
            public Vector3 velocity;
        }

        private class VisualEffect
        {
            public GameObject obj;
            public float timer;
            public float duration;
            public Vector3 velocity;
            public Vector3 startScale;
            public Vector3 endScale;
            public bool scaleUp;
            public float startAlpha;
        }
    }
}