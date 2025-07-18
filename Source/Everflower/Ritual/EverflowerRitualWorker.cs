using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Sound;

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
            if (reservation.ritualDef.effectOnComplete != null)
            {
                reservation.ritualDef.effectOnComplete.SpawnMaintained(reservation.flower.Position, reservation.flower.Map);
            }

            if (reservation.ritualDef.soundOnComplete != null)
            {
                var sound = reservation.ritualDef.soundOnComplete;
                sound.PlayOneShot(new TargetInfo(reservation.flower.Position, reservation.flower.Map));
            }

            reservation.flower.Notify_RitualComplete(reservation.casterPawn);
        }

        public abstract Job TryGiveJob(EverflowerRitualReservation reservation);
    }
}
