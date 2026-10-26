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
        /// <summary>Flat unlit material. Themes replace this properly in phase 5.</summary>
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

        public static PhysicsMaterial2D CreatePhysicsMaterial(string name, float friction, float bounciness)
            => new PhysicsMaterial2D(name) { friction = friction, bounciness = bounciness };

        /// <summary>
        /// Builds the starting arrangement from CONTRACT.md §2.4: the ball resting on a ring at
        /// the equilibrium radius, one ring inside it, and RingsAhead rings outside.
        /// </summary>
        public static TunnelController Build(GameConfig config, Transform parent, out BallController ball)
        {
            var settings = config.ToSettings();

            var ringMaterial = CreateUnlitMaterial(Color.white);
            var ringPhysics = CreatePhysicsMaterial("Ring", config.ringFriction, config.ringBounce);
            var ballPhysics = CreatePhysicsMaterial("Ball", config.ballFriction, config.ballBounce);

            var tunnelGo = new GameObject("Tunnel");
            tunnelGo.transform.SetParent(parent, false);
            var tunnel = tunnelGo.AddComponent<TunnelController>();

            ball = CreateBall(config, parent, ballPhysics, ringMaterial, settings);
            tunnel.Initialise(config, ball);

            // One ring inside the ball, then the ball's own ring, then the ones ahead.
            int id = 0;
            for (int i = -1; i <= settings.RingsAhead; i++)
            {
                double radius = settings.TargetRadius + settings.Step * i;
                if (radius <= settings.Thickness) continue;

                var ring = CreateRing(config, tunnelGo.transform, ringMaterial, id);
                // Gap under the ball to start with: the first rings are the tutorial ones.
                ring.Configure(radius, 270.0, settings.MinGapWidth * 2.0, 0.0);
                tunnel.Register(ring);
                id++;
            }

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
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2 > sides ? 1 : i + 2;
            }

            var mesh = new Mesh { name = "Ball" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
