using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalBattleRoyale
{
    public sealed class BattleRoyaleManager : MonoBehaviour
    {
        private const int MinimapArrowDirectionCount = 36;
        private const float RespawnCountdownSeconds = 3f;
        // Top of the right-side column (minimap, then the time/alive/elims strip, then
        // zone/objective panels). The ammo selector now lives horizontally in the footer.
        // Nudged down from the old 58f so the pause menu's hamburger button — now pinned
        // fully into the top-right corner — has clearance above it.
        private const float RightColumnTopY = 66f;
        // Height reserved for the TEMPO/VIVOS/ELIMS strip now sitting under the minimap
        // (moved from above it), plus the gap below it before the next panel.
        private const float MatchCountersStripHeight = 34f;
        private const float MatchCountersStripGap = 10f;

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
        private GUIStyle compassMajorStyle;
        private GUIStyle compassMinorStyle;
        private GUIStyle counterStyle;
        private GUIStyle weaponLockStyle;
        private GUIStyle abilityCountdownStyle;
        private Texture2D[] minimapArrowTextures;
        private readonly Texture[] animalPortraits = new Texture[AnimalRoster.Count];
        private readonly Texture2D[] weaponIconsColor = new Texture2D[3];
        private readonly Texture2D[] weaponIconsGray = new Texture2D[3];
        private readonly Texture2D[] weaponIconsHorizontalColor = new Texture2D[3];
        private readonly Texture2D[] weaponIconsHorizontalGray = new Texture2D[3];
        private readonly Dictionary<string, Texture2D> abilityIconCache = new Dictionary<string, Texture2D>();
        private Texture2D ammoReloadIcon;
        // OnGUI can fire several times per rendered frame (Layout/Repaint/input events), and
        // these particular labels get re-formatted on every single one of those calls even
        // though the underlying value usually hasn't changed since the last call — each is
        // cached and only rebuilt when its source value actually changes.
        private float cachedHealthValue = float.NaN;
        private float cachedHealthMax = float.NaN;
        private string cachedHealthText = string.Empty;
        private int cachedElapsedSeconds = -1;
        private string cachedElapsedText = "00:00";
        private int cachedMagazineAmmo = int.MinValue;
        private bool cachedReloadingState;
        private string cachedMagazineText = string.Empty;
        private int cachedReserveAmmo = int.MinValue;
        private string cachedReserveText = string.Empty;
        private readonly int[] cachedAbilityCountdown = { int.MinValue, int.MinValue };
        private readonly string[] cachedAbilityCountdownText = { string.Empty, string.Empty };
        private JungleGenerator jungle;
        public Rect WeaponSelectorScreenRect { get; private set; }
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
        private float respawnCountdownEndsAt;
        private float matchStartedAt;
        private int localEliminations;

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
        public float RespawnSecondsRemaining => Mathf.Max(0f, respawnCountdownEndsAt - Time.time);

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
            matchStartedAt = Time.time;
            localEliminations = 0;
            combatUnlockTime = Time.time + Mathf.Max(0f, duration);
        }

        private void OnFighterDied(Health defeated, ThirdPersonAnimalController attacker)
        {
            ThirdPersonAnimalController owner = defeated.Owner;
            if (owner == null) { RecalculateAlive(); return; }

            Vector3 deathPosition = GetGroundedDeathPosition(owner.transform.position);
            AttackVfx.CreateBurst(deathPosition + Vector3.up, new Color(0.92f, 0.08f, 0.045f), 1.6f);
            if (attacker != null && attacker == LocalPlayer && owner != LocalPlayer) localEliminations++;
            ForestMissionDirector.Instance?.RecordElimination(attacker);

            // 3-lives battle royale: a death spends a life and respawns after a short
            // countdown, unless it was the last one.
            if (owner.ConsumeLife())
            {
                owner.BeginRespawnCountdown();
                if (owner.IsLocalPlayer) respawnCountdownEndsAt = Time.time + RespawnCountdownSeconds;
                StartCoroutine(RespawnAfterCountdown(owner));
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

        private IEnumerator RespawnAfterCountdown(ThirdPersonAnimalController owner)
        {
            yield return new WaitForSeconds(RespawnCountdownSeconds);
            if (owner == null || owner.IsEliminated) yield break;

            owner.Respawn(FindRespawnPosition(owner));
            if (owner.IsLocalPlayer)
            {
                respawnBannerUntil = Time.time + 2.6f;
                respawnBannerLives = owner.LivesRemaining;
            }
            RecalculateAlive();
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

            Vector3 best = GetRespawnCandidate(safeZone, respawning);
            float bestClearance = -1f;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector3 candidate = GetRespawnCandidate(safeZone, respawning);
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

        private Vector3 GetRespawnCandidate(SafeZoneController safeZone,
            ThirdPersonAnimalController respawning)
        {
            Vector3 candidate = safeZone != null
                ? safeZone.GetRandomRespawnPoint()
                : jungle.GetMissionSpawnPosition();
            float radius = respawning != null ? respawning.Stats.ControllerRadius : 0.5f;
            float height = respawning != null ? respawning.Stats.ControllerHeight : 1.8f;
            if (!jungle.TryFindSafeAnimalPosition(candidate, radius, height, out Vector3 safeCandidate,
                    20f, respawning != null ? respawning.transform : null))
                safeCandidate = jungle.GetGroundPosition(candidate);
            candidate = safeCandidate;
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

        public void RefreshReplicatedState()
        {
            RecalculateAlive();
            CheckWinCondition();
        }

        private void OnGUI()
        {
            EnsureStyles();
            // The upper bound used to be 1.18, which made sense for the ~1280x720 reference
            // size but meant the HUD stopped growing well before it matched the screen on
            // anything bigger than a small laptop — a 1920x1080 or 4K display got the exact
            // same on-screen HUD size as barely-larger-than-reference, reading as tiny.
            // Letting it scale further (up to 2.4x) keeps it legible on large/high-DPI
            // displays while the lower bound still protects small windows.
            uiScale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 2.4f);
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
            bool mobileControls = MobileInputController.ControlsEnabled;
            string abilityKey = mobileControls ? "PODER" : GameInputBindings.GetDisplayName(GameInputAction.Ability);
            string jumpKey = mobileControls ? "PULO" : GameInputBindings.GetDisplayName(GameInputAction.Jump);
            string rangedKey = mobileControls ? "TIRO" : GameInputBindings.GetDisplayName(GameInputAction.RangedAttack);
            string movementKeys = mobileControls
                ? "JOYSTICK"
                : $"{GameInputBindings.GetDisplayName(GameInputAction.MoveForward)}/"
                  + $"{GameInputBindings.GetDisplayName(GameInputAction.MoveLeft)}/"
                  + $"{GameInputBindings.GetDisplayName(GameInputAction.MoveBackward)}/"
                  + GameInputBindings.GetDisplayName(GameInputAction.MoveRight);
            if (LocalPlayer != null && LocalPlayer.Health != null)
            {
                DrawPlayerHud();
                DrawCompass();
                DrawMatchCounters();
            }

            if (OpeningSecondsRemaining > 0f) DrawOpeningCountdown();
            if (RespawnSecondsRemaining > 0f) DrawRespawnCountdown();

            string contextHint = LocalPlayer != null && LocalPlayer.IsVineLeaping
                ? $"PUXANDO PELO CIPÓ — {movementKeys} controla o desvio lateral"
                : LocalPlayer != null && LocalPlayer.IsHangingVine
                    ? $"CIPÓ — {movementKeys} balança • mire em qualquer superfície e pressione {abilityKey} • {jumpKey} solta"
                : LocalPlayer != null && LocalPlayer.IsFlying
                    ? $"SALTO PLANADO {LocalPlayer.GlideSecondsRemaining:0.0}s — segure {rangedKey} para atirar"
                : LocalPlayer != null && LocalPlayer.IsInAntTunnel
                ? $"NO TÚNEL: invisível — {movementKeys} anda até uma saída neon • {abilityKey} sai • forçado em {LocalPlayer.TunnelSecondsRemaining:0.0}s"
                : LocalPlayer != null && LocalPlayer.IsSwimming
                    ? $"NADANDO — {jumpKey} dá impulso • suba pela margem"
                : LocalPlayer != null && LocalPlayer.IsWading
                    ? "NO LAGO — VELOCIDADE REDUZIDA • alcance a margem para correr normalmente"
                : string.Empty;
            if (!string.IsNullOrEmpty(contextHint)) DrawContextHint(contextHint);

            // The mobile ability is represented by the right-side action button.
            // Keeping the desktop cooldown widget here would occupy the middle of
            // the touch layout.
            if (!mobileControls) DrawAbilityAndAmmoBar();

            DrawAimReticle();

            DrawMinimap();

            DrawObjectiveStatus();

            DrawWeaponSelector();

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

        public void PrepareForSceneExit()
        {
            shuttingDown = true;
        }

        private void OnApplicationQuit()
        {
            shuttingDown = true;
        }

        private void OnDestroy()
        {
            shuttingDown = true;
            for (int i = 0; i < animalPortraits.Length; i++)
            {
                if (animalPortraits[i] is RenderTexture renderTexture) AnimalPreviewRenderer.Release(renderTexture);
            }
            if (Instance == this) Instance = null;
        }

        private void DrawPlayerHud()
        {
            AnimalStats stats = LocalPlayer.Stats;
            Health health = LocalPlayer.Health;
            if (health == null || string.IsNullOrEmpty(stats.DisplayName)) return;
            // Ammo and the weapon-crystal progress moved to the bottom-center ammo slot and
            // the weapon selector respectively — this panel now only shows who you are and
            // how much health you have left.
            Rect panel = new Rect(20f, 20f, 328f, 124f);
            float healthNormalized = health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;
            Color healthColor = healthNormalized <= 0.25f
                ? new Color(1f, 0.24f, 0.2f)
                : healthNormalized <= 0.55f
                    ? new Color(1f, 0.68f, 0.18f)
                    : new Color(0.02f, 0.86f, 0.45f);
            Color panelBorder = Time.time < healthDamagePulseUntil
                ? new Color(1f, 0.3f, 0.18f)
                : new Color(0.08f, 0.72f, 0.46f, 0.95f);
            DrawCartoonPanel(panel, new Color(0.006f, 0.026f, 0.025f, 0.86f), panelBorder, 1f);

            // Both blocks are vertically centered within the panel independently (portrait is
            // 82 tall, the name/health/hearts column is 76 tall — close but not identical, so
            // each gets its own centering offset rather than sharing one fixed top margin,
            // which is what read as "pushed up" before.
            const float portraitBlockHeight = 82f;
            const float textBlockHeight = 76f;
            float portraitY = panel.y + (panel.height - portraitBlockHeight) * 0.5f;
            float textY = panel.y + (panel.height - textBlockHeight) * 0.5f;

            Rect portraitFrame = new Rect(panel.x + 7f, portraitY, 82f, 82f);
            DrawSquareFrame(portraitFrame, new Color(0.03f, 0.065f, 0.055f, 1f),
                Color.Lerp(stats.MainColor, new Color(0.2f, 1f, 0.55f), 0.42f), 2f);
            Texture portrait = GetAnimalPortrait(LocalPlayer.AnimalType);
            if (portrait != null) GUI.DrawTexture(new Rect(portraitFrame.x + 5f, portraitFrame.y + 5f, 72f, 72f), portrait, ScaleMode.ScaleToFit, true);

            float contentX = panel.x + 100f;
            float contentWidth = panel.width - 109f;
            GUI.Label(new Rect(contentX, textY, contentWidth, 20f), stats.DisplayName.ToUpperInvariant(), titleStyle);
            DrawStatBar(new Rect(contentX, textY + 24f, contentWidth, 22f), healthNormalized, healthTrailNormalized,
                healthColor, "VIDA", GetHealthText(health));
            DrawLivesHearts(new Rect(contentX, textY + 52f, contentWidth, 24f), LocalPlayer.LivesRemaining);

            if (healthNormalized <= 0.25f) DrawLowHealthVignette();
        }

        private string GetHealthText(Health health)
        {
            if (health.CurrentHealth == cachedHealthValue && health.MaxHealth == cachedHealthMax)
                return cachedHealthText;
            cachedHealthValue = health.CurrentHealth;
            cachedHealthMax = health.MaxHealth;
            cachedHealthText = $"{cachedHealthValue:0} / {cachedHealthMax:0}";
            return cachedHealthText;
        }

        // A numeric "3" badge was hard to read at a glance — three hearts that empty out one
        // at a time on death reads instantly instead of needing to parse a number.
        private void DrawLivesHearts(Rect row, int livesRemaining)
        {
            const int totalLives = ThirdPersonAnimalController.MaxLives;
            const float heartSize = 22f;
            const float spacing = 6f;
            for (int i = 0; i < totalLives; i++)
            {
                Rect heart = new Rect(row.x + i * (heartSize + spacing), row.y, heartSize, heartSize);
                bool alive = i < livesRemaining;
                GUI.color = alive ? new Color(0.94f, 0.16f, 0.22f) : new Color(0.3f, 0.28f, 0.28f, 0.5f);
                GUI.DrawTexture(heart, alive ? RuntimeGuiTheme.HeartTexture : RuntimeGuiTheme.HeartRingTexture,
                    ScaleMode.ScaleToFit, true);
            }
            GUI.color = Color.white;
        }

        private Texture GetAnimalPortrait(AnimalType type)
        {
            int index = (int)type;
            if (animalPortraits[index] != null) return animalPortraits[index];
            Texture2D face = Resources.Load<Texture2D>($"UI/CharacterPortraits/{type}");
            animalPortraits[index] = face != null ? face : AnimalPreviewRenderer.Create(type, 192);
            return animalPortraits[index];
        }

        private Texture2D GetWeaponIcon(WeaponAmmoType weapon, bool unlocked)
        {
            int index = (int)weapon;
            if (weaponIconsColor[index] == null)
                weaponIconsColor[index] = Resources.Load<Texture2D>($"UI/WeaponIcons/{weapon}");
            Texture2D source = weaponIconsColor[index];
            if (!unlocked && source != null)
            {
                if (weaponIconsGray[index] == null)
                    weaponIconsGray[index] = GenerateGrayscale(source);
                source = weaponIconsGray[index];
            }
            if (source == null || weapon == WeaponAmmoType.Watermelon) return source;

            Texture2D[] horizontalCache = unlocked
                ? weaponIconsHorizontalColor
                : weaponIconsHorizontalGray;
            if (horizontalCache[index] == null)
                horizontalCache[index] = GenerateQuarterTurn(source);
            return horizontalCache[index];
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

        private void DrawCompass()
        {
            Vector3 aimDirection = LocalPlayer.ViewAimDirection;
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude < 0.001f) aimDirection = LocalPlayer.transform.forward;
            float heading = Mathf.Repeat(Mathf.Atan2(aimDirection.x, aimDirection.z) * Mathf.Rad2Deg, 360f);

            const float width = 500f;
            Rect panel = new Rect((viewWidth - width) * 0.5f, 14f, width, 70f);
            DrawRoundedRect(panel, new Color(0.005f, 0.02f, 0.022f, 0.32f));
            GUI.BeginGroup(panel);

            const float pixelsPerDegree = 2.65f;
            int nearestTick = Mathf.RoundToInt(heading / 15f);
            for (int offset = -7; offset <= 7; offset++)
            {
                float angle = Mathf.Repeat((nearestTick + offset) * 15f, 360f);
                float delta = Mathf.DeltaAngle(heading, angle);
                float x = panel.width * 0.5f + delta * pixelsPerDegree;
                if (x < 10f || x > panel.width - 10f) continue;

                int roundedAngle = Mathf.RoundToInt(angle) % 360;
                bool major = roundedAngle % 45 == 0;
                GUI.color = major ? Color.white : new Color(0.8f, 0.9f, 0.92f, 0.78f);
                GUI.DrawTexture(new Rect(x - 1f, major ? 29f : 34f, major ? 2f : 1f, major ? 12f : 7f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                string label = major ? GetCompassLabel(roundedAngle) : roundedAngle.ToString();
                GUI.Label(new Rect(x - 30f, 2f, 60f, 24f), label, major ? compassMajorStyle : compassMinorStyle);
            }

            GUI.Label(new Rect(panel.width * 0.5f - 42f, 38f, 84f, 22f), Mathf.RoundToInt(heading).ToString(), counterStyle);
            GUI.Label(new Rect(panel.width * 0.5f - 18f, 52f, 36f, 18f), "▼", compassMajorStyle);
            GUI.EndGroup();
        }

        private static string GetCompassLabel(int heading)
        {
            return heading switch
            {
                0 => "N",
                45 => "NE",
                90 => "L",
                135 => "SE",
                180 => "S",
                225 => "SO",
                270 => "O",
                315 => "NO",
                _ => heading.ToString()
            };
        }

        private void DrawMatchCounters()
        {
            float elapsed = Mathf.Max(0f, Time.time - matchStartedAt);
            int totalSeconds = Mathf.FloorToInt(elapsed);
            if (totalSeconds != cachedElapsedSeconds)
            {
                cachedElapsedSeconds = totalSeconds;
                cachedElapsedText = $"TEMPO  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }
            // Sits directly under the minimap now (used to float above it) — same width as
            // the map instead of the wider free-floating strip that fit above it.
            float minimapSize = Mathf.Clamp(viewHeight * 0.29f, 190f, 232f);
            Rect strip = new Rect(viewWidth - minimapSize - 20f, RightColumnTopY + minimapSize + 29f,
                minimapSize, MatchCountersStripHeight);
            DrawCartoonPanel(strip, new Color(0.005f, 0.02f, 0.022f, 0.87f), new Color(0.08f, 0.22f, 0.19f, 0.9f), 1f);

            float cellWidth = strip.width / 3f;
            GUI.Label(new Rect(strip.x, strip.y, cellWidth, strip.height), cachedElapsedText, counterStyle);
            GUI.Label(new Rect(strip.x + cellWidth, strip.y, cellWidth, strip.height), $"VIVOS  {AliveCount}", counterStyle);
            GUI.Label(new Rect(strip.x + cellWidth * 2f, strip.y, cellWidth, strip.height), $"ELIMS  {localEliminations}", counterStyle);

            Color previous = GUI.color;
            GUI.color = new Color(0.25f, 0.92f, 0.62f, 0.5f);
            GUI.DrawTexture(new Rect(strip.x + cellWidth, strip.y + 7f, 1f, strip.height - 14f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(strip.x + cellWidth * 2f, strip.y + 7f, 1f, strip.height - 14f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static readonly WeaponAmmoType[] WeaponSelectorSlots =
        {
            WeaponAmmoType.Tomato, WeaponAmmoType.Watermelon, WeaponAmmoType.Seed
        };
        private static readonly GameInputAction[] WeaponSelectorActions =
        {
            GameInputAction.WeaponPrimary,
            GameInputAction.WeaponSecondary,
            GameInputAction.WeaponThird
        };

        // Re-enabled alongside RangedCombatEnabled in ThirdPersonAnimalController.
        private const bool WeaponSelectorEnabled = true;

        private void DrawWeaponSelector()
        {
            if (!WeaponSelectorEnabled || LocalPlayer == null) return;
            bool mobileControls = MobileInputController.ControlsEnabled;
            float desiredIconSize = mobileControls ? 70f : 64f;
            float spacing = mobileControls ? 12f : 14f;
            const float textHeight = 18f;
            const float textGap = 4f;

            GetAbilityAndAmmoBarRects(out _, out _, out Rect ability2Rect);
            float availableLeft = ability2Rect.xMax + 18f;
            float availableRight = viewWidth - 20f;
            float availableWidth = Mathf.Max(0f, availableRight - availableLeft);
            float iconSize = Mathf.Min(desiredIconSize,
                Mathf.Max(36f, (availableWidth - spacing * (WeaponSelectorSlots.Length - 1)) / WeaponSelectorSlots.Length));
            float rowWidth = iconSize * WeaponSelectorSlots.Length + spacing * (WeaponSelectorSlots.Length - 1);
            float rowHeight = iconSize + textGap + textHeight;

            // Center the horizontal ammo strip in the footer space between the E
            // ability and the right edge. On touch layouts that same horizontal
            // position is kept, but the strip moves just above the action-button
            // row so it cannot cover the fire joystick.
            float startX = availableLeft + (availableWidth - rowWidth) * 0.5f;
            float startY = mobileControls
                ? Mathf.Max(72f, viewHeight - 360f)
                : ability2Rect.center.y - iconSize * 0.5f;

            float touchPadding = mobileControls ? 6f : 0f;
            WeaponSelectorScreenRect = new Rect((startX - touchPadding) * uiScale,
                (startY - touchPadding) * uiScale,
                (rowWidth + touchPadding * 2f) * uiScale,
                (rowHeight + touchPadding * 2f) * uiScale);

            // Empty types remain visible as grayscale collection goals. As soon as a pickup
            // adds at least one round, its full-color icon becomes selectable by click/tap
            // or the matching 1/2/3 shortcut.
            for (int i = 0; i < WeaponSelectorSlots.Length; i++)
            {
                WeaponAmmoType weapon = WeaponSelectorSlots[i];
                int reserve = LocalPlayer.ReserveAmmoFor(weapon);
                bool available = reserve > 0;
                bool selected = LocalPlayer.CurrentWeaponAmmo == weapon;
                float x = startX + i * (iconSize + spacing);
                Rect slot = new Rect(x, startY, iconSize, iconSize);
                Rect touchSlot = mobileControls
                    ? new Rect(slot.x - touchPadding, slot.y - touchPadding,
                        slot.width + touchPadding * 2f, slot.height + touchPadding * 2f)
                    : slot;

                if (available && !selected && GUI.Button(touchSlot, GUIContent.none, GUIStyle.none))
                {
                    SelectWeaponFromHud(weapon);
                }

                Texture icon = GetWeaponIcon(weapon, available);
                if (icon != null)
                {
                    Rect iconRect = selected ? new Rect(slot.x - 10f, slot.y - 10f, slot.width + 20f, slot.height + 20f) : slot;
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    DrawRoundedRect(slot, ThirdPersonAnimalController.ColorForWeapon(weapon));
                }

                string label = GameInputBindings.GetDisplayName(WeaponSelectorActions[i]);
                Color color = !available ? new Color(0.55f, 0.58f, 0.58f, 1f)
                    : selected ? new Color(1f, 0.92f, 0.35f, 1f)
                    : new Color(0.4f, 0.95f, 0.6f, 1f);
                float keyWidth = Mathf.Clamp(
                    weaponLockStyle.CalcSize(new GUIContent(label)).x + 12f,
                    24f, iconSize - 4f);
                Rect keyRect = new Rect(slot.x + 2f, slot.y + 2f, keyWidth, 20f);
                DrawKeycapIcon(keyRect, label, color, available);

                // Current collection and its cap stay visible together: 10/20, 60/120, etc.
                Rect countRect = new Rect(slot.x - 10f, slot.yMax + textGap, iconSize + 20f, textHeight);
                DrawOutlinedLabel(countRect,
                    $"{reserve}/{ThirdPersonAnimalController.MaxAmmoForWeapon(weapon)}",
                    available ? new Color(0.85f, 0.9f, 0.92f, 1f)
                        : new Color(0.48f, 0.5f, 0.5f, 1f));
            }
        }

        private void DrawOutlinedLabel(Rect rect, string text, Color color, GUIStyle style = null)
        {
            style ??= weaponLockStyle;
            Color shadow = new Color(0f, 0f, 0f, 0.85f);
            style.normal.textColor = shadow;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        // Shared by the keyboard (1/2/3) and this HUD panel's tap/click targets so both
        // paths report the same way to the host in online matches.
        private void SelectWeaponFromHud(WeaponAmmoType weapon)
        {
            if (LocalPlayer == null || !LocalPlayer.TrySelectWeapon(weapon)) return;
            OnlineMultiplayerManager.Instance?.ReportAction(OnlineActionType.SelectWeapon,
                new Vector3((int)weapon, 0f, 0f));
        }

        private static Color Desaturate(Color color, float amount)
        {
            float gray = color.r * 0.3f + color.g * 0.59f + color.b * 0.11f;
            return Color.Lerp(color, new Color(gray, gray, gray, color.a), amount);
        }

        // Bottom-center row: Ability 1 (Q) — Ammo/reload (bigger, center) — Ability 2 (E,
        // reserved for a future ability). Ability and ammo icons desaturate to grayscale when
        // unavailable (on cooldown / out of ammo), mirroring the weapon selector's locked look.
        private const float AbilityIconSize = 84f;
        private const float AmmoIconSize = 140f;
        private const float AbilityAmmoGap = 22f;

        private void GetAbilityAndAmmoBarRects(out Rect ability1Rect, out Rect ammoRect, out Rect ability2Rect)
        {
            float totalWidth = AbilityIconSize + AbilityAmmoGap + AmmoIconSize + AbilityAmmoGap + AbilityIconSize;
            float startX = viewWidth * 0.5f - totalWidth * 0.5f;
            float centerY = viewHeight - 34f - AmmoIconSize * 0.5f;

            ability1Rect = new Rect(startX, centerY - AbilityIconSize * 0.5f, AbilityIconSize, AbilityIconSize);
            ammoRect = new Rect(ability1Rect.xMax + AbilityAmmoGap, centerY - AmmoIconSize * 0.5f, AmmoIconSize, AmmoIconSize);
            ability2Rect = new Rect(ammoRect.xMax + AbilityAmmoGap, centerY - AbilityIconSize * 0.5f, AbilityIconSize, AbilityIconSize);
        }

        private void DrawAbilityAndAmmoBar()
        {
            if (LocalPlayer == null) return;

            GetAbilityAndAmmoBarRects(out Rect ability1Rect, out Rect ammoRect, out Rect ability2Rect);

            string abilityKey = MobileInputController.ControlsEnabled ? "P" : GameInputBindings.GetDisplayName(GameInputAction.Ability);
            DrawAbilitySlot(ability1Rect, 0, abilityKey);
            DrawAmmoSlot(ammoRect);
            DrawAbilitySlot(ability2Rect, 1, "E");
        }

        private void DrawAbilitySlot(Rect rect, int slot, string keyLabel)
        {
            AnimalStats stats = LocalPlayer.Stats;
            bool implemented = stats.AbilityNames != null && slot < stats.AbilityNames.Length
                && !string.Equals(stats.AbilityNames[slot], "DESATIVADO", System.StringComparison.OrdinalIgnoreCase);

            float remaining = implemented ? LocalPlayer.AbilityCooldownRemainingFor(slot) : 0f;
            bool ready = implemented && remaining <= 0.01f;
            Color accent = !implemented ? new Color(0.5f, 0.52f, 0.52f)
                : ready ? new Color(0.42f, 1f, 0.62f) : new Color(1f, 0.64f, 0.12f);

            if (!implemented)
            {
                // No ability here yet — an empty pentagon outline (same badge shape as the
                // real ability art) instead of a placeholder image, so it reads as "reserved
                // slot" rather than "missing icon".
                GUI.color = new Color(0.55f, 0.5f, 0.34f, 0.85f);
                GUI.DrawTexture(rect, RuntimeGuiTheme.PentagonRingTexture, ScaleMode.StretchToFill, true);
                GUI.color = Color.white;
            }
            else
            {
                Texture2D icon = GetAbilityIcon(LocalPlayer.AnimalType, slot, ready);
                if (icon != null)
                {
                    GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    // No icon dropped in yet for this animal/slot — a plain badge swatch keeps
                    // the layout readable instead of leaving a blank hole.
                    DrawRoundedRect(rect, Desaturate(accent, ready ? 0f : 0.5f));
                }
            }

            if (implemented && !ready)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.4f);
                GUI.DrawTexture(rect, RuntimeGuiTheme.CircleTexture);
                GUI.color = Color.white;
                int countdown = Mathf.CeilToInt(remaining);
                if (countdown != cachedAbilityCountdown[slot])
                {
                    cachedAbilityCountdown[slot] = countdown;
                    cachedAbilityCountdownText[slot] = countdown.ToString();
                }
                DrawOutlinedLabel(rect, cachedAbilityCountdownText[slot], Color.white, abilityCountdownStyle);
            }

            Rect keyRect = new Rect(rect.x + rect.width * 0.5f - 16f, rect.yMax - 12f, 32f, 24f);
            DrawKeycapIcon(keyRect, keyLabel, accent, implemented && ready);
        }

        private void DrawAmmoSlot(Rect rect)
        {
            int magazine = LocalPlayer.RangedMagazineAmmo;
            int reserve = LocalPlayer.RangedReserveAmmo;
            bool reloading = LocalPlayer.IsRangedReloading;
            bool empty = magazine <= 0 && reserve <= 0;

            // Same pentagon badge shape as the ability icons — filled backdrop plus a border
            // ring, drawn procedurally instead of needing a dedicated frame image.
            Color fillColor = empty ? new Color(0.1f, 0.045f, 0.04f, 0.92f) : new Color(0.03f, 0.05f, 0.06f, 0.92f);
            Color borderColor = empty ? new Color(0.6f, 0.32f, 0.28f) : reloading ? new Color(1f, 0.78f, 0.32f) : new Color(0.75f, 0.62f, 0.22f);
            GUI.color = fillColor;
            GUI.DrawTexture(rect, RuntimeGuiTheme.PentagonTexture, ScaleMode.StretchToFill, true);
            GUI.color = borderColor;
            GUI.DrawTexture(rect, RuntimeGuiTheme.PentagonRingTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;

            // The pentagon peaks near the top and tapers to points at the bottom corners, so
            // the rows sit in its wide upper-middle band rather than dead center.
            Rect magazineRow = new Rect(rect.x, rect.y + rect.height * 0.34f, rect.width, rect.height * 0.24f);
            Rect reserveRow = new Rect(rect.x, magazineRow.yMax + 2f, rect.width, rect.height * 0.16f);

            Color textColor = empty ? new Color(1f, 0.4f, 0.32f) : reloading ? new Color(1f, 0.78f, 0.32f) : Color.white;
            string magazineText;
            if (empty)
            {
                magazineText = "0";
            }
            else if (reloading)
            {
                // Ticks continuously while reloading, so caching by value would never hit —
                // this branch is left as a direct format each call.
                magazineText = $"{LocalPlayer.RangedReloadSecondsRemaining:0.0}s";
            }
            else
            {
                if (magazine != cachedMagazineAmmo || cachedReloadingState)
                {
                    cachedMagazineAmmo = magazine;
                    cachedMagazineText = magazine.ToString();
                }
                magazineText = cachedMagazineText;
            }
            cachedReloadingState = reloading;
            DrawIconValueRow(magazineRow,
                GetWeaponIcon(LocalPlayer.CurrentWeaponAmmo, !empty),
                magazineText, textColor, abilityCountdownStyle, magazineRow.height);
            if (!empty && !reloading)
            {
                if (reserve != cachedReserveAmmo)
                {
                    cachedReserveAmmo = reserve;
                    cachedReserveText = reserve.ToString();
                }
                DrawIconValueRow(reserveRow, GetAmmoReloadIcon(), cachedReserveText, new Color(0.85f, 0.88f, 0.88f), smallStyle, reserveRow.height);
            }
        }

        // Icon + number pair, centered as one group within rowRect (icon on the left of its
        // matching value) instead of a bare number, matching the bullet/reload art dropped
        // in for the ammo shield.
        private void DrawIconValueRow(Rect rowRect, Texture2D icon, string text, Color textColor,
            GUIStyle style, float iconSize)
        {
            Vector2 textSize = style.CalcSize(new GUIContent(text));
            float gap = icon != null ? 6f : 0f;
            float groupWidth = (icon != null ? iconSize + gap : 0f) + textSize.x;
            float groupX = rowRect.x + (rowRect.width - groupWidth) * 0.5f;
            if (icon != null)
            {
                Rect iconRect = new Rect(groupX, rowRect.y + (rowRect.height - iconSize) * 0.5f, iconSize, iconSize);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                groupX = iconRect.xMax + gap;
            }
            DrawOutlinedLabel(new Rect(groupX, rowRect.y, textSize.x + 4f, rowRect.height), text, textColor, style);
        }

        private Texture2D GetAbilityIcon(AnimalType type, int slot, bool ready)
        {
            string cacheKey = $"{type}_{slot}_{(ready ? "C" : "G")}";
            if (abilityIconCache.TryGetValue(cacheKey, out Texture2D cached)) return cached;

            string colorKey = $"{type}_{slot}_C";
            if (!abilityIconCache.TryGetValue(colorKey, out Texture2D color))
            {
                color = Resources.Load<Texture2D>($"UI/Abilities/{type}_Ability{slot + 1}");
                abilityIconCache[colorKey] = color;
            }
            if (ready || color == null)
            {
                abilityIconCache[cacheKey] = color;
                return color;
            }

            Texture2D gray = GenerateGrayscale(color);
            abilityIconCache[cacheKey] = gray;
            return gray;
        }

        private Texture2D GetAmmoReloadIcon()
        {
            if (ammoReloadIcon == null) ammoReloadIcon = Resources.Load<Texture2D>("UI/Abilities/AmmoReloadIcon");
            return ammoReloadIcon;
        }

        // Mirrors the weapon icons' pre-authored "_Locked" grayscale variant, but generated
        // at runtime from a single supplied image instead of requiring a second hand-made
        // asset per icon — needs the source texture's Read/Write enabled (see
        // AbilityIconImporter, which configures every icon dropped into Resources/UI/Abilities).
        private static Texture2D GenerateGrayscale(Texture2D source)
        {
            if (source == null) return null;
            try
            {
                Color32[] pixels = source.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];
                    byte gray = (byte)(p.r * 0.3f + p.g * 0.59f + p.b * 0.11f);
                    pixels[i] = new Color32(gray, gray, gray, p.a);
                }
                Texture2D gray2D = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
                {
                    name = source.name + "_Gray",
                    filterMode = source.filterMode,
                    wrapMode = source.wrapMode
                };
                gray2D.SetPixels32(pixels);
                gray2D.Apply();
                return gray2D;
            }
            catch (UnityException)
            {
                // Texture isn't readable (import settings not applied yet) — fall back to
                // the color version rather than throwing every OnGUI frame.
                return source;
            }
        }

        private static Texture2D GenerateQuarterTurn(Texture2D source)
        {
            if (source == null) return null;
            try
            {
                Color32[] sourcePixels = source.GetPixels32();
                Color32[] rotatedPixels = new Color32[sourcePixels.Length];
                int rotatedWidth = source.height;
                int rotatedHeight = source.width;

                // Counter-clockwise quarter turn: the tip that points upward in the supplied
                // walnut/tomato art ends up pointing left, matching the watermelon cartridge.
                for (int y = 0; y < source.height; y++)
                {
                    for (int x = 0; x < source.width; x++)
                    {
                        int rotatedX = source.height - 1 - y;
                        int rotatedY = x;
                        rotatedPixels[rotatedY * rotatedWidth + rotatedX] =
                            sourcePixels[y * source.width + x];
                    }
                }

                Texture2D rotated = new Texture2D(rotatedWidth, rotatedHeight, TextureFormat.RGBA32, false)
                {
                    name = source.name + "_Horizontal",
                    filterMode = source.filterMode,
                    wrapMode = source.wrapMode
                };
                rotated.SetPixels32(rotatedPixels);
                rotated.Apply();
                return rotated;
            }
            catch (UnityException)
            {
                // A vertical icon is preferable to an invisible one if an import setting
                // temporarily prevents pixel access.
                return source;
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
                GameInputAction.RangedAttack, "Ataque longo", accent);
            DrawShortcutRow(new Rect(panel.x + 12f, panel.y + 57f, panel.width - 24f, 26f),
                GameInputAction.MeleeAttack, "Ataque corpo a corpo", new Color(0.96f, 0.68f, 0.2f));
            string ammoStatus = reloading
                ? $"RECARGA {LocalPlayer.RangedReloadSecondsRemaining:0.0}s"
                : $"PENTE {LocalPlayer.RangedMagazineAmmo}/{LocalPlayer.RangedMagazineCapacityValue}  RES. {LocalPlayer.RangedReserveAmmo}";
            GUI.Label(new Rect(panel.x + panel.width - 190f, panel.y + 8f, 178f, 20f), ammoStatus, rightStyle);
            Rect modeBar = new Rect(panel.x + 12f, panel.y + 86f, panel.width - 24f, 4f);
            DrawRoundedRect(modeBar, new Color(accent.r, accent.g, accent.b, 0.75f));
        }

        private void DrawShortcutRow(Rect row, GameInputAction action, string label, Color accent)
        {
            Rect icon = new Rect(row.x, row.y, 58f, row.height);
            DrawKeycapIcon(icon, GameInputBindings.GetDisplayName(action), accent, true);
            GUI.Label(new Rect(row.x + 68f, row.y, row.width - 68f, row.height),
                $"{GameInputBindings.GetDisplayName(action)} - {label}", smallStyle);
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
            bool precisionAiming = LocalPlayer != null && LocalPlayer.IsAiming;
            Color previousColor = GUI.color;
            GUI.color = vineTarget
                ? new Color(0.3f, 1f, 0.58f, 1f)
                : reloading ? new Color(0.96f, 0.68f, 0.2f, 1f)
                : hasAmmo ? Color.white : new Color(1f, 0.3f, 0.24f, 1f);

            float centerX = viewWidth * 0.5f;
            float centerY = viewHeight * 0.5f;
            if (precisionAiming)
            {
                // Tight dot + thin ring instead of the open 4-tick crosshair — reads as
                // "zoomed/precise" while aiming down the sights.
                GUI.DrawTexture(new Rect(centerX - 1.5f, centerY - 1.5f, 3f, 3f), Texture2D.whiteTexture);
                RuntimeGuiTheme.DrawCircle(new Rect(centerX - 22f, centerY - 22f, 44f, 44f), GUI.color, ring: true);
            }
            else
            {
                GUI.DrawTexture(new Rect(centerX - 14f, centerY - 1f, 8f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centerX + 6f, centerY - 1f, 8f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centerX - 1f, centerY - 14f, 2f, 8f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centerX - 1f, centerY + 6f, 2f, 8f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centerX - 1.5f, centerY - 1.5f, 3f, 3f), Texture2D.whiteTexture);
            }
            GUI.color = previousColor;

            if (vineTarget)
            {
                Rect prompt = new Rect(centerX - 150f, centerY + 24f, 300f, 34f);
                DrawCartoonPanel(prompt, new Color(0.025f, 0.075f, 0.055f, 0.92f), new Color(0.3f, 0.9f, 0.58f, 1f), 1f);
                string abilityInput = MobileInputController.ControlsEnabled
                    ? "PODER"
                    : GameInputBindings.GetDisplayName(GameInputAction.Ability);
                string vineAction = LocalPlayer != null && LocalPlayer.IsHangingVine
                    ? $"{abilityInput}  PRENDER NO NOVO ALVO"
                    : $"{abilityInput}  LANÇAR CIPÓ";
                GUI.Label(prompt, vineAction, centeredStyle);
            }
            else if (LocalPlayer != null)
            {
                if (reloading)
                {
                    Rect reloadStatus = new Rect(centerX - 90f, centerY + 22f, 180f, 24f);
                    DrawRoundedRect(reloadStatus, new Color(0.02f, 0.03f, 0.03f, 0.72f));
                    GUI.Label(reloadStatus, $"RECARGA  {LocalPlayer.RangedReloadSecondsRemaining:0.0}s", centeredStyle);
                }
                else if (!hasAmmo)
                {
                    GUI.Label(new Rect(centerX - 100f, centerY + 22f, 200f, 24f), "SEM MUNIÇÃO", centeredStyle);
                }
            }
        }

        private void DrawMinimap()
        {
            if (LocalPlayer == null) return;
            if (jungle == null) jungle = FindAnyObjectByType<JungleGenerator>();
            if (jungle == null) return;

            float size = Mathf.Clamp(viewHeight * 0.29f, 190f, 232f);
            Rect panel = new Rect(viewWidth - size - 20f, RightColumnTopY, size, size + 29f);
            DrawCartoonPanel(panel, new Color(0.01f, 0.035f, 0.035f, 0.96f), new Color(0.03f, 0.92f, 0.55f, 0.98f), 2f);

            Rect map = new Rect(panel.x + 7f, panel.y + 7f, panel.width - 14f, panel.width - 14f);
            Color previous = GUI.color;
            GUI.color = new Color(0.025f, 0.18f, 0.09f, 1f);
            GUI.DrawTexture(map, Texture2D.whiteTexture);
            GUI.color = new Color(0.28f, 0.68f, 0.3f, 0.22f);
            GUI.DrawTexture(new Rect(map.center.x - 1f, map.y, 2f, map.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(map.x, map.center.y - 1f, map.width, 2f), Texture2D.whiteTexture);

            if (CentralLake.Instance != null)
            {
                DrawMinimapLake(CentralLake.Instance.transform.position,
                    CentralLake.Instance.Radius, CentralLake.Instance.Radius, 0f, map, jungle.MapSize);
            }

            if (SwampLake.Instance != null)
            {
                DrawMinimapLake(SwampLake.Instance.transform.position,
                    SwampLake.Instance.HalfLength, SwampLake.Instance.HalfWidth,
                    SwampLake.Instance.transform.eulerAngles.y, map, jungle.MapSize);
            }

            foreach (RangedAmmoPickup pickup in RangedAmmoPickup.ActivePickups)
            {
                if (pickup == null || !pickup.IsAvailable) continue;
                Vector2 point = WorldToMinimap(pickup.transform.position, map, jungle.MapSize);
                DrawMinimapPickupMarker(point, 4.5f, new Color(0.84f, 0.74f, 0.56f, 1f));
            }

            foreach (FoodPickup pickup in FoodPickup.ActivePickups)
            {
                if (pickup == null || !pickup.IsAvailable) continue;
                Vector2 point = WorldToMinimap(pickup.transform.position, map, jungle.MapSize);
                DrawMinimapPickupMarker(point, 4.5f, new Color(0.28f, 0.94f, 0.38f, 1f));
            }

            foreach (DiamondPickup diamond in DiamondPickup.ActivePickups)
            {
                if (diamond == null || !diamond.IsAvailable) continue;
                Vector2 point = WorldToMinimap(diamond.transform.position, map, jungle.MapSize);
                GUI.color = new Color(0.22f, 0.9f, 1f, 1f);
                GUI.DrawTexture(new Rect(point.x - 4f, point.y - 4f, 8f, 8f), RuntimeGuiTheme.CircleTexture);
            }

            if (DiamondObjectiveManager.Instance != null)
            {
                Vector2 portalPoint = WorldToMinimap(DiamondObjectiveManager.Instance.PortalPosition, map, jungle.MapSize);
                GUI.color = new Color(0.75f, 0.25f, 1f, 1f);
                GUI.DrawTexture(new Rect(portalPoint.x - 6f, portalPoint.y - 6f, 12f, 12f), RuntimeGuiTheme.RingTexture);
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
                GUI.DrawTexture(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), RuntimeGuiTheme.RingTexture);
            }

            foreach (ThirdPersonAnimalController fighter in fighters)
            {
                if (fighter == null || fighter.Health == null || fighter.Health.IsDead) continue;
                Vector2 point = WorldToMinimap(fighter.transform.position, map, jungle.MapSize);
                if (!map.Contains(point)) continue;
                if (fighter == LocalPlayer)
                {
                    Vector3 aimDirection = fighter.ViewAimDirection;
                    float heading = aimDirection.sqrMagnitude > 0.001f
                        ? Mathf.Repeat(Mathf.Atan2(aimDirection.x, aimDirection.z) * Mathf.Rad2Deg, 360f)
                        : fighter.transform.eulerAngles.y;
                    DrawMinimapPlayerArrow(point, heading);
                }
                else
                {
                    int fighterDiamonds = DiamondObjectiveManager.Instance != null
                        ? DiamondObjectiveManager.Instance.GetCount(fighter)
                        : 0;
                    if (fighterDiamonds <= 0) continue;
                    GUI.color = new Color(0.15f, 0.92f, 1f, 1f);
                    GUI.DrawTexture(new Rect(point.x - 7f, point.y - 7f, 14f, 14f), RuntimeGuiTheme.RingTexture);
                    GUI.color = new Color(1f, 0.28f, 0.16f, 1f);
                    GUI.DrawTexture(new Rect(point.x - 3.5f, point.y - 3.5f, 7f, 7f), RuntimeGuiTheme.CircleTexture);
                    GUI.color = Color.white;
                    float countX = point.x > map.xMax - 22f ? point.x - 23f : point.x + 5f;
                    float countY = Mathf.Clamp(point.y - 10f, map.y, map.yMax - 18f);
                    GUI.Label(new Rect(countX, countY, 18f, 18f), fighterDiamonds.ToString(), eyebrowStyle);
                }
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(map.center.x - 12f, map.y + 2f, 24f, 18f), "N", minimapStyle);

            float legendY = map.yMax + 4f;
            float legendSplit = panel.x + panel.width * 0.52f;
            DrawMinimapPickupMarker(new Vector2(panel.x + 12f, legendY + 8f), 5f,
                new Color(0.84f, 0.74f, 0.56f, 1f));
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 20f, legendY, legendSplit - panel.x - 20f, 16f),
                "MUNIÇÃO", eyebrowStyle);
            DrawMinimapPickupMarker(new Vector2(legendSplit + 5f, legendY + 8f), 5f,
                new Color(0.28f, 0.94f, 0.38f, 1f));
            GUI.color = Color.white;
            GUI.Label(new Rect(legendSplit + 13f, legendY, panel.xMax - legendSplit - 13f, 16f),
                "CURA", eyebrowStyle);
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
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(point.x - 7.5f, point.y - 9.5f, 15f, 19f), arrow);
            GUI.color = previous;
        }

        private static void DrawMinimapPickupMarker(Vector2 point, float size, Color color)
        {
            GUI.color = new Color(0.01f, 0.025f, 0.015f, 0.9f);
            float outlineSize = size + 2f;
            GUI.DrawTexture(new Rect(point.x - outlineSize * 0.5f,
                point.y - outlineSize * 0.5f, outlineSize, outlineSize),
                RuntimeGuiTheme.CircleTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f,
                size, size), RuntimeGuiTheme.CircleTexture);
        }

        private void DrawObjectiveStatus()
        {
            if (LocalPlayer == null) return;
            float minimapSize = Mathf.Clamp(viewHeight * 0.29f, 190f, 232f);
            float panelY = RightColumnTopY + minimapSize + 29f + MatchCountersStripHeight + MatchCountersStripGap;
            Rect panel = new Rect(viewWidth - minimapSize - 20f, panelY, minimapSize, 76f);
            SafeZoneController zone = SafeZoneController.Instance;
            bool outside = zone != null && zone.IsOutside(LocalPlayer.transform.position);
            DrawCartoonPanel(panel,
                outside ? new Color(0.24f, 0.035f, 0.03f, 0.96f) : new Color(0.022f, 0.042f, 0.044f, 0.94f),
                outside ? new Color(1f, 0.24f, 0.18f, 1f) : new Color(0.03f, 0.78f, 0.47f, 1f), 1f);

            GUI.Label(new Rect(panel.x + 12f, panel.y + 7f, panel.width - 24f, 20f), "ÁREA SEGURA", centeredStyle);

            string zoneText = "ÁREA SEGURA INDISPONÍVEL";
            if (zone != null)
            {
                zoneText = outside
                    ? $"FORA DA ÁREA — VIDA ACABA EM {zone.GetWildfireSecondsRemaining(LocalPlayer):0.0}s"
                    : zone.TimeUntilShrink > 0f
                        ? $"PODERÁ DIMINUIR EM  {FormatCountdown(zone.TimeUntilShrink)}"
                        : $"RAIO ATUAL  {zone.CurrentRadius:0} m";
            }
            GUI.Label(new Rect(panel.x + 10f, panel.y + 34f, panel.width - 20f, 30f), zoneText, centeredStyle);
        }

        private static string FormatCountdown(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
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

        private void DrawRespawnCountdown()
        {
            int seconds = Mathf.Max(1, Mathf.CeilToInt(RespawnSecondsRemaining));
            float width = Mathf.Min(430f, viewWidth - 48f);
            Rect panel = new Rect((viewWidth - width) * 0.5f, viewHeight * 0.4f, width, 92f);
            DrawCartoonPanel(panel, new Color(0.07f, 0.012f, 0.015f, 0.96f),
                new Color(0.92f, 0.14f, 0.1f, 1f), 2f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 9f, panel.width - 36f, 30f),
                "VOCÊ MORREU", resultStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 42f, panel.width - 36f, 35f),
                $"RESPAWN EM {seconds}", centeredStyle);
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

        private static void DrawMinimapLake(Vector3 worldCenter, float halfLength, float halfWidth,
            float rotationDegrees, Rect map, float worldSize)
        {
            Vector2 point = WorldToMinimap(worldCenter, map, worldSize);
            float width = halfLength * 2f / worldSize * map.width;
            float height = halfWidth * 2f / worldSize * map.width;

            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            if (Mathf.Abs(rotationDegrees) > 0.01f) GUIUtility.RotateAroundPivot(rotationDegrees, point);

            GUI.color = new Color(0.16f, 0.52f, 0.95f, 0.5f);
            GUI.DrawTexture(new Rect(point.x - width * 0.5f, point.y - height * 0.5f, width, height), RuntimeGuiTheme.CircleTexture);
            GUI.color = new Color(0.35f, 0.82f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(point.x - width * 0.5f, point.y - height * 0.5f, width, height), RuntimeGuiTheme.RingTexture);

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
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

        // Sharp corners on purpose — contrasts with the round portrait frame in the
        // character-selection menu.
        private static void DrawSquareFrame(Rect rect, Color fill, Color border, float borderSize = 1f)
        {
            Color previous = GUI.color;
            GUI.color = border;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x + borderSize, rect.y + borderSize,
                rect.width - borderSize * 2f, rect.height - borderSize * 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null && normalStyle != null && resultStyle != null && minimapStyle != null
                && eyebrowStyle != null && smallStyle != null && rightStyle != null && centeredStyle != null
                && compassMajorStyle != null && compassMinorStyle != null && counterStyle != null
                && weaponLockStyle != null && abilityCountdownStyle != null
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
            weaponLockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.96f, 0.96f, 0.96f) }
            };
            abilityCountdownStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            compassMajorStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            compassMinorStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.84f, 0.9f, 0.94f) }
            };
            counterStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            minimapArrowTextures = CreateMinimapArrowTextures();
            RuntimeGuiTheme.Ensure();
        }
    }
}
