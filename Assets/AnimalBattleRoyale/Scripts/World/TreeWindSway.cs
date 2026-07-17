using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Cheap near-camera wind motion for Blender trees; distant trees remain static.</summary>
    public sealed class TreeWindSway : MonoBehaviour
    {
        private static readonly List<TreeWindSway> activeTrees = new List<TreeWindSway>();
        private Quaternion restingRotation;
        private Vector3 restingPosition;
        private float phase;
        private bool isSwaying;

        private void Awake()
        {
            restingRotation = transform.localRotation;
            restingPosition = transform.localPosition;
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
                if (isSwaying)
                {
                    transform.localPosition = restingPosition;
                    transform.localRotation = restingRotation;
                }
                isSwaying = false;
                return;
            }

            isSwaying = true;
            float sideSway = Mathf.Sin(time * 0.78f + phase) * 1.65f
                             + Mathf.Sin(time * 1.63f + phase * 0.73f) * 0.55f;
            float depthSway = Mathf.Sin(time * 0.61f + phase * 1.31f) * 0.9f;

            // Apply the wind in the parent/map horizontal axes before the FBX rest rotation.
            // Imported variants can have different internal axes; post-multiplying the sway made
            // some of them appear to bob vertically instead of bending sideways.
            Quaternion horizontalSway = Quaternion.AngleAxis(sideSway, Vector3.forward)
                                         * Quaternion.AngleAxis(depthSway, Vector3.right);
            transform.localPosition = restingPosition;
            transform.localRotation = horizontalSway * restingRotation;
        }
    }
}
