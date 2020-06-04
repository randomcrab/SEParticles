using System.Numerics;
using Random = SE.Utility.Random;

namespace SE.Shapes
{
    public class PointShape : IEmitterShape, IIntersectable
    {
        public Vector2 Center { get; set; }

        public void Get(out Vector2 position, out Vector2 velocity, float uniformRatio)
        {
            position = Vector2.Zero;
            Random.NextUnitVector(out velocity);
        }

        public bool Intersects(Vector2 point)
        {
            return point == Center;
        }

        public bool Intersects(Vector4 bounds)
        {
            return false;
        }
    }
}
