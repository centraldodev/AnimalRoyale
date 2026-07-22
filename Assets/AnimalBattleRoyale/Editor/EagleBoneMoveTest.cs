using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: the wing-flap rotation on bone_5/bone_12 produced zero visible
    // change in renders despite the transform data confirming the rotation was applied —
    // meaning those bones likely don't control the wing mesh at all. This directly rotates
    // one candidate bone at a time by a large, obvious angle and screenshots the result, so
    // which named bone actually IS the wing can be read off visually instead of inferred.
    public static class EagleBoneMoveTest
    {
        [MenuItem("AnimalBattleRoyale/Debug/Test Move Eagle Candidate Bones")]
        public static void TestMove()
        {
            string[] candidates = { "tripo::0_Left_Limb_0", "bone_5", "bone_18" };
            string scratchDir = "/private/tmp/claude-501/-Users-macbookpro-Dev-AnimalBattleRoyale-UnityStarter/42aec13b-9a36-4999-aabb-39584a6059f3/scratchpad";
            Directory.CreateDirectory(scratchDir);

            foreach (string candidate in candidates)
            {
                GameObject root = new GameObject("BoneMoveTestRoot");
                Transform visualRoot = AnimalVisualFactory.Build(root.transform, AnimalType.Eagle, Color.white, Vector3.one);
                Transform bone = FindByName(visualRoot, candidate);

                // Manual transform edits outside Play Mode/AnimationMode don't reliably
                // propagate to skinned mesh renders (stale cached bone matrices) — this forces
                // a fresh recompute every render instead of trusting a cache.
                foreach (SkinnedMeshRenderer skinned in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    skinned.forceMatrixRecalculationPerRender = true;

                GameObject light = new GameObject("Light");
                Light lightComp = light.AddComponent<Light>();
                lightComp.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                lightComp.intensity = 1.2f;

                GameObject camObj = new GameObject("Cam");
                Camera cam = camObj.AddComponent<Camera>();
                cam.backgroundColor = new Color(0.18f, 0.2f, 0.24f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.fieldOfView = 45f;
                cam.transform.position = new Vector3(0f, 1.3f, -3.2f);
                cam.transform.LookAt(new Vector3(0f, 1f, 0f));

                string safeName = candidate.Replace("tripo::", "").Replace("0_", "");
                Shoot(cam, Path.Combine(scratchDir, $"eagle_bonetest_{safeName}_before.png"));

                if (bone != null)
                {
                    bone.localRotation = bone.localRotation * Quaternion.Euler(90f, 0f, 0f);
                    Shoot(cam, Path.Combine(scratchDir, $"eagle_bonetest_{safeName}_after.png"));
                    Debug.Log($"[BoneMoveTest] {candidate}: rotated 90 on X, screenshots saved.");
                }
                else
                {
                    Debug.LogWarning($"[BoneMoveTest] {candidate}: not found.");
                }

                Object.DestroyImmediate(root);
                Object.DestroyImmediate(camObj);
                Object.DestroyImmediate(light);
            }
        }

        private static void Shoot(Camera cam, string path)
        {
            RenderTexture rt = new RenderTexture(800, 800, 24);
            cam.targetTexture = rt;
            cam.Render();
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
