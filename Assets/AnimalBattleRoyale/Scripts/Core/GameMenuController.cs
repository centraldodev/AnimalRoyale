using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalBattleRoyale
{
    [DefaultExecutionOrder(1000)]
    public sealed class GameMenuController : MonoBehaviour
    {
        private enum MenuPage { Closed, Pause, Settings }

        public static GameMenuController Instance { get; private set; }

        private MenuPage page;
        private bool inGame;
        private bool settingsOpenedFromGame;
        private GameInputAction? waitingForBinding;
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
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            ThirdPersonCamera.SetCursorLocked(!paused);
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
                else DrawSettings(width, height);
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
            float panelHeight = 430f;
            Rect panel = new Rect((viewWidth - panelWidth) * 0.5f, (viewHeight - panelHeight) * 0.5f, panelWidth, panelHeight);
            RuntimeGuiTheme.DrawPanel(panel, new Color(0.028f, 0.052f, 0.054f, 0.99f),
                new Color(0.35f, 0.9f, 0.62f, 1f), 2f);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 24f, panel.width - 48f, 42f), "JOGO PAUSADO", titleStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 67f, panel.width - 60f, 28f),
                "ESC também fecha este menu", subtitleStyle);

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
            CaptureBindingEvent(Event.current);

            float panelWidth = Mathf.Min(850f, viewWidth - 36f);
            float panelHeight = Mathf.Min(650f, viewHeight - 30f);
            Rect panel = new Rect((viewWidth - panelWidth) * 0.5f, (viewHeight - panelHeight) * 0.5f, panelWidth, panelHeight);
            RuntimeGuiTheme.DrawPanel(panel, new Color(0.025f, 0.047f, 0.05f, 0.995f),
                new Color(0.32f, 0.82f, 0.6f, 1f), 2f);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 18f, panel.width - 56f, 38f), "CONFIGURAÇÕES", titleStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 55f, panel.width - 60f, 28f),
                waitingForBinding.HasValue
                    ? "Pressione uma tecla ou botão do mouse • ESC cancela"
                    : "Clique em um atalho para trocar sua tecla", subtitleStyle);

            const int columns = 2;
            const float gap = 16f;
            float contentX = panel.x + 30f;
            float contentY = panel.y + 98f;
            float columnWidth = (panel.width - 60f - gap) / columns;
            float rowHeight = 66f;
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

            if (Time.unscaledTime < bindingMessageUntil)
            {
                GUI.Label(new Rect(panel.x + 30f, panel.yMax - 95f, panel.width - 60f, 24f), bindingMessage, subtitleStyle);
            }

            float footerY = panel.yMax - 58f;
            if (DrawMenuButton(new Rect(panel.x + 30f, footerY, 230f, 40f), "RESTAURAR PADRÕES",
                    new Color(0.34f, 0.25f, 0.12f)))
            {
                GameInputBindings.RestoreDefaults();
                bindingMessage = "ATALHOS PADRÃO RESTAURADOS";
                bindingMessageUntil = Time.unscaledTime + 2f;
            }
            if (DrawMenuButton(new Rect(panel.xMax - 190f, footerY, 160f, 40f), "VOLTAR",
                    new Color(0.14f, 0.47f, 0.32f))) CloseSettings();
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
