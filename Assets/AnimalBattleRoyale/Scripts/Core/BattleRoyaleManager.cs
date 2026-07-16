using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalBattleRoyale
{
    public sealed class BattleRoyaleManager : MonoBehaviour
    {
        private const int MinimapArrowDirectionCount = 36;

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
        private Texture2D[] minimapArrowTextures;
        private readonly RenderTexture[] animalPortraits = new RenderTexture[AnimalRoster.Count];
        private JungleGenerator jungle;
        private string resultMessage = string.Empty;
        private float uiScale = 1f;
        private float viewWidth;
        private float viewHeight;
        private Health animatedHealth;
        private float lastHealthValue;
        private float healthTrailNormalized = 1f;
        private float healthDamagePulseUntil;
        private bool openingPhaseStarted;
        private float combatUnlockTime;
        private bool matchFinished;
        private bool shuttingDown;
        private float respawnBannerUntil;
        private int respawnBannerLives;

        public IReadOnlyList<ThirdPersonAnimalController> Fighters => fighters;
        public ThirdPersonAnimalController LocalPlayer { get; private set; }
        public int AliveCount { get; private set; }
        public bool MatchFinished
        {
            get => matchFinished;
            private set
            {
                if (matchFinished == value) return;
                matchFinished = value;
                if (matchFinished) ThirdPersonCamera.SetCursorLocked(false);
            }
        }
        public bool CombatEnabled => !MatchFinished
                                     && (!openingPhaseStarted || Time.time >= combatUnlockTime);
        public float OpeningSecondsRemaining => openingPhaseStarted
            ? Mathf.Max(0f, combatUnlockTime - Time.time)
            : 0f;

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
            if (fighters.Count >= DiamondObjectiveManager.MaxPlayers)
            {
                Debug.LogWarning($"Limite de {DiamondObjectiveManager.MaxPlayers} participantes atingido; {fighter.name} nao foi registrado.");
                return;
            }
            fighters.Add(fighter);
            fighter.Health.Died += OnFighterDied;
            if (fighter.IsLocalPlayer) LocalPlayer = fighter;
            DiamondObjectiveManager.Instance?.RegisterFighter(fighter);
            RecalculateAlive();
        }

        public void BeginOpeningPhase(float duration)
        {
            openingPhaseStarted = true;
            combatUnlockTime = Time.time + Mathf.Max(0f, duration);
        }

        private void OnFighterDied(Health defeated, ThirdPersonAnimalController attacker)
        {
            ThirdPersonAnimalController owner = defeated.Owner;
            if (owner == null) { RecalculateAlive(); return; }

            Vector3 deathPosition = GetGroundedDeathPosition(owner.transform.position);
            AttackVfx.CreateBurst(deathPosition + Vector3.up, new Color(0.92f, 0.08f, 0.045f), 1.6f);
            ForestMissionDirector.Instance?.RecordElimination(attacker);

            // 3-lives battle royale: a death spends a life and respawns, unless it was the last one.
            if (owner.ConsumeLife())
            {
                owner.Respawn(FindRespawnPosition(owner));
                if (owner.IsLocalPlayer)
                {
                    respawnBannerUntil = Time.time + 2.6f;
                    respawnBannerLives = owner.LivesRemaining;
                }
                RecalculateAlive();
                return;
            }

            if (owner.IsLocalPlayer && !MatchFinished)
            {
                MatchFinished = true;
                resultMessage = "VOCÊ FOI ELIMINADO";
                ForestMissionDirector.Instance?.FinishMatch(false);
            }
            owner.SetDefeated();
            RecalculateAlive();
            CheckWinCondition();
        }

        private Vector3 FindRespawnPosition(ThirdPersonAnimalController respawning)
        {
            if (jungle == null) jungle = FindAnyObjectByType<JungleGenerator>();
            SafeZoneController safeZone = SafeZoneController.Instance;
            if (jungle == null)
            {
                Vector3 fallback = safeZone != null
                    ? safeZone.GetRandomRespawnPoint()
                    : respawning.transform.position;
                return fallback + Vector3.up * 2f;
            }

            Vector3 best = GetRespawnCandidate(safeZone);
            float bestClearance = -1f;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector3 candidate = GetRespawnCandidate(safeZone);
                float minDistance = float.MaxValue;
                foreach (ThirdPersonAnimalController fighter in fighters)
                {
                    if (fighter == null || fighter == respawning || fighter.IsEliminated
                        || fighter.Health == null || fighter.Health.IsDead) continue;
                    Vector3 flat = candidate - fighter.transform.position;
                    flat.y = 0f;
                    minDistance = Mathf.Min(minDistance, flat.magnitude);
                }
                if (minDistance > bestClearance)
                {
                    bestClearance = minDistance;
                    best = candidate;
                }
            }
            best = jungle.GetGroundPosition(best);
            return safeZone != null ? safeZone.ClampRespawnPoint(best) : best;
        }

        private Vector3 GetRespawnCandidate(SafeZoneController safeZone)
        {
            Vector3 candidate = safeZone != null
                ? safeZone.GetRandomRespawnPoint()
                : jungle.GetMissionSpawnPosition();
            candidate = jungle.GetGroundPosition(candidate);
            return safeZone != null ? safeZone.ClampRespawnPoint(candidate) : candidate;
        }

        private void CheckWinCondition()
        {
            if (MatchFinished || !openingPhaseStarted) return;
            ThirdPersonAnimalController lastStanding = null;
            int remaining = 0;
            foreach (ThirdPersonAnimalController fighter in fighters)
            {
                if (fighter == null || fighter.IsEliminated) continue;
                remaining++;
                lastStanding = fighter;
            }
            if (remaining > 1 || lastStanding == null) return;

            MatchFinished = true;
            bool localWon = lastStanding.IsLocalPlayer;
            ForestMissionDirector.Instance?.FinishMatch(localWon);
            resultMessage = localWon
                ? "VITÓRIA! ÚLTIMO SOBREVIVENTE"
                : $"{lastStanding.Stats.DisplayName.ToUpperInvariant()} VENCEU A PARTIDA";
        }

        private void DrawRespawnBanner()
        {
            GUIStyle style = centeredStyle ?? resultStyle ?? GUI.skin.label;
            Color previous = GUI.color;
            GUI.color = new Color(0.45f, 0.88f, 1f, 0.95f);
            GUI.Label(new Rect(0f, viewHeight * 0.34f, viewWidth, 44f),
                $"RESPAWN!   {respawnBannerLives} VIDA(S) RESTANTE(S)", style);
            GUI.color = previous;
        }

        public void HandleFighterDisconnected(ThirdPersonAnimalController fighter)
        {
            if (fighter == null || shuttingDown || !gameObject.scene.isLoaded || !fighters.Remove(fighter)) return;

            Health fighterHealth = fighter.Health;
            if (fighterHealth != null) fighterHealth.Died -= OnFighterDied;
            if (fighterHealth != null && fighterHealth.IsDead)
            {
                RecalculateAlive();
                CheckWinCondition();
                return;
            }

            Vector3 deathPosition = GetGroundedDeathPosition(fighter.transform.position);
            AttackVfx.CreateBurst(deathPosition + Vector3.up, new Color(0.92f, 0.08f, 0.045f), 1.9f);
            RecalculateAlive();
            CheckWinCondition();

            if (!fighter.IsLocalPlayer || MatchFinished) return;
            MatchFinished = true;
            resultMessage = "VOCE FOI ELIMINADO";
            ForestMissionDirector.Instance?.FinishMatch(false);
        }

        private Vector3 GetGroundedDeathPosition(Vector3 position)
        {
            if (jungle == null) jungle = FindAnyObjectByType<JungleGenerator>();
            return jungle != null ? jungle.GetGroundPosition(position) : position;
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

            if (OpeningSecondsRemaining > 0f) DrawOpeningCountdown();

            string contextHint = LocalPlayer != null && LocalPlayer.IsVineLeaping
                ? $"SALTANDO PARA O CIPÓ {LocalPlayer.VinesVisitedInChain}/{ThirdPersonAnimalController.MaxVinesPerChain}"
                : LocalPlayer != null && LocalPlayer.IsHangingVine
                    ? LocalPlayer.CanChainToAnotherVine
                        ? $"CIPÓ {LocalPlayer.VinesVisitedInChain}/{ThirdPersonAnimalController.MaxVinesPerChain} — mire no próximo e pressione Q • Espaço para soltar"
                        : $"LIMITE {ThirdPersonAnimalController.MaxVinesPerChain}/{ThirdPersonAnimalController.MaxVinesPerChain} — Espaço para saltar do cipó"
                : LocalPlayer != null && LocalPlayer.IsFlying
                    ? $"SALTO PLANADO {LocalPlayer.GlideSecondsRemaining:0.0}s — segure o MOUSE ESQUERDO para atirar"
                : LocalPlayer != null && LocalPlayer.IsInAntTunnel
                ? $"NO TÚNEL: WASD escolhe saída • saída forçada: {LocalPlayer.TunnelSecondsRemaining:0.0}s"
                : LocalPlayer != null && LocalPlayer.IsSwimming
                    ? "NADANDO — Espaço dá impulso • suba pela margem"
                : LocalPlayer != null && LocalPlayer.IsWading
                    ? "NO LAGO — VELOCIDADE REDUZIDA • alcance a margem para correr normalmente"
                : string.Empty;
            if (!string.IsNullOrEmpty(contextHint)) DrawContextHint(contextHint);

            DrawPowerBar();

            DrawCombatModeHud();

            DrawAimReticle();

            DrawMinimap();

            DrawObjectiveStatus();

            if (Time.time < respawnBannerUntil) DrawRespawnBanner();

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
                float minimapSize = Mathf.Clamp(viewHeight * 0.22f, 176f, 218f);
                float minimapLeft = viewWidth - minimapSize - 20f;
                float availableWidth = minimapLeft - panel.xMax - 24f;
                float bannerWidth = Mathf.Clamp(availableWidth, 280f, 440f);
                float bannerX = Mathf.Clamp((viewWidth - bannerWidth) * 0.5f, panel.xMax + 12f, minimapLeft - bannerWidth - 12f);
                Rect banner = new Rect(bannerX, 20f, bannerWidth, 38f);
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
            if (Instance != null) Instance.shuttingDown = true;
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        private void OnApplicationQuit()
        {
            shuttingDown = true;
        }

        private void OnDestroy()
        {
            shuttingDown = true;
            for (int i = 0; i < animalPortraits.Length; i++) AnimalPreviewRenderer.Release(animalPortraits[i]);
            if (Instance == this) Instance = null;
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
            Texture portrait = GetAnimalPortrait(LocalPlayer.AnimalType);
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
                string energyValue = LocalPlayer.IsMobilityRecharging
                    ? $"{LocalPlayer.MobilityEnergy:0}  {LocalPlayer.MobilityRechargeSecondsRemaining:0.0}s"
                    : $"{LocalPlayer.MobilityEnergy:0}";
                DrawStatBar(new Rect(contentX, panel.y + 72f, contentWidth, 18f), energyNormalized, energyNormalized,
                    new Color(0.28f, 0.76f, 0.94f), LocalPlayer.MobilityEnergyName.ToUpperInvariant(), energyValue);
            }

            if (healthNormalized <= 0.25f) DrawLowHealthVignette();
        }

        private Texture GetAnimalPortrait(AnimalType type)
        {
            int index = (int)type;
            if (animalPortraits[index] != null) return animalPortraits[index];
            animalPortraits[index] = AnimalPreviewRenderer.Create(type, 192);
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
            if (x < 430f) x = Mathf.Min(430f, viewWidth - width - 20f);
            float y = viewHeight - 88f;
            float remaining = LocalPlayer.AbilityCooldownRemainingFor(0);
            float cooldown = stats.AbilityCooldowns != null && stats.AbilityCooldowns.Length > 0 ? Mathf.Max(0.01f, stats.AbilityCooldowns[0]) : 1f;
            float readiness = 1f - Mathf.Clamp01(remaining / cooldown);
            bool ready = remaining <= 0.01f;
            Color accent = ready ? new Color(0.3f, 0.92f, 0.58f) : new Color(0.96f, 0.68f, 0.2f);

            Rect panel = new Rect(x, y, width, 68f);
            DrawCartoonPanel(panel, new Color(0.025f, 0.045f, 0.048f, 0.92f), new Color(0.18f, 0.31f, 0.29f, 1f), 1f);
            Rect key = new Rect(panel.x + 10f, panel.y + 10f, 46f, 46f);
            DrawKeycapIcon(key, "Q", accent, ready);
            GUI.Label(new Rect(panel.x + 68f, panel.y + 9f, panel.width - 150f, 23f), "Q - HABILIDADE 1", normalStyle);
            GUI.Label(new Rect(panel.x + panel.width - 82f, panel.y + 9f, 70f, 23f), ready ? "PRONTO" : $"{remaining:0.0}s", rightStyle);
            Rect cooldownBar = new Rect(panel.x + 68f, panel.y + 42f, panel.width - 80f, 8f);
            DrawRoundedRect(cooldownBar, new Color(0.005f, 0.012f, 0.012f, 0.9f));
            if (readiness > 0.01f) DrawRoundedRect(new Rect(cooldownBar.x, cooldownBar.y, cooldownBar.width * readiness, cooldownBar.height), accent);
            if (!string.IsNullOrEmpty(LocalPlayer.LastPowerName))
            {
                GUI.Label(new Rect(panel.x + 68f, panel.y + 50f, panel.width - 80f, 14f), LocalPlayer.LastPowerName, eyebrowStyle);
            }
        }

        private void DrawCombatModeHud()
        {
            if (LocalPlayer == null) return;
            float powerWidth = 350f;
            float powerX = (viewWidth - powerWidth) * 0.5f;
            if (powerX < 430f) powerX = Mathf.Min(430f, viewWidth - powerWidth - 20f);

            float width = 420f;
            float x = powerX + powerWidth + 10f;
            float y = viewHeight - 108f;
            if (x + width > viewWidth - 20f)
            {
                x = Mathf.Max(20f, powerX + powerWidth - width);
                y -= 110f;
            }

            bool hasAmmo = LocalPlayer.RangedAmmo > 0;
            bool reloading = LocalPlayer.IsRangedReloading;
            Color accent = reloading
                ? new Color(0.96f, 0.68f, 0.2f)
                : hasAmmo ? new Color(0.28f, 0.82f, 1f) : new Color(1f, 0.3f, 0.24f);
            Rect panel = new Rect(x, y, width, 96f);
            DrawCartoonPanel(panel, new Color(0.025f, 0.045f, 0.048f, 0.94f), accent, 1f);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 16f),
                "ATALHOS DE ATAQUE", eyebrowStyle);
            DrawShortcutRow(new Rect(panel.x + 12f, panel.y + 27f, panel.width - 24f, 26f),
                true, "Mouse esquerdo - Ataque longo", accent);
            DrawShortcutRow(new Rect(panel.x + 12f, panel.y + 57f, panel.width - 24f, 26f),
                false, "Mouse direito - Ataque corpo a corpo", new Color(0.96f, 0.68f, 0.2f));
            string ammoStatus = reloading
                ? $"RECARGA {LocalPlayer.RangedReloadSecondsRemaining:0.0}s"
                : $"PENTE {LocalPlayer.RangedMagazineAmmo}/{LocalPlayer.RangedMagazineCapacityValue}  RES. {LocalPlayer.RangedReserveAmmo}";
            GUI.Label(new Rect(panel.x + panel.width - 190f, panel.y + 8f, 178f, 20f), ammoStatus, rightStyle);
            Rect modeBar = new Rect(panel.x + 12f, panel.y + 86f, panel.width - 24f, 4f);
            DrawRoundedRect(modeBar, new Color(accent.r, accent.g, accent.b, 0.75f));
        }

        private void DrawShortcutRow(Rect row, bool leftButton, string label, Color accent)
        {
            Rect icon = new Rect(row.x, row.y, 58f, row.height);
            DrawMouseIcon(icon, leftButton, accent);
            GUI.Label(new Rect(row.x + 68f, row.y, row.width - 68f, row.height), label, smallStyle);
        }

        private void DrawKeycapIcon(Rect rect, string keyText, Color accent, bool ready)
        {
            Color fill = ready ? new Color(0.12f, 0.38f, 0.25f, 1f) : new Color(0.16f, 0.17f, 0.16f, 1f);
            DrawCartoonPanel(rect, fill, accent, 1f);
            DrawRoundedRect(new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 5f), new Color(1f, 1f, 1f, 0.12f));
            GUI.Label(rect, keyText, centeredStyle);
        }

        private void DrawMouseIcon(Rect rect, bool leftButton, Color accent)
        {
            Rect body = new Rect(rect.x + 12f, rect.y + 2f, 34f, rect.height - 4f);
            DrawRoundedRect(body, new Color(0.06f, 0.075f, 0.075f, 1f));
            DrawRoundedRect(new Rect(body.x + 2f, body.y + 2f, body.width - 4f, body.height - 4f), new Color(0.13f, 0.16f, 0.16f, 1f));

            Rect left = new Rect(body.x + 4f, body.y + 4f, body.width * 0.5f - 5f, body.height * 0.42f);
            Rect right = new Rect(body.center.x + 1f, body.y + 4f, body.width * 0.5f - 5f, body.height * 0.42f);
            DrawRoundedRect(leftButton ? left : right, accent);
            DrawRoundedRect(leftButton ? right : left, new Color(0.22f, 0.25f, 0.25f, 1f));

            Color previous = GUI.color;
            GUI.color = new Color(0.03f, 0.04f, 0.04f, 1f);
            GUI.DrawTexture(new Rect(body.center.x - 1f, body.y + 4f, 2f, body.height * 0.42f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(body.x + 10f, body.y + body.height * 0.5f, body.width - 20f, 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawAimReticle()
        {
            bool vineTarget = LocalPlayer != null && VineAnchor.IsLookedAtBy(LocalPlayer);
            bool hasAmmo = LocalPlayer != null && LocalPlayer.RangedAmmo > 0;
            bool reloading = LocalPlayer != null && LocalPlayer.IsRangedReloading;
            Color previousColor = GUI.color;
            GUI.color = vineTarget
                ? new Color(0.3f, 1f, 0.58f, 1f)
                : reloading ? new Color(0.96f, 0.68f, 0.2f, 1f)
                : hasAmmo ? new Color(0.28f, 0.82f, 1f, 1f) : new Color(1f, 0.3f, 0.24f, 1f);

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
                Rect prompt = new Rect(centerX - 150f, centerY + 24f, 300f, 34f);
                DrawCartoonPanel(prompt, new Color(0.025f, 0.075f, 0.055f, 0.92f), new Color(0.3f, 0.9f, 0.58f, 1f), 1f);
                string vineAction = LocalPlayer != null && LocalPlayer.IsHangingVine
                    ? $"Q  PRÓXIMO CIPÓ  {LocalPlayer.VinesVisitedInChain + 1}/{ThirdPersonAnimalController.MaxVinesPerChain}"
                    : $"Q  AGARRAR CIPÓ  1/{ThirdPersonAnimalController.MaxVinesPerChain}";
                GUI.Label(prompt, vineAction, centeredStyle);
            }
            else if (LocalPlayer != null)
            {
                Rect rangedStatus = new Rect(centerX - 250f, centerY + 24f, 500f, 26f);
                string status = reloading
                    ? $"RECARREGANDO... {LocalPlayer.RangedReloadSecondsRemaining:0.0}s"
                    : $"MOUSE ESQUERDO - {LocalPlayer.RangedAttackName}  "
                      + $"{LocalPlayer.RangedMagazineAmmo}/{LocalPlayer.RangedMagazineCapacityValue}  •  RESERVA {LocalPlayer.RangedReserveAmmo}";
                GUI.Label(rangedStatus, status, centeredStyle);
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
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 80f, 20f), "MAPA", eyebrowStyle);
            GUI.Label(new Rect(panel.x + panel.width - 82f, panel.y + 8f, 70f, 20f),
                "▲  VOCÊ", rightStyle);

            Rect map = new Rect(panel.x + 8f, panel.y + 34f, panel.width - 16f, panel.width - 16f);
            Color previous = GUI.color;
            GUI.color = new Color(0.055f, 0.23f, 0.12f, 1f);
            GUI.DrawTexture(map, Texture2D.whiteTexture);
            GUI.color = new Color(0.28f, 0.54f, 0.24f, 0.24f);
            GUI.DrawTexture(new Rect(map.center.x - 1f, map.y, 2f, map.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(map.x, map.center.y - 1f, map.width, 2f), Texture2D.whiteTexture);

            foreach (LifePickup pickup in LifePickup.ActivePickups)
            {
                if (pickup == null || !pickup.IsAvailable) continue;
                Vector2 point = WorldToMinimap(pickup.transform.position, map, jungle.MapSize);
                DrawMinimapLifeMarker(point, 9f);
            }

            foreach (RangedAmmoPickup pickup in RangedAmmoPickup.ActivePickups)
            {
                if (pickup == null || !pickup.IsAvailable) continue;
                Vector2 point = WorldToMinimap(pickup.transform.position, map, jungle.MapSize);
                DrawMinimapAmmoMarker(point, 8f);
            }

            foreach (DiamondPickup diamond in DiamondPickup.ActivePickups)
            {
                if (diamond == null || !diamond.IsAvailable) continue;
                Vector2 point = WorldToMinimap(diamond.transform.position, map, jungle.MapSize);
                GUI.color = new Color(0.22f, 0.9f, 1f, 1f);
                GUI.DrawTexture(new Rect(point.x - 4f, point.y - 4f, 8f, 8f), minimapCircleTexture);
            }

            if (DiamondObjectiveManager.Instance != null)
            {
                Vector2 portalPoint = WorldToMinimap(DiamondObjectiveManager.Instance.PortalPosition, map, jungle.MapSize);
                GUI.color = new Color(0.75f, 0.25f, 1f, 1f);
                GUI.DrawTexture(new Rect(portalPoint.x - 6f, portalPoint.y - 6f, 12f, 12f), minimapRingTexture);
            }

            foreach (MissionNode node in MissionNode.ActiveNodes)
            {
                if (node == null || node.IsActivated) continue;
                Vector2 point = WorldToMinimap(node.transform.position, map, jungle.MapSize);
                GUI.color = node.Kind == MissionNodeKind.Lore
                    ? new Color(0.9f, 0.35f, 1f, 1f)
                    : new Color(1f, 0.76f, 0.12f, 1f);
                GUI.DrawTexture(new Rect(point.x - 3f, point.y - 3f, 6f, 6f), Texture2D.whiteTexture);
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
                if (fighter == null || fighter.Health == null || fighter.Health.IsDead) continue;
                Vector2 point = WorldToMinimap(fighter.transform.position, map, jungle.MapSize);
                if (!map.Contains(point)) continue;
                if (fighter == LocalPlayer)
                {
                    DrawMinimapPlayerArrow(point, fighter.transform.eulerAngles.y);
                }
                else
                {
                    int fighterDiamonds = DiamondObjectiveManager.Instance != null
                        ? DiamondObjectiveManager.Instance.GetCount(fighter)
                        : 0;
                    if (fighterDiamonds <= 0) continue;
                    GUI.color = new Color(0.15f, 0.92f, 1f, 1f);
                    GUI.DrawTexture(new Rect(point.x - 7f, point.y - 7f, 14f, 14f), minimapRingTexture);
                    GUI.color = new Color(1f, 0.28f, 0.16f, 1f);
                    GUI.DrawTexture(new Rect(point.x - 3.5f, point.y - 3.5f, 7f, 7f), minimapCircleTexture);
                    GUI.color = Color.white;
                    float countX = point.x > map.xMax - 22f ? point.x - 23f : point.x + 5f;
                    float countY = Mathf.Clamp(point.y - 10f, map.y, map.yMax - 18f);
                    GUI.Label(new Rect(countX, countY, 18f, 18f), fighterDiamonds.ToString(), eyebrowStyle);
                }
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(map.center.x - 12f, map.y + 2f, 24f, 18f), "N", minimapStyle);

            float legendY = map.yMax + 1f;
            DrawMinimapLifeMarker(new Vector2(panel.x + 16f, legendY + 8f), 8f);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 23f, legendY, 48f, 16f), "VIDA", eyebrowStyle);
            DrawMinimapAmmoMarker(new Vector2(panel.center.x + 5f, legendY + 8f), 7f);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.center.x + 12f, legendY, panel.width * 0.5f - 18f, 16f), "MUNIÇÃO", eyebrowStyle);
            GUI.color = previous;
        }

        private void DrawMinimapPlayerArrow(Vector2 point, float headingDegrees)
        {
            Color previous = GUI.color;
            int directionIndex = Mathf.RoundToInt(Mathf.Repeat(headingDegrees, 360f)
                                                  / 360f * MinimapArrowDirectionCount)
                                 % MinimapArrowDirectionCount;
            Texture2D arrow = minimapArrowTextures[directionIndex];
            GUI.color = new Color(0.015f, 0.025f, 0.02f, 0.98f);
            GUI.DrawTexture(new Rect(point.x - 10f, point.y - 12f, 20f, 24f), arrow);
            GUI.color = new Color(1f, 0.88f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(point.x - 7.5f, point.y - 9.5f, 15f, 19f), arrow);
            GUI.color = previous;
        }

        private void DrawMinimapLifeMarker(Vector2 point, float size)
        {
            GUI.color = new Color(0.01f, 0.05f, 0.025f, 0.98f);
            GUI.DrawTexture(new Rect(point.x - size * 0.62f, point.y - size * 0.62f, size * 1.24f, size * 1.24f), minimapCircleTexture);
            GUI.color = new Color(0.2f, 1f, 0.42f, 1f);
            GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), minimapCircleTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(point.x - size * 0.09f, point.y - size * 0.31f, size * 0.18f, size * 0.62f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(point.x - size * 0.31f, point.y - size * 0.09f, size * 0.62f, size * 0.18f), Texture2D.whiteTexture);
        }

        private void DrawMinimapAmmoMarker(Vector2 point, float size)
        {
            GUI.color = new Color(0.06f, 0.025f, 0.005f, 0.98f);
            GUI.DrawTexture(new Rect(point.x - size * 0.64f, point.y - size * 0.64f, size * 1.28f, size * 1.28f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.58f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.94f, 0.66f, 1f);
            GUI.DrawTexture(new Rect(point.x - size * 0.3f, point.y - size * 0.18f, size * 0.6f, size * 0.13f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(point.x - size * 0.3f, point.y + size * 0.08f, size * 0.6f, size * 0.13f), Texture2D.whiteTexture);
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

            int lives = LocalPlayer.LivesRemaining;
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width * 0.55f, 20f), $"VIVOS  {AliveCount}", smallStyle);
            GUI.Label(new Rect(panel.x + panel.width * 0.5f, panel.y + 8f, panel.width * 0.5f - 12f, 20f),
                $"VIDAS  {lives}/{ThirdPersonAnimalController.MaxLives}", rightStyle);

            string zoneText = "ÁREA SEGURA INDISPONÍVEL";
            if (zone != null)
            {
                zoneText = outside
                    ? "FORA DA ÁREA — QUEIMADA: -10/s"
                    : zone.TimeUntilShrink > 0f
                        ? $"QUEIMADA AVANÇA EM {zone.TimeUntilShrink:0}s"
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

        private void DrawOpeningCountdown()
        {
            int seconds = Mathf.Max(1, Mathf.CeilToInt(OpeningSecondsRemaining));
            float width = Mathf.Min(510f, viewWidth - 48f);
            Rect panel = new Rect((viewWidth - width) * 0.5f, viewHeight * 0.15f, width, 92f);
            DrawCartoonPanel(panel, new Color(0.025f, 0.065f, 0.05f, 0.96f),
                new Color(0.38f, 0.95f, 0.59f, 1f), 2f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 9f, panel.width - 36f, 30f),
                "EXPLORE A CLAREIRA", resultStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 42f, panel.width - 36f, 35f),
                $"COMBATE LIBERADO EM {seconds}  •  NENHUM DANO", centeredStyle);
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

        private static Texture2D[] CreateMinimapArrowTextures()
        {
            const int size = 64;
            Texture2D[] textures = new Texture2D[MinimapArrowDirectionCount];
            float half = (size - 1) * 0.5f;
            for (int direction = 0; direction < MinimapArrowDirectionCount; direction++)
            {
                float radians = direction * Mathf.PI * 2f / MinimapArrowDirectionCount;
                float cosine = Mathf.Cos(radians);
                float sine = Mathf.Sin(radians);
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = $"MinimapPlayerArrow_{direction}",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                Color[] pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float outputX = (x - half) / half;
                        float outputY = (y - half) / half;
                        float arrowX = cosine * outputX - sine * outputY;
                        float arrowY = sine * outputX + cosine * outputY;
                        bool tail = arrowY >= -0.82f && arrowY <= 0.18f && Mathf.Abs(arrowX) <= 0.22f;
                        float headHalfWidth = Mathf.Lerp(0.78f, 0f, Mathf.InverseLerp(-0.05f, 0.92f, arrowY));
                        bool head = arrowY >= -0.05f && arrowY <= 0.92f && Mathf.Abs(arrowX) <= headHalfWidth;
                        pixels[y * size + x] = new Color(1f, 1f, 1f, tail || head ? 1f : 0f);
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                textures[direction] = texture;
            }
            return textures;
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
                && minimapCircleTexture != null && minimapRingTexture != null
                && minimapArrowTextures is { Length: MinimapArrowDirectionCount }) return;

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
            minimapArrowTextures = CreateMinimapArrowTextures();
            RuntimeGuiTheme.Ensure();
        }
    }
}
