using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Hediff_FairyAttunement : HediffWithComps
    {
        private const int PhaseCheckInterval = 250;

        private static readonly Vector3 ShimmerOffset = new Vector3(0f, 0f, 0.12f);

        private bool _phaseEnded;

        [Unsaved]
        private Effecter _auraEffecter;

        [Unsaved]
        private Mote _shimmerMote;

        public override bool ShouldRemove => _phaseEnded || base.ShouldRemove;

        public override void Tick()
        {
            base.Tick();

            if (pawn.IsHashIntervalTick(PhaseCheckInterval) && ViviEndingQuest.Current?.BossComp?.Phase != ApocryphaPhase.Asphodel)
            {
                _phaseEnded = true;
            }

            if (_phaseEnded || !pawn.Spawned || pawn.Dead)
            {
                EndAura();
                return;
            }

            MaintainAura();
        }

        private void MaintainAura()
        {
            if (_auraEffecter == null)
            {
                _auraEffecter = VVEffecterDefOf.VV_Effecter_FairyAttunementAura.SpawnAttached(pawn, pawn.Map);
            }

            _auraEffecter.EffectTick(new TargetInfo(pawn), TargetInfo.Invalid);

            if (_shimmerMote == null || _shimmerMote.Destroyed)
            {
                _shimmerMote = MoteMaker.MakeAttachedOverlay(pawn, VVThingDefOf.VV_Mote_FairyAttunementShimmer, ShimmerOffset);
            }

            _shimmerMote.Maintain();
        }

        private void EndAura()
        {
            _shimmerMote = null;
            EndingVfxUtility.CleanupEffecter(ref _auraEffecter);
        }

        public override void PostRemoved()
        {
            EndAura();
            base.PostRemoved();
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            EndAura();
            EndingVfxUtility.RemoveSelfIfPresent(pawn, this);
        }
    }
}
