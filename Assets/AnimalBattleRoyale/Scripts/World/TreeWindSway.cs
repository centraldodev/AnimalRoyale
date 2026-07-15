using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Cheap near-camera wind motion for Blender trees; distant trees remain static.</summary>
    public sealed class TreeWindSway : MonoBehaviour
    {
        private static readonly List<TreeWindSway> activeTrees = new List<TreeWindSway>();
        private Quaternion restingRotation;
        private float phase;
        private bool isSwaying;

        private void Awake()
        {
            restingRotation = transform.localRotation;
            phase = Random.value * Mathf.PI * 2f;
        }

        private void OnEnable()
        {
            if (!activeTrees.Contains(this)) activeTrees.Add(this);
        }

        private void OnDisable()
        {
            activeTrees.Remove(this);
        }

        internal static void TickVisibleTrees(Transform viewer, float time, int frameGroup)
        {
            for (int i = frameGroup; i < activeTrees.Count; i += 2)
            {
                TreeWindSway tree = activeTrees[i];
                if (tree != null) tree.Tick(viewer, time);
            }
        }

        private void Tick(Transform viewer, float time)
        {
            bool closeEnough = viewer == null || (viewer.position - transform.position).sqrMagnitude <= 72f * 72f;
            if (!closeEnough)
            {
                if (isSwaying) transform.localRotation = restingRotation;
                isSwaying = false;
                return;
            }

            isSwaying = true;
            float gust = Mathf.Sin(time * 0.78f + phase) * 1.7f + Mathf.Sin(time * 1.63f + phase) * 0.65f;
            transform.localRotation = restingRotation * Quaternion.Euler(gust, 0f, gust * 0.55f);
        }
    }
}
