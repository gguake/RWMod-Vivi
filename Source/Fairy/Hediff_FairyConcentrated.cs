using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{

    public class Hediff_FairyConcentrated : HediffWithComps
    {
        public Pawn ownerVivi;

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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerVivi, "ownerVivi");
        }
    }
}
