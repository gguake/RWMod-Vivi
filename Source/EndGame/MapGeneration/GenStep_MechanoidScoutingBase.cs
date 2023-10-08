using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using System;
using Verse;

namespace VVRace
{
    public class GenStep_MechanoidScoutingBase : GenStep_ScattererBestFit
    {
        private ComplexSketch _sketch;

        public override int SeedPart => 84642187;

        protected override IntVec2 Size => new IntVec2(_sketch.layout.container.Width + 10, _sketch.layout.container.Height + 10);

        public override bool CollisionAt(IntVec3 cell, Map map)
        {
            var thingList = cell.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                if (thingList[i].def.IsBuildingArtificial)
                {
                    return true;
                }
            }
            return false;
        }

        public override void Generate(Map map, GenStepParams parms)
        {
            count = 1;
            nearMapCenter = true;

            _sketch = parms.sitePart.parms.ancientComplexSketch;
            base.Generate(map, parms);
        }

        protected override void ScatterAt(IntVec3 c, Map map, GenStepParams genStepParams, int stackCount = 1)
        {
            var sitePartParams = genStepParams.sitePart.parms;

            ResolveParams resolveParams = default;
            resolveParams.ancientComplexSketch = _sketch;
            resolveParams.threatPoints = genStepParams.sitePart.parms.threatPoints;
            if (sitePartParams.interiorThreatPoints > 0f) resolveParams.interiorThreatPoints = sitePartParams.interiorThreatPoints;
            if (sitePartParams.exteriorThreatPoints > 0f) resolveParams.exteriorThreatPoints = sitePartParams.exteriorThreatPoints;

            resolveParams.rect = CellRect.CenteredOn(c, _sketch.layout.container.Width, _sketch.layout.container.Height);
            resolveParams.thingSetMakerDef = genStepParams.sitePart.parms.ancientComplexRewardMaker;

            var component = genStepParams.sitePart.site.GetComponent<FormCaravanComp>();
            if (component != null)
            {
                component.foggedRoomsCheckRect = resolveParams.rect;
            }

            BaseGen.globalSettings.map = map;
            BaseGen.symbolStack.Push("vv_mechanoid_scouting_base", resolveParams);
            BaseGen.Generate();
        }
    }
}
