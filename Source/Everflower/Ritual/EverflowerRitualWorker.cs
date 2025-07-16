using RimWorld;
using System.Collections.Generic;
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

        public abstract IEnumerable<Pawn> GetCandidates(ArcanePlant_Everflower flower);

        public virtual AcceptanceReport CanRitual(ArcanePlant_Everflower everflower, Pawn caster)
        {
            if (caster.Downed)
            {
                return "DownedLower".Translate();
            }

            if (caster.InMentalState)
            {
                return LocalizeString_Etc.VV_FailReasonPawnMentalState.Translate(caster);
            }

            if (caster.GetStatValue(StatDefOf.PsychicSensitivity) < _def.requiredPsychicSensitivity)
            {
                return LocalizeString_Etc.VV_FailReasonPawnPsychicSensitivity.Translate();
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

        public abstract bool StartRitual(ArcanePlant_Everflower flower, Pawn caster);
    }
}
