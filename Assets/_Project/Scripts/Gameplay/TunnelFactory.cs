using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// Builds a working tunnel from a config. One place, so the sandbox scene and the play-mode
    /// tests exercise the same construction rather than two that drift apart.
    /// </summary>
    public static class TunnelFactory
    {
        /// <summary>
        /// Flat unlit material for the procedural ring and ball meshes.
        ///
        /// URP/Unlit is chosen for one concrete reason: it has a _BaseColor the renderer can
        /// tint per object. The 2D renderer's mesh shader has no colour property whatsoever,
        /// so every colour written by a theme or by the depth fade goes nowhere and the ring
        /// draws fully transparent — with no error, no warning and no magenta to hint at it.
        /// </summary>
        public static Material CreateUnlitMaterial(Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");
            if (shader == null)
                throw new System.InvalidOperationException("no unlit shader available in this build");

            var material = new Material(shader) { name = "HR Unlit" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            return material;
        }

        /// <summary>Spec §11.3: the outline is 0.06u wide.</summary>
        public const float BallOutlineWidth = 0.06f;

        public static PhysicsMaterial2D CreatePhysicsMaterial(string name, float friction, float bounciness)
            => new PhysicsMaterial2D(name) { friction = friction, bounciness = bounciness };

        /// <summary>
        /// Builds the starting arrangement from CONTRACT.md §2.4: the ball resting on a ring at
        /// the equilibrium radius, one ring inside it, and RingsAhead rings outside.
        /// </summary>
        public static TunnelController Build(GameConfig config, Transform parent, out BallController ball, int seed = 0)
        {
            var settings = config.ToSettings();

            var ringMaterial = CreateUnlitMaterial(Color.white);
            var ringPhysics = CreatePhysicsMaterial("Ring", config.ringFriction, config.ringBounce);
            var ballPhysics = CreatePhysicsMaterial("Ball", config.ballFriction, config.ballBounce);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(parent, false);
            var tunnel = tunnelGo.AddComponent<TunnelController>();

            ball = CreateBall(config, parent, ballPhysics, ringMaterial, settings);

            // The pooled path is the one that ships, so the sandbox uses it too rather than a
            // hand-placed arrangement that could drift from what a real run does.
            var pool = new RingPool(config, tunnelGo.transform, ringMaterial);
            pool.Prewarm(settings.RingsAhead + 3);

            tunnel.Initialise(config, ball, pool);
            tunnel.SetRingColours(new[] { Color.white });
            tunnel.BeginRun(seed);

            return tunnel;
        }

        public static RingController CreateRing(GameConfig config, Transform parent, Material material, int id)
        {
            var go = new GameObject($"Ring{id}", typeof(Rigidbody2D), typeof(RingController));
            go.transform.SetParent(parent, false);

            var ring = go.GetComponent<RingController>();
            ring.Initialise(config, material, id);
            return ring;
        }

        public static BallController CreateBall(GameConfig config, Transform parent,
                                                PhysicsMaterial2D physics, Material material,
                                                in TunnelSettings settings)
        {
            var go = new GameObject("Ball", typeof(Rigidbody2D), typeof(CircleCollider2D),
                                    typeof(MeshFilter), typeof(MeshRenderer), typeof(BallController));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshFilter>().sharedMesh = BuildDiscMesh(config.ballRadius, 24);

            // A ring of background colour behind the ball. Not decoration: in Monochrome the
            // ball and the rings are deliberately the same ink, and this outline is the only
            // thing that keeps them apart (CONTRACT.md §2.11, spec §11.3).
            var outline = new GameObject("BallOutline", typeof(MeshFilter), typeof(MeshRenderer));
            outline.transform.SetParent(go.transform, false);
            // Pushed away from the camera so the ball always wins the depth test against it.
            outline.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            outline.GetComponent<MeshRenderer>().sharedMaterial = material;
            outline.GetComponent<MeshFilter>().sharedMesh =
                BuildDiscMesh(config.ballRadius + BallOutlineWidth, 24);

            var ball = go.GetComponent<BallController>();
            ball.Initialise(config, physics);
            ball.Place(RestingPosition(settings));
            return ball;
        }

        /// <summary>Where the ball sits on the inner surface of its ring, directly below the centre.</summary>
        public static Vector2 RestingPosition(in TunnelSettings settings)
        {
            double distance = settings.TargetRadius - settings.Thickness * 0.5 - settings.BallRadius;
            return new Vector2(0f, (float)-distance);
        }

        static Mesh BuildDiscMesh(float radius, int sides)
        {
            var vertices = new Vector3[sides + 1];
            var triangles = new int[sides * 3];

            vertices[0] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.PI * 2f * i / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                // Same winding rule as the ring band: faces toward the camera, not away.
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 2 > sides ? 1 : i + 2;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = "Ball" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
