using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.DungeonBurst
{
    [UpdateBefore(typeof(TileViewSystem))]
    public class FancyDropEffectSystem : JobComponentSystem
    {
        private BeginSimulationEntityCommandBufferSystem _commandBufferSystem;
        private bool _isRunning;

        protected override void OnCreate()
        {
            _commandBufferSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space))
            {
                _isRunning = true;
            }
            if (!_isRunning)
            {
                return inputDeps;
            }
            // create concurrent commandbuffer that can be used in mulithreaded burst jobs
            var commandBuffer = _commandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            var gameMap = GetSingleton<GameMap>();
            ref var mapData = ref gameMap.TileMap.Value;
            var mapCenter = new float2(gameMap.Width, gameMap.Height) * 0.5f;
            var offset = UnityEngine.Time.time;
            int mapTileTypes = Enum.GetValues(typeof(MapTileType)).Length;

            // iterate over all tiles that need an update
            var createViewPartsHandle = Entities.ForEach((int entityInQueryIndex, Entity entity, ref MapTile mapTile) =>
            {
                var distance = math.distance(mapCenter, mapTile.Position);
                var sinus = math.sin(distance * 0.8f + offset) * 0.5f + 0.5f;
                var waveType = (MapTileType)((int)(sinus * mapTileTypes));
                if (mapTile.Type != waveType)
                {
                    mapTile.Type = waveType;
                    commandBuffer.AddComponent<UpdateTileView>(entityInQueryIndex, entity);
                }
            }).Schedule(inputDeps);

            // make sure our jobs are finished when the commandbuffer wants to playback
            _commandBufferSystem.AddJobHandleForProducer(createViewPartsHandle);

            return createViewPartsHandle;
        }
    }
}