using Unity.Entities;

namespace Game.DungeonBurst
{
    public struct TileMapBlobAsset
    {
        public BlobArray<Entity> Map;
    }
}