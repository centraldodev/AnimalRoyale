using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: Eagle's rig uses generic Tripo auto-rig names (tripo::Limb_N,
    // bone_N) instead of L_Thigh/L_Upperarm/etc., so before attempting a Humanoid bone map we
    // need to know which chain is which. Logs each candidate root bone's local position
    // (height/side/depth relative to the model) so legs (low, near feet) can be told apart
    // from wings (mid-height, to the sides) and tail (back, near spine) without guessing.
    public static class EagleBoneIdentityProbe
    {
        [MenuItem("AnimalBattleRoyale/Debug/Probe Eagle Bone Identity")]
        public static void Probe()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/AnimalBattleRoyale/Art/Characters/Eagle/Models/Eagle3D_Rigged.fbx");
            if (source == null)
            {
                Debug.LogError("[EagleBoneProbe] Could not load Eagle model.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            Transform armature = FindByName(instance.transform, "Armature");
            if (armature == null)
            {
                Debug.LogError("[EagleBoneProbe] No Armature found.");
                Object.DestroyImmediate(instance);
                return;
            }

            Transform root = FindByName(armature, "tripo::Root");
            Vector3 origin = root != null ? root.position : instance.transform.position;

            string[] candidates =
            {
                "tripo::0_Left_Limb_0", "tripo::0_Left_Limb_4",
                "tripo::0_Right_Limb_0", "tripo::0_Right_Limb_4",
                "bone_5", "bone_9", "bone_10", "bone_11",
                "bone_12", "bone_18", "bone_20",
                "tripo::Spine_0", "tripo::Spine_1", "tripo::Head_0",
            };

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("[EagleBoneProbe] Bone positions relative to tripo::Root (x=side, y=height, z=front/back):");
            foreach (string name in candidates)
            {
                Transform bone = FindByName(armature, name);
                if (bone == null)
                {
                    sb.AppendLine($"  {name}: NOT FOUND");
                    continue;
                }
                Vector3 rel = bone.position - origin;
                sb.AppendLine($"  {name}: x={rel.x:F3} y={rel.y:F3} z={rel.z:F3}");
            }
            Debug.Log(sb.ToString());

            Object.DestroyImmediate(instance);
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
