using System;
using Unity.Entities;
using UnityEngine;

namespace Game.DungeonBurst
{
    [CreateAssetMenu]
    public class MapTileViewConfiguration : ScriptableObject
    {
        public MapTileType TileType;
        public Parts View;

        [Serializable]
        public struct Parts
        {
            public Part Top;
            public Part North;
            public Part East;
            public Part South;
            public Part West;
        }

        [Serializable]
        public struct Part
        {
            public GameObject Prefab;
        }
    }
}