using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    /// <summary>
    /// 정착지 바깥의 밭(포탑 식물밭·꽃밭)을 PrefabDef 배치대로 만든다.
    /// 프리팹의 앞쪽(북쪽)이 정착지 바깥을 보도록 돌려 놓고, 메마른 땅의 마법 식물 밑에는 화분을 깐다.
    /// </summary>
    public class SymbolResolver_ViviGarden : SymbolResolver_ViviPrefab
    {
        // GenStep_ViviSettlement가 정착지 중심을 적어 두는 맵 생성 변수
        public const string SettlementCenterVarName = "VV_ViviSettlementCenter";

        // 밭의 바깥쪽 절반에 두르는 방어벽. 정착지 쪽은 드나들 수 있게 틔워 둔다
        public bool barricade;

        private static readonly List<Thing> tmpThings = new List<Thing>();

        public SymbolResolver_ViviGarden()
        {
            potOnInfertileSoil = true;
        }

        public override void Resolve(ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;
            var center = resolveParams.rect.CenterCell;
            var prefab = prefabs.RandomElement();

            if (!MapGenerator.TryGetVar<IntVec3>(SettlementCenterVarName, out var settlementCenter))
            {
                settlementCenter = map.Center;
            }

            var outward = (center - settlementCenter).ToVector3();
            var rot = PrefabUtility.ValidateRotation(prefab, outward.sqrMagnitude > 0.01f ? Rot4.FromAngleFlat(outward.AngleFlat()) : Rot4.Random);

            // 꽃을 심는 밭인데 지금 꽃이 자랄 수 없는 기온이면 밭을 만들지 않는다
            var plantDefs = prefab.GetThings().Select(t => t.data.def).Where(d => d.category == ThingCategory.Plant).Distinct().ToList();
            if (plantDefs.Count > 0 && !plantDefs.Any(d => PlantUtility.GrowthSeasonNow(center, map, d)))
            {
                return;
            }

            var rect = GenAdj.OccupiedRect(center, rot, prefab.size).ExpandedBy(1).ClipInsideMap(map);
            ClearGround(rect, map);

            if (barricade)
            {
                SpawnBarricades(rect, outward, map, resolveParams.faction);
            }

            PushPrefabThings(prefab, center, rot, resolveParams);
        }

        // 밭 자리의 야생 식물과 돌덩이를 치운다
        private static void ClearGround(CellRect rect, Map map)
        {
            foreach (var cell in rect)
            {
                tmpThings.Clear();
                tmpThings.AddRange(cell.GetThingList(map));
                foreach (var thing in tmpThings)
                {
                    if (thing is Plant || GenStep_ViviSettlementCleanup.IsDebris(thing))
                    {
                        thing.Destroy();
                    }
                }
            }

            tmpThings.Clear();
        }

        private static void SpawnBarricades(CellRect rect, Vector3 outward, Map map, Faction faction)
        {
            var stuff = Rand.Bool ? VVThingDefOf.VV_Viviwax : ThingDefOf.WoodLog;
            var rectCenter = rect.CenterVector3;
            foreach (var cell in rect.EdgeCells)
            {
                if (Vector3.Dot(cell.ToVector3Shifted() - rectCenter, outward) <= 0f)
                {
                    continue;
                }

                if (!cell.Standable(map) || cell.Roofed(map) || !cell.SupportsStructureType(map, ThingDefOf.Barricade.terrainAffordanceNeeded))
                {
                    continue;
                }

                var barricade = ThingMaker.MakeThing(ThingDefOf.Barricade, stuff);
                barricade.SetFaction(faction);
                GenSpawn.Spawn(barricade, cell, map);
            }
        }
    }
}
