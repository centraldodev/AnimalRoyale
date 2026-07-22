using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Configures the ability/ammo HUD icons (dropped into Resources/UI/Abilities) so the
    // runtime grayscale generator in BattleRoyaleManager (used for the "unavailable" cooldown
    // look, mirroring the weapon icons' locked state) can read their pixels — Unity's default
    // texture import has Read/Write disabled, which throws when GetPixels() is called.
    [InitializeOnLoad]
    public static class AbilityIconImporter
    {
        private const string FolderPath = "Assets/AnimalBattleRoyale/Resources/UI/Abilities";

        private static readonly string[] IconNames =
        {
            "Tiger_Ability1", "Ant_Ability1", "Eagle_Ability1", "Monkey_Ability1", "Cow_Ability1",
            "AmmoBulletIcon", "AmmoReloadIcon"
        };

        static AbilityIconImporter()
        {
            EditorApplication.delayCall += ConfigureAll;
        }

        [MenuItem("AnimalBattleRoyale/Configure Ability Icons")]
        public static void ConfigureAll()
        {
            bool changed = false;
            foreach (string name in IconNames)
            {
                string path = $"{FolderPath}/{name}.png";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (importer.isReadable && importer.textureType == TextureImporterType.Default
                    && importer.alphaIsTransparency) continue;

                importer.textureType = TextureImporterType.Default;
                importer.isReadable = true;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
                changed = true;
            }
            if (changed) Debug.Log("[AbilityIconImporter] Ability/ammo icons configured (Read/Write enabled).");
        }
    }
}
