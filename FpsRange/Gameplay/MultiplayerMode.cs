using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using LiteNetLib.Utils;
using FpsRange.Core;
using FpsRange.Networking;
using FpsRange.UI;

namespace FpsRange.Gameplay;

/// <summary>
/// LAN multiplayer on the same map as NPC Battle, but with connected human players instead of
/// NPCs. Host-authoritative combat/HP, client-authoritative movement - see the plan's
/// "Networking model" section for the reasoning. Every other player is rendered via an Npc
/// instance reused purely as a visual proxy (RemotePlayerAvatar).
/// </summary>
public class MultiplayerMode
{
    public NpcBattleMap Map { get; } = new();
    public PlayerHealth LocalHealth { get; } = new();

    public bool IsHost { get; private set; }
    public string StatusText { get; private set; } = "";
    public string HostAddressLabel { get; private set; } = "";

    private readonly Dictionary<int, RemotePlayerAvatar> _remotePlayers = new();
    private readonly Dictionary<int, PlayerHealth> _serverHealth = new(); // host-only: per-client authoritative HP
    private readonly Dictionary<int, float> _pendingRespawnTimers = new(); // host-only
    private readonly Random _rng = new();
    private readonly MultiplayerHud _hud = new();

    private NetHost _host;
    private NetClient _client;
    private int _localId;
    private GraphicsDevice _device;
    private ClientConnectionState? _lastClientState; // tracks NetClient.State so we notice Failed/Disconnected transitions

    private Vector3 _localGroundPos;
    private float _localYaw, _localPitch;

    private float _networkTickTimer;
    private const float NetworkTickInterval = 1f / 30f;
    private const float RespawnDelay = 2f;
    private const float RespawnMinDistance = 12f;

    private Vector3? _pendingLocalRespawn;

    public void Load(GraphicsDevice device, SpriteFont font)
    {
        _device = device;
        Map.Load(device);
        Npc.LoadShared(device); // safe to call again even if NpcBattleMode already loaded it
        _hud.Load(device, font);
    }

    public void ResetRun() => LocalHealth.Reset();

    public void HostGame(int port)
    {
        Shutdown();
        IsHost = true;
        _localId = NetProtocol.HostPlayerId;

        _host = new NetHost();
        _host.OnPlayerConnected += HostOnPlayerConnected;
        _host.OnPlayerDisconnected += HostOnPlayerDisconnected;
        _host.OnStateUpdate += HostOnStateUpdate;
        _host.OnFireFx += HostOnFireFx;

        bool ok = _host.Start(port);
        HostAddressLabel = ok ? $"{GetLanAddress()}:{port}" : "";
        StatusText = ok ? $"Hosting on {HostAddressLabel}" : "Failed to start host (port already in use?)";
    }

    public void JoinGame(string ip, int port)
    {
        Shutdown();
        IsHost = false;

        _client = new NetClient();
        _client.OnWelcome += ClientOnWelcome;
        _client.OnPlayerJoined += ClientOnPlayerJoined;
        _client.OnPlayerLeft += ClientOnPlayerLeft;
        _client.OnWorldSnapshot += ClientOnWorldSnapshot;
        _client.OnFireFx += ClientOnFireFx;
        _client.OnHitEvent += ClientOnHitEvent;
        _client.OnRespawnEvent += ClientOnRespawnEvent;

        _client.Connect(ip, port);
        StatusText = "Connecting...";
    }

    /// <summary>Tears down whatever networking session is active - called before starting a new
    /// one, and by Game1 whenever the player leaves the Multiplayer app state, so the socket/port
    /// is released and peers are cleanly disconnected rather than left dangling.</summary>
    public void Shutdown()
    {
        _host?.Stop();
        _client?.Stop();
        _host = null;
        _client = null;
        _remotePlayers.Clear();
        _serverHealth.Clear();
        _pendingRespawnTimers.Clear();
        _pendingLocalRespawn = null;
        StatusText = "";
        HostAddressLabel = "";
        _lastClientState = null;
    }

    /// <summary>
    /// Polls the network layer only, with no dependency on local input/player state. Game1 calls
    /// this every frame BEFORE its "window not focused" early-out, because network traffic
    /// (most importantly, the host answering connection requests) must keep flowing even while
    /// this window is in the background - e.g. testing host+join with two instances side by side
    /// on one machine, where only one window can be focused at a time. Without this, a
    /// backgrounded host silently stops responding and a join hangs at "Connecting..." forever.
    /// </summary>
    public void PollNetworkOnly()
    {
        _host?.Poll();
        _client?.Poll();
    }

