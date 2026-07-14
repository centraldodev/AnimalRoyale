using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    public enum GlobalForestMission { AncientTotems, PortalRunes, ForestSanctuary, Territory }
    public enum ForestEventType { FruitRain, DenseFog, WildfireSurge, PortalPulse, RareBloom, LakeGuardian }
    public enum MissionNodeKind { Totem, Rune, Offering, Territory, AntSeal, TigerMark, EaglePerch, Lore }

    /// <summary>Randomizes match missions, animal objectives, forest events, lore and mastery rewards.</summary>
    public sealed class ForestMissionDirector : MonoBehaviour
    {
        public static ForestMissionDirector Instance { get; private set; }

        private readonly List<MissionNode> missionNodes = new List<MissionNode>();
        private JungleGenerator jungle;
        private ThirdPersonAnimalController localPlayer;
        private GlobalForestMission globalMission;
        private int globalProgress;
        private int globalGoal;
        private int animalProgress;
        private int animalGoal;
        private bool globalComplete;
        private bool animalComplete;
        private bool matchActive;
        private bool summaryRecorded;
        private bool monkeyWasHanging;
        private float territoryProgress;
        private ThirdPersonAnimalController territoryHolder;
        private float nextCarrierPulse;
        private float carrierRevealUntil;
        private float diamondRevealUntil;
        private float bridgeUntil;
        private GameObject bridgeVisual;
        private float nextEventTime;
        private float eventUntil;
        private ForestEventType activeEvent;
        private string eventMessage = string.Empty;
        private float originalFogDensity;
        private Color originalFogColor;
        private LakeGuardian activeGuardian;
        private float matchStartedAt;
        private int localEliminations;
        private int localDiamondPeak;
        private int localMissionsCompleted;
        private int awardedMastery;
        private float matchDuration;
        private CarrierRevealMarker carrierMarker;
        private bool localUsedMeat;

        public bool RevealDiamonds => Time.time < diamondRevealUntil;
        public bool RevealCarrier => Time.time < carrierRevealUntil;
        public bool MinimapJammed => matchActive && activeEvent == ForestEventType.DenseFog && Time.time < eventUntil;
        public bool LakePassageOpen => Time.time < bridgeUntil;
        public string EventMessage => Time.time < eventUntil ? eventMessage : string.Empty;
        public string GlobalMissionText => GlobalMissionDescription();
        public string AnimalMissionText => AnimalMissionDescription();
        public int AwardedMastery => awardedMastery;
        public string MatchSummary => $"Diamantes: {localDiamondPeak}/10   •   Eliminações: {localEliminations}\nMissões: {localMissionsCompleted}/2   •   Sobrevivência: {matchDuration:0}s\nMaestria recebida: +{awardedMastery}";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            originalFogDensity = RenderSettings.fogDensity;
            originalFogColor = RenderSettings.fogColor;
        }

        public void Initialize(JungleGenerator generatedJungle)
        {
            jungle = generatedJungle;
            SpawnLoreFragments(6);
        }

        public void BeginMatch(ThirdPersonAnimalController player)
        {
            localPlayer = player;
            matchActive = true;
            matchStartedAt = Time.time;
            nextCarrierPulse = Time.time + 22f;
            nextEventTime = Time.time + 38f;
            globalMission = (GlobalForestMission)UnityEngine.Random.Range(0, 4);
            SpawnGlobalMission();
            SpawnAnimalMission();
            ForestProgression.ApplyCosmetic(player);
        }

        private void Update()
        {
            if (!matchActive || localPlayer == null) return;
            if (DiamondObjectiveManager.Instance != null)
            {
                localDiamondPeak = Mathf.Max(localDiamondPeak, DiamondObjectiveManager.Instance.GetCount(localPlayer));
            }

            UpdateAnimalMission();
            UpdateTerritoryMission();
            UpdateCarrierPulse();
            UpdateCarrierMarker();
            UpdateEvents();

            if (bridgeVisual != null && !LakePassageOpen) bridgeVisual.SetActive(false);
            if (Time.time >= eventUntil && activeEvent == ForestEventType.DenseFog) RestoreFog();
        }

        public bool IsCarrierRevealed(ThirdPersonAnimalController fighter)
        {
            if (!RevealCarrier || fighter == null || DiamondObjectiveManager.Instance == null) return false;
            return fighter == GetLeadingCarrier();
        }

        public void ActivateNode(ThirdPersonAnimalController fighter, MissionNode node)
        {
            if (fighter == null || node == null || node.IsActivated) return;
            if (node.Kind == MissionNodeKind.Lore)
            {
                if (fighter != localPlayer) return;
                node.MarkActivated();
                int memory = ForestProgression.UnlockLore();
                eventMessage = $"MEMÓRIA {memory}/12 — {ForestProgression.GetLoreText(memory)}";
                eventUntil = Time.time + 5f;
                return;
            }

            if (IsGlobalNode(node.Kind) && !globalComplete)
            {
                node.MarkActivated();
                globalProgress++;
                if (globalProgress >= globalGoal) CompleteGlobalMission(fighter);
                return;
            }

            if (fighter == localPlayer && IsAnimalNode(node.Kind) && !animalComplete)
            {
                node.MarkActivated();
                animalProgress++;
                if (animalProgress >= animalGoal) CompleteAnimalMission();
            }
        }

        public void NotifyTigerAttack(ThirdPersonAnimalController fighter)
        {
            if (fighter == localPlayer && fighter.AnimalType == AnimalType.Tiger && !animalComplete)
            {
                MissionNode.TryStrikeNearest(fighter);
            }
        }

        public void RecordElimination(ThirdPersonAnimalController attacker)
        {
            if (attacker == localPlayer) localEliminations++;
        }

        public void RecordFoodConsumed(ThirdPersonAnimalController fighter, FoodKind kind)
        {
            if (fighter == localPlayer && kind == FoodKind.Meat) localUsedMeat = true;
        }

        public void FinishMatch(bool localWon)
        {
            if (summaryRecorded || localPlayer == null) return;
            summaryRecorded = true;
            matchActive = false;
            matchDuration = Mathf.Max(0f, Time.time - matchStartedAt);
            RestoreFog();
            int survival = Mathf.RoundToInt(Mathf.Max(0f, Time.time - matchStartedAt) / 10f);
            bool contractComplete = (DateTime.UtcNow.DayOfYear % 3) switch
            {
                0 => localMissionsCompleted >= 2,
                1 => localDiamondPeak >= 5,
                _ => localEliminations >= 2
            };
            awardedMastery = localMissionsCompleted * 35 + localDiamondPeak * 8 + localEliminations * 25 + survival
                              + (localWon ? 120 : 0) + (contractComplete ? 75 : 0);
            ForestProgression.AddMastery(localPlayer.AnimalType, awardedMastery);
            ForestProgression.CheckAchievements(localWon, !localUsedMeat, localPlayer.Health.CurrentHealth);
        }

        private void SpawnGlobalMission()
        {
            switch (globalMission)
            {
                case GlobalForestMission.AncientTotems:
                    globalGoal = 3;
                    SpawnNodes(MissionNodeKind.Totem, globalGoal, 25f);
                    break;
                case GlobalForestMission.PortalRunes:
                    globalGoal = 2;
                    SpawnNodes(MissionNodeKind.Rune, globalGoal, 20f);
                    break;
                case GlobalForestMission.ForestSanctuary:
                    globalGoal = 3;
                    SpawnNodes(MissionNodeKind.Offering, globalGoal, 22f);
                    break;
                case GlobalForestMission.Territory:
                    globalGoal = 1;
                    SpawnNodes(MissionNodeKind.Territory, 1, 34f);
                    break;
            }
        }

        private void SpawnAnimalMission()
        {
            if (localPlayer == null) return;
            switch (localPlayer.AnimalType)
            {
                case AnimalType.Ant:
                    animalGoal = 3;
                    SpawnNodes(MissionNodeKind.AntSeal, animalGoal, 18f);
                    break;
                case AnimalType.Monkey:
                    animalGoal = 4;
                    break;
                case AnimalType.Tiger:
                    animalGoal = 3;
                    SpawnNodes(MissionNodeKind.TigerMark, animalGoal, 20f);
                    break;
                case AnimalType.Eagle:
                    animalGoal = 3;
                    SpawnNodes(MissionNodeKind.EaglePerch, animalGoal, 38f);
                    break;
            }
        }

        private void SpawnNodes(MissionNodeKind kind, int count, float centerClearance)
        {
            for (int i = 0; i < count; i++)
            {
                MissionNode node = MissionNode.Create(jungle.GetMissionSpawnPosition(centerClearance), kind);
                missionNodes.Add(node);
            }
        }

        private void SpawnLoreFragments(int count)
        {
            for (int i = 0; i < count; i++) MissionNode.Create(jungle.GetMissionSpawnPosition(16f), MissionNodeKind.Lore);
        }

        private void UpdateAnimalMission()
        {
            if (animalComplete || localPlayer.Health.IsDead) return;
            if (localPlayer.AnimalType == AnimalType.Monkey)
            {
                bool hanging = localPlayer.IsHangingVine;
                if (hanging && !monkeyWasHanging)
                {
                    animalProgress++;
                    if (animalProgress >= animalGoal) CompleteAnimalMission();
                }
                monkeyWasHanging = hanging;
                return;
            }

            MissionNodeKind expected = localPlayer.AnimalType == AnimalType.Ant ? MissionNodeKind.AntSeal : MissionNodeKind.EaglePerch;
            if (localPlayer.AnimalType != AnimalType.Ant && localPlayer.AnimalType != AnimalType.Eagle) return;
            foreach (MissionNode node in missionNodes)
            {
                if (node == null || node.IsActivated || node.Kind != expected) continue;
                Vector3 offset = node.transform.position - localPlayer.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < 3.2f * 3.2f && (expected != MissionNodeKind.EaglePerch || !localPlayer.IsFlying))
                {
                    ActivateNode(localPlayer, node);
                    break;
                }
            }
        }

        private void UpdateTerritoryMission()
        {
            if (globalComplete || globalMission != GlobalForestMission.Territory) return;
            MissionNode territory = missionNodes.Find(node => node != null && node.Kind == MissionNodeKind.Territory);
            if (territory == null) return;
            ThirdPersonAnimalController occupant = null;
            float closest = 5.2f * 5.2f;
            if (BattleRoyaleManager.Instance != null)
            {
                foreach (ThirdPersonAnimalController fighter in BattleRoyaleManager.Instance.Fighters)
                {
                    if (fighter == null || fighter.Health.IsDead) continue;
                    Vector3 offset = fighter.transform.position - territory.transform.position;
                    offset.y = 0f;
                    if (offset.sqrMagnitude < closest) { closest = offset.sqrMagnitude; occupant = fighter; }
                }
            }
            if (occupant == null) { territoryHolder = null; territoryProgress = 0f; return; }
            if (territoryHolder != occupant) { territoryHolder = occupant; territoryProgress = 0f; }
            territoryProgress += Time.deltaTime;
            if (territoryProgress >= 10f) { territory.MarkActivated(); globalProgress = 1; CompleteGlobalMission(occupant); }
        }

        private void CompleteGlobalMission(ThirdPersonAnimalController contributor)
        {
            globalComplete = true;
            if (contributor == localPlayer) localMissionsCompleted++;
            switch (globalMission)
            {
                case GlobalForestMission.AncientTotems:
                    diamondRevealUntil = Time.time + 32f;
                    eventMessage = "TOTENS DESPERTOS — diamantes revelados no minimapa";
                    break;
                case GlobalForestMission.PortalRunes:
                    bridgeUntil = Time.time + 55f;
                    CreateLakePassage();
                    eventMessage = "RUNAS ATIVADAS — passagem rápida sobre o lago";
                    break;
                case GlobalForestMission.ForestSanctuary:
                    contributor.Health.Heal(contributor.Health.MaxHealth);
                    contributor.RestoreMobilityEnergy(100f);
                    eventMessage = "SANTUÁRIO RESTAURADO — vida e energia recuperadas";
                    break;
                case GlobalForestMission.Territory:
                    carrierRevealUntil = Time.time + 28f;
                    contributor.RestoreMobilityEnergy(100f);
                    eventMessage = "CLAREIRA DOMINADA — maior portador revelado";
                    break;
            }
            eventUntil = Time.time + 7f;
        }

        private void CompleteAnimalMission()
        {
            animalComplete = true;
            localMissionsCompleted++;
            localPlayer.RestoreMobilityEnergy(100f);
            switch (localPlayer.AnimalType)
            {
                case AnimalType.Ant: diamondRevealUntil = Time.time + 24f; break;
                case AnimalType.Monkey: diamondRevealUntil = Time.time + 18f; break;
                case AnimalType.Tiger: carrierRevealUntil = Time.time + 35f; break;
                case AnimalType.Eagle: diamondRevealUntil = Time.time + 30f; carrierRevealUntil = Time.time + 20f; break;
            }
            eventMessage = $"MISSÃO DO {localPlayer.Stats.DisplayName.ToUpperInvariant()} CONCLUÍDA";
            eventUntil = Time.time + 7f;
        }

        private void UpdateCarrierPulse()
        {
            if (Time.time < nextCarrierPulse) return;
            nextCarrierPulse = Time.time + 24f;
            if (GetLeadingCarrier() == null) return;
            carrierRevealUntil = Mathf.Max(carrierRevealUntil, Time.time + 6f);
            eventMessage = "A FLORESTA REVELOU O MAIOR PORTADOR DE DIAMANTES";
            eventUntil = Mathf.Max(eventUntil, Time.time + 5f);
        }

        private ThirdPersonAnimalController GetLeadingCarrier()
        {
            if (BattleRoyaleManager.Instance == null || DiamondObjectiveManager.Instance == null) return null;
            ThirdPersonAnimalController leader = null;
            int best = 0;
            foreach (ThirdPersonAnimalController fighter in BattleRoyaleManager.Instance.Fighters)
            {
                if (fighter == null || fighter.Health.IsDead) continue;
                int count = DiamondObjectiveManager.Instance.GetCount(fighter);
                if (count > best) { best = count; leader = fighter; }
            }
            return leader;
        }

        private void UpdateCarrierMarker()
        {
            ThirdPersonAnimalController leader = RevealCarrier ? GetLeadingCarrier() : null;
            if (carrierMarker != null && (leader == null || carrierMarker.Target != leader))
            {
                Destroy(carrierMarker.gameObject);
                carrierMarker = null;
            }
            if (leader != null && carrierMarker == null) carrierMarker = CarrierRevealMarker.Create(leader);
        }

        private void UpdateEvents()
        {
            if (Time.time < nextEventTime) return;
            nextEventTime = Time.time + UnityEngine.Random.Range(58f, 76f);
            StartEvent((ForestEventType)UnityEngine.Random.Range(0, 6));
        }

        private void StartEvent(ForestEventType forestEvent)
        {
            activeEvent = forestEvent;
            eventUntil = Time.time + 24f;
            switch (forestEvent)
            {
                case ForestEventType.FruitRain:
                    for (int i = 0; i < 18; i++) FoodPickup.Create(FindSafeEventPosition(), FoodKind.Fruit);
                    eventMessage = "EVENTO: CHUVA DE FRUTAS — alimentos extras surgiram";
                    break;
                case ForestEventType.DenseFog:
                    RenderSettings.fogDensity = 0.018f;
                    RenderSettings.fogColor = new Color(0.48f, 0.58f, 0.52f);
                    eventMessage = "EVENTO: NÉVOA DENSA — visibilidade reduzida";
                    break;
                case ForestEventType.WildfireSurge:
                    SafeZoneController.Instance?.AccelerateShrink(24f);
                    eventMessage = "EVENTO: QUEIMADA INTENSA — o fogo avança mais rápido";
                    break;
                case ForestEventType.PortalPulse:
                    carrierRevealUntil = Time.time + 24f;
                    eventMessage = "EVENTO: PULSO DO PORTAL — maior portador revelado";
                    break;
                case ForestEventType.RareBloom:
                    for (int i = 0; i < 6; i++) FoodPickup.Create(FindSafeEventPosition(), FoodKind.GoldenFruit);
                    eventMessage = "EVENTO: FLORESCIMENTO DOURADO — frutas raras apareceram";
                    break;
                case ForestEventType.LakeGuardian:
                    if (activeGuardian != null) Destroy(activeGuardian.gameObject);
                    activeGuardian = LakeGuardian.Create(jungle.LakeSurfaceHeight);
                    eventMessage = "EVENTO: GUARDIÃO DO LAGO — o lago ficou perigoso";
                    break;
            }
        }

        private Vector3 FindSafeEventPosition()
        {
            SafeZoneController zone = SafeZoneController.Instance;
            float radius = zone != null ? Mathf.Max(8f, zone.CurrentRadius - 5f) : jungle.MapSize * 0.4f;
            Vector2 point = UnityEngine.Random.insideUnitCircle * radius;
            return jungle.GetGroundPosition((zone != null ? zone.Center : Vector3.zero) + new Vector3(point.x, 0f, point.y));
        }

        private void RestoreFog()
        {
            RenderSettings.fogDensity = originalFogDensity;
            RenderSettings.fogColor = originalFogColor;
        }

        private void CreateLakePassage()
        {
            if (bridgeVisual == null)
            {
                bridgeVisual = new GameObject("TemporaryRuneLakePassage");
                Material material = MissionNode.CreateMaterial(new Color(0.2f, 0.75f, 1f), true);
                for (int i = -7; i <= 7; i++)
                {
                    GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    stone.name = "GlowingStep";
                    stone.transform.SetParent(bridgeVisual.transform, false);
                    stone.transform.position = new Vector3(Mathf.Sin(i * 0.7f) * 1.2f, jungle.LakeSurfaceHeight + 0.08f, i * jungle.LakeRadius / 7f);
                    stone.transform.localScale = new Vector3(1.7f, 0.12f, 1.25f);
                    stone.GetComponent<Renderer>().sharedMaterial = material;
                    Collider collider = stone.GetComponent<Collider>(); if (collider != null) collider.enabled = false;
                }
            }
            bridgeVisual.SetActive(true);
        }

        private string GlobalMissionDescription()
        {
            if (globalComplete) return "✓ Missão global concluída";
            return globalMission switch
            {
                GlobalForestMission.AncientTotems => $"TOTENS ANTIGOS  {globalProgress}/{globalGoal} — use F",
                GlobalForestMission.PortalRunes => $"RUNAS DO PORTAL  {globalProgress}/{globalGoal} — use F",
                GlobalForestMission.ForestSanctuary => $"OFERENDAS DO SANTUÁRIO  {globalProgress}/{globalGoal} — use F",
                GlobalForestMission.Territory => $"DOMINE A CLAREIRA  {territoryProgress:0}/10s",
                _ => string.Empty
            };
        }

        private string AnimalMissionDescription()
        {
            if (localPlayer == null) return string.Empty;
            if (animalComplete) return "✓ Missão do animal concluída";
            return localPlayer.AnimalType switch
            {
                AnimalType.Ant => $"FORMIGA: visite selos de túnel  {animalProgress}/{animalGoal}",
                AnimalType.Monkey => $"MACACO: agarre cipós  {animalProgress}/{animalGoal}",
                AnimalType.Tiger => $"TIGRE: ataque marcas de garras  {animalProgress}/{animalGoal}",
                AnimalType.Eagle => $"ÁGUIA: pouse nos poleiros  {animalProgress}/{animalGoal}",
                _ => string.Empty
            };
        }

        private bool IsGlobalNode(MissionNodeKind kind)
        {
            return (globalMission == GlobalForestMission.AncientTotems && kind == MissionNodeKind.Totem)
                   || (globalMission == GlobalForestMission.PortalRunes && kind == MissionNodeKind.Rune)
                   || (globalMission == GlobalForestMission.ForestSanctuary && kind == MissionNodeKind.Offering);
        }

        private static bool IsAnimalNode(MissionNodeKind kind)
        {
            return kind == MissionNodeKind.AntSeal || kind == MissionNodeKind.TigerMark || kind == MissionNodeKind.EaglePerch;
        }

        private void OnDestroy()
        {
            RestoreFog();
            if (Instance == this) Instance = null;
        }
    }

    public sealed class MissionNode : MonoBehaviour
    {
        private static readonly List<MissionNode> activeNodes = new List<MissionNode>();
        private Vector3 basePosition;
        private TextMesh label;
        public MissionNodeKind Kind { get; private set; }
        public bool IsActivated { get; private set; }
        public static IReadOnlyList<MissionNode> ActiveNodes => activeNodes;

        public static MissionNode Create(Vector3 position, MissionNodeKind kind)
        {
            GameObject root = new GameObject("Mission_" + kind);
            root.transform.position = position + Vector3.up * 0.35f;
            MissionNode node = root.AddComponent<MissionNode>();
            node.Kind = kind;
            node.basePosition = root.transform.position;
            node.BuildVisual();
            return node;
        }

        private void OnEnable() { if (!activeNodes.Contains(this)) activeNodes.Add(this); }
        private void OnDisable() { activeNodes.Remove(this); }

        public static bool TryUseNearest(ThirdPersonAnimalController fighter)
        {
            MissionNode closest = null; float distance = 2.8f * 2.8f;
            foreach (MissionNode node in activeNodes)
            {
                if (node == null || node.IsActivated || !node.IsInteractive) continue;
                float sqr = (node.transform.position - fighter.transform.position).sqrMagnitude;
                if (sqr < distance) { distance = sqr; closest = node; }
            }
            if (closest == null || ForestMissionDirector.Instance == null) return false;
            ForestMissionDirector.Instance.ActivateNode(fighter, closest);
            return closest.IsActivated;
        }

        public static bool TryStrikeNearest(ThirdPersonAnimalController fighter)
        {
            foreach (MissionNode node in activeNodes)
            {
                if (node == null || node.IsActivated || node.Kind != MissionNodeKind.TigerMark) continue;
                if ((node.transform.position - fighter.transform.position).sqrMagnitude > 3.5f * 3.5f) continue;
                ForestMissionDirector.Instance?.ActivateNode(fighter, node);
                return node.IsActivated;
            }
            return false;
        }

        private bool IsInteractive => Kind == MissionNodeKind.Totem || Kind == MissionNodeKind.Rune
                                      || Kind == MissionNodeKind.Offering || Kind == MissionNodeKind.Lore;

        public void MarkActivated()
        {
            if (IsActivated) return;
            IsActivated = true;
            AttackVfx.CreateBurst(transform.position + Vector3.up, ColorFor(Kind), 2.3f);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsActivated) return;
            transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 1.8f + basePosition.x) * 0.09f);
            transform.Rotate(0f, 18f * Time.deltaTime, 0f, Space.World);
        }

        private void BuildVisual()
        {
            Color color = ColorFor(Kind);
            Material material = CreateMaterial(color, true);
            PrimitiveType primitive = Kind == MissionNodeKind.Territory ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            GameObject symbol = GameObject.CreatePrimitive(primitive);
            symbol.name = "MissionSymbol";
            symbol.transform.SetParent(transform, false);
            symbol.transform.localPosition = Vector3.up * (Kind == MissionNodeKind.EaglePerch ? 2.2f : 0.75f);
            symbol.transform.localScale = ScaleFor(Kind);
            symbol.transform.localRotation = Quaternion.Euler(Kind == MissionNodeKind.Rune ? 45f : 0f, 25f, Kind == MissionNodeKind.Rune ? 45f : 0f);
            symbol.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = symbol.GetComponent<Collider>(); if (collider != null) collider.enabled = false;

            if (Kind == MissionNodeKind.Territory)
            {
                symbol.transform.localScale = new Vector3(5.2f, 0.04f, 5.2f);
            }

            GameObject labelObject = new GameObject("MissionLabel");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = Vector3.up * (Kind == MissionNodeKind.EaglePerch ? 4.2f : 2f);
            label = labelObject.AddComponent<TextMesh>();
            label.text = LabelFor(Kind);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.045f;
            label.fontSize = 48;
            label.color = color;
            labelObject.AddComponent<PickupLabel>();
        }

        public static Material CreateMaterial(Color color, bool emissive)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { color = color, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (emissive && material.HasProperty("_EmissionColor")) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 1.5f); }
            return material;
        }

        private static Color ColorFor(MissionNodeKind kind) => kind switch
        {
            MissionNodeKind.Totem => new Color(0.2f, 1f, 0.45f), MissionNodeKind.Rune => new Color(0.18f, 0.78f, 1f),
            MissionNodeKind.Offering => new Color(1f, 0.72f, 0.12f), MissionNodeKind.Territory => new Color(0.72f, 0.2f, 1f),
            MissionNodeKind.AntSeal => new Color(1f, 0.32f, 0.1f), MissionNodeKind.TigerMark => new Color(1f, 0.48f, 0.06f),
            MissionNodeKind.EaglePerch => new Color(0.76f, 0.94f, 1f), MissionNodeKind.Lore => new Color(0.85f, 0.38f, 1f), _ => Color.white
        };

        private static Vector3 ScaleFor(MissionNodeKind kind) => kind switch
        {
            MissionNodeKind.Totem => new Vector3(0.65f, 1.8f, 0.65f), MissionNodeKind.Rune => Vector3.one * 0.85f,
            MissionNodeKind.Offering => new Vector3(1f, 0.35f, 1f), MissionNodeKind.AntSeal => new Vector3(1.2f, 0.15f, 1.2f),
            MissionNodeKind.TigerMark => new Vector3(0.18f, 1.35f, 1f), MissionNodeKind.EaglePerch => new Vector3(1.7f, 0.18f, 1.7f),
            MissionNodeKind.Lore => new Vector3(0.65f, 0.85f, 0.18f), _ => Vector3.one
        };

        private static string LabelFor(MissionNodeKind kind) => kind switch
        {
            MissionNodeKind.Totem => "F  ATIVAR TOTEM", MissionNodeKind.Rune => "F  ATIVAR RUNA", MissionNodeKind.Offering => "F  RECOLHER OFERENDA",
            MissionNodeKind.Territory => "CLAREIRA SAGRADA", MissionNodeKind.AntSeal => "SELO DE TÚNEL", MissionNodeKind.TigerMark => "ATAQUE A MARCA",
            MissionNodeKind.EaglePerch => "POUSE NO POLEIRO", MissionNodeKind.Lore => "F  MEMÓRIA DA FLORESTA", _ => string.Empty
        };
    }

    public static class ForestProgression
    {
        public static int LoreCount => PlayerPrefs.GetInt("ForestLore", 0);
        public static int GetMastery(AnimalType type) => PlayerPrefs.GetInt("Mastery_" + type, 0);
        public static int GetLevel(AnimalType type) => 1 + GetMastery(type) / 250;
        public static string GetCosmeticName(AnimalType type) => GetLevel(type) switch { >= 5 => "Aura Lendária", >= 3 => "Aura da Floresta", >= 2 => "Aura de Explorador", _ => "Natural" };
        public static string DailyContract => (DateTime.UtcNow.DayOfYear % 3) switch
        {
            0 => "CONTRATO: conclua as duas missões da partida",
            1 => "CONTRATO: carregue pelo menos 5 diamantes",
            _ => "CONTRATO: consiga duas eliminações"
        };

        public static void AddMastery(AnimalType type, int amount)
        {
            PlayerPrefs.SetInt("Mastery_" + type, GetMastery(type) + Mathf.Max(0, amount));
            PlayerPrefs.Save();
        }

        public static int UnlockLore()
        {
            int unlocked = Mathf.Min(12, LoreCount + 1);
            PlayerPrefs.SetInt("ForestLore", unlocked);
            PlayerPrefs.Save();
            return unlocked;
        }

        public static int AchievementCount => PlayerPrefs.GetInt("Achievement_NoMeat", 0) + PlayerPrefs.GetInt("Achievement_LastBreath", 0);
        public static void CheckAchievements(bool won, bool avoidedMeat, float currentHealth)
        {
            if (!won) return;
            if (avoidedMeat) PlayerPrefs.SetInt("Achievement_NoMeat", 1);
            if (currentHealth < 30f) PlayerPrefs.SetInt("Achievement_LastBreath", 1);
            PlayerPrefs.Save();
        }

        public static string GetLoreText(int memory) => memory switch
        {
            1 => "A grande queimada começou quando o coração da floresta se partiu.",
            2 => "Dez pedras guardavam a passagem para além das montanhas.",
            3 => "Os antigos animais prometeram que apenas um cruzaria o portal.",
            4 => "As formigas abriram túneis para proteger as últimas sementes.",
            5 => "Os macacos esconderam runas entre os cipós mais altos.",
            6 => "Os tigres marcaram caminhos que somente caçadores conseguem ler.",
            7 => "As águias viram o incêndio nascer atrás dos picos.",
            8 => "O guardião do lago protege a saída, não os diamantes.",
            9 => "Cada cristal carrega uma lembrança de quem não escapou.",
            10 => "A floresta muda seus caminhos para testar novos sobreviventes.",
            11 => "O portal não escolhe o mais forte, apenas quem reúne a chave.",
            12 => "Escapar é vencer; salvar a floresta será a próxima jornada.",
            _ => "Uma lembrança antiga desperta entre as árvores."
        };

        public static void ApplyCosmetic(ThirdPersonAnimalController fighter)
        {
            if (fighter == null || GetLevel(fighter.AnimalType) < 2) return;
            fighter.gameObject.AddComponent<MasteryCosmetic>().Configure(GetLevel(fighter.AnimalType), fighter.AnimalType);
        }
    }

    public sealed class MasteryCosmetic : MonoBehaviour
    {
        private Transform orbit;
        public void Configure(int level, AnimalType animalType)
        {
            orbit = new GameObject("MasteryAura_" + level).transform; orbit.SetParent(transform, false); orbit.localPosition = Vector3.up * 0.25f;
            Color animalColor = AnimalDefinition.Get(animalType).MainColor;
            Color color = level >= 5 ? new Color(1f, 0.65f, 0.08f) : level >= 3 ? Color.Lerp(animalColor, Color.green, 0.42f) : Color.Lerp(animalColor, Color.cyan, 0.48f);
            Material material = MissionNode.CreateMaterial(color, true);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f; GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.transform.SetParent(orbit, false); orb.transform.localPosition = new Vector3(Mathf.Cos(angle) * 1.1f, 0f, Mathf.Sin(angle) * 1.1f);
                orb.transform.localScale = Vector3.one * 0.13f; orb.GetComponent<Renderer>().sharedMaterial = material;
                Collider collider = orb.GetComponent<Collider>(); if (collider != null) collider.enabled = false;
            }
        }
        private void Update() { if (orbit != null) orbit.Rotate(0f, 65f * Time.deltaTime, 0f); }
    }

    public sealed class LakeGuardian : MonoBehaviour
    {
        private float surfaceHeight; private float nextDamage; private float expiresAt;
        public static LakeGuardian Create(float lakeSurface)
        {
            GameObject root = new GameObject("LakeGuardian"); root.transform.position = new Vector3(0f, lakeSurface + 0.12f, 8f);
            LakeGuardian guardian = root.AddComponent<LakeGuardian>(); guardian.surfaceHeight = lakeSurface; guardian.expiresAt = Time.time + 32f; guardian.Build(); return guardian;
        }
        private void Update()
        {
            if (Time.time >= expiresAt) { Destroy(gameObject); return; }
            float angle = Time.time * 0.34f; transform.position = new Vector3(Mathf.Cos(angle) * 9f, surfaceHeight + 0.12f, Mathf.Sin(angle) * 9f);
            transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)));
            if (Time.time < nextDamage || BattleRoyaleManager.Instance == null) return;
            foreach (ThirdPersonAnimalController fighter in BattleRoyaleManager.Instance.Fighters)
            {
                if (fighter == null || fighter.Health.IsDead) continue;
                Vector3 offset = fighter.transform.position - transform.position; offset.y = 0f;
                if (offset.sqrMagnitude > 3.2f * 3.2f) continue;
                fighter.Health.TakeDamage(16f); AttackVfx.CreateBurst(fighter.transform.position, new Color(0.25f, 0.9f, 0.18f), 1.5f); nextDamage = Time.time + 1.3f; break;
            }
        }
        private void Build()
        {
            Material body = MissionNode.CreateMaterial(new Color(0.12f, 0.46f, 0.16f), false);
            CreatePart(PrimitiveType.Sphere, "GuardianBody", Vector3.zero, new Vector3(2.8f, 0.55f, 0.8f), body);
            CreatePart(PrimitiveType.Sphere, "GuardianHead", new Vector3(2.3f, 0.05f, 0f), new Vector3(1.35f, 0.48f, 0.75f), body);
            for (int i = 0; i < 5; i++) CreatePart(PrimitiveType.Cube, "BackSpike", new Vector3(-1.2f + i * 0.62f, 0.55f, 0f), new Vector3(0.28f, 0.45f, 0.22f), body);
        }
        private void CreatePart(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type); part.name = name; part.transform.SetParent(transform, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material; Collider collider = part.GetComponent<Collider>(); if (collider != null) collider.enabled = false;
        }
    }

    public sealed class CarrierRevealMarker : MonoBehaviour
    {
        public ThirdPersonAnimalController Target { get; private set; }
        private Transform symbol;
        public static CarrierRevealMarker Create(ThirdPersonAnimalController target)
        {
            GameObject root = new GameObject("RevealedDiamondCarrier");
            CarrierRevealMarker marker = root.AddComponent<CarrierRevealMarker>(); marker.Target = target;
            GameObject diamond = new GameObject("CarrierBeacon"); diamond.transform.SetParent(root.transform, false);
            diamond.AddComponent<MeshFilter>().sharedMesh = JungleGenerator.GetCrystalMesh();
            diamond.AddComponent<MeshRenderer>().sharedMaterial = MissionNode.CreateMaterial(new Color(1f, 0.18f, 0.12f), true);
            diamond.transform.localScale = Vector3.one * 0.55f; marker.symbol = diamond.transform; return marker;
        }
        private void Update()
        {
            if (Target == null || Target.Health.IsDead) { Destroy(gameObject); return; }
            transform.position = Target.transform.position + Vector3.up * (Target.Stats.ControllerHeight + 1.25f + Mathf.Sin(Time.time * 3f) * 0.16f);
            if (symbol != null) symbol.Rotate(25f * Time.deltaTime, 90f * Time.deltaTime, 0f, Space.World);
        }
    }
}
