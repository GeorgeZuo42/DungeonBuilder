using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.DungeonBurst
{
    [CreateAssetMenu]
    public class MapLoaderPalette : ScriptableObject
    {
        public List<PaletteEntry> Palette;
        public List<Color32> PlayerColors;

        [NonSerialized]
        private Dictionary<Color32, MapTileType> _paletteMap;

        public MapTileType GetTerrain(Color32 color)
        {
            if (_paletteMap == null)
            {
                _paletteMap = new Dictionary<Color32, MapTileType>();
                foreach (var entry in Palette)
                {
                    _paletteMap.Add(entry.Color, entry.Type);
                }
            }
            if (_paletteMap.ContainsKey(color))
            {
                return _paletteMap[color];
            }
            Debug.LogWarning("InvalidColor " + color);
            return MapTileType.Empty;
        }

        public int GetPlayer(Color32 color)
        {
            return PlayerColors.IndexOf(color) + 1;
        }

        [Serializable]
        public struct PaletteEntry
        {
            public MapTileType Type;
            public Color Color;
        }

    }
}