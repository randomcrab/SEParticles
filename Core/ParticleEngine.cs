using System;
using System.Numerics;
using System.Threading.Tasks;
using SE.Core.Extensions;
using SE.Particles;
using SE.Particles.AreaModules;
using SE.Particles.Shapes;
using SE.Utility;
using Vector2 = System.Numerics.Vector2;

namespace SE.Core
{
    // TODO: Rename SEParticles with MonoGame support SEParticles.MonoGame, and regular just SEParticles.
    public static class ParticleEngine
    {
        public static readonly QuickList<Emitter> Emitters = new QuickList<Emitter>();
        public static readonly QuickList<Emitter> VisibleEmitters = new QuickList<Emitter>();
        public static readonly QuickList<AreaModule> AreaModules = new QuickList<AreaModule>();

        public static bool UseArrayPool => AllocationMode == ParticleAllocationMode.ArrayPool;

        public static int ParticleCount { 
            get  { 
                int total = 0;
                foreach (Emitter emitter in Emitters) {
                    total += emitter.ActiveParticles.Length;
                }
                return total;
            }
        }

        /// <summary>Controls how particles are allocated for new emitters. Cannot be changed after Initialize() has been called.</summary>
        public static ParticleAllocationMode AllocationMode {
            get => allocationMode;
            set {
                if (Initialized)
                    throw new InvalidOperationException("Cannot change allocation mode after the particle engine has been initialized.");

                allocationMode = value;
            }
        }
        private static ParticleAllocationMode allocationMode = ParticleAllocationMode.ArrayPool;

        /// <summary>Controls how the particle engine handles minor exceptions. Cannot be changed after Initialize() has been called.</summary>
        public static ErrorHandling ErrorHandling {
            get => errorHandling;
            set {
                if (Initialized)
                    throw new InvalidOperationException("Cannot change error handling mode after the particle engine has been initialized.");

                errorHandling = value;
            }
        }
        private static ErrorHandling errorHandling = ErrorHandling.Stability;

        /// <summary>Controls how the particle engine updates.</summary>
        public static UpdateMode UpdateMode = UpdateMode.ParallelAsynchronous;
        public static bool Initialized { get; private set; }

        private static Vector4[] tmpViewArr = new Vector4[1];
        private static QuickList<Emitter> pendingDestroy = new QuickList<Emitter>();
        private static Task updateTask;
        private static bool temp = true;

        public static void Initialize()
        {
            Initialized = true;
        }

        public static void Update(float deltaTime, Vector4 viewBounds)
        {
            tmpViewArr[0] = viewBounds;
            Update(deltaTime, tmpViewArr);
        }

