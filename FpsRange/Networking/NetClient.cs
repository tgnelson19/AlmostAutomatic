using System;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;

namespace FpsRange.Networking;

public enum ClientConnectionState { Disconnected, Connecting, Connected, Failed }

/// <summary>
/// Wraps a LiteNetLib NetManager configured as a connecting client. Same threading note as
/// NetHost - Poll() dispatches everything synchronously on the calling thread.
/// </summary>
public class NetClient : INetEventListener
{
    private NetManager _netManager;
    private NetPeer _serverPeer;

    public ClientConnectionState State { get; private set; } = ClientConnectionState.Disconnected;
    public string FailReason { get; private set; } = "";

    public event Action<int, NetDataReader> OnWelcome;            // yourId, reader (existing player list follows)
    public event Action<NetDataReader> OnPlayerJoined;            // reader positioned after the type byte
    public event Action<int> OnPlayerLeft;                        // id
    public event Action<NetDataReader> OnWorldSnapshot;           // reader positioned after the type byte
    public event Action<int> OnFireFx;                            // shooter id
    public event Action<int, float> OnHitEvent;                   // victimId, newHp
    public event Action<int, float, float, float> OnRespawnEvent; // victimId, x, y, z

    public void Connect(string ip, int port)
    {
        _netManager = new NetManager(this);
        _netManager.Start();
        State = ClientConnectionState.Connecting;
        _serverPeer = _netManager.Connect(ip, port, NetProtocol.ConnectionKey);
    }

    public void Poll() => _netManager?.PollEvents();

    public void Stop()
    {
        _netManager?.Stop();
        State = ClientConnectionState.Disconnected;
    }

    public void SendReliable(NetDataWriter writer) => _serverPeer?.Send(writer, NetProtocol.Channel, DeliveryMethod.ReliableOrdered);
    public void SendUnreliable(NetDataWriter writer) => _serverPeer?.Send(writer, NetProtocol.Channel, DeliveryMethod.Sequenced);

    // --- INetEventListener ---

    public void OnPeerConnected(NetPeer peer) => State = ClientConnectionState.Connected;

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (State != ClientConnectionState.Connected)
        {
            State = ClientConnectionState.Failed;
            FailReason = disconnectInfo.Reason.ToString();
        }
        else
        {
            State = ClientConnectionState.Disconnected;
        }
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        State = ClientConnectionState.Failed;
        FailReason = socketError.ToString();
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        var type = (PacketType)reader.GetByte();
        switch (type)
        {
            case PacketType.Welcome:
                OnWelcome?.Invoke(reader.GetInt(), reader);
                break;
            case PacketType.PlayerJoined:
                OnPlayerJoined?.Invoke(reader);
                break;
            case PacketType.PlayerLeft:
                OnPlayerLeft?.Invoke(reader.GetInt());
                break;
            case PacketType.WorldSnapshot:
                OnWorldSnapshot?.Invoke(reader);
                break;
            case PacketType.FireFx:
                OnFireFx?.Invoke(reader.GetInt());
                break;
            case PacketType.HitEvent:
                OnHitEvent?.Invoke(reader.GetInt(), reader.GetFloat());
                break;
            case PacketType.RespawnEvent:
                OnRespawnEvent?.Invoke(reader.GetInt(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
                break;
        }
        reader.Recycle();
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request) => request.Reject(); // clients never accept incoming connections

    public void OnMessageDelivered(NetPeer peer, object userData) { }

    public void OnNtpResponse(NtpPacket packet) { }

    public void OnPeerAddressChanged(NetPeer peer, IPEndPoint previousAddress) { }
}
