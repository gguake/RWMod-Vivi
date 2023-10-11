using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaConnector : SymbolResolver
    {
        public override bool CanResolve(ResolveParams resolveParams)
        {
            if (!base.CanResolve(resolveParams))
            {
                return false;
            }

            var rect = resolveParams.rect;
            if (rect.Width < 2 || rect.Height < 4)
            {
                return false;
            }

            return true;
        }

        public override void Resolve(ResolveParams resolveParams)
        {
            var faction = resolveParams.faction ?? Find.FactionManager.RandomEnemyFaction();

            #region 벽 생성 - 위
            {
                var p = resolveParams;
                p.rect = new CellRect(resolveParams.rect.minX, resolveParams.rect.minZ + 3, 2, 1);
                p.singleThingDef = VVThingDefOf.VV_ViviHoneycombWall;
                p.faction = faction;
                BaseGen.symbolStack.Push("fillWithThings", p);
            }
            #endregion

            #region 벽 생성 - 아래
            {
                var p = resolveParams;
                p.rect = new CellRect(resolveParams.rect.minX, resolveParams.rect.minZ, 2, 1);
                p.singleThingDef = VVThingDefOf.VV_ViviHoneycombWall;
                p.faction = faction;
                BaseGen.symbolStack.Push("fillWithThings", p);
            }
            #endregion

            #region 문 생성
            {
                var p = resolveParams;
                p.rect = new CellRect(resolveParams.rect.minX, resolveParams.rect.minZ + 1, 2, 2);
                p.singleThingDef = ThingDefOf.Door;
                p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                p.faction = faction;
                BaseGen.symbolStack.Push("fillWithThings", p);
            }
            #endregion

            #region 제거
            {
                var p = resolveParams;
                BaseGen.symbolStack.Push("clear", p);
            }
            #endregion
        }
    }
}
