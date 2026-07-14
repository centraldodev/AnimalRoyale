using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Updates every monkey vine marker from one centralized loop.</summary>
    public sealed class VineIndicatorSystem : MonoBehaviour
    {
        private Camera gameCamera;

        private void Update()
        {
            if (gameCamera == null) gameCamera = Camera.main;
            ThirdPersonAnimalController player = BattleRoyaleManager.Instance != null
                ? BattleRoyaleManager.Instance.LocalPlayer
                : null;
            VineAnchor.TickIndicators(player, gameCamera, Time.time);
        }
    }
}
