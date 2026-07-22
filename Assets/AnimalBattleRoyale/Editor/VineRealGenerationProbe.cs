using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: runs the REAL JungleGenerator.Generate() (not an isolated
    // CreateTree call) and scans the resulting scene for actual VineAnchor components, to see
    // whether vines exist at all in a real map, and if they do, whether they're visually
    // broken (wrong scale/position/disabled renderer/invisible material) versus genuinely
    // never created.
    public static class VineRealGenerationProbe
    {
        [MenuItem("AnimalBattleRoyale/Debug/Probe Real Jungle Vine Generation")]
        public static void Probe()
        {
            GameObject root = new GameObject("VineGenProbeRoot");
            JungleGenerator generator = root.AddComponent<JungleGenerator>();
            generator.Generate(1234);

            VineAnchor[] anchors = Object.FindObjectsByType<VineAnchor>(FindObjectsSortMode.None);
            Debug.Log($"[VineGenProbe] Total VineAnchor components in generated scene: {anchors.Length}");

            GameObject treesRoot = GameObject.Find("Trees");
            int treeChildCount = treesRoot != null ? treesRoot.transform.childCount : -1;
            Debug.Log($"[VineGenProbe] Trees root found={treesRoot != null}, tree count={treeChildCount}");

            for (int i = 0; i < Mathf.Min(anchors.Length, 5); i++)
            {
                VineAnchor anchor = anchors[i];
                Transform vineVisual = anchor.transform.parent != null
                    ? anchor.transform.parent.Find("ClimbableVine")
                    : null;
                Renderer renderer = vineVisual != null ? vineVisual.GetComponentInChildren<Renderer>() : null;
                Debug.Log($"[VineGenProbe] Anchor[{i}] pos={anchor.transform.position} " +
                    $"parent={anchor.transform.parent?.name} vineVisualFound={vineVisual != null} " +
                    $"rendererEnabled={(renderer != null ? renderer.enabled.ToString() : "N/A")} " +
                    $"scale={(vineVisual != null ? vineVisual.localScale.ToString() : "N/A")} " +
                    $"material={(renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "N/A")} " +
                    $"shader={(renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.shader.name : "N/A")}");
            }

            // Also count how many trees have zero vines vs at least one, and tally the actual
            // tree GameObject type names in use (which CreateTree sub-path was taken).
            if (treesRoot != null)
            {
                var typeCounts = new System.Collections.Generic.Dictionary<string, int>();
                int treesWithVines = 0;
                for (int i = 0; i < treesRoot.transform.childCount; i++)
                {
                    Transform tree = treesRoot.transform.GetChild(i);
                    string key = tree.name;
                    typeCounts[key] = typeCounts.TryGetValue(key, out int c) ? c + 1 : 1;
                    if (tree.GetComponentInChildren<VineAnchor>(true) != null) treesWithVines++;
                }
                Debug.Log($"[VineGenProbe] Trees with at least one VineAnchor: {treesWithVines}/{treesRoot.transform.childCount}");
                foreach (var kvp in typeCounts.OrderByDescending(k => k.Value))
                    Debug.Log($"[VineGenProbe] Tree type '{kvp.Key}': {kvp.Value}");
            }

            // Screenshot a cluster of trees, then a tight close-up on one vine's curve.
            if (anchors.Length > 0)
            {
                GameObject light = new GameObject("Light");
                Light lightComp = light.AddComponent<Light>();
                lightComp.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                lightComp.intensity = 1.3f;

                GameObject camObj = new GameObject("Cam");
                Camera cam = camObj.AddComponent<Camera>();
                cam.backgroundColor = new Color(0.5f, 0.7f, 0.9f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.fieldOfView = 60f;

                string scratchDir = "/private/tmp/claude-501/-Users-macbookpro-Dev-AnimalBattleRoyale-UnityStarter/42aec13b-9a36-4999-aabb-39584a6059f3/scratchpad";
                Directory.CreateDirectory(scratchDir);

                Vector3 anchorPos = anchors[0].transform.position;
                cam.transform.position = anchorPos + new Vector3(4f, 2f, 4f);
                cam.transform.LookAt(anchorPos);
                Shoot(cam, Path.Combine(scratchDir, "vine_real_gen_closeup.png"));

                Transform pivot0 = anchors[0].transform.parent;
                Vector3 pivotPos = pivot0 != null ? pivot0.position : anchorPos;
                Vector3 midPoint = (pivotPos + anchorPos) * 0.5f;
                cam.fieldOfView = 35f;
                cam.transform.position = midPoint + new Vector3(1.6f, 0.3f, 1.6f);
                cam.transform.LookAt(midPoint);
                Shoot(cam, Path.Combine(scratchDir, "vine_real_gen_curve.png"));

                cam.fieldOfView = 45f;
                cam.transform.position = pivotPos + new Vector3(2.6f, 0.6f, 2.6f);
                cam.transform.LookAt(pivotPos);
                Shoot(cam, Path.Combine(scratchDir, "vine_real_gen_attachment.png"));

                Object.DestroyImmediate(camObj);
                Object.DestroyImmediate(light);
            }

            Object.DestroyImmediate(root);
            Debug.Log("[VineGenProbe] Done.");
        }

        private static void Shoot(Camera cam, string path)
        {
            RenderTexture rt = new RenderTexture(900, 900, 24);
            cam.targetTexture = rt;
            cam.Render();
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
