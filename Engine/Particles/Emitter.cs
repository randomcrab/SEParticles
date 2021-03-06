﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using SE.Core;
using SE.Particles.AreaModules;
using SE.Particles.Modules;
using SE.Particles.Shapes;
using SE.Utility;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;
using Random = SE.Utility.Random;
using static SE.Particles.ParticleMath;

#if MONOGAME
using Microsoft.Xna.Framework.Graphics;
#endif

// ReSharper disable ConvertToAutoPropertyWhenPossible

namespace SE.Particles
{
    /// <summary>
    /// Core of the particle engine. Emitters hold a buffer of particles, and a list of <see cref="ParticleModule"/>.
    /// </summary>
    public unsafe class Emitter : IDisposable
    {
        public IAdditionalData AdditionalData;
        public IEmitterShape Shape;
        public Space Space;
        public BlendMode BlendMode;

        public bool ParallelEmission = true;
        public EmitterConfig Config;

        public Vector2 Size {
            get => size;
            set {
                try {
                    if (value.X <= 0 || value.Y <= 0)
                        throw new InvalidEmitterValueException($"{nameof(Size)} must have values greater than zero.");
                    
                    size = value;
                    Bounds = new Vector4(Position.X - (size.X / 2.0f), Position.Y - (size.Y / 2.0f), size.X, size.Y);
                } catch (Exception) {
                    if (ParticleEngine.ErrorHandling == ErrorHandling.Throw) 
                        throw;

                    size = new Vector2(
                        Clamp(value.X, 1.0f, float.MaxValue), 
                        Clamp(value.Y, 1.0f, float.MaxValue));
                    Bounds = new Vector4(
                        Position.X - (size.X / 2.0f), 
                        Position.Y - (size.Y / 2.0f), 
                        size.X, 
                        size.Y);
                }
            }
        }
        private Vector2 size;

        public Vector2 TextureSize {
            get => textureSize;
            set {
                try {
                    if (value.X <= 0 || value.Y <= 0)
                        throw new InvalidEmitterValueException($"{nameof(TextureSize)} must have values greater than zero.");

                    textureSize = value;
                } catch (Exception) {
                    if (ParticleEngine.ErrorHandling == ErrorHandling.Throw) 
                        throw;

                    textureSize = new Vector2(
                        Clamp(value.X, 1.0f, float.MaxValue), 
                        Clamp(value.Y, 1.0f, float.MaxValue));
                }
            }
        }
        private Vector2 textureSize;

        public Vector4 StartRect {
            get => startRect;
            set {
                try {
                    if (value.Z <= 0 || value.W <= 0 || value.X > value.Z || value.Y > value.W)
                        throw new InvalidEmitterValueException($"{nameof(StartRect)} is not a valid source rectangle.");

                    startRect = value;
                } catch (Exception) {
                    if (ParticleEngine.ErrorHandling == ErrorHandling.Throw) 
                        throw;

                    if (value.Z < 1.0f)         value.Z = 1.0f;
                    if (value.W < 1.0f)         value.W = 1.0f;
                    if (value.X < 1.0f)         value.X = 1.0f;
                    else if (value.X > value.Z) value.X = value.Z;
                    if (value.Y < 1.0f)         value.Y = 1.0f;
                    else if (value.Y > value.W) value.Y = value.W;
                    startRect = value;
                }
            }
        }
        private Vector4 startRect;

#if MONOGAME
        public Texture2D Texture {
            get => texture;
            set {
                texture = value ?? throw new InvalidEmitterValueException(new NullReferenceException());
                TextureSize = new Vector2(texture.Width, texture.Height);
            }
        }
        private Texture2D texture;
#endif

        internal int ParticleEngineIndex = -1;
        internal float TimeToLive;
        internal Particle[] Particles;
        
        private HashSet<AreaModule> areaModules = new HashSet<AreaModule>();
        private PooledList<ParticleModule> modules = new PooledList<ParticleModule>(ParticleEngine.UseArrayPool);
        private int[] newParticles;
        private int numActive;
        private int numNew;
        private int capacity;
        private Vector2 lastPosition;
        private bool isDisposed;
        private bool firstUpdate = true;

        public Vector2 Position {
            get => Shape.Center;
            set {
                Shape.Center = value;
                Bounds = new Vector4(Position.X - (size.X / 2.0f), Position.Y - (size.Y / 2.0f), size.X, size.Y);
            }
        }

        public float Rotation {
            get => Shape.Rotation;
            set => Shape.Rotation = value;
        }

        public Vector4 Bounds { get; private set; } // X, Y, Width, Height

