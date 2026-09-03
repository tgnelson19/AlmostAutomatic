using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Core;
using FpsRange.Rendering;

namespace FpsRange.Gameplay;

/// <summary>
/// The NPC Battle arena, translated from the hand-drawn map: walls (thin lines), tables
/// (hatched boxes), barrels (circles), player-high blocks (solid boxes), and one stepped
/// stair-pyramid (the distinctive cross-shaped solid block). See the plan's "Map data" section
/// for the pixel->world derivation. All obstacles collide as AABBs via CollisionWorld even
/// where they render as something else (barrels render as cylinders, the pyramid as nested
/// terraces) - visuals and collision are intentionally decoupled.
/// </summary>
public class NpcBattleMap
{
    public const float HalfX = 70f;
    public const float HalfZ = 45f;
    public static readonly Vector3 PlayerSpawn = new Vector3(0, 0, -10);
    public static readonly Vector2 PyramidCenter = new Vector2(-7, 13);

    public CollisionWorld Collision { get; } = new();

    private readonly List<(Mesh mesh, Matrix world)> _drawables = new();
    private Mesh _floorMesh;

    public void Load(GraphicsDevice device)
    {
        _drawables.Clear();
        Collision.WallBoxes.Clear();
        Collision.GroundSteps.Clear();

        var (fv, fi) = PrimitiveMeshBuilder.BuildBox(new Vector3(HalfX * 2, 0.2f, HalfZ * 2), new Color(75, 82, 88));
        _floorMesh = new Mesh(device, fv, fi);

        Color wallColor = new Color(120, 110, 95);
        Color tableColor = new Color(120, 80, 50);
        Color barrelColor = new Color(140, 60, 40);
        Color blockColor = new Color(45, 45, 50);

        // --- Buildings: 3 enclosed rectangles, thickness 1.0, height 3.0, 2 door gaps each
        // (3 units wide - enough for the player and NPCs to pass through). Replaces the earlier
        // bare corner-wall clusters, which never actually enclosed anything.

        // Building 1 (NW) - X:[-68,-54] Z:[-45,-5]. North side is the map edge itself, no wall needed there.
        AddWall(device, -68.5f, -45f, -67.5f, -5f, 3f, wallColor);            // west, solid
        AddWall(device, -54.5f, -45f, -53.5f, -26.5f, 3f, wallColor);         // east, door at Z:[-26.5,-23.5]
        AddWall(device, -54.5f, -23.5f, -53.5f, -5f, 3f, wallColor);
        AddWall(device, -68f, -5.5f, -62.5f, -4.5f, 3f, wallColor);           // south, door at X:[-62.5,-59.5]
        AddWall(device, -59.5f, -5.5f, -54f, -4.5f, 3f, wallColor);

        // Building 2 (Room B) - existing west/east/south walls, plus a new north wall (2 doors)
        // replacing what used to be a fully open side.
        AddWall(device, -20.5f, -45f, -19.5f, -21f, 3f, wallColor);  // W3 west
        AddWall(device, 33.5f, -45f, 34.5f, -21f, 3f, wallColor);    // W4 east
        AddWall(device, -20f, -21.5f, 34f, -20.5f, 3f, wallColor);   // W5 south
        AddWall(device, -20f, -44.5f, -15f, -43.5f, 3f, wallColor);  // north, door at X:[-15,-12]
        AddWall(device, -12f, -44.5f, 18f, -43.5f, 3f, wallColor);   // north, door at X:[18,21]
        AddWall(device, 21f, -44.5f, 34f, -43.5f, 3f, wallColor);

        // Building 3 (SE) - X:[32,69] Z:[1,44], shrunk to fit inside HalfX=70 (the old geometry
        // extended to X=77, outside the actual playable map).
        AddWall(device, 32f, 0.5f, 48.5f, 1.5f, 3f, wallColor);      // north, door at X:[48.5,51.5]
        AddWall(device, 51.5f, 0.5f, 69f, 1.5f, 3f, wallColor);
        AddWall(device, 31.5f, 1f, 32.5f, 18.5f, 3f, wallColor);     // west, door at Z:[18.5,21.5]
        AddWall(device, 31.5f, 21.5f, 32.5f, 44f, 3f, wallColor);
        AddWall(device, 68.5f, 1f, 69.5f, 44f, 3f, wallColor);       // east, solid
        AddWall(device, 32f, 43.5f, 69f, 44.5f, 3f, wallColor);      // south, solid

        // --- Tables (1.2 wide x 1.1 tall x 0.8 deep box, rotated where the sketch showed a tall/narrow shape) ---
        AddTable(device, -64f, -23f, wideX: false, tableColor); // T1
        AddTable(device, 17f, -34f, wideX: true, tableColor);   // T2

        // --- Barrels (1.2 diameter x 1.4 tall, rendered as a cylinder, collides as a matching box) ---
        AddBarrel(device, -50f, -42f, barrelColor); // B1
        AddBarrel(device, -49f, -8f, barrelColor);  // B2
        AddBarrel(device, 37f, -42f, barrelColor);  // B3
        AddBarrel(device, 39f, -22f, barrelColor);  // B4
        AddBarrel(device, -18f, -17f, barrelColor); // B5
        AddBarrel(device, -62f, 25f, barrelColor);  // B6
        AddBarrel(device, -55f, 24f, barrelColor);  // B7
        AddBarrel(device, -61f, 33f, barrelColor);  // B8
        AddBarrel(device, -54f, 34f, barrelColor);  // B9
        AddBarrel(device, 28f, 2f, barrelColor);    // B10
        AddBarrel(device, 66f, -3f, barrelColor);   // B11 (moved from X=73, which was outside the map)
        AddBarrel(device, 28f, 42f, barrelColor);   // B12

        // --- Player-high blocks (height 2.0) ---
        AddBlock(device, -7f, -36f, -3f, -32f, blockColor);   // K1
        AddBlock(device, 43f, 10f, 68f, 22f, blockColor);      // K2 main
        AddBlock(device, 43f, 22f, 58f, 32f, blockColor);      // K2 leg
        AddBlock(device, 64f, 32f, 69f, 44f, blockColor);      // K3 (clamped to fit inside the map)

        // --- Stair-pyramid: nested square terraces, riser 0.2 (within CollisionWorld.MaxStepUpHeight
        // so every terrace is climbable from any approach angle around its full perimeter) ---
        BuildPyramid(device);
    }

