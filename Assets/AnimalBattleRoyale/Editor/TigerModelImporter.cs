using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AnimalBattleRoyale.Editor
{
    /// <summary>
    /// One-click pipeline for the first real character model. When Unity notices Tiger.fbx,
    /// it imports it as a Generic animal rig and produces a runtime prefab in Resources.
    /// </summary>
    public sealed class TigerModelImporter : AssetPostprocessor
    {
        private const string CharactersRoot = "Assets/AnimalBattleRoyale/Art/Characters";
        private const string ResourcesRoot = "Assets/AnimalBattleRoyale/Resources/CharacterModels";

        [InitializeOnLoadMethod]
        private static void QueueInitialPrefabBuild()
        {
            // Also covers the first project open, when FBX files and editor scripts can
            // be imported in a different order. Existing assets are not rebuilt during
            // every domain reload, which would interrupt entering Play Mode.
            EditorApplication.delayCall += RebuildMissingPrefabs;
        }

        private static void RebuildMissingPrefabs()
        {
            foreach (string animalName in new[] { "Ant", "Monkey", "Tiger", "Eagle" })
            {
                string controllerPath = $"{CharactersRoot}/{animalName}/Animations/{animalName}.controller";
                string prefabPath = $"{ResourcesRoot}/{animalName}/{animalName}.prefab";
                if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null
                    && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) continue;
                CreateRuntimePrefab($"{CharactersRoot}/{animalName}/Models/{animalName}.fbx", animalName);
            }
        }

        private void OnPreprocessModel()
        {
            if (!TryGetAnimalName(assetPath, out _)) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
        }

        private void OnPreprocessAnimation()
        {
            if (!TryGetAnimalName(assetPath, out _)) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] importedClips = importer.defaultClipAnimations;
            if (importedClips.Length == 0) importedClips = importer.clipAnimations;
            foreach (ModelImporterClipAnimation clip in importedClips)
            {
                string clipName = NormalizeActionName(clip.name);
                clip.loopTime = clipName.EndsWith("_Idle") || clipName.EndsWith("_Walk")
                    || clipName.EndsWith("_Run") || clipName.EndsWith("_Fly");
                clip.loopPose = clip.loopTime;
            }
            if (importedClips.Length > 0) importer.clipAnimations = importedClips;
        }

        private void OnPostprocessModel(GameObject importedModel)
        {
            if (!TryGetAnimalName(assetPath, out string animalName)) return;
            string modelPath = assetPath;
            EditorApplication.delayCall += () => CreateRuntimePrefab(modelPath, animalName);
        }

        [MenuItem("Animal Battle Royale/Rebuild Tiger Prefab")]
        private static void RebuildTigerPrefab()
        {
            CreateRuntimePrefab($"{CharactersRoot}/Tiger/Models/Tiger.fbx", "Tiger");
        }

        [MenuItem("Animal Battle Royale/Rebuild Character Prefabs")]
        private static void RebuildAllPrefabs()
        {
            foreach (string animalName in new[] { "Ant", "Monkey", "Tiger", "Eagle" })
            {
                CreateRuntimePrefab($"{CharactersRoot}/{animalName}/Models/{animalName}.fbx", animalName);
            }
        }

        private static bool TryGetAnimalName(string modelPath, out string animalName)
        {
            animalName = Path.GetFileNameWithoutExtension(modelPath);
            return modelPath == $"{CharactersRoot}/{animalName}/Models/{animalName}.fbx"
                   && (animalName == "Tiger" || animalName == "Monkey" || animalName == "Eagle" || animalName == "Ant");
        }

        private static void CreateRuntimePrefab(string modelPath, string animalName)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return;

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .ToArray();
            if (clips.Length == 0) return;

            string controllerPath = $"{CharactersRoot}/{animalName}/Animations/{animalName}.controller";
            string prefabPath = $"{ResourcesRoot}/{animalName}/{animalName}.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(controllerPath));
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (AnimationClip clip in clips)
            {
                string stateName = NormalizeActionName(clip.name);
                AnimatorState state = stateMachine.AddState(stateName);
                state.motion = clip;
                if (stateName == animalName + "_Idle") stateMachine.defaultState = state;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = animalName;
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string NormalizeActionName(string actionName)
        {
            if (string.IsNullOrEmpty(actionName)) return actionName;
            int separator = actionName.LastIndexOf('|');
            return separator >= 0 && separator < actionName.Length - 1
                ? actionName.Substring(separator + 1)
                : actionName;
        }
    }
}
