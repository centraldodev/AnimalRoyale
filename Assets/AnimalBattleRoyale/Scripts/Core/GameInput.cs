using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AnimalBattleRoyale
{
    public static class GameInput
    {
        private const float MovementDeadZone = 0.18f;

        private static bool GameplayInputBlocked => GameMenuController.Instance != null
                                                    && GameMenuController.Instance.IsBlockingGameplayInput;

        public static Vector2 ReadMovement()
        {
            if (GameplayInputBlocked) return Vector2.zero;
            if (MobileInputController.ControlsEnabled) return ApplyMovementDeadZone(MobileInputController.Movement);
#if ENABLE_INPUT_SYSTEM
            Vector2 value = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) value.y += 1f;
                if (keyboard.sKey.isPressed) value.y -= 1f;
                if (keyboard.dKey.isPressed) value.x += 1f;
                if (keyboard.aKey.isPressed) value.x -= 1f;
            }

            if (gamepad != null && gamepad.leftStick.ReadValue().sqrMagnitude > value.sqrMagnitude)
            {
                value = gamepad.leftStick.ReadValue();
            }

            return ApplyMovementDeadZone(value);
#else
            Vector2 value = Vector2.zero;
            if (GameInputBindings.IsHeld(GameInputAction.MoveForward)) value.y += 1f;
            if (GameInputBindings.IsHeld(GameInputAction.MoveBackward)) value.y -= 1f;
            if (GameInputBindings.IsHeld(GameInputAction.MoveRight)) value.x += 1f;
            if (GameInputBindings.IsHeld(GameInputAction.MoveLeft)) value.x -= 1f;
#endif
            Vector2 mobileMovement = MobileInputController.Movement;
            if (mobileMovement.sqrMagnitude > value.sqrMagnitude) value = mobileMovement;
            return ApplyMovementDeadZone(value);
        }

        private static Vector2 ApplyMovementDeadZone(Vector2 value)
        {
            float magnitude = value.magnitude;
            if (magnitude <= MovementDeadZone) return Vector2.zero;

            float normalizedMagnitude = Mathf.InverseLerp(MovementDeadZone, 1f, Mathf.Min(magnitude, 1f));
            return value.normalized * normalizedMagnitude;
        }

        public static Vector2 ReadLook()
        {
            if (GameplayInputBlocked) return Vector2.zero;
            // Touchscreen events can also surface as a simulated mouse pointer
            // on Android. Reading that delta here made the movement joystick
            // rotate the camera. Mobile look must come exclusively from the
            // right-side touch captured by MobileInputController.
            if (MobileInputController.ControlsEnabled) return MobileInputController.LookDelta;
#if ENABLE_INPUT_SYSTEM
            Vector2 value = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            if (Gamepad.current != null)
            {
                value += Gamepad.current.rightStick.ReadValue() * 18f;
            }
#else
            Vector2 value = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#endif
            return value;
        }

        public static bool JumpPressed()
        {
            if (GameplayInputBlocked) return false;
            if (MobileInputController.ControlsEnabled) return MobileInputController.JumpPressed;
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                   || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                   || MobileInputController.JumpPressed;
#else
            return GameInputBindings.WasPressedThisFrame(GameInputAction.Jump)
                   || MobileInputController.JumpPressed;
#endif
        }

        public static bool JumpHeld()
        {
            if (GameplayInputBlocked) return false;
            if (MobileInputController.ControlsEnabled) return MobileInputController.JumpHeld;
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
                   || (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed)
                   || MobileInputController.JumpHeld;
#else
            return GameInputBindings.IsHeld(GameInputAction.Jump) || MobileInputController.JumpHeld;
#endif
        }

        public static bool DescendHeld()
        {
            if (GameplayInputBlocked) return false;
            if (MobileInputController.ControlsEnabled) return false;
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed))
                   || (Gamepad.current != null && gamepadLeftTriggerHeld());
#else
            return GameInputBindings.IsHeld(GameInputAction.Descend);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool gamepadLeftTriggerHeld()
        {
            return Gamepad.current.leftTrigger.ReadValue() > 0.4f;
        }
