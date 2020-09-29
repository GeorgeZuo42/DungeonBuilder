using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Game.DungeonBurst
{
    public class MapLoaderAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public GameMapConfiguration GameMap;
        public List<MapTileViewConfiguration> TileViewConfigurations;

        public GameObject FloorColliderPrefab;
        public GameObject SolidColliderPrefab;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            //define Archetype for every MapTile
            var mapTileArchetype = dstManager.CreateArchetype(
                typeof(MapTile),
                typeof(Translation),
                typeof(Rotation),
                typeof(LocalToWorld),
                typeof(UpdateTileView)
            );

            int mapWidth = GameMap.Terrain.width;
            int mapHeight = GameMap.Terrain.height;

            // create entities for every tile
            var tileEntities = new NativeArray<Entity>(mapWidth * mapHeight, Allocator.Temp);
            dstManager.CreateEntity(mapTileArchetype, tileEntities);

            // get pixels from the terrain and territory textures
            var terrainPixels = GameMap.Terrain.GetPixels32();
            var territoryPixels = GameMap.Territory.GetPixels32();
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    int tileIndex = x + y * mapWidth;
                    var tileEntity = tileEntities[tileIndex];
                    dstManager.SetName(tileEntity, $"Tile {x} {y}");

                    // define owner and MapTile type by pixel Color
                    var owner = GameMap.Palette.GetPlayer(territoryPixels[tileIndex]);
                    var terrainType = GameMap.Palette.GetTerrain(terrainPixels[tileIndex]);

                    MapTileType type = terrainType;
                    // owned empty tiles will become MapTileType.Tile, owned earth tiles will become MapTileType.Wall
                    if (type == MapTileType.Empty && owner > 0) type = MapTileType.Tile;
                    else if (type == MapTileType.Earth && owner > 0) type = MapTileType.Wall;

                    // assign information to entity
                    dstManager.SetComponentData(tileEntity, new MapTile { Type = type, Owner = owner, Position = new int2(x, y) });
                    dstManager.SetComponentData(tileEntity, new Translation { Value = new float3(x + 0.5f, 0, y + 0.5f) });
                    dstManager.SetComponentData(tileEntity, new Rotation { Value = quaternion.identity });
                }
            }

            // construct BlobAsset containing the Entity id for every cell of the map
            using (BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp))
            {
                ref TileMapBlobAsset tileMapAsset = ref blobBuilder.ConstructRoot<TileMapBlobAsset>();
                BlobBuilderArray<Entity> tileArray = blobBuilder.Allocate(ref tileMapAsset.Map, mapWidth * mapHeight);

                //copy MapTile entities to blob array
                for (int t = 0; t < mapWidth * mapHeight; t++)
                {
                    tileArray[t] = tileEntities[t];
                }

                // create immutable BlobAssetReference
                var assetReference = blobBuilder.CreateBlobAssetReference<TileMapBlobAsset>(Allocator.Persistent);

                // assign BlobAssetReference to GameMap
                dstManager.AddComponentData(entity, new GameMap
                {
                    TileMap = assetReference,
                    Width = mapWidth,
                    Height = mapHeight
                });
            }

            // dispose tileEntities array
            tileEntities.Dispose();

            // initialize systems
            var world = World.DefaultGameObjectInjectionWorld;
            world.GetOrCreateSystem<TileViewSystem>().ConvertMapTileViewConfigurations(TileViewConfigurations);
            world.GetOrCreateSystem<TileCollisionSystem>().ConvertColliderPrefabs(FloorColliderPrefab, SolidColliderPrefab);
        }
    }
}