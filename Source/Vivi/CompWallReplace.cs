using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_CompWallReplace : CompProperties
    {
        public CompProperties_CompWallReplace()
        {
            compClass = typeof(CompWallReplace);
        }
    }

    public class CompWallReplace : ThingComp
    {
        private int buildedTick = 0;

        public override void PostPostMake()
        {
            base.PostPostMake();

            buildedTick = Find.TickManager.TicksGame;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref buildedTick, "buildedTick");
        }

        public override void CompTickLong()
        {
            base.CompTickLong();

            if (Find.TickManager.TicksGame > buildedTick + 5000)
            {
                var map = parent.Map;
                var position = parent.Position;
                var rotation = parent.Rotation;
                var faction = parent.Faction;

                parent.Destroy(DestroyMode.WillReplace);
                var wall = ThingMaker.MakeThing(ThingDefOf.Wall, VVThingDefOf.VV_Viviwax);
                wall.SetFaction(faction);
                GenSpawn.Spawn(wall, position, map, rotation);
            }
        }
    }
}
