using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    public static class AnimalVisualFactory
    {
        public static Transform Build(Transform parent, AnimalType type, Color mainColor, Vector3 scale)
        {
            Transform existing = parent.Find("VisualRoot");
            if (existing != null)
            {
                if (Application.isPlaying) Object.Destroy(existing.gameObject);
                else Object.DestroyImmediate(existing.gameObject);
            }

            GameObject root = new GameObject("VisualRoot");
            root.transform.SetParent(parent, false);
            root.transform.localScale = scale;

            if (TryBuildImportedModel(root.transform, type))
            {
                root.AddComponent<AnimalVisualMotion>().Initialize(type);
                return root.transform;
            }

            Material main = CreateMaterial(mainColor);
            Material dark = CreateMaterial(Color.Lerp(mainColor, Color.black, 0.55f));
            Material light = CreateMaterial(Color.Lerp(mainColor, Color.white, 0.45f));
            Material white = CreateMaterial(new Color(0.98f, 0.96f, 0.88f));
            Material black = CreateMaterial(new Color(0.025f, 0.018f, 0.012f));
            Material eyeGold = CreateMaterial(new Color(1f, 0.58f, 0.08f));

            switch (type)
            {
                case AnimalType.Ant:
                    BuildAnt(root.transform, main, dark, white, black);
                    break;
                case AnimalType.Monkey:
                    BuildMonkey(root.transform, main, light, dark, white, black, eyeGold);
                    break;
                case AnimalType.Tiger:
                    BuildTiger(root.transform, main, light, dark, white, black, eyeGold);
                    break;
                case AnimalType.Eagle:
                    BuildEagle(root.transform, main, light, dark, white, black, eyeGold);
                    break;
            }

            root.AddComponent<AnimalVisualMotion>().Initialize(type);

            return root.transform;
        }

        private static bool TryBuildImportedModel(Transform root, AnimalType type)
        {
            GameObject prefab = Resources.Load<GameObject>($"CharacterModels/{type}/{type}");
            if (prefab == null) return false;

            GameObject instance = Object.Instantiate(prefab, root, false);
            instance.name = type + "Model";
            // CharacterController is correctly grounded at the feet; the imported mesh
            // needs a small visual lift so grass does not cover its paws and body.
            instance.transform.localPosition = Vector3.up * 0.28f;
            // The source rigs were authored in Blender (Z-up) while the gameplay uses
            // Unity's Y-up coordinates. Keep this correction on the prefab instance so
            // the original FBX and its animation clips remain untouched.
            // Blender-to-Unity axis conversion plus a 180° X correction: this places
            // every character's feet below its body instead of pointing upward.
            instance.transform.localRotation = Quaternion.Euler(90f, 180f, 180f);
            return true;
        }

        private static void BuildAnt(Transform root, Material main, Material dark, Material white, Material black)
        {
            CreatePart(root, PrimitiveType.Sphere, "Abdomen", new Vector3(0f, 0.45f, -0.35f), new Vector3(0.7f, 0.55f, 0.9f), main);
            CreatePart(root, PrimitiveType.Sphere, "Thorax", new Vector3(0f, 0.48f, 0.2f), new Vector3(0.55f, 0.5f, 0.55f), main);
            CreatePart(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.5f, 0.65f), new Vector3(0.5f, 0.45f, 0.5f), dark);

            for (int i = -1; i <= 1; i++)
            {
                float z = i * 0.32f + 0.15f;
                CreateLimb(root, new Vector3(-0.42f, 0.28f, z), new Vector3(-0.72f, 0.08f, z + i * 0.12f), 0.07f, dark);
                CreateLimb(root, new Vector3(0.42f, 0.28f, z), new Vector3(0.72f, 0.08f, z + i * 0.12f), 0.07f, dark);
            }

            CreateLimb(root, new Vector3(-0.18f, 0.72f, 0.82f), new Vector3(-0.42f, 0.98f, 1.02f), 0.035f, dark);
            CreateLimb(root, new Vector3(0.18f, 0.72f, 0.82f), new Vector3(0.42f, 0.98f, 1.02f), 0.035f, dark);
            CreateEyePair(root, new Vector3(0f, 0.62f, 1.0f), 0.16f, white, black, 0.16f);
            CreatePart(root, PrimitiveType.Sphere, "MandibleLeft", new Vector3(-0.2f, 0.42f, 1.03f), new Vector3(0.2f, 0.1f, 0.2f), dark);
            CreatePart(root, PrimitiveType.Sphere, "MandibleRight", new Vector3(0.2f, 0.42f, 1.03f), new Vector3(0.2f, 0.1f, 0.2f), dark);
        }

        private static void BuildMonkey(Transform root, Material main, Material light, Material dark, Material white, Material black, Material eyeGold)
        {
            CreatePart(root, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.82f, 0f), new Vector3(0.65f, 0.85f, 0.48f), main);
            CreatePart(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.5f, 0.12f), new Vector3(0.58f, 0.58f, 0.55f), main);
            CreatePart(root, PrimitiveType.Sphere, "Face", new Vector3(0f, 1.43f, 0.46f), new Vector3(0.38f, 0.33f, 0.24f), light);
            CreateLimb(root, new Vector3(-0.28f, 1.12f, 0f), new Vector3(-0.62f, 0.45f, 0.05f), 0.13f, dark);
            CreateLimb(root, new Vector3(0.28f, 1.12f, 0f), new Vector3(0.62f, 0.45f, 0.05f), 0.13f, dark);
            CreateLimb(root, new Vector3(-0.2f, 0.55f, 0f), new Vector3(-0.32f, 0.05f, 0.08f), 0.15f, dark);
            CreateLimb(root, new Vector3(0.2f, 0.55f, 0f), new Vector3(0.32f, 0.05f, 0.08f), 0.15f, dark);
            CreateLimb(root, new Vector3(0f, 0.75f, -0.25f), new Vector3(0.55f, 0.95f, -0.85f), 0.07f, dark);
            CreatePart(root, PrimitiveType.Sphere, "LeftEar", new Vector3(-0.48f, 1.62f, 0.1f), new Vector3(0.25f, 0.28f, 0.18f), dark);
            CreatePart(root, PrimitiveType.Sphere, "RightEar", new Vector3(0.48f, 1.62f, 0.1f), new Vector3(0.25f, 0.28f, 0.18f), dark);
            CreateEyePair(root, new Vector3(0f, 1.59f, 0.57f), 0.19f, white, black, 0.18f, eyeGold);
            CreatePart(root, PrimitiveType.Sphere, "Nose", new Vector3(0f, 1.36f, 0.71f), new Vector3(0.14f, 0.1f, 0.08f), black);
        }

        private static void BuildTiger(Transform root, Material main, Material light, Material dark, Material white, Material black, Material eyeGold)
        {
            CreatePart(root, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.72f, 0f), new Vector3(0.72f, 0.85f, 1.08f), main, new Vector3(90f, 0f, 0f));
            CreatePart(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.87f, 0.92f), new Vector3(0.65f, 0.58f, 0.65f), main);
            CreatePart(root, PrimitiveType.Sphere, "Muzzle", new Vector3(0f, 0.72f, 1.28f), new Vector3(0.42f, 0.3f, 0.3f), light);

            float[] xs = { -0.34f, 0.34f };
            float[] zs = { -0.55f, 0.55f };
            foreach (float x in xs)
            foreach (float z in zs)
            {
                CreateLimb(root, new Vector3(x, 0.6f, z), new Vector3(x, 0.08f, z + 0.06f), 0.16f, main);
            }

            CreateLimb(root, new Vector3(0f, 0.75f, -0.9f), new Vector3(0.35f, 0.85f, -1.65f), 0.1f, main);

            for (int i = -2; i <= 2; i++)
            {
                CreatePart(root, PrimitiveType.Cube, "Stripe", new Vector3(0f, 0.92f, i * 0.28f), new Vector3(0.78f, 0.06f, 0.09f), dark, new Vector3(0f, 0f, 8f * i));
            }
            CreatePart(root, PrimitiveType.Sphere, "LeftEar", new Vector3(-0.42f, 1.3f, 0.87f), new Vector3(0.24f, 0.24f, 0.18f), dark);
            CreatePart(root, PrimitiveType.Sphere, "RightEar", new Vector3(0.42f, 1.3f, 0.87f), new Vector3(0.24f, 0.24f, 0.18f), dark);
            CreateEyePair(root, new Vector3(0f, 1.02f, 1.42f), 0.16f, white, black, 0.16f, eyeGold);
            CreatePart(root, PrimitiveType.Sphere, "TigerNose", new Vector3(0f, 0.78f, 1.56f), new Vector3(0.16f, 0.1f, 0.1f), black);
        }

        private static void BuildEagle(Transform root, Material main, Material light, Material dark, Material white, Material black, Material eyeGold)
        {
            CreatePart(root, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.7f, 0f), new Vector3(0.55f, 0.75f, 0.5f), main, new Vector3(90f, 0f, 0f));
            CreatePart(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.02f, 0.48f), new Vector3(0.48f, 0.44f, 0.48f), light);
            CreatePart(root, PrimitiveType.Cylinder, "Beak", new Vector3(0f, 0.96f, 0.88f), new Vector3(0.12f, 0.28f, 0.12f), dark, new Vector3(90f, 0f, 0f));
            CreatePart(root, PrimitiveType.Cube, "LeftWing", new Vector3(-0.68f, 0.73f, -0.05f), new Vector3(1.15f, 0.1f, 0.55f), main, new Vector3(0f, 0f, -15f));
            CreatePart(root, PrimitiveType.Cube, "RightWing", new Vector3(0.68f, 0.73f, -0.05f), new Vector3(1.15f, 0.1f, 0.55f), main, new Vector3(0f, 0f, 15f));
            CreatePart(root, PrimitiveType.Cube, "Tail", new Vector3(0f, 0.68f, -0.68f), new Vector3(0.52f, 0.1f, 0.72f), dark);
            CreateEyePair(root, new Vector3(0f, 1.15f, 0.77f), 0.13f, white, black, 0.14f, eyeGold);
            CreatePart(root, PrimitiveType.Capsule, "LeftTalon", new Vector3(-0.16f, 0.28f, 0.12f), new Vector3(0.08f, 0.25f, 0.08f), dark);
            CreatePart(root, PrimitiveType.Capsule, "RightTalon", new Vector3(0.16f, 0.28f, 0.12f), new Vector3(0.08f, 0.25f, 0.08f), dark);
        }

        private static void CreateEyePair(Transform root, Vector3 center, float size, Material white, Material black, float pupilScale, Material iris = null)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * size * 0.76f;
                CreatePart(root, PrimitiveType.Sphere, "EyeWhite", center + new Vector3(x, 0f, 0f), new Vector3(size, size, size * 0.5f), white);
                if (iris != null)
                {
                    CreatePart(root, PrimitiveType.Sphere, "Iris", center + new Vector3(x, 0f, size * 0.38f), new Vector3(size * 0.64f, size * 0.64f, size * 0.18f), iris);
                }
                CreatePart(root, PrimitiveType.Sphere, "Pupil", center + new Vector3(x, 0f, size * 0.52f), new Vector3(pupilScale * 0.52f, pupilScale * 0.72f, pupilScale * 0.18f), black);
            }
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType primitive,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Vector3? localEuler = null)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);

            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localEulerAngles = localEuler ?? Vector3.zero;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }

            return part;
        }

        private static void CreateLimb(Transform parent, Vector3 start, Vector3 end, float width, Material material)
        {
            Vector3 direction = end - start;
            GameObject limb = CreatePart(parent, PrimitiveType.Cylinder, "Limb", (start + end) * 0.5f,
                new Vector3(width, direction.magnitude * 0.5f, width), material);
            limb.transform.up = direction.normalized;
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            material.enableInstancing = true;
            return material;
        }

    }
}