    public void Update(GameTime gameTime, PlayerController player, Ray? fireRay, double totalTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _localGroundPos = player.Camera.Position - new Vector3(0, PlayerController.EyeHeight, 0);
        _localYaw = player.Camera.Yaw;
        _localPitch = player.Camera.Pitch;

        _host?.Poll();
        _client?.Poll();

        // NetClient tracks Connecting/Connected/Failed/Disconnected internally, but nothing was
        // reading it - a bad IP or an unreachable/firewalled host would leave the UI stuck on
        // "Connecting..." forever even though NetClient had already given up. Surface the
        // transition once so a dead connection reads as a failure instead of a hang.
        if (!IsHost && _client != null && _client.State != _lastClientState)
        {
            _lastClientState = _client.State;
            switch (_client.State)
            {
                case ClientConnectionState.Failed:
                    StatusText = $"Connection failed: {_client.FailReason}";
                    break;
                case ClientConnectionState.Disconnected:
                    StatusText = $"Disconnected from host ({_client.FailReason})";
                    break;
            }
        }

        foreach (var rp in _remotePlayers.Values)
            rp.Tick(gameTime);

        if (_pendingLocalRespawn.HasValue)
        {
            player.Teleport(_pendingLocalRespawn.Value);
            LocalHealth.Reset();
            _pendingLocalRespawn = null;
        }

        if (IsHost)
        {
            LocalHealth.Update(gameTime);
            foreach (var h in _serverHealth.Values) h.Update(gameTime);
            TickRespawns(gameTime, player);
        }
        else
        {
            // Cosmetic local regen simulation between authoritative corrections from the host -
            // see the plan's networking-model note on why this isn't fully synced every tick.
            LocalHealth.Update(gameTime);
        }

        _networkTickTimer -= dt;
        if (_networkTickTimer <= 0f)
        {
            _networkTickTimer = NetworkTickInterval;
            if (IsHost) BroadcastWorldSnapshot(player);
            else _client?.SendUnreliable(NetProtocol.WriteStateUpdate(
                player.Camera.Position.X, player.Camera.Position.Y, player.Camera.Position.Z,
                player.Camera.Yaw, player.Camera.Pitch));
        }

        if (fireRay.HasValue)
        {
            if (IsHost)
            {
                ResolveShot(NetProtocol.HostPlayerId, fireRay.Value);
                BroadcastFireFx(NetProtocol.HostPlayerId);
            }
            else
            {
                _client?.SendUnreliable(NetProtocol.WriteFireFx(_localId));
            }
        }
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix proj)
    {
        Map.Draw(device, view, proj);
        foreach (var rp in _remotePlayers.Values)
            rp.Avatar.Draw(device, view, proj);
    }

    public void DrawHud(SpriteBatch sb, GraphicsDevice device, Matrix view, Matrix proj)
    {
        int playerCount = _remotePlayers.Count + 1;
        _hud.Draw(sb, device, LocalHealth, StatusText, playerCount, IsHost, HostAddressLabel);

        foreach (var rp in _remotePlayers.Values)
        {
            Vector3 barPos = rp.Avatar.Position + new Vector3(0, Npc.BodySize.Y + Npc.HeadWorldSize + 0.15f, 0);
            _hud.DrawPlayerHealthBar(sb, device, view, proj, barPos, rp.DisplayHp / PlayerHealth.MaxHp, rp.Name);
        }
    }

    // ------------------------------------------------------------------
    // Combat (host-authoritative)
    // ------------------------------------------------------------------

    private void ResolveShot(int shooterId, Ray ray)
    {
        int victimId = int.MinValue;
        bool victimIsHead = false;
        float closestDist = float.MaxValue;

        if (shooterId != NetProtocol.HostPlayerId)
            TestCandidate(NetProtocol.HostPlayerId, Npc.ComputeHeadBounds(_localGroundPos), Npc.ComputeBodyBounds(_localGroundPos), ray, ref victimId, ref victimIsHead, ref closestDist);

        foreach (var kv in _remotePlayers)
        {
            if (kv.Key == shooterId) continue;
            TestCandidate(kv.Key, kv.Value.Avatar.GetHeadBounds(), kv.Value.Avatar.GetBodyBounds(), ray, ref victimId, ref victimIsHead, ref closestDist);
        }

        if (victimId == int.MinValue) return;
        if (Map.Collision.RaycastBlocked(ray, closestDist, out _)) return;

        ApplyDamage(victimId, victimIsHead);
    }

