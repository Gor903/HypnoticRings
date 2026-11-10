using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    public static class TunnelFactory
    {
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

        public const float BallOutlineWidth = 0.06f;

        public static PhysicsMaterial2D CreatePhysicsMaterial(string name, float friction, float bounciness)
            => new PhysicsMaterial2D(name) { friction = friction, bounciness = bounciness };

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

            var pool = new RingPool(config, tunnelGo.transform, ringMaterial);

            pool.Prewarm(TunnelController.MaxRingsInFrame / 2);

            tunnel.Initialise(config, ball, pool);
            tunnel.SetRingColours(new[] { Color.white });
            tunnel.BeginRun(seed);

            return tunnel;
        }

        public static RingController CreateRing(GameConfig config, Transform parent, Material material,
                                                int id, PhysicsMaterial2D physics = null)
        {
            var go = new GameObject($"Ring{id}", typeof(Rigidbody2D), typeof(RingController));
            go.transform.SetParent(parent, false);

            var ring = go.GetComponent<RingController>();
            ring.Initialise(config, material, id,
                            physics ?? CreatePhysicsMaterial("Ring", config.ringFriction, config.ringBounce));
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

            var outline = new GameObject("BallOutline", typeof(MeshFilter), typeof(MeshRenderer));
            outline.transform.SetParent(go.transform, false);

            outline.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            outline.GetComponent<MeshRenderer>().sharedMaterial = material;
            outline.GetComponent<MeshFilter>().sharedMesh =
                BuildDiscMesh(config.ballRadius + BallOutlineWidth, 24);

            var ball = go.GetComponent<BallController>();
            ball.Initialise(config, physics);
            ball.Place(StartPosition(settings));
            return ball;
        }

        public static Vector2 StartPosition(in TunnelSettings settings)
        {
            double distance = settings.TargetRadius - settings.Thickness * 0.5 - settings.BallRadius;
            return new Vector2(0f, (float)-distance);
        }

        public static Mesh BuildDiscMesh(float radius, int sides)
        {
            var vertices = new Vector3[sides + 1];
            var triangles = new int[sides * 3];

            vertices[0] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.PI * 2f * i / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);

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
