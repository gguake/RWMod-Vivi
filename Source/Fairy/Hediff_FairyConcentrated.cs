using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{

    public class Hediff_FairyConcentrated : HediffWithComps
    {
        public Pawn ownerVivi;
        [Unsaved]
        private Effecter _targetMarkerEffecter;

        public override bool ShouldRemove
        {
            get
            {
                if (base.ShouldRemove) { return true; }
                if (ownerVivi == null || ownerVivi.Dead || !ownerVivi.Spawned) { return true; }
                if (pawn == null || pawn.DeadOrDowned || !pawn.Spawned) { return true; }
                if (pawn.Map != ownerVivi.Map) { return true; }
                if (!pawn.HostileTo(ownerVivi)) { return true; }
                return false;
            }
        }

        public static Hediff_FairyConcentrated GetOwnedBy(Pawn pawn, Pawn owner)
        {
            if (pawn == null || pawn.health == null || owner == null) { return null; }

            var hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_FairyConcentrated marker &&
                    marker.def == VVHediffDefOf.VV_FairyConcentrated &&
                    marker.IsOwnedBy(owner))
                {
                    return marker;
                }
            }

            return null;
        }

        public static Hediff_FairyConcentrated GetTargetOwnedBy(Pawn owner)
        {
            if (owner == null || owner.Map == null) { return null; }

            var pawns = owner.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var marker = GetOwnedBy(pawns[i], owner);
                if (marker != null) { return marker; }
            }

            return null;
        }

        public static void RemoveOwnedBy(Pawn owner)
        {
            if (owner == null || Find.Maps == null) { return; }

            var markers = new List<Hediff_FairyConcentrated>();
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn == null || pawn.health == null) { continue; }

                    var hediffs = pawn.health.hediffSet.hediffs;
                    for (int i = 0; i < hediffs.Count; i++)
                    {
                        if (hediffs[i] is Hediff_FairyConcentrated marker &&
                            marker.def == VVHediffDefOf.VV_FairyConcentrated &&
                            marker.ownerVivi == owner)
                        {
                            markers.Add(marker);
                        }
                    }
                }
            }

            foreach (var marker in markers)
            {
                marker.pawn?.health?.RemoveHediff(marker);
            }
        }

        public bool IsOwnedBy(Pawn owner)
        {
            return owner != null && ownerVivi == owner && !ShouldRemove;
        }

        public override void Tick()
        {
            base.Tick();
            MaintainTargetMarkerEffect();
        }

        public override void PostRemoved()
        {
            EndTargetMarkerEffect();
            base.PostRemoved();
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            EndTargetMarkerEffect();

            // Pawn.Kill이 hediff 목록을 정방향 순회하며 이 메서드를 호출하므로,
            // 여기서 즉시 제거하면 목록이 당겨져 다음 hediff의 Notify_PawnDied가 건너뛰어진다.
            var deadPawn = pawn;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (deadPawn?.health != null && deadPawn.health.hediffSet.hediffs.Contains(this))
                {
                    deadPawn.health.RemoveHediff(this);
                }
            });
        }

        private void MaintainTargetMarkerEffect()
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null || ShouldRemove)
            {
                EndTargetMarkerEffect();
                return;
            }

            if (_targetMarkerEffecter == null)
            {
                _targetMarkerEffecter = VVEffecterDefOf.VV_Effecter_FairyTargetMarker?.SpawnAttached(pawn, pawn.Map);
            }

            _targetMarkerEffecter?.EffectTick(new TargetInfo(pawn), TargetInfo.Invalid);
        }

        private void EndTargetMarkerEffect()
        {
            if (_targetMarkerEffecter == null) { return; }

            _targetMarkerEffecter.ForceEnd();
            _targetMarkerEffecter = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerVivi, "ownerVivi");
        }
    }
}
