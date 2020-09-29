using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Game.DungeonBurst
{
    //we always want to update before BuildPhysicsWorld
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(BuildPhysicsWorld))]
    public class TileCollisionSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem _commandBufferSystem;
        private BlobAssetReference<Collider> _floorColliderPrefab;
        private BlobAssetReference<Collider> _solidColliderPrefab;

        protected override void OnStartRunning()
        {
            _commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnDestroy()
        {
            //clean up BlobAssetReferences when initialized
            if (_floorColliderPrefab.IsCreated)
            {
                _floorColliderPrefab.Dispose();
                _solidColliderPrefab.Dispose();
            }
        }

        public void ConvertColliderPrefabs(UnityEngine.GameObject floorCollider, UnityEngine.GameObject solidCollider)
        {
            using (var blobAssetStore = new BlobAssetStore())
            {
                var conversionSettings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, blobAssetStore);
                var floorColliderPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(floorCollider, conversionSettings);
                var solidColliderPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(solidCollider, conversionSettings);
                _floorColliderPrefab = EntityManager.GetComponentData<PhysicsCollider>(floorColliderPrefab).Value;
                _solidColliderPrefab = EntityManager.GetComponentData<PhysicsCollider>(solidColliderPrefab).Value;
                // the BlobAssetStore will contain collider information, which it would try to dispose. This data is handled by UnitPhysics, so we dont want that. resetting the cache seems to do the trick. i hope this has no unintended consequences
                blobAssetStore.ResetCache(false);
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var commandBuffer = _commandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            var floorPrefab = _floorColliderPrefab;
            var solidPrefab = _solidColliderPrefab;

            // iterate over all MapTiles that require an update and already have a collider attached
            var updateColliderHandle = Entities.WithAll<UpdateTileView>().ForEach((int entityInQueryIndex, Entity entity, in MapTile mapTile, in PhysicsCollider collider) =>
            {
                var colliderType = collider.Value.Value.Type;
                var expected = mapTile.Type.IsSolid() ? ColliderType.Box : ColliderType.Quad;
                if (colliderType != expected)
                {
                    // if collider is not what we expect, change it
                    var data = mapTile.Type.IsSolid() ? solidPrefab : floorPrefab;
                    commandBuffer.SetComponent(entityInQueryIndex, entity, new PhysicsCollider { Value = data });
                }
            }).Schedule(inputDeps);

            // iterate over all MapTiles that require an update and do not have a collider attached yet
            var addColliderHandle = Entities.WithAll<UpdateTileView>().WithNone<PhysicsCollider>().ForEach((int entityInQueryIndex, Entity entity, in MapTile mapTile) =>
            {
                var data = mapTile.Type.IsSolid() ? solidPrefab : floorPrefab;
                commandBuffer.AddComponent(entityInQueryIndex, entity, new PhysicsCollider { Value = data });
            }).Schedule(updateColliderHandle);

            // make sure our jobs are finished when the commandbuffer wants to playback
            _commandBufferSystem.AddJobHandleForProducer(addColliderHandle);

            return addColliderHandle;
        }
    }
}