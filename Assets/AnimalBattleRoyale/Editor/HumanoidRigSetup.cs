using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary tool: configures a Tripo animal FBX (custom bone names, Generic rig) as a
    // Unity Humanoid avatar by hand-mapping our bone names to HumanBodyBones, so Mixamo
    // clips can be retargeted onto it. Built from the hierarchy printed by BoneHierarchyDump.
    public static class HumanoidRigSetup
    {
        private static readonly Dictionary<string, string> BoneMap = new Dictionary<string, string>
        {
            { "Hips", "Hip" },
            { "Spine", "Waist" },
            { "Chest", "Spine01" },
            { "UpperChest", "Spine02" },
            { "Neck", "NeckTwist01" },
            { "Head", "Head" },
            { "LeftShoulder", "L_Clavicle" },
            { "LeftUpperArm", "L_Upperarm" },
            { "LeftLowerArm", "L_Forearm" },
            { "LeftHand", "L_Hand" },
            { "RightShoulder", "R_Clavicle" },
            { "RightUpperArm", "R_Upperarm" },
            { "RightLowerArm", "R_Forearm" },
            { "RightHand", "R_Hand" },
            { "LeftUpperLeg", "L_Thigh" },
            { "LeftLowerLeg", "L_Calf" },
            { "LeftFoot", "L_Foot" },
            { "LeftToes", "L_ToeBase" },
            { "RightUpperLeg", "R_Thigh" },
            { "RightLowerLeg", "R_Calf" },
            { "RightFoot", "R_Foot" },
            { "RightToes", "R_ToeBase" },
            { "LeftUpperLegTwist", "L_ThighTwist01" },
            { "LeftLowerLegTwist", "L_CalfTwist01" },
            { "RightUpperLegTwist", "R_ThighTwist01" },
            { "RightLowerLegTwist", "R_CalfTwist01" },
            { "LeftUpperArmTwist", "L_UpperarmTwist01" },
            { "LeftLowerArmTwist", "L_ForearmTwist01" },
            { "RightUpperArmTwist", "R_UpperarmTwist01" },
            { "RightLowerArmTwist", "R_ForearmTwist01" },
        };

        [MenuItem("AnimalBattleRoyale/Debug/Configure Tiger As Humanoid")]
        public static void ConfigureTiger() =>
            Configure("Tiger", "Assets/AnimalBattleRoyale/Art/Characters/Tiger/Models/Tiger3D_Rigged.fbx");

        private static void Configure(string animal, string modelPath)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
            {
                Debug.LogError($"[HumanoidRigSetup] Could not load {modelPath}");
                return;
            }

            // Work on a temporary instance so we can bend the arms into a T-pose purely for
            // avatar calibration, without touching the asset's real rest pose (used by every
            // other rendering/procedural-animation path).
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            Transform armature = FindByName(instance.transform, "Armature") ?? FindByName(instance.transform, "Root");
            if (armature == null)
            {
                Debug.LogError($"[HumanoidRigSetup] {animal}: could not find an 'Armature'/'Root' bone.");
                Object.DestroyImmediate(instance);
                return;
            }

            EnforceArmTPose(armature, "L_Clavicle", "L_Upperarm", "L_Forearm", "L_Hand");
            EnforceArmTPose(armature, "R_Clavicle", "R_Upperarm", "R_Forearm", "R_Hand");

            List<SkeletonBone> skeleton = new List<SkeletonBone>();
            CollectSkeleton(instance.transform, skeleton);
            // SkeletonBone.name must match the ASSET's root name, not the temp instance's
            // (Unity suffixes instantiated names with "(Clone)" etc. in some cases).
            skeleton[0] = new SkeletonBone { name = source.name, position = skeleton[0].position,
                rotation = skeleton[0].rotation, scale = skeleton[0].scale };

            List<HumanBone> humanBones = new List<HumanBone>();
            foreach (KeyValuePair<string, string> pair in BoneMap)
            {
                Transform bone = FindByName(armature, pair.Value);
                if (bone == null)
                {
                    Debug.LogWarning($"[HumanoidRigSetup] {animal}: bone '{pair.Value}' not found, skipping {pair.Key}.");
                    continue;
                }
                humanBones.Add(new HumanBone
                {
                    humanName = pair.Key,
                    boneName = bone.name,
                    limit = new HumanLimit { useDefaultValues = true }
                });
            }

            HumanDescription description = new HumanDescription
            {
                human = humanBones.ToArray(),
                skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };

            Object.DestroyImmediate(instance);

            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
            {
                Debug.LogError($"[HumanoidRigSetup] No ModelImporter at {modelPath}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = description;
            importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelPath);
            if (avatar != null && avatar.isValid && avatar.isHuman)
                Debug.Log($"[HumanoidRigSetup] {animal}: Humanoid avatar configured successfully ({humanBones.Count} bones mapped).");
            else
                Debug.LogError($"[HumanoidRigSetup] {animal}: Avatar is " +
                    $"{(avatar == null ? "null" : avatar.isValid ? "valid but not human" : "INVALID")}. Check Console for Unity's own avatar errors.");
        }

        // Bends one arm chain into a horizontal T-pose in-place (world rotation), purely so
        // the captured SkeletonBone data gives Unity a proper T-pose reference for shoulder/arm
        // muscle calibration. The model's real rest pose (arms at its sides) is untouched —
        // this only ever runs on a throwaway instance that gets discarded after capture.
        private static void EnforceArmTPose(Transform armature, string clavicleName, string upperArmName,
            string forearmName, string handName)
        {
            Transform clavicle = FindByName(armature, clavicleName);
            Transform upperArm = FindByName(armature, upperArmName);
            Transform forearm = FindByName(armature, forearmName);
            Transform hand = FindByName(armature, handName);
            if (clavicle == null || upperArm == null || forearm == null) return;

            // The character is spawned at identity rotation facing world +Z, so its own
            // right side is always world +X (Unity's transform.right for an unrotated
            // object) — fixed and reliable, unlike inferring it from bone positions.
            float side = clavicleName.StartsWith("L_") ? -1f : 1f;
            Vector3 targetDirection = new Vector3(side, 0f, 0f);

            AlignBoneToward(upperArm, forearm.position, targetDirection);
            if (hand != null) AlignBoneToward(forearm, hand.position, targetDirection);
        }

        // Rotates 'bone' (world-space) so the direction from its position to 'childPosition'
        // (captured before any of this frame's edits) points along 'targetDirection' instead.
        // Uses an explicit, fixed "up" reference (rather than FromToRotation's shortest-arc,
        // which leaves roll dependent on the bone's exact starting orientation) so left and
        // right arms — whose starting poses are only approximately mirror images — end up
        // with matching, predictable roll instead of independently "whatever was shortest".
        private static void AlignBoneToward(Transform bone, Vector3 childPosition, Vector3 targetDirection)
        {
            Vector3 currentDirection = (childPosition - bone.position).normalized;
            if (currentDirection.sqrMagnitude < 0.0001f) return;
            Vector3 up = Mathf.Abs(Vector3.Dot(targetDirection, Vector3.forward)) < 0.95f
                ? Vector3.forward
                : Vector3.up;
            Quaternion targetWorldRotation = Quaternion.LookRotation(targetDirection.normalized, up);
            Quaternion currentWorldRotation = Quaternion.LookRotation(currentDirection, up);
            Quaternion correction = targetWorldRotation * Quaternion.Inverse(currentWorldRotation);
            bone.rotation = correction * bone.rotation;
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

        private static void CollectSkeleton(Transform t, List<SkeletonBone> list)
        {
            list.Add(new SkeletonBone
            {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale
            });
            for (int i = 0; i < t.childCount; i++) CollectSkeleton(t.GetChild(i), list);
        }
    }
}
