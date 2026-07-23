using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Owns the player-bound crystal economy and the single escape victory condition.</summary>
    public sealed class DiamondObjectiveManager : MonoBehaviour
    {
        public const int MaxPlayers = 30;
        public static DiamondObjectiveManager Instance { get; private set; }
        public static int RequiredDiamonds => Instance != null ? Instance.requiredDiamonds : 0;

        private readonly Dictionary<ThirdPersonAnimalController, int> carried = new Dictionary<ThirdPersonAnimalController, int>();
        private JungleGenerator jungle;
        private float nextSafetyCheck;
        private bool initialized;
        private int requiredDiamonds;

        // Escape portal removed (see Initialize below); kept as a fixed point so the
        // minimap and bot navigation, which still read this, have a stable target.
        public Vector3 PortalPosition => Vector3.zero;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(JungleGenerator generatedJungle)
        {
            if (initialized) return;
            initialized = true;
            jungle = generatedJungle;
            Physics.SyncTransforms();
            // Escape portal removed: the match is now won by being the last fighter standing
            // (3-lives battle royale), so no portal or crystal-escape objective is created.

            BattleRoyaleManager manager = BattleRoyaleManager.Instance;
            if (manager == null) return;
            foreach (ThirdPersonAnimalController fighter in manager.Fighters) RegisterFighter(fighter);
        }

        public void RegisterFighter(ThirdPersonAnimalController fighter)
        {
            if (fighter == null || carried.ContainsKey(fighter) || requiredDiamonds >= MaxPlayers) return;
            carried.Add(fighter, 1);
            requiredDiamonds++;
        }

        public int GetCount(ThirdPersonAnimalController fighter)
        {
            return fighter != null && carried.TryGetValue(fighter, out int count) ? count : 0;
        }

        public void Collect(ThirdPersonAnimalController fighter, DiamondPickup pickup)
        {
            if (fighter == null || pickup == null) return;
            carried[fighter] = Mathf.Min(RequiredDiamonds, GetCount(fighter) + 1);
            AttackVfx.CreateBurst(pickup.transform.position, new Color(0.08f, 0.78f, 1f), 2.1f);
            CombatFeedback.PlayDiamond(pickup.transform.position);
            pickup.RemoveFromWorld();
        }

        public void DropAll(ThirdPersonAnimalController fighter, Vector3 deathPosition)
        {
            int count = GetCount(fighter);
            if (count <= 0) return;
            carried[fighter] = 0;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / Mathf.Max(1, count);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (1.3f + (i % 3) * 0.35f);
                Vector3 position = GetSafeDropPosition(deathPosition + offset);
                SpawnDiamond(position);
            }
        }

        public bool TryEscape(ThirdPersonAnimalController fighter)
        {
            if (fighter == null || fighter.IsDefeated || fighter.Health.IsDead || GetCount(fighter) < RequiredDiamonds) return false;
            CombatFeedback.PlayPortal(PortalPosition);
            AttackVfx.CreateBurst(PortalPosition + Vector3.up * 2.4f, new Color(0.4f, 0.32f, 1f), 4.8f);
            BattleRoyaleManager.Instance?.CompleteEscape(fighter);
            return true;
        }

        private void Update()
        {
            if (!initialized || Time.time < nextSafetyCheck) return;
            nextSafetyCheck = Time.time + 0.75f;
            SafeZoneController zone = SafeZoneController.Instance;
            if (zone != null)
            {
                foreach (DiamondPickup pickup in DiamondPickup.ActivePickups)
                {
                    if (pickup != null && pickup.IsAvailable && zone.IsOutside(pickup.transform.position, 1.5f))
                    {
                        pickup.Relocate(GetSafeDropPosition(pickup.transform.position));
                    }
                }
            }
        }

        private Vector3 GetSafeDropPosition(Vector3 position)
        {
            SafeZoneController zone = SafeZoneController.Instance;
            if (zone != null)
            {
                Vector3 fromCenter = position - zone.Center;
                fromCenter.y = 0f;
                float safeRadius = Mathf.Max(2f, zone.CurrentRadius - 3f);
                if (fromCenter.sqrMagnitude > safeRadius * safeRadius)
                {
                    Vector3 direction = fromCenter.sqrMagnitude > 0.01f ? fromCenter.normalized : Vector3.forward;
                    position = zone.Center + direction * safeRadius;
                }
            }
            position = ClampAwayFromLake(position);
            return jungle.GetGroundPosition(position);
        }

        // A fighter can die while swimming; keep dropped diamonds out of the lake.
        private Vector3 ClampAwayFromLake(Vector3 position)
        {
            if (jungle == null) return position;
            float minDistance = jungle.LakeRadius + 4f;
            Vector2 planar = new Vector2(position.x, position.z);
            float distance = planar.magnitude;
            if (distance >= minDistance) return position;
            Vector2 direction = distance > 0.01f ? planar / distance : Vector2.right;
            Vector2 safePoint = direction * minDistance;
            return new Vector3(safePoint.x, position.y, safePoint.y);
        }

        private static void SpawnDiamond(Vector3 position)
        {
            DiamondPickup.Create(position);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
