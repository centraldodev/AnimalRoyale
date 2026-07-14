using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AnimalBattleRoyale
{
    /// <summary>Builds navigation from the colliders of the procedurally generated jungle.</summary>
    public sealed class RuntimeNavMeshSurface : MonoBehaviour
    {
        public static bool IsReady { get; private set; }

        private float worldSize = 360f;
        private NavMeshData navMeshData;
        private NavMeshDataInstance navMeshInstance;
        private bool hasInstance;

        public void Configure(float mapSize)
        {
            worldSize = Mathf.Max(40f, mapSize);
        }

        private IEnumerator Start()
        {
            // Let all procedural colliders finish registering before collecting them.
            yield return null;
            Build();
        }

        private void Build()
        {
            if (NavMesh.GetSettingsCount() == 0)
            {
                Debug.LogWarning("No NavMesh agent settings are available; bots will use local steering only.");
                return;
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.48f;
            settings.agentHeight = 1.7f;
            settings.agentClimb = 0.55f;
            settings.agentSlope = 48f;

            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>(2048);
            List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
            NavMeshBuilder.CollectSources(transform, ~0, NavMeshCollectGeometry.PhysicsColliders,
                0, markups, sources);

            Bounds bounds = new Bounds(Vector3.up * 8f, new Vector3(worldSize, 42f, worldSize));
            navMeshData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (navMeshData == null)
            {
                Debug.LogWarning("The procedural jungle NavMesh could not be generated; bots will use local steering only.");
                return;
            }

            navMeshInstance = NavMesh.AddNavMeshData(navMeshData);
            hasInstance = navMeshInstance.valid;
            IsReady = hasInstance;
        }

        private void OnDestroy()
        {
            if (hasInstance) navMeshInstance.Remove();
            hasInstance = false;
            IsReady = false;
        }
    }
}
