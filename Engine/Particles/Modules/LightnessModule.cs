using System;
using System.Numerics;
using SE.Engine.Utility;
using SE.Utility;
using Random = SE.Utility.Random;
using static SE.Particles.ParticleMath;

namespace SE.Particles.Modules
{
    public unsafe class LightnessModule : ParticleModule
    {
        private float[] rand;
        private float[] startLits;
        private float[] randEndLits;

        private Transition transitionType;
        private float end1;
        private float end2;
        private Curve curve;

        private bool IsRandom => transitionType == Transition.RandomLerp;

        public void SetLerp(float end)
        {
            end1 = end;
            transitionType = Transition.Lerp;
        }

        public void SetCurve(Curve curve)
        {
            this.curve = curve;
            transitionType = Transition.Curve;
        }

        public void SetRandomLerp(float min, float max)
        {
            if (min > max)
                Swap(ref min, ref max);

            end1 = min;
            end2 = max;
            transitionType = Transition.RandomLerp;
        }

        public override ParticleModule DeepCopy() 
            => new LightnessModule {
                transitionType = transitionType,
                end1 = end1,
                end2 = end2,
                curve = curve.Clone()
            };

        public override void OnInitialize()
        {
            startLits = new float[Emitter.ParticlesLength];
            RegenerateRandom();
        }

        private void RegenerateRandom()
        {
            if (!IsRandom || Emitter == null) 
                return;

            rand = new float[Emitter.ParticlesLength];
            randEndLits = new float[Emitter.ParticlesLength];
        }

        public override void OnParticlesActivated(Span<int> particlesIndex)
        {
            for (int i = 0; i < particlesIndex.Length; i++) {
                int index = particlesIndex[i];
                startLits[index] = Emitter.Particles[index].Color.Z;
                if (!IsRandom) 
                    continue;

                rand[particlesIndex[i]] = Random.Next(0.0f, 1.0f);
                randEndLits[i] = Between(end1, end2, rand[i]);
            }
        }

        public override void OnUpdate(float deltaTime, Particle* arrayPtr, int length)
        {
            Particle* tail = arrayPtr + length;
            int i = 0;

            switch (transitionType) {
                case Transition.Lerp: {
                    for (Particle* particle = arrayPtr; particle < tail; particle++, i++) {
                        particle->Color = new Vector4(
                            particle->Color.X, 
                            particle->Color.Y, 
                            ParticleMath.Lerp(startLits[i], end1, particle->TimeAlive / particle->InitialLife), 
                            particle->Color.W);
                    }
                } break;
                case Transition.Curve: {
                    for (Particle* particle = arrayPtr; particle < tail; particle++) {
                        float lifeRatio = particle->TimeAlive / particle->InitialLife;
                        particle->Color = new Vector4(
                            particle->Color.X,
                            particle->Color.Y,
                            curve.Evaluate(lifeRatio),
                            particle->Color.W);
                    }
                } break;
                case Transition.RandomLerp: {
                    for (Particle* particle = arrayPtr; particle < tail; particle++, i++) {
                        particle->Color = new Vector4(
                            particle->Color.X,
                            particle->Color.Y,
                            ParticleMath.Lerp(startLits[i], randEndLits[i], particle->TimeAlive / particle->InitialLife),
                            particle->Color.W);
                    }
                } break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static LightnessModule Lerp(float end)
        {
            LightnessModule module = new LightnessModule();
            module.SetLerp(end);
            return module;
        }

        public static LightnessModule Curve(Curve curve)
        {
            LightnessModule module = new LightnessModule();
            module.SetCurve(curve);
            return module;
        }

        public static LightnessModule RandomLerp(float min, float max)
        {
            LightnessModule module = new LightnessModule();
            module.SetRandomLerp(min, max);
            return module;
        }

        private enum Transition
        {
            Lerp,
            Curve,
            RandomLerp
        }
    }
}
