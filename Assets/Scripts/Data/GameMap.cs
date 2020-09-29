using Unity.Entities;

namespace Game.DungeonBurst
{
    public struct GameMap : IComponentData
    {
        public BlobAssetReference<TileMapBlobAsset> TileMap;
        public int Width;
        public int Height;
    }
}