using UnityEngine;

namespace Crestforge.UI
{
    /// <summary>
    /// Generates procedural icons for crests
    /// </summary>
    public static class CrestIcons
    {
        private static Sprite minorCrestSprite;
        private static Sprite majorCrestSprite;

        public static Sprite GetMinorCrestIcon()
        {
            if (minorCrestSprite == null)
            {
                minorCrestSprite = CreateShieldSprite(64, new Color(0.6f, 0.4f, 0.8f), false);
            }
            return minorCrestSprite;
        }

        public static Sprite GetMajorCrestIcon()
        {
            if (majorCrestSprite == null)
            {
                majorCrestSprite = CreateShieldSprite(64, new Color(1f, 0.7f, 0.2f), true);
            }
            return majorCrestSprite;
        }

        private static Sprite CreateShieldSprite(int size, Color color, bool isMajor)
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

            float centerX = size / 2f;
            int padding = 4;

            // Draw shield shape
            for (int y = padding; y < size - padding; y++)
            {
                float normalizedY = (float)(y - padding) / (size - padding * 2);

                // Shield width varies by height - wider at top, pointed at bottom
                float widthMultiplier;
                if (normalizedY < 0.4f)
                {
                    // Top part - wide with slight curve
                    widthMultiplier = 0.9f + normalizedY * 0.1f;
                }
                else
                {
                    // Bottom part - narrows to point
                    float t = (normalizedY - 0.4f) / 0.6f;
                    widthMultiplier = 1f - t * t;
                }

                int halfWidth = Mathf.RoundToInt((size / 2f - padding) * widthMultiplier);

                for (int x = (int)centerX - halfWidth; x <= (int)centerX + halfWidth; x++)
                {
                    if (x >= 0 && x < size)
                    {
                        // Edge detection for border
                        bool isEdge = Mathf.Abs(x - centerX) >= halfWidth - 2 ||
                                     y <= padding + 2 ||
                                     (normalizedY > 0.9f);

                        Color pixelColor;
                        if (isEdge)
                        {
                            // Border - brighter
                            pixelColor = new Color(
                                Mathf.Min(1f, color.r + 0.3f),
                                Mathf.Min(1f, color.g + 0.2f),
                                Mathf.Min(1f, color.b + 0.3f),
                                1f
                            );
                        }
                        else
                        {
                            // Inner gradient
                            float distFromCenter = Mathf.Abs(x - centerX) / halfWidth;
                            float brightness = 1f - distFromCenter * 0.3f - normalizedY * 0.2f;
                            pixelColor = new Color(
                                color.r * brightness,
                                color.g * brightness,
                                color.b * brightness,
                                1f
                            );
                        }

                        texture.SetPixel(x, size - 1 - y, pixelColor); // Flip Y
                    }
                }
            }

            // Add emblem in center
            if (isMajor)
            {
                // Star for major crest
                DrawStar(texture, size / 2, size / 2 - 4, 8,
                    new Color(1f, 0.95f, 0.7f));
            }
            else
            {
                // Diamond for minor crest
                DrawDiamond(texture, size / 2, size / 2 - 4, 6,
                    new Color(0.9f, 0.8f, 1f));
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void DrawStar(Texture2D texture, int cx, int cy, int radius, Color color)
        {
            // Simple 4-point star
            for (int i = -radius; i <= radius; i++)
            {
                // Horizontal line
                int x = cx + i;
                int thickness = Mathf.Max(1, radius / 3 - Mathf.Abs(i) / 2);
                for (int t = -thickness; t <= thickness; t++)
                {
                    if (x >= 0 && x < texture.width && cy + t >= 0 && cy + t < texture.height)
                        texture.SetPixel(x, cy + t, color);
                }

                // Vertical line
                int y = cy + i;
                for (int t = -thickness; t <= thickness; t++)
                {
                    if (cx + t >= 0 && cx + t < texture.width && y >= 0 && y < texture.height)
                        texture.SetPixel(cx + t, y, color);
                }
            }
        }

        private static void DrawDiamond(Texture2D texture, int cx, int cy, int radius, Color color)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int width = radius - Mathf.Abs(y);
                for (int x = -width; x <= width; x++)
                {
                    int px = cx + x;
                    int py = cy + y;
                    if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                    {
                        texture.SetPixel(px, py, color);
                    }
                }
            }
        }
    }
}
