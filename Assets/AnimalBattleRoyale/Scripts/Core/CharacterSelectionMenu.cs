using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class CharacterSelectionMenu : MonoBehaviour
    {
        private GameBootstrap bootstrap;
        private AnimalType selected;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle textStyle;
        private GUIStyle selectedStyle;
        private GUIStyle buttonStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle statStyle;
        private GUIStyle statValueStyle;
        private readonly RenderTexture[] conceptArt = new RenderTexture[AnimalRoster.Count];

        public void Initialize(GameBootstrap gameBootstrap, AnimalType initialSelection)
        {
            bootstrap = gameBootstrap;
            selected = initialSelection;
            ThirdPersonCamera.SetCursorLocked(false);
            for (int i = 0; i < AnimalRoster.Count; i++) conceptArt[i] = AnimalPreviewRenderer.Create((AnimalType)i, 256);
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                ThirdPersonCamera.SetCursorLocked(false);
            }

            int index = GameInput.ReadAnimalSelection();
            if (index >= 0 && index < AnimalRoster.Count) selected = (AnimalType)index;
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

            Color oldColor = GUI.color;
            GUI.color = new Color(0.01f, 0.024f, 0.025f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, viewWidth, viewHeight), Texture2D.whiteTexture);
            GUI.color = oldColor;

            float width = Mathf.Min(1120f, viewWidth - 48f);
            float x = (viewWidth - width) * 0.5f;
            GUI.Label(new Rect(x, 20f, width, 40f), "ANIMAL BATTLE ROYALE", titleStyle);
            GUI.Label(new Rect(x, 58f, width, 24f), "ESCOLHA SEU ANIMAL", subtitleStyle);

            const float gap = 10f;
            const int columns = 4;
            float cardY = 88f;
            float cardHeight = Mathf.Clamp((viewHeight - 220f) * 0.5f, 218f, 270f);
            float cardWidth = (width - gap * (columns - 1f)) / columns;
            for (int i = 0; i < AnimalRoster.Count; i++)
            {
                AnimalType type = (AnimalType)i;
                AnimalStats stats = AnimalDefinition.Get(type);
                int row = i / columns;
                int column = i % columns;
                float cardX = x + column * (cardWidth + gap);
                if (row == 1 && AnimalRoster.Count % columns != 0)
                {
                    int lastRowCount = AnimalRoster.Count % columns;
                    float lastRowWidth = lastRowCount * cardWidth + (lastRowCount - 1) * gap;
                    cardX = x + (width - lastRowWidth) * 0.5f + column * (cardWidth + gap);
                }
                float currentCardY = cardY + row * (cardHeight + gap);
                bool isSelected = selected == type;
                Rect cardRect = new Rect(cardX, currentCardY, cardWidth, cardHeight);
                DrawCartoonPanel(cardRect,
                    isSelected ? new Color(0.045f, 0.105f, 0.078f, 0.98f) : new Color(0.028f, 0.05f, 0.052f, 0.96f),
                    isSelected ? new Color(0.36f, 0.94f, 0.62f, 1f) : new Color(0.19f, 0.31f, 0.29f, 0.95f), isSelected ? 2f : 1f);
                RuntimeGuiTheme.DrawRoundedRect(new Rect(cardRect.x + 8f, cardRect.y + 8f, cardRect.width - 16f, 4f),
                    Color.Lerp(stats.MainColor, Color.white, 0.28f));

                Texture concept = conceptArt[i];
                float portraitHeight = Mathf.Clamp(cardHeight * 0.39f, 82f, 112f);
                Rect portraitRect = new Rect(cardX + 10f, currentCardY + 14f, cardWidth - 20f, portraitHeight);
                RuntimeGuiTheme.DrawRoundedRect(portraitRect, new Color(0.015f, 0.026f, 0.026f, 0.82f));
                if (concept != null)
                {
                    GUI.DrawTexture(new Rect(portraitRect.x + 4f, portraitRect.y + 4f, portraitRect.width - 8f, portraitRect.height - 8f), concept, ScaleMode.ScaleToFit, true);
                }

                float infoY = portraitRect.yMax + 8f;
                GUI.Label(new Rect(cardX + 12f, infoY, cardWidth - 24f, 27f), stats.DisplayName, cardTitleStyle);
                GUI.Label(new Rect(cardX + 12f, infoY + 29f, cardWidth - 24f, 32f), stats.AbilityNames[0], textStyle);
                float metricsY = infoY + 58f;
                DrawMetric(new Rect(cardX + 14f, metricsY, cardWidth - 28f, 22f), "VIDA", stats.MaxHealth / 200f, $"{stats.MaxHealth:0}", new Color(0.22f, 0.82f, 0.48f));
                DrawMetric(new Rect(cardX + 14f, metricsY + 27f, cardWidth - 28f, 22f), "DANO", stats.AttackDamage / 25f, $"{stats.AttackDamage:0}", new Color(0.98f, 0.56f, 0.2f));
                DrawMetric(new Rect(cardX + 14f, metricsY + 54f, cardWidth - 28f, 22f), "VELOCIDADE", stats.SprintSpeed / 9f, $"{stats.SprintSpeed:0.0}", new Color(0.3f, 0.72f, 0.94f));

                float masteryY = cardRect.yMax - 26f;
                GUI.Label(new Rect(cardX + 12f, masteryY, cardWidth - 24f, 20f),
                    $"MAESTRIA {ForestProgression.GetLevel(type)}  •  {ForestProgression.GetCosmeticName(type)}", selectedStyle);
                if (isSelected)
                {
                    Rect selectedPill = new Rect(cardRect.xMax - 92f, cardRect.y + 16f, 76f, 22f);
                    RuntimeGuiTheme.DrawPanel(selectedPill, new Color(0.13f, 0.46f, 0.29f, 0.96f), new Color(0.42f, 1f, 0.68f, 1f), 1f, false);
                    GUI.Label(selectedPill, "SELECIONADO", selectedStyle);
                }

                if (GUI.Button(cardRect, GUIContent.none, GUIStyle.none)) selected = type;
            }

            float footerY = cardY + cardHeight * 2f + gap + 8f;
            GUI.Label(new Rect(x, footerY, width, 22f),
                $"{ForestProgression.DailyContract}   •   MEMÓRIAS {ForestProgression.LoreCount}/12   •   CONQUISTAS {ForestProgression.AchievementCount}/2", selectedStyle);
            Rect playButton = new Rect(x + width * 0.5f - 130f, footerY + 30f, 260f, 48f);
            DrawCartoonPanel(playButton, new Color(0.16f, 0.62f, 0.38f, 1f), new Color(0.48f, 1f, 0.7f, 1f), 1f);
            GUI.Label(playButton, "INICIAR PARTIDA", buttonStyle);
            if (GUI.Button(playButton, GUIContent.none, GUIStyle.none)) StartMatch();
            GUI.matrix = previousMatrix;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < conceptArt.Length; i++) AnimalPreviewRenderer.Release(conceptArt[i]);
        }

        private void DrawMetric(Rect rect, string label, float normalized, string value, Color color)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width * 0.46f, rect.height), label, statStyle);
            GUI.Label(new Rect(rect.x + rect.width * 0.72f, rect.y, rect.width * 0.28f, rect.height), value, statValueStyle);
            Rect bar = new Rect(rect.x + rect.width * 0.38f, rect.y + 8f, rect.width * 0.32f, 6f);
            RuntimeGuiTheme.DrawRoundedRect(bar, new Color(0.005f, 0.012f, 0.012f, 0.9f));
            RuntimeGuiTheme.DrawRoundedRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(normalized), bar.height), color);
        }

        private void StartMatch()
        {
            if (bootstrap == null) return;
            bootstrap.StartMatch(selected);
            Destroy(gameObject);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            RuntimeGuiTheme.Ensure();
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.58f, 0.78f, 0.7f) } };
            selectedStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, normal = { textColor = new Color(0.72f, 0.94f, 0.82f) } };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(0.88f, 0.93f, 0.91f) } };
            buttonStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            cardTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            statStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.58f, 0.7f, 0.66f) } };
            statValueStyle = new GUIStyle(statStyle) { alignment = TextAnchor.MiddleRight, normal = { textColor = Color.white } };
        }

        private static void DrawCartoonPanel(Rect rect, Color fill, Color border, float borderSize)
        {
            RuntimeGuiTheme.DrawPanel(rect, fill, border, borderSize);
        }
    }
}
