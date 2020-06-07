using System;
using System.Numerics;
using static SE.Particles.ParticleMath;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;
using Random = SE.Utility.Random;
using SE.Core.Extensions;

namespace SE.Particles.Shapes
{
    public class RectangleShape : IIntersectable
    {
        public Vector2 Size {
            get => size;
            set {
                size = new Vector2(
                    Clamp(value.X, 1.0f, float.MaxValue),
                    Clamp(value.Y, 1.0f, float.MaxValue));

                UpdateBounds();
            }
        }
        private Vector2 size;

        public Vector2 Center {
            get => center;
            set {
                center = value;
                UpdateBounds();
            }
        }
        private Vector2 center;

        public Vector4 Bounds { get; private set; }
        public float Rotation { get; set; }

        public RectangleShape() : this(new Vector2(128.0f, 128.0f)) { }

        public RectangleShape(Vector2 size)
        {
            Size = size;
        }

        private void UpdateBounds() 
            => Bounds = new Vector4(center.X - (size.X / 2.0f), center.Y - (size.Y / 2.0f), size.X, size.Y);

        public bool Intersects(Vector2 point)
            => Bounds.Intersects(point);

        public bool Intersects(Vector4 otherBounds)
            => Bounds.Intersects(otherBounds);
    }

    public class RectangleEmitterShape : RectangleShape, IEmitterShape
    {
        public EmissionDirection Direction;
        public bool EdgeOnly;
        public bool Uniform;

        public RectangleEmitterShape() { }

        public RectangleEmitterShape(Vector2 size, EmissionDirection direction = EmissionDirection.None, 
            bool edgeOnly = false, bool uniform = false) : base(size)
        {
            Direction = direction;
            EdgeOnly = edgeOnly;
            Uniform = uniform;
        }

        public void Get(float uniformRatio, out Vector2 position, out Vector2 velocity)
        {
            // Return random position and rotation within rectangle if not edge only.
            if (!EdgeOnly) {
                position = new Vector2(Random.Next(Size.X), Random.Next(Size.Y)) - Size / 2.0f;
                velocity = Random.NextUnitVector();
                return;
            }

            // Continue if the emission is edge only.
            float totalLength = (Bounds.Z * 2.0f) + (Bounds.W * 2.0f);
            float len = Uniform 
                ? uniformRatio * totalLength
                : Random.Next(totalLength);

            // Get position from rectangle edges from specified length.
            Vector4 bounds = Bounds;
            if (len < bounds.Z) {
                // Up.
                position = new Vector2(bounds.X + len, bounds.Y);
                velocity = UpDirection;
            } else if (len < bounds.W + bounds.Z) {
                // Right.
                len -= bounds.Z;
                position = new Vector2(bounds.X + bounds.Z, bounds.Y + len);
                velocity = RightDirection;
            } else if (len < bounds.Z + bounds.W + bounds.Z) {
                // Down.
                len -= bounds.W + bounds.Z;
                position = new Vector2((bounds.X + bounds.Z) - len, bounds.Y + bounds.W);
                velocity = DownDirection;
            } else {
                // Left.
                len -= bounds.Z + bounds.W + bounds.Z;
                position = new Vector2(bounds.X, (bounds.Y + bounds.W) - len);
                velocity = LeftDirection;
            }
            position -= Center;

            // Flip velocity if inwards.
            if (Direction == EmissionDirection.In)
                velocity = -velocity;
            // Randomize velocity if there is no emission direction.
            else if (Direction == EmissionDirection.None)
                velocity = Random.NextUnitVector();
        }
    }
}
