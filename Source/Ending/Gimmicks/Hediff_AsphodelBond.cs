using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Hediff_AsphodelBond : HediffWithComps
    {
        private static readonly Vector3 ShimmerOffset = new Vector3(0f, 0f, 0.1f);

        private int _totalDurationTicks = 750;
        private bool _suppressedRemoval;

        [Unsaved]
        private Effecter _auraEffecter;
        [Unsaved]
        private Mote _shimmerMote;
        [Unsaved]
        private HediffComp_Disappears _disappearsComp;

        private HediffComp_Disappears DisappearsComp => _disappearsComp ?? (_disappearsComp = this.TryGetComp<HediffComp_Disappears>());

        public int TicksLeft => DisappearsComp?.ticksToDisappear ?? 0;

        public float Progress => 1f - Mathf.Clamp01(TicksLeft / (float)Mathf.Max(1, _totalDurationTicks));

        public void Configure(int durationTicks)
        {
            _totalDurationTicks = Mathf.Max(1, durationTicks);
            var disappears = this.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
            {
                disappears.ticksToDisappear = _totalDurationTicks;
            }
        }

        public void SuppressRemovalNotification()
        {
            _suppressedRemoval = true;
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);

            var disappears = this.TryGetComp<HediffComp_Disappears>();
            if (disappears != null && disappears.ticksToDisappear > 0)
            {
                _totalDurationTicks = disappears.ticksToDisappear;
            }
        }

        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);

            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                CleanupEffects();
                return;
            }

            if (_auraEffecter == null && VVEffecterDefOf.VV_Effecter_AsphodelBondAura != null)
            {
                _auraEffecter = VVEffecterDefOf.VV_Effecter_AsphodelBondAura.SpawnAttached(pawn, pawn.Map);
            }
            _auraEffecter?.EffectTick(pawn, pawn);

            if ((_shimmerMote == null || _shimmerMote.Destroyed) && VVThingDefOf.VV_Mote_AsphodelBondShimmer != null)
            {
                _shimmerMote = MoteMaker.MakeAttachedOverlay(pawn, VVThingDefOf.VV_Mote_AsphodelBondShimmer, ShimmerOffset);
            }
            _shimmerMote?.Maintain();
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            CleanupEffects();

            if (_suppressedRemoval || pawn == null) { return; }

            var map = pawn.MapHeld;
            var boss = ApocryphaGimmicks.FindBoss(map);
            var chains = boss?.TryGetComp<CompApocrypha>()?.FairyChains;
            if (chains == null) { return; }

            if (TicksLeft <= 0 && !pawn.Dead)
            {
                chains.MarkBondExpired(pawn);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _totalDurationTicks, "totalDurationTicks", 750);
        }

        private void CleanupEffects()
        {
            EndingVfxUtility.CleanupEffecter(ref _auraEffecter);

            if (_shimmerMote != null && !_shimmerMote.Destroyed)
            {
                _shimmerMote.Destroy(DestroyMode.Vanish);
            }
            _shimmerMote = null;
        }
    }
}
