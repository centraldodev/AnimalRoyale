using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class CharacterSelectionMenu : MonoBehaviour
    {
        private static readonly Rect FrontPortraitUv = new Rect(0f, 0.12f, 0.36f, 0.76f);

        private GameBootstrap bootstrap;
        private AnimalType selected;
        private GUIStyle logoStyle;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle textStyle;
        private GUIStyle selectedStyle;
        private GUIStyle buttonStyle;
        private GUIStyle playButtonStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle statStyle;
        private GUIStyle statValueStyle;
        private GUIStyle footerStyle;
        private GUIStyle roomStyle;
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
            float cardHeight = Mathf.Clamp(viewHeight - 310f, 400f, 560f);
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

            if (isSelected)
            {
                RuntimeGuiTheme.DrawPanel(new Rect(card.x - 4f, card.y - 4f, card.width + 8f, card.height + 8f),
                    new Color(0.08f, 0.34f, 0.17f, 0.42f), new Color(0.18f, 1f, 0.42f, 0.88f), 2f);
            }

            DrawCartoonPanel(card,
                hovered ? new Color(0.018f, 0.075f, 0.06f, 0.98f) : new Color(0.012f, 0.046f, 0.04f, 0.96f),
                isSelected ? new Color(0.2f, 1f, 0.45f, 1f) : hovered ? accent : new Color(accent.r, accent.g, accent.b, 0.68f),
                isSelected || hovered ? 2f : 1f);

            RuntimeGuiTheme.DrawRoundedRect(new Rect(card.x + 8f, card.y + 8f, card.width - 16f, 4f), accent);

            float portraitHeight = card.height * 0.48f;
            Rect portrait = new Rect(card.x + 10f, card.y + 16f, card.width - 20f, portraitHeight);
            RuntimeGuiTheme.DrawPanel(portrait, new Color(0.94f, 0.925f, 0.89f, 1f),
                new Color(accent.r, accent.g, accent.b, hovered || isSelected ? 0.95f : 0.48f), 1f, false);
            DrawFrontPortrait(index, portrait);

            if (isSelected)
            {
                Rect selectedPill = new Rect(card.xMax - 116f, card.y + 22f, 101f, 25f);
                RuntimeGuiTheme.DrawPanel(selectedPill, new Color(0.08f, 0.52f, 0.23f, 0.98f),
                    new Color(0.35f, 1f, 0.5f, 1f), 1f, false);
                GUI.Label(selectedPill, "SELECIONADO  ✓", selectedStyle);
            }

            float infoY = portrait.yMax + 7f;
            GUI.Label(new Rect(card.x + 12f, infoY, card.width - 24f, 28f), stats.DisplayName.ToUpperInvariant(), cardTitleStyle);
            GUI.Label(new Rect(card.x + 12f, infoY + 27f, card.width - 24f, 27f), GetAbilitySummary(type), textStyle);

            float metricsY = infoY + 58f;
            DrawMetric(new Rect(card.x + 14f, metricsY, card.width - 28f, 22f), "VIDA", stats.MaxHealth / 100f,
                $"{stats.MaxHealth:0}", new Color(0.16f, 0.86f, 0.36f));
            DrawMetric(new Rect(card.x + 14f, metricsY + 28f, card.width - 28f, 22f), "DANO", stats.AttackDamage / 25f,
                $"{stats.AttackDamage:0}", new Color(1f, 0.52f, 0.08f));
            DrawMetric(new Rect(card.x + 14f, metricsY + 56f, card.width - 28f, 22f), "VELOCIDADE", stats.SprintSpeed / 10f,
                $"{stats.SprintSpeed:0.0}", new Color(0.08f, 0.66f, 1f));

            Rect abilityRow = new Rect(card.x + 14f, card.yMax - 48f, card.width - 28f, 34f);
            RuntimeGuiTheme.DrawRoundedRect(abilityRow, new Color(0.005f, 0.025f, 0.02f, 0.86f));
            GUI.Label(new Rect(abilityRow.x + 8f, abilityRow.y, abilityRow.width * 0.38f, abilityRow.height), "HABILIDADE", statStyle);
            GUI.Label(new Rect(abilityRow.x + abilityRow.width * 0.34f, abilityRow.y, abilityRow.width * 0.63f - 8f, abilityRow.height),
                stats.AbilityNames[0], statValueStyle);

            if (hovered && !isSelected)
            {
                GUI.Label(new Rect(card.x + 14f, card.yMax - 76f, card.width - 28f, 20f), "CLIQUE PARA SELECIONAR", selectedStyle);
            }

            if (GUI.Button(card, GUIContent.none, GUIStyle.none)) selected = type;
        }

        private void DrawFrontPortrait(int index, Rect portrait)
        {
            Texture2D texture = portraitArt[index];
            if (texture == null)
            {
                if (fallbackArt[index] != null)
                {
                    GUI.DrawTexture(new Rect(portrait.x + 5f, portrait.y + 5f, portrait.width - 10f, portrait.height - 10f),
                        fallbackArt[index], ScaleMode.ScaleToFit, true);
                }
                return;
            }

            float sourceAspect = texture.width * FrontPortraitUv.width / (texture.height * FrontPortraitUv.height);
            float availableHeight = portrait.height - 8f;
            float drawWidth = availableHeight * sourceAspect;
            float maxWidth = portrait.width - 8f;
            if (drawWidth > maxWidth)
            {
                drawWidth = maxWidth;
                availableHeight = drawWidth / sourceAspect;
            }

            Rect drawRect = new Rect(portrait.center.x - drawWidth * 0.5f, portrait.center.y - availableHeight * 0.5f,
                drawWidth, availableHeight);
            GUI.DrawTextureWithTexCoords(drawRect, texture, FrontPortraitUv, true);
        }

        private void DrawFooter(float contentX, float contentWidth, float requestedY, float viewHeight)
        {
            float contractWidth = Mathf.Min(690f, contentWidth - 80f);
            float contractY = Mathf.Min(requestedY, viewHeight - 128f);
            Rect contract = new Rect(contentX + contentWidth * 0.5f - contractWidth * 0.5f, contractY, contractWidth, 36f);
            DrawCartoonPanel(contract, new Color(0.025f, 0.07f, 0.06f, 0.96f), new Color(0.35f, 0.66f, 0.48f, 1f), 1f);
            GUI.Label(contract,
                $"CONTRATO: {ForestProgression.DailyContract}   •   MEMÓRIAS {ForestProgression.LoreCount}/12   •   CONQUISTAS {ForestProgression.AchievementCount}/2",
                footerStyle);

            Rect playButton = new Rect(contentX + contentWidth * 0.5f - 210f, contract.yMax + 10f, 420f, 58f);
            bool hovered = playButton.Contains(Event.current.mousePosition);
            DrawCartoonPanel(playButton,
                hovered ? new Color(1f, 0.63f, 0.05f, 1f) : new Color(0.94f, 0.47f, 0.025f, 1f),
                hovered ? new Color(1f, 0.94f, 0.5f, 1f) : new Color(1f, 0.72f, 0.12f, 1f),
                hovered ? 3f : 2f);
            GUI.Label(playButton, "INICIAR PARTIDA", playButtonStyle);
            if (GUI.Button(playButton, GUIContent.none, GUIStyle.none)) StartMatch();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < fallbackArt.Length; i++) AnimalPreviewRenderer.Release(fallbackArt[i]);
        }

        private void DrawMetric(Rect rect, string label, float normalized, string value, Color color)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width * 0.35f, rect.height), label, statStyle);
            GUI.Label(new Rect(rect.x + rect.width * 0.83f, rect.y, rect.width * 0.17f, rect.height), value, statValueStyle);
            Rect bar = new Rect(rect.x + rect.width * 0.37f, rect.y + 7f, rect.width * 0.43f, 8f);
            RuntimeGuiTheme.DrawRoundedRect(bar, new Color(0.005f, 0.012f, 0.012f, 0.96f));
            RuntimeGuiTheme.DrawRoundedRect(new Rect(bar.x + 1f, bar.y + 1f,
                (bar.width - 2f) * Mathf.Clamp01(normalized), bar.height - 2f), color);
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
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(0.88f, 0.93f, 0.85f) } };
            buttonStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            playButtonStyle = new GUIStyle(buttonStyle) { fontSize = 24 };
            cardTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            statStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, normal = { textColor = new Color(0.8f, 0.86f, 0.79f) } };
            statValueStyle = new GUIStyle(statStyle) { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.42f, 1f, 0.56f) } };
            footerStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, normal = { textColor = new Color(0.94f, 0.95f, 0.82f) } };
            roomStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.white } };
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

        private static string GetAbilitySummary(AnimalType type)
        {
            return type switch
            {
                AnimalType.Tiger => "PULO LONGO E RÁPIDO",
                AnimalType.Ant => "TÚNEL OU ARREMESSO",
                AnimalType.Eagle => "SALTO PLANADO",
                AnimalType.Monkey => "SEQUÊNCIA DE CIPÓS",
                _ => string.Empty
            };
        }

        private static void DrawCartoonPanel(Rect rect, Color fill, Color border, float borderSize)
        {
            RuntimeGuiTheme.DrawPanel(rect, fill, border, borderSize);
        }
    }
}
