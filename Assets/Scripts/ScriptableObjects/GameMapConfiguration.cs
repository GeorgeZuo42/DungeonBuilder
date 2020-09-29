using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.DungeonBurst
{
    [CreateAssetMenu]
    public class GameMapConfiguration : ScriptableObject
    {
        public Texture2D Terrain;
        public Texture2D Territory;
        public MapLoaderPalette Palette;
    }
}