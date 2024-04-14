using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class SymbolResolver_ViviRuins : SymbolResolver
    {
        static IntVec2[] _hexaRoomCenters = new[]
        {
            new IntVec2(16, 0),
            new IntVec2(16, 10),
            new IntVec2(12, 18),
            new IntVec2(4, 18),
            new IntVec2(-4, 18),
            new IntVec2(-12, 18),
            new IntVec2(-16, 10),
            new IntVec2(-16, 0),
            new IntVec2(-16, -10),
            new IntVec2(-12, -18),
            new IntVec2(-4, -18),
            new IntVec2(4, -18),
            new IntVec2(12, -18),
            new IntVec2(16, -10),
        };

        public override void Resolve(ResolveParams rp)
        {
            var map = BaseGen.globalSettings.map;

            Rand.PushState();
            try
            {
                Rand.Seed = map.ConstantRandSeed;

                #region 제단 추가
                {
                    var p = rp;
                    BaseGen.symbolStack.Push("vv_dreamum_altar", p);
                }
                #endregion

                #region 폐허 추가 - 외벽
                {
                    foreach (var c in _hexaRoomCenters)
                    {
                        var p = rp;
                        p.rect = CellRect.CenteredOn(rp.rect.CenterCell + c.ToIntVec3, HexagonalRoomUtility.RoomCellSize);
                        p.noRoof = true;
                        p.faction = null;
                        p.chanceToSkipWallBlock = 0.6f;
                        p.hpPercentRange = new FloatRange(0.05f, 0.6f);
                        BaseGen.symbolStack.Push("vv_hexa_edge_honeycomb_wall", p);
                    }
                }
                #endregion

                #region 폐허 추가 - 내벽
                {
                    var p = rp;
                    p.rect = CellRect.CenteredOn(rp.rect.CenterCell, 19, 21);
                    p.noRoof = true;
                    p.faction = null;
                    p.chanceToSkipWallBlock = 0.3f;
                    p.hpPercentRange = new FloatRange(0.1f, 0.9f);
                    BaseGen.symbolStack.Push("vv_edge_honeycomb_wall", p);
                }
                #endregion

                #region 맵 경계 정리
                {
                    var p = rp;
                    p.rect = rp.rect.ContractedBy(4);
                    BaseGen.symbolStack.Push("ensureCanReachMapEdge", p);
                }
                #endregion
            }
            finally
            {
                Rand.PopState();
            }
        }
    }
}
