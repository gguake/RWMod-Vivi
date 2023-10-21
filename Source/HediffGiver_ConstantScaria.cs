using HarmonyLib;
using RimWorld;
using System.Reflection;
using Verse;

namespace VVRace
{
    public class HediffGiver_ConstantScaria : HediffGiver
    {
        private static FieldInfo fieldInfo_HediffComp_KillAfterDays_ticksLeft = AccessTools.Field(typeof(HediffComp_KillAfterDays), "ticksLeft");

        public override void OnIntervalPassed(Pawn pawn, Hediff cause)
        {
            if (pawn.Awake())
            {
                if (!TryApply(pawn))
                {
                    var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Scaria);
                    if (hediff != null)
                    {
                        var killComp = hediff.TryGetComp<HediffComp_KillAfterDays>();
                        if (killComp != null)
                        {
                            var maxDays = 60000 * killComp.Props.days;
                            fieldInfo_HediffComp_KillAfterDays_ticksLeft.SetValue(killComp, maxDays);
                        }
                    }
                }

                if (!pawn.InMentalState)
                {
                    pawn.mindState.mentalStateHandler.TryStartMentalState(VVMentalStateDefOf.VV_HornetBerserk, transitionSilently: true);
                }
            }
        }
    }
}
