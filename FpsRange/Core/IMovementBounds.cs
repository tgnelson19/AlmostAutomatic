using System;
using Microsoft.Xna.Framework;

namespace FpsRange.Core;

/// <summary>
/// Strategy for how PlayerController's desired next position gets constrained to the current
/// map's floor/walls. Lets PlayerController stay mode-agnostic: Aim Testing keeps its original
/// flat-plane+circle behavior unchanged (FlatCircleBounds), NPC Battle swaps in wall/step
/// collision (MapCollisionBounds) without touching the movement/gravity/jump code itself.
/// </summary>
public interface IMovementBounds
{
    /// <summary>
    /// Given the old and desired-next eye position, returns the constrained eye position and
    /// whether the player ends this step grounded.
    /// </summary>
    Vector3 Constrain(Vector3 oldPos, Vector3 desiredPos, float eyeHeight, out bool grounded);
}

/// <summary>Exact port of the original Aim Testing collision: flat ground plane + circular boundary clamp.</summary>
public class FlatCircleBounds : IMovementBounds
{
    private readonly float _radius;

    public FlatCircleBounds(float radius) => _radius = radius;

    public Vector3 Constrain(Vector3 oldPos, Vector3 desiredPos, float eyeHeight, out bool grounded)
    {
        Vector3 pos = desiredPos;

        if (pos.Y <= eyeHeight)
        {
            pos.Y = eyeHeight;
            grounded = true;
        }
        else
        {
            grounded = false;
        }

        float horizDist = MathF.Sqrt(pos.X * pos.X + pos.Z * pos.Z);
        float maxDist = _radius - 0.5f;
        if (horizDist > maxDist)
        {
            float scale = maxDist / horizDist;
            pos.X *= scale;
            pos.Z *= scale;
        }

        return pos;
    }
}

/// <summary>NPC Battle collision: walls block horizontal movement, stair/pyramid GroundSteps raise the floor.</summary>
public class MapCollisionBounds : IMovementBounds
{
    private readonly CollisionWorld _world;
    private readonly float _mapHalfX, _mapHalfZ;

    public MapCollisionBounds(CollisionWorld world, float mapHalfX, float mapHalfZ)
    {
        _world = world;
        _mapHalfX = mapHalfX;
        _mapHalfZ = mapHalfZ;
    }

    public Vector3 Constrain(Vector3 oldPos, Vector3 desiredPos, float eyeHeight, out bool grounded)
    {
        float feetY = oldPos.Y - eyeHeight;

        Vector3 resolved = _world.ResolveHorizontal(oldPos, desiredPos, feetY);

        resolved.X = MathHelper.Clamp(resolved.X, -_mapHalfX + 0.5f, _mapHalfX - 0.5f);
        resolved.Z = MathHelper.Clamp(resolved.Z, -_mapHalfZ + 0.5f, _mapHalfZ - 0.5f);

        float groundY = _world.SampleGroundHeight(resolved.X, resolved.Z, feetY) + eyeHeight;

        if (resolved.Y <= groundY)
        {
            resolved.Y = groundY;
            grounded = true;
        }
        else
        {
            grounded = false;
        }

        return resolved;
    }
}
