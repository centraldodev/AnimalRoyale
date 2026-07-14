using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalBattleRoyale
{
    public sealed class BattleRoyaleManager : MonoBehaviour
    {
        public static BattleRoyaleManager Instance { get; private set; }

        private readonly List<ThirdPersonAnimalController> fighters = new List<ThirdPersonAnimalController>();
        private GUIStyle titleStyle;
        private GUIStyle normalStyle;
        private GUIStyle resultStyle;
        private GUIStyle minimapStyle;
        private GUIStyle eyebrowStyle;
        private GUIStyle smallStyle;
        private GUIStyle rightStyle;
        private GUIStyle centeredStyle;
        private Texture2D minimapCircleTexture;
        private Texture2D minimapRingTexture;
        private readonly Texture2D[] animalPortraits = new Texture2D[4];
        private JungleGenerator jungle;
        private string resultMessage = string.Empty;
        private float uiScale = 1f;
        private float viewWidth;
        private float viewHeight;
        private Health animatedHealth;
        private float lastHealthValue;
        private float healthTrailNormalized = 1f;
        private float healthDamagePulseUntil;

        public IReadOnlyList<ThirdPersonAnimalController> Fighters => fighters;
        public ThirdPersonAnimalController LocalPlayer { get; private set; }
        public int AliveCount { get; private set; }
        public bool MatchFinished { get; private set; }

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
            if (LocalPlayer == null || LocalPlayer.Health == null) return;
            Health health = LocalPlayer.Health;
            float normalized = health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;
            if (animatedHealth != health)
            {
                animatedHealth = health;
                lastHealthValue = health.CurrentHealth;
                healthTrailNormalized = normalized;
                return;
            }

            if (health.CurrentHealth < lastHealthValue - 0.01f)
            {
                healthDamagePulseUntil = Time.time + 0.3f;
            }
            if (normalized >= healthTrailNormalized) healthTrailNormalized = normalized;
            else healthTrailNormalized = Mathf.MoveTowards(healthTrailNormalized, normalized, Time.deltaTime * 0.18f);
            lastHealthValue = health.CurrentHealth;
        }

        public void RegisterFighter(ThirdPersonAnimalController fighter)
        {
            if (fighter == null || fighters.Contains(fighter)) return;
            fighters.Add(fighter);
            fighter.Health.Died += OnFighterDied;
            if (fighter.IsLocalPlayer) LocalPlayer = fighter;
            RecalculateAlive();
        }

        private void OnFighterDied(Health defeated, ThirdPersonAnimalController attacker)
        {
            if (defeated.Owner != null)
            {
                DiamondObjectiveManager.Instance?.DropAll(defeated.Owner, defeated.Owner.transform.position);
            }
            ForestMissionDirector.Instance?.RecordElimination(attacker);
            RecalculateAlive();

            if (defeated.Owner != null && defeated.Owner.IsLocalPlayer)
            {
                MatchFinished = true;
                resultMessage = "VOCÊ FOI ELIMINADO";
                ForestMissionDirector.Instance?.FinishMatch(false);
                return;
            }

        }

        public void CompleteEscape(ThirdPersonAnimalController winner)
        {
            if (MatchFinished || winner == null) return;
            MatchFinished = true;
            ForestMissionDirector.Instance?.FinishMatch(winner.IsLocalPlayer);
            resultMessage = winner.IsLocalPlayer
                ? "VITÓRIA! VOCÊ ESCAPOU DA FLORESTA"
                : $"{winner.Stats.DisplayName.ToUpperInvariant()} ESCAPOU PELO PORTAL";
        }

        private void RecalculateAlive()
        {
            int alive = 0;
            foreach (ThirdPersonAnimalController fighter in fighters)
            {
                if (fighter != null && !fighter.Health.IsDead) alive++;
            }
            AliveCount = alive;
        }

        private void OnGUI()
        {
            EnsureStyles();
            uiScale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1.18f);
            viewWidth = Screen.width / uiScale;
            viewHeight = Screen.height / uiScale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            DrawHUD();
            GUI.matrix = previousMatrix;
        }

        private void DrawHUD()
        {
            ResolveLocalPlayer();
            if (LocalPlayer != null && LocalPlayer.Health != null)
            {
                DrawPlayerHud();
            }

            string contextHint = LocalPlayer != null && LocalPlayer.IsInAntTunnel
                ? $"NO TÚNEL: WASD escolhe saída • saída forçada: {LocalPlayer.TunnelSecondsRemaining:0.0}s"
                : LocalPlayer != null && LocalPlayer.IsSwimming
                    ? "NADANDO — Espaço dá impulso • suba pela rampa de pedra do portal ou pela margem"
                : LocalPlayer != null && LocalPlayer.IsWading
                    ? "NO LAGO — VELOCIDADE REDUZIDA • alcance a margem para correr normalmente"
                : string.Empty;
            if (!string.IsNullOrEmpty(contextHint)) DrawContextHint(contextHint);

            DrawMissionHud();

            DrawPowerBar();

            DrawAimReticle();

            DrawMinimap();

            DrawObjectiveStatus();

            if (MatchFinished)
            {
                Color previous = GUI.color;
                GUI.color = new Color(0.01f, 0.015f, 0.018f, 0.62f);
                GUI.DrawTexture(new Rect(0f, 0f, viewWidth, viewHeight), Texture2D.whiteTexture);
                GUI.color = previous;

                float boxWidth = Mathf.Min(560f, viewWidth - 40f);
                float boxHeight = 250f;
                float boxX = (viewWidth - boxWidth) * 0.5f;
                float boxY = viewHeight * 0.5f - boxHeight * 0.5f;
                DrawCartoonPanel(new Rect(boxX, boxY, boxWidth, boxHeight), new Color(0.035f, 0.055f, 0.06f, 0.98f), new Color(0.35f, 0.86f, 0.62f, 1f));
                GUI.Label(new Rect(boxX + 24f, boxY + 24f, boxWidth - 48f, 48f), resultMessage, resultStyle);
                string summary = ForestMissionDirector.Instance != null ? ForestMissionDirector.Instance.MatchSummary : string.Empty;
                GUI.Label(new Rect(boxX + 36f, boxY + 80f, boxWidth - 72f, 82f), summary, centeredStyle);
                Rect restartButton = new Rect(boxX + boxWidth * 0.5f - 120f, boxY + 183f, 240f, 44f);
                DrawCartoonPanel(restartButton, new Color(0.18f, 0.58f, 0.38f, 1f), new Color(0.48f, 1f, 0.72f, 1f), 1f);
                GUI.Label(restartButton, "JOGAR NOVAMENTE", centeredStyle);
                if (GUI.Button(restartButton, GUIContent.none, GUIStyle.none))
                {
                    RestartMatch();
                }
            }
        }

        private void ResolveLocalPlayer()
        {
            if (LocalPlayer != null) return;

            foreach (ThirdPersonAnimalController fighter in fighters)
            {
                if (fighter != null && fighter.IsLocalPlayer)
                {
                    LocalPlayer = fighter;
                    return;
                }
            }

            ThirdPersonAnimalController[] sceneFighters = FindObjectsByType<ThirdPersonAnimalController>();
            foreach (ThirdPersonAnimalController fighter in sceneFighters)
            {
                if (fighter == null || !fighter.IsLocalPlayer) continue;
                LocalPlayer = fighter;
                if (!fighters.Contains(fighter)) RegisterFighter(fighter);
                return;
            }
        }

        private void DrawMissionHud()
        {
            ForestMissionDirector director = ForestMissionDirector.Instance;
            if (LocalPlayer == null || director == null) return;
            float panelWidth = Mathf.Min(382f, viewWidth * 0.31f);
            Rect panel = new Rect(20f, 20f, panelWidth, 112f);
            DrawCartoonPanel(panel, new Color(0.025f, 0.045f, 0.048f, 0.9f), new Color(0.2f, 0.34f, 0.31f, 0.95f), 1f);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, panel.width - 32f, 18f), "OBJETIVOS", eyebrowStyle);
            DrawQuestCard(new Rect(panel.x + 16f, panel.y + 37f, panel.width - 32f, 28f), director.GlobalMissionText, new Color(0.98f, 0.74f, 0.24f));
            DrawQuestCard(new Rect(panel.x + 16f, panel.y + 72f, panel.width - 32f, 28f), director.AnimalMissionText, new Color(0.36f, 0.86f, 0.58f));
            if (!string.IsNullOrEmpty(director.EventMessage))
            {
                float bannerWidth = Mathf.Min(440f, viewWidth - panelWidth - 300f);
                bannerWidth = Mathf.Max(300f, bannerWidth);
                Rect banner = new Rect((viewWidth - bannerWidth) * 0.5f, 20f, bannerWidth, 38f);
                DrawCartoonPanel(banner, new Color(0.08f, 0.055f, 0.11f, 0.94f), new Color(0.78f, 0.46f, 0.94f, 1f), 1f);
                GUI.Label(banner, director.EventMessage, centeredStyle);
            }
        }

        private void DrawQuestCard(Rect rect, string text, Color accent)
        {
            Color oldColor = GUI.color;
            GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x, rect.y + 3f, 3f, rect.height - 6f), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(new Rect(rect.x + 12f, rect.y, rect.width - 12f, rect.height), text, smallStyle);
        }

        private static void RestartMatch()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        private void DrawPlayerHud()
        {
            AnimalStats stats = LocalPlayer.Stats;
            Health health = LocalPlayer.Health;
            if (health == null || string.IsNullOrEmpty(stats.DisplayName)) return;
            bool hasEnergy = LocalPlayer.UsesMobilityEnergy;
            float panelHeight = hasEnergy ? 110f : 94f;
            Rect panel = new Rect(20f, viewHeight - panelHeight - 20f, 390f, panelHeight);
            float healthNormalized = health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;
            Color healthColor = healthNormalized <= 0.25f
                ? new Color(1f, 0.24f, 0.2f)
                : healthNormalized <= 0.55f
                    ? new Color(1f, 0.68f, 0.18f)
                    : new Color(0.2f, 0.82f, 0.48f);
            Color panelBorder = Time.time < healthDamagePulseUntil ? new Color(1f, 0.3f, 0.24f) : new Color(0.2f, 0.34f, 0.31f);
            DrawCartoonPanel(panel, new Color(0.022f, 0.04f, 0.042f, 0.94f), panelBorder, 1f);

            Rect portraitFrame = new Rect(panel.x + 10f, panel.y + 10f, 74f, 74f);
            DrawCartoonPanel(portraitFrame, new Color(0.055f, 0.075f, 0.072f, 1f), Color.Lerp(stats.MainColor, Color.white, 0.28f), 1f);
            Texture2D portrait = GetAnimalPortrait(LocalPlayer.AnimalType);
            if (portrait != null) GUI.DrawTexture(new Rect(portraitFrame.x + 4f, portraitFrame.y + 4f, 66f, 66f), portrait, ScaleMode.ScaleToFit, true);

            float contentX = panel.x + 96f;
            float contentWidth = panel.width - 108f;
            GUI.Label(new Rect(contentX, panel.y + 10f, contentWidth, 20f), stats.DisplayName.ToUpperInvariant(), titleStyle);
            DrawStatBar(new Rect(contentX, panel.y + 36f, contentWidth, 28f), healthNormalized, healthTrailNormalized,
                healthColor, "VIDA", $"{health.CurrentHealth:0} / {health.MaxHealth:0}");
            if (hasEnergy)
            {
                float energyNormalized = LocalPlayer.MaxMobilityEnergyValue > 0f
                    ? Mathf.Clamp01(LocalPlayer.MobilityEnergy / LocalPlayer.MaxMobilityEnergyValue)
                    : 0f;
                DrawStatBar(new Rect(contentX, panel.y + 72f, contentWidth, 18f), energyNormalized, energyNormalized,
                    new Color(0.28f, 0.76f, 0.94f), LocalPlayer.MobilityEnergyName.ToUpperInvariant(), $"{LocalPlayer.MobilityEnergy:0}");
            }

            if (healthNormalized <= 0.25f) DrawLowHealthVignette();
        }

        private Texture2D GetAnimalPortrait(AnimalType type)
        {
            int index = (int)type;
            if (animalPortraits[index] != null) return animalPortraits[index];
            string assetName = type switch
            {
                AnimalType.Ant => "AntPortrait",
                AnimalType.Monkey => "MonkeyPortrait",
                AnimalType.Tiger => "TigerPortrait",
                AnimalType.Eagle => "EaglePortrait",
                _ => string.Empty
            };
            animalPortraits[index] = Resources.Load<Texture2D>("CharacterConcepts/" + assetName);
            return animalPortraits[index];
        }

        private void DrawStatBar(Rect rect, float normalized, float trailNormalized, Color fillColor, string label, string value)
        {
            DrawRoundedRect(rect, new Color(0.005f, 0.012f, 0.012f, 0.88f));
            Rect inner = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            float trailWidth = inner.width * Mathf.Clamp01(trailNormalized);
            if (trailWidth > 1f) DrawRoundedRect(new Rect(inner.x, inner.y, trailWidth, inner.height), new Color(0.92f, 0.35f, 0.2f, 0.52f));
            float fillWidth = inner.width * Mathf.Clamp01(normalized);
            if (fillWidth > 1f) DrawRoundedRect(new Rect(inner.x, inner.y, fillWidth, inner.height), fillColor);
            GUI.Label(new Rect(rect.x + 9f, rect.y, rect.width * 0.62f, rect.height), label, smallStyle);
            GUI.Label(new Rect(rect.x + rect.width * 0.52f, rect.y, rect.width * 0.45f - 8f, rect.height), value, rightStyle);
        }

        private void DrawPowerBar()
        {
            if (LocalPlayer == null) return;
            AnimalStats stats = LocalPlayer.Stats;
            if (stats.AbilityNames == null || stats.AbilityNames.Length == 0) return;
            float width = 350f;
            float x = (viewWidth - width) * 0.5f;
            float y = viewHeight - 88f;
            float remaining = LocalPlayer.AbilityCooldownRemainingFor(0);
            float cooldown = stats.AbilityCooldowns != null && stats.AbilityCooldowns.Length > 0 ? Mathf.Max(0.01f, stats.AbilityCooldowns[0]) : 1f;
            float readiness = 1f - Mathf.Clamp01(remaining / cooldown);
            bool ready = remaining <= 0.01f;
            Color accent = ready ? new Color(0.3f, 0.92f, 0.58f) : new Color(0.96f, 0.68f, 0.2f);

            Rect panel = new Rect(x, y, width, 68f);
            DrawCartoonPanel(panel, new Color(0.025f, 0.045f, 0.048f, 0.92f), new Color(0.18f, 0.31f, 0.29f, 1f), 1f);
            Rect key = new Rect(panel.x + 10f, panel.y + 10f, 46f, 46f);
            DrawCartoonPanel(key, ready ? new Color(0.12f, 0.38f, 0.25f, 1f) : new Color(0.16f, 0.17f, 0.16f, 1f), accent, 1f);
            GUI.Label(key, "Q", centeredStyle);
            GUI.Label(new Rect(panel.x + 68f, panel.y + 9f, panel.width - 150f, 23f), stats.AbilityNames[0], normalStyle);
            GUI.Label(new Rect(panel.x + panel.width - 82f, panel.y + 9f, 70f, 23f), ready ? "PRONTO" : $"{remaining:0.0}s", rightStyle);
            Rect cooldownBar = new Rect(panel.x + 68f, panel.y + 42f, panel.width - 80f, 8f);
            DrawRoundedRect(cooldownBar, new Color(0.005f, 0.012f, 0.012f, 0.9f));
            if (readiness > 0.01f) DrawRoundedRect(new Rect(cooldownBar.x, cooldownBar.y, cooldownBar.width * readiness, cooldownBar.height), accent);
            if (!string.IsNullOrEmpty(LocalPlayer.LastPowerName))
            {
                GUI.Label(new Rect(panel.x + 68f, panel.y + 50f, panel.width - 80f, 14f), LocalPlayer.LastPowerName, eyebrowStyle);
            }
        }

        private void DrawAimReticle()
        {
            bool vineTarget = LocalPlayer != null && VineAnchor.IsLookedAtBy(LocalPlayer);
            Color previousColor = GUI.color;
            GUI.color = vineTarget ? new Color(0.3f, 1f, 0.58f, 1f) : new Color(1f, 1f, 1f, 0.88f);

            float centerX = viewWidth * 0.5f;
            float centerY = viewHeight * 0.5f;
            GUI.DrawTexture(new Rect(centerX - 14f, centerY - 1f, 8f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX + 6f, centerY - 1f, 8f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - 1f, centerY - 14f, 2f, 8f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - 1f, centerY + 6f, 2f, 8f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - 1.5f, centerY - 1.5f, 3f, 3f), Texture2D.whiteTexture);
            GUI.color = previousColor;

            if (vineTarget)
            {
                Rect prompt = new Rect(centerX - 122f, centerY + 24f, 244f, 34f);
                DrawCartoonPanel(prompt, new Color(0.025f, 0.075f, 0.055f, 0.92f), new Color(0.3f, 0.9f, 0.58f, 1f), 1f);
                GUI.Label(prompt, "Q  AGARRAR CIPÓ", centeredStyle);
            }
        }

        private void DrawMinimap()
        {
            if (LocalPlayer == null) return;
            if (jungle == null) jungle = FindAnyObjectByType<JungleGenerator>();
            if (jungle == null) return;

            float size = Mathf.Clamp(viewHeight * 0.22f, 176f, 218f);
            Rect panel = new Rect(viewWidth - size - 20f, 20f, size, size + 36f);
            DrawCartoonPanel(panel, new Color(0.022f, 0.042f, 0.044f, 0.94f), new Color(0.18f, 0.32f, 0.29f, 0.98f), 1f);
            int carriedDiamonds = DiamondObjectiveManager.Instance != null
                ? DiamondObjectiveManager.Instance.GetCount(LocalPlayer)
                : 0;
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 80f, 20f), "MAPA", eyebrowStyle);
            GUI.Label(new Rect(panel.x + panel.width - 82f, panel.y + 8f, 70f, 20f),
                $"◆ {carriedDiamonds}/{DiamondObjectiveManager.RequiredDiamonds}", rightStyle);

            Rect map = new Rect(panel.x + 8f, panel.y + 34f, panel.width - 16f, panel.width - 16f);
            ForestMissionDirector missionDirector = ForestMissionDirector.Instance;
            bool minimapJammed = missionDirector != null && missionDirector.MinimapJammed;
            Color previous = GUI.color;
            GUI.color = new Color(0.055f, 0.23f, 0.12f, 1f);
            GUI.DrawTexture(map, Texture2D.whiteTexture);
            GUI.color = new Color(0.28f, 0.54f, 0.24f, 0.24f);
            GUI.DrawTexture(new Rect(map.center.x - 1f, map.y, 2f, map.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(map.x, map.center.y - 1f, map.width, 2f), Texture2D.whiteTexture);

            CentralLake lake = CentralLake.Instance;
            if (lake != null)
            {
                Vector2 center = WorldToMinimap(lake.transform.position, map, jungle.MapSize);
                float diameter = lake.Radius * 2f / jungle.MapSize * map.width;
                GUI.color = new Color(0.08f, 0.66f, 0.95f, 0.9f);
                GUI.DrawTexture(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), minimapCircleTexture);
            }

            foreach (DiamondPickup diamond in DiamondPickup.ActivePickups)
            {
                if (diamond == null || !diamond.IsAvailable) continue;
                float localDistance = Vector3.Distance(LocalPlayer.transform.position, diamond.transform.position);
                if (minimapJammed || (localDistance > 36f && (missionDirector == null || !missionDirector.RevealDiamonds))) continue;
                Vector2 point = WorldToMinimap(diamond.transform.position, map, jungle.MapSize);
                GUI.color = new Color(0.22f, 0.9f, 1f, 1f);
                GUI.DrawTexture(new Rect(point.x - 3f, point.y - 3f, 6f, 6f), minimapCircleTexture);
            }

            if (DiamondObjectiveManager.Instance != null)
            {
                Vector2 portalPoint = WorldToMinimap(DiamondObjectiveManager.Instance.PortalPosition, map, jungle.MapSize);
                GUI.color = new Color(0.75f, 0.25f, 1f, 1f);
                GUI.DrawTexture(new Rect(portalPoint.x - 6f, portalPoint.y - 6f, 12f, 12f), minimapRingTexture);
            }

            if (!minimapJammed)
            {
                foreach (MissionNode node in MissionNode.ActiveNodes)
                {
                    if (node == null || node.IsActivated) continue;
                    Vector2 point = WorldToMinimap(node.transform.position, map, jungle.MapSize);
                    GUI.color = node.Kind == MissionNodeKind.Lore
                        ? new Color(0.9f, 0.35f, 1f, 1f)
                        : new Color(1f, 0.76f, 0.12f, 1f);
                    GUI.DrawTexture(new Rect(point.x - 2.5f, point.y - 2.5f, 5f, 5f), Texture2D.whiteTexture);
                }
            }

            SafeZoneController zone = SafeZoneController.Instance;
            if (zone != null)
            {
                Vector2 center = WorldToMinimap(zone.Center, map, jungle.MapSize);
                float diameter = zone.CurrentRadius * 2f / jungle.MapSize * map.width;
                GUI.color = new Color(0.55f, 1f, 0.16f, 0.95f);
                GUI.DrawTexture(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), minimapRingTexture);
            }

            foreach (ThirdPersonAnimalController fighter in fighters)
            {
                if (fighter == null || fighter.Health == null || fighter.Health.IsDead || fighter.IsBurrowed) continue;
                Vector2 point = WorldToMinimap(fighter.transform.position, map, jungle.MapSize);
                if (!map.Contains(point)) continue;
                if (fighter == LocalPlayer)
                {
                    Matrix4x4 oldMatrix = GUI.matrix;
                    GUIUtility.RotateAroundPivot(fighter.transform.eulerAngles.y, point);
                    GUI.color = new Color(1f, 0.9f, 0.18f, 1f);
                    GUI.DrawTexture(new Rect(point.x - 3.5f, point.y - 9f, 7f, 18f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(point.x - 6f, point.y - 9f, 12f, 5f), Texture2D.whiteTexture);
                    GUI.matrix = oldMatrix;
                }
                else
                {
                    float distance = Vector3.Distance(LocalPlayer.transform.position, fighter.transform.position);
                    bool tracked = missionDirector != null && missionDirector.IsCarrierRevealed(fighter);
                    if (minimapJammed || (distance > 42f && !tracked)) continue;
                    GUI.color = new Color(1f, 0.22f, 0.12f, 1f);
                    GUI.DrawTexture(new Rect(point.x - 3.5f, point.y - 3.5f, 7f, 7f), minimapCircleTexture);
                }
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(map.center.x - 12f, map.y + 2f, 24f, 18f), "N", minimapStyle);
            GUI.color = previous;
        }

        private void DrawObjectiveStatus()
        {
            if (LocalPlayer == null) return;
            float minimapSize = Mathf.Clamp(viewHeight * 0.22f, 176f, 218f);
            Rect panel = new Rect(viewWidth - minimapSize - 20f, minimapSize + 64f, minimapSize, 70f);
            SafeZoneController zone = SafeZoneController.Instance;
            bool outside = zone != null && zone.IsOutside(LocalPlayer.transform.position);
            DrawCartoonPanel(panel,
                outside ? new Color(0.24f, 0.035f, 0.03f, 0.96f) : new Color(0.022f, 0.042f, 0.044f, 0.94f),
                outside ? new Color(1f, 0.24f, 0.18f, 1f) : new Color(0.18f, 0.32f, 0.29f, 1f), 1f);

            int diamonds = DiamondObjectiveManager.Instance != null
                ? DiamondObjectiveManager.Instance.GetCount(LocalPlayer)
                : 0;
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width * 0.55f, 20f), $"ANIMAIS  {AliveCount}", smallStyle);
            GUI.Label(new Rect(panel.x + panel.width * 0.5f, panel.y + 8f, panel.width * 0.5f - 12f, 20f),
                $"◆  {diamonds}/{DiamondObjectiveManager.RequiredDiamonds}", rightStyle);

            string zoneText = "ÁREA SEGURA INDISPONÍVEL";
            if (zone != null)
            {
                zoneText = outside
                    ? "FORA DA ÁREA — CHUVA ÁCIDA: -10/s"
                    : zone.TimeUntilShrink > 0f
                        ? $"CHUVA ÁCIDA AVANÇA EM {zone.TimeUntilShrink:0}s"
                        : $"ÁREA SEGURA  {zone.CurrentRadius:0} m";
            }
            GUI.Label(new Rect(panel.x + 12f, panel.y + 35f, panel.width - 24f, 24f), zoneText, centeredStyle);
        }

        private void DrawContextHint(string text)
        {
            float width = Mathf.Min(520f, viewWidth - 48f);
            Rect panel = new Rect((viewWidth - width) * 0.5f, viewHeight * 0.5f + 72f, width, 36f);
            DrawCartoonPanel(panel, new Color(0.025f, 0.045f, 0.048f, 0.92f), new Color(0.32f, 0.72f, 0.62f, 0.95f), 1f);
            GUI.Label(panel, text, centeredStyle);
        }

        private void DrawLowHealthVignette()
        {
            float pulse = 0.045f + (Mathf.Sin(Time.time * 5.5f) * 0.5f + 0.5f) * 0.035f;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.04f, 0.02f, pulse);
            GUI.DrawTexture(new Rect(0f, 0f, viewWidth, 14f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, viewHeight - 14f, viewWidth, 14f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, 0f, 14f, viewHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(viewWidth - 14f, 0f, 14f, viewHeight), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static Vector2 WorldToMinimap(Vector3 worldPosition, Rect map, float worldSize)
        {
            float normalizedX = Mathf.Clamp(worldPosition.x / worldSize + 0.5f, 0f, 1f);
            float normalizedZ = Mathf.Clamp(worldPosition.z / worldSize + 0.5f, 0f, 1f);
            return new Vector2(map.x + normalizedX * map.width, map.y + (1f - normalizedZ) * map.height);
        }

        private static Texture2D CreateMinimapCircleTexture(bool ring)
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = ring ? "MinimapRing" : "MinimapCircle",
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
                    float normalized = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = ring
                        ? Mathf.Clamp01(1f - Mathf.Abs(normalized - 0.92f) * 24f)
                        : Mathf.Clamp01((1f - normalized) * 12f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void DrawRoundedRect(Rect rect, Color color)
        {
            RuntimeGuiTheme.DrawRoundedRect(rect, color);
        }

        private void DrawCartoonPanel(Rect rect, Color fill, Color border, float borderSize = 1f)
        {
            RuntimeGuiTheme.DrawPanel(rect, fill, border, borderSize);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null && normalStyle != null && resultStyle != null && minimapStyle != null
                && eyebrowStyle != null && smallStyle != null && rightStyle != null && centeredStyle != null
                && minimapCircleTexture != null && minimapRingTexture != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            normalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            resultStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            minimapStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            eyebrowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.55f, 0.7f, 0.65f) }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.92f, 0.96f, 0.94f) }
            };
            rightStyle = new GUIStyle(smallStyle)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white }
            };
            centeredStyle = new GUIStyle(normalStyle)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            minimapCircleTexture = CreateMinimapCircleTexture(false);
            minimapRingTexture = CreateMinimapCircleTexture(true);
            RuntimeGuiTheme.Ensure();
        }
    }
}
