using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: builds each animal at rest pose (idle, no clip sampled) with its
    // shoulder weapon attached and screenshots it from the front-side, to check the weapon's
    // position relative to the arm/hand before/after retuning ShoulderWeaponHandOffset.
    public static class WeaponHandHeightProbe
    {
        [MenuItem("AnimalBattleRoyale/Debug/Probe Weapon Hand Height")]
        public static void Probe()
        {
            string scratchDir = "/private/tmp/claude-501/-Users-macbookpro-Dev-AnimalBattleRoyale-UnityStarter/42aec13b-9a36-4999-aabb-39584a6059f3/scratchpad";
            Directory.CreateDirectory(scratchDir);

            GameObject light = new GameObject("Light");
            Light lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightComp.intensity = 1.2f;

            GameObject camObj = new GameObject("Cam");
            Camera cam = camObj.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.18f, 0.2f, 0.24f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 55f;

            AnimationClip idleClip = FindClip("Mixamo_Idle.fbx");

            GameObject groundMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            groundMarker.name = "GroundMarker";
            Object.DestroyImmediate(groundMarker.GetComponent<Collider>());
            groundMarker.transform.position = new Vector3(0.6f, 0f, 0f);
            groundMarker.transform.localScale = Vector3.one * 0.15f;
            groundMarker.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = Color.red };

            GameObject headMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headMarker.name = "HeadMarker";
            Object.DestroyImmediate(headMarker.GetComponent<Collider>());
            headMarker.transform.position = new Vector3(0.6f, 2f, 0f);
            headMarker.transform.localScale = Vector3.one * 0.15f;
            headMarker.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = Color.blue };

            foreach (AnimalType type in new[] { AnimalType.Tiger, AnimalType.Monkey, AnimalType.Eagle, AnimalType.Ant, AnimalType.Cow })
            {
                GameObject root = new GameObject("WeaponHandHeightProbeRoot_" + type);
                Transform visualRoot = AnimalVisualFactory.Build(root.transform, type, Color.white, Vector3.one);
                foreach (SkinnedMeshRenderer skinned in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    skinned.forceMatrixRecalculationPerRender = true;

                if (idleClip != null)
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.SampleAnimationClip(visualRoot.gameObject, idleClip, 0f);
                }

                Bounds bounds = new Bounds(visualRoot.position, Vector3.zero);
                bool any = false;
                foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any) { bounds = renderer.bounds; any = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                Debug.Log($"[WeaponHandHeightProbe] {type} bounds center={bounds.center} size={bounds.size}");

                Transform handBone = FindBoneRecursive(visualRoot, "R_Hand") ?? FindBoneRecursive(visualRoot, "bone_14");
                Transform hipBone = FindBoneRecursive(visualRoot, "Hips") ?? FindBoneRecursive(visualRoot, "Hip");
                Transform socket = FindBoneRecursive(visualRoot, "WeaponMuzzle");
                Debug.Log($"[WeaponHandHeightProbe] {type} handBone={(handBone != null ? handBone.position.ToString() : "null")}" +
                          $" hipBone={(hipBone != null ? hipBone.position.ToString() : "null")}" +
                          $" socket={(socket != null ? socket.position.ToString() : "null")}" +
                          $" rootY={visualRoot.position.y}");
                if (handBone != null)
                {
                    Vector3 localDown = handBone.InverseTransformDirection(Vector3.down);
                    Vector3 localForward = handBone.InverseTransformDirection(visualRoot.forward);
                    Debug.Log($"[WeaponHandHeightProbe] {type} handBone localDown={localDown} localForward={localForward}");
                }

                Transform weaponVisual = FindBoneRecursive(visualRoot, "SeedLauncherVisual");
                if (weaponVisual != null)
                {
                    Bounds weaponBounds = new Bounds(weaponVisual.position, Vector3.zero);
                    bool weaponAny = false;
                    foreach (Renderer renderer in weaponVisual.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!weaponAny) { weaponBounds = renderer.bounds; weaponAny = true; }
                        else weaponBounds.Encapsulate(renderer.bounds);
                    }
                    Debug.Log($"[WeaponHandHeightProbe] {type} weaponPivot={weaponVisual.position} weaponBounds center={weaponBounds.center} size={weaponBounds.size} min={weaponBounds.min} max={weaponBounds.max}");
                }

                Vector3 focus = new Vector3(0f, 1f, 0f);
                cam.transform.position = focus + new Vector3(3.4f, 0.15f, 0.03f);
                cam.transform.LookAt(focus);
                Shoot(cam, Path.Combine(scratchDir, $"weapon_hand_height_{type}_side.png"));

                cam.transform.position = focus + new Vector3(0.03f, 0.15f, -3.4f);
                cam.transform.LookAt(focus);
                Shoot(cam, Path.Combine(scratchDir, $"weapon_hand_height_{type}_front.png"));

                if (idleClip != null) AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(root);
            }

            Object.DestroyImmediate(camObj);
            Object.DestroyImmediate(light);
            Object.DestroyImmediate(groundMarker);
            Object.DestroyImmediate(headMarker);
            Debug.Log("[WeaponHandHeightProbe] DoneV3.");
        }

        private static Transform FindBoneRecursive(Transform root, string boneName)
        {
            if (root.name == boneName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindBoneRecursive(root.GetChild(i), boneName);
                if (found != null) return found;
            }
            return null;
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
