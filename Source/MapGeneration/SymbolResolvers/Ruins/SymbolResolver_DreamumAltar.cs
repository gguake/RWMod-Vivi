using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class SymbolResolver_DreamumAltar : SymbolResolver
    {
        public override void Resolve(ResolveParams rp)
        {
            var rect = CellRect.CenteredOn(rp.rect.CenterCell, VVThingDefOf.VV_DreamumAltar.Size);

            {
                var resolveParams = rp;
                resolveParams.rect = rect;
                resolveParams.singleThingDef = VVThingDefOf.VV_DreamumAltar;
                BaseGen.symbolStack.Push("thing", resolveParams);
            }

            {
                var resolveParams = rp;
                resolveParams.rect = rect.ExpandedBy(1);
                resolveParams.clearFillageOnly = true;
                resolveParams.clearRoof = true;
                BaseGen.symbolStack.Push("clear", resolveParams);
            }
        }
    }
}
