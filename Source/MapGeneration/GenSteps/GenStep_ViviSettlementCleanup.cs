using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    /// <summary>
    /// 비비 정착지보다 늦게 도는 생성 단계(돌덩이·야생 식물)가 방 안에 남긴 것을 치운다.
    /// </summary>
    public class GenStep_ViviSettlementCleanup : GenStep
    {
        // SymbolResolver_ViviBasePart가 방 칸을 적어 두는 맵 생성 변수
        public const string RoomCellsVarName = "VV_ViviSettlementRoomCells";

        private static readonly List<Thing> tmpThings = new List<Thing>();

        public override int SeedPart => 2026111202;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (!MapGenerator.TryGetVar<HashSet<IntVec3>>(RoomCellsVarName, out var roomCells))
            {
                return;
            }

            foreach (var cell in roomCells)
            {
                tmpThings.Clear();
                tmpThings.AddRange(cell.GetThingList(map));
                foreach (var thing in tmpThings)
                {
                    if (IsDebris(thing))
                    {
                        thing.Destroy();
                    }
                }
            }

            tmpThings.Clear();
        }

        public static bool IsDebris(Thing thing)
        {
            if (thing.def == ThingDefOf.Filth_RubbleRock)
            {
                return true;
            }

            if (thing.def.category == ThingCategory.Item)
            {
                return thing.def.IsWithinCategory(ThingCategoryDefOf.Chunks);
            }

            return thing is Plant plant && plant.def.plant.IsTree;
        }
    }
}
