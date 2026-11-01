using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Verb_CastAbilityMaterialize : Verb_CastAbility
    {
        public override bool TryStartCastOn(
            LocalTargetInfo castTarg,
            LocalTargetInfo destTarg,
            bool surpriseAttack = false,
            bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false,
            bool nonInterruptingSelfCast = false)
        {
            if (!base.TryStartCastOn(
                castTarg,
                destTarg,
                surpriseAttack,
                canHitNonTargetPawns,
                preventFriendlyFire,
                nonInterruptingSelfCast))
            {
                return false;
            }

            if (WarmupStance != null)
            {
                WarmupStance.neverAimWeapon = true;
            }

            return true;
        }

        public override float WarmupTime
        {
            get
            {
                float baseWarmup = verbProps.warmupTime;
                if (CasterPawn == null) { return baseWarmup; }

                float psy = Mathf.Clamp(CasterPawn.GetStatValue(StatDefOf.PsychicSensitivity), 0.5f, 10f);
                return baseWarmup / psy;
            }
        }
    }
}
