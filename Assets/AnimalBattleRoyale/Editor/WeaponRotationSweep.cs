using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: the shoulder weapon's forward orientation was wrong (pointing off
    // to the side instead of forward) even with identity rotation relative to the model root.
    // This sweeps a set of candidate Y rotations and photographs each from directly behind the
    // character (matching the real gameplay camera), so the correct offset can be read off
    // visually instead of guessed.
    public static class WeaponRotationSweep
    {
        [MenuItem("AnimalBattleRoyale/Debug/Sweep Tiger Weapon Rotation")]
        public static void Sweep()
        {
            string scratchDir = "/private/tmp/claude-501/-Users-macbookpro-Dev-AnimalBattleRoyale-UnityStarter/42aec13b-9a36-4999-aabb-39584a6059f3/scratchpad";
            Directory.CreateDirectory(scratchDir);

            float[] yAngles = { 0f, 90f, 180f, 270f };
            foreach (float yAngle in yAngles)
            {
                GameObject root = new GameObject("WeaponSweepRoot");
                Transform visualRoot = AnimalVisualFactory.Build(root.transform, AnimalType.Tiger, Color.white, Vector3.one);

                Transform socket = FindByName(visualRoot, "ShoulderWeaponSocket");
                ShoulderWeaponFixedForward fixedForward = socket.GetComponent<ShoulderWeaponFixedForward>();
                Transform modelRoot = visualRoot.Find("Tiger_PackageModel");
                fixedForward.Initialize(modelRoot, Quaternion.Euler(0f, yAngle, 0f));

                // Ground-truth gizmos: red = character's own forward (modelRoot.forward),
                // blue = the weapon socket's current forward after the fix is applied. If the
                // fix is correct these two lines should be parallel.
                CreateMarker(root.transform, modelRoot.position + Vector3.up * 1f, modelRoot.forward, Color.red);
                CreateMarker(root.transform, socket.position, socket.forward, Color.blue);

                GameObject light = new GameObject("SweepLight");
                Light lightComp = light.AddComponent<Light>();
                lightComp.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                lightComp.intensity = 1.2f;

                GameObject camObj = new GameObject("SweepCamera");
                Camera cam = camObj.AddComponent<Camera>();
                cam.backgroundColor = new Color(0.18f, 0.2f, 0.24f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.fieldOfView = 45f;

                cam.transform.position = new Vector3(0f, 1.6f, -3.2f);
                cam.transform.LookAt(new Vector3(0f, 1f, 0f));
                Shoot(cam, Path.Combine(scratchDir, $"tiger_weapon_y{(int)yAngle}_behind.png"));

                // Top-down view: character forward (+Z) reads as "up" in this image, so the
                // barrel's true direction (forward vs. sideways) is unambiguous.
                cam.transform.position = new Vector3(0f, 4f, 0.2f);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Shoot(cam, Path.Combine(scratchDir, $"tiger_weapon_y{(int)yAngle}_top.png"));

                Object.DestroyImmediate(root);
                Object.DestroyImmediate(camObj);
                Object.DestroyImmediate(light);
            }

            Debug.Log($"[WeaponRotationSweep] Captured {yAngles.Length} candidate angles.");
        }

        private static void CreateMarker(Transform parent, Vector3 origin, Vector3 direction, Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(parent, true);
            marker.transform.localScale = new Vector3(0.03f, 0.03f, 1.5f);
            marker.transform.position = origin + direction.normalized * 0.75f;
            marker.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            marker.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = color };
        }

        private static void Shoot(Camera cam, string path)
        {
            RenderTexture rt = new RenderTexture(800, 800, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(800, 800, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindByName(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