    private static void TestCandidate(int id, BoundingBox head, BoundingBox body, Ray ray, ref int bestId, ref bool bestIsHead, ref float bestDist)
    {
        float? h = ray.Intersects(head);
        if (h.HasValue && h.Value < bestDist) { bestDist = h.Value; bestId = id; bestIsHead = true; }
        float? b = ray.Intersects(body);
        if (b.HasValue && b.Value < bestDist) { bestDist = b.Value; bestId = id; bestIsHead = false; }
    }

    private void ApplyDamage(int victimId, bool isHead)
    {
        PlayerHealth health = victimId == NetProtocol.HostPlayerId ? LocalHealth : GetOrCreateServerHealth(victimId);

        if (isHead) health.TakeHeadshot();
        else health.TakeHit();

        _host.SendReliableToAll(NetProtocol.WriteHitEvent(victimId, health.Hp));
        if (victimId != NetProtocol.HostPlayerId && _remotePlayers.TryGetValue(victimId, out var rp))
            rp.DisplayHp = health.Hp;

        if (health.Hp <= 0f)
            _pendingRespawnTimers[victimId] = RespawnDelay;
    }

    private void TickRespawns(GameTime gameTime, PlayerController player)
    {
        if (_pendingRespawnTimers.Count == 0) return;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        var keys = new List<int>(_pendingRespawnTimers.Keys);
        foreach (var id in keys)
        {
            _pendingRespawnTimers[id] -= dt;
            if (_pendingRespawnTimers[id] > 0f) continue;

            _pendingRespawnTimers.Remove(id);
            Vector3 spawnGround = PickRespawnPoint(id);

            if (id == NetProtocol.HostPlayerId)
            {
                player.Teleport(spawnGround);
                LocalHealth.Reset();
            }
            else
            {
                GetOrCreateServerHealth(id).Reset();
                if (_remotePlayers.TryGetValue(id, out var rp)) rp.SnapTo(spawnGround, rp.Avatar.Yaw);
                Vector3 eyePos = spawnGround + new Vector3(0, PlayerController.EyeHeight, 0);
                _host.SendReliableToAll(NetProtocol.WriteRespawnEvent(id, eyePos.X, eyePos.Y, eyePos.Z));
            }
        }
    }

