using System.Collections.Generic;
using LiteNetLib.Utils;

namespace FpsRange.Networking;

public enum PacketType : byte
{
    Welcome = 1,        // host -> new client (reliable)
    PlayerJoined = 2,   // host -> all other clients (reliable)
    PlayerLeft = 3,     // host -> all clients (reliable)
    StateUpdate = 4,    // client -> host (unreliable/sequenced)
    WorldSnapshot = 5,  // host -> all clients (unreliable/sequenced)
    FireFx = 6,          // any -> host, host -> all (unreliable)
    HitEvent = 7,        // host -> all (reliable)
    RespawnEvent = 8,    // host -> all (reliable)
}

public struct NetPlayerInfo
{
    public int Id;
    public string Name;
    public float X, Y, Z, Yaw, Pitch, Hp;
}

/// <summary>
/// Shared wire format for the LAN multiplayer protocol, used by both NetHost and NetClient so
/// the two ends can't drift apart. LiteNetLib peer.Id (assigned automatically by the host's
/// NetManager on connect) IS the PlayerId - no separate id-assignment scheme is needed. The
/// host's own local player uses the reserved HostPlayerId, guaranteed not to collide with any
/// real peer.Id (which are always >= 0).
/// </summary>
public static class NetProtocol
{
    public const string ConnectionKey = "FpsRangeLan-v1";
    public const int DefaultPort = 7777;
    public const int MaxPlayers = 8;
    public const byte Channel = 0;
    public const int HostPlayerId = -1;

    public static NetDataWriter WriteWelcome(int yourId, IReadOnlyList<NetPlayerInfo> existing)
    {
        var w = new NetDataWriter();
        w.Put((byte)PacketType.Welcome);
        w.Put(yourId);
        w.Put(existing.Count);
        foreach (var p in existing) WritePlayerInfo(w, p);
        return w;
    }

    public static NetDataWriter WritePlayerJoined(NetPlayerInfo info)
    {
        var w = new NetDataWriter();
        w.Put((byte)PacketType.PlayerJoined);
        WritePlayerInfo(w, info);
        return w;
    }

    public static NetDataWriter WritePlayerLeft(int id)
    {
        var w = new NetDataWriter();
        w.Put((byte)PacketType.PlayerLeft);
        w.Put(id);
        return w;
    }

    public static NetDataWriter WriteStateUpdate(float x, float y, float z, float yaw, float pitch)
    {
        var w = new NetDataWriter();
        w.Put((byte)PacketType.StateUpdate);
        w.Put(x); w.Put(y); w.Put(z); w.Put(yaw); w.Put(pitch);
        return w;
    }

    public static NetDataWriter WriteWorldSnapshot(IReadOnlyList<NetPlayerInfo> players)
    {
        var w = new NetDataWriter();
        w.Put((byte)PacketType.WorldSnapshot);
        w.Put(players.Count);
        foreach (var p in players)
        {
            w.Put(p.Id); w.Put(p.X); w.Put(p.Y); w.Put(p.Z); w.Put(p.Yaw); w.Put(p.Pitch);
        }
        return w;
    }

    public static NetDataWriter WriteFireFx(int shooterId)
    {
        var w = new NetDataWriter();
        w.Put((byte)PacketType.FireFx);
        w.Put(shooterId);
        return w;
    }

    public static NetDataWriter WriteHitEvent(int victimId, float newHp)
    {
        var w = new NetDataWriter();
        w.Put((byte)PacketType.HitEvent);
        w.Put(victimId);
        w.Put(newHp);
        return w;
    }

    public static NetDataWriter WriteRespawnEvent(int victimId, float x, float y, float z)
    {
        var w = new NetDataWriter();
        w.Put((byte)PacketType.RespawnEvent);
        w.Put(victimId);
        w.Put(x); w.Put(y); w.Put(z);
        return w;
    }

    private static void WritePlayerInfo(NetDataWriter w, NetPlayerInfo p)
    {
        w.Put(p.Id);
        w.Put(p.Name ?? "Player");
        w.Put(p.X); w.Put(p.Y); w.Put(p.Z); w.Put(p.Yaw); w.Put(p.Pitch); w.Put(p.Hp);
    }

    public static NetPlayerInfo ReadPlayerInfo(NetDataReader r)
    {
        return new NetPlayerInfo
        {
            Id = r.GetInt(),
            Name = r.GetString(),
            X = r.GetFloat(), Y = r.GetFloat(), Z = r.GetFloat(),
            Yaw = r.GetFloat(), Pitch = r.GetFloat(), Hp = r.GetFloat(),
        };
    }
}
