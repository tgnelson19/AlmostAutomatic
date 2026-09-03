using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FpsRange.Rendering;

/// <summary>
/// Hand-rolled procedural mesh generation. No model files/importers used -
/// every mesh in this game is built here as raw vertex/index buffers.
/// </summary>
public static class PrimitiveMeshBuilder
{
    /// <summary>
    /// Builds a solid-color axis-aligned box centered at the origin, size given in
    /// full width/height/depth. Per-face flat-shaded normals.
    /// </summary>
    public static (VertexPositionNormalColor[] verts, short[] indices) BuildBox(Vector3 size, Color color)
    {
        Vector3 h = size * 0.5f;
        var verts = new List<VertexPositionNormalColor>();
        var indices = new List<short>();

        void Face(Vector3 normal, Vector3 up, Vector3 right)
        {
            Vector3 center = normal * (normal.X != 0 ? h.X : normal.Y != 0 ? h.Y : h.Z);
            Vector3 vUp = up * (up.X != 0 ? h.X : up.Y != 0 ? h.Y : h.Z);
            Vector3 vRight = right * (right.X != 0 ? h.X : right.Y != 0 ? h.Y : h.Z);

            Vector3 p0 = center - vRight - vUp;
            Vector3 p1 = center + vRight - vUp;
            Vector3 p2 = center + vRight + vUp;
            Vector3 p3 = center - vRight + vUp;

            short baseIndex = (short)verts.Count;
            verts.Add(new VertexPositionNormalColor(p0, normal, color));
            verts.Add(new VertexPositionNormalColor(p1, normal, color));
            verts.Add(new VertexPositionNormalColor(p2, normal, color));
            verts.Add(new VertexPositionNormalColor(p3, normal, color));

            indices.Add((short)(baseIndex + 0));
            indices.Add((short)(baseIndex + 1));
            indices.Add((short)(baseIndex + 2));
            indices.Add((short)(baseIndex + 0));
            indices.Add((short)(baseIndex + 2));
            indices.Add((short)(baseIndex + 3));
        }

        Face(Vector3.Forward, Vector3.Up, Vector3.Right);   // -Z? MonoGame Forward = -Z; treat as front
        Face(Vector3.Backward, Vector3.Up, Vector3.Left);
        Face(Vector3.Left, Vector3.Up, Vector3.Backward);
        Face(Vector3.Right, Vector3.Up, Vector3.Forward);
        Face(Vector3.Up, Vector3.Backward, Vector3.Right);
        Face(Vector3.Down, Vector3.Forward, Vector3.Right);

        return (verts.ToArray(), indices.ToArray());
    }

    /// <summary>
    /// Builds a box whose 6 faces each get a baked-in concentric ring texture coordinate set
    /// (used for the red/white scoring target faces). Colors are supplied per-vertex is not enough
    /// for rings, so this variant emits UVs in [-1,1] per face for a shader/pixel test done in Target.cs
    /// via CPU raycast rather than a fragment shader (kept simple: still flat-colored faces, ring math
    /// is done in Target.cs's hit test, not in the shader).
    /// </summary>
    public static (VertexPositionNormalColor[] verts, short[] indices) BuildTargetCube(float size)
    {
        // Base cube: red diffuse color; the ring pattern shows up as separate thin box overlays
        // (see Target.cs BuildRingOverlays) rather than a texture, keeping everything primitive-based.
        return BuildBox(new Vector3(size), Color.Red);
    }

    /// <summary>
    /// Builds a flat quad (2 triangles) in the XY plane centered at origin, facing +Z (Forward-facing
    /// after world transform). Used for muzzle flash billboards and ring overlay discs.
    /// </summary>
    public static (VertexPositionNormalColor[] verts, short[] indices) BuildQuad(float width, float height, Color color)
    {
        Vector3 normal = Vector3.Backward; // +Z
        var verts = new[]
        {
            new VertexPositionNormalColor(new Vector3(-width / 2, -height / 2, 0), normal, color),
            new VertexPositionNormalColor(new Vector3(width / 2, -height / 2, 0), normal, color),
            new VertexPositionNormalColor(new Vector3(width / 2, height / 2, 0), normal, color),
            new VertexPositionNormalColor(new Vector3(-width / 2, height / 2, 0), normal, color),
        };
        var indices = new short[] { 0, 1, 2, 0, 2, 3 };
        return (verts, indices);
    }

