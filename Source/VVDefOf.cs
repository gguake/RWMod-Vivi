using RimWorld;
using Verse;

namespace VVRace
{
    [DefOf]
    public static class VVHediffDefOf
    {
        public static HediffDef VV_MindTransmitter;
        public static HediffDef VV_MindLink;

        public static HediffDef VV_PsychicConfusion;
    }

    [DefOf]
    public static class VVPawnRelationDefOf
    {
        public static PawnRelationDef VV_MindLink;
    }

    [DefOf]
    public static class VVStatDefOf
    {
        public static StatDef VV_MindLinkBandwidth;
        public static StatDef VV_MindLinkStrength;
        public static StatDef VV_MindLinkRange;

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
    }

    [DefOf]
    public static class VVPawnTableDefOf
    {
        public static PawnTableDef VV_Vivis;
    }

    [DefOf]
    public static class VVThoughtDefOf
    {
        public static ThoughtDef VV_MindLinkForceBreaked;
        public static ThoughtDef VV_MindLink;
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
    public static class VVGeneDefOf
    {
        public static GeneDef VV_ViviGene;
        public static GeneDef VV_BodyGene;

        public static GeneDef Body_Standard;
    }

    [DefOf]
    public static class VVXenotypeDefOf
    {
        public static XenotypeDef VV_Vivi;
    }

    [DefOf]
    public static class VVJobDefOf
    {
        public static JobDef VV_ConnectMindLink;
        public static JobDef VV_DisconnectMindLink;

        public static JobDef VV_LayViviEgg;
        public static JobDef VV_HaulViviEgg;

        public static JobDef VV_GatherHoney;
        public static JobDef VV_GatherPollen;
        public static JobDef VV_GatherPropolis;
        public static JobDef VV_GatherGrassFiber;
    }

    [DefOf]
    public static class VVThingDefOf
    {
        public static ThingDef VV_ViviEgg;

        public static ThingDef VV_ViviHatchery;

        public static ThingDef VV_RawHoney;

        public static ThingDef VV_FilthPollen;

        public static ThingDef VV_Vivicream;
        public static ThingDef VV_Viviwax;
    }

    [DefOf]
    public static class VVPawnKindDefOf
    {
        public static PawnKindDef VV_PlayerVivi;
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
    public static class VVViviSpecializationDefOf
    {
        public static ViviSpecializationDef VV_NoSpecialization;
    }
}