using Unity.Entities;
using Unity.Burst;

namespace DOTS
{
    [BurstCompile]
    [UpdateBefore(typeof(BeePositionUpdateSystem))]
    public partial struct DeadBeesSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new BeeDeadJob
            {
                deltaTime = state.WorldUnmanaged.Time.DeltaTime

            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct BeeDeadJob : IJobEntity
        {
            public float deltaTime;

            private void Execute(Entity e, ref Velocity velocity, ref DeadTimer deadTimer)
            {
                deadTimer.time += deltaTime / 10.0f;
                velocity.Value.y += Field.gravity * deltaTime;
            }
        }
    }
}
