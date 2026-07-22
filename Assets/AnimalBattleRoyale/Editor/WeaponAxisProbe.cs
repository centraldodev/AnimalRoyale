using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: spawns the SeedLauncher prefab alone at identity rotation with a
    // bright red marker extending along its local +Z, so we can see which physical end of the
    // mesh (muzzle vs. rear/mount) +Z actually points toward, removing all guesswork about the
    // shoulder weapon's forward-facing rotation.
    public static class WeaponAxisProbe
    {
        [MenuItem("AnimalBattleRoyale/Debug/Probe Weapon Local Axis")]
        public static void Probe()
        {
            GameObject prefab = Resources.Load<GameObject>("Weapons/SeedLauncher");
            if (prefab == null)
            {
                Debug.LogError("[WeaponAxisProbe] Could not load SeedLauncher prefab.");
                return;
            }

            GameObject instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "ForwardMarker";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.localScale = new Vector3(0.03f, 0.03f, 1.2f);
            marker.transform.position = Vector3.forward * 0.6f;
            marker.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = Color.red };

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

            string scratchDir = "/private/tmp/claude-501/-Users-macbookpro-Dev-AnimalBattleRoyale-UnityStarter/42aec13b-9a36-4999-aabb-39584a6059f3/scratchpad";
            Directory.CreateDirectory(scratchDir);

            cam.transform.position = new Vector3(1.5f, 1.2f, -1.5f);
            cam.transform.LookAt(Vector3.zero);
            Shoot(cam, Path.Combine(scratchDir, "weapon_axis_side.png"));

            cam.transform.position = new Vector3(0f, 3f, 0.01f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Shoot(cam, Path.Combine(scratchDir, "weapon_axis_top.png"));

            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(marker);
            Object.DestroyImmediate(camObj);
            Object.DestroyImmediate(light);
            Debug.Log("[WeaponAxisProbe] Done.");
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
    }
}
