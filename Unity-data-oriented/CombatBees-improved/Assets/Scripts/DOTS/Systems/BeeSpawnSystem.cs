using Unity.Entities;
using Unity.Burst;
using Unity.Core;

namespace DOTS
{
    [BurstCompile]
    [UpdateBefore(typeof(DeadBeesSystem))]
    public partial struct BeeSpawnSystem : ISystem
    {
        private EntityQuery _teamTotal;

        public void OnCreate(ref SystemState state)
        {
            _teamTotal = state.GetEntityQuery(typeof(Team));
            
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer.ParallelWriter ecb = GetEntityCommandBuffer(ref state);

            _teamTotal.SetSharedComponentFilter(new Team { Value = 1 });
            int team1BeeCount = _teamTotal.CalculateEntityCountWithoutFiltering();
            _teamTotal.SetSharedComponentFilter(new Team { Value = 2 });
            int team2BeeCount = _teamTotal.CalculateEntityCount();
            

            // Creates a new instance of the job, assigns the necessary data, and schedules the job in parallel.
            new ProcessSpawnerJob
            {
                Ecb = ecb,
                team1BeeCount = team1BeeCount,
                team2BeeCount = team2BeeCount,
                timeData = state.WorldUnmanaged.Time

            }.ScheduleParallel();
        }

        private EntityCommandBuffer.ParallelWriter GetEntityCommandBuffer(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            return ecb.AsParallelWriter();
        }

        [BurstCompile]
        public partial struct ProcessSpawnerJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public int team1BeeCount;
            public int team2BeeCount;
            public TimeData timeData;

            // IJobEntity generates a component data query based on the parameters of its `Execute` method.
            // This example queries for all Spawner components and uses `ref` to specify that the operation
            // requires read and write access. Unity processes `Execute` for each entity that matches the
            // component data query.
            private void Execute([ChunkIndexInQuery] int chunkIndex, ref Spawner spawner)
            {
                int beesToSpawnTeam1 = Data.beeStartCount / 2 - team1BeeCount;

                for (int i = 0; i < beesToSpawnTeam1; i++)
                {
                    Entity newEntity = Ecb.Instantiate(chunkIndex, spawner.BlueBee);
                    var rand = new RandomComponent();
                    rand.generator.InitState((uint)((i + 1) * (timeData.ElapsedTime + 1.0) * 57131));
                    Ecb.AddComponent(chunkIndex, newEntity, new Velocity());
                    Ecb.AddComponent(chunkIndex, newEntity, new DeadTimer { time = 1});
                    Ecb.AddComponent(chunkIndex, newEntity, new Target());
                    Ecb.AddComponent(chunkIndex, newEntity, rand);
                    Ecb.AddSharedComponent(chunkIndex, newEntity, new Team { Value = 1 });
                    Ecb.RemoveComponent<LinkedEntityGroup>(chunkIndex, newEntity);
                }


                int beesToSpawnTeam2 = Data.beeStartCount / 2 - team2BeeCount;

                for (int i = 0; i < beesToSpawnTeam2; i++)
                {
                    Entity newEntity = Ecb.Instantiate(chunkIndex, spawner.YellowBee);
                    var rand = new RandomComponent();
                    rand.generator.InitState((uint)((i + 1) * (timeData.ElapsedTime + 1.0) * 33223));
                    Ecb.AddComponent(chunkIndex, newEntity, new Velocity());
                    Ecb.AddComponent(chunkIndex, newEntity, new DeadTimer { time = 1 });
                    Ecb.AddComponent(chunkIndex, newEntity, new Target());
                    Ecb.AddComponent(chunkIndex, newEntity, rand);
                    Ecb.AddSharedComponent(chunkIndex, newEntity, new Team { Value = 2 });
                    Ecb.RemoveComponent<LinkedEntityGroup>(chunkIndex, newEntity);
                }
            }
        }
    }
}