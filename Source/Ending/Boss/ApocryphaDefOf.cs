using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 아포크리파 본체 모듈(Task D) 전용 DefOf. 공용 VVDefOf.cs는 건드리지 않는다.
    [DefOf]
    public static class ApocryphaThingDefOf
    {
        public static ThingDef VV_Apocrypha;
        public static ThingDef VV_DyingApocrypha;

        public static ThingDef VV_Mote_ApocryphaAimTarget;
        public static ThingDef VV_Mote_ApocryphaAimCharge;
        public static ThingDef VV_Mote_ApocryphaDrainVortex;
    }

    [DefOf]
    public static class ApocryphaPawnKindDefOf
    {
        public static PawnKindDef VV_Apocrypha;
    }

    [DefOf]
    public static class ApocryphaHediffDefOf
    {
        public static HediffDef VV_ApocryphaFairyBond;
        public static HediffDef VV_ApocryphaCarapace;
    }

    [DefOf]
    public static class ApocryphaJobDefOf
    {
        public static JobDef VV_ApocryphaWait;
        public static JobDef VV_ApocryphaWander;
        public static JobDef VV_ApocryphaApproach;
        public static JobDef VV_ApocryphaBreath;
        public static JobDef VV_ApocryphaChannel;
        public static JobDef VV_ApocryphaCharge;
        public static JobDef VV_ApocryphaSmash;
    }

    [DefOf]
    public static class ApocryphaDutyDefOf
    {
        public static DutyDef VV_TitanicHornetEscort;
        public static DutyDef VV_TitanicHornetFlowerGuard;
    }

    [DefOf]
    public static class ApocryphaEffecterDefOf
    {
        public static EffecterDef VV_Effecter_ApocryphaArrive;
        public static EffecterDef VV_Effecter_ApocryphaAim;
        public static EffecterDef VV_Effecter_ApocryphaAimSustain;
        public static EffecterDef VV_Effecter_ApocryphaShockwave;
        public static EffecterDef VV_Effecter_ApocryphaTransform;
        public static EffecterDef VV_Effecter_ApocryphaMorphPulse;
        public static EffecterDef VV_Effecter_ApocryphaChannel;
        public static EffecterDef VV_Effecter_ApocryphaBreath;
        public static EffecterDef VV_Effecter_ApocryphaDeath;
        public static EffecterDef VV_Effecter_ApocryphaSteal;
        public static EffecterDef VV_Effecter_ApocryphaVanish;
        public static EffecterDef VV_Effecter_ApocryphaSmash;
        public static EffecterDef VV_Effecter_ApocryphaStaggered;
    }

    [DefOf]
    public static class ApocryphaFleckDefOf
    {
        public static FleckDef VV_Fleck_ApocryphaToxicGas;
        public static FleckDef VV_Fleck_ApocryphaToxicGasViolet;
        public static FleckDef VV_Fleck_ApocryphaAsh;
        public static FleckDef VV_Fleck_ApocryphaEmber;
    }
}
