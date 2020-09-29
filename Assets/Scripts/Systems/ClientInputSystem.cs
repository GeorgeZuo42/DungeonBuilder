using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Game.DungeonBurst
{
    // we always want to update this system before the TileViewSystem
    [AlwaysUpdateSystem]
    [UpdateBefore(typeof(TileViewSystem))]
    public class ClientInputSystem : ComponentSystem
    {
        private BuildPhysicsWorld _buildPhysicsSystem;
        private UnityEngine.Camera _camera;

        protected override void OnStartRunning()
        {
            _buildPhysicsSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<BuildPhysicsWorld>();
            _camera = UnityEngine.Camera.main;
        }

        protected override void OnUpdate()
        {
            // when left mouse is clicked
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                int mapTileTypes = Enum.GetValues(typeof(MapTileType)).Length;

                // we need to read and write to MapTile data
                var mapTileData = GetComponentDataFromEntity<MapTile>();
                //get current collision world
                var collisionWorld = _buildPhysicsSystem.PhysicsWorld.CollisionWorld;
                //get ray from camera
                var ray = _camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
                if (RayCast(ray.origin, ray.origin + ray.direction * 100, out RaycastHit result, collisionWorld))
                {
                    // get the hit entity via rigidbodyindex
                    var entity = _buildPhysicsSystem.PhysicsWorld.Bodies[result.RigidBodyIndex].Entity;

                    // get MapTile data
                    var tileData = mapTileData[entity];
                    // change MaptileType to the next type in enum
                    tileData.Type = (MapTileType)(((int)tileData.Type + 1) % mapTileTypes);
                    // apply data to Entity
                    mapTileData[entity] = tileData;

                    // we need to update the changed MapTile and all 8 surrounding neighbours
                    var gameMap = GetSingleton<GameMap>();
                    ref TileMapBlobAsset mapData = ref gameMap.TileMap.Value;
                    var position = tileData.Position;
                    for (int y = position.y - 1; y <= position.y + 1; y++)
                    {
                        for (int x = position.x - 1; x <= position.x + 1; x++)
                        {
                            if (x < 0 || y < 0 || x == gameMap.Width || y == gameMap.Height) continue;

                            var tileEntity = mapData.Map[x + y * gameMap.Width];
                            EntityManager.AddComponentData(tileEntity, new UpdateTileView());
                        }
                    }
                }
            }
        }

        public static bool RayCast(float3 start, float3 end, out RaycastHit result, CollisionWorld world, uint collidesWith = ~0u, uint belongsTo = ~0u)
        {
            RaycastInput input = new RaycastInput
            {
                Start = start,
                End = end,
                Filter = new CollisionFilter
                {
                    BelongsTo = belongsTo,
                    CollidesWith = collidesWith,
                    GroupIndex = 0,
                }
            };
            return world.CastRay(input, out result);
        }
    }
}