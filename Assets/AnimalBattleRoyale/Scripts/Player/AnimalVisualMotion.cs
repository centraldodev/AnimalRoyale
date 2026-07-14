using System.Collections;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Small procedural animation layer until authored Animator clips replace it.</summary>
    public sealed class AnimalVisualMotion : MonoBehaviour
    {
        private AnimalType animalType;
        private Vector3 basePosition;
        private Vector3 baseScale;
        private Transform leftWing;
        private Transform rightWing;
        private Transform monkeyLeftArm;
        private Transform monkeyRightArm;
        private Vector3 monkeyLeftArmScale;
        private Vector3 monkeyRightArmScale;
        private Quaternion monkeyLeftArmRotation;
        private Quaternion monkeyRightArmRotation;
        private Animator animator;
        private Coroutine returnToIdle;
        private float attackUntil;
        private float powerUntil;
        private float actionLockUntil;
        private bool isMoving;
        private bool isSprinting;
        private bool isAirborne;
        private string currentClip;
        private float monkeyGrabUntil;
        private bool monkeyHanging;

        public void Initialize(AnimalType type)
        {
            animalType = type;
            basePosition = transform.localPosition;
            baseScale = transform.localScale;
            leftWing = transform.Find("LeftWing");
            rightWing = transform.Find("RightWing");
            animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
                animator.speed = 1f;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            if (animalType == AnimalType.Monkey && animator != null)
            {
                monkeyLeftArm = FindChildRecursive(animator.transform, "Arm_L");
                monkeyRightArm = FindChildRecursive(animator.transform, "Arm_R");
                if (monkeyLeftArm != null) monkeyLeftArmScale = monkeyLeftArm.localScale;
                if (monkeyRightArm != null) monkeyRightArmScale = monkeyRightArm.localScale;
                if (monkeyLeftArm != null) monkeyLeftArmRotation = monkeyLeftArm.localRotation;
                if (monkeyRightArm != null) monkeyRightArmRotation = monkeyRightArm.localRotation;
            }

            PlayLocomotion();
        }

        public void TriggerAttack()
        {
            attackUntil = Time.time + 0.22f;
            string clip = animalType switch
            {
                AnimalType.Ant => "Ant_Bite",
                AnimalType.Monkey => "Monkey_Punch",
                AnimalType.Tiger => "Tiger_Claw",
                AnimalType.Eagle => "Eagle_Dive",
                _ => null
            };
            if (clip != null) PlayClip(clip, 0.55f);
        }

        public void SetLocomotion(bool moving, bool sprinting, bool airborne)
        {
            isMoving = moving;
            isSprinting = sprinting;
            isAirborne = airborne;
            if (Time.time >= actionLockUntil) PlayLocomotion();
        }

        public void TriggerPower(int slot = -1)
        {
            powerUntil = Time.time + 0.42f;
            if (animalType == AnimalType.Monkey && slot == 0) monkeyGrabUntil = Time.time + 0.68f;
            string clip = animalType switch
            {
                AnimalType.Ant => slot == 0 ? "Ant_Throw" : slot == 1 ? "Ant_Burrow" : "Ant_Throw",
                AnimalType.Monkey => slot == 0 ? "Monkey_VineLeap" : slot == 1 ? "Monkey_Slam" : "Monkey_Run",
                AnimalType.Tiger => slot == 0 ? "Tiger_Pounce" : slot == 2 ? "Tiger_Roar" : "Tiger_Run",
                AnimalType.Eagle => slot == 0 ? "Eagle_Dive" : slot == 1 ? "Eagle_Gust" : "Eagle_Perch",
                _ => null
            };
            if (clip != null) PlayClip(clip, slot == 0 ? 0.7f : 0.58f);
        }

        public void SetVineHanging(bool hanging, bool leftHandGrip = true)
        {
            if (animalType != AnimalType.Monkey || animator == null) return;
            monkeyHanging = hanging;
            monkeyLeftHandGrip = leftHandGrip;
            animator.speed = hanging ? 0f : 1f;
            if (!hanging)
            {
                ResetMonkeyArms();
                actionLockUntil = 0f;
                PlayLocomotion();
                return;
            }

            string clip = ResolveClipName("Monkey_VineLeap");
            if (clip == null) return;
            currentClip = clip;
            // Freeze mid-clip, then bias the pose so one arm keeps the vine while
            // the other remains ready for the next jump.
            animator.Play(clip, 0, 0.52f);
            animator.Update(0f);
            ApplyMonkeyVineArmPose(1f);
        }

        /// <summary>Stops all visual animation so the animal can remain as a corpse.</summary>
        public void Freeze()
        {
            if (returnToIdle != null)
            {
                StopCoroutine(returnToIdle);
                returnToIdle = null;
            }

            if (animator != null) animator.speed = 0f;
            enabled = false;
        }

        private void PlayClip(string clipName, float duration)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            clipName = ResolveClipName(clipName);
            if (clipName == null) return;
            actionLockUntil = Time.time + duration;
            currentClip = clipName;
            animator.CrossFade(clipName, 0.06f, 0, 0f);
            if (returnToIdle != null) StopCoroutine(returnToIdle);
            returnToIdle = StartCoroutine(ReturnToLocomotion(duration));
        }

        private IEnumerator ReturnToLocomotion(float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayLocomotion();
        }

        private void PlayLocomotion()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (monkeyHanging) return;
            string clip;
            if (isAirborne)
            {
                clip = animalType switch
                {
                    AnimalType.Tiger => "Tiger_Pounce",
                    AnimalType.Monkey => "Monkey_VineLeap",
                    AnimalType.Eagle => "Eagle_Fly",
                    AnimalType.Ant => "Ant_Run",
                    _ => animalType + "_Idle"
                };
            }
            else if (!isMoving)
            {
                clip = animalType + "_Idle";
            }
            else
            {
                clip = animalType == AnimalType.Eagle
                    ? "Eagle_Fly"
                    : animalType + (isSprinting ? "_Run" : "_Walk");
            }
            clip = ResolveClipName(clip);
            if (clip == null) return;
            if (currentClip == clip)
            {
                // Imported FBX actions are not always tagged as looping on their first
                // import. Keep locomotion alive even in that case instead of freezing on
                // the last keyframe after one walk/idle cycle.
                if (animator.IsInTransition(0)) return;
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.loop || state.normalizedTime < 0.98f) return;
                animator.Play(clip, 0, 0f);
                return;
            }
            currentClip = clip;
            animator.CrossFade(clip, 0.10f, 0, 0f);
        }

        // Blender exports the actions as "RigName|Animal_Walk". Controllers built from
        // those actions keep that full state name, while gameplay intentionally uses the
        // stable short names (Animal_Walk, Animal_Run, etc.).
        private string ResolveClipName(string requestedName)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(requestedName)) return null;
            if (animator.HasState(0, Animator.StringToHash(requestedName))) return requestedName;
            string fullRequestedName = "Base Layer." + requestedName;
            if (animator.HasState(0, Animator.StringToHash(fullRequestedName))) return fullRequestedName;

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null || !clip.name.EndsWith("|" + requestedName)) continue;
                if (animator.HasState(0, Animator.StringToHash(clip.name))) return clip.name;
                string fullClipName = "Base Layer." + clip.name;
                if (animator.HasState(0, Animator.StringToHash(fullClipName))) return fullClipName;
            }
            return null;
        }

        private void Update()
        {
            float bobSpeed = animalType == AnimalType.Eagle ? 4.4f : 2.6f;
            float bobAmount = animalType == AnimalType.Ant ? 0.018f : 0.035f;
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmount);

            float attack = Mathf.Clamp01((attackUntil - Time.time) / 0.22f);
            float power = Mathf.Clamp01((powerUntil - Time.time) / 0.42f);
            transform.localScale = baseScale * (1f + power * 0.08f + attack * 0.05f);
            transform.localRotation = Quaternion.Euler(attack * -10f, 0f, Mathf.Sin(Time.time * 28f) * power * 5f);

            if (animalType == AnimalType.Eagle && leftWing != null && rightWing != null)
            {
                float flap = Mathf.Sin(Time.time * 8.5f) * 20f + power * 22f;
                leftWing.localEulerAngles = new Vector3(0f, 0f, -15f - flap);
                rightWing.localEulerAngles = new Vector3(0f, 0f, 15f + flap);
            }

            if (animalType == AnimalType.Monkey)
            {
                float progress = 1f - Mathf.Clamp01((monkeyGrabUntil - Time.time) / 0.68f);
                float reach = Mathf.Sin(progress * Mathf.PI);
                if (monkeyHanging) ApplyMonkeyVineArmPose(1f);
                else
                {
                    if (monkeyLeftArm != null) monkeyLeftArm.localScale = monkeyLeftArmScale * (1f + reach * 0.32f);
                    if (monkeyRightArm != null) monkeyRightArm.localScale = monkeyRightArmScale * (1f + reach * 0.32f);
                }
            }
        }

        private bool monkeyLeftHandGrip = true;

        private void ApplyMonkeyVineArmPose(float reach)
        {
            Transform gripArm = monkeyLeftHandGrip ? monkeyLeftArm : monkeyRightArm;
            Transform freeArm = monkeyLeftHandGrip ? monkeyRightArm : monkeyLeftArm;
            Vector3 gripScale = monkeyLeftHandGrip ? monkeyLeftArmScale : monkeyRightArmScale;
            Vector3 freeScale = monkeyLeftHandGrip ? monkeyRightArmScale : monkeyLeftArmScale;
            Quaternion freeRotation = monkeyLeftHandGrip ? monkeyRightArmRotation : monkeyLeftArmRotation;

            if (gripArm != null) gripArm.localScale = gripScale * (1f + reach * 0.34f);
            if (freeArm != null)
            {
                freeArm.localScale = freeScale;
                freeArm.localRotation = freeRotation * Quaternion.Euler(34f, monkeyLeftHandGrip ? -18f : 18f, monkeyLeftHandGrip ? 26f : -26f);
            }
        }

        private void ResetMonkeyArms()
        {
            if (monkeyLeftArm != null)
            {
                monkeyLeftArm.localScale = monkeyLeftArmScale;
                monkeyLeftArm.localRotation = monkeyLeftArmRotation;
            }
            if (monkeyRightArm != null)
            {
                monkeyRightArm.localScale = monkeyRightArmScale;
                monkeyRightArm.localRotation = monkeyRightArmRotation;
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), childName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
