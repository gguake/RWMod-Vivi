using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_CompWallReplace : CompProperties
    {
        public int replaceTicks;
        public ThingDef replaceThing;
        public ThingDef replaceThingStuff;

        public CompProperties_CompWallReplace()
        {
            compClass = typeof(CompWallReplace);
        }
    }

    public class CompWallReplace : ThingComp
    {
        public CompProperties_CompWallReplace Props => (CompProperties_CompWallReplace)props;

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

            if (Find.TickManager.TicksGame > buildedTick + Props.replaceTicks)
            {
                var map = parent.Map;
                var position = parent.Position;
                var rotation = parent.Rotation;
                var faction = parent.Faction;

                var currentHealthPct = (float)parent.HitPoints / parent.MaxHitPoints;

                parent.Destroy(DestroyMode.WillReplace);
                var wall = ThingMaker.MakeThing(Props.replaceThing, Props.replaceThingStuff);
                wall.SetFaction(faction ?? Faction.OfPlayer);
                wall.HitPoints = Mathf.Clamp(Mathf.CeilToInt(currentHealthPct * wall.MaxHitPoints), 1, wall.MaxHitPoints);

                GenSpawn.Spawn(wall, position, map, rotation);
            }
        }

        public override string CompInspectStringExtra()
        {
            var sb = new StringBuilder(base.CompInspectStringExtra());
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            var remainedTicks = Mathf.Max(0, buildedTick + Props.replaceTicks - GenTicks.TicksGame);
            sb.Append(LocalizeString_Inspector.VV_Inspector_ViviWallReplaceCooldown.Translate(remainedTicks.ToStringTicksToPeriod()));
            return sb.ToString();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (DebugSettings.godMode)
            {
                Command_Action command_replaceInstantly = new Command_Action();
                command_replaceInstantly.defaultLabel = "DEV: Replace instantly";
                command_replaceInstantly.action = () =>
                {
                    buildedTick -= Props.replaceTicks;
                };

                yield return command_replaceInstantly;
            }
        }
    }
}
