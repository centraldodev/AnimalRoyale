using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Main lobby, character wardrobe and settings panel shown before a match.</summary>
    public sealed class CharacterSelectionMenu : MonoBehaviour
    {
        private enum LobbyPanelTab
        {
            Online,
            Settings
        }

        private const float ReferenceWidth = 1672f;
        private const float ReferenceHeight = 941f;
        private const float SettingsPanelWidth = 458f;

        private GameBootstrap bootstrap;
        private AnimalType selected;
        private MenuAnimalPreview animalPreview;
        private Texture2D menuBackground;
        private Vector2 settingsScroll;
        private Vector2 onlineScroll;
        private LobbyPanelTab activeLobbyTab = LobbyPanelTab.Online;
        private bool animalDropdownOpen;
        private GameInputAction? waitingForBinding;
        private string bindingMessage = string.Empty;
        private float bindingMessageUntil;

        private GUIStyle logoStyle;
        private GUIStyle titleStyle;
        private GUIStyle sectionTitleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle centeredStyle;
        private GUIStyle buttonStyle;
        private GUIStyle playButtonStyle;
        private GUIStyle roomStyle;
        private GUIStyle keyStyle;

        public void Initialize(GameBootstrap gameBootstrap, AnimalType initialSelection)
        {
            bootstrap = gameBootstrap;
            selected = initialSelection;
            menuBackground = Resources.Load<Texture2D>("UI/MainMenu/MenuBackground");
            animalPreview = gameObject.AddComponent<MenuAnimalPreview>();
            animalPreview.Initialize(selected, 640, 960, false);
            ThirdPersonCamera.SetCursorLocked(false);
            OnlineMultiplayerManager.Instance?.SetLocalSelection(selected);
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                ThirdPersonCamera.SetCursorLocked(false);

            if (waitingForBinding.HasValue) return;

            int index = GameInput.ReadAnimalSelection();
            if (index >= 0 && index < AnimalRoster.Count) SelectAnimal((AnimalType)index);
            if (GameInput.ConfirmPressed() && !animalDropdownOpen) StartMatch();
        }

        private void OnGUI()
        {
            GUI.depth = -900;
            EnsureStyles();
            CaptureBindingEvent(Event.current);

            float uiScale = Mathf.Max(0.52f,
                Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight));
            float viewWidth = Screen.width / uiScale;
            float viewHeight = Screen.height / uiScale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

            DrawBackground(viewWidth, viewHeight);
            float settingsX = viewWidth - SettingsPanelWidth;
            float stageWidth = settingsX;
            DrawStage(stageWidth, viewHeight);
            DrawSettingsPanel(new Rect(settingsX, 0f, SettingsPanelWidth, viewHeight));
            DrawBottomNavigation(stageWidth, viewHeight);

            GUI.matrix = previousMatrix;
        }

        private void DrawBackground(float viewWidth, float viewHeight)
        {
            if (menuBackground != null)
            {
                GUI.DrawTexture(new Rect(0f, 0f, viewWidth, viewHeight), menuBackground,
                    ScaleMode.ScaleAndCrop, false);
            }
            else
            {
                Color previous = GUI.color;
                GUI.color = new Color(0.04f, 0.25f, 0.18f, 1f);
                GUI.DrawTexture(new Rect(0f, 0f, viewWidth, viewHeight), Texture2D.whiteTexture);
                GUI.color = previous;
            }

            Color oldColor = GUI.color;
            GUI.color = new Color(0.01f, 0.04f, 0.035f, 0.1f);
            GUI.DrawTexture(new Rect(0f, 0f, viewWidth, viewHeight), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void DrawStage(float stageWidth, float viewHeight)
        {
            float logoCenter = Mathf.Clamp(stageWidth * 0.64f, 610f, stageWidth - 260f);
            GUI.Label(new Rect(logoCenter - 230f, 12f, 460f, 86f),
                "<color=#FFB719>ANIMAL</color>\nROYALE", logoStyle);

            Rect subtitle = new Rect(logoCenter - 160f, 102f, 320f, 34f);
            DrawPanel(subtitle, new Color(0.015f, 0.06f, 0.045f, 0.94f),
                new Color(0.62f, 0.82f, 0.24f, 1f), 1.5f);
            GUI.Label(subtitle, "ESCOLHA SEU ANIMAL", centeredStyle);

            float previewWidth = Mathf.Min(580f, stageWidth * 0.49f);
            Rect previewRect = new Rect(Mathf.Max(16f, stageWidth * 0.06f), 122f,
                previewWidth, Mathf.Max(520f, viewHeight - 190f));
            if (animalPreview != null && animalPreview.Texture != null)
                GUI.DrawTexture(previewRect, animalPreview.Texture, ScaleMode.ScaleToFit, true);

            float controlsX = Mathf.Clamp(stageWidth * 0.57f, 620f, stageWidth - 440f);
            Rect wardrobeRect = new Rect(controlsX, 280f,
                Mathf.Min(330f, stageWidth - controlsX - 28f), 88f);

            float startWidth = Mathf.Min(430f, previewRect.width - 40f);
            Rect startButton = new Rect(previewRect.center.x - startWidth * 0.5f,
                viewHeight - 150f, startWidth, 68f);
            OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
            string startLabel = online != null && online.IsClientOnly
                ? "PRONTO — AGUARDAR LÍDER"
                : "INICIAR PARTIDA";
            bool hovered = startButton.Contains(Event.current.mousePosition);
            DrawPanel(startButton,
                hovered ? new Color(1f, 0.57f, 0.025f, 1f) : new Color(0.94f, 0.43f, 0.015f, 1f),
                hovered ? new Color(1f, 0.95f, 0.52f, 1f) : new Color(1f, 0.68f, 0.08f, 1f),
                hovered ? 3f : 2f);
            GUI.Label(startButton, startLabel, playButtonStyle);
            if (!animalDropdownOpen && GUI.Button(startButton, GUIContent.none, GUIStyle.none)) StartMatch();

            if (online != null && (online.IsConnected || online.IsBusy)
                               && !string.IsNullOrEmpty(online.Status))
            {
                Rect statusRect = new Rect(controlsX, wardrobeRect.yMax + 14f,
                    wardrobeRect.width, 46f);
                DrawPanel(statusRect, new Color(0.01f, 0.055f, 0.04f, 0.86f),
                    new Color(0.22f, 0.5f, 0.34f, 0.9f), 1f);
                GUI.Label(new Rect(statusRect.x + 10f, statusRect.y + 3f,
                    statusRect.width - 20f, statusRect.height - 6f), online.Status, smallStyle);
            }

            // Draw last so the opened list stays above the start button.
            DrawAnimalDropdown(wardrobeRect);
        }

        private void DrawAnimalDropdown(Rect rect)
        {
            const float optionHeight = 48f;
            Rect listRect = new Rect(rect.x, rect.yMax + 6f, rect.width,
                AnimalRoster.Count * optionHeight + 10f);
            Event currentEvent = Event.current;
            if (animalDropdownOpen && currentEvent.type == EventType.MouseDown
                                   && !rect.Contains(currentEvent.mousePosition)
                                   && !listRect.Contains(currentEvent.mousePosition))
            {
                animalDropdownOpen = false;
                currentEvent.Use();
            }

            bool hovered = rect.Contains(Event.current.mousePosition);
            DrawPanel(rect,
                hovered ? new Color(0.045f, 0.39f, 0.72f, 0.98f) : new Color(0.035f, 0.28f, 0.57f, 0.98f),
                new Color(0.88f, 0.9f, 0.24f, 1f), hovered ? 2.5f : 1.5f);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 10f, rect.width - 36f, 31f), "VESTUÁRIO", titleStyle);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 44f, rect.width - 36f, 28f),
                AnimalDefinition.Get(selected).DisplayName.ToUpperInvariant()
                + (animalDropdownOpen ? "   ▲" : "   ▼"), centeredStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) animalDropdownOpen = !animalDropdownOpen;

            if (!animalDropdownOpen) return;

            DrawPanel(listRect, new Color(0.035f, 0.28f, 0.57f, 0.78f),
                new Color(0.88f, 0.9f, 0.24f, 0.96f), 2f);
            for (int i = 0; i < AnimalRoster.Count; i++)
            {
                AnimalType type = (AnimalType)i;
                Rect option = new Rect(listRect.x + 5f, listRect.y + 5f + i * optionHeight,
                    listRect.width - 10f, optionHeight - 2f);
                bool selectedOption = selected == type;
                if (selectedOption)
                    RuntimeGuiTheme.DrawRoundedRect(option, new Color(0.055f, 0.47f, 0.82f, 0.9f));
                GUI.Label(option, AnimalDefinition.Get(type).DisplayName.ToUpperInvariant(), buttonStyle);
                if (GUI.Button(option, GUIContent.none, GUIStyle.none))
                {
                    SelectAnimal(type);
                    animalDropdownOpen = false;
                }
            }
        }

        private void DrawSettingsPanel(Rect panel)
        {
            DrawPanel(panel, new Color(0.004f, 0.045f, 0.035f, 0.82f),
                new Color(0.24f, 0.48f, 0.27f, 1f), 2f);
            GUI.Label(new Rect(panel.x + 22f, panel.y + 12f, panel.width - 44f, 40f),
                "LOBBY", titleStyle);

            const float tabGap = 8f;
            float tabWidth = (panel.width - 44f - tabGap) * 0.5f;
            Rect onlineTab = new Rect(panel.x + 18f, panel.y + 54f, tabWidth, 42f);
            Rect settingsTab = new Rect(onlineTab.xMax + tabGap, onlineTab.y, tabWidth, onlineTab.height);
            DrawLobbyPanelTab(onlineTab, "JOGAR ONLINE", LobbyPanelTab.Online);
            DrawLobbyPanelTab(settingsTab, "CONFIGURAÇÕES", LobbyPanelTab.Settings);

            OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
            if (activeLobbyTab == LobbyPanelTab.Online)
                DrawOnlineLobbyTab(panel, online);
            else
                DrawLobbySettingsTab(panel);
        }

        private void DrawLobbyPanelTab(Rect rect, string label, LobbyPanelTab tab)
        {
            bool selectedTab = activeLobbyTab == tab;
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color fill = selectedTab
                ? new Color(0.12f, 0.48f, 0.28f, 0.98f)
                : hovered
                    ? new Color(0.055f, 0.2f, 0.14f, 0.94f)
                    : new Color(0.025f, 0.105f, 0.075f, 0.9f);
            Color border = selectedTab
                ? new Color(0.58f, 1f, 0.66f, 1f)
                : new Color(0.16f, 0.38f, 0.25f, 1f);
            DrawPanel(rect, fill, border, selectedTab ? 2f : 1f);
            GUI.Label(rect, label, keyStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                activeLobbyTab = tab;
                animalDropdownOpen = false;
            }
        }

        private void DrawOnlineLobbyTab(Rect panel, OnlineMultiplayerManager online)
        {
            int humans = online != null && online.IsConnected ? online.HumanPlayerCount : 1;
            int target = online != null ? online.ParticipantTarget : 15;
            int friends = Mathf.Max(0, humans - 1);
            int bots = Mathf.Max(0, target - humans);

            Rect room = new Rect(panel.x + 18f, panel.y + 108f, panel.width - 36f, 76f);
            DrawPanel(room, new Color(0.02f, 0.085f, 0.06f, 0.7f),
                new Color(0.44f, 0.78f, 0.3f, 1f), 1f);
            GUI.Label(new Rect(room.x + 14f, room.y + 5f, room.width - 28f, 28f),
                online != null && online.IsConnected ? "SALA ONLINE — GRUPO ATIVO" : "SALA ATUAL — SOMENTE VOCÊ",
                roomStyle);
            GUI.Label(new Rect(room.x + 14f, room.y + 35f, room.width - 28f, 25f),
                $"{friends} AMIGO(S)  •  {bots} BOT(S)  •  TOTAL {target}", centeredStyle);

            Rect viewport = new Rect(panel.x + 14f, room.yMax + 12f,
                panel.width - 28f, panel.yMax - room.yMax - 28f);
            Rect content = new Rect(0f, 0f, viewport.width - 20f, 230f);
            onlineScroll = GUI.BeginScrollView(viewport, onlineScroll, content);
            DrawOnlineSection(content.width, 0f, online);
            GUI.EndScrollView();
        }

        private void DrawLobbySettingsTab(Rect panel)
        {
            Rect viewport = new Rect(panel.x + 14f, panel.y + 108f,
                panel.width - 28f, panel.height - 122f);
            float contentHeight = 820f + GameInputBindings.Definitions.Count * 58f;
            Rect content = new Rect(0f, 0f, viewport.width - 20f, contentHeight);
            settingsScroll = GUI.BeginScrollView(viewport, settingsScroll, content);

            float y = 0f;
            y = DrawSliderSetting(content.width, y, "VOLUME GERAL",
                GameSettings.MasterVolume, 0f, 1f, value => GameSettings.MasterVolume = value, true);
            y = DrawSliderSetting(content.width, y, "EFEITOS E AMBIENTE",
                GameSettings.EffectsAmbientVolume, 0f, 1f,
                value => GameSettings.EffectsAmbientVolume = value, true);
            y = DrawSliderSetting(content.width, y, "SENSIBILIDADE DA CÂMERA / MIRA PRIMÁRIA",
                GameSettings.MouseSensitivity, GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity,
                value => GameSettings.MouseSensitivity = value, false);
            y = DrawSliderSetting(content.width, y, "SENSIBILIDADE DA MIRA SECUNDÁRIA",
                GameSettings.AimMouseSensitivity, GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity,
                value => GameSettings.AimMouseSensitivity = value, false);
            y = DrawChoiceSetting(content.width, y, "LADO DO PERSONAGEM",
                "ESQUERDA", GameSettings.CharacterSide == CharacterScreenSide.Left,
                () => GameSettings.CharacterSide = CharacterScreenSide.Left,
                "DIREITA", GameSettings.CharacterSide == CharacterScreenSide.Right,
                () => GameSettings.CharacterSide = CharacterScreenSide.Right);
            y = DrawChoiceSetting(content.width, y, "CORRIDA AUTOMÁTICA",
                "DESLIGADA", !GameSettings.AutomaticSprint,
                () => GameSettings.AutomaticSprint = false,
                "ATIVADA", GameSettings.AutomaticSprint,
                () => GameSettings.AutomaticSprint = true);
            y = DrawChoiceSetting(content.width, y, "MODO DE DISPARO",
                "CLIQUE ÚNICO", GameSettings.RangedFireMode == RangedFireMode.SingleShot,
                () => GameSettings.RangedFireMode = RangedFireMode.SingleShot,
                "AUTOMÁTICO", GameSettings.RangedFireMode == RangedFireMode.Automatic,
                () => GameSettings.RangedFireMode = RangedFireMode.Automatic);

            GUI.Label(new Rect(8f, y + 4f, content.width - 16f, 28f), "ATALHOS DO TECLADO", sectionTitleStyle);
            y += 38f;
            IReadOnlyList<GameInputBindingDefinition> bindings = GameInputBindings.Definitions;
            for (int i = 0; i < bindings.Count; i++)
            {
                DrawBindingRow(new Rect(8f, y, content.width - 16f, 52f), bindings[i]);
                y += 58f;
            }

            if (Time.unscaledTime < bindingMessageUntil)
            {
                GUI.Label(new Rect(8f, y, content.width - 16f, 38f), bindingMessage, smallStyle);
                y += 42f;
            }

            Rect restore = new Rect(8f, y + 4f, content.width - 16f, 44f);
            if (DrawButton(restore, "RESTAURAR CONFIGURAÇÕES",
                    new Color(0.31f, 0.22f, 0.1f, 1f)))
            {
                GameInputBindings.RestoreDefaults();
                GameSettings.RestoreDefaults();
                bindingMessage = "CONFIGURAÇÕES PADRÃO RESTAURADAS";
                bindingMessageUntil = Time.unscaledTime + 2f;
            }

            GUI.EndScrollView();
        }

        private float DrawOnlineSection(float width, float y, OnlineMultiplayerManager online)
        {
            bool canStart = online != null && !online.IsBusy && !online.MatchStarted && !online.IsConnected;
            bool relayActive = online != null && online.IsRelaySession;
            float bodyHeight = relayActive ? 118f : 150f;
            Rect section = new Rect(8f, y, width - 16f, 46f + bodyHeight);
            DrawPanel(section, new Color(0.015f, 0.07f, 0.052f, 0.7f),
                new Color(0.18f, 0.34f, 0.22f, 1f), 1f);
            GUI.Label(new Rect(section.x + 14f, section.y + 9f, section.width - 28f, 28f),
                "JOGAR ONLINE", sectionTitleStyle);
            GUI.Label(new Rect(section.x + 14f, section.y + 36f, section.width - 28f, 34f),
                "Crie uma sala para jogar online e compartilhe o código com seus amigos.", smallStyle);

            if (online == null)
            {
                GUI.Label(new Rect(section.x + 14f, section.y + 78f, section.width - 28f, 40f),
                    "SERVIÇO ONLINE INDISPONÍVEL", centeredStyle);
                return section.yMax + 12f;
            }

            if (relayActive)
            {
                GUI.Label(new Rect(section.x + 14f, section.y + 78f, section.width - 28f, 20f),
                    "CÓDIGO DA SALA — COMPARTILHE COM SEUS AMIGOS", smallStyle);
                Rect codeField = new Rect(section.x + 14f, section.y + 100f, section.width - 28f, 34f);
                DrawPanel(codeField, new Color(0.06f, 0.13f, 0.1f, 1f), new Color(0.25f, 0.46f, 0.34f, 1f), 1f);
                GUI.TextField(codeField, online.JoinCode, keyStyle);
            }
            else
            {
                Rect createButton = new Rect(section.x + 14f, section.y + 78f, section.width - 28f, 38f);
                if (DrawButton(createButton, online.IsBusy ? "AGUARDE..." : "CRIAR SALA ONLINE",
                        canStart ? new Color(0.07f, 0.24f, 0.4f, 1f) : new Color(0.12f, 0.18f, 0.14f, 1f)) && canStart)
                    online.CreateRelaySession();

                Rect joinRow = new Rect(section.x + 14f, createButton.yMax + 10f, section.width - 28f, 38f);
                Rect codeInput = new Rect(joinRow.x, joinRow.y, joinRow.width * 0.6f, joinRow.height);
                Rect joinButton = new Rect(codeInput.xMax + 8f, joinRow.y, joinRow.width - codeInput.width - 8f, joinRow.height);
                DrawPanel(codeInput, new Color(0.06f, 0.13f, 0.1f, 1f), new Color(0.25f, 0.46f, 0.34f, 1f), 1f);
                online.JoinCodeInput = GUI.TextField(codeInput, online.JoinCodeInput, 8, keyStyle);
                if (DrawButton(joinButton, "ENTRAR",
                        canStart ? new Color(0.2f, 0.52f, 0.21f, 1f) : new Color(0.12f, 0.18f, 0.14f, 1f)) && canStart)
                    online.JoinRelaySession();
            }

            if (online.IsBusy || relayActive)
            {
                GUI.Label(new Rect(section.x + 14f, section.yMax - 26f, section.width - 28f, 20f),
                    online.Status, smallStyle);
            }
            return section.yMax + 12f;
        }

        private float DrawSliderSetting(float width, float y, string title, float current,
            float minimum, float maximum, Action<float> apply, bool percentage)
        {
            Rect rect = new Rect(8f, y, width - 16f, 78f);
            DrawPanel(rect, new Color(0.018f, 0.075f, 0.058f, 0.66f),
                new Color(0.14f, 0.28f, 0.2f, 1f), 1f);
            string valueLabel = percentage
                ? Mathf.RoundToInt(current * 100f) + "%"
                : current.ToString("0.00") + "x";
            GUI.Label(new Rect(rect.x + 14f, rect.y + 7f, rect.width - 28f, 24f),
                title + "   " + valueLabel, labelStyle);
            float value = GUI.HorizontalSlider(new Rect(rect.x + 17f, rect.y + 43f, rect.width - 34f, 22f),
                current, minimum, maximum);
            if (!Mathf.Approximately(value, current)) apply(value);
            return rect.yMax + 10f;
        }

        private float DrawChoiceSetting(float width, float y, string title,
            string leftLabel, bool leftSelected, Action selectLeft,
            string rightLabel, bool rightSelected, Action selectRight)
        {
            Rect rect = new Rect(8f, y, width - 16f, 91f);
            DrawPanel(rect, new Color(0.018f, 0.075f, 0.058f, 0.66f),
                new Color(0.14f, 0.28f, 0.2f, 1f), 1f);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 6f, rect.width - 28f, 25f), title, labelStyle);
            float buttonWidth = (rect.width - 42f) * 0.5f;
            Rect left = new Rect(rect.x + 12f, rect.y + 41f, buttonWidth, 36f);
            Rect right = new Rect(left.xMax + 18f, left.y, buttonWidth, left.height);
            DrawChoiceButton(left, leftLabel, leftSelected, selectLeft);
            DrawChoiceButton(right, rightLabel, rightSelected, selectRight);
            return rect.yMax + 10f;
        }

        private void DrawChoiceButton(Rect rect, string text, bool isSelected, Action select)
        {
            DrawPanel(rect,
                isSelected ? new Color(0.18f, 0.55f, 0.28f, 1f) : new Color(0.06f, 0.13f, 0.1f, 1f),
                isSelected ? new Color(0.62f, 1f, 0.68f, 1f) : new Color(0.2f, 0.38f, 0.28f, 1f), 1f);
            GUI.Label(rect, text, keyStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) select();
        }

        private void DrawBindingRow(Rect rect, GameInputBindingDefinition definition)
        {
            bool waiting = waitingForBinding == definition.Action;
            DrawPanel(rect,
                waiting ? new Color(0.08f, 0.2f, 0.14f, 0.9f) : new Color(0.018f, 0.075f, 0.058f, 0.66f),
                waiting ? new Color(0.52f, 1f, 0.68f, 1f) : new Color(0.14f, 0.28f, 0.2f, 1f), 1f);
            GUI.Label(new Rect(rect.x + 12f, rect.y, rect.width * 0.55f, rect.height),
                definition.Label, labelStyle);
            Rect key = new Rect(rect.x + rect.width * 0.58f, rect.y + 8f, rect.width * 0.39f, rect.height - 16f);
            DrawPanel(key,
                waiting ? new Color(0.2f, 0.62f, 0.39f, 1f) : new Color(0.06f, 0.13f, 0.1f, 1f),
                waiting ? Color.white : new Color(0.25f, 0.46f, 0.34f, 1f), 1f);
            GUI.Label(key, waiting ? "PRESSIONE..." : GameInputBindings.GetDisplayName(definition.Action), keyStyle);
            if (GUI.Button(key, GUIContent.none, GUIStyle.none)) waitingForBinding = definition.Action;
        }

        private void DrawBottomNavigation(float stageWidth, float viewHeight)
        {
            Rect bar = new Rect(0f, viewHeight - 62f, stageWidth, 62f);
            Color previous = GUI.color;
            GUI.color = new Color(0.005f, 0.025f, 0.02f, 0.83f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = previous;

            float buttonWidth = stageWidth / 3f;
            Rect news = new Rect(0f, bar.y, buttonWidth, bar.height);
            Rect credits = new Rect(buttonWidth, bar.y, buttonWidth, bar.height);
            Rect exit = new Rect(buttonWidth * 2f, bar.y, buttonWidth, bar.height);
            GUI.Label(news, "NOTÍCIAS  —  EM BREVE", buttonStyle);
            GUI.Label(credits, "CRÉDITOS  —  EM BREVE", buttonStyle);
            GUI.Label(exit, "SAIR DO JOGO", buttonStyle);
            if (GUI.Button(exit, GUIContent.none, GUIStyle.none)) QuitGame();
        }

        private void SelectAnimal(AnimalType type)
        {
            if ((int)type < 0 || (int)type >= AnimalRoster.Count || selected == type) return;
            selected = type;
            animalPreview?.SetAnimal(type);
            OnlineMultiplayerManager.Instance?.SetLocalSelection(type);
        }

        private void StartMatch()
        {
            if (bootstrap == null) return;
            OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
            if (online != null && online.HandleStartRequest(selected)) return;
            bootstrap.StartMatch(selected);
            Destroy(gameObject);
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
                    bindingMessage = "ALTERAÇÃO CANCELADA";
                    bindingMessageUntil = Time.unscaledTime + 1.5f;
                    currentEvent.Use();
                    return;
                }
                captured = currentEvent.keyCode;
            }
            else if (currentEvent.type == EventType.MouseDown && currentEvent.button is >= 0 and <= 6)
            {
                captured = (KeyCode)((int)KeyCode.Mouse0 + currentEvent.button);
            }

            if (captured == KeyCode.None) return;
            GameInputAction action = waitingForBinding.Value;
            if (GameInputBindings.TryRebind(action, captured, out GameInputAction? swapped))
            {
                bindingMessage = swapped.HasValue
                    ? $"{GameInputBindings.GetActionLabel(action)} ALTERADO; "
                      + $"{GameInputBindings.GetActionLabel(swapped.Value)} RECEBEU A TECLA ANTERIOR"
                    : $"{GameInputBindings.GetActionLabel(action)}: {GameInputBindings.GetKeyDisplayName(captured)}";
                bindingMessageUntil = Time.unscaledTime + 3f;
            }
            waitingForBinding = null;
            currentEvent.Use();
        }

        private bool DrawButton(Rect rect, string text, Color fill)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color hoveredFill = Color.Lerp(fill, Color.white, 0.12f);
            DrawPanel(rect, hovered ? hoveredFill : fill,
                Color.Lerp(fill, Color.white, hovered ? 0.62f : 0.38f), hovered ? 1.8f : 1f);
            GUI.Label(rect, text, keyStyle);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private static void DrawPanel(Rect rect, Color fill, Color border, float borderSize)
        {
            RuntimeGuiTheme.DrawPanel(rect, fill, border, borderSize);
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

        private void OnDestroy()
        {
            animalPreview?.Dispose();
        }

        private void EnsureStyles()
        {
            if (logoStyle != null) return;
            RuntimeGuiTheme.Ensure();
            logoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 39,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = Color.white }
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            sectionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.55f, 0.95f, 0.38f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.94f, 0.96f, 0.91f) }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.7f, 0.84f, 0.75f) }
            };
            centeredStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.9f, 0.95f, 0.84f) }
            };
            buttonStyle = new GUIStyle(centeredStyle) { fontSize = 15 };
            playButtonStyle = new GUIStyle(buttonStyle) { fontSize = 24 };
            roomStyle = new GUIStyle(centeredStyle)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.65f, 1f, 0.46f) }
            };
            keyStyle = new GUIStyle(centeredStyle) { fontSize = 11 };
        }
    }
}
