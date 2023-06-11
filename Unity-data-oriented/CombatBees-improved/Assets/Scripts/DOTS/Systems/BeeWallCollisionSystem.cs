using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(BeePositionUpdateSystem))]
    [UpdateAfter(typeof(BeeInitSystem))]
    public partial struct BeeWallCollisionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer.ParallelWriter ecb = GetEntityCommandBuffer(ref state);

            state.Dependency = new WallCollisionJob
            {
                Ecb = ecb,
                deltaTime = state.WorldUnmanaged.Time.DeltaTime

            }.ScheduleParallel(state.Dependency);           
        }

        private EntityCommandBuffer.ParallelWriter GetEntityCommandBuffer(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            return ecb.AsParallelWriter();
        }

        [BurstCompile]
        public partial struct WallCollisionJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public float deltaTime;


            // IJobEntity generates a component data query based on the parameters of its `Execute` method.
            // This example queries for all Spawner components and uses `ref` to specify that the operation
            // requires read and write access. Unity processes `Execute` for each entity that matches the
            // component data query.
            private void Execute(ref LocalTransform transform, ref Velocity velocity)
            {
                var position = transform.Position;
                if (math.abs(position.x) > DataBurst.FieldSize.x * .5f)
                {
                    position.x = (DataBurst.FieldSize.x * .5f) *  math.sign(position.x);
                    velocity.Value.x *= -.5f;
                    velocity.Value.y *= .8f;
                    velocity.Value.z *= .8f;
                }
                if (math.abs(position.z) > DataBurst.FieldSize.z * .5f)
                {
                    position.z = (DataBurst.FieldSize.z * .5f) * math.sign(position.z);
                    velocity.Value.z *= -.5f;
                    velocity.Value.x *= .8f;
                    velocity.Value.y *= .8f;
                }

                if (math.abs(position.y) > DataBurst.FieldSize.y * .5f)
                {
                    position.y = (DataBurst.FieldSize.y * .5f) * math.sign(position.y);
                    velocity.Value.y *= -.5f;
                    velocity.Value.z *= .8f;
                    velocity.Value.x *= .8f;
                }
                transform.Position = position;
            }
        }

    }
}
