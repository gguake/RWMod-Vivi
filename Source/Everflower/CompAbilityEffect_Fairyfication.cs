using RimWorld;
using Verse;

namespace VVRace
{
    public class AbilityCompProperties_Fairyfication : CompProperties_AbilityEffect
    {
        public EffecterDef completeEffect;

        public AbilityCompProperties_Fairyfication()
        {
            compClass = typeof(CompAbilityEffect_Fairyfication);
        }
    }

    public class CompAbilityEffect_Fairyfication : CompAbilityEffect
    {
        public new AbilityCompProperties_Fairyfication Props => (AbilityCompProperties_Fairyfication)props;

        public override bool CanCast => base.CanCast;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            var targetPawn = target.Pawn;
            if (targetPawn == null) { return false; }
            if (targetPawn.Faction != parent.pawn.Faction) { return false; }
            if (!targetPawn.IsVivi() || targetPawn.IsRoyalVivi()) { return false; }

            var compVivi = parent.pawn.GetCompVivi();
            if (compVivi == null || !compVivi.AttunementActive) { return false; }

            return true;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            var everflower = parent.pawn.GetCompVivi()?.LinkedEverflower;
            if (everflower == null) { return; }

            var targetPawn = target.Pawn;
            if (Props.completeEffect != null)
            {
                Props.completeEffect.Spawn(
                    new TargetInfo(targetPawn.Position, targetPawn.Map),
                    new TargetInfo(everflower));
            }

            base.Apply(target, dest);

            targetPawn.DeSpawn();
            everflower.EverflowerComp.GetDirectlyHeldThings().TryAdd(targetPawn);
        }

        public override bool AICanTargetNow(LocalTargetInfo target) => false;
    }
}
