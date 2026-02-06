using UnityEngine;

namespace Crestforge.UI
{
    /// <summary>
    /// Generates procedural icons for items with rounded corners
    /// </summary>
    public static class ItemIcons
    {
        private static Sprite _roundedSquareSprite;

        // Cached item-specific icons
        private static Sprite _swordIcon;
        private static Sprite _armorIcon;
        private static Sprite _heartIcon;
        private static Sprite _bootIcon;
        private static Sprite _gemIcon;
        private static Sprite _cloakIcon;
        private static Sprite _frostIcon;
        private static Sprite _daggerIcon;
        private static Sprite _thornIcon;
        private static Sprite _shieldIcon;
        private static Sprite _axeIcon;
        private static Sprite _staffIcon;
        private static Sprite _flameIcon;
        private static Sprite _angelIcon;
        private static Sprite _infinityIcon;
        private static Sprite _hatIcon;
        private static Sprite _crestTokenIcon;

        /// <summary>
        /// Get a rounded square sprite for item backgrounds
        /// </summary>
        public static Sprite GetRoundedSquareSprite()
        {
            if (_roundedSquareSprite != null) return _roundedSquareSprite;

            int size = 64;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;

            float cornerRadius = 12f;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = GetRoundedRectAlpha(x, y, size, size, cornerRadius);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _roundedSquareSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _roundedSquareSprite;
        }

        private static float GetRoundedRectAlpha(int x, int y, int width, int height, float radius)
        {
            float px = x + 0.5f;
            float py = y + 0.5f;

            // Check if we're in a corner region
            bool inLeftCorner = px < radius;
            bool inRightCorner = px > width - radius;
            bool inBottomCorner = py < radius;
            bool inTopCorner = py > height - radius;

            if ((inLeftCorner || inRightCorner) && (inBottomCorner || inTopCorner))
            {
                // We're in a corner - check distance from corner center
                float cx = inLeftCorner ? radius : width - radius;
                float cy = inBottomCorner ? radius : height - radius;
                float dist = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));

                if (dist > radius) return 0f;
                float edge = Mathf.Clamp01((radius - dist) * 2f);
                return edge;
            }

