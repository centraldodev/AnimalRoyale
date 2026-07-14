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
        private Texture2D minimapCircleTexture;
        private Texture2D minimapRingTexture;
        private readonly Texture2D[] animalPortraits = new Texture2D[4];
        private JungleGenerator jungle;
        private string resultMessage = string.Empty;

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
            DrawHUD();
        }

        private void DrawHUD()
        {
            ResolveLocalPlayer();
            if (LocalPlayer != null && LocalPlayer.Health != null)
            {
                DrawPlayerHud();
            }

            string controlHint = LocalPlayer != null && LocalPlayer.IsInAntTunnel
                ? $"NO TÚNEL: WASD escolhe saída • saída forçada: {LocalPlayer.TunnelSecondsRemaining:0.0}s"
                : LocalPlayer != null && LocalPlayer.IsSwimming
                    ? "NADANDO — Espaço dá impulso • suba pela rampa de pedra do portal ou pela margem"
                : LocalPlayer != null && LocalPlayer.IsWading
                    ? "NO LAGO — VELOCIDADE REDUZIDA • alcance a margem para correr normalmente"
                : LocalPlayer != null && LocalPlayer.AnimalType == AnimalType.Ant
                    ? "Q arremesso • E entra no túnel • F coleta/consome • Botão direito: mira"
                    : "Q poder especial • F coleta diamante/consome alimento • Botão direito: mira";
            GUI.Label(new Rect(28f, 283f, 650f, 20f), controlHint, normalStyle);

            DrawMissionHud();

            DrawPowerBar();

            DrawAimReticle();

            DrawMinimap();

            DrawObjectiveStatus();

            if (MatchFinished)
            {
                float boxHeight = 270f;
                float boxY = Screen.height * 0.5f - boxHeight * 0.5f;
                DrawCartoonPanel(new Rect(Screen.width * 0.5f - 300f, boxY, 600f, boxHeight), new Color(0.06f, 0.11f, 0.2f, 0.96f), new Color(1f, 0.73f, 0.18f, 1f));
                GUI.Label(new Rect(Screen.width * 0.5f - 280f, boxY + 20f, 560f, 58f), resultMessage, resultStyle);
                string summary = ForestMissionDirector.Instance != null ? ForestMissionDirector.Instance.MatchSummary : string.Empty;
                GUI.Label(new Rect(Screen.width * 0.5f - 270f, boxY + 82f, 540f, 104f), summary, minimapStyle);
                if (GUI.Button(new Rect(Screen.width * 0.5f - 130f, boxY + 207f, 260f, 45f), "JOGAR NOVAMENTE"))
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
            Rect panel = new Rect(16f, 142f, 410f, 132f);
            DrawCartoonPanel(panel, new Color(0.20f, 0.09f, 0.025f, 0.94f), new Color(0.94f, 0.57f, 0.18f, 1f), 4f);
            DrawCartoonPanel(new Rect(24f, 150f, 394f, 30f), new Color(0.31f, 0.14f, 0.045f, 1f), new Color(0.7f, 0.36f, 0.1f, 1f), 2f);
            GUI.Label(new Rect(32f, 154f, 378f, 22f), "MISSÕES DA FLORESTA", titleStyle);
            DrawQuestCard(new Rect(25f, 186f, 392f, 36f), director.GlobalMissionText, new Color(1f, 0.77f, 0.37f));
            DrawQuestCard(new Rect(25f, 228f, 392f, 36f), director.AnimalMissionText, new Color(0.76f, 0.9f, 0.42f));
            if (!string.IsNullOrEmpty(director.EventMessage))
            {
                Rect banner = new Rect(Screen.width * 0.5f - 320f, 18f, 640f, 38f);
                DrawCartoonPanel(banner, new Color(0.18f, 0.06f, 0.25f, 0.94f), new Color(0.92f, 0.42f, 1f, 1f));
                GUI.Label(banner, director.EventMessage, minimapStyle);
            }
        }

        private void DrawQuestCard(Rect rect, string text, Color accent)
        {
            DrawCartoonPanel(rect, new Color(0.92f, 0.72f, 0.42f, 0.98f), new Color(0.42f, 0.2f, 0.055f, 1f), 2f);
            Color oldColor = GUI.color;
            GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x + 7f, rect.y + 7f, 8f, rect.height - 14f), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUIStyle questStyle = new GUIStyle(normalStyle) { fontStyle = FontStyle.Bold };
            questStyle.normal.textColor = new Color(0.18f, 0.075f, 0.02f);
            GUI.Label(new Rect(rect.x + 24f, rect.y + 6f, rect.width - 32f, rect.height - 8f), text, questStyle);
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
            Rect panel = new Rect(16f, 16f, 430f, hasEnergy ? 116f : 104f);
            DrawCartoonPanel(panel, new Color(0.22f, 0.09f, 0.025f, 0.95f), new Color(0.95f, 0.58f, 0.17f, 1f), 4f);

            Rect portraitFrame = new Rect(27f, 26f, 84f, 84f);
            DrawCartoonPanel(portraitFrame, new Color(0.08f, 0.12f, 0.12f, 1f), new Color(1f, 0.72f, 0.22f, 1f), 4f);
            Texture2D portrait = GetAnimalPortrait(LocalPlayer.AnimalType);
            if (portrait != null) GUI.DrawTexture(new Rect(32f, 31f, 74f, 74f), portrait, ScaleMode.ScaleToFit, true);

            GUI.Label(new Rect(122f, 25f, 300f, 25f), stats.DisplayName.ToUpperInvariant(), titleStyle);
            DrawCartoonStatBar(new Rect(122f, 53f, 300f, 31f), health.CurrentHealth, health.MaxHealth,
                new Color(0.14f, 0.78f, 0.16f), "VIDA");
            if (hasEnergy)
            {
                DrawCartoonStatBar(new Rect(122f, 91f, 300f, 18f), LocalPlayer.MobilityEnergy,
                    LocalPlayer.MaxMobilityEnergyValue, new Color(1f, 0.78f, 0.14f), LocalPlayer.MobilityEnergyName);
            }
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

        private void DrawCartoonStatBar(Rect rect, float value, float maxValue, Color fillColor, string label)
        {
            float normalized = maxValue > 0f ? Mathf.Clamp01(value / maxValue) : 0f;
            DrawCartoonPanel(rect, new Color(0f, 0f, 0f, 0.7f), Color.Lerp(fillColor, Color.white, 0.28f), 2f);
            Rect fill = new Rect(rect.x + 3f, rect.y + 3f, (rect.width - 6f) * normalized, rect.height - 6f);
            Color oldColor = GUI.color;
            GUI.color = fillColor;
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(new Rect(rect.x + 9f, rect.y + 1f, rect.width - 18f, rect.height), $"{label}  {value:0}/{maxValue:0}", normalStyle);
        }

        private void DrawPowerBar()
        {
            if (LocalPlayer == null) return;
            AnimalStats stats = LocalPlayer.Stats;
            if (stats.AbilityNames == null || stats.AbilityNames.Length == 0) return;
            float width = Mathf.Min(720f, Screen.width - 32f);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 116f;
            DrawCartoonPanel(new Rect(x, y, width, 98f), new Color(0.04f, 0.1f, 0.17f, 0.9f), new Color(0.26f, 0.75f, 1f, 0.95f));
            GUI.Label(new Rect(x + 12f, y + 7f, width - 24f, 19f), $"Último poder usado: {LocalPlayer.LastPowerName}", normalStyle);
            DrawCartoonPanel(new Rect(x + 10f, y + 31f, width - 20f, 53f), new Color(0.07f, 0.16f, 0.26f, 0.95f),
                new Color(1f, 0.72f, 0.18f), 2f);
            GUI.Label(new Rect(x + 20f, y + 38f, width - 40f, 40f),
                $"Q  {stats.AbilityNames[0]}   •   Recarga: {LocalPlayer.AbilityCooldownRemainingFor(0):0.0}s", normalStyle);
        }

        private void DrawAimReticle()
        {
            bool vineTarget = LocalPlayer != null && VineAnchor.IsLookedAtBy(LocalPlayer);
            Color previousColor = GUI.color;
            GUI.color = vineTarget ? new Color(0.25f, 1f, 0.55f, 1f) : Color.white;

            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            GUI.DrawTexture(new Rect(centerX - 11f, centerY - 1.5f, 22f, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - 1.5f, centerY - 11f, 3f, 22f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - 2f, centerY - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = previousColor;

            if (vineTarget)
            {
                GUI.Label(new Rect(centerX - 105f, centerY + 20f, 210f, 22f), "CIPÓ AO ALCANCE — Q PARA AGARRAR", normalStyle);
            }
        }

        private void DrawMinimap()
        {
            if (LocalPlayer == null) return;
            if (jungle == null) jungle = FindAnyObjectByType<JungleGenerator>();
            if (jungle == null) return;

            float size = Mathf.Clamp(Screen.height * 0.235f, 190f, 250f);
            Rect panel = new Rect(Screen.width - size - 18f, 16f, size, size + 28f);
            DrawCartoonPanel(panel, new Color(0.035f, 0.09f, 0.14f, 0.94f), new Color(1f, 0.7f, 0.18f, 0.96f));
            int carriedDiamonds = DiamondObjectiveManager.Instance != null
                ? DiamondObjectiveManager.Instance.GetCount(LocalPlayer)
                : 0;
            GUI.Label(new Rect(panel.x + 8f, panel.y + 4f, panel.width - 16f, 22f),
                $"MINIMAPA  •  ◆ {carriedDiamonds}/{DiamondObjectiveManager.RequiredDiamonds}", minimapStyle);

            Rect map = new Rect(panel.x + 9f, panel.y + 29f, panel.width - 18f, panel.width - 18f);
            ForestMissionDirector missionDirector = ForestMissionDirector.Instance;
            bool minimapJammed = missionDirector != null && missionDirector.MinimapJammed;
            Color previous = GUI.color;
            GUI.color = new Color(0.08f, 0.31f, 0.13f, 1f);
            GUI.DrawTexture(map, Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.52f, 0.2f, 0.45f);
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
            float minimapSize = Mathf.Clamp(Screen.height * 0.235f, 190f, 250f);
            Rect panel = new Rect(Screen.width - minimapSize - 18f, minimapSize + 52f, minimapSize, 82f);
            SafeZoneController zone = SafeZoneController.Instance;
            bool outside = zone != null && zone.IsOutside(LocalPlayer.transform.position);
            DrawCartoonPanel(panel,
                outside ? new Color(0.34f, 0.055f, 0.025f, 0.96f) : new Color(0.22f, 0.09f, 0.025f, 0.95f),
                outside ? new Color(1f, 0.22f, 0.08f, 1f) : new Color(0.95f, 0.58f, 0.17f, 1f), 4f);

            int diamonds = DiamondObjectiveManager.Instance != null
                ? DiamondObjectiveManager.Instance.GetCount(LocalPlayer)
                : 0;
            GUI.Label(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, 24f),
                $"ANIMAIS  {AliveCount}     ◆  {diamonds}/{DiamondObjectiveManager.RequiredDiamonds}", minimapStyle);

            string zoneText = "ÁREA SEGURA INDISPONÍVEL";
            if (zone != null)
            {
                zoneText = outside
                    ? "FORA DA ÁREA — CHUVA ÁCIDA: -10/s"
                    : zone.TimeUntilShrink > 0f
                        ? $"CHUVA ÁCIDA AVANÇA EM {zone.TimeUntilShrink:0}s"
                        : $"ÁREA SEGURA  {zone.CurrentRadius:0} m";
            }
            GUI.Label(new Rect(panel.x + 10f, panel.y + 39f, panel.width - 20f, 25f), zoneText, minimapStyle);
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

        private static void DrawCartoonPanel(Rect rect, Color fill, Color border, float borderSize = 3f)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, fill.a * 0.42f);
            GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), Texture2D.whiteTexture);
            GUI.color = border;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x + borderSize, rect.y + borderSize, rect.width - borderSize * 2f, rect.height - borderSize * 2f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null && normalStyle != null && resultStyle != null && minimapStyle != null
                && minimapCircleTexture != null && minimapRingTexture != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            normalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            resultStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
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
            minimapCircleTexture = CreateMinimapCircleTexture(false);
            minimapRingTexture = CreateMinimapCircleTexture(true);
        }
    }
}
