using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class TextureCache
    {
        public static readonly Texture2D MindLinkConnect = ContentFinder<Texture2D>.Get("UI/Commands/MindLinkConnect");
        public static readonly Texture2D MindLinkDisconnect = ContentFinder<Texture2D>.Get("UI/Commands/MindLinkDisconnect");
    }
}
