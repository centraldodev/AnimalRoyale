using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: dumps the arm bone chain names/hierarchy for each animal so the
    // exact forearm/hand bone names can be confirmed before wiring up arm-swing overrides.
    public static class ArmBoneDumpProbe
    {
        [MenuItem("AnimalBattleRoyale/Debug/Dump Arm Bone Names")]
        public static void Dump()
        {
            foreach (AnimalType type in new[] { AnimalType.Tiger, AnimalType.Ant, AnimalType.Monkey, AnimalType.Cow, AnimalType.Eagle })
            {
                GameObject root = new GameObject("ArmBoneDumpRoot_" + type);
                Transform visualRoot = AnimalVisualFactory.Build(root.transform, type, Color.white, Vector3.one);

                Transform rHand = FindBone(visualRoot, "R_Hand");
                Transform rUpper = FindBone(visualRoot, "R_Upperarm");
                Debug.Log($"[ArmBoneDump] {type} R_Upperarm found={rUpper != null} R_Hand found={rHand != null}");

                if (rUpper != null)
                {
                    string chain = DumpChain(rUpper, rHand, 0);
                    Debug.Log($"[ArmBoneDump] {type} chain from R_Upperarm to R_Hand:\n{chain}");
                }

                Object.DestroyImmediate(root);
            }
            Debug.Log("[ArmBoneDump] Done.");
        }

        private static string DumpChain(Transform node, Transform stopAtInclusive, int depth)
        {
            string line = new string(' ', depth * 2) + node.name + "\n";
            if (node == stopAtInclusive) return line;
            for (int i = 0; i < node.childCount; i++)
            {
                Transform child = node.GetChild(i);
                // Only descend into children whose subtree actually contains the hand, to avoid
                // dumping unrelated finger/prop branches.
                if (stopAtInclusive != null && !IsAncestorOf(child, stopAtInclusive) && child != stopAtInclusive) continue;
                line += DumpChain(child, stopAtInclusive, depth + 1);
            }
            return line;
        }

        private static bool IsAncestorOf(Transform potentialAncestor, Transform node)
        {
            Transform t = node;
            while (t != null)
            {
                if (t == potentialAncestor) return true;
                t = t.parent;
            }
            return false;
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            if (root.name == boneName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindBone(root.GetChild(i), boneName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
