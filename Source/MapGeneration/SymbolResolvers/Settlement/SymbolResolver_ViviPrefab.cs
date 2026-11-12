using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    /// <summary>
    /// PrefabDef에 적힌 물건을 BaseGen 심볼로 배치하는 리졸버의 공통 부분.
    /// PrefabUtility.SpawnPrefab은 놓을 수 없는 물건을 말없이 건너뛰므로,
    /// 프리팹에서는 배치 정보(종류·재료·위치·방향·확률)만 읽고 실제 배치는 심볼에 맡긴다.
    /// </summary>
    public abstract class SymbolResolver_ViviPrefab : SymbolResolver
    {
        public List<PrefabDef> prefabs;

        // 보관 건물(선반 등)의 칸마다 채울 물품. 칸마다 하나를 고른다
        public List<ThingSetMakerDef> storageThingSets;

        // 일반 화분에 심을 식물 후보
        public List<ThingDef> growerPlants;

        // 프리팹·화분으로 심는 일반 식물의 성장도
        public float plantGrowth = 0.7f;

        // 데워지는 실내처럼 생성 시점 기온과 상관없이 일반 식물을 심는다
        public bool ignoreGrowthSeason;

        public bool medicalBeds;

        // 무기를 드는 포탑 식물(건인장)에 쥐여 줄 무기 후보
        public List<ThingDef> turretWeapons;

        // 땅이 메마른 칸의 마법 식물 밑에 화분을 깐다(야외용. 방 안은 프리팹에 화분을 직접 적는다)
        public bool potOnInfertileSoil;

        // 이 리졸버를 쓸 타일 연평균 기온 범위. 같은 심볼에 기온대별 리졸버를 두어 바이옴마다 다른 배치를 고른다
        public FloatRange tileTemperature = new FloatRange(-9999f, 9999f);

        // 적으면 이 바이옴들에서만 쓰고 기온은 따지지 않는다
        public List<BiomeDef> biomes;

        // 전용 리졸버가 따로 있어 기온대 리졸버에서 빼는 바이옴
        public List<BiomeDef> excludeBiomes;

        public override bool CanResolve(ResolveParams resolveParams)
        {
            if (!base.CanResolve(resolveParams))
            {
                return false;
            }

            var map = BaseGen.globalSettings.map;
            if (biomes != null)
            {
                return biomes.Contains(map.Biome);
            }

            return (excludeBiomes == null || !excludeBiomes.Contains(map.Biome)) && tileTemperature.Includes(map.TileInfo.temperature);
        }

        protected void PushPrefabThings(PrefabDef prefab, IntVec3 center, Rot4 prefabRot, ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;
            var things = PrefabUtility.GetThings(prefab, center, prefabRot)
                .Where(t => Rand.Chance(t.data.chance))
                .ToList();

            // 심볼 스택은 나중에 넣은 것부터 처리된다: 가구·화분 → 마법 식물 → 보관 물품
            if (!storageThingSets.NullOrEmpty())
            {
                foreach (var (data, cell, rot) in things)
                {
                    if (!typeof(Building_Storage).IsAssignableFrom(data.def.thingClass))
                    {
                        continue;
                    }

                    foreach (var storageCell in GenAdj.OccupiedRect(cell, rot, data.def.size))
                    {
                        var p = resolveParams;
                        p.rect = CellRect.SingleCell(storageCell);
                        p.thingSetMakerDef = storageThingSets.RandomElement();
                        BaseGen.symbolStack.Push("vv_thingSet_storage", p);
                    }
                }
            }

            foreach (var (data, cell, _) in things)
            {
                if (IsArcanePlant(data.def))
                {
                    var p = resolveParams;
                    p.rect = CellRect.SingleCell(cell);
                    p.singleThingDef = data.def;
                    p.postThingSpawn = AfterSpawn;
                    BaseGen.symbolStack.Push("vv_arcane_plant", p);
                }
            }

            foreach (var (data, cell, rot) in things)
            {
                var p = resolveParams;
                p.thingRot = rot;
                p.postThingSpawn = AfterSpawn;
                if (IsArcanePlant(data.def))
                {
                    if (!potOnInfertileSoil || map.fertilityGrid.FertilityAt(cell) > 0f)
                    {
                        continue;
                    }

                    p.rect = CellRect.SingleCell(cell);
                    p.singleThingDef = VVThingDefOf.VV_ArcanePlantPot;
                    p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                }
                else
                {
                    p.rect = GenAdj.OccupiedRect(cell, rot, data.def.size);
                    p.singleThingDef = data.def;
                    p.singleThingStuff = data.stuff;
                }

                BaseGen.symbolStack.Push(data.def.IsBed ? "bed" : "thing", p);
            }
        }

        private static bool IsArcanePlant(ThingDef def)
        {
            return typeof(ArcanePlant).IsAssignableFrom(def.thingClass);
        }

        private void AfterSpawn(Thing thing)
        {
            if (thing is Plant plant)
            {
                // 바닥은 식물보다 먼저 깔리므로 심긴 뒤에 본다: 메마른 땅이거나 자랄 수 없는 기온이면 없앤다
                if (plant.Map.fertilityGrid.FertilityAt(plant.Position) < plant.def.plant.fertilityMin || (!ignoreGrowthSeason && !PlantUtility.GrowthSeasonNow(plant.Position, plant.Map, plant.def)))
                {
                    plant.Destroy();
                }
                else
                {
                    plant.Growth = plantGrowth;
                }
            }
            else if (thing is Building_PlantGrower grower)
            {
                if (!growerPlants.NullOrEmpty() && ThingMaker.MakeThing(growerPlants.RandomElement()) is Plant potPlant)
                {
                    potPlant.Growth = plantGrowth;
                    GenSpawn.Spawn(potPlant, grower.Position, grower.Map);
                }
            }
            else if (thing is Building_Bed bed)
            {
                if (medicalBeds)
                {
                    bed.Medical = true;
                }
            }
            else if (thing is ArcanePlant_Shootus shootus)
            {
                if (!turretWeapons.NullOrEmpty())
                {
                    var weaponDef = turretWeapons.RandomElement();
                    var weapon = ThingMaker.MakeThing(weaponDef, GenStuff.DefaultStuffFor(weaponDef));
                    if (shootus.GetDirectlyHeldThings().TryAdd(weapon))
                    {
                        shootus.EquipGun(weapon);
                    }
                    else
                    {
                        weapon.Destroy();
                    }
                }
            }
        }
    }
}
