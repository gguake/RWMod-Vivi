using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class HediffCompProperties_GiveEverflowerAbility : HediffCompProperties
    {
        public List<EverflowerAttunementAbilityInfo> abilities;

        public HediffCompProperties_GiveEverflowerAbility()
        {
            compClass = typeof(HediffComp_GiveEverflowerAbility);
        }
    }

    public class HediffComp_GiveEverflowerAbility : HediffComp
    {
        private HediffCompProperties_GiveEverflowerAbility Props => (HediffCompProperties_GiveEverflowerAbility)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            var compVivi = Pawn.GetCompVivi();
            if (compVivi == null || !compVivi.AttunementActive) { return; }

            var attunementLevel = compVivi.LinkedEverflower.AttunementLevel;
            foreach (var info in Props.abilities)
            {
                if (attunementLevel >= info.level)
                {
                    Pawn.abilities?.GainAbility(info.abilityDef);
                }
            }
        }

        public override void CompPostPostRemoved()
        {
            foreach (var info in Props.abilities)
            {
                Pawn.abilities?.RemoveAbility(info.abilityDef);
            }
        }

        public void CheckAndGiveAbility()
        {
            var compVivi = Pawn.GetCompVivi();
            if (compVivi == null || !compVivi.AttunementActive) { return; }

            var attunementLevel = compVivi.LinkedEverflower.AttunementLevel;
            foreach (var info in Props.abilities)
            {
                if (attunementLevel >= info.level)
                {
                    Pawn.abilities?.GainAbility(info.abilityDef);
                }
            }
        }
    }
}