    private void BuildPyramid(GraphicsDevice device)
    {
        int[] halfExtents = { 8, 7, 6, 5, 4, 3, 2, 1 };
        float[] heights = { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f, 1.2f, 1.4f, 1.6f };
        Color colorA = new Color(58, 56, 54);
        Color colorB = new Color(92, 88, 82);

        for (int i = 0; i < halfExtents.Length; i++)
        {
            float he = halfExtents[i];
            float h = heights[i];
            Vector2 min = PyramidCenter - new Vector2(he, he);
            Vector2 max = PyramidCenter + new Vector2(he, he);

            Collision.GroundSteps.Add(new GroundStep(min, max, h));

            var color = (i % 2 == 0) ? colorA : colorB;
            var (verts, indices) = PrimitiveMeshBuilder.BuildBox(new Vector3(he * 2, h, he * 2), color);
            var mesh = new Mesh(device, verts, indices);
            _drawables.Add((mesh, Matrix.CreateTranslation(PyramidCenter.X, h / 2f, PyramidCenter.Y)));
        }
    }

    private void AddWall(GraphicsDevice device, float minX, float minZ, float maxX, float maxZ, float height, Color color)
    {
        Collision.AddWall(minX, minZ, maxX, maxZ, height);
        var (verts, indices) = PrimitiveMeshBuilder.BuildBox(new Vector3(maxX - minX, height, maxZ - minZ), color);
        var mesh = new Mesh(device, verts, indices);
        _drawables.Add((mesh, Matrix.CreateTranslation((minX + maxX) / 2f, height / 2f, (minZ + maxZ) / 2f)));
    }

    private void AddTable(GraphicsDevice device, float cx, float cz, bool wideX, Color color)
    {
        float w = wideX ? 1.2f : 0.8f;
        float d = wideX ? 0.8f : 1.2f;
        const float h = 1.1f;
        Vector2 min = new Vector2(cx - w / 2f, cz - d / 2f);
        Vector2 max = new Vector2(cx + w / 2f, cz + d / 2f);
        Collision.AddWall(min.X, min.Y, max.X, max.Y, h);
        // Also walkable on top (jumpable, not walk-up-able - see CollisionWorld.MaxStepUpHeight).
        Collision.GroundSteps.Add(new GroundStep(min, max, h));
        var (verts, indices) = PrimitiveMeshBuilder.BuildBox(new Vector3(w, h, d), color);
        var mesh = new Mesh(device, verts, indices);
        _drawables.Add((mesh, Matrix.CreateTranslation(cx, h / 2f, cz)));
    }

    private void AddBarrel(GraphicsDevice device, float cx, float cz, Color color)
    {
        const float r = 0.6f, h = 1.4f;
        Vector2 min = new Vector2(cx - r, cz - r);
        Vector2 max = new Vector2(cx + r, cz + r);
        Collision.AddWall(min.X, min.Y, max.X, max.Y, h);
        // Also walkable on top (jumpable, not walk-up-able - see CollisionWorld.MaxStepUpHeight).
        Collision.GroundSteps.Add(new GroundStep(min, max, h));
        var (verts, indices) = PrimitiveMeshBuilder.BuildCylinder(r, h, color, 12);
        var mesh = new Mesh(device, verts, indices);
        _drawables.Add((mesh, Matrix.CreateTranslation(cx, h / 2f, cz)));
    }

    private void AddBlock(GraphicsDevice device, float minX, float minZ, float maxX, float maxZ, Color color)
    {
        const float h = 2.0f;
        Collision.AddWall(minX, minZ, maxX, maxZ, h);
        var (verts, indices) = PrimitiveMeshBuilder.BuildBox(new Vector3(maxX - minX, h, maxZ - minZ), color);
        var mesh = new Mesh(device, verts, indices);
        _drawables.Add((mesh, Matrix.CreateTranslation((minX + maxX) / 2f, h / 2f, (minZ + maxZ) / 2f)));
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix proj)
    {
        _floorMesh.Draw(device, Matrix.CreateTranslation(0, -0.1f, 0), view, proj);
        foreach (var (mesh, world) in _drawables)
            mesh.Draw(device, world, view, proj);
    }
}
