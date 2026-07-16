using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Runtime touch controls shared by Android and iOS. Input is exposed through
    /// GameInput so gameplay code remains identical across keyboard, gamepad and touch.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class MobileInputController : MonoBehaviour
    {
        private enum TouchAction
        {
            Fire,
            Melee,
            Ability,
            Jump,
            Consume,
            Sprint
        }

        private static MobileInputController instance;

        private readonly Dictionary<int, TouchAction> actionTouches = new Dictionary<int, TouchAction>();
        private int movementTouchId = -1;
        private int lookTouchId = -1;
        private bool gameplayActive;
        private Vector2 movement;
        private Vector2 lookDelta;
        private Vector2 joystickCenter;
        private float joystickRadius;
        private Texture2D circleTexture;
        private Texture2D hexTexture;
        private GUIStyle buttonLabelStyle;
        private GUIStyle hintStyle;

        private Rect fireRect;
        private Rect meleeRect;
        private Rect abilityRect;
        private Rect jumpRect;
        private Rect consumeRect;
        private Rect sprintRect;

        private bool firePressed;
        private bool fireHeld;
        private bool meleePressed;
        private bool abilityPressed;
        private bool jumpPressed;
        private bool jumpHeld;
        private bool consumePressed;
        private bool sprintHeld;

        public static bool ControlsEnabled => instance != null
                                              && instance.gameplayActive
                                              && Application.isMobilePlatform
                                              && (GameMenuController.Instance == null
                                                  || !GameMenuController.Instance.IsBlockingGameplayInput)
                                              && (BattleRoyaleManager.Instance == null
                                                  || !BattleRoyaleManager.Instance.MatchFinished);

        public static Vector2 Movement => ControlsEnabled ? instance.movement : Vector2.zero;
        public static Vector2 LookDelta => ControlsEnabled ? instance.lookDelta : Vector2.zero;
        public static bool FirePressed => ControlsEnabled && instance.firePressed;
        public static bool FireHeld => ControlsEnabled && instance.fireHeld;
        public static bool MeleePressed => ControlsEnabled && instance.meleePressed;
        public static bool AbilityPressed => ControlsEnabled && instance.abilityPressed;
        public static bool JumpPressed => ControlsEnabled && instance.jumpPressed;
        public static bool JumpHeld => ControlsEnabled && instance.jumpHeld;
        public static bool ConsumePressed => ControlsEnabled && instance.consumePressed;
        public static bool SprintHeld => ControlsEnabled && instance.sprintHeld;

        public static void EnsureExists()
        {
            if (!Application.isMobilePlatform || instance != null) return;
            GameObject controls = new GameObject("MobileInputController");
            controls.AddComponent<MobileInputController>();
        }

        public static void SetGameplayActive(bool active)
        {
            if (instance == null) EnsureExists();
            if (instance == null) return;
            instance.gameplayActive = active;
            if (!active) instance.ResetAllInput();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (circleTexture != null) Destroy(circleTexture);
            if (hexTexture != null) Destroy(hexTexture);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ResetAllInput();
        }

        private void Update()
        {
            ResetFrameInput();
            if (!ControlsEnabled)
            {
                ResetContinuousInput();
                return;
            }

            BuildLayout();
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                Vector2 guiPosition = new Vector2(touch.position.x, Screen.height - touch.position.y);
                bool ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;

                if (touch.fingerId == movementTouchId)
                {
                    if (ended)
                    {
                        movementTouchId = -1;
                        movement = Vector2.zero;
                    }
                    else
                    {
                        UpdateMovement(guiPosition);
                    }
                    continue;
                }

                if (touch.fingerId == lookTouchId)
                {
                    if (ended)
                    {
                        lookTouchId = -1;
                    }
                    else if (touch.phase == TouchPhase.Moved)
                    {
                        lookDelta += touch.deltaPosition * 0.82f;
                    }
                    continue;
                }

                if (actionTouches.TryGetValue(touch.fingerId, out TouchAction capturedAction))
                {
                    if (ended) actionTouches.Remove(touch.fingerId);
                    else SetActionState(capturedAction, false);
                    continue;
                }

                if (touch.phase != TouchPhase.Began) continue;
                if (TryGetAction(guiPosition, out TouchAction action))
                {
                    actionTouches[touch.fingerId] = action;
                    SetActionState(action, true);
                }
                else if (CanStartMovement(guiPosition))
                {
                    movementTouchId = touch.fingerId;
                    UpdateMovement(guiPosition);
                }
                else if (CanStartLook(guiPosition))
                {
                    lookTouchId = touch.fingerId;
                }
            }
        }

        private void ResetFrameInput()
        {
            lookDelta = Vector2.zero;
            firePressed = false;
            meleePressed = false;
            abilityPressed = false;
            jumpPressed = false;
            consumePressed = false;
            fireHeld = false;
            jumpHeld = false;
            sprintHeld = false;
        }

        private void ResetContinuousInput()
        {
            movement = Vector2.zero;
            movementTouchId = -1;
            lookTouchId = -1;
            actionTouches.Clear();
        }

        private void ResetAllInput()
        {
            ResetFrameInput();
            ResetContinuousInput();
        }

        private void BuildLayout()
        {
            Rect safe = Screen.safeArea;
            float safeLeft = safe.xMin;
            float safeRight = safe.xMax;
            float safeTop = Screen.height - safe.yMax;
            float safeBottom = Screen.height - safe.yMin;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.78f, 1.6f);
            float margin = 22f * scale;
            float buttonSize = 76f * scale;
            float smallButtonSize = 62f * scale;

            joystickRadius = 82f * scale;
            joystickCenter = new Vector2(safeLeft + margin + joystickRadius, safeBottom - margin - joystickRadius);

            fireRect = CenteredRect(new Vector2(safeRight - margin - buttonSize * 0.68f,
                safeBottom - margin - buttonSize * 0.74f), buttonSize * 1.34f);
            meleeRect = CenteredRect(new Vector2(fireRect.center.x - buttonSize * 1.24f,
                fireRect.center.y + buttonSize * 0.08f), buttonSize * 0.84f);
            jumpRect = CenteredRect(new Vector2(fireRect.center.x - buttonSize * 0.42f,
                fireRect.center.y - buttonSize * 1.23f), buttonSize * 0.82f);

            float utilityY = safeBottom - margin - buttonSize * 0.52f;
            float utilitySpacing = buttonSize * 1.13f;
            abilityRect = CenteredRect(new Vector2(Screen.width * 0.5f, utilityY), buttonSize * 0.94f);
            sprintRect = CenteredRect(new Vector2(abilityRect.center.x - utilitySpacing, utilityY), buttonSize * 0.88f);
            consumeRect = CenteredRect(new Vector2(abilityRect.center.x + utilitySpacing, utilityY), buttonSize * 0.88f);

            // Keep the controls below notches and rounded screen corners.
            float minimumTop = safeTop + margin;
            if (jumpRect.y < minimumTop) jumpRect.y = minimumTop;
        }

        private static Rect CenteredRect(Vector2 center, float size)
        {
            return new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
        }

        private bool CanStartMovement(Vector2 position)
        {
            return movementTouchId < 0
                   && position.x < Screen.width * 0.42f
                   && Vector2.Distance(position, joystickCenter) <= joystickRadius * 1.65f;
        }

        private bool CanStartLook(Vector2 position)
        {
            if (lookTouchId >= 0 || position.x < Screen.width * 0.34f) return false;
            // The upper-left menu is handled by GameMenuController's IMGUI button.
            return !(position.x < 170f && position.y < 190f);
        }

        private void UpdateMovement(Vector2 position)
        {
            Vector2 raw = new Vector2(position.x - joystickCenter.x, joystickCenter.y - position.y) / joystickRadius;
            float magnitude = Mathf.Clamp01(raw.magnitude);
            if (magnitude < 0.12f)
            {
                movement = Vector2.zero;
                return;
            }

            float normalizedMagnitude = Mathf.InverseLerp(0.12f, 1f, magnitude);
            movement = raw.normalized * normalizedMagnitude;
        }

        private bool TryGetAction(Vector2 position, out TouchAction action)
        {
            if (fireRect.Contains(position)) action = TouchAction.Fire;
            else if (meleeRect.Contains(position)) action = TouchAction.Melee;
            else if (abilityRect.Contains(position)) action = TouchAction.Ability;
            else if (jumpRect.Contains(position)) action = TouchAction.Jump;
            else if (consumeRect.Contains(position)) action = TouchAction.Consume;
            else if (sprintRect.Contains(position)) action = TouchAction.Sprint;
            else
            {
                action = default;
                return false;
            }
            return true;
        }

        private void SetActionState(TouchAction action, bool pressedThisFrame)
        {
            switch (action)
            {
                case TouchAction.Fire:
                    fireHeld = true;
                    firePressed |= pressedThisFrame;
                    break;
                case TouchAction.Melee:
                    meleePressed |= pressedThisFrame;
                    break;
                case TouchAction.Ability:
                    abilityPressed |= pressedThisFrame;
                    break;
                case TouchAction.Jump:
                    jumpHeld = true;
                    jumpPressed |= pressedThisFrame;
                    break;
                case TouchAction.Consume:
                    consumePressed |= pressedThisFrame;
                    break;
                case TouchAction.Sprint:
                    sprintHeld = true;
                    break;
            }
        }

        private void OnGUI()
        {
            if (!ControlsEnabled) return;
            BuildLayout();
            EnsureGuiResources();
            GUI.depth = -800;

            float joystickSize = joystickRadius * 2f;
            Rect joystickRect = CenteredRect(joystickCenter, joystickSize);
            DrawCircle(joystickRect, new Color(0.02f, 0.055f, 0.06f, 0.46f));

            Vector2 knobOffset = movement * (joystickRadius * 0.58f);
            Rect knobRect = CenteredRect(joystickCenter + new Vector2(knobOffset.x, -knobOffset.y), joystickRadius * 0.78f);
            DrawCircle(knobRect, new Color(0.32f, 0.9f, 0.58f, 0.74f));
            GUI.Label(new Rect(joystickRect.x, joystickRect.yMax + 2f, joystickRect.width, 26f), "MOVIMENTO", hintStyle);

            DrawCircularActionButton(fireRect, "TIRO", fireHeld, new Color(0.98f, 0.42f, 0.12f, 0.92f), true);
            DrawCircularActionButton(meleeRect, "GOLPE", meleePressed, new Color(0.86f, 0.26f, 0.16f, 0.88f));
            DrawCircularActionButton(jumpRect, "PULO", jumpHeld, new Color(0.12f, 0.62f, 0.94f, 0.88f));
            DrawHexActionButton(sprintRect, "CORRER", sprintHeld, new Color(0.92f, 0.67f, 0.08f, 0.9f));
            DrawHexActionButton(abilityRect, "PODER", abilityPressed, new Color(0.52f, 0.22f, 0.92f, 0.94f));
            DrawHexActionButton(consumeRect, "USAR", consumePressed, new Color(0.15f, 0.72f, 0.42f, 0.9f));
        }

        private void EnsureGuiResources()
        {
            if (circleTexture == null)
            {
                const int size = 96;
                circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "MobileControlCircle",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                Color[] pixels = new Color[size * size];
                Vector2 center = Vector2.one * ((size - 1) * 0.5f);
                float radius = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float edge = Mathf.InverseLerp(radius, radius - 2.5f, Vector2.Distance(new Vector2(x, y), center));
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(edge));
                    }
                }
                circleTexture.SetPixels(pixels);
                circleTexture.Apply(false, true);
            }

            if (hexTexture == null)
            {
                const int size = 96;
                hexTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "MobileControlHex",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                Color[] pixels = new Color[size * size];
                Vector2 center = Vector2.one * ((size - 1) * 0.5f);
                float radius = size * 0.48f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 point = (new Vector2(x, y) - center) / radius;
                        float hexDistance = Mathf.Max(Mathf.Abs(point.x) * 0.8660254f + Mathf.Abs(point.y) * 0.5f,
                            Mathf.Abs(point.y));
                        float alpha = Mathf.InverseLerp(1.02f, 0.96f, hexDistance);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                    }
                }
                hexTexture.SetPixels(pixels);
                hexTexture.Apply(false, true);
            }

            if (buttonLabelStyle == null)
            {
                buttonLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / 44f, 15f, 27f))
                };
                buttonLabelStyle.normal.textColor = Color.white;

                hintStyle = new GUIStyle(buttonLabelStyle)
                {
                    fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / 58f, 12f, 20f))
                };
                hintStyle.normal.textColor = new Color(0.9f, 1f, 0.94f, 0.82f);
            }
        }

        private void DrawCircularActionButton(Rect rect, string label, bool active, Color color, bool primary = false)
        {
            Color fill = active ? Color.Lerp(color, Color.white, 0.28f) : color;
            Rect border = ExpandRect(rect, primary ? 7f : 4f);
            DrawCircle(border, new Color(0.92f, 1f, 0.96f, primary ? 0.9f : 0.56f));
            DrawCircle(rect, new Color(0.015f, 0.035f, 0.04f, 0.9f));
            DrawCircle(ShrinkRect(rect, primary ? 10f : 7f), fill);
            GUI.Label(rect, label, buttonLabelStyle);
        }

        private void DrawHexActionButton(Rect rect, string label, bool active, Color color)
        {
            Color fill = active ? Color.Lerp(color, Color.white, 0.3f) : color;
            DrawTexture(ExpandRect(rect, 4f), hexTexture, new Color(0.92f, 1f, 0.96f, 0.74f));
            DrawTexture(rect, hexTexture, new Color(0.012f, 0.032f, 0.036f, 0.94f));
            DrawTexture(ShrinkRect(rect, 6f), hexTexture, fill);
            GUI.Label(rect, label, buttonLabelStyle);
        }

        private static Rect ExpandRect(Rect rect, float amount)
        {
            return new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }

        private static Rect ShrinkRect(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);
        }

        private void DrawCircle(Rect rect, Color color)
        {
            DrawTexture(rect, circleTexture, color);
        }

        private static void DrawTexture(Rect rect, Texture texture, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }
    }
}
