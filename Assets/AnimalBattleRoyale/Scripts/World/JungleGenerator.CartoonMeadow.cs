using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    public sealed partial class JungleGenerator
    {
        private const int CartoonGrassPatchCount = 2600;
        private const int CartoonFlowerPatchCount = 96;
        private const float CartoonMeadowChunkSize = 40f;
        private const int CartoonPaletteSize = 8;

        private static Material cachedCartoonMeadowMaterial;

        private sealed class MeadowChunkGeometry
        {
            public readonly List<Vector3> Vertices = new List<Vector3>(2048);
            public readonly List<Vector2> Uvs = new List<Vector2>(2048);
            public readonly List<int> Triangles = new List<int>(3072);
        }

        private void CreateCartoonMeadow(Transform parent, System.Random random,
            out int grassTufts, out int flowers)
        {
            Transform meadowRoot = new GameObject("CartoonGrassAndFlowers").transform;
            meadowRoot.SetParent(parent, false);

            Dictionary<Vector2Int, MeadowChunkGeometry> chunks =
                new Dictionary<Vector2Int, MeadowChunkGeometry>();
            grassTufts = 0;
            flowers = 0;

            for (int patch = 0; patch < CartoonGrassPatchCount; patch++)
            {
                if (!TryFindMeadowPosition(random, lakeRadius + 4.5f, out Vector3 center)) continue;
                int tuftCount = random.Next(3, 6);
                for (int member = 0; member < tuftCount; member++)
                {
                    float angle = NextNatureFloat(random, 0f, Mathf.PI * 2f);
                    float radius = member == 0 ? 0f : NextNatureFloat(random, 0.3f, 1.8f);
                    Vector3 position = center + new Vector3(Mathf.Cos(angle) * radius, 0f,
                        Mathf.Sin(angle) * radius);
                    if (!IsMeadowPositionAllowed(new Vector2(position.x, position.z))) continue;
                    position.y = CalculateGroundHeight(position.x, position.z) + 0.02f;
                    AddCartoonGrassTuft(GetMeadowChunk(chunks, position), position, random, grassTufts);
                    grassTufts++;
                }
            }

            for (int patch = 0; patch < CartoonFlowerPatchCount; patch++)
            {
                if (!TryFindFlowerPatchCenter(random, patch, out Vector3 center)) continue;
                int flowerType = patch % 3;
                int flowerCount = random.Next(2, 5);
                for (int member = 0; member < flowerCount; member++)
                {
                    float angle = NextNatureFloat(random, 0f, Mathf.PI * 2f);
                    float radius = member == 0 ? 0f : NextNatureFloat(random, 0.35f, 1.45f);
                    Vector3 position = center + new Vector3(Mathf.Cos(angle) * radius, 0f,
                        Mathf.Sin(angle) * radius);
                    if (!IsMeadowPositionAllowed(new Vector2(position.x, position.z))) continue;
                    position.y = CalculateGroundHeight(position.x, position.z) + 0.025f;
                    AddCartoonFlower(GetMeadowChunk(chunks, position), position, random, flowerType);
                    flowers++;
                }
            }

            Material material = GetCartoonMeadowMaterial();
            int chunkIndex = 0;
            foreach (KeyValuePair<Vector2Int, MeadowChunkGeometry> pair in chunks)
            {
                MeadowChunkGeometry geometry = pair.Value;
                if (geometry.Triangles.Count == 0) continue;

                GameObject chunkObject = new GameObject($"MeadowChunk_{pair.Key.x}_{pair.Key.y}");
                chunkObject.transform.SetParent(meadowRoot, false);
                Mesh mesh = new Mesh { name = $"CartoonMeadow_{chunkIndex++:00}" };
                if (geometry.Vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(geometry.Vertices);
                mesh.SetUVs(0, geometry.Uvs);
                mesh.SetTriangles(geometry.Triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);

                chunkObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = chunkObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                chunkObject.isStatic = true;
            }
        }

        private bool TryFindMeadowPosition(System.Random random, float minimumRadius, out Vector3 position)
        {
            float extent = mapSize * 0.5f - 12f;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                float x = NextNatureFloat(random, -extent, extent);
                float z = NextNatureFloat(random, -extent, extent);
                Vector2 planar = new Vector2(x, z);
                if (planar.magnitude < minimumRadius || !IsMeadowPositionAllowed(planar)) continue;

                position = new Vector3(x, CalculateGroundHeight(x, z) + 0.02f, z);
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private bool TryFindFlowerPatchCenter(System.Random random, int patchIndex, out Vector3 position)
        {
            // Half of the flower beds form an irregular lakeside band so the three
            // silhouettes are easy to discover during normal play. The remainder
            // decorates clearings all the way to the outer mountains.
            if (patchIndex < CartoonFlowerPatchCount / 2)
            {
                int lakesideCount = CartoonFlowerPatchCount / 2;
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    float angle = (patchIndex + NextNatureFloat(random, -0.22f, 0.22f))
                                  * Mathf.PI * 2f / lakesideCount;
                    float radius = NextNatureFloat(random, lakeRadius + 8f, lakeRadius + 30f);
                    Vector2 planar = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!IsMeadowPositionAllowed(planar)) continue;
                    position = new Vector3(planar.x, CalculateGroundHeight(planar.x, planar.y) + 0.025f,
                        planar.y);
                    return true;
                }
            }

            return TryFindMeadowPosition(random, lakeRadius + 6f, out position);
        }

        private bool IsMeadowPositionAllowed(Vector2 position)
        {
            if (IsInsideSwampReserve(position, 1.5f)) return false;

            // Leave the complete bridge crossing and both approaches visually clear.
            if (Mathf.Abs(position.y + 16f) < 6.5f && Mathf.Abs(position.x) < 45f) return false;

            // Keep the player spawn readable instead of hiding the animal inside tall grass.
            float spawnAngle = 220f * Mathf.Deg2Rad;
            Vector2 shoreSpawn = new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle)) * (lakeRadius + 11f);
            return Vector2.Distance(position, shoreSpawn) >= 8.5f;
        }

        private MeadowChunkGeometry GetMeadowChunk(
            Dictionary<Vector2Int, MeadowChunkGeometry> chunks, Vector3 position)
        {
            float halfMap = mapSize * 0.5f;
            Vector2Int key = new Vector2Int(
                Mathf.FloorToInt((position.x + halfMap) / CartoonMeadowChunkSize),
                Mathf.FloorToInt((position.z + halfMap) / CartoonMeadowChunkSize));
            if (!chunks.TryGetValue(key, out MeadowChunkGeometry chunk))
            {
                chunk = new MeadowChunkGeometry();
                chunks.Add(key, chunk);
            }
            return chunk;
        }

        private static void AddCartoonGrassTuft(MeadowChunkGeometry chunk, Vector3 center,
            System.Random random, int tuftIndex)
        {
            int bladeCount = random.Next(5, 9);
            for (int blade = 0; blade < bladeCount; blade++)
            {
                float angle = NextNatureFloat(random, 0f, Mathf.PI * 2f);
                float radius = NextNatureFloat(random, 0.04f, 0.34f);
                Vector3 baseCenter = center + new Vector3(Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius);
                Vector3 widthAxis = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float width = NextNatureFloat(random, 0.055f, 0.12f);
                float height = NextNatureFloat(random, 0.32f, 0.82f);
                Vector3 lean = new Vector3(
                    NextNatureFloat(random, -0.11f, 0.11f), 0f,
                    NextNatureFloat(random, -0.11f, 0.11f));
                int palette = (tuftIndex + blade) % 4 == 0 ? 1 : 0;
                AddPointedBlade(chunk, baseCenter, widthAxis, lean, width, height, palette);
            }
        }

        private static void AddPointedBlade(MeadowChunkGeometry chunk, Vector3 center,
            Vector3 widthAxis, Vector3 lean, float halfWidth, float height, int palette)
        {
            int start = chunk.Vertices.Count;
            Vector3 shoulderCenter = center + Vector3.up * (height * 0.68f) + lean * 0.6f;
            Vector3 tip = center + Vector3.up * height + lean;
            chunk.Vertices.Add(center - widthAxis * halfWidth);
            chunk.Vertices.Add(shoulderCenter - widthAxis * (halfWidth * 0.62f));
            chunk.Vertices.Add(tip);
            chunk.Vertices.Add(shoulderCenter + widthAxis * (halfWidth * 0.62f));
            chunk.Vertices.Add(center + widthAxis * halfWidth);
            AddPaletteUvs(chunk, 5, palette);
            chunk.Triangles.Add(start);
            chunk.Triangles.Add(start + 1);
            chunk.Triangles.Add(start + 3);
            chunk.Triangles.Add(start);
            chunk.Triangles.Add(start + 3);
            chunk.Triangles.Add(start + 4);
            chunk.Triangles.Add(start + 1);
            chunk.Triangles.Add(start + 2);
            chunk.Triangles.Add(start + 3);
        }

        private static void AddCartoonFlower(MeadowChunkGeometry chunk, Vector3 position,
            System.Random random, int flowerType)
        {
            float stemHeight = NextNatureFloat(random, 0.48f, 0.92f);
            float facing = NextNatureFloat(random, 0f, Mathf.PI * 2f);
            Vector3 firstAxis = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing));
            Vector3 secondAxis = Vector3.Cross(Vector3.up, firstAxis).normalized;
            AddStemQuad(chunk, position, firstAxis, stemHeight);
            AddStemQuad(chunk, position, secondAxis, stemHeight);

            // A small side leaf gives the flowers a chunky Tripo-like silhouette.
            Vector3 leafBase = position + Vector3.up * (stemHeight * 0.36f);
            AddPointedBlade(chunk, leafBase, firstAxis, secondAxis * 0.15f,
                0.055f, stemHeight * 0.38f, 1);

            Vector3 head = position + Vector3.up * stemHeight;
            switch (flowerType)
            {
                case 0: // White six-petal daisy.
                    AddRadialFlower(chunk, head, 6, 0.24f, 0.1f, 6, 4, facing);
                    break;
                case 1: // Wide pink five-point star flower.
                    AddRadialFlower(chunk, head, 5, 0.31f, 0.085f, 3, 7, facing);
                    break;
                default: // Compact purple four-petal blossom.
                    AddRadialFlower(chunk, head, 4, 0.27f, 0.14f, 5, 4, facing + Mathf.PI * 0.25f);
                    break;
            }
        }

        private static void AddStemQuad(MeadowChunkGeometry chunk, Vector3 position,
            Vector3 widthAxis, float height)
        {
            int start = chunk.Vertices.Count;
            const float halfWidth = 0.022f;
            chunk.Vertices.Add(position - widthAxis * halfWidth);
            chunk.Vertices.Add(position + Vector3.up * height - widthAxis * halfWidth);
            chunk.Vertices.Add(position + Vector3.up * height + widthAxis * halfWidth);
            chunk.Vertices.Add(position + widthAxis * halfWidth);
            AddPaletteUvs(chunk, 4, 2);
            chunk.Triangles.Add(start);
            chunk.Triangles.Add(start + 1);
            chunk.Triangles.Add(start + 2);
            chunk.Triangles.Add(start);
            chunk.Triangles.Add(start + 2);
            chunk.Triangles.Add(start + 3);
        }

        private static void AddRadialFlower(MeadowChunkGeometry chunk, Vector3 center, int petalCount,
            float petalLength, float petalWidth, int petalPalette, int centerPalette, float rotation)
        {
            for (int petal = 0; petal < petalCount; petal++)
            {
                float angle = rotation + petal * Mathf.PI * 2f / petalCount;
                Vector3 forward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = new Vector3(-forward.z, 0f, forward.x);
                Vector3 inner = center + forward * 0.025f;
                Vector3 outer = center + forward * petalLength + Vector3.up * 0.025f;
                int start = chunk.Vertices.Count;
                chunk.Vertices.Add(inner - side * (petalWidth * 0.45f));
                chunk.Vertices.Add(center + forward * (petalLength * 0.5f) - side * petalWidth);
                chunk.Vertices.Add(outer);
                chunk.Vertices.Add(center + forward * (petalLength * 0.5f) + side * petalWidth);
                chunk.Vertices.Add(inner + side * (petalWidth * 0.45f));
                AddPaletteUvs(chunk, 5, petalPalette);
                chunk.Triangles.Add(start);
                chunk.Triangles.Add(start + 3);
                chunk.Triangles.Add(start + 1);
                chunk.Triangles.Add(start);
                chunk.Triangles.Add(start + 4);
                chunk.Triangles.Add(start + 3);
                chunk.Triangles.Add(start + 1);
                chunk.Triangles.Add(start + 3);
                chunk.Triangles.Add(start + 2);
            }

            const int centerSides = 6;
            Vector3 raisedCenter = center + Vector3.up * 0.04f;
            for (int sideIndex = 0; sideIndex < centerSides; sideIndex++)
            {
                float firstAngle = sideIndex * Mathf.PI * 2f / centerSides;
                float secondAngle = (sideIndex + 1) * Mathf.PI * 2f / centerSides;
                int start = chunk.Vertices.Count;
                chunk.Vertices.Add(raisedCenter);
                chunk.Vertices.Add(raisedCenter + new Vector3(Mathf.Cos(firstAngle), 0f,
                    Mathf.Sin(firstAngle)) * 0.085f);
                chunk.Vertices.Add(raisedCenter + new Vector3(Mathf.Cos(secondAngle), 0f,
                    Mathf.Sin(secondAngle)) * 0.085f);
                AddPaletteUvs(chunk, 3, centerPalette);
                chunk.Triangles.Add(start);
                chunk.Triangles.Add(start + 2);
                chunk.Triangles.Add(start + 1);
            }
        }

        private static void AddPaletteUvs(MeadowChunkGeometry chunk, int count, int palette)
        {
            float u = (Mathf.Clamp(palette, 0, CartoonPaletteSize - 1) + 0.5f) / CartoonPaletteSize;
            for (int index = 0; index < count; index++) chunk.Uvs.Add(new Vector2(u, 0.5f));
        }

        private static Material GetCartoonMeadowMaterial()
        {
            if (cachedCartoonMeadowMaterial != null) return cachedCartoonMeadowMaterial;

            Texture2D palette = new Texture2D(CartoonPaletteSize, 1, TextureFormat.RGBA32, false)
            {
                name = "CartoonMeadowPalette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            palette.SetPixels(new[]
            {
                new Color(0.18f, 0.54f, 0.06f),
                new Color(0.4f, 0.74f, 0.12f),
                new Color(0.1f, 0.4f, 0.04f),
                new Color(1f, 0.18f, 0.52f),
                new Color(1f, 0.78f, 0.08f),
                new Color(0.58f, 0.18f, 0.94f),
                new Color(1f, 0.96f, 0.86f),
                new Color(1f, 0.4f, 0.06f)
            });
            palette.Apply(false, true);

            Material material = CreateMaterial(Color.white);
            material.name = "CartoonGrassAndFlowers";
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", palette);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", palette);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.08f);
            material.enableInstancing = true;
            cachedCartoonMeadowMaterial = material;
            return cachedCartoonMeadowMaterial;
        }
    }
}
