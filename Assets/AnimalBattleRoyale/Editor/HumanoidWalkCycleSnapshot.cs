using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: spawns a character exactly like AnimalVisualFactory does at
    // match start, forces the Humanoid Animator into the Walk blend value, manually steps
    // it through several points in the cycle, and photographs each pose.
    public static class HumanoidWalkCycleSnapshot
    {
        [MenuItem("AnimalBattleRoyale/Debug/Capture Tiger Walk Cycle")]
        public static void CaptureTigerWalk() => Capture("Tiger", "Mixamo_Walk.fbx", "walk");

        [MenuItem("AnimalBattleRoyale/Debug/Capture Tiger Run Cycle")]
        public static void CaptureTigerRun() => Capture("Tiger", "Mixamo_Run.fbx", "run");

        [MenuItem("AnimalBattleRoyale/Debug/Capture Tiger Run Cycle From Behind")]
        public static void CaptureTigerRunBehind() => CaptureBehind("Tiger", "Mixamo_Run.fbx", "run_behind");

        [MenuItem("AnimalBattleRoyale/Debug/Capture Tiger Jump Sequence")]
        public static void CaptureTigerJump() => Capture("Tiger", "Mixamo_Jump.fbx", "jump", 6);

        [MenuItem("AnimalBattleRoyale/Debug/Capture Ant Run Cycle")]
        public static void CaptureAntRun() => Capture("Ant", "Mixamo_Run.fbx", "run");

        [MenuItem("AnimalBattleRoyale/Debug/Capture Monkey Run Cycle")]
        public static void CaptureMonkeyRun() => Capture("Monkey", "Mixamo_Run.fbx", "run");

        [MenuItem("AnimalBattleRoyale/Debug/Capture Cow Run Cycle")]
        public static void CaptureCowRun() => Capture("Cow", "Mixamo_Run.fbx", "run");

        [MenuItem("AnimalBattleRoyale/Debug/Capture Eagle Run Cycle")]
        public static void CaptureEagleRun() => Capture("Eagle", "Mixamo_Run.fbx", "run", 6);

        private static void Capture(string animal, string clipFileName, string tag, int frameCount = 4)
        {
            GameObject root = new GameObject("WalkCycleTestRoot");
            Transform visualRoot;
            try
            {
                visualRoot = AnimalVisualFactory.Build(root.transform,
                    (AnimalType)System.Enum.Parse(typeof(AnimalType), animal),
                    Color.white, Vector3.one);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[WalkCycleSnapshot] Build failed: {exception}");
                Object.DestroyImmediate(root);
                return;
            }

            Animator animator = visualRoot.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.enabled)
            {
                Debug.LogError("[WalkCycleSnapshot] No active Humanoid Animator found on the spawned model.");
                Object.DestroyImmediate(root);
                return;
            }

            AnimationClip walkClip = FindClip(clipFileName);
            if (walkClip == null)
            {
                Debug.LogError($"[WalkCycleSnapshot] Could not find clip {clipFileName}.");
                Object.DestroyImmediate(root);
                return;
            }

            GameObject light = new GameObject("SnapshotLight");
            Light lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightComp.intensity = 1.2f;

            GameObject camObj = new GameObject("SnapshotCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.18f, 0.2f, 0.24f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 45f;

            string scratchDir = "/private/tmp/claude-501/-Users-macbookpro-Dev-AnimalBattleRoyale-UnityStarter/42aec13b-9a36-4999-aabb-39584a6059f3/scratchpad";
            Directory.CreateDirectory(scratchDir);

            AnimationMode.StartAnimationMode();
            try
            {
                for (int i = 0; i < frameCount; i++)
                {
                    float normalizedTime = frameCount <= 1 ? 0f : (float)i / (frameCount - 1);
                    AnimationMode.SampleAnimationClip(visualRoot.gameObject, walkClip,
                        normalizedTime * walkClip.length);
                    ShootFrom(cam, new Vector3(3.2f, 1.3f, 0f), new Vector3(0f, 1f, 0f),
                        Path.Combine(scratchDir, $"{animal.ToLowerInvariant()}_{tag}_{i}.png"));
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camObj);
            Object.DestroyImmediate(light);
            Debug.Log($"[WalkCycleSnapshot] {animal} done, {frameCount} frames captured.");
        }

        // Matches the actual gameplay camera framing (behind the character, looking along
        // its forward axis) so we can tell "points forward" from "just looks forward-ish
        // from a diagonal angle" — the earlier side-angle shots were misleading exactly
        // this way.
        private static void CaptureBehind(string animal, string clipFileName, string tag)
        {
            GameObject root = new GameObject("WalkCycleTestRoot");
            Transform visualRoot;
            try
            {
                visualRoot = AnimalVisualFactory.Build(root.transform,
                    (AnimalType)System.Enum.Parse(typeof(AnimalType), animal),
                    Color.white, Vector3.one);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[WalkCycleSnapshot] Build failed: {exception}");
                Object.DestroyImmediate(root);
                return;
            }

            AnimationClip clip = FindClip(clipFileName);
            if (clip == null)
            {
                Debug.LogError($"[WalkCycleSnapshot] Could not find clip {clipFileName}.");
                Object.DestroyImmediate(root);
                return;
            }

            GameObject light = new GameObject("SnapshotLight");
            Light lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightComp.intensity = 1.2f;

            GameObject camObj = new GameObject("SnapshotCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.18f, 0.2f, 0.24f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 45f;

            string scratchDir = "/private/tmp/claude-501/-Users-macbookpro-Dev-AnimalBattleRoyale-UnityStarter/42aec13b-9a36-4999-aabb-39584a6059f3/scratchpad";
            Directory.CreateDirectory(scratchDir);

            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.SampleAnimationClip(visualRoot.gameObject, clip, clip.length * 0.1f);
                ShootFrom(cam, new Vector3(0f, 1.6f, -3.2f), new Vector3(0f, 1f, 0f),
                    Path.Combine(scratchDir, $"{animal.ToLowerInvariant()}_{tag}.png"));
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camObj);
            Object.DestroyImmediate(light);
            Debug.Log($"[WalkCycleSnapshot] {animal} behind-view captured.");
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

        private static void ShootFrom(Camera cam, Vector3 position, Vector3 lookAt, string path)
        {
            cam.transform.position = position;
            cam.transform.LookAt(lookAt);

            RenderTexture rt = new RenderTexture(800, 800, 24);
            cam.targetTexture = rt;
            // A single Render() right after AnimationMode.SampleAnimationClip sometimes
            // captures the skinned mesh's PREVIOUS pose (one sample behind) — rendering twice
            // reliably flushes it.
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
