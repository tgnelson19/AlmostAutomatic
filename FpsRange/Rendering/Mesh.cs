using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FpsRange.Rendering;

/// <summary>
/// Thin wrapper around a static vertex/index buffer pair plus a BasicEffect,
/// so gameplay code can just call Draw(world, view, proj).
/// </summary>
public class Mesh
{
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly int _primitiveCount;
    private readonly BasicEffect _effect;

    public Mesh(GraphicsDevice device, VertexPositionNormalColor[] verts, short[] indices, bool lighting = true)
    {
        _vertexBuffer = new VertexBuffer(device, VertexPositionNormalColor.VertexDeclaration, verts.Length, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(verts);

        _indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        _indexBuffer.SetData(indices);

        _primitiveCount = indices.Length / 3;

        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = lighting,
        };
        if (lighting)
        {
            _effect.EnableDefaultLighting();
            _effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.3f));
            _effect.AmbientLightColor = new Vector3(0.45f, 0.45f, 0.5f);
        }
    }

    public void Draw(GraphicsDevice device, Matrix world, Matrix view, Matrix projection, float alpha = 1f)
    {
        _effect.World = world;
        _effect.View = view;
        _effect.Projection = projection;
        _effect.Alpha = alpha;

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _primitiveCount);
        }
    }
}
