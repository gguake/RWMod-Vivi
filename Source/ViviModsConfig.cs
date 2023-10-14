using Verse;

namespace VVRace
{
    public class VVRaceMod : Mod
    {
        public VVRaceMod(ModContentPack content) : base(content)
        {
            ViviHarmonyPatcher.PatchAll();
        }
    }
}
