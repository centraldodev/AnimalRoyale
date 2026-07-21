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
        Consume,
        SelectWeapon
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
        private const int TargetParticipants = 15;
        private const ushort LocalPort = 7777;
        private const float LocalConnectionTimeout = 10f;
        private const float LanInviteGracePeriod = 12f;
        private const string SelectionMessage = "ABR_SELECTION_V1";
        private const string StartMessage = "ABR_START_V1";
        private const string TransformMessage = "ABR_TRANSFORM_V1";
        private const string StateMessage = "ABR_STATE_V1";
        private const string ActionMessage = "ABR_ACTION_V1";
        private const string TuningMessage = "ABR_TUNING_V1";
        private const float SendInterval = 0.05f;

        public static OnlineMultiplayerManager Instance { get; private set; }

        private readonly Dictionary<ulong, AnimalType> playerSelections = new Dictionary<ulong, AnimalType>();
        private readonly Dictionary<int, ThirdPersonAnimalController> entities = new Dictionary<int, ThirdPersonAnimalController>();
        private readonly Dictionary<int, ulong> entityOwners = new Dictionary<int, ulong>();
        private readonly Dictionary<ThirdPersonAnimalController, int> entityIds = new Dictionary<ThirdPersonAnimalController, int>();

        private GameBootstrap bootstrap;
        private NetworkManager networkManager;
        private UnityTransport transport;
        private CustomMessagingManager registeredMessagingManager;
        private LanFriendDiscovery lanDiscovery;
        private ISession session;
        private AnimalType localSelection = AnimalType.Tiger;
        private ThirdPersonAnimalController localFighter;
        private bool handlersRegistered;
        private bool busy;
        private bool connected;
        private bool matchStarted;
        private bool localLanSession;
        private bool localClientConnecting;
        private bool lanRefreshWasActive;
        private float nextSendTime;
        private float localConnectionDeadline;
        private float lanInviteWaitEndsAt;
        private int expectedLanHumanCount;
        private string joinCodeInput = string.Empty;
        private string directAddress = "127.0.0.1";
        private string status = "OFFLINE";
        private string visibleJoinCode = string.Empty;

        public bool IsConnected => connected && networkManager != null && networkManager.IsListening;
        public bool IsHost => IsConnected && networkManager.IsHost;
        public bool IsClientOnly => IsConnected && networkManager.IsClient && !networkManager.IsHost;
        public bool IsRelaySession => connected && session != null;
        public bool MatchStarted => matchStarted;
        public bool UsesRemoteAuthority => matchStarted && IsClientOnly;
        public bool IsBusy => busy;
        public bool IsLanRefreshing => lanDiscovery != null && lanDiscovery.IsRefreshing;
        public bool IsWaitingForLanFriend => IsHost && localLanSession && !matchStarted
                                                    && expectedLanHumanCount > HumanPlayerCount
                                                    && Time.unscaledTime < lanInviteWaitEndsAt;
        public int LanInviteWaitSeconds => IsWaitingForLanFriend
            ? Mathf.Max(1, Mathf.CeilToInt(lanInviteWaitEndsAt - Time.unscaledTime))
            : 0;
        public bool CanInviteLanFriends => !busy && !matchStarted
                                                  && (!IsConnected || IsHost && localLanSession);
        public int ParticipantTarget => TargetParticipants;
        public int HumanPlayerCount => session != null ? session.PlayerCount
            : networkManager != null && networkManager.IsListening ? networkManager.ConnectedClientsIds.Count : 1;
        public int PlannedBotCount => Mathf.Max(0, TargetParticipants - HumanPlayerCount);
        public int FriendCount => Mathf.Max(0, HumanPlayerCount - 1);
        public string Status => status;
        public string JoinCode => visibleJoinCode;
        public IReadOnlyList<LanPeerInfo> LanFriends => lanDiscovery != null
            ? lanDiscovery.Peers
            : Array.Empty<LanPeerInfo>();
        public string JoinCodeInput
        {
            get => joinCodeInput;
            set => joinCodeInput = value ?? string.Empty;
        }
        public string DirectAddress
        {
            get => directAddress;
            set => directAddress = value ?? string.Empty;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureNetworkStack();
            EnsureLanDiscovery();
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

        private void EnsureLanDiscovery()
        {
            lanDiscovery = GetComponent<LanFriendDiscovery>();
            if (lanDiscovery == null) lanDiscovery = gameObject.AddComponent<LanFriendDiscovery>();
            lanDiscovery.IsInLobbyProvider = () => IsConnected && localLanSession;
            lanDiscovery.IsJoinableProvider = () => !busy && !matchStarted && !IsConnected;
            lanDiscovery.HumanCountProvider = () => HumanPlayerCount;
            lanDiscovery.InviteReceived -= OnLanInviteReceived;
            lanDiscovery.InviteReceived += OnLanInviteReceived;
        }

        private void Update()
        {
            if (lanRefreshWasActive && !IsLanRefreshing)
            {
                lanRefreshWasActive = false;
                if (status == "PROCURANDO AMIGOS NA REDE LOCAL...")
                {
                    int found = LanFriends.Count;
                    status = found == 0
                        ? "NENHUM AMIGO ENCONTRADO NA REDE LOCAL"
                        : found == 1 ? "1 AMIGO ENCONTRADO NA REDE LOCAL" : $"{found} AMIGOS ENCONTRADOS NA REDE LOCAL";
                }
            }

            if (localClientConnecting && Time.unscaledTime >= localConnectionDeadline)
            {
                localClientConnecting = false;
                busy = false;
                connected = false;
                localLanSession = false;
                status = "O HOST LOCAL NÃO RESPONDEU";
                if (networkManager != null && networkManager.IsListening) networkManager.Shutdown();
            }

            if (expectedLanHumanCount > 0 && Time.unscaledTime >= lanInviteWaitEndsAt)
            {
                expectedLanHumanCount = 0;
                lanInviteWaitEndsAt = 0f;
                if (IsHost && status.StartsWith("CONVITE ENVIADO", StringComparison.Ordinal))
                    status = "AMIGO NÃO ENTROU — VOCÊ PODE INICIAR COM BOTS";
            }

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
            if (IsWaitingForLanFriend)
            {
                status = $"AGUARDANDO O AMIGO ENTRAR — {LanInviteWaitSeconds}s";
                return true;
            }
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

        public async void CreateRelaySession()
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

        public async void JoinRelaySession()
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

        public void StartLocalHost()
        {
            if (busy || IsConnected) return;
            ConfigureConnectionApproval();
            RegisterNetworkHandlers();
            transport.SetConnectionData("127.0.0.1", LocalPort, "0.0.0.0");
            localLanSession = true;
            if (!networkManager.StartHost())
            {
                localLanSession = false;
                status = "NÃO FOI POSSÍVEL INICIAR O HOST LOCAL";
                return;
            }

            RegisterNetworkHandlers();
            connected = true;
            visibleJoinCode = $"LAN:{LocalPort}";
            status = $"HOST LOCAL ATIVO NA PORTA {LocalPort}";
            SetLocalSelection(localSelection);
            Debug.Log($"[Multiplayer] Host local iniciado na porta {LocalPort}.");
        }

        public void StartLocalClient()
        {
            StartLocalClientAt(directAddress, LocalPort);
        }

        private void StartLocalClientAt(string requestedAddress, ushort port)
        {
            if (busy || IsConnected) return;
            string address = string.IsNullOrWhiteSpace(requestedAddress) ? "127.0.0.1" : requestedAddress.Trim();
            directAddress = address;
            RegisterNetworkHandlers();
            transport.SetConnectionData(address, port);
            localLanSession = true;
            busy = true;
            localClientConnecting = true;
            if (!networkManager.StartClient())
            {
                busy = false;
                localClientConnecting = false;
                localLanSession = false;
                status = "NÃO FOI POSSÍVEL INICIAR O CLIENTE LOCAL";
                return;
            }

            RegisterNetworkHandlers();
            connected = false;
            localConnectionDeadline = Time.unscaledTime + LocalConnectionTimeout;
            visibleJoinCode = $"{address}:{port}";
            status = "CONECTANDO AO HOST LOCAL...";
            Debug.Log($"[Multiplayer] Cliente tentando conectar a {address}:{port}.");
        }

        public void RefreshLanFriends()
        {
            if (matchStarted) return;
            lanDiscovery?.RequestRefresh();
            lanRefreshWasActive = lanDiscovery != null && lanDiscovery.IsAvailable;
            status = lanDiscovery != null && lanDiscovery.IsAvailable
                ? "PROCURANDO AMIGOS NA REDE LOCAL..."
                : "DESCOBERTA DE REDE LOCAL INDISPONÍVEL";
        }

        public bool InviteLanFriend(string peerId)
        {
            if (!CanInviteLanFriends || lanDiscovery == null || string.IsNullOrWhiteSpace(peerId)) return false;
            LanPeerInfo peer = lanDiscovery.Peers.FirstOrDefault(candidate => candidate.PeerId == peerId);
            if (peer == null || !peer.IsJoinable)
            {
                status = "ESTE AMIGO NÃO ESTÁ DISPONÍVEL PARA CONVITE";
                return false;
            }

            if (!IsConnected)
            {
                StartLocalHost();
                if (!IsHost) return false;
            }

            if (!localLanSession || HumanPlayerCount >= TargetParticipants)
            {
                status = !localLanSession ? "CONVITES LOCAIS EXIGEM UMA SALA LAN" : "A SALA JÁ ESTÁ CHEIA";
                return false;
            }

            // A pessoa que criou a sala pode deixar o endereço em branco: o convidado usa o IP
            // de origem do próprio pacote. Clientes da mesma sala encaminham o IP do host.
            string hostAddress = IsHost ? string.Empty : directAddress;
            bool sent = lanDiscovery.SendInvite(peerId, hostAddress, LocalPort);
            if (sent)
            {
                expectedLanHumanCount = Mathf.Min(TargetParticipants, HumanPlayerCount + 1);
                lanInviteWaitEndsAt = Time.unscaledTime + LanInviteGracePeriod;
            }
            status = sent
                ? $"CONVITE ENVIADO PARA {peer.DisplayName.ToUpperInvariant()}"
                : "NÃO FOI POSSÍVEL ENVIAR O CONVITE";
            return sent;
        }

        private void OnLanInviteReceived(LanInviteInfo invite)
        {
            if (matchStarted || busy || IsConnected) return;
            status = $"CONVITE DE {invite.SenderDisplayName.ToUpperInvariant()} — ENTRANDO NA SALA...";
            StartLocalClientAt(invite.HostAddress, invite.HostPort);
        }

        private void ConfigureConnectionApproval()
        {
            if (networkManager == null || networkManager.IsListening) return;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            bool roomIsFull = networkManager != null
                              && networkManager.ConnectedClientsIds.Count >= TargetParticipants;
            response.Approved = !matchStarted && !roomIsFull;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = matchStarted
                ? "A partida já começou"
                : roomIsFull ? "A sala está cheia" : string.Empty;
        }

        private void RegisterNetworkHandlers()
        {
            if (networkManager == null) return;
            if (!handlersRegistered)
            {
                handlersRegistered = true;
                networkManager.OnClientConnectedCallback += OnClientConnected;
                networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }

            CustomMessagingManager messaging = networkManager.CustomMessagingManager;
            if (messaging == null || registeredMessagingManager == messaging) return;
            registeredMessagingManager = messaging;
            messaging.RegisterNamedMessageHandler(SelectionMessage, ReceiveSelection);
            messaging.RegisterNamedMessageHandler(StartMessage, ReceiveStartMatch);
            messaging.RegisterNamedMessageHandler(TransformMessage, ReceiveClientTransform);
            messaging.RegisterNamedMessageHandler(StateMessage, ReceiveAuthoritativeState);
            messaging.RegisterNamedMessageHandler(ActionMessage, ReceiveAction);
            messaging.RegisterNamedMessageHandler(TuningMessage, ReceiveServerTuning);
        }

        private void OnClientConnected(ulong clientId)
        {
            connected = true;
            if (clientId == networkManager.LocalClientId)
            {
                localClientConnecting = false;
                busy = false;
            }
            if (expectedLanHumanCount > 0 && HumanPlayerCount >= expectedLanHumanCount)
            {
                expectedLanHumanCount = 0;
                lanInviteWaitEndsAt = 0f;
            }
            status = IsHost
                ? $"JOGADORES HUMANOS: {networkManager.ConnectedClientsIds.Count}/{TargetParticipants}"
                : "CONECTADO AO HOST — ESCOLHA SEU ANIMAL";
            if (clientId == networkManager.LocalClientId && IsClientOnly) SendSelection(localSelection);
            if (IsHost && clientId != networkManager.LocalClientId) SendServerTuning(clientId);
        }

        public void BroadcastServerTuning()
        {
            if (!IsHost || networkManager == null || networkManager.CustomMessagingManager == null) return;
            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId == networkManager.LocalClientId) continue;
                SendServerTuning(clientId);
            }
        }

        private void SendServerTuning(ulong clientId)
        {
            using FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
            WriteServerTuning(writer);
            networkManager.CustomMessagingManager.SendNamedMessage(TuningMessage, clientId, writer,
                NetworkDelivery.ReliableSequenced);
        }

        private static void WriteServerTuning(FastBufferWriter writer)
        {
            writer.WriteValueSafe(ServerGameTuning.TigerLeapDuration);
            writer.WriteValueSafe(ServerGameTuning.TigerLeapSpeed);
            writer.WriteValueSafe(ServerGameTuning.TigerLeapUpSpeed);
            writer.WriteValueSafe(ServerGameTuning.TigerLeapHitRadius);
            writer.WriteValueSafe(ServerGameTuning.TigerLeapDamage);
            writer.WriteValueSafe(ServerGameTuning.TigerLeapKnockback);
            writer.WriteValueSafe(ServerGameTuning.ProjectileSpeed);
            writer.WriteValueSafe(ServerGameTuning.ProjectileRangeSeconds);
            writer.WriteValueSafe(ServerGameTuning.ProjectileGravityMultiplier);
            writer.WriteValueSafe(ServerGameTuning.ProjectileLiftMultiplier);
            writer.WriteValueSafe(ServerGameTuning.ProjectileDamageMultiplier);
            writer.WriteValueSafe(ServerGameTuning.ProjectileRadiusMultiplier);
            writer.WriteValueSafe(ServerGameTuning.RangedShotsPerSecond);
            writer.WriteValueSafe(ServerGameTuning.RangedReloadSeconds);
            writer.WriteValueSafe(ServerGameTuning.SafeZoneWaitBeforeShrink);
            writer.WriteValueSafe(ServerGameTuning.SafeZoneShrinkSpeed);
            writer.WriteValueSafe(ServerGameTuning.SafeZoneDamagePerSecond);
            writer.WriteValueSafe(ServerGameTuning.JumpGravityMultiplier);
            writer.WriteValueSafe(ServerGameTuning.EagleFlightDuration);
            writer.WriteValueSafe(ServerGameTuning.EagleJumpSpeed);
            writer.WriteValueSafe(ServerGameTuning.EagleFlySpeedBonus);
            writer.WriteValueSafe(ServerGameTuning.EagleGlideGravityMultiplier);
            writer.WriteValueSafe(ServerGameTuning.EagleMaximumFallSpeed);
        }

        private void ReceiveServerTuning(ulong sender, FastBufferReader reader)
        {
            if (IsHost || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out float tigerLeapDuration);
            reader.ReadValueSafe(out float tigerLeapSpeed);
            reader.ReadValueSafe(out float tigerLeapUpSpeed);
            reader.ReadValueSafe(out float tigerLeapHitRadius);
            reader.ReadValueSafe(out float tigerLeapDamage);
            reader.ReadValueSafe(out float tigerLeapKnockback);
            reader.ReadValueSafe(out float projectileSpeed);
            reader.ReadValueSafe(out float projectileRangeSeconds);
            reader.ReadValueSafe(out float projectileGravityMultiplier);
            reader.ReadValueSafe(out float projectileLiftMultiplier);
            reader.ReadValueSafe(out float projectileDamageMultiplier);
            reader.ReadValueSafe(out float projectileRadiusMultiplier);
            reader.ReadValueSafe(out float rangedShotsPerSecond);
            reader.ReadValueSafe(out float rangedReloadSeconds);
            reader.ReadValueSafe(out float safeZoneWaitBeforeShrink);
            reader.ReadValueSafe(out float safeZoneShrinkSpeed);
            reader.ReadValueSafe(out float safeZoneDamagePerSecond);
            reader.ReadValueSafe(out float jumpGravityMultiplier);
            reader.ReadValueSafe(out float eagleFlightDuration);
            reader.ReadValueSafe(out float eagleJumpSpeed);
            reader.ReadValueSafe(out float eagleFlySpeedBonus);
            reader.ReadValueSafe(out float eagleGlideGravityMultiplier);
            reader.ReadValueSafe(out float eagleMaximumFallSpeed);

            ServerGameTuning.TigerLeapDuration = tigerLeapDuration;
            ServerGameTuning.TigerLeapSpeed = tigerLeapSpeed;
            ServerGameTuning.TigerLeapUpSpeed = tigerLeapUpSpeed;
            ServerGameTuning.TigerLeapHitRadius = tigerLeapHitRadius;
            ServerGameTuning.TigerLeapDamage = tigerLeapDamage;
            ServerGameTuning.TigerLeapKnockback = tigerLeapKnockback;
            ServerGameTuning.ProjectileSpeed = projectileSpeed;
            ServerGameTuning.ProjectileRangeSeconds = projectileRangeSeconds;
            ServerGameTuning.ProjectileGravityMultiplier = projectileGravityMultiplier;
            ServerGameTuning.ProjectileLiftMultiplier = projectileLiftMultiplier;
            ServerGameTuning.ProjectileDamageMultiplier = projectileDamageMultiplier;
            ServerGameTuning.ProjectileRadiusMultiplier = projectileRadiusMultiplier;
            ServerGameTuning.RangedShotsPerSecond = rangedShotsPerSecond;
            ServerGameTuning.RangedReloadSeconds = rangedReloadSeconds;
            ServerGameTuning.SafeZoneWaitBeforeShrink = safeZoneWaitBeforeShrink;
            ServerGameTuning.SafeZoneShrinkSpeed = safeZoneShrinkSpeed;
            ServerGameTuning.SafeZoneDamagePerSecond = safeZoneDamagePerSecond;
            ServerGameTuning.JumpGravityMultiplier = jumpGravityMultiplier;
            ServerGameTuning.EagleFlightDuration = eagleFlightDuration;
            ServerGameTuning.EagleJumpSpeed = eagleJumpSpeed;
            ServerGameTuning.EagleFlySpeedBonus = eagleFlySpeedBonus;
            ServerGameTuning.EagleGlideGravityMultiplier = eagleGlideGravityMultiplier;
            ServerGameTuning.EagleMaximumFallSpeed = eagleMaximumFallSpeed;
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
            localClientConnecting = false;
            busy = false;
            localLanSession = false;
            string reason = networkManager != null ? networkManager.DisconnectReason : string.Empty;
            status = string.IsNullOrWhiteSpace(reason)
                ? "DESCONECTADO DO HOST"
                : $"CONEXÃO RECUSADA — {reason.ToUpperInvariant()}";
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
                writer.WriteValueSafe(fighter.WeaponLevel);
                writer.WriteValueSafe(fighter.WeaponCrystalProgress);
                writer.WriteValueSafe(fighter.SelectedWeaponSlot);
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
                reader.ReadValueSafe(out int weaponLevel);
                reader.ReadValueSafe(out int crystalProgress);
                reader.ReadValueSafe(out int selectedWeapon);
                reader.ReadValueSafe(out bool eliminated);
                if (!entities.TryGetValue(entityId, out ThirdPersonAnimalController fighter) || fighter == null) continue;
                bool isLocal = fighter == localFighter;
                bool hostRespawnedLocalPlayer = isLocal && lives < fighter.LivesRemaining && !eliminated;
                fighter.ApplyNetworkSnapshot(position, rotation, health, lives, ammo, magazineAmmo,
                    weaponLevel, crystalProgress, selectedWeapon, eliminated, !isLocal || hostRespawnedLocalPlayer);
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
            if (lanDiscovery != null) lanDiscovery.InviteReceived -= OnLanInviteReceived;
            if (registeredMessagingManager != null)
            {
                registeredMessagingManager.UnregisterNamedMessageHandler(SelectionMessage);
                registeredMessagingManager.UnregisterNamedMessageHandler(StartMessage);
                registeredMessagingManager.UnregisterNamedMessageHandler(TransformMessage);
                registeredMessagingManager.UnregisterNamedMessageHandler(StateMessage);
                registeredMessagingManager.UnregisterNamedMessageHandler(ActionMessage);
                registeredMessagingManager.UnregisterNamedMessageHandler(TuningMessage);
            }
            if (networkManager != null && handlersRegistered)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (Instance == this) Instance = null;
        }
    }
}
