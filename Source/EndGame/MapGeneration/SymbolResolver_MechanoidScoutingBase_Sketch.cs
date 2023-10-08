using RimWorld.BaseGen;

namespace VVRace
{
    public class SymbolResolver_MechanoidScoutingBase_Sketch : SymbolResolver
    {
        public override bool CanResolve(ResolveParams param)
        {
            if (base.CanResolve(param))
            {
                return param.ancientComplexSketch != null;
            }

            return false;
        }

        public override void Resolve(ResolveParams param)
        {
            param.ancientComplexSketch.complexDef.Worker.Spawn(param.ancientComplexSketch, BaseGen.globalSettings.map, param.rect.BottomLeft, param.threatPoints);
        }
    }
}
