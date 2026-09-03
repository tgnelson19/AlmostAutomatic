using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FpsRange.Rendering;

/// <summary>
/// A short-lived bright quad drawn at the pistol barrel on fire, scaling up then
/// fading out over ~0.06s. Purely a viewmodel-space effect, no world-space particle system needed.
/// </summary>
public class MuzzleFlash
{
    private const float Duration = 0.06f;
    private Mesh _quad;
    private float _timer = -1f;

    public void Load(GraphicsDevice device)
    {
        var (verts, indices) = PrimitiveMeshBuilder.BuildQuad(0.18f, 0.18f, new Color(255, 230, 120));
        _quad = new Mesh(device, verts, indices, lighting: false);
    }

    public void Trigger() => _timer = Duration;

    public void Update(GameTime gameTime)
    {
        if (_timer > 0f) _timer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
    }

    public void Draw(GraphicsDevice device, Matrix barrelWorld, Matrix view, Matrix projection)
    {
        if (_timer <= 0f) return;

        float t = 1f - (_timer / Duration); // 0 -> 1 over lifetime
        float scale = MathHelper.Lerp(0.4f, 1.4f, t);
        float alpha = MathHelper.Lerp(1f, 0f, t);

        Matrix world = Matrix.CreateScale(scale) * barrelWorld;

        var prevBlend = device.BlendState;
        var prevDepth = device.DepthStencilState;
        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.DepthRead;

        _quad.Draw(device, world, view, projection, alpha);

        device.BlendState = prevBlend;
        device.DepthStencilState = prevDepth;
    }
}
