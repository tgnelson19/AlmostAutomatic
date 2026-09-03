using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Rendering;

namespace FpsRange.Gameplay;

/// <summary>
/// A single red/white cube target. Each of its 6 faces carries 4 concentric scoring
/// rings (10/5/3/1 points). Hit testing is done by hand: ray-vs-AABB to find which face
/// and where, then a 2D distance-from-face-center check against the ring radii.
/// </summary>
public class Target
{
    public const float Size = 2.4f; // 2x the original 1.2f
    public const float HalfSize = Size / 2f;

    // Ring radius fractions of the face half-width, outer-to-inner.
    private static readonly (float radiusFrac, int points, Color color)[] Rings =
    {
        (1.00f, 1,  Color.White),
        (0.65f, 3,  Color.Red),
        (0.35f, 5,  Color.White),
        (0.15f, 10, Color.Red),
    };

    public Vector3 Position;
    public bool Alive = true;

    private static Mesh _cubeMesh;         // shared base cube mesh (white base so rings read cleanly)
    private static Mesh[] _ringDiscMeshes; // 4 shared ring-disc meshes (outer->inner), reused for all 6 faces

    /// <summary>Exposed so Npc's head can reuse the exact same cube+ring visuals for cosmetic
    /// consistency with the aim-testing targets, even though a headshot's hit-test is binary
    /// (not scored by ring) - see Npc.cs.</summary>
    internal static Mesh SharedCubeMesh => _cubeMesh;
    internal static Mesh[] SharedRingDiscMeshes => _ringDiscMeshes;
    internal static (Vector3 normal, Vector3 right, Vector3 up)[] SharedFaces => Faces;

    public static void LoadShared(GraphicsDevice device)
    {
        var (verts, indices) = PrimitiveMeshBuilder.BuildBox(new Vector3(Size), Color.White);
        _cubeMesh = new Mesh(device, verts, indices);

        _ringDiscMeshes = new Mesh[Rings.Length];
        for (int i = 0; i < Rings.Length; i++)
        {
            float outerR = Rings[i].radiusFrac * HalfSize;
            var (rv, ri) = PrimitiveMeshBuilder.BuildRingDisc(0f, outerR, Rings[i].color, 20);
            _ringDiscMeshes[i] = new Mesh(device, rv, ri, lighting: false);
        }
    }

    // Local-space face definitions: normal, right, up (unit axes), used both for rendering ring
    // overlays and for the hit-test math below.
    private static readonly (Vector3 normal, Vector3 right, Vector3 up)[] Faces =
    {
        (Vector3.Forward,  Vector3.Right,   Vector3.Up),      // -Z... MonoGame Forward = (0,0,-1)
        (Vector3.Backward, Vector3.Left,    Vector3.Up),
        (Vector3.Left,     Vector3.Backward,Vector3.Up),
        (Vector3.Right,    Vector3.Forward, Vector3.Up),
        (Vector3.Up,       Vector3.Right,   Vector3.Backward),
        (Vector3.Down,     Vector3.Right,   Vector3.Forward),
    };

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
    {
        Matrix world = Matrix.CreateTranslation(Position);
        _cubeMesh.Draw(device, world, view, projection);

        // Draw ring overlays on each face, offset slightly outward to avoid z-fighting.
        const float epsilon = 0.006f;
        foreach (var face in Faces)
        {
            Vector3 faceCenter = face.normal * HalfSize + face.normal * epsilon;
            // Build a rotation that maps the quad's local +Z (Backward) axis to the face normal,
            // and local up (Y) to the face's "up" direction.
            Matrix rot = MatrixFromAxes(face.right, face.up, -face.normal);
            Matrix faceWorld = rot * Matrix.CreateTranslation(faceCenter) * world;

            foreach (var ringMesh in _ringDiscMeshes)
                ringMesh.Draw(device, faceWorld, view, projection);
        }
    }

    private static Matrix MatrixFromAxes(Vector3 right, Vector3 up, Vector3 backward)
    {
        return new Matrix(
            right.X, right.Y, right.Z, 0,
            up.X, up.Y, up.Z, 0,
            backward.X, backward.Y, backward.Z, 0,
            0, 0, 0, 1);
    }

    /// <summary>
    /// Ray-vs-target hit test. Returns true and outputs the scored points if the ray hits
    /// any face's cube extent; points are computed from which concentric ring the hit point
    /// falls into (0 if it lands outside the outermost ring, which in practice is the whole face).
    /// </summary>
    public bool TryHit(Ray ray, out int points, out float distance)
    {
        points = 0;
        distance = float.MaxValue;
        if (!Alive) return false;

        var box = new BoundingBox(Position - new Vector3(HalfSize), Position + new Vector3(HalfSize));
        float? hit = ray.Intersects(box);
        if (hit == null) return false;

        distance = hit.Value;
        Vector3 hitPoint = ray.Position + ray.Direction * hit.Value;
        Vector3 local = hitPoint - Position;

        // Determine which face was hit: the axis where |local| is closest to HalfSize.
        Vector3 abs = new Vector3(Math.Abs(local.X), Math.Abs(local.Y), Math.Abs(local.Z));
        (Vector3 normal, Vector3 right, Vector3 up) face;
        if (abs.X >= abs.Y && abs.X >= abs.Z)
            face = local.X > 0 ? Faces[3] : Faces[2];
        else if (abs.Y >= abs.X && abs.Y >= abs.Z)
            face = local.Y > 0 ? Faces[4] : Faces[5];
        else
            face = local.Z > 0 ? Faces[1] : Faces[0];

        float u = Vector3.Dot(local, face.right);
        float v = Vector3.Dot(local, face.up);
        float distFromCenter = MathF.Sqrt(u * u + v * v);

        // Rings array is outer-to-inner; walk inward, last matching (smallest) ring wins.
        foreach (var ring in Rings)
        {
            if (distFromCenter <= ring.radiusFrac * HalfSize)
                points = ring.points;
        }

        return true;
    }
}
