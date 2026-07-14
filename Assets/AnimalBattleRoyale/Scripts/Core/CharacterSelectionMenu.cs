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
        private readonly Texture2D[] conceptArt = new Texture2D[4];

        public void Initialize(GameBootstrap gameBootstrap, AnimalType initialSelection)
        {
            bootstrap = gameBootstrap;
            selected = initialSelection;
            conceptArt[(int)AnimalType.Ant] = Resources.Load<Texture2D>("CharacterConcepts/AntPortrait");
            conceptArt[(int)AnimalType.Monkey] = Resources.Load<Texture2D>("CharacterConcepts/MonkeyPortrait");
            conceptArt[(int)AnimalType.Tiger] = Resources.Load<Texture2D>("CharacterConcepts/TigerPortrait");
            conceptArt[(int)AnimalType.Eagle] = Resources.Load<Texture2D>("CharacterConcepts/EaglePortrait");
        }

        private void Update()
        {
            int index = GameInput.ReadAnimalSelection();
            if (index >= 0 && index <= 3) selected = (AnimalType)index;
            if (GameInput.ConfirmPressed()) StartMatch();
        }

        private void OnGUI()
        {
            EnsureStyles();
            Color oldColor = GUI.color;
            GUI.color = new Color(0.015f, 0.05f, 0.1f, 0.45f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            float width = Mathf.Min(900f, Screen.width - 36f);
            float x = (Screen.width - width) * 0.5f;
            float y = Mathf.Max(22f, Screen.height * 0.5f - 262f);
            DrawCartoonPanel(new Rect(x, y, width, 525f), new Color(0.035f, 0.1f, 0.18f, 0.97f), new Color(1f, 0.69f, 0.16f, 1f), 4f);
            GUI.Label(new Rect(x + 20f, y + 22f, width - 40f, 42f), "ANIMAL BATTLE ROYALE", titleStyle);
            GUI.Label(new Rect(x + 20f, y + 66f, width - 40f, 28f), "ESCOLHA SEU ANIMAL", subtitleStyle);
            GUI.Label(new Rect(x + 20f, y + 91f, width - 40f, 24f), "Encontre 10 diamantes e atravesse o portal no lago. Q usa o poder; F coleta ou come.", textStyle);

            float cardWidth = (width - 58f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                AnimalType type = (AnimalType)i;
                AnimalStats stats = AnimalDefinition.Get(type);
                float cardX = x + 20f + i * (cardWidth + 6f);
                bool isSelected = selected == type;
                Rect cardRect = new Rect(cardX, y + 124f, cardWidth, 285f);
                DrawCartoonPanel(cardRect, new Color(0.07f, 0.16f, 0.26f, 0.98f),
                    isSelected ? new Color(1f, 0.72f, 0.18f, 1f) : new Color(0.22f, 0.55f, 0.76f, 0.9f), isSelected ? 5f : 3f);

                // One clear front-facing portrait per animal keeps the choice readable.
                Texture2D concept = conceptArt[i];
                if (concept != null)
                {
                    GUI.DrawTexture(new Rect(cardX + 8f, y + 132f, cardWidth - 16f, 132f), concept, ScaleMode.ScaleToFit, true);
                }

                if (GUI.Button(cardRect, GUIContent.none, GUIStyle.none)) selected = type;

                GUI.Label(new Rect(cardX + 12f, y + 266f, cardWidth - 24f, 28f), stats.DisplayName, titleStyle);
                string antTunnelHint = type == AnimalType.Ant ? "\nE  Entrar nos túneis" : string.Empty;
                GUI.Label(new Rect(cardX + 12f, y + 295f, cardWidth - 24f, 70f),
                    $"Q  {stats.AbilityNames[0]}{antTunnelHint}\nClique esquerdo: ataque-base", textStyle);
                GUI.Label(new Rect(cardX + 12f, y + 363f, cardWidth - 24f, 38f),
                    $"Vida {stats.MaxHealth:0}  •  Dano {stats.AttackDamage:0}\nMaestria Nv. {ForestProgression.GetLevel(type)} — {ForestProgression.GetCosmeticName(type)}", textStyle);
                GUI.Label(new Rect(cardX + 12f, y + 389f, cardWidth - 24f, 18f), isSelected ? "✓ SELECIONADO" : "CLIQUE PARA ESCOLHER", selectedStyle);
            }

            Rect playButton = new Rect(x + width * 0.5f - 145f, y + 443f, 290f, 54f);
            GUI.Label(new Rect(x + 20f, y + 414f, width - 40f, 24f),
                $"{ForestProgression.DailyContract}   •   MEMÓRIAS {ForestProgression.LoreCount}/12   •   CONQUISTAS {ForestProgression.AchievementCount}/2", selectedStyle);
            DrawCartoonPanel(playButton, new Color(0.15f, 0.58f, 0.28f, 1f), new Color(0.72f, 1f, 0.38f, 1f), 4f);
            GUI.Label(playButton, "INICIAR PARTIDA  •  ENTER", buttonStyle);
            if (GUI.Button(playButton, GUIContent.none, GUIStyle.none)) StartMatch();
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
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.75f, 0.24f) } };
            selectedStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.86f, 0.38f) } };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperCenter, normal = { textColor = Color.white } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private static void DrawCartoonPanel(Rect rect, Color fill, Color border, float borderSize)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 5f, rect.width, rect.height), Texture2D.whiteTexture);
            GUI.color = border;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x + borderSize, rect.y + borderSize, rect.width - borderSize * 2f, rect.height - borderSize * 2f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }
    }
}
