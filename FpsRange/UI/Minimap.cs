using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FpsRange.UI;

/// <summary>
/// Circular top-right minimap: player-fixed, world-rotates-around-you (Doom/Quake-style radar) -
/// right for a reflex arena shooter where re-mapping "north" onto current facing would slow
/// reactions to blips. True circular clipping via a two-pass stencil-buffer technique (no
/// custom shader needed): pass 1 rasterizes a filled circle into the stencil buffer only
/// (color writes off), pass 2 draws the actual content with a stencil-equal test so only
/// pixels inside the circle survive. Obstacles/NPCs/player are redrawn as simple 2D shapes
/// every frame (not baked to a RenderTarget2D) - simpler than a baked top-down 3D pass and
/// plenty fast at this obstacle count.
/// </summary>
public class Minimap
{
    public const float MinimapRange = 35f;
    private const int PanelSize = 180;

    private Texture2D _pixel;
    private IReadOnlyList<BoundingBox> _obstacles;

    public void Load(GraphicsDevice device, IReadOnlyList<BoundingBox> obstacles)
    {
        _obstacles = obstacles;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch sb, GraphicsDevice device, Vector3 playerPos, float playerYaw, IReadOnlyList<Vector3> npcPositions)
    {
        int panelX = device.Viewport.Width - PanelSize - 20;
        int panelY = 20;
        var panelRect = new Rectangle(panelX, panelY, PanelSize, PanelSize);
        var center = new Vector2(panelRect.Center.X, panelRect.Center.Y);
        float radius = PanelSize / 2f;
        float pixelsPerUnit = radius / MinimapRange;

        device.Clear(ClearOptions.Stencil, Color.Black, 0f, 0);
        DrawStencilCircle(sb, center, radius);

        var contentStencilState = new DepthStencilState
        {
            StencilEnable = true,
            StencilFunction = CompareFunction.Equal,
            ReferenceStencil = 1,
            StencilPass = StencilOperation.Keep,
            StencilFail = StencilOperation.Keep,
            DepthBufferEnable = false,
        };
        sb.Begin(blendState: BlendState.AlphaBlend, depthStencilState: contentStencilState);

        sb.Draw(_pixel, panelRect, new Color(20, 24, 28, 235));

        float cosY = MathF.Cos(playerYaw), sinY = MathF.Sin(playerYaw);
        Vector2 ToPanel(float worldX, float worldZ)
        {
            float dx = worldX - playerPos.X;
            float dz = worldZ - playerPos.Z;
            float rx = dx * cosY + dz * sinY;
            float ry = dz * cosY - dx * sinY;
            return center + new Vector2(rx, ry) * pixelsPerUnit;
        }

        // Obstacles: small dot per wall/table/barrel/block center, within range.
        foreach (var box in _obstacles)
        {
            Vector3 c = (box.Min + box.Max) / 2f;
            if (Vector2.DistanceSquared(new Vector2(c.X, c.Z), new Vector2(playerPos.X, playerPos.Z)) > MinimapRange * MinimapRange) continue;
            Vector2 p = ToPanel(c.X, c.Z);
            sb.Draw(_pixel, new Rectangle((int)p.X - 2, (int)p.Y - 2, 4, 4), new Color(140, 140, 148));
        }

        // NPC blips (red dots).
        foreach (var npcPos in npcPositions)
        {
            if (Vector2.DistanceSquared(new Vector2(npcPos.X, npcPos.Z), new Vector2(playerPos.X, playerPos.Z)) > MinimapRange * MinimapRange) continue;
            Vector2 p = ToPanel(npcPos.X, npcPos.Z);
            sb.Draw(_pixel, new Rectangle((int)p.X - 3, (int)p.Y - 3, 6, 6), new Color(220, 60, 50));
        }

        // Player marker: fixed arrow at panel center, always pointing "up" (screen-forward).
        DrawTriangle(sb, center, 8f, Color.White);

        DrawRing(sb, center, radius, new Color(200, 205, 210));

        sb.End();
    }

    private void DrawStencilCircle(SpriteBatch sb, Vector2 center, float radius)
    {
        var stencilWrite = new DepthStencilState
        {
            StencilEnable = true,
            StencilFunction = CompareFunction.Always,
            StencilPass = StencilOperation.Replace,
            ReferenceStencil = 1,
            DepthBufferEnable = false,
        };
        var colorWriteOff = new BlendState { ColorWriteChannels = ColorWriteChannels.None };

        sb.Begin(blendState: colorWriteOff, depthStencilState: stencilWrite);
        int r = (int)radius;
        for (int y = -r; y <= r; y++)
        {
            float span = MathF.Sqrt(MathF.Max(0f, radius * radius - y * y));
            if (span < 0.5f) continue;
            sb.Draw(_pixel, new Rectangle((int)(center.X - span), (int)(center.Y + y), (int)(span * 2f), 1), Color.White);
        }
        sb.End();
    }

    private void DrawTriangle(SpriteBatch sb, Vector2 center, float size, Color color)
    {
        // Cheap arrow: 3 tiny rects approximating a triangle pointing up.
        for (int i = 0; i < (int)size; i++)
        {
            float w = size - i;
            sb.Draw(_pixel, new Rectangle((int)(center.X - w / 2f), (int)(center.Y - size / 2f + i), (int)w, 1), color);
        }
    }

    private void DrawRing(SpriteBatch sb, Vector2 center, float radius, Color color)
    {
        int segments = 64;
        for (int i = 0; i < segments; i++)
        {
            float a = MathHelper.TwoPi * i / segments;
            Vector2 p = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
            sb.Draw(_pixel, new Rectangle((int)p.X - 1, (int)p.Y - 1, 2, 2), color);
        }
    }
}
