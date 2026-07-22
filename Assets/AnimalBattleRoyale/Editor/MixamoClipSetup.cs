using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary tool: configures the imported Mixamo motion-only FBX clips as Humanoid
    // animations with sensible loop settings, ready to be retargeted onto our custom
    // animal Humanoid avatars (see HumanoidRigSetup.cs).
    public static class MixamoClipSetup
    {
        private const string Folder = "Assets/AnimalBattleRoyale/Art/SharedAnimations/";

        [MenuItem("AnimalBattleRoyale/Debug/Configure Mixamo Clips")]
        public static void ConfigureAll()
        {
            ConfigureOne("Mixamo_Walk.fbx", loop: true);
            ConfigureOne("Mixamo_Run.fbx", loop: true);
            ConfigureOne("Mixamo_Idle.fbx", loop: true);
            ConfigureOne("Mixamo_Jump.fbx", loop: false);
            AssetDatabase.SaveAssets();
            Debug.Log("[MixamoClipSetup] Done.");
        }

        private static void ConfigureOne(string fileName, bool loop)
        {
            string path = Folder + fileName;
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
            {
                Debug.LogError($"[MixamoClipSetup] No ModelImporter at {path}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = loop;
                clips[i].loopPose = loop;
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].keepOriginalPositionXZ = false;
                clips[i].lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            bool ok = avatar != null && avatar.isValid && avatar.isHuman;
            Debug.Log($"[MixamoClipSetup] {fileName}: avatar {(ok ? "OK" : "FAILED")}, {clips.Length} clip(s), loop={loop}.");
        }
    }
}
