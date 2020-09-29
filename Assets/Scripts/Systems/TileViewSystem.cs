using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.DungeonBurst
{
    public class TileViewSystem : JobComponentSystem
    {
        private BeginTransformCommandBufferSystem _commandBufferSystem;
        private EntityQuery _updateQuery;

        // map the int value of every MapTileType to a configuration of view prefabs
        private NativeHashMap<int, MapTileViewParts> _viewPartMap;

        // will hold all entity prefabs for a MapTileType
        private struct MapTileViewParts : IComponentData
        {
            public Entity Top;
            public Entity North;
            public Entity East;
            public Entity South;
            public Entity West;
        }

        protected override void OnCreate()
        {
            _commandBufferSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<BeginTransformCommandBufferSystem>();
            _updateQuery = EntityManager.CreateEntityQuery(typeof(UpdateTileView));
            // only update this system when there are entities that require update
            RequireForUpdate(_updateQuery);
        }

        protected override void OnDestroy()
        {
            if (_viewPartMap.IsCreated)
            {
                _viewPartMap.Dispose();
            }
        }

        public void ConvertMapTileViewConfigurations(List<MapTileViewConfiguration> configurations)
        {
            _viewPartMap = new NativeHashMap<int, MapTileViewParts>(configurations.Count, Allocator.Persistent);

            // we can use using here to make sure the BlobAssetStore is disposed when we are finished
            using (var blobAssetStore = new BlobAssetStore())
            {
                var conversionSettings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, blobAssetStore);
                foreach (var config in configurations)
                {
                    var viewParts = new MapTileViewParts();
                    // convert all GameObject prefabs into Entity prefabs
                    viewParts.Top = GameObjectConversionUtility.ConvertGameObjectHierarchy(config.View.Top.Prefab, conversionSettings);
                    viewParts.North = GameObjectConversionUtility.ConvertGameObjectHierarchy(config.View.North.Prefab, conversionSettings);
                    viewParts.East = GameObjectConversionUtility.ConvertGameObjectHierarchy(config.View.East.Prefab, conversionSettings);
                    viewParts.South = GameObjectConversionUtility.ConvertGameObjectHierarchy(config.View.South.Prefab, conversionSettings);
                    viewParts.West = GameObjectConversionUtility.ConvertGameObjectHierarchy(config.View.West.Prefab, conversionSettings);
                    // since enums do boxing we will use the int value of the MapTileType as key
                    _viewPartMap.Add((int)config.TileType, viewParts);
                }
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            // create concurrent commandbuffer that can be used in mulithreaded burst jobs
            var commandBuffer = _commandBufferSystem.CommandBuffer.AsParallelWriter();

            // get all entities that require an update
            var updateEntities = _updateQuery.ToEntityArray(Allocator.TempJob);

            // first we wanna clean up all ViewPart Entities currently attached to the MapTiles that require an update

            // iterate over all ViewParts
            var cleanUpViewPartsHandle = Entities.WithAll<ViewPart>().ForEach((int entityInQueryIndex, Entity entity, in Parent parent) =>
            {
                // if parent requires update
                if (updateEntities.Contains(parent.Value))
                {
                    // destroy ViewPart Entity
                    commandBuffer.DestroyEntity(entityInQueryIndex, entity);
                }
                // we need to tell the compiler that we will only read the updateEntities array. when the job is complete dispose it    
            }).WithReadOnly(updateEntities).WithDisposeOnCompletion(updateEntities).Schedule(inputDeps);

            // second we want to instantiate all ViewParts for all MapTiles that require an update

            // we only have one Entity with GameMap componendata, so we can get it by singleton
            var gameMap = GetSingleton<GameMap>();
            var mapData = gameMap.TileMap;
            int mapWidth = gameMap.Width;
            int mapHeight = gameMap.Height;
            // we need to read LocalToWorld and MapTile data from Entities
            var localToWorldData = GetComponentDataFromEntity<LocalToWorld>(true);
            var mapTileData = GetComponentDataFromEntity<MapTile>(true);
            var viewPartMap = _viewPartMap;
            // iterate over all tiles that need an update
            var createViewPartsHandle = Entities.WithAll<UpdateTileView>().ForEach((int entityInQueryIndex, Entity entity, in MapTile mapTile) =>
            {
                // if we have a configuration for this type of MapTile
                if (viewPartMap.ContainsKey((int)mapTile.Type))
                {
                    var viewConfig = viewPartMap[(int)mapTile.Type];
                    int x = mapTile.Position.x;
                    int y = mapTile.Position.y;
                    if (viewConfig.Top != Entity.Null)
                    {
                        // instantiate top ViewPart prefab
                        var viewPart = commandBuffer.Instantiate(entityInQueryIndex, viewConfig.Top);
                        // get position from the viewpart prefab, this will be our parent offset
                        var localToParent = localToWorldData[viewConfig.Top].Value;
                        // assign viewpart data 
                        SetupViewPart(entityInQueryIndex, viewPart, entity, localToParent, commandBuffer);
                    }
                    //we only want to spawn walls towards sides that are not solid and in map bounds
                    if (viewConfig.North != Entity.Null && !IsSolidTile(x, y + 1, mapWidth, mapHeight, ref mapData.Value, mapTileData))
                    {
                        var viewPart = commandBuffer.Instantiate(entityInQueryIndex, viewConfig.North);
                        var localToWorld = localToWorldData[viewConfig.North].Value;
                        SetupViewPart(entityInQueryIndex, viewPart, entity, localToWorld, commandBuffer);
                    }
                    if (viewConfig.East != Entity.Null && !IsSolidTile(x + 1, y, mapWidth, mapHeight, ref mapData.Value, mapTileData))
                    {
                        var viewPart = commandBuffer.Instantiate(entityInQueryIndex, viewConfig.East);
                        var localToWorld = localToWorldData[viewConfig.East].Value;
                        SetupViewPart(entityInQueryIndex, viewPart, entity, localToWorld, commandBuffer);
                    }
                    if (viewConfig.South != Entity.Null && !IsSolidTile(x, y - 1, mapWidth, mapHeight, ref mapData.Value, mapTileData))
                    {
                        var viewPart = commandBuffer.Instantiate(entityInQueryIndex, viewConfig.South);
                        var localToWorld = localToWorldData[viewConfig.South].Value;
                        SetupViewPart(entityInQueryIndex, viewPart, entity, localToWorld, commandBuffer);
                    }
                    if (viewConfig.West != Entity.Null && !IsSolidTile(x - 1, y, mapWidth, mapHeight, ref mapData.Value, mapTileData))
                    {
                        var viewPart = commandBuffer.Instantiate(entityInQueryIndex, viewConfig.West);
                        var localToWorld = localToWorldData[viewConfig.West].Value;
                        SetupViewPart(entityInQueryIndex, viewPart, entity, localToWorld, commandBuffer);
                    }
                }
                // remove the update tile flag 
                commandBuffer.RemoveComponent<UpdateTileView>(entityInQueryIndex, entity);
                // we need to tell the compiler that we only want to read from these data containers
            }).WithReadOnly(viewPartMap).WithReadOnly(localToWorldData).WithReadOnly(mapTileData).Schedule(cleanUpViewPartsHandle);

            // make sure our jobs are finished when the commandbuffer wants to playback
            _commandBufferSystem.AddJobHandleForProducer(createViewPartsHandle);

            return createViewPartsHandle;
        }

        private static void SetupViewPart(int entityInQueryIndex, Entity entity, Entity parent, float4x4 localToParent, EntityCommandBuffer.ParallelWriter commandBuffer)
        {
            commandBuffer.AddComponent(entityInQueryIndex, entity, new Parent { Value = parent });
            commandBuffer.AddComponent(entityInQueryIndex, entity, new LocalToParent { Value = localToParent }); ;
            commandBuffer.AddComponent(entityInQueryIndex, entity, new ViewPart());
        }

        private static bool IsSolidTile(int x, int y, int mapWidth, int mapHeight, ref TileMapBlobAsset mapData, ComponentDataFromEntity<MapTile> tileData)
        {
            // if coordinate is out of bounds consider it solid
            if (x < 0 || y < 0 || x == mapWidth || y == mapHeight) return true;

            // find MapTile entity in our TileMapBlobAsset reference
            var tileEntity = mapData.Map[x + y * mapWidth];

            // get MapTile data for entity
            var tile = tileData[tileEntity];
            return tile.Type.IsSolid();
        }
    }
}