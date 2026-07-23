using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Configures the ability and ammunition HUD icons so BattleRoyaleManager can generate
    // their unavailable grayscale versions at runtime. Unity's default texture import has
    // Read/Write disabled, which would otherwise throw when GetPixels() is called.
    [InitializeOnLoad]
    public static class AbilityIconImporter
    {
        private static readonly string[] IconPaths =
        {
            "Assets/AnimalBattleRoyale/Resources/UI/Abilities/Tiger_Ability1.png",
            "Assets/AnimalBattleRoyale/Resources/UI/Abilities/Ant_Ability1.png",
            "Assets/AnimalBattleRoyale/Resources/UI/Abilities/Eagle_Ability1.png",
            "Assets/AnimalBattleRoyale/Resources/UI/Abilities/Monkey_Ability1.png",
            "Assets/AnimalBattleRoyale/Resources/UI/Abilities/Cow_Ability1.png",
            "Assets/AnimalBattleRoyale/Resources/UI/Abilities/AmmoBulletIcon.png",
            "Assets/AnimalBattleRoyale/Resources/UI/Abilities/AmmoReloadIcon.png",
            "Assets/AnimalBattleRoyale/Resources/UI/WeaponIcons/Seed.png",
            "Assets/AnimalBattleRoyale/Resources/UI/WeaponIcons/Tomato.png",
            "Assets/AnimalBattleRoyale/Resources/UI/WeaponIcons/Watermelon.png"
        };

        static AbilityIconImporter()
        {
            EditorApplication.delayCall += ConfigureAll;
        }

        [MenuItem("AnimalBattleRoyale/Configure Ability Icons")]
        public static void ConfigureAll()
        {
            bool changed = false;
            foreach (string path in IconPaths)
            {
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
