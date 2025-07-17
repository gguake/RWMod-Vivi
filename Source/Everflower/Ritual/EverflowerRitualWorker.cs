using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public abstract class EverflowerRitualWorker
    {
        public virtual bool Targettable => false;

        protected EverflowerRitualDef _def;

        public EverflowerRitualWorker(EverflowerRitualDef def)
        {
            _def = def;
        }

        public virtual IEnumerable<Pawn> GetCandidates(ArcanePlant_Everflower flower)
        {
            foreach (var pawn in flower.Map.mapPawns.AllPawnsSpawned.Where(p => p.IsColonist))
            {
                var compVivi = pawn.GetCompVivi();
                if (compVivi == null || !compVivi.isRoyal) { continue; }

                var linkedFlower = compVivi.LinkedEverflower;
                if (linkedFlower == flower || (_def.allowUnlinkedPawn && linkedFlower == null))
                {
                    yield return pawn;
                }
            }
        }

        public virtual AcceptanceReport CanRitual(ArcanePlant_Everflower everflower, Pawn caster)
        {
            if (caster.Downed)
            {
                return "DownedLower".Translate();
            }

            if (caster.InMentalState)
            {
                return LocalizeString_Etc.VV_FailReason_PawnMentalState.Translate(caster);
            }

            if (caster.GetStatValue(StatDefOf.PsychicSensitivity) < _def.requiredPsychicSensitivity)
            {
                return LocalizeString_Etc.VV_FailReason_PawnPsychicSensitivity.Translate();
            }

            if (!caster.CanReserve(everflower, ignoreOtherReservations: true))
            {
                return "Reserved".Translate();
            }

            if (!caster.CanReach(everflower, PathEndMode.Touch, Danger.Deadly))
            {
                return "CannotReach".Translate();
            }

            return true;
        }

        public abstract void StartRitual(ArcanePlant_Everflower flower, Pawn caster, Action<EverflowerRitualReservation> onStartCallback);

        public virtual void Complete(EverflowerRitualReservation reservation)
        {
            reservation.flower.Notify_RitualComplete(reservation.casterPawn);
        }

        public abstract Job TryGiveJob(EverflowerRitualReservation reservation);
    }
}