        public int ParticlesLength => capacity;
        public ref Particle GetParticle(int index) => ref Particles[index];
        public Span<Particle> ActiveParticles => new Span<Particle>(Particles, 0, numActive);
        private Span<int> NewParticleIndexes => new Span<int>(newParticles, 0, numNew);

        /// <summary>Controls whether or not the emitter will emit new particles.</summary>
        public bool EmissionEnabled { get; set; } = true;

        /// <summary>Enabled/Disabled state. Disabled emitters are not updated or registered to the particle engine.</summary>
        public bool Enabled {
            get => enabled;
            set {
                enabled = value;
                if (enabled)
                    ParticleEngine.AddEmitter(this);
                else
                    ParticleEngine.RemoveEmitter(this);
            }
        }
        private bool enabled;

        public Emitter(Vector2 size, int capacity = 2048, IEmitterShape shape = null)
        {
            if (!ParticleEngine.Initialized)
                throw new InvalidOperationException("Particle engine has not been initialized. Call ParticleEngine.Initialize() first.");

            this.capacity = capacity;
            Config = new EmitterConfig();
            Shape = shape ?? new PointEmitterShape();
            Size = size;
            Position = Vector2.Zero;
            TextureSize = new Vector2(128, 128);         // Dummy value.
            StartRect = new Vector4(0, 0, 128, 128); // Dummy value.
            switch (ParticleEngine.AllocationMode) {
                case ParticleAllocationMode.ArrayPool:
                    Particles = ArrayPool<Particle>.Shared.Rent(capacity);
                    newParticles = ArrayPool<int>.Shared.Rent(capacity);
                    break;
                case ParticleAllocationMode.Array:
                    Particles = new Particle[capacity];
                    newParticles = new int[capacity];
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            for (int i = 0; i < capacity; i++) {
                Particles[i] = Particle.Default;
            }
            Enabled = true;
        }

        public Emitter(int capacity = 2048, IEmitterShape shape = null) 
            : this(new Vector2(512.0f, 512.0f), capacity, shape) { }

        internal void Update(float deltaTime)
        {
            ParticleModule[] modules = this.modules.Array;
            if (firstUpdate) {
                lastPosition = Position;
                firstUpdate = false;
            }

            // Update bounds.
            Bounds = new Vector4(Position.X - (Size.X / 2), Position.Y - (Size.Y / 2), Size.X, Size.Y);

            // Inform the modules of newly activated particles.
            for (int i = 0; i < this.modules.Count; i++) {
                modules[i].OnParticlesActivated(NewParticleIndexes);
            }
            numNew = 0;

            fixed (Particle* ptr = Particles) {
                Particle* tail = ptr + numActive;
                int i = 0;

                // Update the particles, and deactivate those whose TTL <= 0.
                for (Particle* particle = ptr; particle < tail; particle++, i++) {
                    particle->TimeAlive += deltaTime;
                    if (particle->TimeAlive >= particle->InitialLife) {
                        DeactivateParticleInternal(particle, i);
                    }
                }

                // Update enabled modules.
                for (i = 0; i < this.modules.Count; i++) {
                    if(!modules[i].Enabled)
                        continue;

                    modules[i].OnUpdate(deltaTime, ptr, numActive);
                }

                // Update the area modules influencing this emitter.
                foreach (AreaModule areaModule in areaModules) {
                    areaModule.ProcessParticles(deltaTime, ptr, numActive);
                }

                // Update particle positions.
                tail = ptr + numActive;
                switch (Space) {
                    case Space.World: {
                        for (Particle* particle = ptr; particle < tail; particle++) {
                            particle->Position += particle->Direction * particle->Speed * deltaTime;
                        }
                    } break;
                    case Space.Local: {
                        for (Particle* particle = ptr; particle < tail; particle++) {
                            particle->Position += (particle->Direction * particle->Speed * deltaTime) + (Position - lastPosition);
                        }
                    } break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            lastPosition = Position;
        }

        internal void CheckParticleIntersections(QuickList<Particle> list, AreaModule areaModule)
        {
            Span<Particle> particles = ActiveParticles;
            for (int i = 0; i < capacity; i++) {
                ref Particle particle = ref particles[i];
                if (areaModule.Shape.Intersects(particle.Position)) {
                    list.Add(particle);
                }
            }
        }

        public void Clear()
        {
            numActive = 0;
            numNew = 0;
        }

        /// <summary>
        /// Deactivates a particles at specified index.
        /// </summary>
        /// <param name="index">Index of particle to deactivate.</param>
        public void DeactivateParticle(int index)
        {
            if (index > numActive || index < 0)
                throw new IndexOutOfRangeException(nameof(index));

            fixed (Particle* particle = &Particles[index]) {
                DeactivateParticleInternal(particle, index);
            }
        }

        // Higher performance deactivation function with fewer safety checks.
        private void DeactivateParticleInternal(Particle* particle, int index)
        {
            particle->Position = new Vector2(float.MinValue, float.MinValue);
            numActive--;
            if (index != numActive) {
                Particles[index] = Particles[numActive];
            }
        }

        public void Emit(int amount = 1)
        {
            amount = (int) Clamp(amount, 1.0f, capacity - numActive);
            if (!enabled || !EmissionEnabled)
                return;
            if(numActive + amount > capacity)
                return;

            bool shouldMultiThread = amount >= 2048 / Environment.ProcessorCount;
            if (shouldMultiThread && ParallelEmission) {
                Parallel.For(0, amount, i => {
                    EmitParticle(i, amount);
                });
            } else {
                for (int i = 0; i < amount; i++) {
                    EmitParticle(i, amount);
                }
            }

            numActive += amount;
            numNew += amount;
        }

        private void EmitParticle(int i, int maxIteration)
        {
            fixed (Particle* particle = &Particles[numActive + i]) {
                Shape.Get((float)i / maxIteration, out particle->Position, out particle->Direction);
                particle->Position += Position;
                particle->TimeAlive = 0.0f;
                particle->SourceRectangle = StartRect;

                // Configure particle speed.
                EmitterConfig.SpeedConfig speed = Config.Speed;
                switch (Config.Speed.StartValueType) {
                    case EmitterConfig.StartingValue.Normal: {
                        particle->Speed = speed.Min;
                    } break;
                    case EmitterConfig.StartingValue.Random: {
                        particle->Speed = Between(speed.Min, speed.Max, Random.Next());
                    } break;
                    case EmitterConfig.StartingValue.RandomCurve: {
                        lock (speed.Curve) {
                            particle->Speed = speed.Curve.Evaluate(Random.Next());
                        }
                    } break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Configure particle scale.
                EmitterConfig.ScaleConfig scale = Config.Scale;
                switch (Config.Scale.StartValueType) {
                    case EmitterConfig.StartingValue.Normal: {
                        particle->Scale = scale.Min;
                    } break;
                    case EmitterConfig.StartingValue.Random: {
                        if (scale.TwoDimensions) {
                            particle->Scale = new Vector2(
                                Between(scale.Min.X, scale.Max.X, Random.Next()),
                                Between(scale.Min.Y, scale.Max.Y, Random.Next()));
                        } else {
                            float s = Random.Next();
                            particle->Scale = new Vector2(
                                Between(scale.Min.X, scale.Max.X, s),
                                Between(scale.Min.Y, scale.Max.Y, s));
                        }
                    } break;
                    case EmitterConfig.StartingValue.RandomCurve: {
                        if (scale.TwoDimensions) {
                            lock (scale.Curve) {
                                particle->Scale = new Vector2(
                                    scale.Curve.X.Evaluate(Random.Next()), 
                                    scale.Curve.Y.Evaluate(Random.Next()));
                            }
                        } else {
                            float rand = Random.Next();
                            lock (scale.Curve) { 
                                particle->Scale = new Vector2(
                                    scale.Curve.X.Evaluate(rand), 
                                    scale.Curve.Y.Evaluate(rand));
                            }
                        }
                    } break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Configure particle color.
                EmitterConfig.ColorConfig color = Config.Color;
                switch (Config.Color.StartValueType) {
                    case EmitterConfig.StartingValue.Normal: {
                        particle->Color = color.Min;
                    } break;
                    case EmitterConfig.StartingValue.Random: {
                        particle->Color = new Vector4(
                            Between(color.Min.X, color.Max.X, Random.Next()),
                            Between(color.Min.Y, color.Max.Y, Random.Next()),
                            Between(color.Min.Z, color.Max.Z, Random.Next()),
                            Between(color.Min.W, color.Max.W, Random.Next()));
                    } break;
                    case EmitterConfig.StartingValue.RandomCurve: {
                        lock (color.Curve) {
                            particle->Color = new Vector4(
                                color.Curve.X.Evaluate(Random.Next()), 
                                color.Curve.Y.Evaluate(Random.Next()),
                                color.Curve.Z.Evaluate(Random.Next()),
                                color.Curve.W.Evaluate(Random.Next()));
                        }
                    } break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Configure particle life.
                EmitterConfig.LifeConfig life = Config.Life;
                switch (Config.Life.StartValueType) {
                    case EmitterConfig.StartingValue.Normal: {
                        particle->InitialLife = life.Min;
                    } break;
                    case EmitterConfig.StartingValue.Random: {
                        particle->InitialLife = Between(life.Min, life.Max, Random.Next());
                    } break;
                    case EmitterConfig.StartingValue.RandomCurve: {
                        lock (life.Curve) {
                            particle->InitialLife = life.Curve.Evaluate(Random.Next());
                        }
                    } break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            newParticles[numNew + i] = numActive + i;
        }

        public bool RemoveModule(ParticleModule module) 
            => module == null 
                ? throw new ArgumentNullException(nameof(module)) 
                : modules.Remove(module);

        public bool RemoveModules(params ParticleModule[] modules)
        {
            bool b = false;
            for (int i = modules.Length - 1; i >= 0; i--) {
                if (RemoveModule(modules[i])) {
                    b = true;
                }
            }
            return b;
        }

        public void RemoveModules<T>() where T : ParticleModule 
            => RemoveModules(typeof(T));

        public bool RemoveModules(Type moduleType)
        {
            if (RemoveModule(moduleType)) {
                while (RemoveModule(moduleType)) { }
                return true;
            }
            return false;
        }

        public bool RemoveModule<T>() where T : ParticleModule
            => RemoveModule(typeof(T));

        public bool RemoveModule(Type moduleType)
        {
            if (moduleType == null)
                throw new ArgumentNullException(nameof(moduleType));
            if (!moduleType.IsSubclassOf(typeof(ParticleModule)))
                throw new ArgumentException("Type is not of particle module.", nameof(moduleType));

            ParticleModule[] arr = modules.Array;
            for (int i = modules.Count - 1; i >= 0; i--) {
                if (arr[i].GetType() == moduleType) {
                    modules.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public T GetModule<T>() where T : ParticleModule 
            => (T) GetModule(typeof(T));

        public ParticleModule GetModule(Type moduleType)
        {
            if (moduleType == null)
                throw new ArgumentNullException(nameof(moduleType));
            if (!moduleType.IsSubclassOf(typeof(ParticleModule)))
                throw new ArgumentException("Type is not of particle module.", nameof(moduleType));

            ParticleModule[] arr = modules.Array;
            for (int i = 0; i < modules.Count; i++) {
                if (arr[i].GetType() == moduleType) {
                    return arr[i];
                }
            }
            return null;
        }

        public IEnumerable<T> GetModules<T>() where T : ParticleModule 
            => GetModules(typeof(T)) as IEnumerable<T>;

        public IEnumerable<ParticleModule> GetModules(Type moduleType)
        {
            if (moduleType == null)
                throw new ArgumentNullException(nameof(moduleType));
            if (!moduleType.IsSubclassOf(typeof(ParticleModule)))
                throw new ArgumentException("Type is not of particle module.", nameof(moduleType));

            QuickList<ParticleModule> tmpModules = new QuickList<ParticleModule>();
            ParticleModule[] arr = modules.Array;
            for (int i = 0; i < modules.Count; i++) {
                if (arr[i].GetType() == moduleType) {
                    tmpModules.Add(arr[i]);
                }
            }
            return tmpModules;
        }

        public void AddModule(ParticleModule module)
        {
            if (module == null)
                throw new ArgumentException(nameof(module));

            modules.Add(module);
            module.Emitter = this;
            module.OnInitialize();
        }

        public void AddModules(params ParticleModule[] modules)
        {
            foreach (ParticleModule module in modules)
                AddModule(module);
        }

        
        internal void AddAreaModule(AreaModule mod)
        {
            lock (areaModules)
                areaModules.Add(mod);
        }

        internal void RemoveAreaModule(AreaModule mod)
        {
            lock (areaModules)
                areaModules.Remove(mod);
        }

        internal HashSet<AreaModule> GetAreaModules()
        {
            lock (areaModules)
                return areaModules;
        }

        public Emitter DeepCopy()
        {
            Emitter emitter = new Emitter(capacity) {
                Config = Config.DeepCopy(),
                Position = Position
            };
            for (int i = 0; i < modules.Count; i++) {
                emitter.AddModule(modules.Array[i].DeepCopy());
            }
            return emitter;
        }

        public void DisposeAfter(float? time = null)
        {
            TimeToLive = time ?? Config.Life.maxLife;
            ParticleEngine.DestroyPending(this);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(isDisposed)
                return;

            Enabled = false;
            switch (ParticleEngine.AllocationMode) {
                case ParticleAllocationMode.ArrayPool:
                    ArrayPool<Particle>.Shared.Return(Particles);
                    ArrayPool<int>.Shared.Return(newParticles);
                    break;
                case ParticleAllocationMode.Array:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            modules.Dispose();
            isDisposed = true;
        }
    }

    public enum Space
    {
        World,
        Local
    }

    public enum BlendMode
    {
        Alpha,
        Additive,
        Subtractive
    }
}
