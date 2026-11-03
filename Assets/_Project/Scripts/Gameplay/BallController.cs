using HR.Core;
using UnityEngine;

namespace HR.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class BallController : MonoBehaviour
    {
        public event System.Action<float> Struck;

        void OnCollisionEnter2D(Collision2D collision)
        {
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

        public double DistanceToCentre
        {
            get
            {
                Vector2 p = _body.position;
                return Mathf.Sqrt(p.x * p.x + p.y * p.y);
            }
        }

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

            _storedVelocity = Vector2.zero;
        }

        public void Launch(in LaunchPlan plan)
        {
            Place(new Vector2((float)plan.X, (float)plan.Y));

            var velocity = new Vector2((float)plan.VelocityX, (float)plan.VelocityY);
            _body.linearVelocity = velocity;
            _storedVelocity = velocity;
        }

        public bool Frozen { get; private set; }

        Vector2 _storedVelocity;

        public void SetFrozen(bool frozen)
        {
            if (Frozen == frozen) return;
            Frozen = frozen;

            if (frozen)
            {
                _storedVelocity = _body.linearVelocity;
                _body.linearVelocity = Vector2.zero;
                _body.simulated = false;
                return;
            }

            _body.simulated = true;
            _body.linearVelocity = _storedVelocity;
        }

        void FixedUpdate()
        {
            if (Frozen) return;

            HoldCruisingSpeed(Time.fixedDeltaTime);
            ClampSpeed();
        }

        public void HoldCruisingSpeed(float dt)
        {
            if (_cruiseSpeed <= 0f || dt <= 0f) return;

            Vector2 v = _body.linearVelocity;
            float speed = v.magnitude;

            if (speed < 1e-4f) return;

            float wanted = Mathf.MoveTowards(speed, _cruiseSpeed, _cruiseAccel * dt);
            _body.linearVelocity = v * (wanted / speed);
        }

        public void ClampSpeed()
        {
            Vector2 v = _body.linearVelocity;
            float sqr = v.sqrMagnitude;
            if (sqr > _maxSpeed * _maxSpeed)
                _body.linearVelocity = v * (_maxSpeed / Mathf.Sqrt(sqr));
        }

        public float MaxTravelPerStep => _maxSpeed * Time.fixedDeltaTime;
    }
}