#endif

        public static bool SprintHeld()
        {
            if (GameplayInputBlocked) return false;
            if (MobileInputController.ControlsEnabled) return MobileInputController.SprintHeld;
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
                   || (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed)
                   || MobileInputController.SprintHeld;
#else
            return GameInputBindings.IsHeld(GameInputAction.Sprint) || MobileInputController.SprintHeld;
#endif
        }

        public static bool RangedAttackPressed()
        {
            if (GameplayInputBlocked) return false;
            if (MobileInputController.ControlsEnabled) return MobileInputController.FirePressed;
#if ENABLE_INPUT_SYSTEM
            return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                   || (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame)
                   || MobileInputController.FirePressed;
#else
            return GameInputBindings.WasPressedThisFrame(GameInputAction.RangedAttack)
                   || MobileInputController.FirePressed;
#endif
        }

        public static bool RangedAttackHeld()
        {
            if (GameplayInputBlocked) return false;
            if (MobileInputController.ControlsEnabled) return MobileInputController.FireHeld;
#if ENABLE_INPUT_SYSTEM
            return (Mouse.current != null && Mouse.current.leftButton.isPressed)
                   || (Gamepad.current != null && Gamepad.current.rightShoulder.isPressed)
                   || MobileInputController.FireHeld;
#else
            return GameInputBindings.IsHeld(GameInputAction.RangedAttack) || MobileInputController.FireHeld;
#endif
        }

        public static bool AttackPressed() => RangedAttackPressed();

        public static bool ConsumePressed()
        {
            if (GameplayInputBlocked) return false;
            if (MobileInputController.ControlsEnabled) return MobileInputController.ConsumePressed;
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                   || MobileInputController.ConsumePressed;
#else
            return GameInputBindings.WasPressedThisFrame(GameInputAction.Consume)
                   || MobileInputController.ConsumePressed;
#endif
        }

        public static bool AbilityOnePressed()
        {
            if (GameplayInputBlocked) return false;
            if (MobileInputController.ControlsEnabled) return MobileInputController.AbilityPressed;
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && keyboardQPressed()
                   || (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame)
                   || MobileInputController.AbilityPressed;
#else
            return GameInputBindings.WasPressedThisFrame(GameInputAction.Ability)
                   || MobileInputController.AbilityPressed;
#endif
        }

        public static bool AbilityTwoPressed()
        {
            if (GameplayInputBlocked) return false;
            if (MobileInputController.ControlsEnabled) return MobileInputController.AbilitySecondaryPressed;
#if ENABLE_INPUT_SYSTEM
            return GameInputBindings.WasPressedThisFrame(GameInputAction.AbilitySecondary)
                   || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame)
                   || MobileInputController.AbilitySecondaryPressed;
#else
            return GameInputBindings.WasPressedThisFrame(GameInputAction.AbilitySecondary)
                   || MobileInputController.AbilitySecondaryPressed;
#endif
        }

        public static bool AbilityThreePressed()
        {
            if (MobileInputController.ControlsEnabled) return false;
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        public static bool MeleeAttackPressed()
        {
            if (GameplayInputBlocked) return false;
            if (MobileInputController.ControlsEnabled) return MobileInputController.MeleePressed;
#if ENABLE_INPUT_SYSTEM
            // Moved off the right mouse button onto V so aim (see AimHeld) can have RMB.
            return (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
                   || (Gamepad.current != null && Gamepad.current.leftTrigger.wasPressedThisFrame)
                   || MobileInputController.MeleePressed;
#else
            return GameInputBindings.WasPressedThisFrame(GameInputAction.MeleeAttack)
                   || MobileInputController.MeleePressed;
#endif
        }

        public static bool AttackModeTogglePressed() => MeleeAttackPressed();

        /// <summary>Held to enter the secondary, zoomed aiming mode for any ammo type.</summary>
        public static bool AimHeld()
        {
            if (GameplayInputBlocked) return false;
            if (MobileInputController.ControlsEnabled) return MobileInputController.AimHeld;
#if ENABLE_INPUT_SYSTEM
            return (Mouse.current != null && Mouse.current.rightButton.isPressed)
                   || (Gamepad.current != null && Gamepad.current.leftTrigger.isPressed)
                   || MobileInputController.AimHeld;
#else
            return GameInputBindings.IsHeld(GameInputAction.Aim) || MobileInputController.AimHeld;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool keyboardQPressed()
        {
            return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
        }
#endif

        public static int ReadAnimalSelection()
        {
            if (GameplayInputBlocked) return -1;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return -1;
            if (keyboard.digit1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame) return 3;
            if (keyboard.digit5Key.wasPressedThisFrame) return 4;
            if (keyboard.digit6Key.wasPressedThisFrame) return 5;
            if (keyboard.digit7Key.wasPressedThisFrame) return 6;
#else
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha4)) return 3;
            if (Input.GetKeyDown(KeyCode.Alpha5)) return 4;
            if (Input.GetKeyDown(KeyCode.Alpha6)) return 5;
            if (Input.GetKeyDown(KeyCode.Alpha7)) return 6;
#endif
            return -1;
        }

        public static int ReadWeaponSelection()
        {
            if (GameplayInputBlocked) return -1;
            if (GameInputBindings.WasPressedThisFrame(GameInputAction.WeaponPrimary)) return 0;
            if (GameInputBindings.WasPressedThisFrame(GameInputAction.WeaponSecondary)) return 1;
            if (GameInputBindings.WasPressedThisFrame(GameInputAction.WeaponThird)) return 2;
            return -1;
        }

        public static bool ReloadPressed()
        {
            if (GameplayInputBlocked || MobileInputController.ControlsEnabled) return false;
            return GameInputBindings.WasPressedThisFrame(GameInputAction.Reload);
        }

        /// <summary>+1 scrolled up, -1 scrolled down, 0 if no scroll happened this frame.</summary>
        public static int ReadWeaponScroll()
        {
            if (GameplayInputBlocked || MobileInputController.ControlsEnabled) return 0;
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null) return 0;
            float scroll = mouse.scroll.ReadValue().y;
#else
            float scroll = Input.mouseScrollDelta.y;
#endif
            if (scroll > 0.01f) return 1;
            if (scroll < -0.01f) return -1;
            return 0;
        }

        public static bool ConfirmPressed()
        {
            if (GameplayInputBlocked) return false;
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame
                                                  || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                   || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }

        public static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
