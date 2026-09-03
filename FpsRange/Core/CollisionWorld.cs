using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FpsRange.Core;

/// <summary>
/// A walkable stair/pyramid tread: a flat XZ footprint (Footprint.Min/Max.Y is unused) whose
/// top sits at TopY. Contributes to ground height but never blocks movement/bullets - unlike
/// WallBoxes, which are full obstacles.
/// </summary>
public readonly struct GroundStep
{
    public readonly BoundingBox Footprint;
    public readonly float TopY;

    public GroundStep(Vector2 minXZ, Vector2 maxXZ, float topY)
    {
        Footprint = new BoundingBox(new Vector3(minXZ.X, 0, minXZ.Y), new Vector3(maxXZ.X, 0, maxXZ.Y));
        TopY = topY;
    }

    public bool ContainsXZ(float x, float z) =>
        x >= Footprint.Min.X && x <= Footprint.Max.X && z >= Footprint.Min.Z && z <= Footprint.Max.Z;
}

/// <summary>
/// Generalized world collision for the NPC Battle map: walls/tables/barrels/blocks are plain
/// AABB "WallBoxes" that block both player movement and bullets (player's and NPCs'); stair
/// tiers are "GroundSteps" that only raise the walkable floor height. No physics engine - this
/// mirrors the ray-vs-AABB pattern already used by Target.TryHit, just applied to obstacles
/// instead of scoring targets.
/// </summary>
public class CollisionWorld
{
    public const float MaxStepUpHeight = 0.25f; // auto-step tolerance between adjacent GroundSteps
    public const float PlayerRadius = PlayerController.PlayerSize / 2f; // 0.5f

    public readonly List<BoundingBox> WallBoxes = new();
    public readonly List<GroundStep> GroundSteps = new();

    public void AddWall(float minX, float minZ, float maxX, float maxZ, float height, float baseY = 0f)
    {
        WallBoxes.Add(new BoundingBox(new Vector3(minX, baseY, minZ), new Vector3(maxX, baseY + height, maxZ)));
    }

    /// <summary>
    /// Highest walkable surface at this XZ position that's reachable (step height) from the
    /// current feet height - flat ground (y=0) is always a fallback floor.
    /// </summary>
    public float SampleGroundHeight(float x, float z, float currentFeetY)
    {
        float best = 0f;
        foreach (var step in GroundSteps)
        {
            if (!step.ContainsXZ(x, z)) continue;
            if (step.TopY <= currentFeetY + MaxStepUpHeight && step.TopY > best)
                best = step.TopY;
        }
        return best;
    }

    /// <summary>
    /// Axis-separated slide-vs-AABB: resolves the X move and Z move independently against any
    /// overlapping WallBoxes, the standard cheap technique for a non-physics-engine FPS. Only
    /// wall boxes whose vertical extent overlaps the player's current standing height are
    /// considered, so low obstacles the player has climbed onto (via GroundSteps) don't wall
    /// them off, and the pyramid's stair-corner WallBoxes only block below their own tier height.
    /// </summary>
    public Vector3 ResolveHorizontal(Vector3 oldPos, Vector3 desiredPos, float feetY)
    {
        float r = PlayerRadius;
        float x = TryAxis(oldPos.X, desiredPos.X, oldPos.Z, feetY, r, horizontal: true);
        float z = TryAxis(oldPos.Z, desiredPos.Z, x, feetY, r, horizontal: false);
        return new Vector3(x, desiredPos.Y, z);
    }

    private float TryAxis(float oldVal, float desiredVal, float otherAxisVal, float feetY, float r, bool horizontal)
    {
        float result = desiredVal;
        foreach (var box in WallBoxes)
        {
            // Ignore walls entirely below the player's feet (e.g. a low block the player is standing atop).
            if (box.Max.Y <= feetY + 0.05f) continue;
            if (box.Min.Y > feetY + 2.2f) continue; // ignore anything far above (ceiling), not relevant here

            float minOther = horizontal ? box.Min.Z - r : box.Min.X - r;
            float maxOther = horizontal ? box.Max.Z + r : box.Max.X + r;

            if (otherAxisVal < minOther || otherAxisVal > maxOther) continue;

            float boxMin = horizontal ? box.Min.X - r : box.Min.Z - r;
            float boxMax = horizontal ? box.Max.X + r : box.Max.Z + r;

            if (result >= boxMin && result <= boxMax)
            {
                // Clamp to whichever edge we approached from.
                result = Math.Abs(oldVal - boxMin) < Math.Abs(oldVal - boxMax) ? boxMin : boxMax;
            }
        }
        return result;
    }

    /// <summary>Linear scan ray-vs-WallBoxes, closest hit within maxDistance (or null).</summary>
    public bool RaycastBlocked(Ray ray, float maxDistance, out float hitDistance)
    {
        hitDistance = float.MaxValue;
        bool blocked = false;
        foreach (var box in WallBoxes)
        {
            float? hit = ray.Intersects(box);
            if (hit.HasValue && hit.Value < maxDistance && hit.Value < hitDistance)
            {
                hitDistance = hit.Value;
                blocked = true;
            }
        }
        return blocked;
    }
}
