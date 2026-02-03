using UnityEngine;
using Crestforge.Data;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Creates different 3D shapes for units based on their class/traits.
    /// Uses medieval-themed styling with toon shaders.
    /// </summary>
    public static class UnitShapeFactory
    {
        /// <summary>
        /// Determine the visual archetype for a unit based on traits
        /// </summary>
        public static UnitArchetype GetArchetype(UnitData template)
        {
            if (template == null || template.traits == null)
                return UnitArchetype.Default;

            foreach (var trait in template.traits)
            {
                if (trait == null) continue;

                string traitName = trait.traitName.ToLower();

                // Tank types
                if (traitName == "tank")
                    return UnitArchetype.Tank;

                // Warrior types
                if (traitName == "warrior")
                    return UnitArchetype.Warrior;

                // Mage types
                if (traitName == "mage")
                    return UnitArchetype.Mage;

                // Assassin types
                if (traitName == "assassin")
                    return UnitArchetype.Assassin;

                // Ranger types
                if (traitName == "ranger")
                    return UnitArchetype.Ranger;

                // Support types
                if (traitName == "support")
                    return UnitArchetype.Support;

                // Beast origin
                if (traitName == "beast")
                    return UnitArchetype.Beast;

                // Berserker
                if (traitName == "berserker")
                    return UnitArchetype.Berserker;

                // Summoner
                if (traitName == "summoner")
                    return UnitArchetype.Summoner;
            }

            return UnitArchetype.Default;
        }

        /// <summary>
        /// Create the body mesh for a unit archetype with medieval styling
        /// </summary>
        public static GameObject CreateBody(UnitArchetype archetype, Transform parent, Color baseColor, float scale, UnitData template = null)
        {
            GameObject body = new GameObject("Body");
            body.transform.SetParent(parent);
            body.transform.localPosition = Vector3.zero;

            // Get color palette based on origin
            ColorPalette palette = template != null
                ? MedievalVisualConfig.GetPaletteForUnit(template)
                : new ColorPalette(baseColor, baseColor, baseColor, Color.black, Color.white);

            // Blend with tier color if we have template
            Color primaryColor = template != null
                ? palette.GetBlendedPrimary(template.cost)
                : baseColor;

            switch (archetype)
            {
                case UnitArchetype.Tank:
                    CreateTankBody(body, palette, primaryColor, scale);
                    break;
                case UnitArchetype.Warrior:
                    CreateWarriorBody(body, palette, primaryColor, scale);
                    break;
                case UnitArchetype.Mage:
                    CreateMageBody(body, palette, primaryColor, scale);
                    break;
                case UnitArchetype.Assassin:
                    CreateAssassinBody(body, palette, primaryColor, scale);
                    break;
                case UnitArchetype.Ranger:
                    CreateRangerBody(body, palette, primaryColor, scale);
                    break;
                case UnitArchetype.Support:
                    CreateSupportBody(body, palette, primaryColor, scale);
                    break;
                case UnitArchetype.Beast:
                    CreateBeastBody(body, palette, primaryColor, scale);
                    break;
                case UnitArchetype.Berserker:
                    CreateBerserkerBody(body, palette, primaryColor, scale);
                    break;
                case UnitArchetype.Summoner:
                    CreateSummonerBody(body, palette, primaryColor, scale);
                    break;
                default:
                    CreateDefaultBody(body, palette, primaryColor, scale);
                    break;
            }

            return body;
        }

        // Legacy overload for compatibility
        public static GameObject CreateBody(UnitArchetype archetype, Transform parent, Color color, float scale)
        {
            return CreateBody(archetype, parent, color, scale, null);
        }

        private static void CreateTankBody(GameObject parent, ColorPalette palette, Color primaryColor, float scale)
        {
            // Heavily armored knight
            GameObject torso = CreatePrimitive(PrimitiveType.Cube, parent.transform, "Torso",
                new Vector3(0, 0.38f * scale, 0),
                new Vector3(0.42f * scale, 0.45f * scale, 0.32f * scale));
            ApplyToonMaterial(torso, primaryColor, palette.Shadow);

            // Pauldrons (shoulder armor)
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject pauldron = CreatePrimitive(PrimitiveType.Sphere, parent.transform, $"Pauldron{(i < 0 ? "L" : "R")}",
                    new Vector3(i * 0.28f * scale, 0.55f * scale, 0),
                    new Vector3(0.2f * scale, 0.18f * scale, 0.2f * scale));
                ApplyMetallicMaterial(pauldron, palette.Secondary);
            }

            // Helmet
            GameObject helmet = CreatePrimitive(PrimitiveType.Cube, parent.transform, "Head",
                new Vector3(0, 0.72f * scale, 0),
                new Vector3(0.26f * scale, 0.28f * scale, 0.24f * scale));
            ApplyMetallicMaterial(helmet, palette.Secondary);

            // Helmet visor
            GameObject visor = CreatePrimitive(PrimitiveType.Cube, parent.transform, "Visor",
                new Vector3(0, 0.68f * scale, 0.1f * scale),
                new Vector3(0.2f * scale, 0.08f * scale, 0.08f * scale));
            ApplyToonMaterial(visor, Color.Lerp(palette.Shadow, Color.black, 0.5f));

            // Tower shield
            GameObject shield = CreatePrimitive(PrimitiveType.Cube, parent.transform, "Shield",
                new Vector3(-0.32f * scale, 0.38f * scale, 0.08f * scale),
                new Vector3(0.08f * scale, 0.5f * scale, 0.35f * scale));
            ApplyMetallicMaterial(shield, Color.Lerp(primaryColor, palette.Secondary, 0.4f));

            // Shield emblem
            GameObject emblem = CreatePrimitive(PrimitiveType.Quad, parent.transform, "Emblem",
                new Vector3(-0.37f * scale, 0.38f * scale, 0.08f * scale),
                new Vector3(0.15f * scale, 0.2f * scale, 1f),
                Quaternion.Euler(0, -90, 0));
            ApplyToonMaterial(emblem, palette.Accent);
        }

        private static void CreateWarriorBody(GameObject parent, ColorPalette palette, Color primaryColor, float scale)
        {
            // Medium armored soldier
            GameObject torso = CreatePrimitive(PrimitiveType.Capsule, parent.transform, "Torso",
                new Vector3(0, 0.4f * scale, 0),
                new Vector3(0.28f * scale, 0.35f * scale, 0.22f * scale));
            ApplyToonMaterial(torso, primaryColor, palette.Shadow);

            // Chainmail visible at waist
            GameObject chainmail = CreatePrimitive(PrimitiveType.Cylinder, parent.transform, "Chainmail",
                new Vector3(0, 0.18f * scale, 0),
                new Vector3(0.3f * scale, 0.08f * scale, 0.3f * scale));
            ApplyMetallicMaterial(chainmail, MedievalVisualConfig.ClassAccents.Warrior);

            // Head with open helm
            GameObject head = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Head",
                new Vector3(0, 0.72f * scale, 0),
                new Vector3(0.22f * scale, 0.24f * scale, 0.22f * scale));
            ApplyToonMaterial(head, Color.Lerp(primaryColor, new Color(0.9f, 0.75f, 0.65f), 0.6f)); // Skin tone

            // Nasal helm
            GameObject helm = CreatePrimitive(PrimitiveType.Cube, parent.transform, "Helm",
                new Vector3(0, 0.82f * scale, 0),
                new Vector3(0.24f * scale, 0.12f * scale, 0.2f * scale));
            ApplyMetallicMaterial(helm, palette.Secondary);

            // Sword
            CreateSword(parent.transform, palette, scale, new Vector3(0.22f * scale, 0.35f * scale, 0));
        }

        private static void CreateMageBody(GameObject parent, ColorPalette palette, Color primaryColor, float scale)
        {
            // Robed spellcaster
            GameObject robe = CreatePrimitive(PrimitiveType.Capsule, parent.transform, "Robe",
                new Vector3(0, 0.38f * scale, 0),
                new Vector3(0.22f * scale, 0.35f * scale, 0.22f * scale));
            ApplyToonMaterial(robe, primaryColor, palette.Shadow);

            // Robe skirt
            GameObject skirt = CreatePrimitive(PrimitiveType.Cylinder, parent.transform, "Skirt",
                new Vector3(0, 0.1f * scale, 0),
                new Vector3(0.32f * scale, 0.1f * scale, 0.32f * scale));
            ApplyToonMaterial(skirt, primaryColor, palette.Shadow);

            // Hood
            GameObject hood = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Hood",
                new Vector3(0, 0.7f * scale, -0.02f * scale),
                new Vector3(0.26f * scale, 0.28f * scale, 0.24f * scale));
            ApplyToonMaterial(hood, Color.Lerp(primaryColor, palette.Shadow, 0.3f));

            // Face (darker inside hood)
            GameObject face = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Head",
                new Vector3(0, 0.68f * scale, 0.04f * scale),
                new Vector3(0.16f * scale, 0.18f * scale, 0.14f * scale));
            ApplyToonMaterial(face, new Color(0.9f, 0.8f, 0.7f)); // Skin

            // Wizard hat
            GameObject hat = CreatePrimitive(PrimitiveType.Cylinder, parent.transform, "Hat",
                new Vector3(0, 0.92f * scale, 0),
                new Vector3(0.12f * scale, 0.18f * scale, 0.12f * scale));
            ApplyToonMaterial(hat, palette.Secondary);

            // Hat brim
            GameObject brim = CreatePrimitive(PrimitiveType.Cylinder, parent.transform, "Brim",
                new Vector3(0, 0.78f * scale, 0),
                new Vector3(0.22f * scale, 0.02f * scale, 0.22f * scale));
            ApplyToonMaterial(brim, palette.Secondary);

            // Magic staff
            CreateMagicStaff(parent.transform, palette, scale);
        }

        private static void CreateAssassinBody(GameObject parent, ColorPalette palette, Color primaryColor, float scale)
        {
            // Sleek, cloaked figure
            GameObject torso = CreatePrimitive(PrimitiveType.Capsule, parent.transform, "Torso",
                new Vector3(0, 0.42f * scale, 0),
                new Vector3(0.18f * scale, 0.35f * scale, 0.14f * scale));
            ApplyToonMaterial(torso, Color.Lerp(primaryColor, Color.black, 0.4f), palette.Shadow);

            // Cloak
            GameObject cloak = CreatePrimitive(PrimitiveType.Cube, parent.transform, "Cloak",
                new Vector3(0, 0.38f * scale, -0.1f * scale),
                new Vector3(0.28f * scale, 0.55f * scale, 0.05f * scale));
            cloak.transform.localRotation = Quaternion.Euler(8, 0, 0);
            ApplyToonMaterial(cloak, Color.Lerp(primaryColor, Color.black, 0.5f));

            // Hood
            GameObject hood = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Hood",
                new Vector3(0, 0.72f * scale, 0),
                new Vector3(0.22f * scale, 0.24f * scale, 0.2f * scale));
            ApplyToonMaterial(hood, Color.Lerp(primaryColor, Color.black, 0.6f));

            // Shadowed face (just eyes glow)
            GameObject face = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Head",
                new Vector3(0, 0.7f * scale, 0.06f * scale),
                new Vector3(0.12f * scale, 0.08f * scale, 0.08f * scale));
            ApplyGlowMaterial(face, palette.Shadow, palette.Glow, 0.5f);

            // Dual daggers
            for (int i = -1; i <= 1; i += 2)
            {
                CreateDagger(parent.transform, palette, scale, i);
            }
        }

        private static void CreateRangerBody(GameObject parent, ColorPalette palette, Color primaryColor, float scale)
        {
            // Medium build with hood
            GameObject torso = CreatePrimitive(PrimitiveType.Capsule, parent.transform, "Torso",
                new Vector3(0, 0.4f * scale, 0),
                new Vector3(0.22f * scale, 0.32f * scale, 0.18f * scale));
            ApplyToonMaterial(torso, primaryColor, palette.Shadow);

            // Leather vest
            GameObject vest = CreatePrimitive(PrimitiveType.Cube, parent.transform, "Vest",
                new Vector3(0, 0.42f * scale, 0),
                new Vector3(0.24f * scale, 0.25f * scale, 0.16f * scale));
            ApplyToonMaterial(vest, Color.Lerp(primaryColor, MedievalVisualConfig.ClassAccents.Ranger, 0.5f));

            // Head
            GameObject head = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Head",
                new Vector3(0, 0.7f * scale, 0),
                new Vector3(0.2f * scale, 0.22f * scale, 0.2f * scale));
            ApplyToonMaterial(head, new Color(0.85f, 0.7f, 0.6f)); // Skin

            // Hood (down)
            GameObject hood = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Hood",
                new Vector3(0, 0.65f * scale, -0.08f * scale),
                new Vector3(0.22f * scale, 0.15f * scale, 0.18f * scale));
            ApplyToonMaterial(hood, MedievalVisualConfig.ClassAccents.Ranger);

            // Quiver
            GameObject quiver = CreatePrimitive(PrimitiveType.Cylinder, parent.transform, "Quiver",
                new Vector3(0.1f * scale, 0.5f * scale, -0.1f * scale),
                new Vector3(0.08f * scale, 0.18f * scale, 0.08f * scale));
            quiver.transform.localRotation = Quaternion.Euler(15, 0, -15);
            ApplyToonMaterial(quiver, new Color(0.5f, 0.35f, 0.2f));

            // Arrows in quiver
            for (int i = 0; i < 3; i++)
            {
                GameObject arrow = CreatePrimitive(PrimitiveType.Cylinder, parent.transform, $"Arrow{i}",
                    new Vector3((0.08f + i * 0.02f) * scale, 0.65f * scale, (-0.08f - i * 0.01f) * scale),
                    new Vector3(0.015f * scale, 0.12f * scale, 0.015f * scale));
                arrow.transform.localRotation = Quaternion.Euler(10, 0, -15 + i * 5);
                ApplyToonMaterial(arrow, new Color(0.4f, 0.3f, 0.2f));
            }

            // Bow
            CreateBow(parent.transform, palette, scale);
        }

        private static void CreateSupportBody(GameObject parent, ColorPalette palette, Color primaryColor, float scale)
        {
            // Priest/healer robes
            GameObject robe = CreatePrimitive(PrimitiveType.Capsule, parent.transform, "Robe",
                new Vector3(0, 0.38f * scale, 0),
                new Vector3(0.26f * scale, 0.35f * scale, 0.26f * scale));
            ApplyToonMaterial(robe, primaryColor, palette.Shadow);

            // Robe bottom
            GameObject skirt = CreatePrimitive(PrimitiveType.Cylinder, parent.transform, "Skirt",
                new Vector3(0, 0.08f * scale, 0),
                new Vector3(0.35f * scale, 0.08f * scale, 0.35f * scale));
            ApplyToonMaterial(skirt, primaryColor);

            // Collar/mantle
            GameObject mantle = CreatePrimitive(PrimitiveType.Cylinder, parent.transform, "Mantle",
                new Vector3(0, 0.6f * scale, 0),
                new Vector3(0.3f * scale, 0.06f * scale, 0.3f * scale));
            ApplyToonMaterial(mantle, MedievalVisualConfig.ClassAccents.Support);

            // Head
            GameObject head = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Head",
                new Vector3(0, 0.72f * scale, 0),
                new Vector3(0.2f * scale, 0.22f * scale, 0.2f * scale));
            ApplyToonMaterial(head, new Color(0.9f, 0.8f, 0.7f)); // Skin

            // Halo
            GameObject halo = CreatePrimitive(PrimitiveType.Cylinder, parent.transform, "Halo",
                new Vector3(0, 0.92f * scale, 0),
                new Vector3(0.18f * scale, 0.012f * scale, 0.18f * scale));
            ApplyGlowMaterial(halo, MedievalVisualConfig.ClassAccents.Support, palette.Glow, 1.5f);

            // Healing staff
            CreateHealingStaff(parent.transform, palette, scale);
        }

        private static void CreateBeastBody(GameObject parent, ColorPalette palette, Color primaryColor, float scale)
        {
            // Four-legged creature
            GameObject body = CreatePrimitive(PrimitiveType.Capsule, parent.transform, "Body",
                new Vector3(0, 0.28f * scale, 0),
                new Vector3(0.22f * scale, 0.22f * scale, 0.38f * scale));
            body.transform.localRotation = Quaternion.Euler(90, 0, 0);
            ApplyToonMaterial(body, primaryColor, palette.Shadow);

            // Head
            GameObject head = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Head",
                new Vector3(0, 0.38f * scale, 0.22f * scale),
                new Vector3(0.2f * scale, 0.18f * scale, 0.22f * scale));
            ApplyToonMaterial(head, Color.Lerp(primaryColor, Color.white, 0.1f));

            // Snout
            GameObject snout = CreatePrimitive(PrimitiveType.Cube, parent.transform, "Snout",
                new Vector3(0, 0.32f * scale, 0.35f * scale),
                new Vector3(0.1f * scale, 0.08f * scale, 0.12f * scale));
            ApplyToonMaterial(snout, Color.Lerp(primaryColor, Color.black, 0.15f));

            // Ears
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject ear = CreatePrimitive(PrimitiveType.Cube, parent.transform, $"Ear{(i < 0 ? "L" : "R")}",
                    new Vector3(i * 0.09f * scale, 0.52f * scale, 0.18f * scale),
                    new Vector3(0.04f * scale, 0.1f * scale, 0.03f * scale));
                ear.transform.localRotation = Quaternion.Euler(-10, 0, i * 15);
                ApplyToonMaterial(ear, primaryColor);
            }

            // Tail
            GameObject tail = CreatePrimitive(PrimitiveType.Capsule, parent.transform, "Tail",
                new Vector3(0, 0.32f * scale, -0.32f * scale),
                new Vector3(0.05f * scale, 0.14f * scale, 0.05f * scale));
            tail.transform.localRotation = Quaternion.Euler(50, 0, 0);
            ApplyToonMaterial(tail, primaryColor);

            // Legs
            float[] legX = { -0.1f, 0.1f, -0.1f, 0.1f };
            float[] legZ = { 0.12f, 0.12f, -0.12f, -0.12f };
            for (int i = 0; i < 4; i++)
            {
                GameObject leg = CreatePrimitive(PrimitiveType.Cylinder, parent.transform, $"Leg{i}",
                    new Vector3(legX[i] * scale, 0.1f * scale, legZ[i] * scale),
                    new Vector3(0.05f * scale, 0.1f * scale, 0.05f * scale));
                ApplyToonMaterial(leg, Color.Lerp(primaryColor, Color.black, 0.1f));
            }

            // Glowing eyes
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject eye = CreatePrimitive(PrimitiveType.Sphere, parent.transform, $"Eye{(i < 0 ? "L" : "R")}",
                    new Vector3(i * 0.06f * scale, 0.4f * scale, 0.3f * scale),
                    new Vector3(0.03f * scale, 0.025f * scale, 0.02f * scale));
                ApplyGlowMaterial(eye, palette.Glow, palette.Glow, 1f);
            }
        }

        private static void CreateBerserkerBody(GameObject parent, ColorPalette palette, Color primaryColor, float scale)
        {
            // Muscular, minimal armor
            GameObject torso = CreatePrimitive(PrimitiveType.Capsule, parent.transform, "Torso",
                new Vector3(0, 0.4f * scale, 0),
                new Vector3(0.32f * scale, 0.38f * scale, 0.25f * scale));
            ApplyToonMaterial(torso, Color.Lerp(primaryColor, new Color(0.85f, 0.7f, 0.6f), 0.4f)); // More skin showing

            // Fur collar/cape
            GameObject fur = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Fur",
                new Vector3(0, 0.6f * scale, -0.05f * scale),
                new Vector3(0.38f * scale, 0.18f * scale, 0.28f * scale));
            ApplyToonMaterial(fur, MedievalVisualConfig.ClassAccents.Berserker);

            // Head
            GameObject head = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Head",
                new Vector3(0, 0.72f * scale, 0),
                new Vector3(0.22f * scale, 0.24f * scale, 0.22f * scale));
            ApplyToonMaterial(head, new Color(0.85f, 0.7f, 0.6f)); // Skin

            // War paint (stripe across face)
            GameObject paint = CreatePrimitive(PrimitiveType.Cube, parent.transform, "Paint",
                new Vector3(0, 0.72f * scale, 0.1f * scale),
                new Vector3(0.18f * scale, 0.04f * scale, 0.02f * scale));
            ApplyToonMaterial(paint, MedievalVisualConfig.ClassAccents.Berserker);

            // Giant axe
            CreateBattleAxe(parent.transform, palette, scale);
        }

        private static void CreateSummonerBody(GameObject parent, ColorPalette palette, Color primaryColor, float scale)
        {
            // Mystical robed figure
            GameObject robe = CreatePrimitive(PrimitiveType.Capsule, parent.transform, "Robe",
                new Vector3(0, 0.35f * scale, 0),
                new Vector3(0.24f * scale, 0.32f * scale, 0.24f * scale));
            ApplyToonMaterial(robe, primaryColor, palette.Shadow);

            // Mystical symbols (floating rings)
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                GameObject ring = CreatePrimitive(PrimitiveType.Cylinder, parent.transform, $"Ring{i}",
                    new Vector3(Mathf.Cos(angle) * 0.25f * scale, 0.4f * scale, Mathf.Sin(angle) * 0.25f * scale),
                    new Vector3(0.08f * scale, 0.005f * scale, 0.08f * scale));
                ring.transform.localRotation = Quaternion.Euler(90, i * 40, 0);
                ApplyGlowMaterial(ring, palette.Accent, palette.Glow, 0.8f);
            }

            // Head with mask
            GameObject head = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Head",
                new Vector3(0, 0.68f * scale, 0),
                new Vector3(0.2f * scale, 0.22f * scale, 0.2f * scale));
            ApplyToonMaterial(head, palette.Secondary);

            // Mask details
            GameObject mask = CreatePrimitive(PrimitiveType.Cube, parent.transform, "Mask",
                new Vector3(0, 0.66f * scale, 0.08f * scale),
                new Vector3(0.14f * scale, 0.1f * scale, 0.05f * scale));
            ApplyGlowMaterial(mask, palette.Accent, palette.Glow, 0.5f);

            // Floating orb
            GameObject orb = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Orb",
                new Vector3(0, 0.95f * scale, 0),
                new Vector3(0.12f * scale, 0.12f * scale, 0.12f * scale));
            ApplyGlowMaterial(orb, palette.Glow, palette.Glow, 1.2f);
        }

        private static void CreateDefaultBody(GameObject parent, ColorPalette palette, Color primaryColor, float scale)
        {
            // Generic humanoid
            GameObject torso = CreatePrimitive(PrimitiveType.Capsule, parent.transform, "Torso",
                new Vector3(0, 0.4f * scale, 0),
                new Vector3(0.2f * scale, 0.32f * scale, 0.18f * scale));
            ApplyToonMaterial(torso, primaryColor, palette.Shadow);

            GameObject head = CreatePrimitive(PrimitiveType.Sphere, parent.transform, "Head",
                new Vector3(0, 0.72f * scale, 0),
                new Vector3(0.2f * scale, 0.22f * scale, 0.2f * scale));
            ApplyToonMaterial(head, Color.Lerp(primaryColor, Color.white, 0.3f));
        }

        // ==========================================
        // WEAPON/ACCESSORY CREATION
        // ==========================================

        private static void CreateSword(Transform parent, ColorPalette palette, float scale, Vector3 offset)
        {
            // Blade
            GameObject blade = CreatePrimitive(PrimitiveType.Cube, parent, "SwordBlade",
                offset + new Vector3(0, 0.25f * scale, 0.08f * scale),
                new Vector3(0.03f * scale, 0.35f * scale, 0.02f * scale));
            ApplyMetallicMaterial(blade, new Color(0.8f, 0.8f, 0.85f));

            // Guard
            GameObject guard = CreatePrimitive(PrimitiveType.Cube, parent, "SwordGuard",
                offset + new Vector3(0, 0.05f * scale, 0.08f * scale),
                new Vector3(0.12f * scale, 0.025f * scale, 0.025f * scale));
            ApplyMetallicMaterial(guard, palette.Secondary);

            // Handle
            GameObject handle = CreatePrimitive(PrimitiveType.Cylinder, parent, "SwordHandle",
                offset + new Vector3(0, -0.05f * scale, 0.08f * scale),
                new Vector3(0.025f * scale, 0.06f * scale, 0.025f * scale));
            ApplyToonMaterial(handle, new Color(0.4f, 0.25f, 0.15f));
        }

        private static void CreateDagger(Transform parent, ColorPalette palette, float scale, int side)
        {
            Vector3 offset = new Vector3(side * 0.16f * scale, 0.3f * scale, 0.1f * scale);

            GameObject blade = CreatePrimitive(PrimitiveType.Cube, parent, $"Dagger{(side < 0 ? "L" : "R")}",
                offset,
                new Vector3(0.02f * scale, 0.18f * scale, 0.015f * scale));
            blade.transform.localRotation = Quaternion.Euler(0, 0, side * -25);
            ApplyMetallicMaterial(blade, new Color(0.7f, 0.7f, 0.75f));
        }

        private static void CreateMagicStaff(Transform parent, ColorPalette palette, float scale)
        {
            // Staff pole
            GameObject staff = CreatePrimitive(PrimitiveType.Cylinder, parent, "Staff",
                new Vector3(0.2f * scale, 0.45f * scale, 0),
                new Vector3(0.035f * scale, 0.5f * scale, 0.035f * scale));
            ApplyToonMaterial(staff, new Color(0.35f, 0.25f, 0.15f));

            // Crystal/orb at top
            GameObject orb = CreatePrimitive(PrimitiveType.Sphere, parent, "StaffOrb",
                new Vector3(0.2f * scale, 0.98f * scale, 0),
                new Vector3(0.1f * scale, 0.1f * scale, 0.1f * scale));
            ApplyGlowMaterial(orb, palette.Glow, palette.Glow, 1f);

            // Decorative wrap
            GameObject wrap = CreatePrimitive(PrimitiveType.Cylinder, parent, "StaffWrap",
                new Vector3(0.2f * scale, 0.85f * scale, 0),
                new Vector3(0.05f * scale, 0.05f * scale, 0.05f * scale));
            ApplyMetallicMaterial(wrap, palette.Secondary);
        }

        private static void CreateHealingStaff(Transform parent, ColorPalette palette, float scale)
        {
            // Staff
            GameObject staff = CreatePrimitive(PrimitiveType.Cylinder, parent, "Staff",
                new Vector3(0.22f * scale, 0.42f * scale, 0),
                new Vector3(0.03f * scale, 0.45f * scale, 0.03f * scale));
            ApplyToonMaterial(staff, new Color(0.9f, 0.85f, 0.75f));

            // Cross top
            GameObject crossV = CreatePrimitive(PrimitiveType.Cube, parent, "CrossV",
                new Vector3(0.22f * scale, 0.92f * scale, 0),
                new Vector3(0.025f * scale, 0.12f * scale, 0.025f * scale));
            ApplyGlowMaterial(crossV, MedievalVisualConfig.ClassAccents.Support, palette.Glow, 0.8f);

            GameObject crossH = CreatePrimitive(PrimitiveType.Cube, parent, "CrossH",
                new Vector3(0.22f * scale, 0.88f * scale, 0),
                new Vector3(0.08f * scale, 0.025f * scale, 0.025f * scale));
            ApplyGlowMaterial(crossH, MedievalVisualConfig.ClassAccents.Support, palette.Glow, 0.8f);
        }

        private static void CreateBow(Transform parent, ColorPalette palette, float scale)
        {
            GameObject bowContainer = new GameObject("Bow");
            bowContainer.transform.SetParent(parent);
            bowContainer.transform.localPosition = new Vector3(-0.2f * scale, 0.4f * scale, 0.05f * scale);
            bowContainer.transform.localRotation = Quaternion.Euler(0, 0, 10);

            // Bow limbs
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject limb = CreatePrimitive(PrimitiveType.Cube, bowContainer.transform, $"Limb{(i < 0 ? "B" : "T")}",
                    new Vector3(0.02f * scale, i * 0.15f * scale, 0),
                    new Vector3(0.025f * scale, 0.18f * scale, 0.015f * scale));
                limb.transform.localRotation = Quaternion.Euler(0, 0, i * -15);
                ApplyToonMaterial(limb, new Color(0.5f, 0.35f, 0.2f));
            }

            // Grip
            GameObject grip = CreatePrimitive(PrimitiveType.Cylinder, bowContainer.transform, "Grip",
                Vector3.zero,
                new Vector3(0.03f * scale, 0.05f * scale, 0.03f * scale));
            ApplyToonMaterial(grip, new Color(0.4f, 0.25f, 0.15f));

            // String
            GameObject bowString = CreatePrimitive(PrimitiveType.Cube, bowContainer.transform, "String",
                new Vector3(0.04f * scale, 0, 0),
                new Vector3(0.008f * scale, 0.32f * scale, 0.008f * scale));
            ApplyToonMaterial(bowString, new Color(0.85f, 0.8f, 0.7f));
        }

        private static void CreateBattleAxe(Transform parent, ColorPalette palette, float scale)
        {
            // Handle
            GameObject handle = CreatePrimitive(PrimitiveType.Cylinder, parent, "AxeHandle",
                new Vector3(-0.25f * scale, 0.5f * scale, 0),
                new Vector3(0.04f * scale, 0.55f * scale, 0.04f * scale));
            handle.transform.localRotation = Quaternion.Euler(0, 0, 20);
            ApplyToonMaterial(handle, new Color(0.4f, 0.28f, 0.18f));

            // Axe head
            GameObject head = CreatePrimitive(PrimitiveType.Cube, parent, "AxeHead",
                new Vector3(-0.38f * scale, 0.85f * scale, 0),
                new Vector3(0.25f * scale, 0.2f * scale, 0.03f * scale));
            head.transform.localRotation = Quaternion.Euler(0, 0, 20);
            ApplyMetallicMaterial(head, new Color(0.6f, 0.6f, 0.65f));
        }

        // ==========================================
        // HELPER METHODS
        // ==========================================

        private static GameObject CreatePrimitive(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            if (rotation.HasValue)
                obj.transform.localRotation = rotation.Value;
            Object.Destroy(obj.GetComponent<Collider>());
            return obj;
        }

        private static void ApplyToonMaterial(GameObject obj, Color mainColor, Color? shadowColor = null)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = MedievalVisualConfig.CreateToonMaterial(mainColor, shadowColor);
            }
        }

        private static void ApplyMetallicMaterial(GameObject obj, Color mainColor)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = MedievalVisualConfig.CreateMetallicMaterial(mainColor);
            }
        }

        private static void ApplyGlowMaterial(GameObject obj, Color mainColor, Color glowColor, float intensity)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = MedievalVisualConfig.CreateGlowMaterial(mainColor, glowColor, intensity);
            }
        }

        /// <summary>
        /// Create a shadow under the unit
        /// </summary>
        public static GameObject CreateShadow(Transform parent, float scale = 1f)
        {
            GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name = "Shadow";
            shadow.transform.SetParent(parent);
            shadow.transform.localPosition = new Vector3(0, 0.01f, 0);
            shadow.transform.localRotation = Quaternion.Euler(90, 0, 0);
            shadow.transform.localScale = new Vector3(0.5f * scale, 0.5f * scale, 1f);
            Object.Destroy(shadow.GetComponent<Collider>());

            // Use URP-compatible unlit shader for shadow
            Shader shadowShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shadowShader == null)
                shadowShader = Shader.Find("Sprites/Default");

            Material shadowMat = new Material(shadowShader);
            shadowMat.SetColor("_BaseColor", new Color(0, 0, 0, 0.35f));
            shadowMat.color = new Color(0, 0, 0, 0.35f); // Fallback for non-URP

            // Enable transparency for URP/Unlit
            shadowMat.SetFloat("_Surface", 1); // 1 = Transparent
            shadowMat.SetFloat("_Blend", 0); // 0 = Alpha
            shadowMat.SetOverrideTag("RenderType", "Transparent");
            shadowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            shadowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            shadowMat.SetInt("_ZWrite", 0);
            shadowMat.renderQueue = 3000;

            shadow.GetComponent<Renderer>().material = shadowMat;
            shadow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shadow.GetComponent<Renderer>().receiveShadows = false;

            return shadow;
        }
    }

    public enum UnitArchetype
    {
        Default,
        Tank,
        Warrior,
        Mage,
        Assassin,
        Ranger,
        Support,
        Beast,
        Berserker,
        Summoner
    }
}
