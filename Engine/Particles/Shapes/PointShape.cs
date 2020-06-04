using System.Numerics;
using Random = SE.Utility.Random;

namespace SE.Particles.Shapes
{
    public class PointShape : IIntersectable
    {
        public Vector2 Center { get; set; }
        public float Rotation { get; set; }

        public bool Intersects(Vector2 point)
        {
            return point == Center;
        }

        public bool Intersects(Vector4 bounds)
        {
            return false;
        }
    }

    public class PointEmitterShape : PointShape, IEmitterShape
    {
        public void Get(float uniformRatio, out Vector2 position, out Vector2 velocity)
        {
            position = Vector2.Zero;
            Random.NextUnitVector(out velocity);
        }
    }
}
