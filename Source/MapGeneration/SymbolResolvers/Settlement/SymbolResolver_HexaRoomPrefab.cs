using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    /// <summary>
    /// 육각 방을 만들고 PrefabDef에 적힌 가구를 배치한다.
    /// </summary>
    public class SymbolResolver_HexaRoomPrefab : SymbolResolver_ViviPrefab
    {
        // 지붕 없는 안뜰
        public bool noRoof;

        // 정착지가 정한 바닥 대신 쓸 바닥
        public TerrainDef floorDef;

        public override void Resolve(ResolveParams resolveParams)
        {
            PushPrefabThings(prefabs.RandomElement(), resolveParams.rect.CenterCell, Rot4.North, resolveParams);

            // 나중에 넣은 방이 먼저 만들어진다
            var p = resolveParams;
            if (noRoof)
            {
                p.noRoof = true;
            }
            if (floorDef != null)
            {
                // 다리 위처럼 지형이 받쳐 주지 않는 칸에도 깐다(흙이 안 깔리면 안뜰 꽃이 자라지 못한다)
                p.floorDef = floorDef;
                p.floorOnlyIfTerrainSupports = false;
            }
            BaseGen.symbolStack.Push("vv_hexa_empty_room", p);
        }
    }
}
