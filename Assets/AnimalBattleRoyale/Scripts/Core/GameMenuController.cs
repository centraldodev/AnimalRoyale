using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalBattleRoyale
{
    [DefaultExecutionOrder(1000)]
    public sealed class GameMenuController : MonoBehaviour
    {
        private enum MenuPage { Closed, Pause, Settings, Admin }

        public static GameMenuController Instance { get; private set; }

        private MenuPage page;
        private bool inGame;
        private bool settingsOpenedFromGame;
        private GameInputAction? waitingForBinding;
        private string bindingMessage = string.Empty;
        private float bindingMessageUntil;
        private Vector2 adminScroll;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle labelStyle;
        private GUIStyle centeredStyle;
        private GUIStyle buttonStyle;
        private GUIStyle keyStyle;

        private readonly struct AdminControl
        {
            public readonly string Label;
            public readonly string Unit;
            public readonly float Min;
            public readonly float Max;
            public readonly Func<float> Get;
            public readonly Action<float> Set;

            public AdminControl(string label, string unit, float min, float max, Func<float> get, Action<float> set)
            {
                Label = label;
                Unit = unit;
                Min = min;
                Max = max;
                Get = get;
                Set = set;
            }
        }

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

            if (page == MenuPage.Admin)
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

        private void OpenAdminMenu()
        {
            if (!CanOpenAdminMenu()) return;
            waitingForBinding = null;
            page = MenuPage.Admin;
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

            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1.18f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            if (inGame && page == MenuPage.Closed
                       && (BattleRoyaleManager.Instance == null || !BattleRoyaleManager.Instance.MatchFinished))
            {
                DrawHamburgerButton();
            }

            if (page != MenuPage.Closed)
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(0.005f, 0.012f, 0.014f, 0.78f);
                GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
                GUI.color = previousColor;

                if (page == MenuPage.Pause) DrawPauseMenu(width, height);
                else if (page == MenuPage.Settings) DrawSettings(width, height);
                else DrawAdminMenu(width, height);
            }

            GUI.matrix = previousMatrix;
        }

        private void DrawHamburgerButton()
        {
            Rect button = new Rect(20f, 126f, 104f, 40f);
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
            bool canAdmin = CanOpenAdminMenu();
            float panelHeight = canAdmin ? 500f : 430f;
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
            if (canAdmin && DrawMenuButton(new Rect(buttonX, panel.y + 378f, buttonWidth, 50f), "ADMIN DO SERVIDOR",
                    new Color(0.42f, 0.24f, 0.5f))) OpenAdminMenu();
            if (DrawMenuButton(new Rect(buttonX, panel.y + 236f, buttonWidth, 50f), "VOLTAR À TELA INICIAL",
                    new Color(0.32f, 0.29f, 0.16f))) ReturnToMainMenu();
            if (DrawMenuButton(new Rect(buttonX, panel.y + 316f, buttonWidth, 50f), "SAIR DO JOGO",
                    new Color(0.48f, 0.12f, 0.1f))) QuitGame();
        }

        private void DrawAdminMenu(float viewWidth, float viewHeight)
        {
            float panelWidth = Mathf.Min(930f, viewWidth - 36f);
            float panelHeight = Mathf.Min(690f, viewHeight - 30f);
            Rect panel = new Rect((viewWidth - panelWidth) * 0.5f, (viewHeight - panelHeight) * 0.5f, panelWidth, panelHeight);
            RuntimeGuiTheme.DrawPanel(panel, new Color(0.025f, 0.04f, 0.052f, 0.995f),
                new Color(0.72f, 0.52f, 1f, 1f), 2f);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 18f, panel.width - 56f, 38f), "ADMIN DO SERVIDOR", titleStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 55f, panel.width - 60f, 28f),
                "Ajustes aplicam imediatamente nesta partida local ou no host", subtitleStyle);

            Rect scrollRect = new Rect(panel.x + 26f, panel.y + 96f, panel.width - 52f, panel.height - 168f);
            Rect contentRect = new Rect(0f, 0f, scrollRect.width - 18f, 950f);
            adminScroll = GUI.BeginScrollView(scrollRect, adminScroll, contentRect);

            float y = 0f;
            y = DrawAdminSection(contentRect, y, "TIGRE - PULO LONGO", new[]
            {
                new AdminControl("DURACAO DO PULO", "s", 0.25f, 1.8f, () => ServerGameTuning.TigerLeapDuration, value => ServerGameTuning.TigerLeapDuration = value),
                new AdminControl("DISTANCIA DO PULO", "m/s", 10f, 45f, () => ServerGameTuning.TigerLeapSpeed, value => ServerGameTuning.TigerLeapSpeed = value),
                new AdminControl("ALTURA DO PULO", "m/s", 3f, 16f, () => ServerGameTuning.TigerLeapUpSpeed, value => ServerGameTuning.TigerLeapUpSpeed = value),
                new AdminControl("RAIO DO IMPACTO", "m", 0.35f, 3f, () => ServerGameTuning.TigerLeapHitRadius, value => ServerGameTuning.TigerLeapHitRadius = value),
                new AdminControl("DANO DO PULO", "hp", 0f, 70f, () => ServerGameTuning.TigerLeapDamage, value => ServerGameTuning.TigerLeapDamage = value),
                new AdminControl("EMPURRAO DO PULO", "", 0f, 28f, () => ServerGameTuning.TigerLeapKnockback, value => ServerGameTuning.TigerLeapKnockback = value)
            });
            y = DrawAdminSection(contentRect, y + 12f, "TIRO - PROJETEIS", new[]
            {
                new AdminControl("VELOCIDADE DO PROJETIL", "m/s", 8f, 80f, () => ServerGameTuning.ProjectileSpeed, value => ServerGameTuning.ProjectileSpeed = value),
                new AdminControl("ALCANCE DO TIRO", "s", 0.5f, 10f, () => ServerGameTuning.ProjectileRangeSeconds, value => ServerGameTuning.ProjectileRangeSeconds = value),
                new AdminControl("QUEDA DO PROJETIL", "x", 0f, 4f, () => ServerGameTuning.ProjectileGravityMultiplier, value => ServerGameTuning.ProjectileGravityMultiplier = value),
                new AdminControl("ELEVACAO DO PROJETIL", "x", 0f, 4f, () => ServerGameTuning.ProjectileLiftMultiplier, value => ServerGameTuning.ProjectileLiftMultiplier = value),
                new AdminControl("DANO DO TIRO", "x", 0f, 4f, () => ServerGameTuning.ProjectileDamageMultiplier, value => ServerGameTuning.ProjectileDamageMultiplier = value),
                new AdminControl("RAIO DO TIRO", "x", 0.25f, 3f, () => ServerGameTuning.ProjectileRadiusMultiplier, value => ServerGameTuning.ProjectileRadiusMultiplier = value),
                new AdminControl("TIROS POR SEGUNDO", "tiros/s", 1f, 20f, () => ServerGameTuning.RangedShotsPerSecond, value => ServerGameTuning.RangedShotsPerSecond = value),
                new AdminControl("TEMPO DE RECARGA", "s", 0.1f, 8f, () => ServerGameTuning.RangedReloadSeconds, value => ServerGameTuning.RangedReloadSeconds = value)
            });
            y = DrawAdminSection(contentRect, y + 12f, "AGUIA - VOO E PLANEIO", new[]
            {
                new AdminControl("DURACAO DO VOO", "s", 1f, 14f, () => ServerGameTuning.EagleFlightDuration, value => ServerGameTuning.EagleFlightDuration = value),
                new AdminControl("ALTURA DO VOO", "m/s", 3f, 18f, () => ServerGameTuning.EagleJumpSpeed, value => ServerGameTuning.EagleJumpSpeed = value),
                new AdminControl("DISTANCIA DO PLANEIO", "x", 0.5f, 3.5f, () => ServerGameTuning.EagleFlySpeedBonus, value => ServerGameTuning.EagleFlySpeedBonus = value),
                new AdminControl("VELOCIDADE DE DESCIDA", "x", 0f, 1.5f, () => ServerGameTuning.EagleGlideGravityMultiplier, value => ServerGameTuning.EagleGlideGravityMultiplier = value),
                new AdminControl("LIMITE DE QUEDA", "m/s", -9f, -0.4f, () => ServerGameTuning.EagleMaximumFallSpeed, value => ServerGameTuning.EagleMaximumFallSpeed = value)
            });
            y = DrawAdminSection(contentRect, y + 12f, "ZONA SEGURA", new[]
            {
                new AdminControl("ESPERA PARA FECHAR", "s", 0f, 120f, () => ServerGameTuning.SafeZoneWaitBeforeShrink, value => ServerGameTuning.SafeZoneWaitBeforeShrink = value),
                new AdminControl("VELOCIDADE DE FECHAMENTO", "m/s", 0.05f, 5f, () => ServerGameTuning.SafeZoneShrinkSpeed, value => ServerGameTuning.SafeZoneShrinkSpeed = value),
                new AdminControl("DANO FORA DA ZONA", "HP/s", 1f, 100f, () => ServerGameTuning.SafeZoneDamagePerSecond, value => ServerGameTuning.SafeZoneDamagePerSecond = value)
            });
            DrawAdminSection(contentRect, y + 12f, "GERAL", new[]
            {
                new AdminControl("QUEDA DO PULO", "x", 0.35f, 3f, () => ServerGameTuning.JumpGravityMultiplier, value => ServerGameTuning.JumpGravityMultiplier = value)
            });

            GUI.EndScrollView();

            float footerY = panel.yMax - 58f;
            if (DrawMenuButton(new Rect(panel.x + 30f, footerY, 230f, 40f), "RESTAURAR PADRAO",
                    new Color(0.34f, 0.25f, 0.12f)))
            {
                ServerGameTuning.RestoreDefaults();
                NotifyServerTuningChanged();
            }
            if (DrawMenuButton(new Rect(panel.xMax - 190f, footerY, 160f, 40f), "VOLTAR",
                    new Color(0.14f, 0.47f, 0.32f))) page = MenuPage.Pause;
        }

        private float DrawAdminSection(Rect contentRect, float y, string title, IReadOnlyList<AdminControl> controls)
        {
            const int columns = 2;
            const float gap = 14f;
            const float rowHeight = 52f;
            int rows = Mathf.CeilToInt(controls.Count / (float)columns);
            Rect section = new Rect(contentRect.x, y, contentRect.width, 40f + rows * rowHeight);
            RuntimeGuiTheme.DrawPanel(section, new Color(0.035f, 0.06f, 0.078f, 0.96f),
                new Color(0.22f, 0.28f, 0.34f, 1f), 1f, false);
            GUI.Label(new Rect(section.x + 14f, section.y + 8f, section.width - 28f, 24f), title, buttonStyle);

            float controlWidth = (section.width - 28f - gap) / columns;
            for (int index = 0; index < controls.Count; index++)
            {
                int column = index % columns;
                int row = index / columns;
                Rect controlRect = new Rect(section.x + 14f + column * (controlWidth + gap),
                    section.y + 36f + row * rowHeight, controlWidth, 44f);
                DrawAdminSlider(controlRect, controls[index]);
            }

            return section.yMax;
        }

        private void DrawAdminSlider(Rect rect, AdminControl control)
        {
            float previousValue = Mathf.Clamp(control.Get(), control.Min, control.Max);
            string unitText = string.IsNullOrEmpty(control.Unit) ? string.Empty : " " + control.Unit;
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 18f),
                $"{control.Label}  {previousValue:0.##}{unitText}", labelStyle);
            float value = GUI.HorizontalSlider(new Rect(rect.x, rect.y + 22f, rect.width, 20f),
                previousValue, control.Min, control.Max);
            if (!Mathf.Approximately(value, previousValue))
            {
                control.Set(value);
                NotifyServerTuningChanged();
            }
        }

        private static bool CanOpenAdminMenu()
        {
            OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
            return online == null || !online.IsConnected || online.IsHost;
        }

        private static void NotifyServerTuningChanged()
        {
            ServerGameTuning.SaveCurrentAsProjectDefault();
            OnlineMultiplayerManager.Instance?.BroadcastServerTuning();
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
            GUI.Label(new Rect(panel.x + 30f, panel.y + 55f, panel.width - 60f, 28f),
                mobile
                    ? "Arraste o lado direito para mirar e use os botões na tela"
                    : waitingForBinding.HasValue
                    ? "Pressione uma tecla ou botão do mouse • ESC cancela"
                    : "Clique em um atalho para trocar sua tecla", subtitleStyle);

            const int columns = 2;
            const float gap = 16f;
            float contentX = panel.x + 30f;
            float contentY = panel.y + 98f;
            float columnWidth = (panel.width - 60f - gap) / columns;
            float rowHeight = 58f;
            float settingsY;
            if (mobile)
            {
                Rect touchGuide = new Rect(contentX, contentY, panel.width - 60f, 88f);
                RuntimeGuiTheme.DrawPanel(touchGuide, new Color(0.035f, 0.07f, 0.07f, 0.96f),
                    new Color(0.18f, 0.62f, 0.46f, 1f), 1f, false);
                GUI.Label(new Rect(touchGuide.x + 16f, touchGuide.y + 8f, touchGuide.width - 32f, 24f),
                    "CONTROLES TOUCHSCREEN", buttonStyle);
                GUI.Label(new Rect(touchGuide.x + 16f, touchGuide.y + 37f, touchGuide.width - 32f, 42f),
                    "Metade esquerda: movimento WASD  •  Metade direita: câmera  •  Atirar: segure e arraste para atirar e mirar; fora do círculo, apenas câmera",
                    centeredStyle);
                settingsY = touchGuide.yMax + 12f;
            }
            else
            {
                IReadOnlyList<GameInputBindingDefinition> definitions = GameInputBindings.Definitions;
                for (int index = 0; index < definitions.Count; index++)
                {
                    GameInputBindingDefinition definition = definitions[index];
                    int column = index % columns;
                    int row = index / columns;
                    Rect rowRect = new Rect(contentX + column * (columnWidth + gap), contentY + row * rowHeight,
                        columnWidth, 54f);
                    DrawBindingRow(rowRect, definition);
                }

                settingsY = contentY + Mathf.CeilToInt(definitions.Count / (float)columns) * rowHeight + 4f;
            }
            DrawGameplaySettings(new Rect(contentX, settingsY, panel.width - 60f, 156f));

            if (Time.unscaledTime < bindingMessageUntil)
            {
                GUI.Label(new Rect(panel.x + 30f, panel.yMax - 84f, panel.width - 60f, 22f), bindingMessage, subtitleStyle);
            }

            float footerY = panel.yMax - 58f;
            if (DrawMenuButton(new Rect(panel.x + 30f, footerY, 230f, 40f), "RESTAURAR PADRÕES",
                    new Color(0.34f, 0.25f, 0.12f)))
            {
                GameInputBindings.RestoreDefaults();
                GameSettings.RestoreDefaults();
                bindingMessage = "ATALHOS PADRÃO RESTAURADOS";
                bindingMessageUntil = Time.unscaledTime + 2f;
            }
            if (DrawMenuButton(new Rect(panel.xMax - 190f, footerY, 160f, 40f), "VOLTAR",
                    new Color(0.14f, 0.47f, 0.32f))) CloseSettings();
        }

        private void DrawGameplaySettings(Rect rect)
        {
            float halfWidth = (rect.width - 16f) * 0.5f;
            const float controlHeight = 74f;
            DrawSensitivityControl(new Rect(rect.x, rect.y, halfWidth, controlHeight));
            DrawCharacterSideControl(new Rect(rect.x + halfWidth + 16f, rect.y, halfWidth, controlHeight));
            DrawAutomaticSprintControl(new Rect(rect.x, rect.y + controlHeight + 8f, halfWidth, controlHeight));
            DrawRangedFireModeControl(new Rect(rect.x + halfWidth + 16f, rect.y + controlHeight + 8f,
                halfWidth, controlHeight));
        }

        private void DrawSensitivityControl(Rect rect)
        {
            RuntimeGuiTheme.DrawPanel(rect, new Color(0.035f, 0.07f, 0.07f, 0.96f),
                new Color(0.14f, 0.27f, 0.25f, 1f), 1f, false);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, 24f),
                $"SENSIBILIDADE DA CÂMERA  {GameSettings.MouseSensitivity:0.00}x", labelStyle);

            Rect sliderRect = new Rect(rect.x + 14f, rect.y + 36f, rect.width - 28f, 24f);
            float previousValue = GameSettings.MouseSensitivity;
            float value = GUI.HorizontalSlider(sliderRect, previousValue,
                GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity);
            if (!Mathf.Approximately(value, previousValue))
                GameSettings.MouseSensitivity = value;
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
