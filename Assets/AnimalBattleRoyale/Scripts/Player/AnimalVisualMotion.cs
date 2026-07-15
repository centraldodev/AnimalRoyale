using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Connects the package animator (Vert/State blend tree) to gameplay and adds a
    /// restrained whole-model attack pose. Bone rotations remain owned by the FBX
    /// animator, preventing the twisting that happened with procedural limb edits.
    /// </summary>
    public sealed class AnimalVisualMotion : MonoBehaviour
    {
        private static readonly int VerticalParameter = Animator.StringToHash("Vert");
        private static readonly int StateParameter = Animator.StringToHash("State");

        private AnimalType animalType;
        private Animator animator;
        private Transform modelRoot;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private bool moving;
        private bool sprinting;
        private bool airborne;
        private bool frozen;
        private float attackStartedAt = -10f;
        private float attackDuration = 0.48f;
        private float powerStartedAt = -10f;
        private float powerDuration = 0.62f;
        private Quaternion surfaceAlignment = Quaternion.identity;
        private readonly RaycastHit[] surfaceHits = new RaycastHit[12];

        public float MeleeImpactDelay => attackDuration * 0.42f;

        public void Initialize(AnimalType type)
        {
            animalType = type;
            animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.updateMode = AnimatorUpdateMode.Normal;
                modelRoot = animator.transform;
            }
            else if (transform.childCount > 0)
            {
                modelRoot = transform.GetChild(0);
            }

            if (modelRoot != null)
            {
                basePosition = modelRoot.localPosition;
                baseRotation = modelRoot.localRotation;
            }
        }

        public void SetLocomotion(bool isMoving, bool isSprinting, bool isAirborne)
        {
            moving = isMoving;
            sprinting = isSprinting;
            airborne = isAirborne;
        }

        public void TriggerAttack(bool meleeAttack = true)
        {
            attackStartedAt = Time.time;
            attackDuration = meleeAttack ? AttackDurationFor(animalType) : 0.36f;
        }

        public void TriggerPower(int slot)
        {
            powerStartedAt = Time.time;
            powerDuration = 0.64f;
        }

        // Retained for compatibility with old scene objects. Package animals do not use vines.
        public void SetVineHanging(bool hanging, bool leftHand = true) { }

        public void Freeze()
        {
            frozen = true;
            moving = false;
            if (animator != null) animator.speed = 0f;
        }

        private void Update()
        {
            if (frozen) return;
            if (animator != null)
            {
                float verticalTarget = moving ? 1f : 0f;
                float runTarget = sprinting ? 1f : 0f;
                animator.SetFloat(VerticalParameter, verticalTarget, 0.12f, Time.deltaTime);
                animator.SetFloat(StateParameter, runTarget, 0.12f, Time.deltaTime);
                animator.speed = airborne ? 0.92f : 1f;
            }
        }

        private void LateUpdate()
        {
            if (frozen || modelRoot == null) return;

            UpdateSurfaceAlignment();

            float attack = Envelope(attackStartedAt, attackDuration);
            float power = Envelope(powerStartedAt, powerDuration);
            float strongest = Mathf.Max(attack, power);
            float side = animalType is AnimalType.Chicken or AnimalType.Penguin ? 1f : -1f;
            float pitch = Mathf.Sin(strongest * Mathf.PI) * (power > attack ? -11f : -7f);
            float yaw = attack * (1f - attack) * 18f * side;
            float forward = Mathf.Sin(strongest * Mathf.PI) * (power > attack ? 0.18f : 0.1f);
            float lift = power * (1f - power) * 0.12f;

            modelRoot.localPosition = basePosition + Vector3.forward * forward + Vector3.up * lift;
            modelRoot.localRotation = baseRotation * Quaternion.Euler(pitch, yaw, 0f);
        }

        private void UpdateSurfaceAlignment()
        {
            Quaternion targetAlignment = Quaternion.identity;
            Transform owner = transform.parent;
            if (!airborne && owner != null)
            {
                Vector3 origin = owner.position + Vector3.up * 1.6f + owner.forward * 0.22f;
                int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, surfaceHits, 4.2f, ~0, QueryTriggerInteraction.Ignore);
                RaycastHit hit = default;
                bool foundSurface = false;
                for (int i = 0; i < hitCount; i++)
                {
                    Transform candidate = surfaceHits[i].transform;
                    if (candidate == null || candidate == owner || candidate.IsChildOf(owner)) continue;
                    if (!foundSurface || surfaceHits[i].distance < hit.distance)
                    {
                        hit = surfaceHits[i];
                        foundSurface = true;
                    }
                }
                if (foundSurface)
                {
                    Vector3 groundNormal = hit.normal;
                    float slope = Vector3.Angle(groundNormal, Vector3.up);
                    if (slope > 42f) groundNormal = Vector3.Slerp(Vector3.up, groundNormal, 42f / slope);
                    Vector3 slopeForward = Vector3.ProjectOnPlane(owner.forward, groundNormal).normalized;
                    if (slopeForward.sqrMagnitude > 0.001f)
                    {
                        Quaternion worldSlopeRotation = Quaternion.LookRotation(slopeForward, groundNormal);
                        targetAlignment = Quaternion.Inverse(owner.rotation) * worldSlopeRotation;
                    }
                }
            }

            surfaceAlignment = Quaternion.Slerp(surfaceAlignment, targetAlignment, 10f * Time.deltaTime);
            transform.localRotation = surfaceAlignment;
        }

        private static float Envelope(float startedAt, float duration)
        {
            if (duration <= 0f) return 0f;
            float elapsed = Time.time - startedAt;
            return elapsed >= 0f && elapsed < duration ? elapsed / duration : 0f;
        }

        private static float AttackDurationFor(AnimalType type) => type switch
        {
            AnimalType.Chicken => 0.4f,
            AnimalType.Cat => 0.42f,
            AnimalType.Dog => 0.46f,
            AnimalType.Deer => 0.52f,
            AnimalType.Horse => 0.56f,
            AnimalType.Penguin => 0.5f,
            _ => 0.52f
        };
    }
}
