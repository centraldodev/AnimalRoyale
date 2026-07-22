using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: confirms the wing-pin fix actually overrides the Run clip's
    // arm-swing on the wing chain when not flying. Samples the Run clip (which drives the
    // wing bones via the Humanoid "arm" mapping) then invokes AnimalVisualMotion's private
    // UpdateWingFlap via reflection with isFlying=false, exactly like a real LateUpdate would
    // during gameplay, and screenshots before/after so the override is visually verifiable.
    public static class EagleWingPinDuringRunProbe
    {
        [MenuItem("AnimalBattleRoyale/Debug/Probe Eagle Wing Pin During Run")]
        public static void Probe()
        {
            GameObject root = new GameObject("WingPinProbeRoot");
            Transform visualRoot = AnimalVisualFactory.Build(root.transform, AnimalType.Eagle, Color.white, Vector3.one);
            AnimalVisualMotion motion = visualRoot.GetComponent<AnimalVisualMotion>();
            foreach (SkinnedMeshRenderer skinned in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skinned.forceMatrixRecalculationPerRender = true;

            AnimationClip runClip = FindClip("Mixamo_Run.fbx");
            if (runClip == null)
            {
                Debug.LogError("[WingPinDuringRun] Could not find Mixamo_Run.fbx clip.");
                Object.DestroyImmediate(root);
                return;
            }

            System.Type type = typeof(AnimalVisualMotion);
            FieldInfo isFlyingField = type.GetField("isFlying", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo blendField = type.GetField("wingFlapBlend", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo updateMethod = type.GetMethod("UpdateWingFlap", BindingFlags.NonPublic | BindingFlags.Instance);
            isFlyingField.SetValue(motion, false);
            blendField.SetValue(motion, 0f);

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

            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.SampleAnimationClip(visualRoot.gameObject, runClip, runClip.length * 0.3f);
                Shoot(cam, Path.Combine(scratchDir, "eagle_wingpin_run_before_fix.png"));

                updateMethod.Invoke(motion, null);
                Shoot(cam, Path.Combine(scratchDir, "eagle_wingpin_run_after_fix.png"));
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camObj);
            Object.DestroyImmediate(light);
            Debug.Log("[WingPinDuringRun] Done.");
        }

        private static AnimationClip FindClip(string fbxFileName)
        {
            string path = "Assets/AnimalBattleRoyale/Art/SharedAnimations/" + fbxFileName;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.Contains("__preview__")) return clip;
            }
            return null;
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
