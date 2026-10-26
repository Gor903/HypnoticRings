using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// The ball. Moved only by physics — never teleported, never animated along a path, because
    /// the whole design rests on the contact between ball and arc feeling honest.
    ///
    /// The one thing imposed on it is a speed ceiling, and that is a safety rule: a ball moving
    /// further in one step than a ring is thick could pass straight through one.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class BallController : MonoBehaviour
    {
        Rigidbody2D _body;
        CircleCollider2D _collider;
        float _maxSpeed;

        public Rigidbody2D Body => _body;
        public float Radius => _collider.radius;

        /// <summary>Distance from the tunnel centre, which is what passing and crushing are measured in.</summary>
        public double DistanceToCentre
        {
            get
            {
                Vector2 p = _body.position;
                return Mathf.Sqrt(p.x * p.x + p.y * p.y);
            }
        }

        /// <summary>Angle around the tunnel centre in degrees, matching the ring gap convention.</summary>
        public double AngleDeg
        {
            get
            {
                Vector2 p = _body.position;
                return Mathf.Atan2(p.y, p.x) * (float)TunnelMath.DegPerRad;
            }
        }

        public void Initialise(GameConfig config, PhysicsMaterial2D material)
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();

            _maxSpeed = config.maxBallSpeed;
            _collider.radius = config.ballRadius;
            _collider.sharedMaterial = material;

            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.mass = 1f;
            _body.gravityScale = 1f;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.sharedMaterial = material;
        }

        public void Place(Vector2 position)
        {
            _body.position = position;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            transform.position = position;
        }

        void FixedUpdate()
        {
            ClampSpeed();
        }

        /// <summary>
        /// Exposed so the physics-step tests can drive it without waiting for FixedUpdate.
        /// </summary>
        public void ClampSpeed()
        {
            Vector2 v = _body.linearVelocity;
            float sqr = v.sqrMagnitude;
            if (sqr > _maxSpeed * _maxSpeed)
                _body.linearVelocity = v * (_maxSpeed / Mathf.Sqrt(sqr));
        }

        /// <summary>
        /// Furthest the ball can travel in one physics step at its ceiling. Must stay under a
        /// ring's thickness or discrete collision could miss a ring entirely.
        /// </summary>
        public float MaxTravelPerStep => _maxSpeed * Time.fixedDeltaTime;
    }
}
