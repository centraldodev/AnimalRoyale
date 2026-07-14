using UnityEngine;
using UnityEngine.AI;

namespace AnimalBattleRoyale
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(ThirdPersonAnimalController))]
    public sealed class SimpleBotAI : MonoBehaviour
    {
        [SerializeField] private float visionRange = 32f;
        [SerializeField] private float thinkInterval = 0.25f;

        private ThirdPersonAnimalController controller;
        private ThirdPersonAnimalController target;
        private float nextThinkTime;
        private float nextAbilityDecision;
        private Vector3 wanderDirection;
        private float nextWanderTime;
        private NavMeshPath navigationPath;
        private readonly Vector3[] pathCorners = new Vector3[32];
        private int pathCornerCount;
        private int pathCornerIndex;
        private float nextPathRefresh;
        private Vector3 pathDestination;
        private Vector3 lastProgressPosition;
        private float nextProgressCheck;
        private Vector3 unstuckDirection;
        private float unstuckUntil;

        private void Awake()
        {
            controller = GetComponent<ThirdPersonAnimalController>();
            navigationPath = new NavMeshPath();
            lastProgressPosition = transform.position;
            nextProgressCheck = Time.time + 1.25f;
        }

        private void Update()
        {
            if (controller.IsDefeated || controller.Health.IsDead) return;

            DiamondPickup.TryCollectNearest(controller);
            MissionNode.TryUseNearest(controller);
            if (controller.Health.CurrentHealth < controller.Health.MaxHealth * 0.72f)
            {
                FoodPickup.TryConsumeNearest(controller);
            }

            if (Time.time >= nextThinkTime)
            {
                nextThinkTime = Time.time + thinkInterval;
                target = FindClosestTarget();
            }

            Vector3 desiredDirection;
            bool sprint = false;
            bool attack = false;
            int abilitySlot = -1;
            DiamondObjectiveManager objective = DiamondObjectiveManager.Instance;
            DiamondPickup objectiveDiamond = DiamondPickup.FindClosest(transform.position);
            int carriedDiamonds = objective != null ? objective.GetCount(controller) : 0;
            bool targetHasDiamonds = target != null && objective != null && objective.GetCount(target) > 0;
            float targetDistance = target != null ? Vector3.Distance(transform.position, target.transform.position) : float.MaxValue;
            bool shouldFight = target != null && (objectiveDiamond == null || (targetHasDiamonds && targetDistance < 18f));

            SafeZoneController zone = SafeZoneController.Instance;
            if (zone != null && zone.IsOutside(transform.position, 3f))
            {
                Vector3 fallback = (zone.Center - transform.position).normalized;
                desiredDirection = GetNavigationDirection(zone.Center, fallback);
                sprint = true;
            }
            else if (objective != null && carriedDiamonds >= DiamondObjectiveManager.RequiredDiamonds)
            {
                Vector3 portalPosition = objective.PortalPosition;
                Vector3 fallback = portalPosition - transform.position;
                fallback.y = 0f;
                desiredDirection = GetNavigationDirection(portalPosition, fallback.normalized);
                sprint = true;
            }
            else if (shouldFight)
            {
                Vector3 toTarget = target.transform.position - transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                Vector3 fallback = distance > 0.1f ? toTarget / distance : Vector3.zero;
                desiredDirection = GetNavigationDirection(target.transform.position, fallback);
                sprint = distance > controller.Stats.AttackRange * 1.2f;
                attack = distance <= controller.Stats.AttackRange * 1.15f;

                if (Time.time >= nextAbilityDecision && distance < 12f)
                {
                    nextAbilityDecision = Time.time + Random.Range(2.5f, 5.5f);
                    abilitySlot = Random.value > 0.38f ? 0 : -1;
                }
            }
            else if (objectiveDiamond != null)
            {
                Vector3 toDiamond = objectiveDiamond.transform.position - transform.position;
                toDiamond.y = 0f;
                desiredDirection = GetNavigationDirection(objectiveDiamond.transform.position, toDiamond.normalized);
                sprint = true;
            }
            else if (target != null)
            {
                Vector3 toTarget = target.transform.position - transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                desiredDirection = GetNavigationDirection(target.transform.position, toTarget.normalized);
                sprint = true;
                attack = distance <= controller.Stats.AttackRange * 1.15f;
            }
            else
            {
                desiredDirection = GetWanderDirection();
            }

            desiredDirection = RecoverIfStuck(desiredDirection);
            desiredDirection = AvoidObstacles(desiredDirection);
            controller.SetAIInput(desiredDirection, sprint, attack, abilitySlot);
        }

        private ThirdPersonAnimalController FindClosestTarget()
        {
            BattleRoyaleManager manager = BattleRoyaleManager.Instance;
            if (manager == null) return null;

            ThirdPersonAnimalController closest = null;
            float closestSqr = visionRange * visionRange;

            foreach (ThirdPersonAnimalController candidate in manager.Fighters)
            {
                if (candidate == null || candidate == controller || candidate.IsDefeated || candidate.Health.IsDead || candidate.IsBurrowed) continue;
                float sqr = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closest = candidate;
                }
            }

            return closest;
        }

        private Vector3 GetWanderDirection()
        {
            if (Time.time >= nextWanderTime)
            {
                nextWanderTime = Time.time + Random.Range(1.5f, 4f);
                Vector2 random = Random.insideUnitCircle.normalized;
                wanderDirection = new Vector3(random.x, 0f, random.y);
            }
            return wanderDirection;
        }

        private Vector3 AvoidObstacles(Vector3 desired)
        {
            if (desired.sqrMagnitude < 0.01f) return desired;

            Vector3 origin = transform.position + Vector3.up * 0.6f;
            if (Physics.SphereCast(origin, 0.35f, desired, out RaycastHit hit, 2.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                {
                    Vector3 side = Vector3.Cross(Vector3.up, hit.normal).normalized;
                    if (Vector3.Dot(side, desired) < 0f) side = -side;
                    desired = (desired + side * 1.4f).normalized;
                }
            }
            return desired;
        }

        private Vector3 GetNavigationDirection(Vector3 destination, Vector3 fallback)
        {
            if (!RuntimeNavMeshSurface.IsReady) return fallback;

            bool destinationChanged = (destination - pathDestination).sqrMagnitude > 9f;
            if (destinationChanged || Time.time >= nextPathRefresh || pathCornerCount < 2)
            {
                nextPathRefresh = Time.time + Random.Range(0.45f, 0.75f);
                pathDestination = destination;
                pathCornerIndex = 1;

                if (!NavMesh.SamplePosition(transform.position, out NavMeshHit start, 5f, NavMesh.AllAreas)
                    || !NavMesh.SamplePosition(destination, out NavMeshHit end, 8f, NavMesh.AllAreas)
                    || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, navigationPath))
                {
                    pathCornerCount = 0;
                    return fallback;
                }
                pathCornerCount = navigationPath.GetCornersNonAlloc(pathCorners);
            }

            if (pathCornerCount < 2) return fallback;
            while (pathCornerIndex < pathCornerCount - 1
                   && (pathCorners[pathCornerIndex] - transform.position).sqrMagnitude < 2.25f)
            {
                pathCornerIndex++;
            }

            Vector3 toCorner = pathCorners[Mathf.Clamp(pathCornerIndex, 0, pathCornerCount - 1)] - transform.position;
            toCorner.y = 0f;
            return toCorner.sqrMagnitude > 0.01f ? toCorner.normalized : fallback;
        }

        private Vector3 RecoverIfStuck(Vector3 desired)
        {
            if (Time.time >= nextProgressCheck)
            {
                bool tryingToMove = desired.sqrMagnitude > 0.05f;
                bool barelyMoved = (transform.position - lastProgressPosition).sqrMagnitude < 0.2f;
                if (tryingToMove && barelyMoved)
                {
                    float turn = Random.value > 0.5f ? 82f : -82f;
                    unstuckDirection = Quaternion.Euler(0f, turn, 0f) * desired.normalized;
                    unstuckUntil = Time.time + 1.1f;
                    nextPathRefresh = 0f;
                }
                lastProgressPosition = transform.position;
                nextProgressCheck = Time.time + 1.25f;
            }

            return Time.time < unstuckUntil
                ? (desired + unstuckDirection * 1.8f).normalized
                : desired;
        }
    }
}
