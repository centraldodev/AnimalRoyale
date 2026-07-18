using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Snapshot of another Animal Battle Royale instance found on the local network.
    /// </summary>
    public sealed class LanPeerInfo
    {
        public string PeerId { get; internal set; }
        public string DisplayName { get; internal set; }
        public string Address { get; internal set; }
        public bool IsInLobby { get; internal set; }
        public bool IsJoinable { get; internal set; }
        public int HumanCount { get; internal set; }
        public float LastSeenRealtime { get; internal set; }
    }

    public readonly struct LanInviteInfo
    {
        public readonly string SenderPeerId;
        public readonly string SenderDisplayName;
        public readonly string HostAddress;
        public readonly ushort HostPort;

        public LanInviteInfo(string senderPeerId, string senderDisplayName, string hostAddress, ushort hostPort)
        {
            SenderPeerId = senderPeerId;
            SenderDisplayName = senderDisplayName;
            HostAddress = hostAddress;
            HostPort = hostPort;
        }
    }

    /// <summary>
    /// Lightweight LAN presence and invitation service. All socket work is polled from Update;
    /// no worker thread is created and no Unity object is touched outside the main thread.
    /// </summary>
    [DefaultExecutionOrder(-175)]
    public sealed class LanFriendDiscovery : MonoBehaviour
    {
        private const int DiscoveryPort = 47777;
        private const string ProtocolMagic = "ABR_LAN_V1";
        private const string AnnouncePacket = "announce";
        private const string QueryPacket = "query";
        private const string InvitePacket = "invite";
        private const float AnnounceInterval = 1.5f;
        private const float PeerExpirySeconds = 6f;
        private const float RefreshDuration = 1.6f;
        private const float RefreshQueryInterval = 0.45f;
        private const float SocketRetryInterval = 3f;
        private const float InviteRetryInterval = 0.24f;
        private const int InviteSendCount = 3;
        private const int MaximumPacketsPerFrame = 32;

        [Serializable]
        private sealed class DiscoveryPacket
        {
            public string magic;
            public string type;
            public string peerId;
            public string displayName;
            public string targetPeerId;
            public string hostAddress;
            public int hostPort;
            public bool isInLobby;
            public bool isJoinable;
            public int humanCount;
        }

        private sealed class PendingInvite
        {
            public DiscoveryPacket Packet;
            public IPEndPoint Destination;
            public int SendsRemaining;
            public float NextSendTime;
        }

        private readonly List<LanPeerInfo> peers = new List<LanPeerInfo>();
        private readonly Dictionary<string, LanPeerInfo> peersById = new Dictionary<string, LanPeerInfo>();
        private readonly List<PendingInvite> pendingInvites = new List<PendingInvite>();
        private UdpClient udp;
        private IPEndPoint broadcastEndpoint;
        private string peerId;
        private string displayName;
        private float nextAnnounceTime;
        private float nextRefreshQueryTime;
        private float nextSocketRetryTime;
        private float refreshEndsAt;
        private bool socketWarningLogged;

        public event Action<LanInviteInfo> InviteReceived;

        public IReadOnlyList<LanPeerInfo> Peers => peers;
        public bool IsRefreshing => Time.realtimeSinceStartup < refreshEndsAt;
        public bool IsAvailable => udp != null;
        public string LastError { get; private set; } = string.Empty;

        public Func<bool> IsInLobbyProvider { private get; set; }
        public Func<bool> IsJoinableProvider { private get; set; }
        public Func<int> HumanCountProvider { private get; set; }

        private void Awake()
        {
            peerId = Guid.NewGuid().ToString("N");
            displayName = BuildDisplayName();
            broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
            OpenSocket();
        }

        private void Start()
        {
            RequestRefresh();
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (udp == null && now >= nextSocketRetryTime) OpenSocket();
            ReceivePackets();

            if (now >= nextAnnounceTime)
            {
                nextAnnounceTime = now + AnnounceInterval;
                BroadcastAnnouncement();
            }

            if (now < refreshEndsAt && now >= nextRefreshQueryTime)
            {
                nextRefreshQueryTime = now + RefreshQueryInterval;
                SendPacket(CreatePacket(QueryPacket), broadcastEndpoint);
            }

            SendPendingInvites(now);
            ExpirePeers(now);
        }

        public void RequestRefresh()
        {
            float now = Time.realtimeSinceStartup;
            if (udp == null && now >= nextSocketRetryTime) OpenSocket();
            refreshEndsAt = now + RefreshDuration;
            nextRefreshQueryTime = 0f;
            nextAnnounceTime = 0f;
        }

        public bool SendInvite(string targetPeerId, string hostAddress, ushort hostPort)
        {
            if (string.IsNullOrWhiteSpace(targetPeerId)) return false;
            if (!peersById.TryGetValue(targetPeerId, out LanPeerInfo target)) return false;
            if (!IPAddress.TryParse(target.Address, out IPAddress targetAddress)) return false;

            DiscoveryPacket packet = CreatePacket(InvitePacket);
            packet.targetPeerId = targetPeerId;
            packet.hostAddress = hostAddress ?? string.Empty;
            packet.hostPort = hostPort;

            IPEndPoint destination = new IPEndPoint(targetAddress, DiscoveryPort);
            SendPacket(packet, destination);
            // UDP is intentionally connectionless. Two short retries make a button click robust
            // without blocking the main thread or keeping a background worker alive.
            pendingInvites.Add(new PendingInvite
            {
                Packet = packet,
                Destination = destination,
                SendsRemaining = InviteSendCount - 1,
                NextSendTime = Time.realtimeSinceStartup + InviteRetryInterval
            });
            return true;
        }

        public void SetDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            displayName = SanitizeDisplayName(value);
            nextAnnounceTime = 0f;
        }

        private void OpenSocket()
        {
            CloseSocket();
            UdpClient candidate = null;
            try
            {
                candidate = new UdpClient(AddressFamily.InterNetwork);
                candidate.ExclusiveAddressUse = false;
                candidate.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                candidate.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                candidate.EnableBroadcast = true;
                candidate.Client.Blocking = false;
                udp = candidate;
                nextSocketRetryTime = 0f;
                LastError = string.Empty;
                socketWarningLogged = false;
            }
            catch (Exception exception)
            {
                candidate?.Dispose();
                nextSocketRetryTime = Time.realtimeSinceStartup + SocketRetryInterval;
                LastError = $"Descoberta LAN indisponível: {exception.Message}";
                if (!socketWarningLogged)
                {
                    socketWarningLogged = true;
                    Debug.LogWarning($"[LAN] {LastError}");
                }
            }
        }

        private void ReceivePackets()
        {
            if (udp == null) return;
            try
            {
                int processed = 0;
                while (processed < MaximumPacketsPerFrame && udp.Available > 0)
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] payload = udp.Receive(ref sender);
                    processed++;
                    ProcessPacket(payload, sender);
                }
            }
            catch (SocketException exception)
            {
                // A non-blocking socket may report WouldBlock between Available and Receive.
                if (exception.SocketErrorCode != SocketError.WouldBlock)
                {
                    LastError = $"Erro ao receber descoberta LAN: {exception.Message}";
                }
            }
            catch (ObjectDisposedException)
            {
                udp = null;
            }
        }

        private void ProcessPacket(byte[] payload, IPEndPoint sender)
        {
            if (payload == null || payload.Length == 0 || sender == null) return;

            DiscoveryPacket packet;
            try
            {
                packet = JsonUtility.FromJson<DiscoveryPacket>(Encoding.UTF8.GetString(payload));
            }
            catch
            {
                return;
            }

            if (packet == null || packet.magic != ProtocolMagic || string.IsNullOrEmpty(packet.peerId)
                || packet.peerId.Length > 64) return;
            if (packet.peerId == peerId) return;

            if (packet.type == QueryPacket)
            {
                SendPacket(CreatePacket(AnnouncePacket), new IPEndPoint(sender.Address, DiscoveryPort));
                return;
            }

            if (packet.type == AnnouncePacket)
            {
                AddOrUpdatePeer(packet, sender.Address.ToString());
                return;
            }

            if (packet.type != InvitePacket || packet.targetPeerId != peerId) return;

            string hostAddress = packet.hostAddress;
            if (!IPAddress.TryParse(hostAddress, out _)) hostAddress = sender.Address.ToString();
            ushort hostPort = packet.hostPort > 0 && packet.hostPort <= ushort.MaxValue
                ? (ushort)packet.hostPort
                : (ushort)7777;
            InviteReceived?.Invoke(new LanInviteInfo(packet.peerId, SanitizeDisplayName(packet.displayName),
                hostAddress, hostPort));
        }

        private void AddOrUpdatePeer(DiscoveryPacket packet, string address)
        {
            if (!peersById.TryGetValue(packet.peerId, out LanPeerInfo peer))
            {
                peer = new LanPeerInfo { PeerId = packet.peerId };
                peersById.Add(packet.peerId, peer);
                peers.Add(peer);
            }

            peer.DisplayName = SanitizeDisplayName(packet.displayName);
            peer.Address = address;
            peer.IsInLobby = packet.isInLobby;
            peer.IsJoinable = packet.isJoinable;
            peer.HumanCount = Mathf.Max(1, packet.humanCount);
            peer.LastSeenRealtime = Time.realtimeSinceStartup;
            peers.Sort(ComparePeers);
        }

        private static int ComparePeers(LanPeerInfo left, LanPeerInfo right)
        {
            int lobbyOrder = right.IsInLobby.CompareTo(left.IsInLobby);
            return lobbyOrder != 0
                ? lobbyOrder
                : string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private void ExpirePeers(float now)
        {
            for (int i = peers.Count - 1; i >= 0; i--)
            {
                LanPeerInfo peer = peers[i];
                if (now - peer.LastSeenRealtime <= PeerExpirySeconds) continue;
                peers.RemoveAt(i);
                peersById.Remove(peer.PeerId);
            }
        }

        private void SendPendingInvites(float now)
        {
            for (int i = pendingInvites.Count - 1; i >= 0; i--)
            {
                PendingInvite invite = pendingInvites[i];
                if (now < invite.NextSendTime) continue;
                SendPacket(invite.Packet, invite.Destination);
                invite.SendsRemaining--;
                if (invite.SendsRemaining <= 0)
                {
                    pendingInvites.RemoveAt(i);
                }
                else
                {
                    invite.NextSendTime = now + InviteRetryInterval;
                }
            }
        }

        private void BroadcastAnnouncement()
        {
            SendPacket(CreatePacket(AnnouncePacket), broadcastEndpoint);
        }

        private DiscoveryPacket CreatePacket(string type)
        {
            return new DiscoveryPacket
            {
                magic = ProtocolMagic,
                type = type,
                peerId = peerId,
                displayName = displayName,
                isInLobby = IsInLobbyProvider?.Invoke() ?? false,
                isJoinable = IsJoinableProvider?.Invoke() ?? true,
                humanCount = Mathf.Max(1, HumanCountProvider?.Invoke() ?? 1)
            };
        }

        private void SendPacket(DiscoveryPacket packet, IPEndPoint destination)
        {
            if (udp == null || packet == null || destination == null) return;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                udp.Send(bytes, bytes.Length, destination);
            }
            catch (Exception exception) when (exception is SocketException || exception is ObjectDisposedException)
            {
                LastError = $"Erro ao enviar descoberta LAN: {exception.Message}";
            }
        }

        private static string BuildDisplayName()
        {
            string machineName;
            try
            {
                machineName = Environment.MachineName;
            }
            catch
            {
                machineName = string.Empty;
            }
            return string.IsNullOrWhiteSpace(machineName) ? "Jogador local" : SanitizeDisplayName(machineName);
        }

        private static string SanitizeDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Jogador local";
            string trimmed = value.Trim().Replace('\n', ' ').Replace('\r', ' ');
            return trimmed.Length <= 28 ? trimmed : trimmed.Substring(0, 28);
        }

        private void CloseSocket()
        {
            if (udp == null) return;
            udp.Close();
            udp.Dispose();
            udp = null;
        }

        private void OnDestroy()
        {
            pendingInvites.Clear();
            CloseSocket();
        }
    }
}
