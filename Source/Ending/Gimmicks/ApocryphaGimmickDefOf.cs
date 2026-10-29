using RimWorld;
using Verse;

namespace VVRace
{
    // 아포크리파 1페이즈 기믹(사출탄·멸망꽃/요정꽃·요정 간섭·연결선) 모듈 전용 DefOf.
    // 병렬 작업 충돌을 피하기 위해 VVDefOf.cs 대신 이 폴더에 둔다.
    [DefOf]
    public static class ApocryphaGimmickDefOf
    {
        public static ThingDef VV_Projectile_FairyShell;
        public static ThingDef VV_Projectile_DoomSeed;
        public static ThingDef VV_Projectile_FairySeed;

        public static ThingDef VV_DoomFlower;
        public static ThingDef VV_FairyFlower;

        public static HediffDef VV_FairyInterference;
        public static HediffDef VV_DoomBlight_Fear;
        public static HediffDef VV_DoomBlight_Hunger;
        public static HediffDef VV_DoomBlight_Plague;

        public static JobDef VV_BondFairyFlower;

        public static EffecterDef VV_Effecter_DoomBloomCurse;
        public static EffecterDef VV_Effecter_FairyFlowerBonding;
        public static EffecterDef VV_Effecter_ManaBackflow;
        public static EffecterDef VV_Effecter_FairyInterferenceAura;
        public static EffecterDef VV_Effecter_FairyReturn;

        public static FleckDef VV_Fleck_GimmickBackflowBeam;
    }
}
