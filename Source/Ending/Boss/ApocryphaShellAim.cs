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
            var direction = cell.ToVector3Shifted() - boss.DrawPos;
            direction.y = 0f;
            direction.Normalize();

            var aimStartTick = Mathf.Min(fireTick - aimTicks, Find.TickManager.TicksGame);
            if (_targetMote == null || _targetMote.Destroyed)
            {
                _targetMote = MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, VVThingDefOf.VV_Mote_ApocryphaAimTarget, 1f, makeOffscreen: true);
                _targetMote?.ForceSpawnTick(aimStartTick);
            }

            if (_targetMote != null)
            {
                _targetMote.exactRotation = direction.AngleFlat();
                _targetMote.Maintain();
            }

            var chargePos = bodyCenter + direction * 0.7f;
            if (_chargeMote == null || _chargeMote.Destroyed)
            {
                _chargeMote = MoteMaker.MakeStaticMote(chargePos, map, VVThingDefOf.VV_Mote_ApocryphaAimCharge, 1f, makeOffscreen: true);
                _chargeMote?.ForceSpawnTick(aimStartTick);
            }

            if (_chargeMote != null)
            {
                _chargeMote.exactPosition = chargePos;
                _chargeMote.exactRotation = direction.AngleFlat();
                _chargeMote.Maintain();
            }

            var source = new TargetInfo(boss);
            var target = new TargetInfo(cell, map);
            if (_effecter == null)
            {
                _effecter = VVEffecterDefOf.VV_Effecter_ApocryphaAim.Spawn();
                _effecter.Trigger(source, target);
            }

            _effecter.EffectTick(source, target);
        }

        public void CleanupVisuals()
        {
            _effecter?.Cleanup();
            _effecter = null;
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
