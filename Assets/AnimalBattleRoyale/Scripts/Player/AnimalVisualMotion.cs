using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Compatibility bridge kept while the animal animation set is rebuilt.
    /// Gameplay can continue reporting movement and actions, but this component
    /// deliberately leaves every rig bone in its imported rest pose.
    /// </summary>
    public sealed class AnimalVisualMotion : MonoBehaviour
    {
        public float MeleeImpactDelay => 0f;

        public void Initialize(AnimalType type)
        {
            transform.localRotation = Quaternion.identity;

            // Keep the Animator component and the complete skeleton available for
            // the new animation work, while ensuring no old controller can run.
            foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = null;
                animator.enabled = false;
            }
        }

        public void SetLocomotion(bool isMoving, bool isSprinting, bool isAirborne,
            float currentVerticalSpeed = 0f, bool isFlying = false) { }

        public void TriggerAttack(bool meleeAttack = true) { }

        public void TriggerPower(int slot) { }

        public void SetVineHanging(bool hanging, bool leftHand = true) { }

        public void Freeze() { }

        public void Unfreeze() { }
    }
}
