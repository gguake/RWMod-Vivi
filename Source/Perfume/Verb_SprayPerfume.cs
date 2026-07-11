using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompProperties_PerfumeVerbOwner : CompProperties_ApparelVerbOwner
    {
        public CompProperties_PerfumeVerbOwner()
        {
            compClass = typeof(CompPerfumeVerbOwner);
        }
    }

    public class CompPerfumeVerbOwner : CompApparelVerbOwner
    {
        public CompPerfumeBottle BottleComp => parent.TryGetComp<CompPerfumeBottle>();

        public override string GizmoExtraLabel => BottleComp?.SpraysRemaining.ToString();

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            SetCaster(pawn);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            foreach (var verb in AllVerbs)
            {
                verb.Notify_EquipmentLost();
                verb.caster = null;
            }

            base.Notify_Unequipped(pawn);
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            if (BottleComp?.IsComplete != true || Find.Selector.SelectedPawns.Count > 1)
            {
                yield break;
            }

            SetCaster(Wearer);

            foreach (var gizmo in base.CompGetWornGizmosExtra())
            {
                if (gizmo is Command_VerbTarget command && command.verb is Verb_SprayPerfume sprayVerb)
                {
                    command.defaultLabel = LocalizeString_Perfume.VV_Command_SprayPerfume.Translate();
                    command.defaultDesc = LocalizeString_Perfume.VV_Command_SprayPerfumeDesc.Translate(
                        sprayVerb.VerbProps.range,
                        BottleComp.SpraysRemaining);
                }

                yield return gizmo;
            }
        }

        private void SetCaster(Pawn pawn)
        {
            foreach (var verb in AllVerbs)
            {
                verb.caster = pawn;
            }
        }
    }

    public class VerbProperties_SprayPerfume : VerbProperties
    {
        public VerbProperties_SprayPerfume()
        {
            verbClass = typeof(Verb_SprayPerfume);
            drawAimPie = false;
        }
    }

    public class Verb_SprayPerfume : Verb
    {
        public VerbProperties_SprayPerfume VerbProps => (VerbProperties_SprayPerfume)verbProps;

        private CompPerfumeBottle BottleComp => (DirectOwner as CompPerfumeVerbOwner)?.BottleComp;

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

        public override bool Available()
        {
            return base.Available() &&
                BottleComp?.IsComplete == true &&
                BottleComp.WearingPawn == CasterPawn;
        }

        protected override bool TryCastShot()
        {
            var sprayed = BottleComp?.TrySpray(CasterPawn, VerbProps.range) == true;
            if (sprayed)
            {
                lastShotTick = Find.TickManager.TicksGame;
            }

            return sprayed;
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            if (caster?.Spawned == true)
            {
                GenDraw.DrawRadiusRing(caster.Position, VerbProps.range);
            }
        }
    }
}
