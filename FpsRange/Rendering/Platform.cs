using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FpsRange.Rendering;

/// <summary>
/// The large flat circular-ish platform the player runs/jumps on. Rendered as a thin
/// disc (many-sided polygon) so its edge roughly matches the circular boundary the
/// PlayerController and TargetSpawner already clamp to.
/// </summary>
public class Platform
{
    private Mesh _mesh;

    public void Load(GraphicsDevice device, float radius, int segments = 64)
    {
        Color color = new Color(70, 78, 85);
        var verts = new VertexPositionNormalColor[segments + 1];
        var indices = new short[segments * 3];

        verts[0] = new VertexPositionNormalColor(Vector3.Zero, Vector3.Up, color);
        for (int i = 0; i < segments; i++)
        {
            float a = MathHelper.TwoPi * i / segments;
            verts[i + 1] = new VertexPositionNormalColor(new Vector3(MathF.Cos(a) * radius, 0, MathF.Sin(a) * radius), Vector3.Up, color);
        }
        for (int i = 0; i < segments; i++)
        {
            int next = i + 1 == segments ? 1 : i + 2;
            indices[i * 3 + 0] = 0;
            indices[i * 3 + 1] = (short)next;
            indices[i * 3 + 2] = (short)(i + 1);
        }

        _mesh = new Mesh(device, verts, indices);
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
    {
        _mesh.Draw(device, Matrix.Identity, view, projection);
    }
}
