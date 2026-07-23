using System.Text;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: dumps the full bone hierarchy (name + local position) of each
    // newly-imported animal prefab, to check which follow the standard Tripo biped naming
    // convention (Hip/Waist/Spine01/L_Thigh/...) vs need a bespoke bone map (like Eagle).
    public static class BoneHierarchyDumpV2
    {
        [MenuItem("AnimalBattleRoyale/Debug/Dump New Animal Bone Hierarchies")]
        public static void Dump()
        {
            string[] paths =
            {
                "Assets/AnimalBattleRoyale/Resources/CharacterModels/Tiger/Tiger.prefab",
                "Assets/AnimalBattleRoyale/Resources/CharacterModels/Ant/Ant.prefab",
                "Assets/AnimalBattleRoyale/Resources/CharacterModels/Monkey/Monkey.prefab",
                "Assets/AnimalBattleRoyale/Resources/CharacterModels/Eagle/Eagle.prefab",
            };

            foreach (string path in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[BoneDumpV2] Missing prefab at {path}");
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Transform armature = FindByName(instance.transform, "Armature") ?? FindByName(instance.transform, "Root");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[BoneDumpV2] {path} armature found={armature != null}");
                if (armature != null) DumpRecursive(armature, 0, sb);
                Debug.Log(sb.ToString());
                Object.DestroyImmediate(instance);
            }
            Debug.Log("[BoneDumpV2] Done.");
        }

        private static void DumpRecursive(Transform t, int depth, StringBuilder sb)
        {
            sb.AppendLine(new string(' ', depth * 2) + t.name + "  pos=" + t.localPosition.ToString("0.###"));
            for (int i = 0; i < t.childCount; i++) DumpRecursive(t.GetChild(i), depth + 1, sb);
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
