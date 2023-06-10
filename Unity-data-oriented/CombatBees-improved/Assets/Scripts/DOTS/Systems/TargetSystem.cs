using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(AttackSystem))]
    [UpdateAfter(typeof(BeeWallCollisionSystem))]
    public partial struct TargetSystem : ISystem
    {
        private EntityQuery team1Alive;
        private EntityQuery team2Alive;

        public void OnCreate(ref SystemState state)
        {
            team1Alive = state.EntityManager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithDisabled<DeadTimer>().WithAll<Team>());
            team1Alive.AddSharedComponentFilter<Team>(1);
            team2Alive = state.EntityManager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithDisabled<DeadTimer>().WithAll<Team>());
            team2Alive.AddSharedComponentFilter<Team>(2);
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var team1Entities = team1Alive.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var dep1);
            var team2Entities = team2Alive.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var dep2);

            state.Dependency = new TargetJob
            {
                team1Enemies = team2Entities.AsDeferredJobArray(),
                team2Enemies = team1Entities.AsDeferredJobArray()
            }.ScheduleParallel(JobHandle.CombineDependencies(dep1, dep2, state.Dependency));

            team1Entities.Dispose(state.Dependency);
            team2Entities.Dispose(state.Dependency);
        }


        [BurstCompile]
        public partial struct TargetJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity> team1Enemies;
            [ReadOnly] public NativeArray<Entity> team2Enemies;

            private void Execute(ref RandomComponent random, ref Target target, in Team team)
            {
                if (target.enemyTarget == Entity.Null)
                {
                    var enemies = team == 1 ? team1Enemies : team2Enemies;
                    int newTarget = random.generator.NextInt(0, enemies.Length);
                    target.enemyTarget = enemies[newTarget];
                }
            }
        }
    }
}