            // Not in corner, fully opaque (with slight edge softening)
            float edgeDist = Mathf.Min(
                Mathf.Min(px, width - px),
                Mathf.Min(py, height - py)
            );
            return Mathf.Clamp01(edgeDist * 2f);
        }

        /// <summary>
        /// Get an icon for a specific item by its itemId
        /// </summary>
        public static Sprite GetItemIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            string id = itemId.ToLower();
            return id switch
            {
                "ironsword" => GetSwordIcon(),
                "leatherarmor" => GetArmorIcon(),
                "healthcharm" => GetHeartIcon(),
                "quickenboots" => GetBootIcon(),
                "managem" => GetGemIcon(),
                "magiccloak" => GetCloakIcon(),
                "frostblade" => GetFrostIcon(),
                "vampiricdagger" => GetDaggerIcon(),
                "thornmail" => GetThornIcon(),
                "spellshield" => GetShieldIcon(),
                "berserkeraxe" => GetAxeIcon(),
                "arcanestaff" => GetStaffIcon(),
                "blazingsword" => GetFlameIcon(),
                "guardianangel" => GetAngelIcon(),
                "infinityedge" => GetInfinityIcon(),
                "deathcap" => GetHatIcon(),
                "crest_token" => GetCrestTokenIcon(),
                "item_anvil" => ConsumableIcons.GetItemAnvilIcon(),
                _ => null
            };
        }

        // Sword icon - simple blade shape
        private static Sprite GetSwordIcon()
        {
            if (_swordIcon != null) return _swordIcon;
            _swordIcon = CreateIcon(64, (tex, size) => {
                Color blade = new Color(0.75f, 0.75f, 0.8f);
                Color hilt = new Color(0.55f, 0.35f, 0.2f);
                Color guard = new Color(0.8f, 0.7f, 0.3f);

                // Blade (diagonal)
                for (int i = 0; i < 36; i++)
                {
                    int x = 18 + i;
                    int y = 10 + i;
                    DrawPixelSafe(tex, x, y, blade);
                    DrawPixelSafe(tex, x+1, y, blade);
                    DrawPixelSafe(tex, x, y+1, blade);
                }
                // Guard
                for (int i = -4; i <= 4; i++)
                {
                    DrawPixelSafe(tex, 20+i, 14-i, guard);
                    DrawPixelSafe(tex, 21+i, 14-i, guard);
                }
                // Hilt
                for (int i = 0; i < 10; i++)
                {
                    DrawPixelSafe(tex, 12+i, 4+i, hilt);
                    DrawPixelSafe(tex, 13+i, 4+i, hilt);
                }
            });
            return _swordIcon;
        }

        // Armor icon - chest plate shape
        private static Sprite GetArmorIcon()
        {
            if (_armorIcon != null) return _armorIcon;
            _armorIcon = CreateIcon(64, (tex, size) => {
                Color metal = new Color(0.5f, 0.45f, 0.4f);
                Color highlight = new Color(0.65f, 0.6f, 0.55f);

                // Main body
                for (int y = 12; y < 52; y++)
                {
                    int width = y < 20 ? (y - 12) * 2 + 10 : (y < 44 ? 26 : 26 - (y - 44));
                    int startX = 32 - width/2;
                    for (int x = startX; x < startX + width; x++)
                    {
                        DrawPixelSafe(tex, x, y, metal);
                    }
                }
                // Highlight stripe
                for (int y = 20; y < 44; y++)
                {
                    DrawPixelSafe(tex, 32, y, highlight);
                    DrawPixelSafe(tex, 33, y, highlight);
                }
            });
            return _armorIcon;
        }

        // Heart icon - health charm
        private static Sprite GetHeartIcon()
        {
            if (_heartIcon != null) return _heartIcon;
            _heartIcon = CreateIcon(64, (tex, size) => {
                Color heart = new Color(0.9f, 0.3f, 0.35f);
                Color highlight = new Color(1f, 0.5f, 0.55f);

                float cx = 32f, cy = 28f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float nx = (x - cx) / 16f;
                        float ny = (y - cy) / 16f;
                        // Heart equation
                        float val = (nx*nx + ny*ny - 1);
                        val = val * val * val - nx*nx * ny*ny*ny;
                        if (val < 0)
                        {
                            Color c = (x < 30 && y > 30) ? highlight : heart;
                            DrawPixelSafe(tex, x, y, c);
                        }
                    }
                }
            });
            return _heartIcon;
        }

        // Boot icon - speed boots
        private static Sprite GetBootIcon()
        {
            if (_bootIcon != null) return _bootIcon;
            _bootIcon = CreateIcon(64, (tex, size) => {
                Color boot = new Color(0.45f, 0.35f, 0.25f);
                Color sole = new Color(0.3f, 0.25f, 0.2f);
                Color wing = new Color(0.9f, 0.85f, 0.5f);

                // Boot shape
                for (int y = 8; y < 24; y++)
                {
                    for (int x = 20; x < 44; x++) DrawPixelSafe(tex, x, y, sole);
                }
                for (int y = 24; y < 48; y++)
                {
                    int w = y < 36 ? 12 : 12 - (y - 36) / 2;
                    for (int x = 26; x < 26 + w; x++) DrawPixelSafe(tex, x, y, boot);
                }
                // Wing accent
                for (int i = 0; i < 8; i++)
                {
                    DrawPixelSafe(tex, 38 + i, 36 + i/2, wing);
                    DrawPixelSafe(tex, 38 + i, 38 - i/2, wing);
                }
            });
            return _bootIcon;
        }

        // Gem icon - mana gem
        private static Sprite GetGemIcon()
        {
            if (_gemIcon != null) return _gemIcon;
            _gemIcon = CreateIcon(64, (tex, size) => {
                Color gem = new Color(0.3f, 0.5f, 0.9f);
                Color shine = new Color(0.6f, 0.75f, 1f);

                // Diamond shape
                for (int y = 12; y < 52; y++)
                {
                    int half = y < 32 ? (y - 12) : (52 - y);
                    for (int x = 32 - half; x < 32 + half; x++)
                    {
                        Color c = (x < 32 && y > 24) ? shine : gem;
                        DrawPixelSafe(tex, x, y, c);
                    }
                }
            });
            return _gemIcon;
        }

        // Cloak icon - magic cloak
        private static Sprite GetCloakIcon()
        {
            if (_cloakIcon != null) return _cloakIcon;
            _cloakIcon = CreateIcon(64, (tex, size) => {
                Color cloak = new Color(0.4f, 0.3f, 0.6f);
                Color trim = new Color(0.7f, 0.6f, 0.3f);

                // Cloak body - flowing shape
                for (int y = 10; y < 54; y++)
                {
                    int wave = (int)(Mathf.Sin(y * 0.2f) * 3);
                    int width = y < 20 ? (y - 10) * 2 : (y < 45 ? 20 + wave : 20 - (y - 45));
                    int startX = 32 - width/2;
                    for (int x = startX; x < startX + width; x++)
                    {
                        DrawPixelSafe(tex, x, y, cloak);
                    }
                }
                // Gold trim at top
                for (int x = 22; x < 42; x++)
                {
                    DrawPixelSafe(tex, x, 52, trim);
                    DrawPixelSafe(tex, x, 53, trim);
                }
            });
            return _cloakIcon;
        }

        // Frost icon - ice blade
        private static Sprite GetFrostIcon()
        {
            if (_frostIcon != null) return _frostIcon;
            _frostIcon = CreateIcon(64, (tex, size) => {
                Color ice = new Color(0.6f, 0.85f, 1f);
                Color frost = new Color(0.85f, 0.95f, 1f);

                // Icicle blade shape
                for (int i = 0; i < 40; i++)
                {
                    int x = 16 + i;
                    int y = 8 + i;
                    int width = Mathf.Max(1, 6 - i/8);
                    for (int w = -width; w <= width; w++)
                    {
                        Color c = (w == 0) ? frost : ice;
                        DrawPixelSafe(tex, x, y+w, c);
                    }
                }
                // Frost sparkles
                DrawPixelSafe(tex, 30, 30, frost);
                DrawPixelSafe(tex, 40, 36, frost);
                DrawPixelSafe(tex, 35, 24, frost);
            });
            return _frostIcon;
        }

        // Dagger icon - vampiric dagger
        private static Sprite GetDaggerIcon()
        {
            if (_daggerIcon != null) return _daggerIcon;
            _daggerIcon = CreateIcon(64, (tex, size) => {
                Color blade = new Color(0.6f, 0.6f, 0.65f);
                Color blood = new Color(0.7f, 0.15f, 0.2f);
                Color hilt = new Color(0.3f, 0.2f, 0.25f);

                // Small dagger blade
                for (int i = 0; i < 28; i++)
                {
                    int x = 22 + i;
                    int y = 14 + i;
                    DrawPixelSafe(tex, x, y, blade);
                    DrawPixelSafe(tex, x+1, y, blade);
                    if (i % 4 == 0) DrawPixelSafe(tex, x, y-1, blood);
                }
                // Hilt
                for (int i = 0; i < 8; i++)
                {
                    DrawPixelSafe(tex, 16+i, 10+i, hilt);
                    DrawPixelSafe(tex, 17+i, 10+i, hilt);
                }
            });
            return _daggerIcon;
        }

        // Thorn icon - thornmail
        private static Sprite GetThornIcon()
        {
            if (_thornIcon != null) return _thornIcon;
            _thornIcon = CreateIcon(64, (tex, size) => {
                Color thorn = new Color(0.4f, 0.55f, 0.3f);
                Color spike = new Color(0.55f, 0.7f, 0.4f);

                // Central vine
                for (int y = 10; y < 54; y++)
                {
                    int wave = (int)(Mathf.Sin(y * 0.3f) * 2);
                    DrawPixelSafe(tex, 32 + wave, y, thorn);
                    DrawPixelSafe(tex, 33 + wave, y, thorn);
                }
                // Thorns/spikes
                int[] spikeY = { 18, 28, 38, 48 };
                foreach (int sy in spikeY)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        DrawPixelSafe(tex, 36 + i, sy + i/2, spike);
                        DrawPixelSafe(tex, 28 - i, sy + 5 + i/2, spike);
                    }
                }
            });
            return _thornIcon;
        }

        // Shield icon - spell shield
        private static Sprite GetShieldIcon()
        {
            if (_shieldIcon != null) return _shieldIcon;
            _shieldIcon = CreateIcon(64, (tex, size) => {
                Color shield = new Color(0.4f, 0.5f, 0.7f);
                Color magic = new Color(0.7f, 0.8f, 1f);
                Color rim = new Color(0.6f, 0.55f, 0.4f);

                // Shield body
                for (int y = 12; y < 52; y++)
                {
                    int width = y < 36 ? 24 : 24 - (y - 36);
                    int startX = 32 - width/2;
                    for (int x = startX; x < startX + width; x++)
                    {
                        bool edge = x == startX || x == startX + width - 1;
                        DrawPixelSafe(tex, x, y, edge ? rim : shield);
                    }
                }
                // Magic rune in center
                for (int i = -4; i <= 4; i++)
                {
                    DrawPixelSafe(tex, 32 + i, 30, magic);
                    DrawPixelSafe(tex, 32, 30 + i, magic);
                }
            });
            return _shieldIcon;
        }

        // Axe icon - berserker axe
        private static Sprite GetAxeIcon()
        {
            if (_axeIcon != null) return _axeIcon;
            _axeIcon = CreateIcon(64, (tex, size) => {
                Color blade = new Color(0.6f, 0.6f, 0.65f);
                Color handle = new Color(0.5f, 0.35f, 0.2f);
                Color blood = new Color(0.7f, 0.2f, 0.2f);

                // Handle
                for (int i = 0; i < 40; i++)
                {
                    DrawPixelSafe(tex, 14 + i, 10 + i, handle);
                    DrawPixelSafe(tex, 15 + i, 10 + i, handle);
                }
                // Axe head
                for (int y = 30; y < 50; y++)
                {
                    int width = 12 - Mathf.Abs(y - 40);
                    for (int x = 38; x < 38 + width; x++)
                    {
                        DrawPixelSafe(tex, x, y, blade);
                    }
                }
                // Blood accent
                DrawPixelSafe(tex, 44, 38, blood);
                DrawPixelSafe(tex, 46, 40, blood);
            });
            return _axeIcon;
        }

        // Staff icon - arcane staff
        private static Sprite GetStaffIcon()
        {
            if (_staffIcon != null) return _staffIcon;
            _staffIcon = CreateIcon(64, (tex, size) => {
                Color wood = new Color(0.5f, 0.35f, 0.2f);
                Color orb = new Color(0.6f, 0.4f, 0.9f);
                Color glow = new Color(0.8f, 0.6f, 1f);

                // Staff
                for (int i = 0; i < 44; i++)
                {
                    DrawPixelSafe(tex, 20 + i/2, 8 + i, wood);
                    DrawPixelSafe(tex, 21 + i/2, 8 + i, wood);
                }
                // Orb at top
                for (int y = 44; y < 56; y++)
                {
                    for (int x = 36; x < 48; x++)
                    {
                        float dx = x - 42f;
                        float dy = y - 50f;
                        if (dx*dx + dy*dy < 36)
                        {
                            Color c = (dx < 0 && dy < 0) ? glow : orb;
                            DrawPixelSafe(tex, x, y, c);
                        }
                    }
                }
            });
            return _staffIcon;
        }

        // Flame icon - blazing sword
        private static Sprite GetFlameIcon()
        {
            if (_flameIcon != null) return _flameIcon;
            _flameIcon = CreateIcon(64, (tex, size) => {
                Color blade = new Color(0.7f, 0.7f, 0.75f);
                Color fire1 = new Color(1f, 0.6f, 0.2f);
                Color fire2 = new Color(1f, 0.85f, 0.3f);

                // Blade
                for (int i = 0; i < 32; i++)
                {
                    int x = 20 + i;
                    int y = 12 + i;
                    DrawPixelSafe(tex, x, y, blade);
                    DrawPixelSafe(tex, x+1, y, blade);
                }
                // Flames around blade
                for (int i = 0; i < 28; i += 4)
                {
                    int x = 24 + i;
                    int y = 16 + i;
                    for (int f = 0; f < 6; f++)
                    {
                        Color c = (f % 2 == 0) ? fire1 : fire2;
                        DrawPixelSafe(tex, x - 2 + f/2, y + 4 + f, c);
                        DrawPixelSafe(tex, x + 4 - f/2, y - 2 + f, c);
                    }
                }
            });
            return _flameIcon;
        }

        // Angel icon - guardian angel
        private static Sprite GetAngelIcon()
        {
            if (_angelIcon != null) return _angelIcon;
            _angelIcon = CreateIcon(64, (tex, size) => {
                Color wing = new Color(1f, 0.95f, 0.85f);
                Color halo = new Color(1f, 0.9f, 0.5f);
                Color body = new Color(0.9f, 0.85f, 0.8f);

                // Wings
                for (int y = 20; y < 44; y++)
                {
                    int spread = (44 - y) / 2;
                    // Left wing
                    for (int x = 20 - spread; x < 28; x++)
                        DrawPixelSafe(tex, x, y, wing);
                    // Right wing
                    for (int x = 36; x < 44 + spread; x++)
                        DrawPixelSafe(tex, x, y, wing);
                }
                // Body center
                for (int y = 16; y < 48; y++)
                {
                    for (int x = 28; x < 36; x++)
                        DrawPixelSafe(tex, x, y, body);
                }
                // Halo
                for (int x = 26; x < 38; x++)
                {
                    DrawPixelSafe(tex, x, 50, halo);
                    DrawPixelSafe(tex, x, 51, halo);
                }
            });
            return _angelIcon;
        }

        // Infinity icon - infinity edge
        private static Sprite GetInfinityIcon()
        {
            if (_infinityIcon != null) return _infinityIcon;
            _infinityIcon = CreateIcon(64, (tex, size) => {
                Color blade = new Color(0.75f, 0.75f, 0.8f);
                Color edge = new Color(0.95f, 0.85f, 0.4f);
                Color hilt = new Color(0.4f, 0.3f, 0.5f);

                // Large sword blade
                for (int i = 0; i < 40; i++)
                {
                    int x = 16 + i;
                    int y = 8 + i;
                    int width = Mathf.Max(1, 4 - i/12);
                    for (int w = -width; w <= width; w++)
                    {
                        Color c = (w == width || w == -width) ? edge : blade;
                        DrawPixelSafe(tex, x, y+w, c);
                    }
                }
                // Ornate hilt
                for (int i = 0; i < 12; i++)
                {
                    DrawPixelSafe(tex, 10+i, 4+i, hilt);
                    DrawPixelSafe(tex, 11+i, 4+i, hilt);
                }
                // Cross guard
                for (int i = -5; i <= 5; i++)
                {
                    DrawPixelSafe(tex, 18+i, 12-i/2, edge);
                }
            });
            return _infinityIcon;
        }

        // Hat icon - deathcap (wizard hat)
        private static Sprite GetHatIcon()
        {
            if (_hatIcon != null) return _hatIcon;
            _hatIcon = CreateIcon(64, (tex, size) => {
                Color hat = new Color(0.35f, 0.25f, 0.5f);
                Color band = new Color(0.7f, 0.6f, 0.3f);
                Color star = new Color(0.9f, 0.85f, 0.4f);

                // Hat cone
                for (int y = 10; y < 50; y++)
                {
                    int width = (y - 10) * 2 / 3 + 2;
                    int startX = 32 - width/2;
                    for (int x = startX; x < startX + width; x++)
                    {
                        DrawPixelSafe(tex, x, y, hat);
                    }
                }
                // Brim
                for (int x = 16; x < 48; x++)
                {
                    DrawPixelSafe(tex, x, 12, hat);
                    DrawPixelSafe(tex, x, 13, hat);
                }
                // Gold band
                for (int x = 24; x < 40; x++)
                {
                    DrawPixelSafe(tex, x, 16, band);
                    DrawPixelSafe(tex, x, 17, band);
                }
                // Star decoration
                DrawPixelSafe(tex, 32, 30, star);
                DrawPixelSafe(tex, 31, 29, star);
                DrawPixelSafe(tex, 33, 29, star);
                DrawPixelSafe(tex, 32, 28, star);
                DrawPixelSafe(tex, 32, 32, star);
            });
            return _hatIcon;
        }

        // Crest token icon - shield with question mark
        private static Sprite GetCrestTokenIcon()
        {
            if (_crestTokenIcon != null) return _crestTokenIcon;
            _crestTokenIcon = CreateIcon(64, (tex, size) => {
                Color shield = new Color(0.5f, 0.35f, 0.7f);
                Color rim = new Color(0.7f, 0.55f, 0.9f);
                Color question = new Color(1f, 0.95f, 0.8f);

                // Shield shape
                for (int y = 10; y < 54; y++)
                {
                    int width = y < 38 ? 28 : 28 - (y - 38);
                    int startX = 32 - width/2;
                    for (int x = startX; x < startX + width; x++)
                    {
                        bool edge = x == startX || x == startX + width - 1 || y == 10;
                        DrawPixelSafe(tex, x, y, edge ? rim : shield);
                    }
                }

                // Question mark
                // Top curve of ?
                for (int x = 26; x < 38; x++)
                {
                    DrawPixelSafe(tex, x, 42, question);
                    DrawPixelSafe(tex, x, 43, question);
                }
                for (int y = 36; y < 43; y++)
                {
                    DrawPixelSafe(tex, 36, y, question);
                    DrawPixelSafe(tex, 37, y, question);
                }
                for (int x = 30; x < 38; x++)
                {
                    DrawPixelSafe(tex, x, 35, question);
                    DrawPixelSafe(tex, x, 36, question);
                }
                // Stem of ?
                for (int y = 26; y < 33; y++)
                {
                    DrawPixelSafe(tex, 32, y, question);
                    DrawPixelSafe(tex, 33, y, question);
                }
                // Dot of ?
                DrawPixelSafe(tex, 32, 20, question);
                DrawPixelSafe(tex, 33, 20, question);
                DrawPixelSafe(tex, 32, 21, question);
                DrawPixelSafe(tex, 33, 21, question);
            });
            return _crestTokenIcon;
        }

        private static Sprite CreateIcon(int size, System.Action<Texture2D, int> drawFunc)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            // Clear to transparent
            Color[] clear = new Color[size * size];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            tex.SetPixels(clear);

            drawFunc(tex, size);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static void DrawPixelSafe(Texture2D tex, int x, int y, Color color)
        {
            if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
            {
                tex.SetPixel(x, y, color);
            }
        }
    }
}
