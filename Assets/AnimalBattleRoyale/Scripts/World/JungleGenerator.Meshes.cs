using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed partial class JungleGenerator
    {
        public static Mesh GetCrystalMesh()
        {
            if (cachedCrystalMesh != null) return cachedCrystalMesh;

            const int sides = 6;
            List<Vector3> vertices = new List<Vector3>(sides * 18);
            List<int> triangles = new List<int>(sides * 18);
            Vector3 top = new Vector3(0f, 0.62f, 0f);
            Vector3 bottom = new Vector3(0f, -0.52f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float angleA = i * Mathf.PI * 2f / sides;
                float angleB = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 lowerA = new Vector3(Mathf.Cos(angleA) * 0.31f, -0.31f, Mathf.Sin(angleA) * 0.31f);
                Vector3 lowerB = new Vector3(Mathf.Cos(angleB) * 0.31f, -0.31f, Mathf.Sin(angleB) * 0.31f);
                Vector3 upperA = new Vector3(Mathf.Cos(angleA) * 0.31f, 0.25f, Mathf.Sin(angleA) * 0.31f);
                Vector3 upperB = new Vector3(Mathf.Cos(angleB) * 0.31f, 0.25f, Mathf.Sin(angleB) * 0.31f);
                AddFlatQuad(vertices, triangles, lowerA, upperA, upperB, lowerB);
                AddFlatTriangle(vertices, triangles, upperA, top, upperB);
                AddFlatTriangle(vertices, triangles, lowerA, lowerB, bottom);
            }

            cachedCrystalMesh = new Mesh { name = "FacetedCartoonCrystal" };
            cachedCrystalMesh.SetVertices(vertices);
            cachedCrystalMesh.SetTriangles(triangles, 0);
            cachedCrystalMesh.RecalculateNormals();
            cachedCrystalMesh.RecalculateBounds();
            return cachedCrystalMesh;
        }

        private static Mesh GetLakeDiscMesh()
        {
            if (cachedLakeDiscMesh != null) return cachedLakeDiscMesh;
            const int segments = 96;
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int next = (i + 1) % segments;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = next + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            cachedLakeDiscMesh = new Mesh { name = "CentralLakeDisc" };
            cachedLakeDiscMesh.vertices = vertices;
            cachedLakeDiscMesh.triangles = triangles;
            cachedLakeDiscMesh.RecalculateNormals();
            cachedLakeDiscMesh.RecalculateBounds();
            return cachedLakeDiscMesh;
        }

        private static Mesh GetLakeShoreMesh()
        {
            if (cachedLakeShoreMesh != null) return cachedLakeShoreMesh;
            const int segments = 96;
            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float wobble = 1f + Mathf.Sin(i * 1.91f) * 0.025f + Mathf.Sin(i * 0.47f) * 0.018f;
                vertices[i * 2] = new Vector3(Mathf.Cos(angle) * 0.92f, 0f, Mathf.Sin(angle) * 0.92f);
                vertices[i * 2 + 1] = new Vector3(Mathf.Cos(angle) * 1.14f * wobble, 0f, Mathf.Sin(angle) * 1.14f * wobble);
                int next = (i + 1) % segments;
                int index = i * 6;
                triangles[index] = i * 2;
                triangles[index + 1] = next * 2;
                triangles[index + 2] = i * 2 + 1;
                triangles[index + 3] = next * 2;
                triangles[index + 4] = next * 2 + 1;
                triangles[index + 5] = i * 2 + 1;
            }
            cachedLakeShoreMesh = new Mesh { name = "IrregularSandyLakeShore" };
            cachedLakeShoreMesh.vertices = vertices;
            cachedLakeShoreMesh.triangles = triangles;
            cachedLakeShoreMesh.RecalculateNormals();
            cachedLakeShoreMesh.RecalculateBounds();
            return cachedLakeShoreMesh;
        }

        private static Mesh GetBackdropMountainMesh()
        {
            if (cachedBackdropMountainMesh != null) return cachedBackdropMountainMesh;

            const int sides = 9;
            List<Vector3> vertices = new List<Vector3>(sides * 30);
            List<int> rockTriangles = new List<int>(sides * 24);
            List<int> peakTriangles = new List<int>(sides * 6);
            Vector3 top = new Vector3(0.06f, 1f, -0.04f);
            for (int i = 0; i < sides; i++)
            {
                float angleA = i * Mathf.PI * 2f / sides;
                float angleB = (i + 1) * Mathf.PI * 2f / sides;
                float wobbleA = 1f + Mathf.Sin(i * 2.17f) * 0.12f;
                float wobbleB = 1f + Mathf.Sin((i + 1) * 2.17f) * 0.12f;
                Vector3 baseA = new Vector3(Mathf.Cos(angleA) * 0.52f * wobbleA, 0f, Mathf.Sin(angleA) * 0.52f * wobbleA);
                Vector3 baseB = new Vector3(Mathf.Cos(angleB) * 0.52f * wobbleB, 0f, Mathf.Sin(angleB) * 0.52f * wobbleB);
                Vector3 middleA = new Vector3(Mathf.Cos(angleA) * 0.34f * wobbleB, 0.48f, Mathf.Sin(angleA) * 0.34f * wobbleB);
                Vector3 middleB = new Vector3(Mathf.Cos(angleB) * 0.34f * wobbleA, 0.48f, Mathf.Sin(angleB) * 0.34f * wobbleA);
                Vector3 highA = new Vector3(Mathf.Cos(angleA) * 0.16f, 0.76f, Mathf.Sin(angleA) * 0.16f);
                Vector3 highB = new Vector3(Mathf.Cos(angleB) * 0.16f, 0.76f, Mathf.Sin(angleB) * 0.16f);
                AddFlatQuad(vertices, rockTriangles, baseA, middleA, middleB, baseB);
                AddFlatQuad(vertices, rockTriangles, middleA, highA, highB, middleB);
                AddFlatTriangle(vertices, peakTriangles, highA, top, highB);
            }

            cachedBackdropMountainMesh = new Mesh { name = "FacetedBackdropMountain" };
            cachedBackdropMountainMesh.SetVertices(vertices);
            cachedBackdropMountainMesh.subMeshCount = 2;
            cachedBackdropMountainMesh.SetTriangles(rockTriangles, 0);
            cachedBackdropMountainMesh.SetTriangles(peakTriangles, 1);
            cachedBackdropMountainMesh.RecalculateNormals();
            cachedBackdropMountainMesh.RecalculateBounds();
            return cachedBackdropMountainMesh;
        }

        private static void AddFlatQuad(List<Vector3> vertices, List<int> triangles,
            Vector3 lowerA, Vector3 upperA, Vector3 upperB, Vector3 lowerB)
        {
            AddFlatTriangle(vertices, triangles, lowerA, upperA, upperB);
            AddFlatTriangle(vertices, triangles, lowerA, upperB, lowerB);
        }

        private static void AddFlatTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

    }
}
