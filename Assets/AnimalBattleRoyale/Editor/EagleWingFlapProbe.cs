using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: forces AnimalVisualMotion's private wing-flap state (via
    // reflection, since it's normally driven by Time.deltaTime in LateUpdate, which doesn't
    // tick usefully outside Play Mode) to a specific phase and screenshots the result, so the
    // flap's rotation axis/sign can be visually tuned instead of guessed.
    public static class EagleWingFlapProbe
    {
        [MenuItem("AnimalBattleRoyale/Debug/Probe Eagle Wing Flap")]
        public static void Probe()
        {
            GameObject root = new GameObject("WingFlapProbeRoot");
            Transform visualRoot = AnimalVisualFactory.Build(root.transform, AnimalType.Eagle, Color.white, Vector3.one);
            AnimalVisualMotion motion = visualRoot.GetComponent<AnimalVisualMotion>();

            // Manual transform edits outside Play Mode/AnimationMode don't reliably propagate
            // to skinned mesh renders (stale cached bone matrices) — force a fresh recompute.
            foreach (SkinnedMeshRenderer skinned in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skinned.forceMatrixRecalculationPerRender = true;

            System.Type type = typeof(AnimalVisualMotion);
            FieldInfo blendField = type.GetField("wingFlapBlend", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo phaseField = type.GetField("wingFlapPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo isFlyingField = type.GetField("isFlying", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo updateMethod = type.GetMethod("UpdateWingFlap", BindingFlags.NonPublic | BindingFlags.Instance);
            if (blendField == null || phaseField == null || isFlyingField == null || updateMethod == null)
            {
                Debug.LogError("[WingFlapProbe] Reflection lookup failed — field/method names changed?");
                Object.DestroyImmediate(root);
                return;
            }

            FieldInfo leftWingField = type.GetField("leftWing", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo rightWingField = type.GetField("rightWing", BindingFlags.NonPublic | BindingFlags.Instance);
            object leftWingValue = leftWingField?.GetValue(motion);
            object rightWingValue = rightWingField?.GetValue(motion);
            Debug.Log($"[WingFlapProbe] leftWing found={leftWingValue != null}, rightWing found={rightWingValue != null}");

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
            cam.transform.position = new Vector3(0f, 4f, 0.01f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            string scratchDir = "/private/tmp/claude-501/-Users-macbookpro-Dev-AnimalBattleRoyale-UnityStarter/42aec13b-9a36-4999-aabb-39584a6059f3/scratchpad";
            Directory.CreateDirectory(scratchDir);

            // Rest (closed) and mid-flap (near-max open) from directly above, so left/right
            // wing lift is unambiguous regardless of foreshortening from a side angle.
            (float blend, float phase, string tag)[] poses =
            {
                (0f, 0f, "rest"),
                (1f, Mathf.PI, "open"),
            };
            foreach ((float blend, float phase, string tag) in poses)
            {
                blendField.SetValue(motion, blend);
                phaseField.SetValue(motion, phase);
                isFlyingField.SetValue(motion, blend > 0f);
                updateMethod.Invoke(motion, null);

                Transform leftWingTransform = (Transform)leftWingValue;
                Transform rightWingTransform = (Transform)rightWingValue;
                Debug.Log($"[WingFlapProbe] tag={tag} leftWing.localEuler={leftWingTransform.localEulerAngles} " +
                    $"rightWing.localEuler={rightWingTransform.localEulerAngles} " +
                    $"leftWing.worldPos={leftWingTransform.position}");
                Shoot(cam, Path.Combine(scratchDir, $"eagle_wingflap_top_{tag}.png"));

                cam.transform.position = new Vector3(0f, 1.3f, -3.2f);
                cam.transform.LookAt(new Vector3(0f, 1f, 0f));
                Shoot(cam, Path.Combine(scratchDir, $"eagle_wingflap_behind_{tag}.png"));
                cam.transform.position = new Vector3(0f, 4f, 0.01f);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camObj);
            Object.DestroyImmediate(light);
            Debug.Log("[WingFlapProbe] Done.");
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
    }
}
