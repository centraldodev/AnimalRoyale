using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    /// <summary>
    /// Generic importer for the Rodin-extracted character figures. For every animal that
    /// has a Models/{Animal}_Rigged.fbx under Art/Characters,
    /// it configures the FBX,
    /// builds a Standard PBR material from Textures/{Animal}_*.png, creates a prefab and
    /// wires it into the AnimalPackageCatalog. Single-mesh (LODGroup added later).
    /// Run: -executeMethod AnimalBattleRoyale.EditorTools.AnimalModelImporter.ImportAll
    /// </summary>
    public static class AnimalModelImporter
    {
        private static readonly string[] Animals = { "Tiger", "Ant", "Eagle", "Monkey" };
        private const string CharBase = "Assets/AnimalBattleRoyale/Art/Characters";
        private const string CatalogPath = "Assets/AnimalBattleRoyale/Resources/AnimalPackageCatalog.asset";
        private const float TargetHeight = 2.0f; // prefab authored height; VisualScale trims it further
        private const float BlenderFbxPrefabScale = 100f;

        [MenuItem("AnimalBattleRoyale/Import All Generated Gameplay Models")]
        public static void ImportAllGameplayModels()
        {
            ImportAll();
            ImportLifeOrb();
            ImportAmmo();
            ImportCountdown();
            ImportShoulderWeapon();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Import] All generated gameplay models rebuilt");
        }

        [MenuItem("AnimalBattleRoyale/Import All Animal Models")]
        public static void ImportAll()
        {
            // The original generated animals were replaced by the current Tripo
            // models. Delegate to their dedicated importers so this legacy menu
            // remains safe after the unused source assets are removed.
            TripoTigerImporter.Import();
            TripoAnimalImporter.ImportAll();
            Debug.Log("[Import] Current Tripo animal models rebuilt");
        }

        private static void ImportOne(string animal, string fbxPath)
        {
            Debug.Log($"[Import] {animal} <- {fbxPath}");
            ConfigureFbx(fbxPath);
            Material mat = BuildMaterial(animal);
            GameObject prefab = BuildPrefab(animal, fbxPath, mat);
            WireCatalog(animal, prefab);
        }

        private static void ConfigureFbx(string fbxPath)
        {
            if (AssetImporter.GetAtPath(fbxPath) is ModelImporter mi)
            {
                mi.importAnimation = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importBlendShapes = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.importNormals = ModelImporterNormals.Import;
                bool proceduralRig = Path.GetFileNameWithoutExtension(fbxPath).EndsWith("_Rigged");
                if (proceduralRig)
                {
                    mi.animationType = ModelImporterAnimationType.Generic;
                    mi.optimizeGameObjects = false;
                    mi.preserveHierarchy = true;
                }
                // Blender FBX exports declare centimeters. Keeping file scale enabled
                // makes nested prefabs shrink another 100x after they are saved/reloaded.
                mi.useFileScale = false;
                mi.globalScale = 1f;
                mi.isReadable = false;
                mi.SaveAndReimport();
            }
        }

        private static Material BuildMaterial(string animal)
        {
            string texDir = $"{CharBase}/{animal}/Textures";
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{animal}_Diffuse.png");
            string normalPath = $"{texDir}/{animal}_Normal.png";
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{animal}_Metallic.png");

            if (AssetImporter.GetAtPath(normalPath) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            string matPath = $"{CharBase}/{animal}/{animal}_Material.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = Shader.Find("Standard");
            if (diffuse != null) mat.SetTexture("_MainTex", diffuse);
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            if (metallic != null) { mat.SetTexture("_MetallicGlossMap", metallic); mat.EnableKeyword("_METALLICGLOSSMAP"); }
            mat.SetFloat("_Glossiness", 0.2f);
            mat.SetFloat("_GlossMapScale", 0.35f);
            // Generated character textures use opaque RGB. Explicitly clear any
            // transparency state retained by an earlier material/import pass.
            mat.SetFloat("_Mode", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
            mat.color = Color.white;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject BuildPrefab(string animal, string fbxPath, Material mat)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) { Debug.LogError("[Import] FBX load failed " + fbxPath); return null; }

            GameObject root = new GameObject(animal);
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            inst.name = animal + "Model";

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            Bounds b = CalcBounds(root);
            float h = Mathf.Max(b.size.x, b.size.y, b.size.z);
            bool proceduralRig = Path.GetFileNameWithoutExtension(fbxPath).EndsWith("_Rigged");
            // Static Blender FBXs acquire a 0.01 nested-prefab conversion and need
            // the legacy compensation. Armature FBXs already preserve their units;
            // applying that same x100 made bots gigantic and put the player camera
            // inside its own skin, which looked like a transparent character.
            float nestedScaleCompensation = proceduralRig ? 1f : BlenderFbxPrefabScale;
            float scale = h > 0.0001f ? TargetHeight / h * nestedScaleCompensation : 1f;
            inst.transform.localScale = Vector3.one * scale;
            b = CalcBounds(root);
            inst.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);

            if (proceduralRig)
            {
                foreach (SkinnedMeshRenderer skinnedRenderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Bounds localBounds = skinnedRenderer.localBounds;
                    localBounds.Expand(localBounds.size * 0.35f);
                    skinnedRenderer.localBounds = localBounds;
                }
            }

            string prefabPath = $"Assets/AnimalBattleRoyale/Resources/CharacterModels/{animal}/{animal}.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[Import] {animal} prefab saved (scale {scale:0.###}, srcH {h:0.###})");
            return saved;
        }

        private static void WireCatalog(string animal, GameObject prefab)
        {
            if (prefab == null) return;
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalPackageCatalog>(CatalogPath);
            if (catalog == null) { Debug.LogError("[Import] catalog missing"); return; }
            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty p = so.FindProperty(animal.ToLowerInvariant());
            if (p != null) { p.objectReferenceValue = prefab; so.ApplyModifiedProperties(); }
            EditorUtility.SetDirty(catalog);
            Debug.Log($"[Import] catalog.{animal.ToLowerInvariant()} wired");
        }

        private const string LifeBase = "Assets/AnimalBattleRoyale/Art/Pickups/Life";
        private const string LifePrefabPath = "Assets/AnimalBattleRoyale/Resources/Pickups/LifeOrb.prefab";

        [MenuItem("AnimalBattleRoyale/Import Life Orb Pickup")]
        public static void ImportLifeOrb()
        {
            string fbx = $"{LifeBase}/Models/LifeOrb.fbx";
            if (!File.Exists(fbx)) { Debug.LogError("[LifeOrb] no FBX at " + fbx); return; }
            ConfigureFbx(fbx);

            string tex = $"{LifeBase}/Textures";
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tex}/LifeOrb_Diffuse.png");
            Texture2D emissive = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tex}/LifeOrb_Emissive.png");
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tex}/LifeOrb_Metallic.png");
            string normalPath = $"{tex}/LifeOrb_Normal.png";
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            string matPath = $"{LifeBase}/LifeOrb_Material.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = Shader.Find("Standard");
            if (diffuse != null) mat.SetTexture("_MainTex", diffuse);
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            if (metallic != null) { mat.SetTexture("_MetallicGlossMap", metallic); mat.EnableKeyword("_METALLICGLOSSMAP"); }
            if (emissive != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", emissive);
                mat.SetColor("_EmissionColor", new Color(0.5f, 1f, 0.55f) * 1.6f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            mat.SetFloat("_Glossiness", 0.65f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            GameObject root = new GameObject("LifeOrb");
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            // keep only the lightest LOD mesh for a small pickup
            Transform keep = null;
            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("LOD4")) keep = t;
            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t == inst.transform || t == keep) continue;
                if (t.name.Contains("LOD0") || t.name.Contains("LOD1") || t.name.Contains("LOD2") || t.name.Contains("LOD3"))
                    Object.DestroyImmediate(t.gameObject);
            }
            RemoveLodGroups(root);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;

            Bounds b = CalcBounds(root);
            float h = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float scale = h > 0.0001f ? 1.15f / h : 1f;
            inst.transform.localScale = Vector3.one * scale;
            b = CalcBounds(root);
            inst.transform.position -= b.center;

            Directory.CreateDirectory(Path.GetDirectoryName(LifePrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, LifePrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[LifeOrb] prefab saved (scale {scale:0.###}) -> {LifePrefabPath}");
        }

        [MenuItem("AnimalBattleRoyale/Import Ammo Prop")]
        public static void ImportAmmo()
        {
            BuildBasicProp("Assets/AnimalBattleRoyale/Art/Pickups/Ammo", "AmmoBox",
                "Assets/AnimalBattleRoyale/Resources/Pickups/AmmoBox.prefab", 1.3f, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("AnimalBattleRoyale/Import Pickup Countdown")]
        public static void ImportCountdown()
        {
            const string baseDir = "Assets/AnimalBattleRoyale/Art/Countdown";
            const string prefabPath = "Assets/AnimalBattleRoyale/Resources/Countdown/Countdown.prefab";
            string textureDir = $"{baseDir}/Textures";

            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureDir}/Countdown_Diffuse.png");
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureDir}/Countdown_Metallic.png");
            string normalPath = $"{textureDir}/Countdown_Normal.png";
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            string materialPath = $"{baseDir}/Countdown_Material.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = Shader.Find("Standard");
            if (diffuse != null) material.SetTexture("_MainTex", diffuse);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (metallic != null)
            {
                material.SetTexture("_MetallicGlossMap", metallic);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            material.SetFloat("_Glossiness", 0.42f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            GameObject root = new GameObject("Countdown");
            for (int number = 1; number <= 10; number++)
            {
                string fbxPath = $"{baseDir}/Models/Count_{number}.fbx";
                if (!File.Exists(fbxPath))
                {
                    Object.DestroyImmediate(root);
                    Debug.LogError("[Countdown] missing FBX: " + fbxPath);
                    return;
                }

                ConfigureFbx(fbxPath);
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                GameObject numberRoot = new GameObject("Num_" + number);
                numberRoot.transform.SetParent(root.transform, false);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.transform.SetParent(numberRoot.transform, false);
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = material;

                Bounds bounds = CalcBounds(numberRoot);
                float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                float scale = size > 0.0001f ? 1.2f / size * BlenderFbxPrefabScale : 1f;
                instance.transform.localScale = Vector3.one * scale;
                bounds = CalcBounds(numberRoot);
                instance.transform.position -= bounds.center;
                numberRoot.SetActive(false);
            }

            root.AddComponent<Countdown>();
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Countdown] prefab saved -> " + prefabPath);
        }

        [MenuItem("AnimalBattleRoyale/Import Shoulder Weapon")]
        public static void ImportShoulderWeapon()
        {
            const string baseDir = "Assets/AnimalBattleRoyale/Art/Weapons/SeedLauncher";
            const string modelPath = baseDir + "/Models/SeedLauncher.fbx";
            const string prefabPath = "Assets/AnimalBattleRoyale/Resources/Weapons/SeedLauncher.prefab";
            if (!File.Exists(modelPath))
            {
                Debug.LogError("[Weapon] missing FBX: " + modelPath);
                return;
            }

            ConfigureFbx(modelPath);
            string textureDir = baseDir + "/Textures";
            // Source art is 4096x4096; that's sized for a hero character, not a small
            // shoulder-mounted prop that occupies a sliver of the screen. Capping on import
            // (rather than relying on the platform's default max size) keeps VRAM/build size
            // down without touching the source files.
            CapTextureSize(textureDir + "/SeedLauncher_Diffuse.png", 1024);
            CapTextureSize(textureDir + "/SeedLauncher_Metallic.png", 1024);
            CapTextureSize(textureDir + "/SeedLauncher_Normal.png", 1024);
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(textureDir + "/SeedLauncher_Diffuse.png");
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(textureDir + "/SeedLauncher_Metallic.png");
            Texture2D emissive = AssetDatabase.LoadAssetAtPath<Texture2D>(textureDir + "/SeedLauncher_Emissive.png");
            string normalPath = textureDir + "/SeedLauncher_Normal.png";
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            string materialPath = baseDir + "/SeedLauncher_Material.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = Shader.Find("Standard");
            if (diffuse != null) material.SetTexture("_MainTex", diffuse);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (metallic != null)
            {
                material.SetTexture("_MetallicGlossMap", metallic);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            if (emissive != null)
            {
                material.SetTexture("_EmissionMap", emissive);
                material.SetColor("_EmissionColor", Color.white * 1.15f);
                material.EnableKeyword("_EMISSION");
            }
            material.SetFloat("_Glossiness", 0.38f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject root = new GameObject("SeedLauncher");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "SeedLauncherModel";
            instance.transform.SetParent(root.transform, false);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            // Unlike the legacy Blender-exported weapons, this model (like the current animal
            // rigs) is a Tripo export at roughly real-world scale already (~0.98 units), not
            // a Blender FBX needing the x100 nested-prefab compensation — applying that
            // unconditionally here blew it up to ~107 units.
            Bounds bounds = CalcBounds(root);
            float length = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = length > 0.0001f ? 1.05f / length : 1f;
            instance.transform.localScale = Vector3.one * scale;
            bounds = CalcBounds(root);
            instance.transform.position -= bounds.center;

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = Vector3.forward * 0.525f;

            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Weapon] shoulder prefab saved -> " + prefabPath);
        }

        private static void BuildBasicProp(string baseDir, string name, string prefabPath, float targetSize, bool groundAlign)
        {
            string fbx = $"{baseDir}/Models/{name}.fbx";
            if (!File.Exists(fbx)) { Debug.LogError("[Prop] no FBX " + fbx); return; }
            ConfigureFbx(fbx);

            string tex = $"{baseDir}/Textures";
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tex}/{name}_Diffuse.png");
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tex}/{name}_Metallic.png");
            string normalPath = $"{tex}/{name}_Normal.png";
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            string matPath = $"{baseDir}/{name}_Material.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = Shader.Find("Standard");
            if (diffuse != null) mat.SetTexture("_MainTex", diffuse);
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            if (metallic != null) { mat.SetTexture("_MetallicGlossMap", metallic); mat.EnableKeyword("_METALLICGLOSSMAP"); }
            mat.SetFloat("_Glossiness", 0.3f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            GameObject root = new GameObject(name);
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            Transform keep = null;
            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("LOD4")) keep = t;
            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t == inst.transform || t == keep) continue;
                if (t.name.Contains("LOD0") || t.name.Contains("LOD1") || t.name.Contains("LOD2") || t.name.Contains("LOD3"))
                    Object.DestroyImmediate(t.gameObject);
            }
            RemoveLodGroups(root);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;

            Bounds b = CalcBounds(root);
            float h = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float scale = h > 0.0001f ? targetSize / h : 1f;
            inst.transform.localScale = Vector3.one * scale;
            b = CalcBounds(root);
            inst.transform.position -= groundAlign
                ? new Vector3(b.center.x, b.min.y, b.center.z)
                : b.center;

            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[Prop] {name} saved (scale {scale:0.###}) -> {prefabPath}");
        }

        private static void CapTextureSize(string path, int maxSize)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.maxTextureSize <= maxSize) return;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }

        private static void RemoveLodGroups(GameObject root)
        {
            foreach (LODGroup lodGroup in root.GetComponentsInChildren<LODGroup>(true))
                Object.DestroyImmediate(lodGroup);
        }

        private static Bounds CalcBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
