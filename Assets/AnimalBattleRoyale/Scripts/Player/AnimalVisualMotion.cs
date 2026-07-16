using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Drives the generated animal rig directly from gameplay state. The imported
    /// meshes expose smooth-weighted head, arm/wing and leg bones, so locomotion is
    /// visible in the silhouette without depending on authored animation clips.
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
        private Vector3 baseScale;
        private bool moving;
        private bool sprinting;
        private bool airborne;
        private bool flying;
        private bool frozen;
        private float verticalSpeed;
        private float locomotionPhase;
        private float locomotionBlend;
        private float airborneBlend;
        private float attackStartedAt = -10f;
        private float attackDuration = 0.48f;
        private float powerStartedAt = -10f;
        private float powerDuration = 0.62f;
        private float shotStartedAt = -10f;
        private const float ShotRecoilDuration = 0.16f;
        private Quaternion surfaceAlignment = Quaternion.identity;
        private readonly RaycastHit[] surfaceHits = new RaycastHit[12];
        private RigBone bodyBone;
        private RigBone headBone;
        private RigBone leftArmBone;
        private RigBone rightArmBone;
        private RigBone leftLegBone;
        private RigBone rightLegBone;
        private RigBone tailBone;
        private RigBone leftAntennaBone;
        private RigBone rightAntennaBone;

        private sealed class RigBone
        {
            public Transform Transform;
            public Quaternion RestRotation;
        }

        public float MeleeImpactDelay => attackDuration * 0.42f;

        public void Initialize(AnimalType type)
        {
            animalType = type;
            animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null)
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
                baseScale = modelRoot.localScale;
                CacheRigBones();
            }
        }

        public void SetLocomotion(bool isMoving, bool isSprinting, bool isAirborne,
            float currentVerticalSpeed = 0f, bool isFlying = false)
        {
            moving = isMoving;
            sprinting = isSprinting;
            airborne = isAirborne;
            verticalSpeed = currentVerticalSpeed;
            flying = isFlying;
        }

        public void TriggerAttack(bool meleeAttack = true)
        {
            if (!meleeAttack)
            {
                shotStartedAt = Time.time;
                return;
            }
            attackStartedAt = Time.time;
            attackDuration = AttackDurationFor(animalType);
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
            locomotionBlend = Mathf.MoveTowards(locomotionBlend, moving && !airborne ? 1f : 0f,
                Time.deltaTime * (moving ? 8f : 6f));
            airborneBlend = Mathf.MoveTowards(airborneBlend, airborne ? 1f : 0f, Time.deltaTime * 10f);
            if (locomotionBlend > 0.001f)
            {
                float cadence = sprinting ? 11.5f : 7.4f;
                if (animalType == AnimalType.Ant) cadence *= 1.3f;
                locomotionPhase += Time.deltaTime * cadence;
            }

            if (animator != null && animator.runtimeAnimatorController != null)
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
            float side = animalType is AnimalType.Eagle ? 1f : -1f;
            float actionWave = Mathf.Sin(strongest * Mathf.PI);

            float stride = Mathf.Sin(locomotionPhase);
            float step = Mathf.Abs(stride);
            float groundedMotion = locomotionBlend * (1f - airborneBlend);
            float bob = step * WalkBobFor(animalType) * groundedMotion;
            float walkRoll = stride * WalkRollFor(animalType) * groundedMotion;
            float walkPitch = Mathf.Sin(locomotionPhase * 2f) * WalkPitchFor(animalType) * groundedMotion;

            float idleAmount = (1f - locomotionBlend) * (1f - airborneBlend);
            float breath = Mathf.Sin(Time.time * 2.25f + (int)animalType * 0.7f) * 0.008f * idleAmount;

            float verticalNormalized = Mathf.Clamp(verticalSpeed / 9f, -1f, 1f);
            float jumpPitch = airborneBlend * (flying ? 3f : -verticalNormalized * 8f);
            float jumpLift = airborneBlend * (flying ? 0.035f : 0.055f);

            float flapWave = Mathf.Sin(Time.time * 13.5f);
            float flapAmount = animalType == AnimalType.Eagle && flying ? airborneBlend : 0f;
            float flapLift = Mathf.Abs(flapWave) * 0.02f * flapAmount;
            float flapRoll = flapWave * 1.5f * flapAmount;

            float shot = ShotRecoilEnvelope();
            float actionPitch = actionWave * (power > attack ? -11f : -7f);
            float actionYaw = attack * (1f - attack) * 18f * side;
            float actionForward = actionWave * (power > attack ? 0.18f : 0.1f);
            float actionLift = power * (1f - power) * 0.12f;

            Vector3 scaleMultiplier = Vector3.one;
            float stepSquash = step * groundedMotion * 0.028f;
            scaleMultiplier.x += stepSquash;
            scaleMultiplier.y += breath - stepSquash * 0.8f + verticalNormalized * airborneBlend * 0.025f;
            scaleMultiplier.z += stepSquash * 0.35f - shot * 0.025f;
            if (animalType == AnimalType.Eagle && airborneBlend > 0.001f)
            {
                scaleMultiplier = Vector3.one;
                scaleMultiplier.y += breath * 0.35f;
            }

            modelRoot.localPosition = basePosition
                                      + Vector3.up * (bob + breath * 0.5f + jumpLift + flapLift + actionLift)
                                      + Vector3.forward * (actionForward - shot * 0.115f);
            modelRoot.localRotation = baseRotation * Quaternion.Euler(
                actionPitch + walkPitch + jumpPitch + shot * 7.5f,
                actionYaw,
                walkRoll + flapRoll);
            modelRoot.localScale = Vector3.Scale(baseScale, scaleMultiplier);

            ApplyRigMotion(stride, groundedMotion, actionWave, power, shot, verticalNormalized);
        }

        private void CacheRigBones()
        {
            bodyBone = FindRigBone("Body");
            headBone = FindRigBone("Head");
            leftArmBone = FindRigBone(animalType == AnimalType.Eagle ? "Wing_L" : "Arm_L");
            rightArmBone = FindRigBone(animalType == AnimalType.Eagle ? "Wing_R" : "Arm_R");
            leftLegBone = FindRigBone("Leg_L");
            rightLegBone = FindRigBone("Leg_R");
            tailBone = FindRigBone("Tail");
            leftAntennaBone = FindRigBone("Antenna_L");
            rightAntennaBone = FindRigBone("Antenna_R");
        }

        private RigBone FindRigBone(string boneName)
        {
            foreach (Transform child in modelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != boneName && !child.name.EndsWith("|" + boneName)) continue;
                return new RigBone { Transform = child, RestRotation = child.localRotation };
            }
            return null;
        }

        private void ApplyRigMotion(float stride, float groundedMotion, float actionWave,
            float power, float shot, float verticalNormalized)
        {
            float runMultiplier = sprinting ? 1.18f : 1f;
            float legSwing = stride * 28f * groundedMotion * runMultiplier;
            float armSwing = stride * 21f * groundedMotion * runMultiplier;
            float jumpTuck = airborneBlend * (flying ? 7f : 22f);
            float actionReach = actionWave * (power > 0f ? 14f : 9f);

            SetBoneRotation(leftLegBone, new Vector3(legSwing + jumpTuck, 0f, 0f));
            SetBoneRotation(rightLegBone, new Vector3(-legSwing + jumpTuck, 0f, 0f));

            if (animalType == AnimalType.Eagle)
            {
                float airborneSpread = airborneBlend * (flying ? 1f : 0.55f);
                float flap = Mathf.Sin(Time.time * (flying ? 9.2f : 7.4f));
                float openAmount = Mathf.SmoothStep(0f, 1f, (flap + 1f) * 0.5f);
                float liftAngle = airborneSpread * openAmount * (flying ? 28f : 18f);
                float groundSway = armSwing * 0.32f;
                SetBoneRotation(leftArmBone, new Vector3(-groundSway, 0f, liftAngle));
                SetBoneRotation(rightArmBone, new Vector3(groundSway, 0f, -liftAngle));
            }
            else
            {
                float aerialArmLift = airborneBlend * 10f;
                SetBoneRotation(leftArmBone,
                    new Vector3(-armSwing + aerialArmLift - actionReach - shot * 3f, 0f, 0f));
                SetBoneRotation(rightArmBone,
                    new Vector3(armSwing + aerialArmLift - actionReach - shot * 5f, 0f, 0f));
            }

            float headPitch = -verticalNormalized * airborneBlend * 5f - shot * 2.2f;
            float headYaw = stride * groundedMotion * 2.2f;
            float headRoll = -stride * groundedMotion * 2.8f;
            SetBoneRotation(headBone, new Vector3(headPitch, headYaw, headRoll));
            SetBoneRotation(bodyBone, new Vector3(actionWave * -3f + shot * 2.5f, 0f, 0f));

            float idleTail = Mathf.Sin(Time.time * 2.1f) * (1f - locomotionBlend) * 4f;
            SetBoneRotation(tailBone, new Vector3(0f, 0f, idleTail + stride * groundedMotion * 11f));

            float antennaWave = Mathf.Sin(Time.time * 4.5f + locomotionPhase * 0.35f);
            float antennaStrength = 3f + locomotionBlend * 5f + airborneBlend * 3f;
            SetBoneRotation(leftAntennaBone, new Vector3(0f, 0f, antennaWave * antennaStrength));
            SetBoneRotation(rightAntennaBone, new Vector3(0f, 0f, -antennaWave * antennaStrength));
        }

        private static void SetBoneRotation(RigBone bone, Vector3 eulerOffset)
        {
            if (bone?.Transform == null) return;
            bone.Transform.localRotation = bone.RestRotation * Quaternion.Euler(eulerOffset);
        }

        private void UpdateSurfaceAlignment()
        {
            Quaternion targetAlignment = Quaternion.identity;
            Transform owner = transform.parent;
            // Only tilt the model to the ground slope while actually moving. When the
            // animal is standing still the down-ray can flip between overlapping
            // colliders on the dense procedural map, which made a stationary animal
            // rock back and forth like it was rolling over speed bumps. A still animal
            // now settles to a stable upright pose.
            if (!airborne && moving && owner != null)
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

        private float ShotRecoilEnvelope()
        {
            float elapsed = Time.time - shotStartedAt;
            if (elapsed < 0f || elapsed >= ShotRecoilDuration) return 0f;
            float progress = elapsed / ShotRecoilDuration;
            return Mathf.Sin(progress * Mathf.PI) * (1f - progress * 0.35f);
        }

        private static float WalkBobFor(AnimalType type) => type switch
        {
            AnimalType.Ant => 0.035f,
            AnimalType.Monkey => 0.06f,
            AnimalType.Eagle => 0.042f,
            _ => 0.052f
        };

        private static float WalkRollFor(AnimalType type) => type switch
        {
            AnimalType.Ant => 4.2f,
            AnimalType.Monkey => 5.2f,
            AnimalType.Eagle => 3.4f,
            _ => 4.4f
        };

        private static float WalkPitchFor(AnimalType type) => type switch
        {
            AnimalType.Ant => 2.2f,
            AnimalType.Monkey => 3.2f,
            AnimalType.Eagle => 2.4f,
            _ => 2.8f
        };

        private static float AttackDurationFor(AnimalType type) => type switch
        {
            AnimalType.Ant => 0.42f,
            AnimalType.Monkey => 0.46f,
            AnimalType.Eagle => 0.5f,
            AnimalType.Tiger => 0.52f,
            _ => 0.52f
        };
    }
}
