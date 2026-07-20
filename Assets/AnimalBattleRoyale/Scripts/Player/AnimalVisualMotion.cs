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

        public float MeleeImpactDelay => 0f;

        private Transform leftThigh, rightThigh, leftUpperarm, rightUpperarm;
        private Quaternion leftThighRest, rightThighRest, leftUpperarmRest, rightUpperarmRest;
        private Transform leftHandBone, rightHandBone;
        private Transform activeHandBone;

        private bool isMoving, isSprinting, isAirborne, hanging, frozen;
        private float gaitPhase;
        private float locomotionBlend;
        private float airborneBlend;
        private float sprintBlend;
        private Vector3 handAimTarget;

        public void Initialize(AnimalType type)
        {
            transform.localRotation = Quaternion.identity;

            leftThigh = FindBone(transform, "L_Thigh");
            rightThigh = FindBone(transform, "R_Thigh");
            leftUpperarm = FindBone(transform, "L_Upperarm");
            rightUpperarm = FindBone(transform, "R_Upperarm");
            leftHandBone = FindBone(transform, "L_Hand");
            rightHandBone = FindBone(transform, "R_Hand");

            if (leftThigh != null) leftThighRest = leftThigh.localRotation;
            if (rightThigh != null) rightThighRest = rightThigh.localRotation;
            if (leftUpperarm != null) leftUpperarmRest = leftUpperarm.localRotation;
            if (rightUpperarm != null) rightUpperarmRest = rightUpperarm.localRotation;

            // No clip/retargeting involved anymore — keep any imported Animator off so
            // it can never fight these procedural bone rotations.
            foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.enabled = false;
            }
        }

        public void SetLocomotion(bool moving, bool sprinting, bool airborne,
            float currentVerticalSpeed = 0f, bool isFlying = false)
        {
            isMoving = moving;
            isSprinting = sprinting;
            isAirborne = airborne;
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
        }

        private void UpdateLocomotionPose()
        {
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
