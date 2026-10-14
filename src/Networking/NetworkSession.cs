using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace EmberLighthouse.Networking;

/// <summary>
/// ENet transport for a host-authoritative room. Add the same named node beneath
/// Main on every machine before opening a connection; RPC paths must match.
/// World state, player identity, and command authorization belong to the caller.
/// </summary>
public partial class NetworkSession : Node
{
    public const int DefaultPort = 24567;
    public const int HostPeerId = 1;
    public const int MaximumReliableBytes = 1024 * 1024;
    public const int MaximumMotionBytes = 16 * 1024;
    public const int RecommendedSnapshotChunkBytes = 48 * 1024;

    private ENetMultiplayerPeer? peer;
    private MultiplayerApi? api;
    private readonly HashSet<int> peers = [];
    private bool host, connected, closing;
    private int callbackDepth, generation;

    public bool IsHost => peer is not null && host && !closing;
    public bool IsClient => peer is not null && !host && !closing;
    public bool Connected => connected && !closing;
    public int LocalPeerId => Connected && api is not null ? api.GetUniqueId() : 0;
    public IReadOnlyCollection<int> Peers => peers;
    public long SentMessages { get; private set; }
    public long ReceivedMessages { get; private set; }
    public long SentBytes { get; private set; }
    public long ReceivedBytes { get; private set; }

    public event Action? ConnectedToHost;
    public event Action<int>? PeerJoined;
    public event Action<int>? PeerLeft;
    public event Action<string>? Disconnected;
    public event Action<int, string>? Message;
    public event Action<int, string>? Motion;

    public NetworkSession() { Name = "NetworkSession"; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        api = Multiplayer;
        api.PeerConnected += OnPeerConnected;
        api.PeerDisconnected += OnPeerDisconnected;
        api.ConnectedToServer += OnConnectedToServer;
        api.ConnectionFailed += OnConnectionFailed;
        api.ServerDisconnected += OnServerDisconnected;
    }

    public Error CreateServer(int port, int maxRemoteClients = 3)
    {
        if (api is null || !IsInsideTree()) return Error.Unconfigured;
        if (callbackDepth > 0) return Error.Busy;
        if (port is < 1024 or > 65535 || maxRemoteClients is < 1 or > 3)
            return Error.InvalidParameter;
        Close();
        FinishClose();
        var candidate = new ENetMultiplayerPeer();
        Error result = candidate.CreateServer(port, maxRemoteClients, 3);
        if (result != Error.Ok) { candidate.Dispose(); return result; }
        Start(candidate, true);
        connected = true;
        return Error.Ok;
    }

    public Error Join(string address, int port)
    {
        if (api is null || !IsInsideTree()) return Error.Unconfigured;
        if (callbackDepth > 0) return Error.Busy;
        if (string.IsNullOrWhiteSpace(address) || address.Length > 253 || port is < 1024 or > 65535)
            return Error.InvalidParameter;
        Close();
        FinishClose();
        var candidate = new ENetMultiplayerPeer();
        Error result = candidate.CreateClient(address.Trim(), port, 3);
        if (result != Error.Ok) { candidate.Dispose(); return result; }
        Start(candidate, false);
        return Error.Ok;
    }

    private void Start(ENetMultiplayerPeer candidate, bool isHost)
    {
        generation++;
        closing = false;
        host = isHost;
        peer = candidate;
        connected = false;
        peers.Clear();
        SentMessages = ReceivedMessages = SentBytes = ReceivedBytes = 0;
        api!.MultiplayerPeer = candidate;
    }

    /// <summary>Closing is deferred if invoked from a multiplayer signal or RPC.</summary>
    public void Close()
    {
        if (peer is null) return;
        connected = false;
        closing = true;
        peers.Clear();
        if (callbackDepth == 0) { FinishClose(); return; }
        int expectedGeneration = generation;
        Callable.From(() => { if (generation == expectedGeneration && closing) FinishClose(); }).CallDeferred();
    }

