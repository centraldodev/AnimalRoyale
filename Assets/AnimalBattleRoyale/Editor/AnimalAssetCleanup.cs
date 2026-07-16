using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    /// <summary>
    /// Keeps only the current generated animal models and removes legacy character assets.
    /// </summary>
    public static class AnimalAssetCleanup
    {
        private static readonly string[] Animals = { "Tiger", "Ant", "Eagle", "Monkey" };

        [MenuItem("AnimalBattleRoyale/Cleanup Legacy Animal Assets")]
        public static void Run()
        {
            AnimalModelImporter.ImportAll();

            int deletedCount = 0;
            foreach (string assetPath in GetLegacyAssetPaths())
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) == null && !AssetDatabase.IsValidFolder(assetPath))
                    continue;

                if (AssetDatabase.DeleteAsset(assetPath))
                    deletedCount++;
                else
                    Debug.LogWarning("[Animal Cleanup] Could not delete " + assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Animal Cleanup] Removed {deletedCount} legacy asset path(s). Current animals use *_Rigged.fbx.");
        }

        private static IEnumerable<string> GetLegacyAssetPaths()
        {
            foreach (string animal in Animals)
            {
                string basePath = $"Assets/AnimalBattleRoyale/Art/Characters/{animal}/Models";
                yield return $"{basePath}/{animal}.fbx";
                yield return $"{basePath}/{animal}_Source.blend";
            }

            yield return "Assets/AnimalBattleRoyale/Art/Characters/Previews";
            yield return "Assets/AnimalBattleRoyale/Resources/CharacterConcepts";

            foreach (string sfxPath in GetLegacySfxPaths("Assets/AnimalBattleRoyale/Resources/Audio/SFX"))
                yield return sfxPath;
            foreach (string sfxPath in GetLegacySfxPaths("Assets/AnimalBattleRoyale/Resources/Audio/Sfx"))
                yield return sfxPath;
        }

        private static IEnumerable<string> GetLegacySfxPaths(string folder)
        {
            string[] keep =
            {
                "AmmoPickup.wav",
                "EagleFlight.wav",
                "Footstep.wav",
                "LongJump.wav",
                "PlayerDeath.wav",
                "PlayerHit.wav",
                "ProjectileFly.wav",
                "ProjectileImpact.wav",
                "SeedShot.wav"
            };

            if (!Directory.Exists(folder))
                yield break;

            HashSet<string> keepSet = new HashSet<string>(keep);
            foreach (string filePath in Directory.GetFiles(folder, "*.wav"))
            {
                string fileName = Path.GetFileName(filePath);
                if (!keepSet.Contains(fileName))
                    yield return filePath.Replace('\\', '/');
            }
        }
    }
}
