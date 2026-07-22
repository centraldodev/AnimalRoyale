using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: prints the full bone Transform hierarchy of a character prefab's
    // imported model (parent/child structure, not just flat names) so a Humanoid Avatar bone
    // mapping can be built by hand instead of guessing from a flat name list.
    public static class BoneHierarchyDump
    {
        [MenuItem("AnimalBattleRoyale/Debug/Dump Tiger Bone Hierarchy")]
        public static void DumpTiger() => Dump("Tiger");

        [MenuItem("AnimalBattleRoyale/Debug/Dump Ant Bone Hierarchy")]
        public static void DumpAnt() => Dump("Ant");

        [MenuItem("AnimalBattleRoyale/Debug/Dump Eagle Bone Hierarchy")]
        public static void DumpEagle() => Dump("Eagle");

        [MenuItem("AnimalBattleRoyale/Debug/Dump Monkey Bone Hierarchy")]
        public static void DumpMonkey() => Dump("Monkey");

        [MenuItem("AnimalBattleRoyale/Debug/Dump Cow Bone Hierarchy")]
        public static void DumpCow() => Dump("Cow");

        private static void Dump(string animal)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/AnimalBattleRoyale/Resources/CharacterModels/{animal}/{animal}.prefab");
            if (prefab == null)
            {
                Debug.LogError($"[BoneDump] {animal} prefab not found.");
                return;
            }

            Transform modelRoot = null;
            for (int i = 0; i < prefab.transform.childCount; i++)
            {
                Transform child = prefab.transform.GetChild(i);
                if (child.name.Contains("3DModel") || child.name.Contains("Model"))
                {
                    modelRoot = child;
                    break;
                }
            }
            if (modelRoot == null && prefab.transform.childCount > 0) modelRoot = prefab.transform.GetChild(0);
            if (modelRoot == null)
            {
                Debug.LogError($"[BoneDump] {animal} prefab has no children.");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"[BoneDump] {animal} full hierarchy from '{modelRoot.name}':");
            PrintRecursive(modelRoot, 0, sb);
            Debug.Log(sb.ToString());
        }

        private static void PrintRecursive(Transform t, int depth, System.Text.StringBuilder sb)
        {
            sb.AppendLine(new string(' ', depth * 2) + t.name);
            for (int i = 0; i < t.childCount; i++)
                PrintRecursive(t.GetChild(i), depth + 1, sb);
        }
    }
}
