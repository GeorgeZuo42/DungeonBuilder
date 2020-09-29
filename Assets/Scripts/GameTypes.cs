namespace Game.DungeonBurst
{
    public enum MapTileType
    {
        Empty,
        Water,
        Earth,
        Stone,
        Gold,
        Gems,
        Tile,
        Wall,
    }

    public static class MapTileTypeExtension
    {
        public static bool IsSolid(this MapTileType tileType)
        {
            switch (tileType)
            {
                case MapTileType.Earth:
                case MapTileType.Stone:
                case MapTileType.Gold:
                case MapTileType.Gems:
                case MapTileType.Wall:
                    return true;
                default:
                    return false;
            }
        }
    }
}