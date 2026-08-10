using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompProperties_ScentVerbOwner : CompProperties_ApparelVerbOwner
    {
        public CompProperties_ScentVerbOwner()
        {
            compClass = typeof(CompScentVerbOwner);
        }
    }

    public class CompScentVerbOwner : CompApparelVerbOwner
    {
        private CompScentBottle _cachedBottleComp;
        public CompScentBottle BottleComp
        {
            get
            {
                if (_cachedBottleComp == null)
                {
                    _cachedBottleComp = parent.TryGetComp<CompScentBottle>();
                }
                return _cachedBottleComp;
            }
        }

        public override string GizmoExtraLabel => BottleComp?.RemainingSprays.ToString();

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
            if (BottleComp?.HasScent != true || Find.Selector.SelectedPawns.Count > 1)
            {
                yield break;
            }

            SetCaster(Wearer);

            foreach (var gizmo in base.CompGetWornGizmosExtra())
            {
                if (gizmo is Command_VerbTarget command && command.verb is Verb_SprayScent sprayVerb)
                {
                    command.defaultLabel = LocalizeString_Scent.VV_Command_SprayScent.Translate();
                    command.defaultDesc = LocalizeString_Scent.VV_Command_SprayScentDesc.Translate(
                        BottleComp.ScentFlower.LabelCap,
                        sprayVerb.verbProps.range,
                        BottleComp.RemainingSprays);
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

    public class VerbProperties_SprayScent : VerbProperties
    {
        public VerbProperties_SprayScent()
        {
            verbClass = typeof(Verb_SprayScent);
            drawAimPie = false;
        }
    }

    public class Verb_SprayScent : Verb
    {
        private CompScentBottle BottleComp => (DirectOwner as CompScentVerbOwner)?.BottleComp;

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
                BottleComp?.HasScent == true &&
                BottleComp.WearingPawn == CasterPawn;
        }

        protected override bool TryCastShot()
        {
            var sprayed = BottleComp?.TrySpray(CasterPawn, verbProps.range) == true;
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
                GenDraw.DrawRadiusRing(caster.Position, verbProps.range);
            }
        }
    }
}
