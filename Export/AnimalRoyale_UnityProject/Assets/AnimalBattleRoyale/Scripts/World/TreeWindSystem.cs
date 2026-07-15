using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>One update loop drives every nearby tree, avoiding hundreds of MonoBehaviour updates.</summary>
    public sealed class TreeWindSystem : MonoBehaviour
    {
        private Transform viewer;

        private void Update()
        {
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
            // Each tree is refreshed at 30 Hz while the system itself remains smooth.
            TreeWindSway.TickVisibleTrees(viewer, Time.time, Time.frameCount & 1);
        }
    }
}
