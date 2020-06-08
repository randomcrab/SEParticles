using System.Collections.Generic;
using System.Numerics;
using SE.Core;
using SE.Particles.Shapes;

namespace SE.Particles.AreaModules
{
    public abstract class AreaModule
    {
        internal bool AddedToEngine = false;
        private HashSet<Emitter> AttachedEmitters = new HashSet<Emitter>();

        public Vector2 Position {
            get => Shape.Center;
            set => Shape.Center = value;
        }

        public bool Enabled {
            get => enabled;
            set {
                enabled = value;
                if (enabled)
                    ParticleEngine.AddAreaModule(this);
                else
                    ParticleEngine.RemoveAreaModule(this);
            }
        }
        private bool enabled = true;

        public IIntersectable Shape { get; set; }

        public AreaModule(IIntersectable shape, Vector2? position = null)
        {
            Shape = shape;
            if (position.HasValue) {
                Position = position.Value;
            }
        }

        internal void AddEmitter(Emitter e)
        {
            lock (AttachedEmitters)
                AttachedEmitters.Add(e);
        }

        internal void RemoveEmitter(Emitter e)
        {
            lock (AttachedEmitters)
                AttachedEmitters.Remove(e);
        }

        internal HashSet<Emitter> GetEmitters()
        {
            lock(AttachedEmitters)
                return AttachedEmitters;
        }

        public abstract unsafe void ProcessParticles(float deltaTime, Particle* particles, int length);
    }
}