    private Vector3 PickRespawnPoint(int excludeId)
    {
        var mapMin = new Vector2(-NpcBattleMap.HalfX, -NpcBattleMap.HalfZ);
        var mapMax = new Vector2(NpcBattleMap.HalfX, NpcBattleMap.HalfZ);
        Vector3 best = Vector3.Zero;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            float x = MathHelper.Lerp(mapMin.X, mapMax.X, (float)_rng.NextDouble());
            float z = MathHelper.Lerp(mapMin.Y, mapMax.Y, (float)_rng.NextDouble());
            var candidate = new Vector3(x, 0, z);
            best = candidate;

            bool tooClose = excludeId != NetProtocol.HostPlayerId &&
                Vector2.Distance(new Vector2(x, z), new Vector2(_localGroundPos.X, _localGroundPos.Z)) < RespawnMinDistance;

            if (!tooClose)
            {
                foreach (var kv in _remotePlayers)
                {
                    if (kv.Key == excludeId) continue;
                    if (Vector2.Distance(new Vector2(x, z), new Vector2(kv.Value.Avatar.Position.X, kv.Value.Avatar.Position.Z)) < RespawnMinDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
            }

            bool overlapsWall = false;
            foreach (var box in Map.Collision.WallBoxes)
            {
                if (x >= box.Min.X - 0.6f && x <= box.Max.X + 0.6f && z >= box.Min.Z - 0.6f && z <= box.Max.Z + 0.6f)
                {
                    overlapsWall = true;
                    break;
                }
            }

            if (!tooClose && !overlapsWall)
            {
                candidate.Y = Map.Collision.SampleGroundHeight(x, z, 0f);
                return candidate;
            }
        }

        best.Y = Map.Collision.SampleGroundHeight(best.X, best.Z, 0f);
        return best;
    }

    private PlayerHealth GetOrCreateServerHealth(int id)
    {
        if (!_serverHealth.TryGetValue(id, out var h))
        {
            h = new PlayerHealth();
            _serverHealth[id] = h;
        }
        return h;
    }

    private static Vector3 ForwardFromYawPitch(float yaw, float pitch) => new Vector3(
        MathF.Cos(pitch) * MathF.Sin(yaw),
        MathF.Sin(pitch),
        -MathF.Cos(pitch) * MathF.Cos(yaw));

    // ------------------------------------------------------------------
    // Host-side network event handlers
    // ------------------------------------------------------------------

    private void HostOnPlayerConnected(int playerId)
    {
        var existing = new List<NetPlayerInfo>
        {
            new NetPlayerInfo
            {
                Id = NetProtocol.HostPlayerId, Name = "Host",
                X = _localGroundPos.X + 0, Y = _localGroundPos.Y + PlayerController.EyeHeight, Z = _localGroundPos.Z,
                Yaw = _localYaw, Pitch = _localPitch, Hp = LocalHealth.Hp,
            },
        };
        foreach (var kv in _remotePlayers)
        {
            var a = kv.Value;
            existing.Add(new NetPlayerInfo
            {
                Id = kv.Key, Name = a.Name,
                X = a.Avatar.Position.X, Y = a.Avatar.Position.Y + PlayerController.EyeHeight, Z = a.Avatar.Position.Z,
                Yaw = a.Avatar.Yaw, Pitch = a.LastPitch, Hp = a.DisplayHp,
            });
        }
        _host.SendReliableTo(playerId, NetProtocol.WriteWelcome(playerId, existing));

        string name = $"Player {playerId}";
        var newAvatar = new RemotePlayerAvatar { Name = name };
        newAvatar.Avatar.LoadMuzzleFlash(_device);
        _remotePlayers[playerId] = newAvatar;
        _serverHealth[playerId] = new PlayerHealth();

        _host.SendReliableToAll(NetProtocol.WritePlayerJoined(new NetPlayerInfo { Id = playerId, Name = name, Hp = PlayerHealth.MaxHp }));
    }

    private void HostOnPlayerDisconnected(int playerId)
    {
        _remotePlayers.Remove(playerId);
        _serverHealth.Remove(playerId);
        _pendingRespawnTimers.Remove(playerId);
        _host.SendReliableToAll(NetProtocol.WritePlayerLeft(playerId));
    }

    private void HostOnStateUpdate(int fromId, NetDataReader reader)
    {
        float x = reader.GetFloat(), y = reader.GetFloat(), z = reader.GetFloat(), yaw = reader.GetFloat(), pitch = reader.GetFloat();
        if (!_remotePlayers.TryGetValue(fromId, out var rp)) return;

        Vector3 groundPos = new Vector3(x, y, z) - new Vector3(0, PlayerController.EyeHeight, 0);
        rp.ApplySnapshot(groundPos, yaw);
        rp.LastPitch = pitch;
    }

    private void HostOnFireFx(int shooterId)
    {
        if (!_remotePlayers.TryGetValue(shooterId, out var shooter)) return;

        Vector3 origin = shooter.Avatar.Position + new Vector3(0, PlayerController.EyeHeight, 0);
        Vector3 dir = ForwardFromYawPitch(shooter.Avatar.Yaw, shooter.LastPitch);

        ResolveShot(shooterId, new Ray(origin, dir));
        BroadcastFireFx(shooterId);
    }

    private void BroadcastFireFx(int shooterId)
    {
        _host.SendUnreliableToAll(NetProtocol.WriteFireFx(shooterId));
        if (shooterId != NetProtocol.HostPlayerId && _remotePlayers.TryGetValue(shooterId, out var rp))
            rp.Avatar.TriggerMuzzleFlash();
    }

    private void BroadcastWorldSnapshot(PlayerController player)
    {
        var list = new List<NetPlayerInfo>
        {
            new NetPlayerInfo
            {
                Id = NetProtocol.HostPlayerId,
                X = player.Camera.Position.X, Y = player.Camera.Position.Y, Z = player.Camera.Position.Z,
                Yaw = player.Camera.Yaw, Pitch = player.Camera.Pitch,
            },
        };
        foreach (var kv in _remotePlayers)
        {
            var a = kv.Value.Avatar;
            list.Add(new NetPlayerInfo
            {
                Id = kv.Key,
                X = a.Position.X, Y = a.Position.Y + PlayerController.EyeHeight, Z = a.Position.Z,
                Yaw = a.Yaw, Pitch = kv.Value.LastPitch,
            });
        }
        _host.SendUnreliableToAll(NetProtocol.WriteWorldSnapshot(list));
    }

    // ------------------------------------------------------------------
    // Client-side network event handlers
    // ------------------------------------------------------------------

    private void ClientOnWelcome(int yourId, NetDataReader reader)
    {
        _localId = yourId;
        int count = reader.GetInt();
        for (int i = 0; i < count; i++)
            AddOrUpdateRemote(NetProtocol.ReadPlayerInfo(reader));
        StatusText = "Connected";
    }

    private void ClientOnPlayerJoined(NetDataReader reader) => AddOrUpdateRemote(NetProtocol.ReadPlayerInfo(reader));

    private void ClientOnPlayerLeft(int id) => _remotePlayers.Remove(id);

    private void ClientOnWorldSnapshot(NetDataReader reader)
    {
        int count = reader.GetInt();
        for (int i = 0; i < count; i++)
        {
            int id = reader.GetInt();
            float x = reader.GetFloat(), y = reader.GetFloat(), z = reader.GetFloat(), yaw = reader.GetFloat(), pitch = reader.GetFloat();
            if (id == _localId) continue; // never override local prediction with a network echo of ourselves

            if (!_remotePlayers.TryGetValue(id, out var rp))
            {
                rp = new RemotePlayerAvatar { Name = id == NetProtocol.HostPlayerId ? "Host" : $"Player {id}" };
                rp.Avatar.LoadMuzzleFlash(_device);
                _remotePlayers[id] = rp;
            }
            Vector3 groundPos = new Vector3(x, y, z) - new Vector3(0, PlayerController.EyeHeight, 0);
            rp.ApplySnapshot(groundPos, yaw);
            rp.LastPitch = pitch;
        }
    }

    private void ClientOnFireFx(int shooterId)
    {
        if (shooterId == _localId) return; // already played our own muzzle flash locally on fire
        if (_remotePlayers.TryGetValue(shooterId, out var rp))
            rp.Avatar.TriggerMuzzleFlash();
    }

    private void ClientOnHitEvent(int victimId, float newHp)
    {
        if (victimId == _localId) LocalHealth.SetHp(newHp);
        else if (_remotePlayers.TryGetValue(victimId, out var rp)) rp.DisplayHp = newHp;
    }

    private void ClientOnRespawnEvent(int victimId, float x, float y, float z)
    {
        Vector3 groundPos = new Vector3(x, y - PlayerController.EyeHeight, z);
        if (victimId == _localId) _pendingLocalRespawn = groundPos;
        else if (_remotePlayers.TryGetValue(victimId, out var rp)) rp.SnapTo(groundPos, rp.Avatar.Yaw);
    }

    private void AddOrUpdateRemote(NetPlayerInfo info)
    {
        if (info.Id == _localId) return; // never create an avatar for ourselves
        if (!_remotePlayers.TryGetValue(info.Id, out var rp))
        {
            rp = new RemotePlayerAvatar { Name = info.Name };
            rp.Avatar.LoadMuzzleFlash(_device);
            _remotePlayers[info.Id] = rp;
        }
        Vector3 groundPos = new Vector3(info.X, info.Y, info.Z) - new Vector3(0, PlayerController.EyeHeight, 0);
        rp.ApplySnapshot(groundPos, info.Yaw);
        rp.LastPitch = info.Pitch;
        rp.DisplayHp = info.Hp;
    }

    /// <summary>
    /// Picks the address to show as the host's connect string. GetAllNetworkInterfaces() often
    /// lists virtual adapters (Hyper-V vEthernet, Docker/WSL, VPN) ahead of the real Wi-Fi/Ethernet
    /// adapter; blindly taking the first "Up" IPv4 address can hand out one of those instead of the
    /// LAN address the other machine can actually reach, which is exactly what makes a client
    /// connect attempt just sit there. This machine's real LAN lives on 192.168.1.0/24, so prefer
    /// an address on that subnet outright, then fall back to the first non-virtual private address.
    /// </summary>
    private static string GetLanAddress()
    {
        const string preferredSubnetPrefix = "192.168.1.";
        try
        {
            string firstFallback = null;
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                if (ni.Description.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (ni.Description.IndexOf("Hyper-V", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (ni.Description.IndexOf("VMware", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (ni.Description.IndexOf("VPN", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string ip = addr.Address.ToString();
                    if (ip.StartsWith(preferredSubnetPrefix, StringComparison.Ordinal)) return ip;
                    firstFallback ??= ip;
                }
            }
            if (firstFallback != null) return firstFallback;
        }
        catch { /* best-effort - fall through to the loopback fallback below */ }
        return "127.0.0.1";
    }
}
