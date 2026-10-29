using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    /// <summary>
    /// The ball. Moved only by physics — never teleported, never animated along a path, because
    /// the whole design rests on the contact between ball and arc feeling honest.
    ///
    /// Two things are imposed on it. A speed ceiling, which is a safety rule: a ball moving
    /// further in one step than a ring is thick could pass straight through one. And a
    /// regulator that holds it near its cruising speed, which is what keeps it flying for a
    /// whole run — perfect bounces are not actually perfect, and a moving wall gives energy as
    /// readily as it takes it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class BallController : MonoBehaviour
    {
        /// <summary>Raised when the ball strikes a ring, with the impact speed.</summary>
        public event System.Action<float> Struck;

        void OnCollisionEnter2D(Collision2D collision)
        {
            // Speed, not just the fact of a touch: a ball resting against a wall produces a
            // contact every step, and a knock on every one of them is a buzz, not a knock.
            float speed = collision.relativeVelocity.magnitude;
            if (speed > 0.6f) Struck?.Invoke(speed);
        }

        Rigidbody2D _body;
        CircleCollider2D _collider;
        float _maxSpeed;
        float _cruiseSpeed;
        float _cruiseAccel;

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
            _cruiseSpeed = config.cruiseSpeed;
            _cruiseAccel = config.cruiseAccel;
            _collider.radius = config.ballRadius;
            _collider.sharedMaterial = material;

            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.mass = 1f;
            _body.gravityScale = config.ballGravityScale;
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

        /// <summary>Throws the ball: a place and a heading, both decided by the run's seed.</summary>
        public void Launch(in LaunchPlan plan)
        {
            Place(new Vector2((float)plan.X, (float)plan.Y));
            _body.linearVelocity = new Vector2((float)plan.VelocityX, (float)plan.VelocityY);
        }

        void FixedUpdate()
        {
            HoldCruisingSpeed(Time.fixedDeltaTime);
            ClampSpeed();
        }

        /// <summary>
        /// Eases the ball's speed back toward its cruising speed.
        ///
        /// Exposed so the physics-step tests can drive it without waiting for FixedUpdate. Only
        /// the magnitude is touched — the direction belongs to whatever the ball last hit, and
        /// steering it here would be animating the ball rather than simulating it.
        /// </summary>
        public void HoldCruisingSpeed(float dt)
        {
            if (_cruiseSpeed <= 0f || dt <= 0f) return;

            Vector2 v = _body.linearVelocity;
            float speed = v.magnitude;

            // A ball at a dead stop has no direction to preserve, so there is nothing to scale.
            // It cannot happen with a full bounce, and guessing a heading would hide it if it did.
            if (speed < 1e-4f) return;

            float wanted = Mathf.MoveTowards(speed, _cruiseSpeed, _cruiseAccel * dt);
            _body.linearVelocity = v * (wanted / speed);
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
