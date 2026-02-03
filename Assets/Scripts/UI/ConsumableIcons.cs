using UnityEngine;

namespace Crestforge.UI
{
    /// <summary>
    /// Generates procedural icons for consumable items
    /// </summary>
    public static class ConsumableIcons
    {
        private static Sprite crestTokenSprite;
        private static Sprite itemAnvilSprite;

        public static Sprite GetCrestTokenIcon()
        {
            if (crestTokenSprite == null)
            {
                crestTokenSprite = CreateCircleSprite(64, new Color(0.8f, 0.5f, 1f));
            }
            return crestTokenSprite;
        }

        public static Sprite GetItemAnvilIcon()
        {
            if (itemAnvilSprite == null)
            {
                itemAnvilSprite = CreateAnvilSprite(64, new Color(1f, 0.8f, 0.3f));
            }
            return itemAnvilSprite;
        }

        private static Sprite CreateCircleSprite(int size, Color color)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            float center = size / 2f;
            float radius = size / 2f - 2f;
            float innerRadius = radius * 0.6f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius)
                    {
                        // Outer ring
                        if (dist > innerRadius)
                        {
                            float edgeSoftness = Mathf.Clamp01((radius - dist) / 2f);
                            texture.SetPixel(x, y, new Color(color.r, color.g, color.b, edgeSoftness));
                        }
                        // Inner filled circle with gradient
                        else
                        {
                            float brightness = 1f - (dist / innerRadius) * 0.3f;
                            Color innerColor = new Color(
                                Mathf.Min(1f, color.r * brightness + 0.2f),
                                Mathf.Min(1f, color.g * brightness + 0.1f),
                                Mathf.Min(1f, color.b * brightness + 0.2f),
                                1f
                            );
                            texture.SetPixel(x, y, innerColor);
                        }
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateAnvilSprite(int size, Color color)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            // Clear background
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }

            // Draw anvil shape
            int padding = 4;
            int anvilWidth = size - padding * 2;
            int anvilHeight = size - padding * 2;

            // Base (wide bottom)
            int baseHeight = anvilHeight / 5;
            int baseY = padding;
            DrawRect(texture, padding, baseY, anvilWidth, baseHeight, color);

            // Stem (narrow middle)
            int stemWidth = anvilWidth / 3;
            int stemHeight = anvilHeight / 3;
            int stemX = padding + (anvilWidth - stemWidth) / 2;
            int stemY = baseY + baseHeight;
            DrawRect(texture, stemX, stemY, stemWidth, stemHeight, color);

            // Top (wide work surface with horn)
            int topHeight = anvilHeight / 3;
            int topY = stemY + stemHeight;
            int topWidth = anvilWidth;
            DrawRect(texture, padding, topY, topWidth, topHeight, color);

            // Horn (triangular extension on right)
            int hornWidth = anvilWidth / 4;
            int hornHeight = topHeight / 2;
            int hornX = padding + topWidth - 2;
            int hornY = topY + topHeight / 4;
            DrawTriangle(texture, hornX, hornY, hornWidth, hornHeight, color, true);

            // Add highlight
            Color highlight = new Color(
                Mathf.Min(1f, color.r + 0.3f),
                Mathf.Min(1f, color.g + 0.2f),
                Mathf.Min(1f, color.b + 0.1f),
                1f
            );
            DrawRect(texture, padding + 2, topY + topHeight - 3, topWidth - 4, 2, highlight);

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            for (int py = y; py < y + height && py < texture.height; py++)
            {
                for (int px = x; px < x + width && px < texture.width; px++)
                {
                    if (px >= 0 && py >= 0)
                    {
                        texture.SetPixel(px, py, color);
                    }
                }
            }
        }

        private static void DrawTriangle(Texture2D texture, int x, int y, int width, int height, Color color, bool pointRight)
        {
            for (int py = 0; py < height; py++)
            {
                float progress = (float)py / height;
                int rowWidth = pointRight ?
                    Mathf.RoundToInt(width * (1f - Mathf.Abs(progress - 0.5f) * 2f)) :
                    Mathf.RoundToInt(width * progress);

                for (int px = 0; px < rowWidth; px++)
                {
                    int drawX = x + px;
                    int drawY = y + py;
                    if (drawX >= 0 && drawX < texture.width && drawY >= 0 && drawY < texture.height)
                    {
                        texture.SetPixel(drawX, drawY, color);
                    }
                }
            }
        }
    }
}
