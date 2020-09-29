using Unity.Entities;
using Unity.Mathematics;

namespace Game.DungeonBurst
{
    public struct MapTile : IComponentData
    {
        public MapTileType Type;
        public int2 Position;
        public int Owner;
    }
}