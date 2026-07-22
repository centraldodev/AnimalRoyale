using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Poses the animal's rig procedurally instead of playing animation clips. Reusing
    /// the Tiger/Ant clips across species via Humanoid retargeting distorted bones that
    /// aren't part of the humanoid mapping (tails, etc.), so this rotates only a small,
    /// explicit set of limb bones by hand, relative to their imported rest pose, and
    /// never touches anything else (tail/wing/mandible/ears stay exactly as imported).
    /// Root movement is fully owned by <see cref="ThirdPersonAnimalController"/> via
    /// CharacterController — this component only adjusts bones in place.
    /// </summary>
    public sealed class AnimalVisualMotion : MonoBehaviour
    {
        private const float WalkFrequency = 2.1f;
        private const float RunFrequency = 3.3f;
        private const float LegSwingDegrees = 11f;
        private const float RunSwingMultiplier = 1.4f;
        private const float ArmSwingDegrees = 6f;
        private const float AirborneTuckDegrees = 14f;
        private const float BlendSpeed = 5f;
        private const float WingFlapFrequency = 2.4f;
        private const float WingFlapDegrees = 55f;
        private const float WingFlapBlendSpeed = 6f;

        public float MeleeImpactDelay => 0f;

        private const string LocomotionControllerPath = "Animation/AnimalLocomotion";

        private Transform leftThigh, rightThigh, leftUpperarm, rightUpperarm;
        private Quaternion leftThighRest, rightThighRest, leftUpperarmRest, rightUpperarmRest;
        private Transform leftHandBone, rightHandBone;
        private Transform activeHandBone;
        private Animator humanoidAnimator;
        private bool useHumanoidAnimation;
        private float animatorSpeedVelocity;

        private bool isMoving, isSprinting, isAirborne, isFlying, hanging, frozen;
        private float gaitPhase;
        private float locomotionBlend;
        private float airborneBlend;
        private float sprintBlend;
        private Vector3 handAimTarget;

        private Transform leftWing, rightWing;
        private Quaternion leftWingRest, rightWingRest;
        private Transform[] leftWingChain, rightWingChain;
        private Quaternion[] leftWingChainRest, rightWingChainRest;
        private float wingFlapPhase;
        private float wingFlapBlend;

        public void Initialize(AnimalType type)
        {
            transform.localRotation = Quaternion.identity;

            leftHandBone = FindBone(transform, "L_Hand");
            rightHandBone = FindBone(transform, "R_Hand");

            // Eagle's rig doesn't use the shared L_/R_ bone names (see EagleHumanoidRigSetup) —
            // its wing roots are these generic auto-rig names instead. The whole chain (not
            // just the root) is mapped to Humanoid arm bones purely so Mixamo clips retarget
            // at all, so the rest of the chain needs to be pinned to rest too, or the Run
            // clip's arm-swing visibly drags the wingtip forward even when not flying.
            if (type == AnimalType.Eagle)
            {
                leftWing = FindBone(transform, "bone_5");
                rightWing = FindBone(transform, "bone_12");
                if (leftWing != null) leftWingRest = leftWing.localRotation;
                if (rightWing != null) rightWingRest = rightWing.localRotation;

                leftWingChain = new[] { FindBone(transform, "bone_6"), FindBone(transform, "bone_7") };
                rightWingChain = new[] { FindBone(transform, "bone_13"), FindBone(transform, "bone_14") };
                leftWingChainRest = System.Array.ConvertAll(leftWingChain, b => b != null ? b.localRotation : Quaternion.identity);
                rightWingChainRest = System.Array.ConvertAll(rightWingChain, b => b != null ? b.localRotation : Quaternion.identity);
            }

            // Prefer a properly rigged Humanoid avatar (real walk/run/jump clips retargeted
            // from Mixamo) when the model has one; otherwise fall back to hand-swung limb
            // bones so animals without a Humanoid setup still move instead of sliding.
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
            {
                RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(LocomotionControllerPath);
                if (controller != null)
                {
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.enabled = true;
                    humanoidAnimator = animator;
                    useHumanoidAnimation = true;
                }
            }

            if (!useHumanoidAnimation)
            {
                leftThigh = FindBone(transform, "L_Thigh");
                rightThigh = FindBone(transform, "R_Thigh");
                leftUpperarm = FindBone(transform, "L_Upperarm");
                rightUpperarm = FindBone(transform, "R_Upperarm");

                if (leftThigh != null) leftThighRest = leftThigh.localRotation;
                if (rightThigh != null) rightThighRest = rightThigh.localRotation;
                if (leftUpperarm != null) leftUpperarmRest = leftUpperarm.localRotation;
                if (rightUpperarm != null) rightUpperarmRest = rightUpperarm.localRotation;

                // No clip/retargeting involved for this animal — keep any imported Animator
                // off so it can never fight the procedural bone rotations below.
                foreach (Animator disabled in GetComponentsInChildren<Animator>(true))
                {
                    disabled.runtimeAnimatorController = null;
                    disabled.applyRootMotion = false;
                    disabled.enabled = false;
                }
            }
        }

        public void SetLocomotion(bool moving, bool sprinting, bool airborne,
            float currentVerticalSpeed = 0f, bool flying = false, bool jumped = false)
        {
            isMoving = moving;
            isSprinting = sprinting;
            isAirborne = airborne;
            isFlying = flying;

            if (!useHumanoidAnimation || humanoidAnimator == null) return;
            // Airborne (flying, pouncing, jumping) never blends toward the ground Walk/Run
            // poses even while movement input is held — otherwise holding forward mid-air
            // shows the running-legs pose instead of the wing flap / a neutral airborne pose.
            float targetSpeed = !moving || airborne ? 0f : sprinting ? 2f : 1f;
            float current = humanoidAnimator.GetFloat("Speed");
            humanoidAnimator.SetFloat("Speed",
                Mathf.SmoothDamp(current, targetSpeed, ref animatorSpeedVelocity, 0.12f));
            humanoidAnimator.SetBool("Grounded", !airborne);
            // Jump has no dedicated animation for now (disabled per request) — the Animator
            // stays on the Locomotion blend tree through the whole jump, so it's just the
            // plain physics arc with no clip transition.
        }

        public void TriggerAttack(bool meleeAttack = true) { }

        public void TriggerPower(int slot) { }

        public void SetVineHanging(bool isHanging, bool leftHand = true)
        {
            hanging = isHanging;
            activeHandBone = isHanging ? (leftHand ? leftHandBone : rightHandBone) : null;
        }

        public void SetHandAimTarget(Vector3 worldPosition)
        {
            handAimTarget = worldPosition;
        }

        public void Freeze() => frozen = true;

        public void Unfreeze() => frozen = false;

        private void LateUpdate()
        {
            UpdateLocomotionPose();
            UpdateHandAim();
            UpdateWingFlap();
        }

        private void UpdateLocomotionPose()
        {
            if (useHumanoidAnimation) return;
            float dt = Time.deltaTime;

            float airborneTarget = !frozen && isAirborne ? 1f : 0f;
            airborneBlend = Mathf.MoveTowards(airborneBlend, airborneTarget, dt * BlendSpeed);

            float locomotionTarget = !frozen && isMoving && !isAirborne && !hanging ? 1f : 0f;
            locomotionBlend = Mathf.MoveTowards(locomotionBlend, locomotionTarget, dt * BlendSpeed);

            float sprintTarget = isSprinting ? 1f : 0f;
            sprintBlend = Mathf.MoveTowards(sprintBlend, sprintTarget, dt * 3f);

            if (locomotionBlend > 0.001f && !frozen)
            {
                float frequency = Mathf.Lerp(WalkFrequency, RunFrequency, sprintBlend);
                gaitPhase += dt * frequency * Mathf.PI * 2f;
            }

            float amplitude = Mathf.Lerp(LegSwingDegrees, LegSwingDegrees * RunSwingMultiplier, sprintBlend) * locomotionBlend;
            float legSwing = Mathf.Sin(gaitPhase) * amplitude;
            float tuck = AirborneTuckDegrees * airborneBlend;

            ApplyBoneSwing(leftThigh, leftThighRest, legSwing - tuck);
            ApplyBoneSwing(rightThigh, rightThighRest, -legSwing - tuck);

            float armSwing = legSwing * (ArmSwingDegrees / Mathf.Max(1f, LegSwingDegrees));
            ApplyBoneSwing(leftUpperarm, leftUpperarmRest, -armSwing);
            ApplyBoneSwing(rightUpperarm, rightUpperarmRest, armSwing);
        }

        private static void ApplyBoneSwing(Transform bone, Quaternion restRotation, float angleDegrees)
        {
            if (bone == null) return;
            bone.localRotation = restRotation * Quaternion.Euler(angleDegrees, 0f, 0f);
        }

        // Layered after the bone swing above, so the gripping hand visually reaches
        // toward the vine without needing a dedicated per-vine-angle animation clip.
        private void UpdateHandAim()
        {
            if (!hanging || activeHandBone == null) return;
            Vector3 toTarget = handAimTarget - activeHandBone.position;
            if (toTarget.sqrMagnitude < 0.0001f) return;
            Quaternion desired = Quaternion.LookRotation(toTarget.normalized, transform.up);
            activeHandBone.rotation = Quaternion.Slerp(activeHandBone.rotation, desired, 0.5f);
        }

        // Runs after the Humanoid Animator's own pose for this frame (LateUpdate always
        // follows Animator updates), so it overrides whatever the walk/run/idle clip was
        // doing with the wing bones — they're mapped as the Humanoid "arms" (see
        // EagleHumanoidRigSetup) purely so Mixamo clips retarget at all, not because their
        // arm-swing should drive flight. The flap always starts and ends at the bone's
        // imported rest rotation ("normal position"), never holding mid-flap, and fades out
        // via wingFlapBlend when isFlying turns off instead of snapping back.
        private void UpdateWingFlap()
        {
            if (leftWing == null && rightWing == null) return;
            float dt = Time.deltaTime;

            wingFlapBlend = Mathf.MoveTowards(wingFlapBlend, isFlying ? 1f : 0f, dt * WingFlapBlendSpeed);
            if (wingFlapBlend > 0.001f)
                wingFlapPhase += dt * WingFlapFrequency * Mathf.PI * 2f;
            else
                wingFlapPhase = 0f;

            // 0 -> max -> 0 each cycle (never negative), so the wing opens away from rest and
            // always returns to it, instead of swinging past rest to the far side.
            float flapAngle = WingFlapDegrees * (0.5f - 0.5f * Mathf.Cos(wingFlapPhase)) * wingFlapBlend;
            ApplyBoneSwing(leftWing, leftWingRest, flapAngle);
            ApplyBoneSwing(rightWing, rightWingRest, flapAngle);

            // Rest of the chain never animates on its own (no separate elbow/wrist flex) —
            // just held rigid at rest so the Humanoid arm-swing can never reach it, whether
            // flying or not.
            PinChainToRest(leftWingChain, leftWingChainRest);
            PinChainToRest(rightWingChain, rightWingChainRest);
        }

        private static void PinChainToRest(Transform[] chain, Quaternion[] restRotations)
        {
            if (chain == null) return;
            for (int i = 0; i < chain.Length; i++)
                if (chain[i] != null) chain[i].localRotation = restRotations[i];
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            if (root.name == boneName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindBone(root.GetChild(i), boneName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
