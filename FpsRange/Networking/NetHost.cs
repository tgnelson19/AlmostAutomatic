using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;

namespace FpsRange.Networking;

/// <summary>
/// Wraps a LiteNetLib NetManager configured as the multiplayer session host. Each connecting
/// peer's LiteNetLib-assigned peer.Id becomes that player's PlayerId directly - no separate
/// id-assignment scheme needed. All events fire synchronously from Poll() on the calling
/// thread (LiteNetLib's documented usage pattern), so no locking is needed anywhere.
/// </summary>
public class NetHost : INetEventListener
{
    private NetManager _netManager;

    public event Action<int> OnPlayerConnected;              // playerId
    public event Action<int> OnPlayerDisconnected;           // playerId
    public event Action<int, NetDataReader> OnStateUpdate;   // fromPlayerId, reader (x,y,z,yaw,pitch follow)
    public event Action<int> OnFireFx;                       // fromPlayerId

    public bool IsRunning { get; private set; }
    public int Port { get; private set; }

    public bool Start(int port)
    {
        _netManager = new NetManager(this);
        IsRunning = _netManager.Start(port);
        Port = port;
        return IsRunning;
    }

    public void Poll() => _netManager?.PollEvents();

    public void Stop()
    {
        _netManager?.Stop();
        IsRunning = false;
    }

    public void SendReliableToAll(NetDataWriter writer) => _netManager?.SendToAll(writer, NetProtocol.Channel, DeliveryMethod.ReliableOrdered);
    public void SendUnreliableToAll(NetDataWriter writer) => _netManager?.SendToAll(writer, NetProtocol.Channel, DeliveryMethod.Sequenced);

    public void SendReliableTo(int peerId, NetDataWriter writer)
    {
        if (_netManager == null) return;
        foreach (var peer in _netManager)
        {
            if (peer.Id == peerId) { peer.Send(writer, NetProtocol.Channel, DeliveryMethod.ReliableOrdered); return; }
        }
    }

    public IEnumerable<int> ConnectedPeerIds()
    {
        if (_netManager == null) yield break;
        foreach (var peer in _netManager) yield return peer.Id;
    }

    public int ConnectedCount => _netManager?.ConnectedPeersCount ?? 0;

    // --- INetEventListener ---

    public void OnPeerConnected(NetPeer peer) => OnPlayerConnected?.Invoke(peer.Id);

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) => OnPlayerDisconnected?.Invoke(peer.Id);

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        var type = (PacketType)reader.GetByte();
        switch (type)
        {
            case PacketType.StateUpdate:
                OnStateUpdate?.Invoke(peer.Id, reader);
                break;
            case PacketType.FireFx:
                reader.GetInt(); // shooter id in the payload is ignored - peer.Id is authoritative
                OnFireFx?.Invoke(peer.Id);
                break;
        }
        reader.Recycle();
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        if (_netManager.ConnectedPeersCount >= NetProtocol.MaxPlayers - 1)
        {
            request.Reject();
            return;
        }
        request.AcceptIfKey(NetProtocol.ConnectionKey);
    }

    public void OnMessageDelivered(NetPeer peer, object userData) { }

    public void OnNtpResponse(NtpPacket packet) { }

    public void OnPeerAddressChanged(NetPeer peer, IPEndPoint previousAddress) { }
}
