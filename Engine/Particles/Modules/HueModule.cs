using System;
using System.Numerics;
using SE.Engine.Utility;
using SE.Utility;
using Random = SE.Utility.Random;
using static SE.Particles.ParticleMath;

namespace SE.Particles.Modules
{
    public unsafe class HueModule : ParticleModule
    {
        private float[] rand;
        private float[] startHues;
        private float[] randEndHues;

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
            => new HueModule {
                transitionType = transitionType,
                end1 = end1,
                end2 = end2,
                curve = curve.Clone()
            };

        public override void OnInitialize()
        {
            startHues = new float[Emitter.ParticlesLength];
            RegenerateRandom();
        }

        private void RegenerateRandom()
        {
            if (!IsRandom || Emitter == null) 
                return;

            rand = new float[Emitter.ParticlesLength];
            randEndHues = new float[Emitter.ParticlesLength];
        }

        public override void OnParticlesActivated(Span<int> particlesIndex)
        {
            for (int i = 0; i < particlesIndex.Length; i++) {
                int index = particlesIndex[i];
                startHues[index] = Emitter.Particles[index].Color.X;
                if (!IsRandom) 
                    continue;

                rand[particlesIndex[i]] = Random.Next(0.0f, 1.0f);
                randEndHues[i] = Between(end1, end2, rand[i]);
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
                            ParticleMath.Lerp(startHues[i], end1, particle->TimeAlive / particle->InitialLife), 
                            particle->Color.Y, 
                            particle->Color.Z,
                            particle->Color.W);
                    }
                } break;
                case Transition.Curve: {
                    for (Particle* particle = arrayPtr; particle < tail; particle++) {
                        float lifeRatio = particle->TimeAlive / particle->InitialLife;
                        particle->Color = new Vector4(
                            curve.Evaluate(lifeRatio),
                            particle->Color.Y,
                            particle->Color.Z,
                            particle->Color.W);
                    }
                } break;
                case Transition.RandomLerp: {
                    for (Particle* particle = arrayPtr; particle < tail; particle++, i++) {
                        particle->Color = new Vector4(
                            ParticleMath.Lerp(startHues[i], randEndHues[i], particle->TimeAlive / particle->InitialLife),
                            particle->Color.Y,
                            particle->Color.Z,
                            particle->Color.W);
                    }
                } break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static HueModule Lerp(float end)
        {
            HueModule module = new HueModule();
            module.SetLerp(end);
            return module;
        }

        public static HueModule Curve(Curve curve)
        {
            HueModule module = new HueModule();
            module.SetCurve(curve);
            return module;
        }

        public static HueModule RandomLerp(float min, float max)
        {
            HueModule module = new HueModule();
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
