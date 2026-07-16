using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public enum OnlineActionType : byte
    {
        RangedAttack,
        MeleeAttack,
        Ability,
        Consume
    }

    public readonly struct NetworkSpawnDefinition
    {
        public readonly int EntityId;
        public readonly ulong OwnerClientId;
        public readonly AnimalType AnimalType;
        public readonly bool IsBot;
        public readonly Vector3 Position;

        public NetworkSpawnDefinition(int entityId, ulong ownerClientId, AnimalType animalType, bool isBot, Vector3 position)
        {
            EntityId = entityId;
            OwnerClientId = ownerClientId;
            AnimalType = animalType;
            IsBot = isBot;
            Position = position;
        }
    }

    [DefaultExecutionOrder(-180)]
    public sealed class OnlineMultiplayerManager : MonoBehaviour
    {
        private const int TargetParticipants = 10;
        private const ushort LocalPort = 7777;
        private const string SelectionMessage = "ABR_SELECTION_V1";
        private const string StartMessage = "ABR_START_V1";
        private const string TransformMessage = "ABR_TRANSFORM_V1";
        private const string StateMessage = "ABR_STATE_V1";
        private const string ActionMessage = "ABR_ACTION_V1";
        private const float SendInterval = 0.05f;

        public static OnlineMultiplayerManager Instance { get; private set; }

        private readonly Dictionary<ulong, AnimalType> playerSelections = new Dictionary<ulong, AnimalType>();
        private readonly Dictionary<int, ThirdPersonAnimalController> entities = new Dictionary<int, ThirdPersonAnimalController>();
        private readonly Dictionary<int, ulong> entityOwners = new Dictionary<int, ulong>();
        private readonly Dictionary<ThirdPersonAnimalController, int> entityIds = new Dictionary<ThirdPersonAnimalController, int>();

        private GameBootstrap bootstrap;
        private NetworkManager networkManager;
        private UnityTransport transport;
        private ISession session;
        private AnimalType localSelection = AnimalType.Tiger;
        private ThirdPersonAnimalController localFighter;
        private bool handlersRegistered;
        private bool busy;
        private bool connected;
        private bool matchStarted;
        private float nextSendTime;
        private string joinCodeInput = string.Empty;
        private string directAddress = "127.0.0.1";
        private string status = "OFFLINE";
        private string visibleJoinCode = string.Empty;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle buttonStyle;
        private GUIStyle fieldStyle;

        public bool IsConnected => connected && networkManager != null && networkManager.IsListening;
        public bool IsHost => IsConnected && networkManager.IsHost;
        public bool IsClientOnly => IsConnected && networkManager.IsClient && !networkManager.IsHost;
        public bool MatchStarted => matchStarted;
        public bool UsesRemoteAuthority => matchStarted && IsClientOnly;
        public int ParticipantTarget => TargetParticipants;
        public int HumanPlayerCount => session != null ? session.PlayerCount
            : networkManager != null && networkManager.IsListening ? networkManager.ConnectedClientsIds.Count : 1;
        public int PlannedBotCount => Mathf.Max(0, TargetParticipants - HumanPlayerCount);
        public string Status => status;
        public string JoinCode => visibleJoinCode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureNetworkStack();
        }

        public void Initialize(GameBootstrap gameBootstrap)
        {
            bootstrap = gameBootstrap;
        }

        private void EnsureNetworkStack()
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                transport = networkManager.GetComponent<UnityTransport>();
                return;
            }

            GameObject networkObject = new GameObject("AnimalRoyaleNetworkManager");
            transport = networkObject.AddComponent<UnityTransport>();
            networkManager = networkObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = null,
                EnableSceneManagement = false,
                ForceSamePrefabs = false,
                TickRate = 30
            };
        }

        private void Update()
        {
            if (!matchStarted || !IsConnected || Time.unscaledTime < nextSendTime) return;
            nextSendTime = Time.unscaledTime + SendInterval;

            if (IsHost) SendAuthoritativeState();
            else SendLocalTransform();
        }

        public void SetLocalSelection(AnimalType type)
        {
            if (localSelection == type && (!IsConnected || playerSelections.ContainsKey(networkManager.LocalClientId))) return;
            localSelection = type;
            if (!IsConnected) return;

            playerSelections[networkManager.LocalClientId] = type;
            if (IsClientOnly) SendSelection(type);
        }

        public bool HandleStartRequest(AnimalType type)
        {
            if (!IsConnected) return false;
            SetLocalSelection(type);

            if (IsClientOnly)
            {
                SendSelection(type);
                status = "PRONTO — AGUARDANDO O HOST INICIAR";
                return true;
            }

            StartHostedMatch();
            return true;
        }

        public void ReportAction(OnlineActionType action, Vector3 direction)
        {
            if (!matchStarted || !IsClientOnly || localFighter == null) return;
            if (!entityIds.TryGetValue(localFighter, out int entityId)) return;

            using FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteValueSafe(entityId);
            writer.WriteValueSafe((byte)action);
            writer.WriteValueSafe(direction);
            networkManager.CustomMessagingManager.SendNamedMessage(ActionMessage, NetworkManager.ServerClientId, writer,
                NetworkDelivery.ReliableSequenced);
        }

        public void RegisterSpawnedFighter(NetworkSpawnDefinition definition, ThirdPersonAnimalController fighter)
        {
            if (fighter == null) return;
            entities[definition.EntityId] = fighter;
            entityOwners[definition.EntityId] = definition.OwnerClientId;
            entityIds[fighter] = definition.EntityId;
            if (definition.OwnerClientId == networkManager.LocalClientId && !definition.IsBot) localFighter = fighter;
        }

        public void ClearSpawnedFighters()
        {
            entities.Clear();
            entityOwners.Clear();
            entityIds.Clear();
            localFighter = null;
        }

        private async void CreateRelaySession()
        {
            if (busy || IsConnected) return;
            if (string.IsNullOrWhiteSpace(Application.cloudProjectId))
            {
                status = "VINCULE O PROJETO EM PROJECT SETTINGS > SERVICES";
                return;
            }

            busy = true;
            status = "CONECTANDO AOS SERVIÇOS UNITY...";
            try
            {
                await EnsureServicesReady();
                SessionOptions options = new SessionOptions
                {
                    Name = $"Animal Royale {DateTime.UtcNow:HHmm}",
                    MaxPlayers = TargetParticipants,
                    IsPrivate = true
                }.WithRelayNetwork();

                session = await MultiplayerService.Instance.CreateSessionAsync(options);
                visibleJoinCode = session.Code;
                connected = true;
                status = $"SALA CRIADA — CÓDIGO {visibleJoinCode}";
                AttachSessionEvents();
                RegisterNetworkHandlers();
                SetLocalSelection(localSelection);
            }
            catch (Exception exception)
            {
                status = $"ERRO AO CRIAR SALA: {ShortError(exception)}";
                Debug.LogException(exception);
            }
            finally
            {
                busy = false;
            }
        }

        private async void JoinRelaySession()
        {
            if (busy || IsConnected) return;
            if (string.IsNullOrWhiteSpace(Application.cloudProjectId))
            {
                status = "VINCULE O PROJETO EM PROJECT SETTINGS > SERVICES";
                return;
            }

            string code = joinCodeInput.Trim().ToUpperInvariant();
            if (code.Length < 4)
            {
                status = "DIGITE O CÓDIGO DA SALA";
                return;
            }

            busy = true;
            status = "ENTRANDO NA SALA...";
            try
            {
                await EnsureServicesReady();
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                visibleJoinCode = session.Code;
                connected = true;
                status = $"CONECTADO — SALA {visibleJoinCode}";
                AttachSessionEvents();
                RegisterNetworkHandlers();
                SetLocalSelection(localSelection);
            }
            catch (Exception exception)
            {
                status = $"ERRO AO ENTRAR: {ShortError(exception)}";
                Debug.LogException(exception);
            }
            finally
            {
                busy = false;
            }
        }

        private async Task EnsureServicesReady()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                InitializationOptions options = new InitializationOptions();
                options.SetProfile($"abr-{System.Diagnostics.Process.GetCurrentProcess().Id}");
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        private void StartLocalHost()
        {
            if (busy || IsConnected) return;
            transport.SetConnectionData("127.0.0.1", LocalPort, "0.0.0.0");
            if (!networkManager.StartHost())
            {
                status = "NÃO FOI POSSÍVEL INICIAR O HOST LOCAL";
                return;
            }

            connected = true;
            visibleJoinCode = $"LAN:{LocalPort}";
            status = $"HOST LOCAL ATIVO NA PORTA {LocalPort}";
            RegisterNetworkHandlers();
            SetLocalSelection(localSelection);
            Debug.Log($"[Multiplayer] Host local iniciado na porta {LocalPort}.");
        }

        private void StartLocalClient()
        {
            if (busy || IsConnected) return;
            string address = string.IsNullOrWhiteSpace(directAddress) ? "127.0.0.1" : directAddress.Trim();
            transport.SetConnectionData(address, LocalPort);
            if (!networkManager.StartClient())
            {
                status = "NÃO FOI POSSÍVEL INICIAR O CLIENTE LOCAL";
                return;
            }

            connected = true;
            visibleJoinCode = $"{address}:{LocalPort}";
            status = "CONECTANDO AO HOST LOCAL...";
            RegisterNetworkHandlers();
            Debug.Log($"[Multiplayer] Cliente tentando conectar a {address}:{LocalPort}.");
        }

        private void RegisterNetworkHandlers()
        {
            if (handlersRegistered || networkManager == null || networkManager.CustomMessagingManager == null) return;
            handlersRegistered = true;
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(SelectionMessage, ReceiveSelection);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(StartMessage, ReceiveStartMatch);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(TransformMessage, ReceiveClientTransform);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, ReceiveAuthoritativeState);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ActionMessage, ReceiveAction);
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnClientConnected(ulong clientId)
        {
            connected = true;
            status = IsHost
                ? $"JOGADORES HUMANOS: {networkManager.ConnectedClientsIds.Count}/{TargetParticipants}"
                : "CONECTADO AO HOST — ESCOLHA SEU ANIMAL";
            if (clientId == networkManager.LocalClientId && IsClientOnly) SendSelection(localSelection);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            playerSelections.Remove(clientId);
            if (IsHost && matchStarted && clientId != networkManager.LocalClientId)
            {
                ConvertDisconnectedPlayerToBot(clientId);
                status = $"JOGADOR DESCONECTOU — BOT ASSUMIU ({HumanPlayerCount} HUMANOS)";
            }
            if (clientId != networkManager.LocalClientId) return;
            connected = false;
            status = "DESCONECTADO DO HOST";
        }

        private void ConvertDisconnectedPlayerToBot(ulong clientId)
        {
            int entityId = int.MinValue;
            foreach (KeyValuePair<int, ulong> pair in entityOwners)
            {
                if (pair.Value != clientId) continue;
                entityId = pair.Key;
                break;
            }
            if (entityId == int.MinValue) return;
            if (!entities.TryGetValue(entityId, out ThirdPersonAnimalController fighter) || fighter == null) return;

            entityOwners[entityId] = ulong.MaxValue;
            fighter.SetNetworkProxy(false);
            SimpleBotAI bot = fighter.GetComponent<SimpleBotAI>();
            if (bot == null) bot = fighter.gameObject.AddComponent<SimpleBotAI>();
            bot.enabled = true;
        }

        private void AttachSessionEvents()
        {
            if (session == null) return;
            session.Changed += OnSessionChanged;
            session.RemovedFromSession += OnRemovedFromSession;
        }

        private void OnSessionChanged()
        {
            if (session == null) return;
            status = IsHost
                ? $"SALA {session.Code} — {session.PlayerCount}/{TargetParticipants} HUMANOS"
                : $"CONECTADO À SALA {session.Code}";
        }

        private void OnRemovedFromSession()
        {
            connected = false;
            status = "VOCÊ SAIU DA SALA";
        }

        private void SendSelection(AnimalType type)
        {
            if (!IsClientOnly || networkManager.CustomMessagingManager == null) return;
            using FastBufferWriter writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe((int)type);
            networkManager.CustomMessagingManager.SendNamedMessage(SelectionMessage, NetworkManager.ServerClientId, writer,
                NetworkDelivery.ReliableSequenced);
        }

        private void ReceiveSelection(ulong sender, FastBufferReader reader)
        {
            if (!IsHost) return;
            reader.ReadValueSafe(out int typeValue);
            playerSelections[sender] = (AnimalType)Mathf.Clamp(typeValue, 0, AnimalRoster.Count - 1);
        }

        private void StartHostedMatch()
        {
            if (!IsHost || matchStarted || bootstrap == null) return;

            List<ulong> humanClients = networkManager.ConnectedClientsIds
                .OrderBy(clientId => clientId)
                .Take(TargetParticipants)
                .ToList();
            if (!humanClients.Contains(networkManager.LocalClientId)) humanClients.Insert(0, networkManager.LocalClientId);
            playerSelections[networkManager.LocalClientId] = localSelection;

            int seed = unchecked(Environment.TickCount ^ DateTime.UtcNow.Millisecond << 16);
            bootstrap.PrepareNetworkWorld(seed);
            Vector3[] positions = bootstrap.CreateNetworkSpawnPositions(TargetParticipants);
            List<NetworkSpawnDefinition> roster = new List<NetworkSpawnDefinition>(TargetParticipants);

            int slot = 0;
            foreach (ulong clientId in humanClients)
            {
                AnimalType type = playerSelections.TryGetValue(clientId, out AnimalType chosen) ? chosen : AnimalType.Tiger;
                roster.Add(new NetworkSpawnDefinition(unchecked((int)clientId), clientId, type, false, positions[slot++]));
            }

            System.Random random = new System.Random(seed);
            while (slot < TargetParticipants)
            {
                AnimalType botType = (AnimalType)random.Next(0, AnimalRoster.Count);
                roster.Add(new NetworkSpawnDefinition(1000 + slot, ulong.MaxValue, botType, true, positions[slot]));
                slot++;
            }

            SendStartMatch(seed, roster);
            Debug.Log($"[Multiplayer] Partida iniciada com {humanClients.Count} humano(s) e "
                      + $"{TargetParticipants - humanClients.Count} bot(s). Seed: {seed}.");
            ApplyStartMatch(seed, roster, true);
        }

        private void SendStartMatch(int seed, IReadOnlyList<NetworkSpawnDefinition> roster)
        {
            using FastBufferWriter writer = new FastBufferWriter(2048, Allocator.Temp);
            writer.WriteValueSafe(seed);
            writer.WriteValueSafe(roster.Count);
            foreach (NetworkSpawnDefinition definition in roster)
            {
                writer.WriteValueSafe(definition.EntityId);
                writer.WriteValueSafe(definition.OwnerClientId);
                writer.WriteValueSafe((int)definition.AnimalType);
                writer.WriteValueSafe(definition.IsBot);
                writer.WriteValueSafe(definition.Position);
            }

            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId == networkManager.LocalClientId) continue;
                networkManager.CustomMessagingManager.SendNamedMessage(StartMessage, clientId, writer,
                    NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private void ReceiveStartMatch(ulong sender, FastBufferReader reader)
        {
            if (IsHost || sender != NetworkManager.ServerClientId || matchStarted) return;
            reader.ReadValueSafe(out int seed);
            reader.ReadValueSafe(out int count);
            List<NetworkSpawnDefinition> roster = new List<NetworkSpawnDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int entityId);
                reader.ReadValueSafe(out ulong ownerClientId);
                reader.ReadValueSafe(out int animalType);
                reader.ReadValueSafe(out bool isBot);
                reader.ReadValueSafe(out Vector3 position);
                roster.Add(new NetworkSpawnDefinition(entityId, ownerClientId,
                    (AnimalType)Mathf.Clamp(animalType, 0, AnimalRoster.Count - 1), isBot, position));
            }
            ApplyStartMatch(seed, roster, false);
        }

        private void ApplyStartMatch(int seed, IReadOnlyList<NetworkSpawnDefinition> roster, bool worldAlreadyPrepared)
        {
            matchStarted = true;
            status = IsHost ? "PARTIDA ONLINE — HOST" : "PARTIDA ONLINE — CLIENTE";
            ClearSpawnedFighters();
            bootstrap.StartNetworkMatch(seed, roster, networkManager.LocalClientId, IsHost, worldAlreadyPrepared, this);
            Debug.Log($"[Multiplayer] Roster aplicado neste {(IsHost ? "host" : "cliente")}: {roster.Count} participantes.");
        }

        private void SendLocalTransform()
        {
            if (localFighter == null || !entityIds.TryGetValue(localFighter, out int entityId)) return;
            using FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteValueSafe(entityId);
            writer.WriteValueSafe(localFighter.transform.position);
            writer.WriteValueSafe(localFighter.transform.rotation);
            networkManager.CustomMessagingManager.SendNamedMessage(TransformMessage, NetworkManager.ServerClientId, writer,
                NetworkDelivery.UnreliableSequenced);
        }

        private void ReceiveClientTransform(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || !matchStarted) return;
            reader.ReadValueSafe(out int entityId);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out Quaternion rotation);
            if (!entityOwners.TryGetValue(entityId, out ulong ownerId) || ownerId != sender) return;
            if (entities.TryGetValue(entityId, out ThirdPersonAnimalController fighter))
            {
                fighter.ApplyNetworkTransform(position, rotation, false);
            }
        }

        private void SendAuthoritativeState()
        {
            if (networkManager.ConnectedClientsIds.Count <= 1) return;
            List<KeyValuePair<int, ThirdPersonAnimalController>> activeEntities = entities
                .Where(pair => pair.Value != null)
                .ToList();
            using FastBufferWriter writer = new FastBufferWriter(4096, Allocator.Temp);
            writer.WriteValueSafe(activeEntities.Count);
            foreach (KeyValuePair<int, ThirdPersonAnimalController> pair in activeEntities)
            {
                ThirdPersonAnimalController fighter = pair.Value;
                writer.WriteValueSafe(pair.Key);
                writer.WriteValueSafe(fighter.transform.position);
                writer.WriteValueSafe(fighter.transform.rotation);
                writer.WriteValueSafe(fighter.Health != null ? fighter.Health.CurrentHealth : 0f);
                writer.WriteValueSafe(fighter.LivesRemaining);
                writer.WriteValueSafe(fighter.RangedAmmo);
                writer.WriteValueSafe(fighter.RangedMagazineAmmo);
                writer.WriteValueSafe(fighter.IsEliminated);
            }

            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId == networkManager.LocalClientId) continue;
                networkManager.CustomMessagingManager.SendNamedMessage(StateMessage, clientId, writer,
                    NetworkDelivery.UnreliableSequenced);
            }
        }

        private void ReceiveAuthoritativeState(ulong sender, FastBufferReader reader)
        {
            if (IsHost || sender != NetworkManager.ServerClientId || !matchStarted) return;
            reader.ReadValueSafe(out int count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int entityId);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out Quaternion rotation);
                reader.ReadValueSafe(out float health);
                reader.ReadValueSafe(out int lives);
                reader.ReadValueSafe(out int ammo);
                reader.ReadValueSafe(out int magazineAmmo);
                reader.ReadValueSafe(out bool eliminated);
                if (!entities.TryGetValue(entityId, out ThirdPersonAnimalController fighter) || fighter == null) continue;
                bool isLocal = fighter == localFighter;
                bool hostRespawnedLocalPlayer = isLocal && lives < fighter.LivesRemaining && !eliminated;
                fighter.ApplyNetworkSnapshot(position, rotation, health, lives, ammo, magazineAmmo, eliminated,
                    !isLocal || hostRespawnedLocalPlayer);
            }
            BattleRoyaleManager.Instance?.RefreshReplicatedState();
        }

        private void ReceiveAction(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || !matchStarted) return;
            reader.ReadValueSafe(out int entityId);
            reader.ReadValueSafe(out byte actionValue);
            reader.ReadValueSafe(out Vector3 direction);
            if (!entityOwners.TryGetValue(entityId, out ulong ownerId) || ownerId != sender) return;
            if (!entities.TryGetValue(entityId, out ThirdPersonAnimalController fighter) || fighter == null) return;
            fighter.ExecuteNetworkAction((OnlineActionType)actionValue, direction);
        }

        private void OnGUI()
        {
            if (bootstrap == null || bootstrap.MatchStarted) return;
            EnsureStyles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1.18f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            Rect panel = new Rect(20f, height - 144f, 312f, 124f);
            RuntimeGuiTheme.DrawPanel(panel, new Color(0.012f, 0.045f, 0.04f, 0.97f),
                IsConnected ? new Color(0.2f, 1f, 0.48f, 1f) : new Color(0.36f, 0.64f, 0.48f, 1f), 1f);
            GUI.Label(new Rect(panel.x + 10f, panel.y + 5f, panel.width - 20f, 20f), "MULTIPLAYER — 10 PARTICIPANTES", titleStyle);

            if (IsConnected)
            {
                string role = IsHost ? "HOST" : "CLIENTE";
                GUI.Label(new Rect(panel.x + 10f, panel.y + 30f, panel.width - 20f, 24f),
                    $"{role}  •  HUMANOS {HumanPlayerCount}/{TargetParticipants}  •  BOTS {Mathf.Max(0, TargetParticipants - HumanPlayerCount)}", textStyle);
                GUI.Label(new Rect(panel.x + 10f, panel.y + 56f, panel.width - 20f, 24f),
                    string.IsNullOrEmpty(visibleJoinCode) ? status : $"CÓDIGO: {visibleJoinCode}", textStyle);
                GUI.Label(new Rect(panel.x + 10f, panel.y + 84f, panel.width - 20f, 29f), status, textStyle);
            }
            else
            {
                Rect createButton = new Rect(panel.x + 10f, panel.y + 30f, 140f, 28f);
                DrawButton(createButton, busy ? "AGUARDE..." : "CRIAR ONLINE", CreateRelaySession);
                joinCodeInput = GUI.TextField(new Rect(panel.x + 158f, panel.y + 30f, 62f, 28f), joinCodeInput, 8, fieldStyle).ToUpperInvariant();
                DrawButton(new Rect(panel.x + 226f, panel.y + 30f, 76f, 28f), "ENTRAR", JoinRelaySession);

                DrawButton(new Rect(panel.x + 10f, panel.y + 65f, 140f, 28f), "HOST LOCAL", StartLocalHost);
                directAddress = GUI.TextField(new Rect(panel.x + 158f, panel.y + 65f, 96f, 28f), directAddress, 32, fieldStyle);
                DrawButton(new Rect(panel.x + 260f, panel.y + 65f, 42f, 28f), "LAN", StartLocalClient);
                GUI.Label(new Rect(panel.x + 10f, panel.y + 96f, panel.width - 20f, 20f), status, textStyle);
            }

            GUI.matrix = previous;
        }

        private void DrawButton(Rect rect, string label, Action action)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            RuntimeGuiTheme.DrawPanel(rect,
                hovered ? new Color(0.15f, 0.52f, 0.31f, 1f) : new Color(0.08f, 0.28f, 0.2f, 1f),
                hovered ? new Color(0.52f, 1f, 0.68f, 1f) : new Color(0.27f, 0.72f, 0.48f, 1f), 1f, false);
            GUI.Label(rect, label, buttonStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) action?.Invoke();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            RuntimeGuiTheme.Ensure();
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.72f, 1f, 0.8f) } };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, normal = { textColor = Color.white } };
            buttonStyle = new GUIStyle(textStyle) { fontSize = 10 };
            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white, background = Texture2D.grayTexture } };
        }

        private static string ShortError(Exception exception)
        {
            if (exception == null || string.IsNullOrWhiteSpace(exception.Message)) return "DESCONHECIDO";
            string message = exception.Message.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return message.Length <= 54 ? message.ToUpperInvariant() : message.Substring(0, 54).ToUpperInvariant();
        }

        private async void OnApplicationQuit()
        {
            if (session == null) return;
            try
            {
                await session.LeaveAsync();
            }
            catch
            {
                // Application is already shutting down.
            }
        }

        private void OnDestroy()
        {
            if (networkManager != null && handlersRegistered)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (Instance == this) Instance = null;
        }
    }
}
