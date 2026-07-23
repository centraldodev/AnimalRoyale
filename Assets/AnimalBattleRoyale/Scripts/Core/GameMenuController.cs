using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalBattleRoyale
{
    [DefaultExecutionOrder(1000)]
    public sealed class GameMenuController : MonoBehaviour
    {
        private enum MenuPage { Closed, Pause, Settings }
        private enum SettingsCategory { Audio, Controls, Gameplay }
        private enum ControlCategory { Movement, Combat, Interaction }

        private static readonly GameInputAction[] MovementBindings =
        {
            GameInputAction.MoveForward,
            GameInputAction.MoveBackward,
            GameInputAction.MoveLeft,
            GameInputAction.MoveRight,
            GameInputAction.Jump,
            GameInputAction.Sprint,
            GameInputAction.Descend
        };

        private static readonly GameInputAction[] CombatBindings =
        {
            GameInputAction.RangedAttack,
            GameInputAction.Reload,
            GameInputAction.WeaponPrimary,
            GameInputAction.WeaponSecondary,
            GameInputAction.WeaponThird,
            GameInputAction.MeleeAttack,
            GameInputAction.Aim
        };

        private static readonly GameInputAction[] InteractionBindings =
        {
            GameInputAction.Ability,
            GameInputAction.Consume
        };

        public static GameMenuController Instance { get; private set; }

        private MenuPage page;
        private bool inGame;
        private bool settingsOpenedFromGame;
        private GameInputAction? waitingForBinding;
        private SettingsCategory settingsCategory = SettingsCategory.Controls;
        private ControlCategory controlCategory = ControlCategory.Movement;
        private string bindingMessage = string.Empty;
        private float bindingMessageUntil;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle labelStyle;
        private GUIStyle centeredStyle;
        private GUIStyle buttonStyle;
        private GUIStyle keyStyle;

        public bool IsOpen => page != MenuPage.Closed;
        public bool IsBlockingGameplayInput => IsOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (!GameInput.EscapePressed()) return;

            if (waitingForBinding.HasValue)
            {
                waitingForBinding = null;
                bindingMessage = "ALTERAÇÃO CANCELADA";
                bindingMessageUntil = Time.unscaledTime + 1.5f;
                return;
            }

            if (!inGame)
            {
                if (page == MenuPage.Settings) CloseSettings();
                return;
            }

            if (page == MenuPage.Settings)
            {
                page = MenuPage.Pause;
                return;
            }

            if (page == MenuPage.Pause) CloseInGameMenu();
            else OpenPauseMenu();
        }

        public void SetInGame(bool value)
        {
            inGame = value;
            waitingForBinding = null;
            page = MenuPage.Closed;
            ApplyPausedState(false);
            if (!value) ThirdPersonCamera.SetCursorLocked(false);
        }

        public void OpenSettingsFromMainMenu()
        {
            settingsOpenedFromGame = false;
            waitingForBinding = null;
            page = MenuPage.Settings;
            ThirdPersonCamera.SetCursorLocked(false);
        }

        public void OpenPauseMenu()
        {
            if (!inGame) return;
            settingsOpenedFromGame = true;
            waitingForBinding = null;
            page = MenuPage.Pause;
            ApplyPausedState(true);
        }

        private void OpenInGameSettings()
        {
            settingsOpenedFromGame = true;
            waitingForBinding = null;
            page = MenuPage.Settings;
            ApplyPausedState(true);
        }

        private void CloseInGameMenu()
        {
            waitingForBinding = null;
            page = MenuPage.Closed;
            ApplyPausedState(false);
        }

        private void CloseSettings()
        {
            waitingForBinding = null;
            if (settingsOpenedFromGame) page = MenuPage.Pause;
            else page = MenuPage.Closed;
        }

        private static void ApplyPausedState(bool paused)
        {
            bool pauseSimulation = paused && !IsOnlineMatchActive();
            Time.timeScale = pauseSimulation ? 0f : 1f;
            AudioListener.pause = paused;
            ThirdPersonCamera.SetCursorLocked(!paused);
        }

        private static bool IsOnlineMatchActive()
        {
            OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
            return online != null && online.IsConnected && online.MatchStarted;
        }

        private void OnGUI()
        {
            GUI.depth = -1000;
            EnsureStyles();

            // Same reference-size scaling as BattleRoyaleManager's HUD (kept in sync so the
            // menu and the HUD grow together) — 1.18 used to cap this well before it matched
            // large/high-DPI screens, making the pause menu read as tiny relative to the
            // inflated virtual canvas behind it.
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 2.4f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            if (inGame && page == MenuPage.Closed
                       && (BattleRoyaleManager.Instance == null || !BattleRoyaleManager.Instance.MatchFinished))
            {
                DrawHamburgerButton(width);
            }

            if (page != MenuPage.Closed)
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(0.005f, 0.012f, 0.014f, 0.78f);
                GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
                GUI.color = previousColor;

                if (page == MenuPage.Pause) DrawPauseMenu(width, height);
                else DrawSettings(width, height);
            }

            GUI.matrix = previousMatrix;
        }

        private void DrawHamburgerButton(float viewWidth)
        {
            Rect button = new Rect(viewWidth - 20f - 104f, 20f, 104f, 40f);
            RuntimeGuiTheme.DrawPanel(button, new Color(0.025f, 0.05f, 0.052f, 0.95f),
                new Color(0.3f, 0.76f, 0.56f, 1f), 1f);
            Color previous = GUI.color;
            GUI.color = Color.white;
            for (int line = 0; line < 3; line++)
            {
                GUI.DrawTexture(new Rect(button.x + 12f, button.y + 10f + line * 8f, 23f, 3f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
            GUI.Label(new Rect(button.x + 43f, button.y, button.width - 49f, button.height), "MENU", buttonStyle);
            if (GUI.Button(button, GUIContent.none, GUIStyle.none)) OpenPauseMenu();
        }

        private void DrawPauseMenu(float viewWidth, float viewHeight)
        {
            float panelWidth = Mathf.Min(460f, viewWidth - 40f);
            const float panelHeight = 430f;
            Rect panel = new Rect((viewWidth - panelWidth) * 0.5f, (viewHeight - panelHeight) * 0.5f, panelWidth, panelHeight);
            RuntimeGuiTheme.DrawPanel(panel, new Color(0.028f, 0.052f, 0.054f, 0.99f),
                new Color(0.35f, 0.9f, 0.62f, 1f), 2f);
            bool onlineMatch = IsOnlineMatchActive();
            GUI.Label(new Rect(panel.x + 24f, panel.y + 24f, panel.width - 48f, 42f),
                onlineMatch ? "MENU DO JOGO" : "JOGO PAUSADO", titleStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 67f, panel.width - 60f, 28f),
                onlineMatch ? "A partida online continua enquanto este menu está aberto"
                    : "ESC também fecha este menu", subtitleStyle);

            float buttonX = panel.x + 58f;
            float buttonWidth = panel.width - 116f;
            if (DrawMenuButton(new Rect(buttonX, panel.y + 112f, buttonWidth, 50f), "CONTINUAR",
                    new Color(0.16f, 0.62f, 0.38f))) CloseInGameMenu();
            if (DrawMenuButton(new Rect(buttonX, panel.y + 174f, buttonWidth, 50f), "CONFIGURAÇÕES",
                    new Color(0.12f, 0.38f, 0.42f))) OpenInGameSettings();
            if (DrawMenuButton(new Rect(buttonX, panel.y + 236f, buttonWidth, 50f), "VOLTAR À TELA INICIAL",
                    new Color(0.32f, 0.29f, 0.16f))) ReturnToMainMenu();
            if (DrawMenuButton(new Rect(buttonX, panel.y + 316f, buttonWidth, 50f), "SAIR DO JOGO",
                    new Color(0.48f, 0.12f, 0.1f))) QuitGame();
        }

        private void DrawSettings(float viewWidth, float viewHeight)
        {
            bool mobile = Application.isMobilePlatform;
            if (!mobile) CaptureBindingEvent(Event.current);

            float panelWidth = Mathf.Min(850f, viewWidth - 36f);
            float panelHeight = Mathf.Min(650f, viewHeight - 30f);
            Rect panel = new Rect((viewWidth - panelWidth) * 0.5f, (viewHeight - panelHeight) * 0.5f, panelWidth, panelHeight);
            RuntimeGuiTheme.DrawPanel(panel, new Color(0.025f, 0.047f, 0.05f, 0.995f),
                new Color(0.32f, 0.82f, 0.6f, 1f), 2f);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 18f, panel.width - 56f, 38f), "CONFIGURAÇÕES", titleStyle);
            string categoryHint = settingsCategory switch
            {
                SettingsCategory.Audio => "Ajuste o volume geral e a intensidade dos efeitos e do ambiente",
                SettingsCategory.Gameplay => "Personalize câmera, personagem e comportamento durante a partida",
                _ => mobile
                    ? "Consulte os controles touchscreen usados durante a partida"
                    : "Escolha uma categoria e clique em um atalho para trocar sua tecla"
            };
            GUI.Label(new Rect(panel.x + 30f, panel.y + 55f, panel.width - 60f, 28f),
                waitingForBinding.HasValue
                    ? "Pressione uma tecla ou botão do mouse • ESC cancela"
                    : categoryHint, subtitleStyle);

            const float tabGap = 10f;
            Rect tabStrip = new Rect(panel.x + 30f, panel.y + 91f, panel.width - 60f, 42f);
            float tabWidth = (tabStrip.width - tabGap * 2f) / 3f;
            DrawSettingsCategoryTab(new Rect(tabStrip.x, tabStrip.y, tabWidth, tabStrip.height),
                "ÁUDIO", SettingsCategory.Audio);
            DrawSettingsCategoryTab(new Rect(tabStrip.x + tabWidth + tabGap, tabStrip.y, tabWidth, tabStrip.height),
                "CONTROLES", SettingsCategory.Controls);
            DrawSettingsCategoryTab(new Rect(tabStrip.x + (tabWidth + tabGap) * 2f, tabStrip.y, tabWidth, tabStrip.height),
                "JOGABILIDADE", SettingsCategory.Gameplay);

            float footerY = panel.yMax - 58f;
            Rect content = new Rect(panel.x + 30f, tabStrip.yMax + 14f,
                panel.width - 60f, footerY - tabStrip.yMax - 28f);
            RuntimeGuiTheme.DrawPanel(content, new Color(0.018f, 0.038f, 0.04f, 0.96f),
                new Color(0.1f, 0.26f, 0.22f, 1f), 1f, false);

            switch (settingsCategory)
            {
                case SettingsCategory.Audio:
                    DrawAudioSettings(content);
                    break;
                case SettingsCategory.Gameplay:
                    GUI.Label(new Rect(content.x + 18f, content.y + 10f, content.width - 36f, 30f),
                        "JOGABILIDADE", buttonStyle);
                    DrawGameplaySettings(new Rect(content.x + 16f, content.y + 52f,
                        content.width - 32f, 238f));
                    break;
                default:
                    DrawControlsSettings(content, mobile);
                    break;
            }

            if (Time.unscaledTime < bindingMessageUntil)
            {
                GUI.Label(new Rect(content.x + 16f, content.yMax - 28f, content.width - 32f, 22f),
                    bindingMessage, subtitleStyle);
            }

            if (DrawMenuButton(new Rect(panel.x + 30f, footerY, 230f, 40f), "RESTAURAR PADRÕES",
                    new Color(0.34f, 0.25f, 0.12f)))
            {
                GameInputBindings.RestoreDefaults();
                GameSettings.RestoreDefaults();
                bindingMessage = "CONFIGURAÇÕES PADRÃO RESTAURADAS";
                bindingMessageUntil = Time.unscaledTime + 2f;
            }
            if (DrawMenuButton(new Rect(panel.xMax - 190f, footerY, 160f, 40f), "VOLTAR",
                    new Color(0.14f, 0.47f, 0.32f))) CloseSettings();
        }

        private void DrawSettingsCategoryTab(Rect rect, string text, SettingsCategory category)
        {
            bool selected = settingsCategory == category;
            RuntimeGuiTheme.DrawPanel(rect,
                selected ? new Color(0.16f, 0.56f, 0.36f, 1f) : new Color(0.045f, 0.1f, 0.095f, 1f),
                selected ? Color.white : new Color(0.25f, 0.52f, 0.43f, 1f), 1f, false);
            GUI.Label(rect, text, buttonStyle);
            if (!GUI.Button(rect, GUIContent.none, GUIStyle.none)) return;
            settingsCategory = category;
            waitingForBinding = null;
        }

        private void DrawAudioSettings(Rect content)
        {
            GUI.Label(new Rect(content.x + 18f, content.y + 10f, content.width - 36f, 30f),
                "ÁUDIO", buttonStyle);
            const float gap = 16f;
            float cardWidth = (content.width - 48f - gap) * 0.5f;
            Rect master = new Rect(content.x + 16f, content.y + 58f, cardWidth, 116f);
            Rect effects = new Rect(master.xMax + gap, master.y, cardWidth, master.height);
            DrawVolumeControl(master, "VOLUME GERAL", GameSettings.MasterVolume,
                value => GameSettings.MasterVolume = value);
            DrawVolumeControl(effects, "EFEITOS E AMBIENTE", GameSettings.EffectsAmbientVolume,
                value => GameSettings.EffectsAmbientVolume = value);

            Rect note = new Rect(content.x + 16f, master.yMax + 18f, content.width - 32f, 74f);
            RuntimeGuiTheme.DrawPanel(note, new Color(0.03f, 0.065f, 0.065f, 0.92f),
                new Color(0.12f, 0.3f, 0.26f, 1f), 1f, false);
            GUI.Label(new Rect(note.x + 18f, note.y + 8f, note.width - 36f, note.height - 16f),
                "VOLUME GERAL controla toda a saída do jogo. EFEITOS E AMBIENTE ajusta tiros, recargas, coletas, animais e sons da floresta.",
                centeredStyle);
        }

        private void DrawVolumeControl(Rect rect, string label, float value, Action<float> setValue)
        {
            RuntimeGuiTheme.DrawPanel(rect, new Color(0.035f, 0.07f, 0.07f, 0.96f),
                new Color(0.18f, 0.46f, 0.37f, 1f), 1f, false);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 26f),
                $"{label}  {Mathf.RoundToInt(value * 100f)}%", labelStyle);
            Rect slider = new Rect(rect.x + 18f, rect.y + 51f, rect.width - 36f, 28f);
            float newValue = GUI.HorizontalSlider(slider, value, 0f, 1f);
            if (!Mathf.Approximately(newValue, value)) setValue(newValue);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 79f, rect.width - 36f, 24f),
                value <= 0.001f ? "MUDO" : value >= 0.999f ? "MÁXIMO" : "AJUSTE LIVRE",
                centeredStyle);
        }

        private void DrawControlsSettings(Rect content, bool mobile)
        {
            GUI.Label(new Rect(content.x + 18f, content.y + 8f, content.width - 36f, 28f),
                mobile ? "CONTROLES TOUCHSCREEN" : "ATALHOS DO TECLADO", buttonStyle);
            if (mobile)
            {
                Rect guide = new Rect(content.x + 18f, content.y + 54f, content.width - 36f, 120f);
                RuntimeGuiTheme.DrawPanel(guide, new Color(0.035f, 0.07f, 0.07f, 0.96f),
                    new Color(0.18f, 0.62f, 0.46f, 1f), 1f, false);
                GUI.Label(new Rect(guide.x + 18f, guide.y + 14f, guide.width - 36f, guide.height - 28f),
                    "Metade esquerda: movimento • Metade direita: câmera\n"
                    + "Botões de ação: atirar, bater, pular, habilidade e usar",
                    centeredStyle);
                return;
            }

            const float categoryGap = 10f;
            Rect categoryStrip = new Rect(content.x + 16f, content.y + 44f, content.width - 32f, 36f);
            float categoryWidth = (categoryStrip.width - categoryGap * 2f) / 3f;
            DrawControlCategoryTab(new Rect(categoryStrip.x, categoryStrip.y, categoryWidth, categoryStrip.height),
                "MOVIMENTO", ControlCategory.Movement);
            DrawControlCategoryTab(new Rect(categoryStrip.x + categoryWidth + categoryGap, categoryStrip.y,
                    categoryWidth, categoryStrip.height),
                "COMBATE E MUNIÇÃO", ControlCategory.Combat);
            DrawControlCategoryTab(new Rect(categoryStrip.x + (categoryWidth + categoryGap) * 2f, categoryStrip.y,
                    categoryWidth, categoryStrip.height),
                "INTERAÇÃO", ControlCategory.Interaction);

            GameInputAction[] actions = controlCategory switch
            {
                ControlCategory.Combat => CombatBindings,
                ControlCategory.Interaction => InteractionBindings,
                _ => MovementBindings
            };
            const float rowGap = 10f;
            const float columnGap = 14f;
            float columnWidth = (content.width - 32f - columnGap) * 0.5f;
            float startY = categoryStrip.yMax + 14f;
            for (int index = 0; index < actions.Length; index++)
            {
                int column = index % 2;
                int row = index / 2;
                Rect rowRect = new Rect(content.x + 16f + column * (columnWidth + columnGap),
                    startY + row * (52f + rowGap), columnWidth, 52f);
                DrawBindingRow(rowRect, GetBindingDefinition(actions[index]));
            }
        }

        private void DrawControlCategoryTab(Rect rect, string text, ControlCategory category)
        {
            bool selected = controlCategory == category;
            RuntimeGuiTheme.DrawPanel(rect,
                selected ? new Color(0.12f, 0.38f, 0.28f, 1f) : new Color(0.04f, 0.09f, 0.085f, 1f),
                selected ? new Color(0.46f, 0.94f, 0.68f, 1f) : new Color(0.16f, 0.34f, 0.29f, 1f),
                1f, false);
            GUI.Label(rect, text, keyStyle);
            if (!GUI.Button(rect, GUIContent.none, GUIStyle.none)) return;
            controlCategory = category;
            waitingForBinding = null;
        }

        private static GameInputBindingDefinition GetBindingDefinition(GameInputAction action)
        {
            IReadOnlyList<GameInputBindingDefinition> definitions = GameInputBindings.Definitions;
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i].Action == action) return definitions[i];
            return default;
        }

        private void DrawGameplaySettings(Rect rect)
        {
            float halfWidth = (rect.width - 16f) * 0.5f;
            const float controlHeight = 74f;
            DrawSensitivityControl(new Rect(rect.x, rect.y, halfWidth, controlHeight));
            DrawAimSensitivityControl(new Rect(rect.x + halfWidth + 16f, rect.y, halfWidth, controlHeight));
            DrawCharacterSideControl(new Rect(rect.x, rect.y + controlHeight + 8f, halfWidth, controlHeight));
            DrawAutomaticSprintControl(new Rect(rect.x + halfWidth + 16f, rect.y + controlHeight + 8f,
                halfWidth, controlHeight));
            DrawRangedFireModeControl(new Rect(rect.x, rect.y + (controlHeight + 8f) * 2f,
                rect.width, controlHeight));
        }

        private void DrawSensitivityControl(Rect rect)
        {
            RuntimeGuiTheme.DrawPanel(rect, new Color(0.035f, 0.07f, 0.07f, 0.96f),
                new Color(0.14f, 0.27f, 0.25f, 1f), 1f, false);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, 24f),
                $"MIRA PRIMÁRIA / CÂMERA  {GameSettings.MouseSensitivity:0.00}x", labelStyle);

            Rect sliderRect = new Rect(rect.x + 14f, rect.y + 36f, rect.width - 28f, 24f);
            float previousValue = GameSettings.MouseSensitivity;
            float value = GUI.HorizontalSlider(sliderRect, previousValue,
                GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity);
            if (!Mathf.Approximately(value, previousValue))
                GameSettings.MouseSensitivity = value;
        }

        private void DrawAimSensitivityControl(Rect rect)
        {
            RuntimeGuiTheme.DrawPanel(rect, new Color(0.035f, 0.07f, 0.07f, 0.96f),
                new Color(0.14f, 0.27f, 0.25f, 1f), 1f, false);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, 24f),
                $"MIRA SECUNDÁRIA  {GameSettings.AimMouseSensitivity:0.00}x", labelStyle);

            Rect sliderRect = new Rect(rect.x + 14f, rect.y + 36f, rect.width - 28f, 24f);
            float previousValue = GameSettings.AimMouseSensitivity;
            float value = GUI.HorizontalSlider(sliderRect, previousValue,
                GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity);
            if (!Mathf.Approximately(value, previousValue))
                GameSettings.AimMouseSensitivity = value;
        }

        private void DrawCharacterSideControl(Rect rect)
        {
            RuntimeGuiTheme.DrawPanel(rect, new Color(0.035f, 0.07f, 0.07f, 0.96f),
                new Color(0.14f, 0.27f, 0.25f, 1f), 1f, false);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, 24f), "LADO DO BONECO", labelStyle);

            float buttonWidth = (rect.width - 38f) * 0.5f;
            Rect leftButton = new Rect(rect.x + 12f, rect.y + 32f, buttonWidth, 30f);
            Rect rightButton = new Rect(leftButton.xMax + 14f, leftButton.y, buttonWidth, leftButton.height);
            DrawSideButton(leftButton, "ESQUERDA", CharacterScreenSide.Left);
            DrawSideButton(rightButton, "DIREITA", CharacterScreenSide.Right);
        }

        private void DrawSideButton(Rect rect, string text, CharacterScreenSide side)
        {
            bool selected = GameSettings.CharacterSide == side;
            RuntimeGuiTheme.DrawPanel(rect,
                selected ? new Color(0.2f, 0.62f, 0.39f, 1f) : new Color(0.1f, 0.17f, 0.17f, 1f),
                selected ? Color.white : new Color(0.34f, 0.62f, 0.53f, 1f), 1f, false);
            GUI.Label(rect, text, keyStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                GameSettings.CharacterSide = side;
        }

        private void DrawAutomaticSprintControl(Rect rect)
        {
            RuntimeGuiTheme.DrawPanel(rect, new Color(0.035f, 0.07f, 0.07f, 0.96f),
                new Color(0.14f, 0.27f, 0.25f, 1f), 1f, false);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, 24f),
                "CORRIDA AUTOMÁTICA", labelStyle);

            float buttonWidth = (rect.width - 38f) * 0.5f;
            Rect offButton = new Rect(rect.x + 12f, rect.y + 32f, buttonWidth, 30f);
            Rect onButton = new Rect(offButton.xMax + 14f, offButton.y, buttonWidth, offButton.height);
            DrawBooleanSettingButton(offButton, "DESLIGADA", !GameSettings.AutomaticSprint,
                () => GameSettings.AutomaticSprint = false);
            DrawBooleanSettingButton(onButton, "ATIVADA", GameSettings.AutomaticSprint,
                () => GameSettings.AutomaticSprint = true);
        }

        private void DrawRangedFireModeControl(Rect rect)
        {
            RuntimeGuiTheme.DrawPanel(rect, new Color(0.035f, 0.07f, 0.07f, 0.96f),
                new Color(0.14f, 0.27f, 0.25f, 1f), 1f, false);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, 24f),
                "MODO DE DISPARO", labelStyle);

            float buttonWidth = (rect.width - 38f) * 0.5f;
            Rect singleButton = new Rect(rect.x + 12f, rect.y + 32f, buttonWidth, 30f);
            Rect automaticButton = new Rect(singleButton.xMax + 14f, singleButton.y, buttonWidth,
                singleButton.height);
            DrawBooleanSettingButton(singleButton, "CLIQUE ÚNICO",
                GameSettings.RangedFireMode == RangedFireMode.SingleShot,
                () => GameSettings.RangedFireMode = RangedFireMode.SingleShot);
            DrawBooleanSettingButton(automaticButton, "AUTOMÁTICO",
                GameSettings.RangedFireMode == RangedFireMode.Automatic,
                () => GameSettings.RangedFireMode = RangedFireMode.Automatic);
        }

        private void DrawBooleanSettingButton(Rect rect, string text, bool selected, Action select)
        {
            RuntimeGuiTheme.DrawPanel(rect,
                selected ? new Color(0.2f, 0.62f, 0.39f, 1f) : new Color(0.1f, 0.17f, 0.17f, 1f),
                selected ? Color.white : new Color(0.34f, 0.62f, 0.53f, 1f), 1f, false);
            GUI.Label(rect, text, keyStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) select();
        }

        private void DrawBindingRow(Rect rect, GameInputBindingDefinition definition)
        {
            bool waiting = waitingForBinding == definition.Action;
            RuntimeGuiTheme.DrawPanel(rect,
                waiting ? new Color(0.08f, 0.2f, 0.15f, 1f) : new Color(0.035f, 0.07f, 0.07f, 0.96f),
                waiting ? new Color(0.42f, 1f, 0.68f, 1f) : new Color(0.14f, 0.27f, 0.25f, 1f), 1f, false);
            GUI.Label(new Rect(rect.x + 12f, rect.y, rect.width * 0.52f, rect.height), definition.Label, labelStyle);

            Rect keyButton = new Rect(rect.x + rect.width * 0.53f, rect.y + 8f, rect.width * 0.44f - 8f, rect.height - 16f);
            RuntimeGuiTheme.DrawPanel(keyButton,
                waiting ? new Color(0.2f, 0.62f, 0.39f, 1f) : new Color(0.1f, 0.17f, 0.17f, 1f),
                waiting ? Color.white : new Color(0.34f, 0.62f, 0.53f, 1f), 1f, false);
            GUI.Label(keyButton, waiting ? "AGUARDANDO..." : GameInputBindings.GetDisplayName(definition.Action), keyStyle);
            if (GUI.Button(keyButton, GUIContent.none, GUIStyle.none)) waitingForBinding = definition.Action;
        }

        private void CaptureBindingEvent(Event currentEvent)
        {
            if (!waitingForBinding.HasValue || currentEvent == null) return;

            KeyCode captured = KeyCode.None;
            if (currentEvent.type == EventType.KeyDown)
            {
                if (currentEvent.keyCode == KeyCode.Escape)
                {
                    waitingForBinding = null;
                    currentEvent.Use();
                    return;
                }
                captured = currentEvent.keyCode;
            }
            else if (currentEvent.type == EventType.MouseDown && currentEvent.button >= 0 && currentEvent.button <= 6)
            {
                captured = (KeyCode)((int)KeyCode.Mouse0 + currentEvent.button);
            }

            if (captured == KeyCode.None) return;

            GameInputAction action = waitingForBinding.Value;
            if (GameInputBindings.TryRebind(action, captured, out GameInputAction? swapped))
            {
                bindingMessage = swapped.HasValue
                    ? $"{GameInputBindings.GetActionLabel(action)} alterado; {GameInputBindings.GetActionLabel(swapped.Value)} recebeu a tecla anterior"
                    : $"{GameInputBindings.GetActionLabel(action)} alterado para {GameInputBindings.GetKeyDisplayName(captured)}";
                bindingMessageUntil = Time.unscaledTime + 3f;
            }
            waitingForBinding = null;
            currentEvent.Use();
        }

        private bool DrawMenuButton(Rect rect, string text, Color fill)
        {
            RuntimeGuiTheme.DrawPanel(rect, fill, Color.Lerp(fill, Color.white, 0.45f), 1f);
            GUI.Label(rect, text, buttonStyle);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void ReturnToMainMenu()
        {
            BattleRoyaleManager.Instance?.PrepareForSceneExit();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        private static void QuitGame()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            RuntimeGuiTheme.Ensure();
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.64f, 0.84f, 0.75f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.88f, 0.95f, 0.92f) }
            };
            centeredStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(centeredStyle) { fontSize = 14, normal = { textColor = Color.white } };
            keyStyle = new GUIStyle(centeredStyle) { fontSize = 11, clipping = TextClipping.Clip, normal = { textColor = Color.white } };
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Instance = null;
        }
    }
}
