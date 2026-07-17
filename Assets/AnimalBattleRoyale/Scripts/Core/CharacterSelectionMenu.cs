using System;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class CharacterSelectionMenu : MonoBehaviour
    {
        private GameBootstrap bootstrap;
        private AnimalType selected;
        private GUIStyle logoStyle;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle selectedStyle;
        private GUIStyle buttonStyle;
        private GUIStyle playButtonStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle footerStyle;
        private GUIStyle roomStyle;
        private GUIStyle onlineTitleStyle;
        private GUIStyle onlineButtonStyle;
        private GUIStyle onlineFieldStyle;
        private readonly Texture2D[] portraitArt = new Texture2D[AnimalRoster.Count];
        private readonly RenderTexture[] fallbackArt = new RenderTexture[AnimalRoster.Count];

        public void Initialize(GameBootstrap gameBootstrap, AnimalType initialSelection)
        {
            bootstrap = gameBootstrap;
            selected = initialSelection;
            ThirdPersonCamera.SetCursorLocked(false);

            for (int i = 0; i < AnimalRoster.Count; i++)
            {
                AnimalType type = (AnimalType)i;
                portraitArt[i] = Resources.Load<Texture2D>($"UI/CharacterPortraits/{type}");
                if (portraitArt[i] == null) fallbackArt[i] = AnimalPreviewRenderer.Create(type, 256);
            }
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                ThirdPersonCamera.SetCursorLocked(false);
            }

            if (GameMenuController.Instance != null && GameMenuController.Instance.IsBlockingGameplayInput) return;

            int index = GameInput.ReadAnimalSelection();
            if (index >= 0 && index < AnimalRoster.Count) selected = (AnimalType)index;
            OnlineMultiplayerManager.Instance?.SetLocalSelection(selected);
            if (GameInput.ConfirmPressed()) StartMatch();
        }

        private void OnGUI()
        {
            EnsureStyles();
            float uiScale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1.18f);
            float viewWidth = Screen.width / uiScale;
            float viewHeight = Screen.height / uiScale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

            DrawBackdrop(viewWidth, viewHeight);
            DrawHeader(viewWidth);

            float contentWidth = Mathf.Min(1360f, viewWidth - 44f);
            float contentX = (viewWidth - contentWidth) * 0.5f;
            const float gap = 14f;
            const float cardY = 124f;
            float cardHeight = Mathf.Clamp(viewHeight * 0.42f, 300f, 360f);
            float cardWidth = (contentWidth - gap * (AnimalRoster.Count - 1f)) / AnimalRoster.Count;

            for (int i = 0; i < AnimalRoster.Count; i++)
            {
                Rect baseRect = new Rect(contentX + i * (cardWidth + gap), cardY, cardWidth, cardHeight);
                bool hovered = baseRect.Contains(Event.current.mousePosition);
                DrawAnimalCard((AnimalType)i, baseRect, hovered);
            }

            DrawFooter(contentX, contentWidth, cardY + cardHeight + 12f, viewHeight);
            GUI.matrix = previousMatrix;
        }

        private void DrawBackdrop(float viewWidth, float viewHeight)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.005f, 0.035f, 0.024f, 0.38f);
            GUI.DrawTexture(new Rect(0f, 0f, viewWidth, viewHeight), Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0.018f, 0.012f, 0.28f);
            GUI.DrawTexture(new Rect(0f, 0f, viewWidth, 118f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, viewHeight - 105f, viewWidth, 105f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawHeader(float viewWidth)
        {
            OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
            string participants = online != null && online.IsConnected
                ? $"{online.HumanPlayerCount} HUMANOS + {online.PlannedBotCount} BOTS"
                : "10/10 PARTICIPANTES";
            Rect roomPanel = new Rect(22f, 20f, 188f, 68f);
            DrawCartoonPanel(roomPanel, new Color(0.025f, 0.055f, 0.045f, 0.94f),
                new Color(0.66f, 0.72f, 0.42f, 0.95f), 1f);
            GUI.Label(new Rect(roomPanel.x + 12f, roomPanel.y + 7f, roomPanel.width - 24f, 29f),
                "SALA:  <color=#55FF72>001</color>", roomStyle);
            GUI.Label(new Rect(roomPanel.x + 12f, roomPanel.y + 38f, roomPanel.width - 24f, 20f),
                participants, selectedStyle);

            GUI.Label(new Rect(viewWidth * 0.5f - 260f, 3f, 520f, 78f),
                "<color=#FFB20F>ANIMAL</color>\nROYALE", logoStyle);

            Rect subtitlePanel = new Rect(viewWidth * 0.5f - 154f, 82f, 308f, 32f);
            DrawCartoonPanel(subtitlePanel, new Color(0.04f, 0.09f, 0.06f, 0.96f),
                new Color(0.3f, 0.52f, 0.3f, 1f), 1f);
            GUI.Label(subtitlePanel, "◆  ESCOLHA SEU ANIMAL  ◆", subtitleStyle);

            Rect settingsButton = new Rect(viewWidth - 214f, 20f, 192f, 48f);
            bool settingsHovered = settingsButton.Contains(Event.current.mousePosition);
            DrawCartoonPanel(settingsButton,
                settingsHovered ? new Color(0.13f, 0.31f, 0.24f, 0.99f) : new Color(0.035f, 0.075f, 0.065f, 0.96f),
                settingsHovered ? new Color(0.52f, 1f, 0.68f, 1f) : new Color(0.48f, 0.64f, 0.42f, 1f),
                settingsHovered ? 2f : 1f);
            GUI.Label(settingsButton, "⚙  CONFIGURAÇÕES", buttonStyle);
            if (GUI.Button(settingsButton, GUIContent.none, GUIStyle.none))
            {
                GameMenuController.Instance?.OpenSettingsFromMainMenu();
            }
        }

        private void DrawAnimalCard(AnimalType type, Rect baseRect, bool hovered)
        {
            int index = (int)type;
            AnimalStats stats = AnimalDefinition.Get(type);
            bool isSelected = selected == type;
            Color accent = GetCardAccent(type);
            float lift = hovered ? 6f : 0f;
            Rect card = new Rect(baseRect.x - lift * 0.35f, baseRect.y - lift, baseRect.width + lift * 0.7f, baseRect.height + lift);

            float diameter = Mathf.Min(card.width - 20f, card.height * 0.66f);
            Rect circle = new Rect(card.center.x - diameter * 0.5f, card.y + 6f, diameter, diameter);

            Color ringColor = isSelected
                ? new Color(0.2f, 1f, 0.45f, 1f)
                : hovered
                    ? accent
                    : new Color(accent.r, accent.g, accent.b, 0.55f);

            RuntimeGuiTheme.DrawCircle(new Rect(circle.x + 2f, circle.y + 4f, circle.width, circle.height),
                new Color(0f, 0f, 0f, 0.32f));
            RuntimeGuiTheme.DrawCircle(circle, ringColor, true);
            Rect inner = new Rect(circle.x + 6f, circle.y + 6f, circle.width - 12f, circle.height - 12f);
            RuntimeGuiTheme.DrawCircle(inner, new Color(0.94f, 0.925f, 0.89f, 1f));
            DrawFrontPortrait(index, inner);

            GUI.Label(new Rect(card.x, circle.yMax + 10f, card.width, 32f), stats.DisplayName.ToUpperInvariant(), cardTitleStyle);

            if (GUI.Button(card, GUIContent.none, GUIStyle.none)) selected = type;
        }

        private void DrawFrontPortrait(int index, Rect inner)
        {
            Texture2D texture = portraitArt[index];
            Texture source = texture != null ? texture : fallbackArt[index];
            if (source == null) return;
            Rect drawRect = new Rect(inner.x + 5f, inner.y + 5f, inner.width - 10f, inner.height - 10f);
            GUI.DrawTexture(drawRect, source, ScaleMode.ScaleToFit, true);
        }

        private void DrawFooter(float contentX, float contentWidth, float requestedY, float viewHeight)
        {
            float playButtonY = Mathf.Min(requestedY, viewHeight - 226f);
            Rect playButton = new Rect(contentX + contentWidth * 0.5f - 210f, playButtonY, 420f, 58f);
            bool hovered = playButton.Contains(Event.current.mousePosition);
            DrawCartoonPanel(playButton,
                hovered ? new Color(1f, 0.63f, 0.05f, 1f) : new Color(0.94f, 0.47f, 0.025f, 1f),
                hovered ? new Color(1f, 0.94f, 0.5f, 1f) : new Color(1f, 0.72f, 0.12f, 1f),
                hovered ? 3f : 2f);
            GUI.Label(playButton, "INICIAR PARTIDA", playButtonStyle);
            if (GUI.Button(playButton, GUIContent.none, GUIStyle.none)) StartMatch();

            DrawOnlinePanel(contentX, contentWidth, playButton.yMax + 16f);
        }

        private void DrawOnlinePanel(float contentX, float contentWidth, float y)
        {
            OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
            if (online == null) return;

            bool connected = online.IsConnected;
            float panelWidth = Mathf.Min(560f, contentWidth - 80f);
            float panelHeight = connected ? 92f : 134f;
            Rect panel = new Rect(contentX + contentWidth * 0.5f - panelWidth * 0.5f, y, panelWidth, panelHeight);

            RuntimeGuiTheme.DrawPanel(new Rect(panel.x + 3f, panel.y + 4f, panel.width, panel.height),
                new Color(0f, 0f, 0f, 0.3f), new Color(0f, 0f, 0f, 0f), 0f, false);
            DrawCartoonPanel(panel, new Color(0.014f, 0.05f, 0.043f, 0.97f),
                connected ? new Color(0.22f, 1f, 0.5f, 0.95f) : new Color(0.32f, 0.6f, 0.46f, 0.9f), 1f);
            RuntimeGuiTheme.DrawRoundedRect(new Rect(panel.x + panel.width * 0.5f - 78f, panel.y - 11f, 156f, 22f),
                new Color(0.014f, 0.05f, 0.043f, 0.97f));
            GUI.Label(new Rect(panel.x + panel.width * 0.5f - 78f, panel.y - 11f, 156f, 22f), "◆  JOGO ONLINE  ◆", onlineTitleStyle);

            if (connected)
            {
                string role = online.IsHost ? "HOST" : "CLIENTE";
                GUI.Label(new Rect(panel.x + 18f, panel.y + 16f, panel.width - 36f, 24f),
                    $"{role}   •   HUMANOS {online.HumanPlayerCount}/{online.ParticipantTarget}   •   BOTS {online.PlannedBotCount}",
                    footerStyle);
                string codeLine = string.IsNullOrEmpty(online.JoinCode) ? online.Status : $"CÓDIGO  {online.JoinCode}";
                GUI.Label(new Rect(panel.x + 18f, panel.y + 44f, panel.width - 36f, 24f), codeLine, footerStyle);
                GUI.Label(new Rect(panel.x + 18f, panel.y + 68f, panel.width - 36f, 20f), online.Status, footerStyle);
                return;
            }

            const float rowHeight = 32f;
            const float rowGap = 8f;
            float row1Y = panel.y + 20f;
            DrawOnlineButton(new Rect(panel.x + 16f, row1Y, 148f, rowHeight),
                online.IsBusy ? "AGUARDE..." : "CRIAR ONLINE", online.CreateRelaySession);
            online.JoinCodeInput = GUI.TextField(new Rect(panel.x + 172f, row1Y, 88f, rowHeight),
                online.JoinCodeInput, 8, onlineFieldStyle).ToUpperInvariant();
            DrawOnlineButton(new Rect(panel.x + 268f, row1Y, panel.width - 284f, rowHeight), "ENTRAR", online.JoinRelaySession);

            float row2Y = row1Y + rowHeight + rowGap;
            DrawOnlineButton(new Rect(panel.x + 16f, row2Y, 148f, rowHeight), "HOST LOCAL", online.StartLocalHost);
            online.DirectAddress = GUI.TextField(new Rect(panel.x + 172f, row2Y, 108f, rowHeight),
                online.DirectAddress, 32, onlineFieldStyle);
            DrawOnlineButton(new Rect(panel.x + 288f, row2Y, panel.width - 304f, rowHeight), "LAN", online.StartLocalClient);

            GUI.Label(new Rect(panel.x + 16f, row2Y + rowHeight + 6f, panel.width - 32f, 18f), online.Status, footerStyle);
        }

        private void DrawOnlineButton(Rect rect, string label, Action action)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            DrawCartoonPanel(rect,
                hovered ? new Color(0.15f, 0.52f, 0.31f, 1f) : new Color(0.055f, 0.2f, 0.15f, 1f),
                hovered ? new Color(0.52f, 1f, 0.68f, 1f) : new Color(0.27f, 0.72f, 0.48f, 1f), hovered ? 2f : 1f);
            GUI.Label(rect, label, onlineButtonStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) action?.Invoke();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < fallbackArt.Length; i++) AnimalPreviewRenderer.Release(fallbackArt[i]);
        }

        private void StartMatch()
        {
            if (bootstrap == null) return;
            OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
            if (online != null && online.HandleStartRequest(selected)) return;
            bootstrap.StartMatch(selected);
            Destroy(gameObject);
        }

        private void EnsureStyles()
        {
            if (logoStyle != null) return;
            RuntimeGuiTheme.Ensure();
            logoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 31,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = Color.white }
            };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.92f, 0.95f, 0.77f) } };
            selectedStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, normal = { textColor = new Color(0.72f, 1f, 0.78f) } };
            buttonStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            playButtonStyle = new GUIStyle(buttonStyle) { fontSize = 24 };
            cardTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            footerStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, normal = { textColor = new Color(0.94f, 0.95f, 0.82f) } };
            roomStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.white } };
            onlineTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.55f, 1f, 0.72f) } };
            onlineButtonStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, normal = { textColor = Color.white } };
            onlineFieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white, background = Texture2D.grayTexture } };
        }

        private static Color GetCardAccent(AnimalType type)
        {
            return type switch
            {
                AnimalType.Tiger => new Color(0.16f, 1f, 0.36f),
                AnimalType.Ant => new Color(0.38f, 0.86f, 0.18f),
                AnimalType.Eagle => new Color(0.08f, 0.68f, 1f),
                AnimalType.Monkey => new Color(1f, 0.58f, 0.08f),
                _ => new Color(0.3f, 0.9f, 0.6f)
            };
        }

        private static void DrawCartoonPanel(Rect rect, Color fill, Color border, float borderSize)
        {
            RuntimeGuiTheme.DrawPanel(rect, fill, border, borderSize);
        }
    }
}
