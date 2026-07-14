#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalBattleRoyale.Editor
{
    public static class StarterSceneCreator
    {
        private const string SceneFolder = "Assets/AnimalBattleRoyale/Scenes";
        private const string ScenePath = SceneFolder + "/Prototype.unity";

        [MenuItem("Animal Battle Royale/Create Starter Scene")]
        public static void CreateStarterScene()
        {
            if (!Directory.Exists(SceneFolder)) Directory.CreateDirectory(SceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject bootstrap = new GameObject("GameBootstrap");
            bootstrap.AddComponent<GameBootstrap>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Selection.activeGameObject = bootstrap;

            EditorUtility.DisplayDialog(
                "Animal Battle Royale",
                "Cena criada em Assets/AnimalBattleRoyale/Scenes/Prototype.unity.\n\nPressione Play para iniciar o protótipo.",
                "Abrir cena");
        }
    }
}
#endif
