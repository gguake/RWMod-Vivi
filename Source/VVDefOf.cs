using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    [DefOf]
    public static class VVDamageDefOf
    {
        public static DamageDef VV_ManawaveStun;

        public static DamageDef VV_GiantNeedlePierce;
    }

    [DefOf]
    public static class VVStatDefOf
    {
        public static StatDef VV_HoneyGatherSpeed;
        public static StatDef VV_HoneyGatherYield;
        public static StatDef VV_PlantGatherSpeed;
        public static StatDef VV_PlantGatherYield;

        public static StatDef VV_MinGrowthPlantGatherable;
        public static StatDef VV_PlantGatherCooldown;
        public static StatDef VV_PlantHoneyGatherYield;
        public static StatDef VV_TreeResinGatherYield;

        public static StatDef VV_GrowthPointsFactor;
        public static StatDef VV_MeleeWeapon_ManaCost;
        public static StatDef VV_RangedWeapon_ManaCost;
    }

    [DefOf]
    public static class VVWorkTypeDefOf
    {
        public static WorkTypeDef Patient;
        public static WorkTypeDef PatientBedRest;
        public static WorkTypeDef Cooking;
        public static WorkTypeDef VV_Gathering;
    }

    [DefOf]
    public static class VVNeedDefOf
    {
        public static NeedDef VV_RoyalJelly;
    }

    [DefOf]
    public static class VVXenotypeDefOf
    {
        public static XenotypeDef VV_Vivi;
    }

    [DefOf]
    public static class VVJobDefOf
    {
        public static JobDef VV_LayViviEgg;

        public static JobDef VV_GatherHoney;
        public static JobDef VV_GatherPropolis;

        public static JobDef VV_FortifyHoneycombWall;

        public static JobDef VV_PlantArcaneSeed;

        public static JobDef VV_MoveEverflower;
        public static JobDef VV_DeliverToEverflower;

        public static JobDef VV_CollectFloralScent;

        public static JobDef VV_ApocryphaWait;
        public static JobDef VV_ApocryphaWander;
        public static JobDef VV_ApocryphaApproach;
        public static JobDef VV_ApocryphaPursue;
        public static JobDef VV_ApocryphaBreath;
        public static JobDef VV_ApocryphaChannel;
        public static JobDef VV_ApocryphaCharge;
        public static JobDef VV_ApocryphaSmash;
        public static JobDef VV_ActivateApocryphaNeedle;

        public static JobDef VV_BondFairyFlower;

        public static JobDef VV_ViviInspectLure;
        public static JobDef VV_DevourBait;
        public static JobDef VV_ViviNestUnload;
        public static JobDef VV_ViviNestDigFertilizer;
        public static JobDef VV_ViviNestBuildRoof;
        public static JobDef VV_ViviNestFertilizePlant;
        public static JobDef VV_ViviNestGatherHoney;
        public static JobDef VV_ViviNestGatherPollen;
        public static JobDef VV_ViviNestGatherPropolis;
        public static JobDef VV_ViviNestNap;
        public static JobDef VV_ViviNestLoaf;
        public static JobDef VV_ViviNestClean;
        public static JobDef Play_Horseshoes;
    }

    [DefOf]
    public static class VVThingDefOf
    {
        public static ThingDef VV_Vivi;
        public static ThingDef VV_ViviEgg;

        public static ThingDef VV_ViviHatchery;

        public static ThingDef VV_RawHoney;
        public static ThingDef VV_FilteredHoney;

        public static ThingDef VV_FilthPollen;

        public static ThingDef VV_Vivicream;
        public static ThingDef VV_Viviwax;
        public static ThingDef VV_Lifestrand;

        public static ThingDef VV_Seed_UnknownPlant;
        public static ThingDef VV_Fertilizer;

        public static ThingDef VV_GatheringBarrel;
        public static ThingDef VV_RefiningWorkbench;
        public static ThingDef VV_ArcanePlantPot;
        public static ThingDef VV_ManaEmitter;

        public static ThingDef VV_EmberBloom;
        public static ThingDef VV_FrostBloom;
        public static ThingDef VV_Shockerbud;
        public static ThingDef VV_Thunderpetals;
        public static ThingDef VV_Waterdrops;
        public static ThingDef VV_Radiantflower;
        public static ThingDef VV_Richflower;
        public static ThingDef VV_Peashooter;
        public static ThingDef VV_Cornlauncher;
        public static ThingDef VV_Shootus;
        public static ThingDef VV_Skyweed;
        public static ThingDef VV_Everflower;

        public static ThingDef VV_ArcanePlantSeedling;

        public static ThingDef VV_ViviCreamWall;
        public static ThingDef VV_SmoothedViviCreamWall;
        public static ThingDef VV_ViviHoneycombWall;
        public static ThingDef VV_ViviHardenHoneycombWall;

        public static ThingDef Plant_Strawberry;
        public static ThingDef Plant_Corn;
        public static ThingDef Plant_Cotton;
        public static ThingDef Plant_Healroot;
        public static ThingDef Plant_Daylily;
        public static ThingDef Plant_Rose;

        public static ThingDef Shelf;
        public static ThingDef ShelfSmall;
        public static ThingDef HorseshoesPin;
        public static ThingDef Crib;
        public static ThingDef Table3x3c;
        public static ThingDef PlantPot;

        public static ThingDef HandTailoringBench;
        public static ThingDef FueledSmithy;

        public static ThingDef VV_TitanicHornet;

        public static ThingDef VV_Plant_FireArcaneFlower;
        public static ThingDef VV_Plant_IceArcaneFlower;
        public static ThingDef VV_Plant_LightningArcaneFlower;
        public static ThingDef VV_Plant_LifeArcaneFlower;

        public static ThingDef VV_SteamProjectile;
        public static ThingDef VV_ExplosiveSnowman;

        public static ThingDef VV_ViviFairy;

        public static ThingDef VV_EverflowerAltar;

        public static ThingDef VV_AncientAmberCore;

        public static ThingDef VV_Apocrypha;
        public static ThingDef VV_DyingApocrypha;
        public static ThingDef VV_ApocryphaManifestation;
        public static ThingDef VV_HornetManifestation;

        public static ThingDef VV_Mote_ApocryphaAimTarget;
        public static ThingDef VV_Mote_ApocryphaAimCharge;
        public static ThingDef VV_Mote_ApocryphaDrainVortex;
        public static ThingDef VV_Mote_ApocryphaRepelCharge;
        public static ThingDef VV_Mote_FairyInterferenceShimmer;

        public static ThingDef VV_ApocryphaNeedle;

        public static ThingDef VV_Projectile_FairyShell;
        public static ThingDef VV_Projectile_DoomSeed;
        public static ThingDef VV_Projectile_FairySeed;

        public static ThingDef VV_DoomFlower;
        public static ThingDef VV_FairyFlower;

        public static ThingDef VV_SkyShield;
        public static ThingDef VV_QueenManaBarrierShield;
        public static ThingDef VV_QueenVerdictNeedle;
        public static ThingDef VV_WindOfRest;
        public static ThingDef VV_DissolvingHornet;
        public static ThingDef VV_TitanicHornetWing;

        public static ThingDef VV_Mote_EverflowerCircle;

        public static ThingDef VV_ViviLure;
        public static ThingDef VV_BasicWorkerCostume;
        public static ThingDef VV_Childrenswear;
        public static ThingDef RawBerries;
        public static ThingDef VV_ViviNestWorkSpot;
        public static ThingDef VV_Pollen;
        public static ThingDef VV_Propolis;
        public static ThingDef VV_HoneyPollenCake;
        public static ThingDef VV_Slingshot;
    }

    [DefOf]
    public static class VVAbilityDefOf
    {
        public static AbilityDef VV_FairyMaterialize;
        public static AbilityDef VV_FairyGuard;
        public static AbilityDef VV_FairyConcentration;
        public static AbilityDef VV_FairyExpansion;
    }

    [DefOf]
    public static class VVPawnKindDefOf
    {
        public static PawnKindDef VV_PlayerVivi;
        public static PawnKindDef VV_PlayerExiledRoyalVivi;
        public static PawnKindDef VV_RoyalVivi;

        public static PawnKindDef VV_TitanicHornet;

        // 여제·지도자 유형은 사망 시 연방이 적대화되므로 지원군으로 사용하지 않는다.
        public static PawnKindDef VV_ViviGuard;
        public static PawnKindDef VV_ViviFirefighter;
        public static PawnKindDef VV_ViviSniper;

        public static PawnKindDef VV_WanderingViviWorker;
        public static PawnKindDef VV_WanderingViviNestborn;
        public static PawnKindDef VV_WanderingViviSlinger;
        public static PawnKindDef VV_WanderingRoyalVivi;
        public static PawnKindDef Bear_Grizzly;
        public static PawnKindDef Bear_Polar;

        public static PawnKindDef VV_Apocrypha;
    }

    [DefOf]
    public static class VVRecipeDefOf
    {
        public static RecipeDef VV_GatherHoney;
        public static RecipeDef VV_GatherPropolis;

        public static RecipeDef VV_MakeVivicream;
        public static RecipeDef VV_FilteringHoney;

        public static RecipeDef VV_GatherPollen;
        public static RecipeDef VV_MakeViviwax;
        public static RecipeDef VV_Make_HoneyPollenCake;
        public static RecipeDef Make_VV_BasicWorkerCostume;
        public static RecipeDef Make_VV_Childrenswear;
        public static RecipeDef Make_VV_Slingshot;
    }

    [DefOf]
    public static class VVDesignationDefOf
    {
        public static DesignationDef VV_FortifyHoneycombWall;
    }

    [DefOf]
    public static class VVDesignationCategoryDefOf
    {
        public static DesignationCategoryDef VV_Bulidings;
    }

    [DefOf]
    public static class VVMentalBreakDefOf
    {
        public static MentalBreakDef RunWild;
        public static MentalBreakDef GiveUpExit;
    }

    [DefOf]
    public static class VVThoughtDefOf
    {
        public static ThoughtDef VV_AtePollen;

        public static ThoughtDef VV_EverflowerFullBloom;
    }

    [DefOf]
    public static class VVHediffDefOf
    {
        public static HediffDef VV_RoyalVivi;
        public static HediffDef VV_HoneyNeed;
        public static HediffDef VV_CombatHormoneJelly;
        public static HediffDef VV_EverflowerImpact;

        public static HediffDef Flu;
        public static HediffDef Malaria;

        public static HediffDef VV_EverflowerLink;
        public static HediffDef VV_EverflowerReverberation;
        public static HediffDef VV_ViviFairyFollow;
        public static HediffDef VV_FairyficationSickness;

        public static HediffDef VV_FairyMastery;
        public static HediffDef VV_EverflowerAssimilation;
        public static HediffDef VV_FairyGuarded;
        public static HediffDef VV_FairyConcentrated;
        public static HediffDef VV_ArchiteGermlineImprint;

        public static HediffDef VV_ApocryphaFairyBond;
        public static HediffDef VV_ApocryphaStuckNeedle;
        public static HediffDef VV_CorruptedEverflowerAssimilation;

        public static HediffDef VV_FairyInterference;
        public static HediffDef VV_DoomBlight_Fear;
        public static HediffDef VV_DoomBlight_Hunger;
        public static HediffDef VV_DoomBlight_Plague;

        public static HediffDef VV_EverflowerQueen;

        public static HediffDef VV_EverflowerBlessing;
        public static HediffDef VV_GeneticUnstability;

        public static HediffDef VV_EverflowerFullAssimilation;
    }

    [DefOf]
    public static class VVMapGeneratorDefOf
    {
        public static MapGeneratorDef VV_Base_ViviFaction;
    }

    [DefOf]
    public static class VVCultureDefOf
    {
        public static CultureDef VV_ViviCulture;
        public static CultureDef Astropolitan;
    }

    [DefOf]
    public static class VVTerrainDefOf
    {
        public static TerrainDef VV_ViviCreamFloor;
        public static TerrainDef VV_HoneycombTile;
        public static TerrainDef VV_SterileHoneycombTile;
    }

    [DefOf]
    public static class VVThingSetMakerDefOf
    {
        public static ThingSetMakerDef VV_SettlementDiningRoomThingSet;
        public static ThingSetMakerDef VV_SettlementGreenHouseThingSet;
        public static ThingSetMakerDef VV_SettlementGatheringRoomThingSet;
        public static ThingSetMakerDef VV_SettlementRefiningRoomThingSet;
        public static ThingSetMakerDef VV_SettlementCraftingRoomThingSet;
    }

    [DefOf]
    public static class VVMentalStateDefOf
    {
        public static MentalStateDef VV_HornetBerserk;
    }

    [DefOf]
    public static class VVMapMeshFlagDefOf
    {
        public static MapMeshFlagDef VV_ManaFluxGrid;
    }

    [DefOf]
    public static class VVEffecterDefOf
    {
        public static EffecterDef VV_PollenEmitting;
        public static EffecterDef VV_Gather_Honey;
        public static EffecterDef VV_EverflowerLink;
        public static EffecterDef VV_StalitFlowerAura;

        public static EffecterDef VV_EverflowerGrow_1_Level;
        public static EffecterDef VV_EverflowerGrow_2_Level;
        public static EffecterDef VV_EverflowerGrow_3_Level;
        public static EffecterDef VV_EverflowerGrow_4_Level;

        public static EffecterDef VV_Effecter_FairyPhase;
        public static EffecterDef VV_Effecter_FairyTargetMarker;

        public static EffecterDef VV_Gather_Scent;
        public static EffecterDef VV_ScentSpray;

        public static EffecterDef VV_Effecter_AltarEnshrine;

        public static EffecterDef VV_Effecter_EndingGoldenWave;

        public static EffecterDef VV_Effecter_AltarEverflowerPulse_5;
        public static EffecterDef VV_Effecter_AltarEverflowerPulse_6;

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

        public static EffecterDef VV_Effecter_DoomBloomCurse;
        public static EffecterDef VV_Effecter_FairyFlowerBonding;
        public static EffecterDef VV_Effecter_ManaBackflow;
        public static EffecterDef VV_Effecter_FairyInterferenceAura;
        public static EffecterDef VV_Effecter_FairyReturn;

        public static EffecterDef VV_Effecter_QueenCorpseBloom;
        public static EffecterDef VV_Effecter_QueenRebirth;
        public static EffecterDef VV_Effecter_QueenVerdictSummon;
        public static EffecterDef VV_Effecter_QueenManaBarrier;
        public static EffecterDef VV_Effecter_WindOfRestCleanse;
        public static EffecterDef VV_Effecter_HornetDissolve;
        public static EffecterDef VV_Effecter_HornetAshen;
        public static EffecterDef VV_Effecter_HornetManifestBegin;
        public static EffecterDef VV_Effecter_HornetManifestComplete;

        public static EffecterDef VV_Effecter_AssimilationBegin;
        public static EffecterDef VV_Effecter_AssimilationAbsorb;
    }

    [DefOf]
    public static class VVChemicalDefOf
    {
        public static ChemicalDef Ambrosia;
    }

    [DefOf]
    public static class VVFleckDefOf
    {
        public static FleckDef HeatGlow_Intense;
        public static FleckDef ElectricalSpark;
        public static FleckDef Fleck_VaporizeCenterFlash;

        public static FleckDef VV_Fleck_Steam;

        public static FleckDef VV_Fleck_NeedlePierce;
        public static FleckDef VV_Fleck_NeedleShard;

        public static FleckDef VV_Fleck_GoldenWaveRing;
        public static FleckDef VV_Fleck_EndingPetal;

        public static FleckDef VV_Fleck_GoldenBloomPetal;

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

        public static FleckDef VV_Fleck_GimmickBackflowBeam;

        public static FleckDef VV_Fleck_WindOfRestGust;
        public static FleckDef VV_Fleck_HornetDust;

        public static FleckDef VV_Fleck_AssimilationMote;
        public static FleckDef VV_Fleck_AssimilationSoul;
    }

    [DefOf]
    public static class VVDutyDefOf
    {
        public static DutyDef VV_DefendViviBase;

        public static DutyDef VV_TitanicHornetAssault;

        public static DutyDef VV_TitanicHornetEscort;
        public static DutyDef VV_TitanicHornetFlowerGuard;

        public static DutyDef VV_ViviLureScout;
        public static DutyDef VV_ViviBaitThief;
        public static DutyDef VV_ViviNestLife;
        public static DutyDef VV_ViviNestExit;
    }

    [DefOf]
    public static class VVResearchProjectDefOf
    {
        public static ResearchProjectDef VV_ArcanePlantSowing;
        public static ResearchProjectDef VV_ArcaneBotany;
        public static ResearchProjectDef VV_Lifestrand;
    }

    [DefOf]
    public static class VVLifeStageDefOf
    {
        public static LifeStageDef HumanlikePreTeenager_Vivi;
        public static LifeStageDef HumanlikePreTeenager;
        public static LifeStageDef HumanlikeTeenager_Vivi;
        public static LifeStageDef HumanlikeTeenager;
        public static LifeStageDef HumanlikeAdult_Vivi;
    }

    [DefOf]
    public static class VVIncidentDefOf
    {
        public static IncidentDef VV_TitanicHornetSwarm;
        public static IncidentDef VV_TitanicHornetAssault;

        public static IncidentDef VV_ViviLureVisit;
        public static IncidentDef VV_ViviNestSettlers;
    }

    [DefOf]
    public static class VVGameConditionDefOf
    {
        public static GameConditionDef VV_EverflowerWeatherControl;

        public static GameConditionDef VV_OminousSilence;

        public static GameConditionDef VV_GoldenBloom;
    }

    [DefOf]
    public static class VVWeatherDefOf
    {
        public static WeatherDef Clear;
        public static WeatherDef Fog;
        public static WeatherDef Rain;
        public static WeatherDef SnowGentle;

        public static WeatherDef VV_GoldenBloomMist;
    }

    [DefOf]
    public static class VVPreceptDefOf
    {
        public static PreceptDef VV_ChangeWeatherRitual;

        public static PreceptDef VV_QueensCoronation;

        public static PreceptDef VV_GrandAttunement;
    }

    [DefOf]
    public static class VVSoundDefOf
    {
        public static SoundDef VV_EverflowerRitualCast;
        public static SoundDef VV_Fairyfication;
        public static SoundDef VV_FairyMaterialize;
        public static SoundDef VV_FairyDematerialize;
        public static SoundDef VV_FairyTeleport;
        public static SoundDef VV_ApocryphaSummon;
    }

    [DefOf]
    public static class VVTileMutatorDefOf
    {
        [MayRequireOdyssey]
        public static TileMutatorDef VV_ManaVein;
    }

    [DefOf]
    public static class VVQuestScriptDefOf
    {
        public static QuestScriptDef VV_EndGame_ViviMissive;
        public static QuestScriptDef VV_EndGame_ViviBloom;
    }

    [DefOf]
    public static class VVFactionDefOf
    {
        public static FactionDef VV_ViviFederation;
        public static FactionDef VV_PlayerExiledVivi;
        public static FactionDef VV_PlayerViviRefugee;

        public static FactionDef VV_WanderingVivi;
    }
}
