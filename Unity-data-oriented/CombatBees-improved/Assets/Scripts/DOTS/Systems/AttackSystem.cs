using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(BeePositionUpdateSystem))]
    [UpdateAfter(typeof(BeeInitSystem))]
    public partial struct AttackSystem : ISystem
    {
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer.ParallelWriter ecb = GetEntityCommandBuffer(ref state);
            state.Dependency = new AttackJob
            {
                Ecb = ecb,
                deltaTime = state.WorldUnmanaged.Time.DeltaTime,
                DeadLookup = SystemAPI.GetComponentLookup<DeadTimer>(true),
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true)

            }.ScheduleParallel(state.Dependency);
        }

        private EntityCommandBuffer.ParallelWriter GetEntityCommandBuffer(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            return ecb.AsParallelWriter();
        }

        [BurstCompile]
        public partial struct AttackJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public float deltaTime;
            [ReadOnly] public ComponentLookup<DeadTimer> DeadLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;

            private void Execute(Entity e, [ChunkIndexInQuery] int chunkIndex, ref Velocity velocity, ref Target target, in LocalTransform transform)
            {
                if (target.enemyTarget != Entity.Null && DeadLookup.IsComponentEnabled(target.enemyTarget))
                {
                    //the target is dead
                    target.enemyTarget = Entity.Null;
                    return;
                }

                var beePosition = transform.Position;
                var enmeyPosition = TransformLookup.GetRefRO(target.enemyTarget).ValueRO.Position;

                var delta = enmeyPosition - beePosition;
                float sqrDist = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;
                if (sqrDist > Data.attackDistance * Data.attackDistance)
                {
                    velocity.Value += delta * (Data.chaseForce * deltaTime / math.sqrt(sqrDist));
                }
                else
                {
                    velocity.Value += delta * (Data.attackForce * deltaTime / math.sqrt(sqrDist));
                    if (sqrDist < Data.hitDistance * Data.hitDistance)
                    {
                        Ecb.SetComponentEnabled<DeadTimer>(chunkIndex, target.enemyTarget, true);
                    }
                }
            }
        }
    }
}
