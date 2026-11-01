using RimWorld;
using Verse;

namespace VVRace
{
    [DefOf]
    public static class QueenDefOf
    {
        public static HediffDef VV_EverflowerQueen;

        public static ThingDef VV_SkyShield;
        public static ThingDef VV_QueenManaBarrierShield;
        public static ThingDef VV_QueenVerdictNeedle;
        public static ThingDef VV_WindOfRest;
        public static ThingDef VV_DissolvingHornet;
        public static ThingDef VV_TitanicHornetWing;

        public static EffecterDef VV_Effecter_QueenCorpseBloom;
        public static EffecterDef VV_Effecter_QueenRebirth;
        public static EffecterDef VV_Effecter_QueenVerdictSummon;
        public static EffecterDef VV_Effecter_QueenManaBarrier;
        public static EffecterDef VV_Effecter_WindOfRestCleanse;
        public static EffecterDef VV_Effecter_HornetDissolve;

        public static FleckDef VV_Fleck_WindOfRestGust;
        public static FleckDef VV_Fleck_HornetDust;

        static QueenDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(QueenDefOf));
        }
    }
}
