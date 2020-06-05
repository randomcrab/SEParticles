using System;
using System.Collections.Generic;
using SE.Core;
using SE.Utility;

namespace SE.Particles
{
    public class EmitterSet : IDisposable
    {
        private PooledList<Emitter> Emitters { get; } = new PooledList<Emitter>(ParticleEngine.UseArrayPool);
        private bool isDisposed;

        public EmitterSet()
        {
            if (!ParticleEngine.Initialized)
                throw new InvalidOperationException("Particle engine has not been initialized. Call ParticleEngine.Initialize() first.");
        }

        public EmitterSet(Emitter emitter) : this()
            => Emitters.Add(emitter);

        public EmitterSet(Emitter[] emitters) : this()
            => Emitters.AddRange(emitters);

        public EmitterSet(IEnumerable<Emitter> emitters) : this()
            => Emitters.AddRange(emitters);

        public bool Enabled {
            set {
                for (int i = 0; i < Emitters.Count; i++)
                    Emitters.Array[i].Enabled = value;
            }
        }

        public void Emit(int amount = 1)
        {
            Emitter[] emitterArray = Emitters.Array;
            for (int i = 0; i < Emitters.Count; i++) {
                emitterArray[i].Emit(amount);
            }
        }

        public void Add(Emitter emitter) 
            => Emitters.Add(emitter);

        public void Remove(Emitter emitter) 
            => Emitters.Remove(emitter);

        public void Clear() 
            => Emitters.Clear();

        public EmitterSet DeepCopy()
        {
            EmitterSet set = new EmitterSet();
            for (int i = 0; i < Emitters.Count; i++) {
                set.Emitters.Add(Emitters.Array[i].DeepCopy());
            }
            return set;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(isDisposed)
                return;

            foreach (IDisposable disposable in Emitters) {
                disposable.Dispose();
            }
            Emitters.Dispose();
            isDisposed = true;
        }
    }
}
