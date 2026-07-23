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
        private const float LegSwingDegrees = 11f;
        private const float ArmSwingDegrees = 6f;
        private const float AirborneTuckDegrees = 14f;
        private const float BlendSpeed = 5f;
        private const float LocomotionPlaybackBlendSpeed = 4f;
        private const float DefaultRunPlaybackSpeed = 1.45f;
        private const float MinimumLocomotionPlaybackSpeed = 0.45f;
        private const float MaximumLocomotionPlaybackSpeed = 1.65f;
        private const float WingFlapFrequency = 2.4f;
        private const float WingFlapDegrees = 55f;
        private const float WingFlapBlendSpeed = 6f;

        public float MeleeImpactDelay => 0f;

        private const string LocomotionControllerPath = "Animation/AnimalLocomotion";

        private Transform leftThigh, rightThigh, leftUpperarm, rightUpperarm;
        private Quaternion leftThighRest, rightThighRest, leftUpperarmRest, rightUpperarmRest;
        private Transform leftForearm, rightForearm;
        private Quaternion leftForearmRest, rightForearmRest;
        private Transform leftHandBone, rightHandBone;
        private Quaternion leftHandRest, rightHandRest;
        private Transform activeHandBone;
        private bool pinRightArm, pinLeftArm, customLeftArmSwing;
        private Animator humanoidAnimator;
        private bool useHumanoidAnimation;
        private float animatorSpeedVelocity;

        private bool isMoving, isAirborne, isFlying, hanging, frozen;
        private float gaitPhase;
        private float armSwingPhase;
        private float locomotionBlend;
        private float airborneBlend;
        private float locomotionPlaybackSpeed = 1f;
        private float targetLocomotionPlaybackSpeed = 1f;
        private Vector3 handAimTarget;
        private WeaponMuzzleSocket weaponSocket;

        private Transform leftWing, rightWing;
        private Quaternion leftWingRest, rightWingRest;
        private Vector3 leftWingFlapAxis = Vector3.right;
        private Vector3 rightWingFlapAxis = Vector3.right;
        private float leftWingFlapSign = 1f;
        private float rightWingFlapSign = 1f;
        private Transform[] leftWingChain, rightWingChain;
        private Quaternion[] leftWingChainRest, rightWingChainRest;
        private float wingFlapPhase;
        private float wingFlapBlend;

        public void Initialize(AnimalType type)
        {
            transform.localRotation = Quaternion.identity;

            leftHandBone = FindBone(transform, "L_Hand");
            rightHandBone = FindBone(transform, "R_Hand");
            leftUpperarm = FindBone(transform, "L_Upperarm");
            rightUpperarm = FindBone(transform, "R_Upperarm");
            leftForearm = FindBone(transform, "L_Forearm");
            rightForearm = FindBone(transform, "R_Forearm");
            if (leftUpperarm != null) leftUpperarmRest = leftUpperarm.localRotation;
            if (rightUpperarm != null) rightUpperarmRest = rightUpperarm.localRotation;
            if (leftForearm != null) leftForearmRest = leftForearm.localRotation;
            if (rightForearm != null) rightForearmRest = rightForearm.localRotation;
            if (leftHandBone != null) leftHandRest = leftHandBone.localRotation;
            if (rightHandBone != null) rightHandRest = rightHandBone.localRotation;

            // The Run/Walk clips (and the procedural fallback below) swing the right arm
            // side-to-side across the body, which reads badly now that the seed weapon hangs
            // rigidly from that hand — so the whole right arm is held at its imported rest
            // pose instead (see UpdateArmOverride). The left arm keeps a light custom
            // forward/back-only swing in its place. The cow doesn't carry a weapon and
            // shouldn't swing either arm. Eagle is handled separately by the wing system.
            pinRightArm = type != AnimalType.Eagle;
            pinLeftArm = type == AnimalType.Cow;
            customLeftArmSwing = !pinLeftArm && type != AnimalType.Eagle;

            // The current Eagle uses a standard Humanoid shoulder/arm chain for each wing.
            // Older Eagle assets used generic bone_5/bone_12 names, so keep those as a
            // fallback. Every child wing bone is pinned to its imported rotation after the
            // Animator runs; only the shoulder root flaps, keeping the wing rigid and
            // guaranteeing that the end of every flap is exactly the original pose.
            if (type == AnimalType.Eagle)
            {
                Transform standardLeftWing = FindBone(transform, "L_Clavicle");
                Transform standardRightWing = FindBone(transform, "R_Clavicle");
                bool usesStandardWingRig = standardLeftWing != null && standardRightWing != null;

                leftWing = usesStandardWingRig ? standardLeftWing : FindBone(transform, "bone_5");
                rightWing = usesStandardWingRig ? standardRightWing : FindBone(transform, "bone_12");
                if (leftWing != null) leftWingRest = leftWing.localRotation;
                if (rightWing != null) rightWingRest = rightWing.localRotation;

                if (usesStandardWingRig)
                {
                    leftWingChain = new[]
                    {
                        leftUpperarm,
                        FindBone(transform, "L_UpperarmTwist01"),
                        FindBone(transform, "L_UpperarmTwist02"),
                        leftForearm,
                        FindBone(transform, "L_ForearmTwist01"),
                        FindBone(transform, "L_ForearmTwist02"),
                        leftHandBone
                    };
                    rightWingChain = new[]
                    {
                        rightUpperarm,
                        FindBone(transform, "R_UpperarmTwist01"),
                        FindBone(transform, "R_UpperarmTwist02"),
                        rightForearm,
                        FindBone(transform, "R_ForearmTwist01"),
                        FindBone(transform, "R_ForearmTwist02"),
                        rightHandBone
                    };

                    // Rotate around the animal's forward axis. Opposite signs make a wing
                    // resting at each side open outward symmetrically instead of both being
                    // pushed toward the same side. The axes are stored in each rest bone's
                    // local space, so angle zero remains the exact imported rotation.
                    leftWingFlapAxis = leftWing.InverseTransformDirection(transform.forward).normalized;
                    rightWingFlapAxis = rightWing.InverseTransformDirection(transform.forward).normalized;
                    leftWingFlapSign = -1f;
                    rightWingFlapSign = 1f;
                }
                else
                {
                    leftWingChain = new[] { FindBone(transform, "bone_6"), FindBone(transform, "bone_7") };
                    rightWingChain = new[] { FindBone(transform, "bone_13"), FindBone(transform, "bone_14") };
                }

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

                if (leftThigh != null) leftThighRest = leftThigh.localRotation;
                if (rightThigh != null) rightThighRest = rightThigh.localRotation;

                // No clip/retargeting involved for this animal — keep any imported Animator
                // off so it can never fight the procedural bone rotations below.
                foreach (Animator disabled in GetComponentsInChildren<Animator>(true))
                {
                    disabled.runtimeAnimatorController = null;
                    disabled.applyRootMotion = false;
                    disabled.enabled = false;
                }
            }

            weaponSocket = GetComponentInChildren<WeaponMuzzleSocket>(true);
        }

        /// <summary>Muzzle world position/rotation for spawning a projectile — the socket is
        /// already stable by construction (see WeaponMuzzleSocket), no per-shot settling
        /// needed.</summary>
        public bool TryGetWeaponMuzzle(out Vector3 position, out Quaternion rotation)
        {
            if (weaponSocket == null)
            {
                position = default;
                rotation = default;
                return false;
            }
            position = weaponSocket.MuzzlePosition;
            rotation = weaponSocket.MuzzleRotation;
            return true;
        }

        public void SetLocomotion(bool moving, bool sprinting, bool airborne,
            float currentVerticalSpeed = 0f, bool flying = false, bool jumped = false,
            float horizontalSpeed = 0f, float referenceWalkSpeed = 1f)
        {
            isMoving = moving;
            isAirborne = airborne;
            isFlying = flying;

            // Walking is the only ground locomotion cycle. Running keeps that exact pose
            // and changes only its playback rate, based on how fast the controller is
            // actually moving (including aiming, slows and water), so the feet stay in pace.
            float requestedPlaybackSpeed = horizontalSpeed > 0.01f
                ? horizontalSpeed / Mathf.Max(0.01f, referenceWalkSpeed)
                : sprinting ? DefaultRunPlaybackSpeed : 1f;
            targetLocomotionPlaybackSpeed = moving && !airborne
                ? Mathf.Clamp(requestedPlaybackSpeed,
                    MinimumLocomotionPlaybackSpeed, MaximumLocomotionPlaybackSpeed)
                : 1f;

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
            float dt = Time.deltaTime;

            float locomotionTarget = !frozen && isMoving && !isAirborne && !hanging ? 1f : 0f;
            locomotionBlend = Mathf.MoveTowards(locomotionBlend, locomotionTarget, dt * BlendSpeed);

            locomotionPlaybackSpeed = Mathf.MoveTowards(locomotionPlaybackSpeed,
                targetLocomotionPlaybackSpeed, dt * LocomotionPlaybackBlendSpeed);

            if (humanoidAnimator != null)
                humanoidAnimator.speed = frozen ? 0f : locomotionPlaybackSpeed;

            if (locomotionBlend > 0.001f && !frozen)
            {
                gaitPhase += dt * WalkFrequency * locomotionPlaybackSpeed * Mathf.PI * 2f;
                // Left-arm swing always paces like the walk cycle, even while sprinting — the
                // walking arm swing read fine, but speeding it up to match the run cadence
                // looked bad, so it's kept on its own phase instead of following gaitPhase.
                armSwingPhase += dt * WalkFrequency * Mathf.PI * 2f;
            }

            UpdateArmOverride();

            if (useHumanoidAnimation) return;

            float airborneTarget = !frozen && isAirborne ? 1f : 0f;
            airborneBlend = Mathf.MoveTowards(airborneBlend, airborneTarget, dt * BlendSpeed);

            float amplitude = LegSwingDegrees * locomotionBlend;
            float legSwing = Mathf.Sin(gaitPhase) * amplitude;
            float tuck = AirborneTuckDegrees * airborneBlend;

            ApplyBoneSwing(leftThigh, leftThighRest, legSwing - tuck);
            ApplyBoneSwing(rightThigh, rightThighRest, -legSwing - tuck);
        }

        // Runs after the Humanoid Animator's own pose for this frame (LateUpdate always
        // follows Animator updates) so it overrides whatever the Run/Walk clip did with the
        // arms — see pinRightArm/pinLeftArm/customLeftArmSwing in Initialize for why. Shared
        // by both the humanoid-clip path and the procedural fallback below, so the same rule
        // applies regardless of which one is driving the legs.
        private void UpdateArmOverride()
        {
            bool rightHanging = hanging && activeHandBone == rightHandBone;
            bool leftHanging = hanging && activeHandBone == leftHandBone;

            if (pinRightArm && !rightHanging)
            {
                ApplyBoneSwing(rightUpperarm, rightUpperarmRest, 0f);
                ApplyBoneSwing(rightForearm, rightForearmRest, 0f);
                ApplyBoneSwing(rightHandBone, rightHandRest, 0f);
            }

            if (leftHanging) return;

            if (pinLeftArm)
            {
                ApplyBoneSwing(leftUpperarm, leftUpperarmRest, 0f);
                ApplyBoneSwing(leftForearm, leftForearmRest, 0f);
            }
            else if (customLeftArmSwing)
            {
                float swing = Mathf.Sin(armSwingPhase) * ArmSwingDegrees * locomotionBlend;
                ApplyBoneSwing(leftUpperarm, leftUpperarmRest, swing);
                ApplyBoneSwing(leftForearm, leftForearmRest, 0f);
            }
        }

        private static void ApplyBoneSwing(Transform bone, Quaternion restRotation, float angleDegrees)
        {
            if (bone == null) return;
            bone.localRotation = restRotation * Quaternion.Euler(angleDegrees, 0f, 0f);
        }

        private static void ApplyBoneRotation(Transform bone, Quaternion restRotation,
            Vector3 localAxis, float angleDegrees)
        {
            if (bone == null) return;
            bone.localRotation = restRotation * Quaternion.AngleAxis(angleDegrees, localAxis);
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
            ApplyBoneRotation(leftWing, leftWingRest, leftWingFlapAxis,
                flapAngle * leftWingFlapSign);
            ApplyBoneRotation(rightWing, rightWingRest, rightWingFlapAxis,
                flapAngle * rightWingFlapSign);

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
