using RimWorld;
using Verse;

namespace VVRace
{
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
    }

    [DefOf]
    public static class VVWorkTypeDefOf
    {
        public static WorkTypeDef Patient;
        public static WorkTypeDef PatientBedRest;
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
        public static JobDef VV_HaulViviEgg;

        public static JobDef VV_GatherHoney;
        public static JobDef VV_GatherPollen;
        public static JobDef VV_GatherPropolis;

        public static JobDef VV_FertilizeArcanePlant;
        public static JobDef VV_EquipWeaponToShootus;

        public static JobDef VV_HaulGerminatingIngredient;
        public static JobDef VV_PackingSeedling;
        public static JobDef VV_ClearGerminator;
    }

    [DefOf]
    public static class VVThingDefOf
    {
        public static ThingDef VV_Vivi;
        public static ThingDef VV_ViviEgg;

        public static ThingDef VV_ViviHatchery;

        public static ThingDef VV_RawHoney;

        public static ThingDef VV_FilthPollen;

        public static ThingDef VV_Vivicream;
        public static ThingDef VV_Viviwax;

        public static ThingDef VV_UnknownSeed;
        public static ThingDef VV_Fertilizer;

        public static ThingDef VV_GatheringBarrel;
        public static ThingDef VV_RefiningWorkbench;
        public static ThingDef VV_ArcanePlantPot;
        public static ThingDef VV_SeedlingGerminator;

        public static ThingDef VV_EmberBloom;
        public static ThingDef VV_FrostBloom;
        public static ThingDef VV_Shockerbud;
        public static ThingDef VV_Thunderpetals;
        public static ThingDef VV_Waterdrops;
        public static ThingDef VV_Radiantflower;
        public static ThingDef VV_Richflower;
        public static ThingDef VV_Peashooter;
        public static ThingDef VV_Pealauncher;
        public static ThingDef VV_Shootus;

        public static ThingDef VV_ViviHoneycombWall;

        public static ThingDef Plant_Strawberry;
        public static ThingDef Plant_Corn;
        public static ThingDef Plant_Cotton;
        public static ThingDef Plant_Healroot;
        public static ThingDef Plant_Daylily;

        public static ThingDef Shelf;
        public static ThingDef ShelfSmall;
        public static ThingDef Crib;
        public static ThingDef Table3x3c;
        public static ThingDef PlantPot;

        public static ThingDef HandTailoringBench;
        public static ThingDef FueledSmithy;

        public static ThingDef VV_TitanicHornet;
    }

    [DefOf]
    public static class VVPawnKindDefOf
    {
        public static PawnKindDef VV_PlayerVivi;
        public static PawnKindDef VV_PlayerExiledRoyalVivi;

        public static PawnKindDef VV_TitanicHornet;
    }

    [DefOf]
    public static class VVRecipeDefOf
    {
        public static RecipeDef VV_GatherHoney;
        public static RecipeDef VV_GatherPollen;
        public static RecipeDef VV_GatherPropolis;

        public static RecipeDef VV_MakeVivicream;
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
    public static class VVGerminateScheduleDefOf
    {
        public static GerminateScheduleDef VV_DoNothing;
    }

    [DefOf]
    public static class VVThoughtDefOf
    {
        public static ThoughtDef VV_AtePollen;
    }

    [DefOf]
    public static class VVHediffDefOf
    {
        public static HediffDef VV_RoyalVivi;

        public static HediffDef VV_HoneyNeed;

        public static HediffDef Flu;
        public static HediffDef Malaria;
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
    public static class VVResearchProjectDefOf
    {
        public static ResearchProjectDef VV_AncientPlantCuttings;
    }

    [DefOf]
    public static class VVMapMeshFlagDefOf
    {
        public static MapMeshFlagDef VV_ManaFluxGrid;
    }
}