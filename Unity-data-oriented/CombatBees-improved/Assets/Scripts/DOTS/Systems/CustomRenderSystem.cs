using System;
using System.Threading;
using TMPro.EditorUtilities;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
//using Unity.Rendering;
using Unity.Transforms;
using UnityEditor.Experimental;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CustomRenderSystem : SystemBase
    {
        public Mesh Mesh;
        public Bounds Bounds;
        private ComputeShader _computeShader;
        public Material RenderMaterial1;
        public Material RenderMaterial2;
        
        public Buffers Team1Buffer;
        public Buffers Team2Buffer;
        private NativeReference<int> Counter1;
        private NativeReference<int> Counter2;
        private ComponentTypeHandle<LocalToWorld> m_LtwHandle;
        private EntityQuery m_Bees;
        
        private EntityArchetype m_BeeArchetype;
        private bool m_HaveArchetype;
        private int m_NumBees;
        public uint[] ShaderArgs = new uint[5];
        public int WriteCount;
        public static CustomRenderSystem Instance;
        public JobHandle OutDependency;

        public struct Properties
        {
            //public float3 Position;
            //public float3 TargetDir;
            public float4x4 Ltw;
        }
        
        public struct Buffers : IDisposable
        {
            public ComputeBuffer Args;
            public ComputeBuffer DataA;
            public ComputeBuffer DataB;
            public ComputeBuffer DataC;

            public void Swap()
            {
                (DataA, DataB, DataC) = (DataB, DataC, DataA);
            }

            public void Dispose()
            {
                Args.Dispose();
                DataA.Dispose();
                DataB.Dispose();
                DataC.Dispose();
            }
        }

        public bool Inited { get; private set; }
        private bool TryInit()
        {
            if (Inited)
                return Inited;
            var instance = RenderSystemBridge.Instance;
            if (instance == null)
            {
                return false;
            }
            _computeShader = instance.ComputeShader;
            RenderMaterial1 = new Material(instance.RenderMaterial);
            RenderMaterial2 = new Material(instance.RenderMaterial);
            //Mesh = CreateQuad();
            Mesh = instance.Mesh;

            // Boundary surrounding the meshes we will be drawing.  Used for occlusion.
            Bounds = new Bounds(Vector3.zero, new Vector3(100, 100, 100));

            // Arguments for drawing mesh.
            // 0 == number of triangle indices, 1 == num, others are only relevant if drawing submeshes.
            ShaderArgs[0] = Mesh.GetIndexCount(0);
            ShaderArgs[1] = (uint)WriteCount;
            ShaderArgs[2] = Mesh.GetIndexStart(0);
            ShaderArgs[3] = Mesh.GetBaseVertex(0);
            Team1Buffer = InitializeBuffers(ShaderArgs, WriteCount);
            Team2Buffer = InitializeBuffers(ShaderArgs, WriteCount);

            //World.GetExistingSystemManaged<EntitiesGraphicsSystem>().Enabled = false;
            Inited = true;
            Debug.Log("render init");
            return true;
        }

        protected override void OnCreate()
        {
            Instance = this;
            m_LtwHandle = GetComponentTypeHandle<LocalToWorld>();
            m_Bees = GetEntityQuery(typeof(LocalToWorld), typeof(Team));
            Counter1 = new NativeReference<int>(0, Allocator.Persistent);
            Counter2 = new NativeReference<int>(0, Allocator.Persistent);
        }


        protected override void OnDestroy()
        {
            Counter1.Dispose();
            Counter2.Dispose();
            if (!Inited)
                return;
            Object.Destroy(Mesh);
            Object.Destroy(RenderMaterial1);
            Object.Destroy(RenderMaterial2);
            Team1Buffer.Dispose();
            Team2Buffer.Dispose();
        }
        
        private static Buffers InitializeBuffers(uint[] shaderArgs, int num)
        {
            var buffers = new Buffers();
            buffers.Args = new ComputeBuffer(1, shaderArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            buffers.Args.SetData(shaderArgs);
            int size;
            unsafe
            {
                size = sizeof(Properties);
            }
            buffers.DataA = new ComputeBuffer(num, size, ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);
            buffers.DataB = new ComputeBuffer(num, size, ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);
            buffers.DataC = new ComputeBuffer(num, size, ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);
            return buffers;
        }

        private Mesh CreateQuad(float width = 1f, float height = 1f)
        {
            // Create a quad mesh.
            var mesh = new Mesh();

            float w = width * .5f;
            float h = height * .5f;
            var vertices = new Vector3[4] {
                new(-w, -h, 0),
                new(w, -h, 0),
                new(-w, h, 0),
                new(w, h, 0)
            };

            var tris = new int[6] {
                // lower left tri.
                0, 2, 1,
                // lower right tri
                2, 3, 1
            };

            var normals = new Vector3[4] {
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
            };

            var uv = new Vector2[4] {
                new(0, 0),
                new(1, 0),
                new(0, 1),
                new(1, 1),
            };

            mesh.vertices = vertices;
            mesh.triangles = tris;
            mesh.normals = normals;
            mesh.uv = uv;

            return mesh;
        }

        /*private void RenderTeamBuffer(in Buffers buf, Color color)
        {
            //int kernel = _computeShader.FindKernel("CSMain");
            //_computeShader.SetBuffer(kernel, "_Properties", buf.Data);
            //_computeShader.Dispatch(kernel, Mathf.CeilToInt((m_NumBees / 2) / 64f), 1, 1);
            RenderMaterial.SetBuffer(PropertiesId, buf.Data);
            RenderMaterial.SetColor(ColorId, color);
            buf.Args.SetData(ShaderArgs);
            Debug.Log("DrawMeshInstancedIndirect(" + Mesh + ", 0, " + RenderMaterial + ", " + Bounds + ", " + buf.Args + ")");
            Graphics.DrawMeshInstancedIndirect(Mesh, 0, RenderMaterial, Bounds, buf.Args);
        }*/
        
        [BurstCompile]
        private unsafe struct CollectData : IJobChunk
        {
            [ReadOnly]
            public ComponentTypeHandle<LocalToWorld> Ltw;
            [NativeDisableParallelForRestriction]
            public NativeArray<Properties> Properties;
            [NativeDisableParallelForRestriction]
            public NativeReference<int> Counter;
            public int ChunkCapacity;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var src = (LocalToWorld*)chunk.GetNativeArray(ref Ltw).GetUnsafeReadOnlyPtr();
                var dst = (Properties*)Properties.GetUnsafePtr();
                int n = chunk.Count;
                int chunkIndex = Interlocked.Increment(ref *Counter.GetUnsafePtr()) - 1;
                int offset = chunkIndex * ChunkCapacity;
                for (int i = 0; i < n; i++)
                {
                    Properties[offset + i] = new Properties
                    {
                        Ltw = src[i].Value
                    };
                }
            }
        }

        private bool FindBeeArchetype()
        {
            var archetypes = new NativeList<EntityArchetype>(Allocator.Temp);
            EntityManager.GetAllArchetypes(archetypes);
            foreach (var a in archetypes)
            {
                if (a.Prefab || a.ChunkCount == 0)
                    continue;
                var comps = a.GetComponentTypes();
                foreach (var c in comps)
                {
                    if (c.GetManagedType() == typeof(Team))
                    {
                        m_BeeArchetype = a;
                        m_HaveArchetype = true;
                        return true;
                    }
                }
            }

            return false;
        }
        
        protected override unsafe void OnUpdate()
        {
            m_LtwHandle.Update(this);
            if (m_Bees.IsEmptyIgnoreFilter)
                return;

            if (!m_HaveArchetype)
            {
                if (!FindBeeArchetype())
                    return;
                Debug.Log("Found bee archetype, num bees: " + m_Bees.CalculateEntityCountWithoutFiltering() + ", chunk size: " + m_BeeArchetype.ChunkCapacity);
                WriteCount = (m_BeeArchetype.ChunkCapacity * m_BeeArchetype.ChunkCount) / 2;
            }
            
            if (!TryInit())
                return;
            
            Counter1.Value = Counter2.Value = 0;
            var d = Dependency;
            m_Bees.SetSharedComponentFilter(new Team { Value = 1 });
            JobHandle j1 = new CollectData
            {
                Ltw = m_LtwHandle,
                Properties = Team1Buffer.DataA.BeginWrite<Properties>(0, WriteCount),
                ChunkCapacity = m_BeeArchetype.ChunkCapacity,
                Counter = Counter1,
            }.ScheduleParallel(m_Bees, d);
            m_Bees.SetSharedComponentFilter(new Team { Value = 2 }); 
            JobHandle j2 = new CollectData
            {
                Ltw = m_LtwHandle,
                Properties = Team2Buffer.DataA.BeginWrite<Properties>(0, WriteCount),
                ChunkCapacity = m_BeeArchetype.ChunkCapacity,
                Counter = Counter2,
            }.ScheduleParallel(m_Bees, d);
            OutDependency = JobHandle.CombineDependencies(j1, j2);
            Dependency = OutDependency;
        }
    }
}