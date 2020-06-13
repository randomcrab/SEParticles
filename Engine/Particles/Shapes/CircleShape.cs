using System;
using System.Numerics;
using SE.Core.Extensions;
using Random = SE.Utility.Random;
using static SE.Particles.ParticleMath;

namespace SE.Particles.Shapes
{
    public class CircleShape : IIntersectable
    {
        public Vector2 Center { get; set; }
        public float Rotation { get; set; }

        public float Radius {
            get => radius;
            set => radius = Clamp(value, 1.0f, float.MaxValue);
        }
        private float radius;

        public float AngleRatio {
            get => angleRatio;
            set {
                value = Clamp(value, 0.001f, 1.0f);
                angleRatio = value;

                // Full circle if ratio is near 1.0f.
                IsFullCircle = Math.Abs(value - 1.0f) < 0.0001f;
            }
        }
        private float angleRatio;

        public bool IsFullCircle { get; private set; }  = true;

        public CircleShape(float radius = 32.0f, float angleRatio = 1.0f)
        {
            Radius = radius;
            AngleRatio = angleRatio;
        }

        public bool Intersects(Vector2 point)
        {
            float dx = Math.Abs(point.X - Center.X);
            float dy = Math.Abs(point.Y - Center.Y);
            float r = radius;

            if (dx > r || dy > r)
                return false;
            if (dx + dy <= r)
                return true;

            return (dx * dx) + (dy * dy) <= (r * r);
        }

        public bool Intersects(Vector4 bounds)
        {
            Vector2 circleDistance;

            circleDistance.X = Math.Abs(Center.X - bounds.X);
            circleDistance.Y = Math.Abs(Center.Y - bounds.Y);

            if (circleDistance.X > (bounds.Z/2 + radius) || circleDistance.Y > (bounds.W/2 + radius)) 
                return false;
            if (circleDistance.X <= (bounds.Z/2) || circleDistance.Y <= (bounds.W/2)) 
                return true;

            float foo = (circleDistance.X - bounds.Z / 2) * (circleDistance.X - bounds.Z / 2);
            float bar = (circleDistance.Y - bounds.W / 2) * (circleDistance.Y - bounds.W / 2);
            float cornerDistanceSq = foo + bar;

            return (cornerDistanceSq <= (radius * radius));
        }
    }

    public class CircleEmitterShape : CircleShape, IEmitterShape
    {
        public EmissionDirection Direction;
        public bool EdgeOnly;
        public bool Uniform;

        public CircleEmitterShape(float radius, EmissionDirection direction = EmissionDirection.None, 
            bool edgeOnly = false, bool uniform = false, float angleRatio = 1.0f) : base(radius, angleRatio)
        {
            Direction = direction;
            EdgeOnly = edgeOnly;
            Uniform = uniform;
        }

        public void Get(float uniformRatio, out Vector2 position, out Vector2 velocity)
        {
            float rotation;
            float distance = EdgeOnly 
                ? Radius 
                : Random.Next(0.0f, Radius);

            if (IsFullCircle) {
                // Optimized algorithm for full 360 degree circle.
                rotation = Uniform
                    ? Between(-_PI, _PI, uniformRatio) 
                    : Random.NextAngle();
            } else {
                // Slower algorithm for semicircles.
                rotation = Uniform
                    ? Between(-_PI, -_PI + (_2PI * AngleRatio), uniformRatio) + Rotation
                    : Random.NextAngle(AngleRatio) + Rotation;
            }

            velocity = rotation.ToDirectionVector();
            position = Direction == EmissionDirection.In
                ? new Vector2(-velocity.X * distance, -velocity.Y * distance)
                : new Vector2(velocity.X * distance, velocity.Y * distance);

            if (Direction == EmissionDirection.None)
                velocity = Random.NextUnitVector();
        }
    }
}
