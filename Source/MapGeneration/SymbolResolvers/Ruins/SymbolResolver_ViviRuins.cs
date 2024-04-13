using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class SymbolResolver_ViviRuins : SymbolResolver
    {
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
