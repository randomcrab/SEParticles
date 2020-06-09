using System;
using System.Numerics;
using SE.Particles.Shapes;
using static SE.Particles.ParticleMath;

namespace SE.Particles.AreaModules
{
    public class ForceModule : AreaModule
    {
        public float MaxDistance {
            get => maxDistance;
            set => maxDistance = Clamp(value, minDistance, float.MaxValue);
        }
        private float maxDistance = float.MaxValue;

        public float MinDistance {
            get => minDistance;
            set => minDistance = Clamp(value, 0.0f, MaxDistance);
        }
        private float minDistance;

        public float Intensity {
            get => intensity;
            set => intensity = Clamp(value, 0.0f, float.MaxValue);
        }
        private float intensity = 25.0f;

        public float SpeedIncrease {
            get => speedIncrease;
            set => speedIncrease = Clamp(value, 0.0f, float.MaxValue);
        }
        private float speedIncrease = 12.0f;

        private Mode mode = Mode.Attract;

        public ForceModule(IIntersectable shape, Vector2? position = null) : base(shape, position) { }
        
        public override unsafe void ProcessParticles(float deltaTime, Particle* particles, int length)
        {
            Particle* tail = particles + length;
            for (Particle* particle = particles; particle < tail; particle++) {
                if (!Shape.Intersects(particle->Position)) 
                    continue;

                float distance = (Position - particle->Position).Length();
                if (distance > maxDistance)
                    continue;

                float ratio = GetRatio(maxDistance, minDistance, distance);
                float speedDelta = speedIncrease * ratio;
                GetAngle(particle->Position, Position, out Vector2 direction, out float angle);
                if(mode == Mode.Repel)
                    direction = -direction;

                particle->Direction = Vector2.Lerp(particle->Direction, direction, ratio * intensity * deltaTime);
                particle->SpriteRotation = Lerp(particle->SpriteRotation, angle, ratio * intensity * deltaTime);
                particle->Speed += speedDelta;
            }
        }

        private void GetAngle(Vector2 from, Vector2 to, out Vector2 direction, out float angle)
        {
            direction = Vector2.Normalize(to - from);
#if NETSTANDARD2_1
            angle = MathF.Atan2(-direction.X, direction.Y);
#else
            angle = (float)Math.Atan2(-direction.X, direction.Y);
#endif
        }

        public static ForceModule Attract(IIntersectable shape, Vector2 position, float minDistance, float maxDistance, 
            float intensity = 10.0f, float speedIncrease = 10.0f) 
            => new ForceModule(shape) {
                Position = position,
                MaxDistance = maxDistance,
                MinDistance = minDistance,
                Intensity = intensity,
                SpeedIncrease = speedIncrease,
                mode = Mode.Attract
            };

        public static ForceModule Repel(IIntersectable shape, Vector2 position, float minDistance, float maxDistance, 
            float intensity = 10.0f, float speedIncrease = 10.0f) 
            => new ForceModule(shape) {
                Position = position,
                MinDistance = minDistance,
                MaxDistance = maxDistance,
                Intensity = intensity,
                SpeedIncrease = speedIncrease,
                mode = Mode.Repel
            };

        private enum Mode
        {
            Attract,
            Repel
        }
    }
}