        public static void Update(float deltaTime, Span<Vector4> viewBounds = default)
        {
            if (!Initialized)
                throw new InvalidOperationException("Particle engine has not been initialized. Call ParticleEngine.Initialize() first.");

            FindVisible(viewBounds);
            switch (UpdateMode) {
                case UpdateMode.ParallelAsynchronous: {
                    CreateTasks(deltaTime);
                } break;
                case UpdateMode.ParallelSynchronous: {
                    CreateTasks(deltaTime);
                    WaitForThreads();
                } break;
                case UpdateMode.Synchronous: {
                    // Update area modules.
                    foreach (AreaModule aMod in AreaModules) {
                        for (int i = 0; i < Emitters.Count; i++) {
                            Emitter e = Emitters.Array[i];
                            if (aMod.Shape.Intersects(e.Bounds)) {
                                e.AddAreaModule(aMod);
                                aMod.AddEmitter(e);
                            } else {
                                e.RemoveAreaModule(aMod);
                                aMod.RemoveEmitter(e);
                            }
                        }
                    }

                    // Update emitters.
                    for (int i = 0; i < VisibleEmitters.Count; i++) {
                        VisibleEmitters.Array[i].Update(deltaTime);
                    }
                } break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // TODO: Temp code. Remove when done.
            if (temp) {
                ForceModule mod = ForceModule.Attract(
                    new CircleShape(1024.0f), 
                    new Vector2(1024.0f, 1024.0f), 
                    256.0f, 
                    1024.0f);

                //AreaModules.Add(mod);

                //AttractorModule mod2 = new AttractorModule(new CircleShape(512.0f), new System.Numerics.Vector2(2048.0f, 1024.0f));
                //AreaModules.Add(mod2);

                temp = false;
            }

            DestroyPending(deltaTime);
        }

        private static void FindVisible(Span<Vector4> viewBounds)
        {
            VisibleEmitters.Clear();
            if (viewBounds == default) {
                VisibleEmitters.AddRange(Emitters);
            } else {
                for (int i = 0; i < Emitters.Count; i++) {
                    Emitter emitter = Emitters.Array[i];
                    if (CheckIntersection(emitter.Bounds, viewBounds)) {
                        VisibleEmitters.Add(emitter);
                    } else {
                        emitter.Clear();
                    }
                }
            }
        }

        private static void CreateTasks(float deltaTime)
        {
            updateTask = Task.Factory.StartNew(() => {
                Parallel.ForEach(AreaModules, aMod => {
                    foreach (Emitter e in Emitters) {
                        if (aMod.Shape.Intersects(e.Bounds)) {
                            e.AddAreaModule(aMod);
                            aMod.AddEmitter(e);
                        } else {
                            e.RemoveAreaModule(aMod);
                            aMod.RemoveEmitter(e);
                        }
                    }
                });
            }).ContinueWith(t1 => {
                // Update emitters.
                Parallel.ForEach(VisibleEmitters, emitter => {
                    emitter.Update(deltaTime);
                });
            });
        }


        private static void DestroyPending(float deltaTime)
        {
            Emitter[] pendingDestroyArray = pendingDestroy.Array;
            for (int i = pendingDestroy.Count - 1; i >= 0; i--) {
                Emitter emitter = pendingDestroyArray[i];
                emitter.TimeToLive -= deltaTime;
                if (emitter.TimeToLive <= 0.0f) {
                    emitter.Dispose();
                }
            }
        }

        public static void WaitForThreads()
        {
            if (!Initialized)
                throw new InvalidOperationException("Particle engine has not been initialized. Call ParticleEngine.Initialize() first.");

            if (updateTask != null && !updateTask.IsCompleted) {
                updateTask.Wait();
            }
        }

        internal static void AddEmitter(Emitter emitter)
        {
            if(emitter.ParticleEngineIndex != -1)
                return;

            emitter.ParticleEngineIndex = Emitters.Count;
            Emitters.Add(emitter);
            foreach (AreaModule aModule in emitter.GetAreaModules()) {
                aModule.AddEmitter(emitter);
            }
        }

        internal static void RemoveEmitter(Emitter emitter)
        {
            if(emitter.ParticleEngineIndex == -1)
                return;

            Emitters.Remove(emitter);
            emitter.ParticleEngineIndex = -1;
            foreach (AreaModule aModule in emitter.GetAreaModules()) {
                aModule.RemoveEmitter(emitter);
            }
            pendingDestroy.Remove(emitter);
        }

        internal static void AddAreaModule(AreaModule module)
        {
            if(module.AddedToEngine)
                return;

            module.AddedToEngine = true;
            AreaModules.Add(module);
            foreach (Emitter e in module.GetEmitters()) {
                e.AddAreaModule(module);
            }
        }

        internal static void RemoveAreaModule(AreaModule module)
        {
            module.AddedToEngine = false;
            AreaModules.Remove(module);
            foreach (Emitter e in module.GetEmitters()) {
                e.RemoveAreaModule(module);
            }
        }

        internal static void DestroyPending(Emitter emitter)
        {
            pendingDestroy.Add(emitter);
        }

        private static bool CheckIntersection(Vector4 bounds, Span<Vector4> otherBounds)
        {
            for (int i = 0; i < otherBounds.Length; i++) {
                if (bounds.Intersects(otherBounds[i])) {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Controls how particles are allocated for new emitters.
    /// </summary>
    public enum ParticleAllocationMode
    {
        /// <summary>Particles are allocated using standard arrays. Most useful when fewer emitters are created and destroyed at runtime,
        ///          such as when particle emitters are pooled.</summary>
        Array,

        /// <summary>Particles are allocated using arrays, which are rented and returned to the shared array pool.
        ///          This option will result in less garbage generation, and faster instantiation of new particle emitters.
        ///          Most useful when creating and destroying many particle emitters at runtime.
        ///          However, this option may result in a buildup of memory usage due to how ArrayPool.Shared internally works.</summary>
        ArrayPool
    }

    /// <summary>
    /// Controls how the particle engine updates.
    /// </summary>
    public enum UpdateMode
    {
        /// <summary>Update is done using Parallel loops, within tasks. ParticleEngine.WaitForThreads() must be called when the state
        ///          is to be synchronized. For example, ParticleEngine.WaitForThreads() would be called before you render or query the particles.
        ///          Results in better performance on machines with >2 cores.</summary>
        ParallelAsynchronous,

        /// <summary>Update is done synchronously using Parallel loops. Results in betters performance on machines with >2 cores.
        ///          State is synchronized immediately after Update() has finished processing.</summary>
        ParallelSynchronous,

        /// <summary>Update is done synchronously on whatever thread Update() was called from. Results in lower performance on machines with
        ///          >2 cores, and potentially better performance on machines with 1-2 cores.</summary>
        Synchronous
    }

    /// <summary>
    /// Controls how the particle engine handles certain exceptions.
    /// </summary>
    public enum ErrorHandling
    {
        /// <summary>The Engine will prioritize stability, and will attempt to correct certain exceptions. Exceptions related to emitter
        ///          values, such as invalid bounds, sizes, etc, will not be thrown. Exceptions that can't be corrected will still be thrown.</summary>
        Stability,

        /// <summary>Exceptions will be thrown whenever values are invalid. This is useful for debugging and fine-tuning particle emitters.</summary>
        Throw
    }
}
