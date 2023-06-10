using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Systems
{
    [BurstCompile]
    public partial class CustomRenderSystem : SystemBase
    {
        private Mesh _mesh;
        private Bounds _bounds;
        private ComputeShader _computeShader;
        private Material _renderMaterial;
        
        private Buffers _team1Buffer;
        private Buffers _team2Buffer;
        private ComponentTypeHandle<LocalToWorld> _ltwHandle;
        private EntityQuery _team1Bees;
        private EntityQuery _team2Bees;

        struct Properties
        {
            //public float3 Position;
            //public float3 TargetDir;
            public float4x4 Ltw;
        }
        
        struct Buffers : IDisposable
        {
            public ComputeBuffer Args;
            public ComputeBuffer GpuProperties;
            public NativeArray<Properties> CpuProperties;

            public void Dispose()
            {
                Args.Dispose();
                GpuProperties.Dispose();
                CpuProperties.Dispose();
            }
        }
        
        protected override void OnCreate()
        {
            _computeShader = RenderSystemBridge.Instance.ComputeShader;
            _renderMaterial = RenderSystemBridge.Instance.RenderMaterial;
            _mesh = CreateQuad();
            _ltwHandle = GetComponentTypeHandle<LocalToWorld>();

            // Boundary surrounding the meshes we will be drawing.  Used for occlusion.
            _bounds = new Bounds(Camera.main.transform.position, Vector3.one * (10 + 1));

            int maxNumBees = Data.beeStartCount;
            _team1Buffer = InitializeBuffers(_mesh, maxNumBees);
            _team2Buffer = InitializeBuffers(_mesh, maxNumBees);
        }
        
        
        protected override void OnDestroy()
        {
            Object.Destroy(_mesh);
            _team1Buffer.Dispose();
            _team2Buffer.Dispose();
        }
        
        private static Buffers InitializeBuffers(Mesh mesh, int num)
        {
            var buffers = new Buffers();
            
            // Argument buffer used by DrawMeshInstancedIndirect.
            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            // Arguments for drawing mesh.
            // 0 == number of triangle indices, 1 == num, others are only relevant if drawing submeshes.
            args[0] = (uint)mesh.GetIndexCount(0);
            args[1] = (uint)num;
            args[2] = (uint)mesh.GetIndexStart(0);
            args[3] = (uint)mesh.GetBaseVertex(0);
            buffers.Args = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            buffers.Args.SetData(args);

            buffers.CpuProperties = new NativeArray<Properties>(num, Allocator.Persistent);

            unsafe
            {
                buffers.GpuProperties = new ComputeBuffer(num, sizeof(Properties));
            }
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

        private void RenderTeamBuffer(Buffers buf)
        {
            int kernel = _computeShader.FindKernel("CSMain");
            buf.GpuProperties.SetData(buf.CpuProperties);
            _computeShader.SetBuffer(kernel, "_Properties", buf.GpuProperties);
            _renderMaterial.SetBuffer("_Properties", buf.GpuProperties);
            _computeShader.Dispatch(kernel, Mathf.CeilToInt(buf.CpuProperties.Length / 64f), 1, 1);
            Graphics.DrawMeshInstancedIndirect(_mesh, 0, _renderMaterial, _bounds, buf.Args);
        }
        
        [BurstCompile]
        private unsafe struct CollectData : IJobChunk
        {
            public ComponentTypeHandle<LocalToWorld> Ltw;
            [NativeDisableParallelForRestriction]
            public NativeArray<Properties> Properties;
            public int ChunkCapacity;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var src = (LocalToWorld*)chunk.GetNativeArray(Ltw).GetUnsafeReadOnlyPtr();
                var dst = (Properties*)Properties.GetUnsafePtr();
                int n = chunk.Count;
                int offset = unfilteredChunkIndex * ChunkCapacity;
                for (int i = 0; i < n; i++)
                {
                    dst[offset + i].Ltw = src[i].Value;
                }
            }
        }
        
        protected override void OnUpdate()
        {
            _ltwHandle.Update(this);
            new CollectData
            {
                Ltw = _ltwHandle,
                Properties = _team1Buffer.CpuProperties,
                //ChunkCapacity = 
            }.ScheduleParallel(_team1Bees, Dependency);
            new CollectData
                        {
                            Ltw = _ltwHandle,
                            Properties = _team1Buffer.CpuProperties,
                            //ChunkCapacity = 
                        }.ScheduleParallel(_team1Bees, Dependency);
            
            RenderTeamBuffer(_team1Buffer);
            RenderTeamBuffer(_team2Buffer);
        }
    }
}