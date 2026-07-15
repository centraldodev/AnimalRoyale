using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AnimalBattleRoyale
{
    public static class GameInput
    {
        public static Vector2 ReadMovement()
        {
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

            return Vector2.ClampMagnitude(value, 1f);
#else
            return Vector2.ClampMagnitude(
                new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#endif
        }

        public static Vector2 ReadLook()
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 value = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            if (Gamepad.current != null)
            {
                value += Gamepad.current.rightStick.ReadValue() * 18f;
            }
            return value;
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#endif
        }

        public static bool JumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                   || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
#else
            return Input.GetButtonDown("Jump");
#endif
        }

        public static bool JumpHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
                   || (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
#else
            return Input.GetButton("Jump");
#endif
        }

        public static bool DescendHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed))
                   || (Gamepad.current != null && gamepadLeftTriggerHeld());
#else
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
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
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
                   || (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);
#else
            return Input.GetKey(KeyCode.LeftShift);
#endif
        }

        public static bool RangedAttackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                   || (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        public static bool AttackPressed() => RangedAttackPressed();

        public static bool ConsumePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }

        public static bool AbilityOnePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && keyboardQPressed()
                   || (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Q);
#endif
        }

        public static bool AbilityTwoPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        public static bool AbilityThreePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        public static bool MeleeAttackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                   || (Gamepad.current != null && Gamepad.current.leftTrigger.wasPressedThisFrame);
#else
            return Input.GetMouseButtonDown(1);
#endif
        }

        public static bool AttackModeTogglePressed() => MeleeAttackPressed();

#if ENABLE_INPUT_SYSTEM
        private static bool keyboardQPressed()
        {
            return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
        }
#endif

        public static int ReadAnimalSelection()
        {
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

        public static bool ConfirmPressed()
        {
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
