using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: builds a single procedural tree exactly like JungleGenerator
    // does at match start (same private CreateTree method via reflection, same colors) and
    // photographs it close-up, to check whether the vine is actually present/visible without
    // needing to generate/play a full match.
    public static class TreeVineSnapshot
    {
        [MenuItem("AnimalBattleRoyale/Debug/Capture Tree Vine Snapshot")]
        public static void Capture()
        {
            Material trunkMaterial = CreateMaterial(new Color(0.31f, 0.12f, 0.035f));
            Material leafMaterial = CreateMaterial(new Color(0.035f, 0.39f, 0.075f));
            Material vineMaterial = CreateMaterial(new Color(0.24f, 0.56f, 0.1f));
            Material fruitRedMaterial = CreateMaterial(new Color(0.95f, 0.12f, 0.06f));
            Material fruitGoldMaterial = CreateMaterial(new Color(1f, 0.58f, 0.06f));

            GameObject root = new GameObject("VineSnapshotRoot");

            MethodInfo createTree = typeof(JungleGenerator).GetMethod("CreateTree",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (createTree == null)
            {
                Debug.LogError("[TreeVineSnapshot] Could not find JungleGenerator.CreateTree via reflection.");
                Object.DestroyImmediate(root);
                return;
            }

            Random.InitState(1234);
            createTree.Invoke(null, new object[]
            {
                root.transform, Vector3.zero, trunkMaterial, leafMaterial, vineMaterial,
                fruitRedMaterial, fruitGoldMaterial, null
            });

            GameObject light = new GameObject("SnapshotLight");
            Light lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            lightComp.intensity = 1.3f;

            GameObject camObj = new GameObject("SnapshotCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.5f, 0.7f, 0.9f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 50f;

            string scratchDir = "/private/tmp/claude-501/-Users-macbookpro-Dev-AnimalBattleRoyale-UnityStarter/42aec13b-9a36-4999-aabb-39584a6059f3/scratchpad";
            Directory.CreateDirectory(scratchDir);

            // Full tree, wide shot.
            ShootFrom(cam, new Vector3(8f, 5f, 8f), new Vector3(0f, 3f, 0f),
                Path.Combine(scratchDir, "tree_full.png"));

            // Close on the branch area where the vine should hang.
            ShootFrom(cam, new Vector3(3f, 4.5f, 3f), new Vector3(0.5f, 3f, 0.5f),
                Path.Combine(scratchDir, "tree_closeup.png"));

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camObj);
            Object.DestroyImmediate(light);

            Debug.Log("[TreeVineSnapshot] Done.");
        }

        private static Material CreateMaterial(Color color)
        {
            Material material = new Material(ShaderLibrary.Lit) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }

        private static void ShootFrom(Camera cam, Vector3 position, Vector3 lookAt, string path)
        {
            cam.transform.position = position;
            cam.transform.LookAt(lookAt);

            RenderTexture rt = new RenderTexture(900, 900, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
