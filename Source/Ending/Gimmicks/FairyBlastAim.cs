using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyBlastAim : IExposable
    {
        public const int AimTicks = 240;

        public IntVec3 cell = IntVec3.Invalid;
        public int detonateTick = -1;

        protected Mote _targetMote;
        private Effecter _effecter;

        public FairyBlastAim()
        {
        }

        public FairyBlastAim(IntVec3 cell, int detonateTick)
        {
            this.cell = cell;
            this.detonateTick = detonateTick;
        }

        public void TickVisuals(Map map)
        {
            if (map == null || !cell.InBounds(map)) { return; }

            var aimStartTick = Mathf.Min(detonateTick - AimTicks, Find.TickManager.TicksGame);
            if (_targetMote == null || _targetMote.Destroyed)
            {
                _targetMote = MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, VVThingDefOf.VV_Mote_ApocryphaAimTarget, 1f, makeOffscreen: true);
                _targetMote?.ForceSpawnTick(aimStartTick);
            }

            _targetMote?.Maintain();

            var target = new TargetInfo(cell, map);
            EndingVfxUtility.MaintainEffecter(ref _effecter, VVEffecterDefOf.VV_Effecter_ApocryphaAim, target, target);
        }

        public void CleanupVisuals()
        {
            EndingVfxUtility.CleanupEffecter(ref _effecter);
            _targetMote = null;
        }

        public void Detonate(Map map, float radius, List<Thing> ignored, Thing instigator, float screenShake)
        {
            CleanupVisuals();
            if (map == null || !cell.InBounds(map)) { return; }

            ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyShellImpact, cell, map);

            var damageDef = VVDamageDefOf.VV_FairyBlast;
            GenExplosion.DoExplosion(
                cell,
                map,
                radius,
                damageDef,
                instigator,
                damageDef.defaultDamage,
                damageDef.defaultArmorPenetration,
                ignoredThings: ignored,
                screenShakeFactor: screenShake);
        }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref detonateTick, "detonateTick", -1);
        }
    }
}
