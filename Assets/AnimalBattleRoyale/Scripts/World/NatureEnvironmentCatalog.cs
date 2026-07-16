using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Loads legacy binary Tree Creator prefabs by path. This avoids serializing their
    /// unstable internal file IDs, which changed when Unity upgraded the old package.
    /// </summary>
    public sealed class NatureEnvironmentCatalog : ScriptableObject
    {
        [SerializeField] private string[] treeResourcePaths;
        [SerializeField] private string[] bushResourcePaths;
        [SerializeField] private Texture2D grassGround;
        [SerializeField] private Texture2D dryGround;
        [SerializeField] private Texture2D dirtGround;
        [SerializeField] private Texture2D[] grassDetails;

        private GameObject[] loadedTrees;
        private GameObject[] loadedBushes;

        public bool HasTrees => GetTrees().Length > 0;
        public bool HasBushes => GetBushes().Length > 0;
        public Texture2D GrassGround => grassGround;
        public Texture2D DryGround => dryGround;
        public Texture2D DirtGround => dirtGround;

        public GameObject GetRandomTree() => GetRandom(GetTrees());
        public GameObject GetRandomBush() => GetRandom(GetBushes());

        public Texture2D GetGrassDetail(int index)
        {
            if (grassDetails == null || grassDetails.Length == 0) return null;
            return grassDetails[Mathf.Abs(index) % grassDetails.Length];
        }

        private static T GetRandom<T>(T[] values) where T : Object
        {
            if (values == null || values.Length == 0) return null;
            return values[Random.Range(0, values.Length)];
        }

        private GameObject[] GetTrees()
        {
            loadedTrees ??= LoadPrefabs(treeResourcePaths);
            return loadedTrees;
        }

        private GameObject[] GetBushes()
        {
            loadedBushes ??= LoadPrefabs(bushResourcePaths);
            return loadedBushes;
        }

        private static GameObject[] LoadPrefabs(string[] resourcePaths)
        {
            if (resourcePaths == null || resourcePaths.Length == 0)
                return System.Array.Empty<GameObject>();

            List<GameObject> prefabs = new List<GameObject>(resourcePaths.Length);
            foreach (string path in resourcePaths)
            {
                // The non-generic overload asks Unity's native asset database for the correct
                // type before the managed cast, preventing InvalidCastException on old prefabs.
                Object asset = Resources.Load(path, typeof(GameObject));
                if (asset is GameObject prefab)
                    prefabs.Add(prefab);
                else
                    Debug.LogWarning($"Prefab de vegetação não pôde ser carregado como GameObject: {path}");
            }
            return prefabs.ToArray();
        }
    }
}
