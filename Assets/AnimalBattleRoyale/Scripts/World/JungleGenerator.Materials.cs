using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    public sealed partial class JungleGenerator
    {
        private static Material cachedArtRefTreeMaterial;
        private static Material cachedArtRefBushMaterial;
        private static Material cachedArtRefBambooMaterial;
        private static Material cachedArtRefRockMaterial;
        private static Material cachedArtRefHouseMaterial;

        private static Material GetArtRefTreeMaterial()
        {
            if (cachedArtRefTreeMaterial != null) return cachedArtRefTreeMaterial;

            Texture2D albedo = Resources.Load<Texture2D>("EnvironmentModels/ArtRefTrees/texture_diffuse");
            if (albedo == null) return null;

            Material material = new Material(ShaderLibrary.Lit)
            {
                name = "ArtRefTree_RuntimePBR",
                color = Color.white,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

            Texture2D normal = Resources.Load<Texture2D>("EnvironmentModels/ArtRefTrees/texture_normal");
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.72f);
                material.EnableKeyword("_NORMALMAP");
            }

            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.16f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.16f);
            cachedArtRefTreeMaterial = material;
            return cachedArtRefTreeMaterial;
        }

        private static Material GetArtRefBushMaterial()
        {
            return GetArtRefEnvironmentMaterial(ref cachedArtRefBushMaterial, "bush", "ArtRefBush_RuntimePBR", 0.62f, 0.1f);
        }

        private static Material GetArtRefBambooMaterial()
        {
            return GetArtRefEnvironmentMaterial(ref cachedArtRefBambooMaterial, "bamboo", "ArtRefBamboo_RuntimePBR", 0.68f, 0.14f);
        }

        private static Material GetArtRefRockMaterial()
        {
            if (cachedArtRefRockMaterial != null) return cachedArtRefRockMaterial;

            Texture2D albedo = Resources.Load<Texture2D>("EnvironmentModels/ArtRefRocks/texture_diffuse");
            if (albedo == null) return null;
            Material material = new Material(ShaderLibrary.Lit)
            {
                name = "ArtRefRock_RuntimePBR",
                color = Color.white,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

            Texture2D normal = Resources.Load<Texture2D>("EnvironmentModels/ArtRefRocks/texture_normal");
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.86f);
                material.EnableKeyword("_NORMALMAP");
            }

            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.12f);
            cachedArtRefRockMaterial = material;
            return cachedArtRefRockMaterial;
        }

        private static Material GetArtRefHouseMaterial()
        {
            if (cachedArtRefHouseMaterial != null) return cachedArtRefHouseMaterial;

            Texture2D albedo = Resources.Load<Texture2D>("EnvironmentModels/ArtRefHouses/texture_diffuse");
            if (albedo == null) return null;
            Material material = new Material(ShaderLibrary.Lit)
            {
                name = "ArtRefHouse_RuntimePBR",
                color = Color.white,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

            Texture2D normal = Resources.Load<Texture2D>("EnvironmentModels/ArtRefHouses/texture_normal");
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.76f);
                material.EnableKeyword("_NORMALMAP");
            }

            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.17f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.17f);
            cachedArtRefHouseMaterial = material;
            return cachedArtRefHouseMaterial;
        }

        private static Material GetArtRefEnvironmentMaterial(ref Material cachedMaterial, string texturePrefix,
            string materialName, float normalStrength, float smoothness)
        {
            if (cachedMaterial != null) return cachedMaterial;

            string resourceRoot = "EnvironmentModels/ArtRefEnvironment/" + texturePrefix;
            Texture2D albedo = Resources.Load<Texture2D>(resourceRoot + "_diffuse");
            if (albedo == null) return null;

            Material material = new Material(ShaderLibrary.Lit)
            {
                name = materialName,
                color = Color.white,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

            Texture2D normal = Resources.Load<Texture2D>(resourceRoot + "_normal");
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", normalStrength);
                material.EnableKeyword("_NORMALMAP");
            }

            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            cachedMaterial = material;
            return cachedMaterial;
        }

        private static Material CreateGroundMaterial(Texture2D naturalTexture)
        {
            if (naturalTexture != null)
                return CreateNaturalSurfaceMaterial(naturalTexture, new Color(0.72f, 0.9f, 0.64f), 1f);

            Texture2D natureStarterGrass = Resources.Load<Texture2D>(
                "EnvironmentModels/NewNaturePack/NatureStarterGrass");
            if (natureStarterGrass != null)
            {
                // The source is a seamless 2K ground texture. A saturated tint and
                // broad tiling preserve the game's cartoon palette while adding the
                // missing fine grass detail between the lightweight 3D tufts.
                return CreateNaturalSurfaceMaterial(
                    natureStarterGrass, new Color(0.82f, 1f, 0.68f), 28f);
            }

            Material material = CreateMaterial(Color.white);
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = "CartoonForestFloor",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            Color moss = new Color(0.075f, 0.31f, 0.055f);
            Color grass = new Color(0.16f, 0.47f, 0.08f);
            Color sunlitGrass = new Color(0.28f, 0.58f, 0.105f);
            Color earth = new Color(0.31f, 0.19f, 0.07f);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float broad = Mathf.PerlinNoise(x * 0.038f, y * 0.038f);
                    float detail = Mathf.PerlinNoise((x + 37f) * 0.115f, (y - 19f) * 0.115f);
                    Color color = Color.Lerp(moss, grass, broad);
                    color = Color.Lerp(color, sunlitGrass, Mathf.Clamp01((detail - 0.63f) * 1.8f));
                    color = Color.Lerp(color, earth, Mathf.Clamp01((0.24f - broad) * 2.4f));
                    pixels[y * size + x] = color;
                }
            }
            for (int i = 0; i < size; i++)
            {
                pixels[(size - 1) * size + i] = pixels[i];
                pixels[i * size + size - 1] = pixels[i * size];
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            return material;
        }

        private static Material CreateDirtTrailMaterial(Texture2D naturalTexture)
        {
            if (naturalTexture != null)
                return CreateNaturalSurfaceMaterial(naturalTexture, new Color(0.78f, 0.67f, 0.48f), 1.35f);

            Material material = CreateMaterial(Color.white);
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = "PackedJungleDirt",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            Color darkSoil = new Color(0.25f, 0.125f, 0.035f);
            Color packedSoil = new Color(0.5f, 0.275f, 0.075f);
            Color drySoil = new Color(0.64f, 0.39f, 0.13f);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float broad = Mathf.PerlinNoise((x + 11f) * 0.075f, (y - 7f) * 0.075f);
                    float grit = Mathf.PerlinNoise((x - 19f) * 0.24f, (y + 23f) * 0.24f);
                    Color color = Color.Lerp(darkSoil, packedSoil, broad);
                    color = Color.Lerp(color, drySoil, Mathf.Clamp01((grit - 0.64f) * 1.6f));
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            return material;
        }

        private static Material CreateNaturalSurfaceMaterial(Texture2D texture, Color tint, float tiling)
        {
            Material material = CreateMaterial(tint);
            if (texture == null) return material;

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", Vector2.one * tiling);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", Vector2.one * tiling);
            }
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.04f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.04f);
            material.enableInstancing = true;
            return material;
        }

        private static Material[] CreateGrassDetailMaterials(NatureEnvironmentCatalog catalog)
        {
            List<Material> materials = new List<Material>(2);
            for (int i = 0; i < 2; i++)
            {
                Texture2D texture = catalog != null ? catalog.GetGrassDetail(i) : null;
                if (texture == null) continue;

                Material material = CreateNaturalSurfaceMaterial(texture, new Color(0.45f, 0.78f, 0.28f), 1f);
                if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 1f);
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
                if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
                if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.34f);
                if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                materials.Add(material);
            }

            if (materials.Count == 0)
            {
                Material fallback = CreateMaterial(new Color(0.18f, 0.5f, 0.075f));
                if (fallback.HasProperty("_Cull")) fallback.SetFloat("_Cull", (float)CullMode.Off);
                if (fallback.HasProperty("_Smoothness")) fallback.SetFloat("_Smoothness", 0f);
                materials.Add(fallback);
            }
            return materials.ToArray();
        }

        private static Material CreateMaterial(Color color, Color? emission = null)
        {
            Shader shader = ShaderLibrary.Lit;
            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.18f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            material.enableInstancing = true;
            return material;
        }

        /// <summary>
        /// Blender procedural nodes are not preserved by FBX. Rebuild their visual character
        /// with shared, noise-textured Unity materials so the detailed kit keeps bark grain,
        /// mottled leaves, moss and saturated petals without duplicating materials per instance.
        /// </summary>
        private static void EnhanceImportedMaterials(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] source = renderer.sharedMaterials;
                Material[] enhanced = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    string materialName = source[i] != null ? source[i].name : "HD_Leaf";
                    if (!detailedMaterialCache.TryGetValue(materialName, out Material material))
                    {
                        material = CreateDetailedSurfaceMaterial(materialName);
                        detailedMaterialCache.Add(materialName, material);
                    }
                    enhanced[i] = material;
                }
                renderer.sharedMaterials = enhanced;

                // TwoSided is the most expensive shadow mode (draws both faces in the shadow
                // pass) and was applied to every leaf/moss/petal renderer on every high-detail
                // tree — the shadow-quality gain over single-sided is imperceptible at this
                // scale, but the draw-call cost isn't.
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static Material CreateDetailedSurfaceMaterial(string materialName)
        {
            string key = materialName.ToLowerInvariant();
            Color dark;
            Color light;
            float smoothness = 0.12f;
            float noiseScale = 0.1f;
            float relief = 0.22f;
            bool doubleSided = false;
            Color? emission = null;

            if (key.Contains("barkdark")) { dark = new Color(0.055f, 0.012f, 0.004f); light = new Color(0.24f, 0.065f, 0.012f); noiseScale = 0.16f; relief = 0.4f; }
            else if (key.Contains("barklight")) { dark = new Color(0.19f, 0.045f, 0.008f); light = new Color(0.58f, 0.22f, 0.035f); noiseScale = 0.14f; relief = 0.38f; }
            else if (key.Contains("bark")) { dark = new Color(0.085f, 0.018f, 0.004f); light = new Color(0.38f, 0.105f, 0.015f); noiseScale = 0.15f; relief = 0.42f; }
            else if (key.Contains("leafsun")) { dark = new Color(0.09f, 0.35f, 0.012f); light = new Color(0.46f, 0.82f, 0.055f); doubleSided = true; noiseScale = 0.09f; }
            else if (key.Contains("leafmid")) { dark = new Color(0.025f, 0.22f, 0.012f); light = new Color(0.14f, 0.64f, 0.035f); doubleSided = true; noiseScale = 0.095f; }
            else if (key.Contains("leaf") || key.Contains("vine")) { dark = new Color(0.008f, 0.105f, 0.004f); light = new Color(0.055f, 0.43f, 0.018f); doubleSided = true; noiseScale = 0.09f; }
            else if (key.Contains("moss")) { dark = new Color(0.035f, 0.16f, 0.008f); light = new Color(0.25f, 0.57f, 0.035f); doubleSided = true; noiseScale = 0.18f; }
            else if (key.Contains("rocklight")) { dark = new Color(0.2f, 0.27f, 0.3f); light = new Color(0.49f, 0.58f, 0.6f); noiseScale = 0.13f; relief = 0.5f; }
            else if (key.Contains("rock")) { dark = new Color(0.075f, 0.14f, 0.17f); light = new Color(0.35f, 0.45f, 0.46f); noiseScale = 0.14f; relief = 0.52f; }
            else if (key.Contains("earth")) { dark = new Color(0.19f, 0.065f, 0.01f); light = new Color(0.58f, 0.29f, 0.055f); noiseScale = 0.18f; }
            else if (key.Contains("crystalblue")) { dark = new Color(0.005f, 0.12f, 0.65f); light = new Color(0.04f, 0.72f, 1f); smoothness = 0.78f; emission = new Color(0f, 0.12f, 0.55f); }
            else if (key.Contains("crystalpurple")) { dark = new Color(0.16f, 0.005f, 0.55f); light = new Color(0.68f, 0.08f, 1f); smoothness = 0.76f; emission = new Color(0.16f, 0f, 0.48f); }
            else if (key.Contains("flowergold")) { dark = new Color(0.75f, 0.16f, 0.005f); light = new Color(1f, 0.86f, 0.06f); smoothness = 0.28f; doubleSided = true; }
            else if (key.Contains("flowerpurple")) { dark = new Color(0.2f, 0.005f, 0.48f); light = new Color(0.78f, 0.08f, 1f); smoothness = 0.28f; doubleSided = true; }
            else if (key.Contains("flowerpink")) { dark = new Color(0.55f, 0.01f, 0.16f); light = new Color(1f, 0.18f, 0.58f); smoothness = 0.28f; doubleSided = true; }
            else if (key.Contains("flower")) { dark = new Color(0.55f, 0.015f, 0.005f); light = new Color(1f, 0.28f, 0.025f); smoothness = 0.28f; doubleSided = true; }
            else { dark = new Color(0.08f, 0.25f, 0.025f); light = new Color(0.25f, 0.58f, 0.08f); doubleSided = true; }

            Material material = CreateMaterial(Color.white, emission);
            material.name = materialName + "_RuntimeDetailed";
            Texture2D albedo = CreateMottledTexture(materialName, dark, light, noiseScale);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", CreateMottledNormalTexture(materialName, noiseScale, relief));
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (doubleSided && material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            return material;
        }

        private static Texture2D CreateMottledTexture(string name, Color dark, Color light, float scale)
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = name + "_MottledAlbedo",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            Color[] pixels = new Color[size * size];
            float offset = Mathf.Abs(name.GetHashCode() % 997);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float broad = Mathf.PerlinNoise((x + offset) * scale * 0.38f, (y - offset) * scale * 0.38f);
                    float grain = Mathf.PerlinNoise((x - offset) * scale * 1.65f, (y + offset) * scale * 1.65f);
                    float variation = Mathf.Clamp01(broad * 0.74f + grain * 0.26f);
                    pixels[y * size + x] = Color.Lerp(dark, light, variation);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D CreateMottledNormalTexture(string name, float scale, float strength)
        {
            const int size = 128;
            float[] heights = new float[size * size];
            float offset = Mathf.Abs(name.GetHashCode() % 997);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float broad = Mathf.PerlinNoise((x + offset) * scale * 0.38f, (y - offset) * scale * 0.38f);
                    float grain = Mathf.PerlinNoise((x - offset) * scale * 1.65f, (y + offset) * scale * 1.65f);
                    heights[y * size + x] = broad * 0.68f + grain * 0.32f;
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = name + "_SurfaceNormal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int previousY = (y - 1 + size) % size;
                int nextY = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int previousX = (x - 1 + size) % size;
                    int nextX = (x + 1) % size;
                    float dx = (heights[y * size + nextX] - heights[y * size + previousX]) * strength;
                    float dy = (heights[nextY * size + x] - heights[previousY * size + x]) * strength;
                    Vector3 normal = new Vector3(-dx, -dy, 1f).normalized;
                    pixels[y * size + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Material CreateWaterMaterial(Color color)
        {
            Material material = CreateMaterial(color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.72f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.72f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static void ConfigureVisualOptimization(GameObject root, float cullScreenHeight)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null) material.enableInstancing = true;
                }
            }

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null) lodGroup = root.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.SetLODs(new[] { new LOD(cullScreenHeight, renderers) });
            lodGroup.RecalculateBounds();
        }
    }
}
