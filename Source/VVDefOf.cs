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
        public static StatDef VV_GrassFiberGatherYield;

        public static StatDef VV_GrowthPointsFactor;
        public static StatDef VV_LearningRateFactor;
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
        public static JobDef VV_GatherGrassFiber;

        public static JobDef VV_FertilizeArtificialPlant;

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

        public static ThingDef VV_SeedlingGerminator;

        public static ThingDef VV_Thunderbud;

        public static ThingDef Plant_Strawberry;
        public static ThingDef Plant_Corn;
        public static ThingDef Plant_Cotton;
        public static ThingDef Plant_Healroot;
    }

    [DefOf]
    public static class VVPawnKindDefOf
    {
        public static PawnKindDef VV_PlayerVivi;
        public static PawnKindDef VV_PlayerExiledRoyalVivi;
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
}