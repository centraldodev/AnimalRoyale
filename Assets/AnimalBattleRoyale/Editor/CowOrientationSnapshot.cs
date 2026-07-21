using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: renders a character prefab from a few angles at identity rotation
    // (the same rotation ThirdPersonAnimalController spawns fighters with) so we can see
    // which way the model's front actually faces relative to world +Z (the assumed forward).
    public static class CowOrientationSnapshot
    {
        [MenuItem("AnimalBattleRoyale/Debug/Capture Cow Orientation Snapshot")]
        public static void CaptureCow() => Capture("Cow");

        [MenuItem("AnimalBattleRoyale/Debug/Capture Eagle Orientation Snapshot")]
        public static void CaptureEagle() => Capture("Eagle");

        [MenuItem("AnimalBattleRoyale/Debug/Capture Ant Orientation Snapshot")]
        public static void CaptureAnt() => Capture("Ant");

        [MenuItem("AnimalBattleRoyale/Debug/Capture Tiger Orientation Snapshot")]
        public static void CaptureTiger() => Capture("Tiger");

        [MenuItem("AnimalBattleRoyale/Debug/Capture Monkey Orientation Snapshot")]
        public static void CaptureMonkey() => Capture("Monkey");

        private static void Capture(string animal)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/AnimalBattleRoyale/Resources/CharacterModels/{animal}/{animal}.prefab");
            if (prefab == null)
            {
                Debug.LogError($"[OrientationSnapshot] {animal} prefab not found.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

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
            string prefix = animal.ToLowerInvariant();

            // Front view: camera on the +Z side looking back at the model (-Z), which is
            // where a viewer would stand to see the FACE if the model already faces +Z.
            ShootFrom(cam, new Vector3(0f, 1.2f, 5f), new Vector3(0f, 1f, 0f),
                Path.Combine(scratchDir, $"{prefix}_front.png"));

            // Top-down view with the model's local +Z (assumed forward) pointing "up" the
            // image (toward -Y in screen space / away from camera's bottom edge).
            camObj.transform.position = new Vector3(0f, 8f, 0f);
            camObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            RenderAndSave(cam, Path.Combine(scratchDir, $"{prefix}_top.png"));

            // 3/4 perspective, easiest for a human to read at a glance.
            ShootFrom(cam, new Vector3(3.5f, 2.2f, 3.5f), new Vector3(0f, 1f, 0f),
                Path.Combine(scratchDir, $"{prefix}_threequarter.png"));

            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(camObj);
            Object.DestroyImmediate(light);

            Debug.Log($"[OrientationSnapshot] {animal} done.");
        }

        private static void ShootFrom(Camera cam, Vector3 position, Vector3 lookAt, string path)
        {
            cam.transform.position = position;
            cam.transform.LookAt(lookAt);
            RenderAndSave(cam, path);
        }

        private static void RenderAndSave(Camera cam, string path)
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
    }
}
