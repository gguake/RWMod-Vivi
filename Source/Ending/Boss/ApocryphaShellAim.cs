using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaShellAim : IExposable
    {
        public IntVec3 cell = IntVec3.Invalid;
        public int fireTick = -1;

        private Mote _targetMote;
        private Mote _chargeMote;
        private Effecter _effecter;

        public ApocryphaShellAim()
        {
        }

        public ApocryphaShellAim(IntVec3 cell, int fireTick)
        {
            this.cell = cell;
            this.fireTick = fireTick;
        }

        public void TickVisuals(Pawn boss, Map map, Vector3 bodyCenter, int aimTicks)
        {
            var direction = (cell.ToVector3Shifted() - boss.DrawPos).Yto0().normalized;
            var aimStartTick = Mathf.Min(fireTick - aimTicks, Find.TickManager.TicksGame);
            var angle = direction.AngleFlat();

            MaintainAimMote(ref _targetMote, cell.ToVector3Shifted(), map, VVThingDefOf.VV_Mote_ApocryphaAimTarget, aimStartTick, angle, updatePosition: false);
            MaintainAimMote(ref _chargeMote, bodyCenter + direction * 0.7f, map, VVThingDefOf.VV_Mote_ApocryphaAimCharge, aimStartTick, angle, updatePosition: true);

            EndingVfxUtility.MaintainEffecter(ref _effecter, VVEffecterDefOf.VV_Effecter_ApocryphaAim, new TargetInfo(boss), new TargetInfo(cell, map));
        }

        private static void MaintainAimMote(ref Mote mote, Vector3 pos, Map map, ThingDef moteDef, int aimStartTick, float angle, bool updatePosition)
        {
            if (mote == null || mote.Destroyed)
            {
                mote = MoteMaker.MakeStaticMote(pos, map, moteDef, 1f, makeOffscreen: true);
                mote?.ForceSpawnTick(aimStartTick);
            }

            if (mote == null) { return; }

            if (updatePosition)
            {
                mote.exactPosition = pos;
            }
            mote.exactRotation = angle;
            mote.Maintain();
        }

        public void CleanupVisuals()
        {
            EndingVfxUtility.CleanupEffecter(ref _effecter);
            _targetMote = null;
            _chargeMote = null;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref fireTick, "fireTick", -1);
        }
    }
}
