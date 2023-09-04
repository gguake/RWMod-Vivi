using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using VVRace.HarmonyPatches.Internals;

namespace VVRace
{
    public class HediffCompProperties_ViviGrowthBoost : HediffCompProperties
    {
        public int tickInterval;
        public float additionalAgeBoostMultiplier = 0.0f;

        public HediffCompProperties_ViviGrowthBoost()
        {
            compClass = typeof(HediffComp_ViviGrowthBoost);
        }
    }
    
    public class HediffComp_ViviGrowthBoost : HediffComp
    {
        public override bool CompShouldRemove => !(Pawn is Vivi && !Pawn.DevelopmentalStage.Adult() && !Pawn.Dead);

        public override string CompTipStringExtra => (string)("AgingSpeed".Translate() + ": x") + (((HediffCompProperties_ViviGrowthBoost)props).additionalAgeBoostMultiplier + 1f).ToString("F1");

        public override void CompPostTick(ref float severityAdjustment)
        {
            var myProps = (HediffCompProperties_ViviGrowthBoost)props;
            if (!Pawn.Suspended && Pawn.IsHashIntervalTick(myProps.tickInterval) && Pawn.ageTracker != null)
            {
                var ticks = (int)(myProps.tickInterval * Mathf.Max(myProps.additionalAgeBoostMultiplier, 0f));
                if (ticks > 0)
                {
                    AddProgressToNextBiologicalTick(Pawn.ageTracker, ticks);
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [ILOverride(typeof(HediffComp_ViviGrowthBoost), nameof(AddProgressToNextBiologicalTick_IL))]
        private static void AddProgressToNextBiologicalTick(Pawn_AgeTracker ageTracker, float value) { }
        private static IEnumerable<CodeInstruction> AddProgressToNextBiologicalTick_IL(IEnumerable<CodeInstruction> _)
        {
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Dup);
            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Pawn_AgeTracker), "progressToNextBiologicalTick"));
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Add);
            yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(Pawn_AgeTracker), "progressToNextBiologicalTick"));
            yield return new CodeInstruction(OpCodes.Ret);
        }
    }
}