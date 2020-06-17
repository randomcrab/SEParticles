using System;
using static SE.Particles.ParticleMath;
using Vector4 = System.Numerics.Vector4;

namespace SE.Particles.Modules
{
    public class TextureAnimationModule : ParticleModule
    {
        public int SheetRows;
        public int SheetColumns;
        public float Speed;

        private Mode loopMode;

        public void SetOverLifetime(int sheetRows, int sheetColumns)
        {
            SheetRows = sheetRows;
            SheetColumns = sheetColumns;
        }

        public override unsafe void OnUpdate(float deltaTime, Particle* arrayPtr, int length)
        {
            Particle* tail = arrayPtr + length;
            int totalFrames = SheetRows * SheetColumns;
            int frameSize = (int) Emitter.TextureSize.X / SheetRows;
            switch (loopMode) {
                case Mode.Life: {
                    for (Particle* particle = arrayPtr; particle < tail; particle++) {
                        int frame = (int) Between(0.0f, totalFrames, particle->TimeAlive / particle->InitialLife);
#if NETSTANDARD2_1
                        int frameX = (int) MathF.Floor(frame % SheetRows);
                        int frameY = (int) MathF.Floor(frame / SheetRows);
#else
                        int frameX = (int) Math.Floor((double) frame % SheetRows);
                        int frameY = (int) Math.Floor((double) frame / SheetRows);
#endif
                        particle->SourceRectangle = new Vector4(
                            frameX * frameSize, 
                            frameY * frameSize, 
                            frameSize, 
                            frameSize);
                    }
                } break;
                case Mode.Loop: {
                    for (Particle* particle = arrayPtr; particle < tail; particle++) {
                        // TODO:
                    } 
                } break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override ParticleModule DeepCopy()
        {
            return new TextureAnimationModule {
                SheetRows = SheetRows,
                SheetColumns = SheetColumns,
                loopMode = loopMode
            };
        }

        public static TextureAnimationModule OverLifetime(int sheetRows, int sheetColumns)
        {
            TextureAnimationModule mod = new TextureAnimationModule();
            mod.SetOverLifetime(sheetRows, sheetColumns);
            return mod;
        }

        private enum Mode
        {
            Life,
            Loop
        }
    }
}
