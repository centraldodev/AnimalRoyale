using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: drives the real Animator (Run state) plus AnimalVisualMotion's
    // own LateUpdate override for several simulated frames, then checks that the right arm's
    // local rotation stayed pinned to rest while the left arm's moved (a light forward/back
    // swing) — confirms the arm-swing removal fix actually takes effect on top of the clip.
    public static class ArmSwingRemovalProbe
    {
        [MenuItem("AnimalBattleRoyale/Debug/Probe Arm Swing Removal")]
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
            cam.fieldOfView = 45f;

            foreach (AnimalType type in new[] { AnimalType.Tiger, AnimalType.Cow })
            {
                GameObject root = new GameObject("ArmSwingProbeRoot_" + type);
                Transform visualRoot = AnimalVisualFactory.Build(root.transform, type, Color.white, Vector3.one);
                AnimalVisualMotion motion = visualRoot.GetComponent<AnimalVisualMotion>();
                Animator animator = visualRoot.GetComponentInChildren<Animator>(true);

                MethodInfo lateUpdateMethod = typeof(AnimalVisualMotion).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo rightUpperarmField = typeof(AnimalVisualMotion).GetField("rightUpperarm", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo rightUpperarmRestField = typeof(AnimalVisualMotion).GetField("rightUpperarmRest", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo leftUpperarmField = typeof(AnimalVisualMotion).GetField("leftUpperarm", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo leftUpperarmRestField = typeof(AnimalVisualMotion).GetField("leftUpperarmRest", BindingFlags.NonPublic | BindingFlags.Instance);

                Transform rightUpperarm = (Transform)rightUpperarmField.GetValue(motion);
                Quaternion rightUpperarmRest = (Quaternion)rightUpperarmRestField.GetValue(motion);
                Transform leftUpperarm = (Transform)leftUpperarmField.GetValue(motion);
                Quaternion leftUpperarmRest = (Quaternion)leftUpperarmRestField.GetValue(motion);

                motion.SetLocomotion(moving: true, sprinting: true, airborne: false);
                animator.SetFloat("Speed", 2f);
                animator.SetBool("Grounded", true);

                float maxRightDelta = 0f;
                float maxLeftDelta = 0f;
                for (int i = 0; i < 90; i++)
                {
                    float dt = 1f / 60f;
                    animator.Update(dt);
                    lateUpdateMethod.Invoke(motion, null);

                    if (rightUpperarm != null)
                    {
                        float rightDelta = Quaternion.Angle(rightUpperarm.localRotation, rightUpperarmRest);
                        maxRightDelta = Mathf.Max(maxRightDelta, rightDelta);
                    }
                    if (leftUpperarm != null)
                    {
                        float leftDelta = Quaternion.Angle(leftUpperarm.localRotation, leftUpperarmRest);
                        maxLeftDelta = Mathf.Max(maxLeftDelta, leftDelta);
                    }

                    if (i == 45)
                    {
                        cam.transform.position = new Vector3(1.6f, 1.0f, -1.6f);
                        cam.transform.LookAt(new Vector3(0f, 0.7f, 0f));
                        Shoot(cam, Path.Combine(scratchDir, $"armswing_{type}_frame45.png"));
                    }
                }

                Debug.Log($"[ArmSwingRemovalProbe] {type} maxRightUpperarmDeltaFromRest={maxRightDelta:0.00} deg, maxLeftUpperarmDeltaFromRest={maxLeftDelta:0.00} deg");

                Object.DestroyImmediate(root);
            }

            Object.DestroyImmediate(camObj);
            Object.DestroyImmediate(light);
            Debug.Log("[ArmSwingRemovalProbe] Done.");
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