    /// <summary>
    /// Builds a flat ring/annulus disc (used to overlay concentric scoring rings on target faces)
    /// in the XY plane facing +Z, with inner radius (0 = filled disc) and outer radius.
    /// </summary>
    public static (VertexPositionNormalColor[] verts, short[] indices) BuildRingDisc(float innerRadius, float outerRadius, Color color, int segments = 24)
    {
        var verts = new List<VertexPositionNormalColor>();
        var indices = new List<short>();
        Vector3 normal = Vector3.Backward;

        for (int i = 0; i < segments; i++)
        {
            float a0 = MathHelper.TwoPi * i / segments;
            float a1 = MathHelper.TwoPi * (i + 1) / segments;

            Vector3 o0 = new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0) * outerRadius;
            Vector3 o1 = new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0) * outerRadius;
            Vector3 i0 = new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0) * innerRadius;
            Vector3 i1 = new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0) * innerRadius;

            short baseIndex = (short)verts.Count;
            verts.Add(new VertexPositionNormalColor(i0, normal, color));
            verts.Add(new VertexPositionNormalColor(o0, normal, color));
            verts.Add(new VertexPositionNormalColor(o1, normal, color));
            verts.Add(new VertexPositionNormalColor(i1, normal, color));

            indices.Add((short)(baseIndex + 0));
            indices.Add((short)(baseIndex + 1));
            indices.Add((short)(baseIndex + 2));
            indices.Add((short)(baseIndex + 0));
            indices.Add((short)(baseIndex + 2));
            indices.Add((short)(baseIndex + 3));
        }

        return (verts.ToArray(), indices.ToArray());
    }

    /// <summary>
    /// Builds an upright cylinder (side wall + top/bottom triangle-fan caps) centered at the
    /// origin - used to render barrels as visually round while their collision stays a simple
    /// AABB box (see CollisionWorld). Mirrors BuildRingDisc's fan-triangulation for the caps.
    /// </summary>
    public static (VertexPositionNormalColor[] verts, short[] indices) BuildCylinder(float radius, float height, Color color, int segments = 16)
    {
        var verts = new List<VertexPositionNormalColor>();
        var indices = new List<short>();
        float halfH = height * 0.5f;

        // Side wall: two rings of verts (bottom/top), quads between adjacent segments.
        for (int i = 0; i <= segments; i++)
        {
            float a = MathHelper.TwoPi * i / segments;
            Vector3 dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            Vector3 normal = dir;
            verts.Add(new VertexPositionNormalColor(dir * radius + new Vector3(0, -halfH, 0), normal, color));
            verts.Add(new VertexPositionNormalColor(dir * radius + new Vector3(0, halfH, 0), normal, color));
        }
        for (int i = 0; i < segments; i++)
        {
            short b0 = (short)(i * 2);
            short b1 = (short)(i * 2 + 1);
            short b2 = (short)((i + 1) * 2);
            short b3 = (short)((i + 1) * 2 + 1);
            indices.Add(b0); indices.Add(b2); indices.Add(b1);
            indices.Add(b1); indices.Add(b2); indices.Add(b3);
        }

        // Caps: triangle fans from a center vertex.
        void Cap(float y, Vector3 normal, bool flip)
        {
            short center = (short)verts.Count;
            verts.Add(new VertexPositionNormalColor(new Vector3(0, y, 0), normal, color));
            short first = (short)verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = MathHelper.TwoPi * i / segments;
                Vector3 dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
                verts.Add(new VertexPositionNormalColor(dir * radius + new Vector3(0, y, 0), normal, color));
            }
            for (int i = 0; i < segments; i++)
            {
                short v0 = (short)(first + i);
                short v1 = (short)(first + i + 1);
                if (flip) { indices.Add(center); indices.Add(v1); indices.Add(v0); }
                else { indices.Add(center); indices.Add(v0); indices.Add(v1); }
            }
        }
        Cap(-halfH, Vector3.Down, flip: true);
        Cap(halfH, Vector3.Up, flip: false);

        return (verts.ToArray(), indices.ToArray());
    }
}
