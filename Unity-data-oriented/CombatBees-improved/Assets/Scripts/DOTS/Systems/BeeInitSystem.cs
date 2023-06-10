using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace DOTS
{
    [BurstCompile]
    [UpdateAfter(typeof(BeeSpawnSystem))]
    public partial struct BeeInitSystem : ISystem
    {
        private EntityQuery _deadQuery;
        private ComponentLookup<DeadTimer> _deadTimerLookup;

        public void OnCreate(ref SystemState state)
        {
            _deadQuery = state.GetEntityQuery(typeof(DeadTimer), typeof(Velocity), typeof(LocalTransform), typeof(Target), typeof(RandomComponent), typeof(Team));
            _deadTimerLookup = state.GetComponentLookup<DeadTimer>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _deadTimerLookup.Update(ref state);
            _deadQuery.SetSharedComponentFilter(new Team { Value = 1 });
            var j1 = new InitStateJobChunk
            {
                SpawnPosition = LocalTransform.FromPosition(DataBurst.Team1BeeSpawnPos),
                DeadTimerLookup = _deadTimerLookup,
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                DeadTimerHandle = SystemAPI.GetComponentTypeHandle<DeadTimer>(),
                VelocityHandle = SystemAPI.GetComponentTypeHandle<Velocity>(),
                TargetHandle = SystemAPI.GetComponentTypeHandle<Target>(),
                RandomComponentHandle = SystemAPI.GetComponentTypeHandle<RandomComponent>(),
                LocalTransformHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
            }.ScheduleParallel(_deadQuery, state.Dependency);
            
            _deadQuery.SetSharedComponentFilter(new Team { Value = 2 });
            var j2 = new InitStateJobChunk
            {
                SpawnPosition = LocalTransform.FromPosition(DataBurst.Team2BeeSpawnPos),
                DeadTimerLookup = _deadTimerLookup,
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                DeadTimerHandle = SystemAPI.GetComponentTypeHandle<DeadTimer>(),
                VelocityHandle = SystemAPI.GetComponentTypeHandle<Velocity>(),
                TargetHandle = SystemAPI.GetComponentTypeHandle<Target>(),
                RandomComponentHandle = SystemAPI.GetComponentTypeHandle<RandomComponent>(),
                LocalTransformHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
            }.ScheduleParallel(_deadQuery, state.Dependency);
            state.Dependency = JobHandle.CombineDependencies(j1, j2);
        }

        [BurstCompile]
        private unsafe struct InitStateJobChunk : IJobChunk
        {
            public LocalTransform SpawnPosition;
            [NativeDisableContainerSafetyRestriction]
            public ComponentTypeHandle<DeadTimer> DeadTimerHandle;
            [NativeDisableContainerSafetyRestriction]
            public ComponentTypeHandle<Velocity> VelocityHandle;
            [NativeDisableContainerSafetyRestriction]
            public ComponentTypeHandle<Target> TargetHandle;
            [NativeDisableContainerSafetyRestriction]
            public ComponentTypeHandle<RandomComponent> RandomComponentHandle;
            [NativeDisableContainerSafetyRestriction]
            public ComponentTypeHandle<LocalTransform> LocalTransformHandle;
            public EntityTypeHandle EntityTypeHandle;
            [NativeDisableContainerSafetyRestriction]
            public ComponentLookup<DeadTimer> DeadTimerLookup;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                v128 mask = chunkEnabledMask;
                int start = 0, end = 0;
                var entityPtr = chunk.GetEntityDataPtrRO(EntityTypeHandle);
                var deadTimerPtr = chunk.GetComponentDataPtrRW(ref DeadTimerHandle);
                var velPtr = chunk.GetComponentDataPtrRW(ref VelocityHandle);
                var targetPtr = chunk.GetComponentDataPtrRW(ref TargetHandle);
                var randPtr = chunk.GetComponentDataPtrRW(ref RandomComponentHandle);
                var localTransformPtr = chunk.GetComponentDataPtrRW(ref LocalTransformHandle);
                while (EnabledBitUtility.GetNextRange(ref mask, ref start, ref end))
                {
                    for (int i = start; i < end; i++)
                    {
                        Execute(entityPtr[i], ref velPtr[i], ref targetPtr[i], ref randPtr[i], ref localTransformPtr[i], ref deadTimerPtr[i]);
                    }
                }
            }
            
            private void Execute(Entity e,
                ref Velocity vel, ref Target target,
                ref RandomComponent rand, ref LocalTransform transform,
                ref DeadTimer timer)
            {
                if (timer.time < 1f)
                    return;
                timer.time = 0;
                DeadTimerLookup.SetComponentEnabled(e, false);
                transform = SpawnPosition;
                transform.Scale = rand.generator.NextFloat(Data.minBeeSize, Data.maxBeeSize);
                vel = default;
                target = default;
            }
        }
    }
}