    private void FinishClose()
    {
        if (!closing) return;
        var oldPeer = peer;
        peer = null;
        // A caller may have just queued a room-closed or leave message. Send it
        // before dropping the socket, rather than losing it until the next poll.
        // ServerDisconnected can leave the ENet peer already inactive. Its Host
        // getter reports a native error in that state, even with null chaining.
        if (oldPeer is not null && oldPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
            oldPeer.Host?.Flush();
        if (api is not null) api.MultiplayerPeer = new OfflineMultiplayerPeer();
        oldPeer?.Close();
        oldPeer?.Dispose();
        host = connected = closing = false;
        peers.Clear();
    }

    public void Send(int peerId, string payload, bool reliable = true)
    {
        if (!Connected || peerId <= 0 || peerId == LocalPeerId || !peers.Contains(peerId)) return;
        // Clients submit all commands to the host; they never communicate game state directly.
        if (!host && peerId != HostPeerId) return;
        int bytes = ValidatePayload(payload, reliable);
        if (bytes < 0) return;
        Error result = RpcId(peerId, reliable ? nameof(ReceiveReliable) : nameof(ReceiveMotion), payload);
        if (result == Error.Ok) { SentMessages++; SentBytes += bytes; }
    }

    public void Broadcast(string payload, bool reliable = true)
    {
        if (!Connected || !host) return;
        // Snapshot the collection because callers can close a connection from callbacks.
        foreach (int peerId in new List<int>(peers)) Send(peerId, payload, reliable);
    }

    public double GetRoundTripMilliseconds(int peerId = HostPeerId)
    {
        if (!Connected || peer is null || !peers.Contains(peerId)) return -1;
        var remote = peer.GetPeer(peerId);
        return remote is null ? -1 : remote.GetStatistic(ENetPacketPeer.PeerStatistic.RoundTripTime);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = 1)]
    public void ReceiveReliable(string payload) => Receive(payload, true);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
        TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 2)]
    public void ReceiveMotion(string payload) => Receive(payload, false);

    private void Receive(string payload, bool reliable)
    {
        if (!Connected || api is null) return;
        int sender = api.GetRemoteSenderId();
        if (sender <= 0 || !peers.Contains(sender) || (!host && sender != HostPeerId)) return;
        int bytes = ValidatePayload(payload, reliable);
        if (bytes < 0) return;
        ReceivedMessages++; ReceivedBytes += bytes;
        InvokeCallback(() => { if (reliable) Message?.Invoke(sender, payload); else Motion?.Invoke(sender, payload); });
    }

    private static int ValidatePayload(string? payload, bool reliable)
    {
        int limit = reliable ? MaximumReliableBytes : MaximumMotionBytes;
        if (string.IsNullOrEmpty(payload) || payload.Length > limit) return -1;
        int bytes = Encoding.UTF8.GetByteCount(payload);
        return bytes <= limit ? bytes : -1;
    }

    private void OnPeerConnected(long id)
    {
        if (peer is null || closing || id <= 0 || id > int.MaxValue) return;
        int peerId = (int)id;
        if (peers.Add(peerId)) InvokeCallback(() => PeerJoined?.Invoke(peerId));
    }

    private void OnPeerDisconnected(long id)
    {
        if (peer is null || closing || id <= 0 || id > int.MaxValue) return;
        int peerId = (int)id;
        if (peers.Remove(peerId)) InvokeCallback(() => PeerLeft?.Invoke(peerId));
    }

    private void OnConnectedToServer()
    {
        if (!IsClient) return;
        connected = true;
        peers.Add(HostPeerId);
        InvokeCallback(() => ConnectedToHost?.Invoke());
    }

    private void OnConnectionFailed() => Fail("无法连接房主，请检查地址、虚拟局域网和防火墙。");
    private void OnServerDisconnected() => Fail("房主已退出或连接中断。");

    private void Fail(string reason)
    {
        if (peer is null || closing) return;
        InvokeCallback(() => { Close(); Disconnected?.Invoke(reason); });
    }

    private void InvokeCallback(Action callback)
    {
        callbackDepth++;
        try { callback(); }
        finally { callbackDepth--; }
    }

    public override void _ExitTree()
    {
        if (api is not null)
        {
            api.PeerConnected -= OnPeerConnected;
            api.PeerDisconnected -= OnPeerDisconnected;
            api.ConnectedToServer -= OnConnectedToServer;
            api.ConnectionFailed -= OnConnectionFailed;
            api.ServerDisconnected -= OnServerDisconnected;
        }
        Close();
        FinishClose();
        api = null;
    }
}
