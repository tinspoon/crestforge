using UnityEngine;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Component attached to each hex tile for interaction
    /// </summary>
    public class HexTile3D : MonoBehaviour
    {
        public Color baseColor;
        public bool useMedievalTheme = true;
        private Material material;
        private bool isHighlighted;
        private float pulseTime;

        private void Awake()
        {
            material = GetComponent<MeshRenderer>()?.material;
        }

        private void Update()
        {
            // Subtle pulse effect when highlighted
            if (isHighlighted && material != null && useMedievalTheme)
            {
                pulseTime += Time.deltaTime * 2f;
                float pulse = Mathf.Sin(pulseTime) * 0.05f + 0.95f;
                material.SetFloat("_RimIntensity", 0.3f + pulse * 0.2f);
            }
        }

        public void SetHighlight(Color color)
        {
            isHighlighted = true;
            pulseTime = 0;

            if (material != null)
            {
                if (useMedievalTheme)
                {
                    material.SetColor("_MainColor", color);
                    material.SetColor("_RimColor", Color.Lerp(color, Color.white, 0.5f));
                    material.SetFloat("_RimIntensity", 0.5f);
                }
                else
                {
                    material.color = color;
                }
            }
        }

        public void ClearHighlight()
        {
            isHighlighted = false;

            if (material != null)
            {
                if (useMedievalTheme)
                {
                    material.SetColor("_MainColor", baseColor);
                    material.SetColor("_RimColor", Color.Lerp(baseColor, Color.white, 0.3f));
                    material.SetFloat("_RimIntensity", 0.15f);
                }
                else
                {
                    material.color = baseColor;
                }
            }
        }

        public void SetHover(bool hover)
        {
            if (isHighlighted) return;

            if (material != null)
            {
                if (hover)
                {
                    Color hoverColor = Color.Lerp(baseColor, Color.white, 0.15f);
                    if (useMedievalTheme)
                    {
                        material.SetColor("_MainColor", hoverColor);
                        material.SetFloat("_RimIntensity", 0.25f);
                    }
                    else
                    {
                        material.color = hoverColor;
                    }
                }
                else
                {
                    if (useMedievalTheme)
                    {
                        material.SetColor("_MainColor", baseColor);
                        material.SetFloat("_RimIntensity", 0.15f);
                    }
                    else
                    {
                        material.color = baseColor;
                    }
                }
            }
        }
    }
}
