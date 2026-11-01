using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    [DefOf]
    public static class ApocryphaThingDefOf
    {
        public static ThingDef VV_Apocrypha;
        public static ThingDef VV_DyingApocrypha;
        public static ThingDef VV_ApocryphaManifestation;

        public static ThingDef VV_Mote_ApocryphaAimTarget;
        public static ThingDef VV_Mote_ApocryphaAimCharge;
        public static ThingDef VV_Mote_ApocryphaDrainVortex;
        public static ThingDef VV_Mote_ApocryphaRepelCharge;

        public static ThingDef VV_ApocryphaNeedle;
    }

    [DefOf]
    public static class ApocryphaDamageDefOf
    {
        public static DamageDef VV_GiantNeedlePierce;
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
        public static HediffDef VV_ApocryphaStuckNeedle;
    }

    [DefOf]
    public static class ApocryphaJobDefOf
    {
        public static JobDef VV_ApocryphaWait;
        public static JobDef VV_ApocryphaWander;
        public static JobDef VV_ApocryphaApproach;
        public static JobDef VV_ApocryphaPursue;
        public static JobDef VV_ApocryphaBreath;
        public static JobDef VV_ApocryphaChannel;
        public static JobDef VV_ApocryphaCharge;
        public static JobDef VV_ApocryphaSmash;
        public static JobDef VV_ActivateApocryphaNeedle;
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
        public static EffecterDef VV_Effecter_ApocryphaShieldBreak;
        public static EffecterDef VV_Effecter_ApocryphaShieldRestore;
        public static EffecterDef VV_Effecter_ApocryphaNeedleAim;
        public static EffecterDef VV_Effecter_ApocryphaNeedleFire;
        public static EffecterDef VV_Effecter_ApocryphaNeedleHit;
        public static EffecterDef VV_Effecter_ApocryphaNeedleEnd;
        public static EffecterDef VV_Effecter_ApocryphaNeedleVanish;
        public static EffecterDef VV_Effecter_ApocryphaNeedleStick;
        public static EffecterDef VV_Effecter_ApocryphaNeedlePullOut;
        public static EffecterDef VV_Effecter_ApocryphaNeedlePlant;
        public static EffecterDef VV_Effecter_ApocryphaManifestComplete;
        public static EffecterDef VV_Effecter_ApocryphaRepel;
        public static EffecterDef VV_Effecter_ApocryphaManifestLand;
    }

    [DefOf]
    public static class ApocryphaFleckDefOf
    {
        public static FleckDef VV_Fleck_ApocryphaToxicGas;
        public static FleckDef VV_Fleck_ApocryphaToxicGasViolet;
        public static FleckDef VV_Fleck_ApocryphaAsh;
        public static FleckDef VV_Fleck_ApocryphaEmber;
        public static FleckDef VV_Fleck_ApocryphaStealPetal;
        public static FleckDef VV_Fleck_ApocryphaDarkWisp;
        public static FleckDef VV_Fleck_ApocryphaManifestSpark;
        public static FleckDef VV_Fleck_ApocryphaRepelWave;
        public static FleckDef VV_Fleck_ApocryphaAimSpark;
        public static FleckDef VV_Fleck_ApocryphaAimTwinkle;
    }

    [DefOf]
    public static class ApocryphaSoundDefOf
    {
        public static SoundDef PsychicArtifactWarmupSustained;
    }
}
