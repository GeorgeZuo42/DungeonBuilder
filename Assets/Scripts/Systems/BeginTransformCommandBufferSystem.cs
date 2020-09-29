using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace Game.DungeonBurst
{
    // we want this to be the first in TransformSystemGroup
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(EndFrameParentSystem))]
    public class BeginTransformCommandBufferSystem : ComponentSystem
    {
        public EntityCommandBuffer CommandBuffer;
        private NativeList<JobHandle> _jobHandles;

        protected override void OnCreate()
        {
            // we will create our own command buffer, for other systems to use
            CommandBuffer = new EntityCommandBuffer(Allocator.Persistent);
            // we will store all jobhandles we need to wait for
            _jobHandles = new NativeList<JobHandle>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            CommandBuffer.Dispose();
            _jobHandles.Dispose();
        }

        // other systems can register their JobHandles, so we can make sure they are finished
        public void AddJobHandleForProducer(JobHandle jobHandle)
        {
            _jobHandles.Add(jobHandle);
            // somebody needs this commandbuffer so we reenable the system
            Enabled = true;
        }

        protected override void OnUpdate()
        {
            // complete all registered JobHandles
            for (int j = 0; j < _jobHandles.Length; j++)
            {
                _jobHandles[j].Complete();
            }
            // clear for next update
            _jobHandles.Clear();

            // playback all stored entity commands
            CommandBuffer.Playback(EntityManager);

            // dispose and recreate the commandbuffer, because we cannot reuse it
            CommandBuffer.Dispose();
            CommandBuffer = new EntityCommandBuffer(Allocator.Persistent);

            // disable system until other systems register their jobhandles
            Enabled = false;
        }
    }
}
