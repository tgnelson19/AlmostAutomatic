using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FpsRange.Rendering;

/// <summary>
/// A big inverted cube around the camera, vertex-colored with a grey (top) to
/// blue-grey (horizon) gradient. No texture asset - pure vertex color gradient,
/// drawn first with depth writes disabled so it always sits "behind" everything.
/// </summary>
public class Skybox
{
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly BasicEffect _effect;
    private readonly int _primitiveCount;
    // Corner-to-center distance is Size * sqrt(3) (~1559 here) - must stay comfortably under
    // Camera.FarPlane (2500f) or the far clip plane slices off the cube's corners, letting the
    // black clear color show through as a stray triangle near the horizon.
    private const float Size = 900f;

    public Skybox(GraphicsDevice device)
    {
        Color top = new Color(150, 165, 185);      // soft grey
        Color horizon = new Color(120, 140, 165);  // blue-grey

        float h = Size;
        // 8 corners of a cube, colored by height (top vs bottom/sides use horizon tone).
        Vector3[] pos = new[]
        {
            new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(-h, h, -h), // back
            new Vector3(-h, -h,  h), new Vector3(h, -h,  h), new Vector3(h, h,  h), new Vector3(-h, h,  h), // front
        };

        Color ColorFor(Vector3 p) => Color.Lerp(horizon, top, MathHelper.Clamp((p.Y + h) / (2f * h), 0f, 1f));

        var verts = new VertexPositionColor[8];
        for (int i = 0; i < 8; i++)
            verts[i] = new VertexPositionColor(pos[i], ColorFor(pos[i]));

        // Indices wound so normals face INWARD (we're inside the box).
        short[] indices = new short[]
        {
            // back (-Z)
            0, 2, 1, 0, 3, 2,
            // front (+Z)
            4, 5, 6, 4, 6, 7,
            // left (-X)
            0, 4, 7, 0, 7, 3,
            // right (+X)
            1, 2, 6, 1, 6, 5,
            // top (+Y)
            3, 7, 6, 3, 6, 2,
            // bottom (-Y)
            0, 1, 5, 0, 5, 4,
        };

        _vertexBuffer = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, verts.Length, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(verts);
        _indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        _indexBuffer.SetData(indices);
        _primitiveCount = indices.Length / 3;

        _effect = new BasicEffect(device) { VertexColorEnabled = true, LightingEnabled = false };
    }

    public void Draw(GraphicsDevice device, Vector3 cameraPosition, Matrix view, Matrix projection)
    {
        var prevDepth = device.DepthStencilState;
        device.DepthStencilState = DepthStencilState.None;

        _effect.World = Matrix.CreateTranslation(cameraPosition);
        _effect.View = view;
        _effect.Projection = projection;

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _primitiveCount);
        }

        device.DepthStencilState = prevDepth;
    }
}
