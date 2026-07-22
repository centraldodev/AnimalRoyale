using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    public static class AnimatorDiagnostic
    {
        [MenuItem("AnimalBattleRoyale/Debug/Diagnose Tiger Animator")]
        public static void Diagnose()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/AnimalBattleRoyale/Resources/CharacterModels/Tiger/Tiger.prefab");
            if (prefab == null)
            {
                Debug.LogError("[AnimatorDiag] Tiger.prefab not found.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
            Debug.Log($"[AnimatorDiag] Found {animators.Length} Animator(s) on instantiated Tiger.prefab.");
            foreach (Animator a in animators)
            {
                Debug.Log($"[AnimatorDiag] Animator on '{a.gameObject.name}': enabled={a.enabled}, " +
                    $"avatar={(a.avatar == null ? "NULL" : a.avatar.name)}, " +
                    $"avatarValid={(a.avatar != null && a.avatar.isValid)}, " +
                    $"avatarIsHuman={(a.avatar != null && a.avatar.isHuman)}");
            }

            // Also directly check the source FBX's importer state.
            string modelPath = "Assets/AnimalBattleRoyale/Art/Characters/Tiger/Models/Tiger3D_Rigged.fbx";
            if (AssetImporter.GetAtPath(modelPath) is ModelImporter importer)
            {
                Debug.Log($"[AnimatorDiag] FBX importer animationType={importer.animationType}");
            }
            Avatar sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelPath);
            Debug.Log($"[AnimatorDiag] Source FBX avatar: {(sourceAvatar == null ? "NULL" : sourceAvatar.name)}, " +
                $"valid={(sourceAvatar != null && sourceAvatar.isValid)}, isHuman={(sourceAvatar != null && sourceAvatar.isHuman)}");

            Object.DestroyImmediate(instance);
        }
    }
}
