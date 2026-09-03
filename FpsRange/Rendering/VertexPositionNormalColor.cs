using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FpsRange.Rendering;

/// <summary>
/// Custom vertex format (Position + Normal + Color) - MonoGame ships PositionColor and
/// PositionNormalTexture built in, but not PositionNormalColor, so it's defined here by hand.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VertexPositionNormalColor : IVertexType
{
    public Vector3 Position;
    public Vector3 Normal;
    public Color Color;

    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0)
    );

    public VertexPositionNormalColor(Vector3 position, Vector3 normal, Color color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
