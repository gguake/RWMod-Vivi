using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public static class RuinLayoutUtility
    {
        public const int SealStageCount = 4;
        public const float RingOuterRadius = 19.6f;
        public const float SanctumInnerRadius = 7.5f;

        private static readonly IntVec2[] HexagonPotOffsets =
        {
            new IntVec2(-6, 36),
            new IntVec2(6, 36),
            new IntVec2(12, 26),
            new IntVec2(6, 16),
            new IntVec2(-6, 16),
            new IntVec2(-12, 26),
        };

        private static readonly IntVec2[][] HiddenPathTemplates =
        {
            new[]
            {
                new IntVec2(0, -10), new IntVec2(0, -8), new IntVec2(-4, -8), new IntVec2(-4, -4),
                new IntVec2(3, -4), new IntVec2(3, -1), new IntVec2(-1, -1), new IntVec2(-1, 2),
            },
            new[]
            {
                new IntVec2(0, -10), new IntVec2(0, -8), new IntVec2(4, -8), new IntVec2(4, -5),
                new IntVec2(-3, -5), new IntVec2(-3, -1), new IntVec2(2, -1), new IntVec2(2, 2),
            },
            new[]
            {
                new IntVec2(0, -10), new IntVec2(-3, -10), new IntVec2(-3, -6), new IntVec2(4, -6),
                new IntVec2(4, -2), new IntVec2(-2, -2), new IntVec2(-2, 2),
            },
            new[]
            {
                new IntVec2(0, -10), new IntVec2(3, -10), new IntVec2(3, -6), new IntVec2(-4, -6),
                new IntVec2(-4, -2), new IntVec2(1, -2), new IntVec2(1, 2),
            },
        };

        // 미로(9열 x 5행의 3x3 방 격자) 통로 정의: 열 [-4..4], 행 [0..4]
        private enum PassageKind : byte
        {
            Wall = 0,
            Open = 1,
            CreamBlockade = 2,
        }

        private static readonly (IntVec2 a, IntVec2 b, PassageKind kind)[] MazePassages =
        {
            // 서쪽 주 경로 및 갈림길
            (new IntVec2(0, 0), new IntVec2(-1, 0), PassageKind.Open),
            (new IntVec2(-1, 0), new IntVec2(-1, 1), PassageKind.Open),
            (new IntVec2(-1, 1), new IntVec2(0, 1), PassageKind.Open),
            (new IntVec2(-1, 0), new IntVec2(-2, 0), PassageKind.CreamBlockade),
            (new IntVec2(-2, 0), new IntVec2(-3, 0), PassageKind.Open),
            (new IntVec2(-3, 0), new IntVec2(-3, 1), PassageKind.Open),
            (new IntVec2(-3, 1), new IntVec2(-2, 1), PassageKind.Open),
            (new IntVec2(-2, 1), new IntVec2(-2, 2), PassageKind.Open),
            (new IntVec2(-3, 1), new IntVec2(-4, 1), PassageKind.Open),
            (new IntVec2(-4, 1), new IntVec2(-4, 2), PassageKind.CreamBlockade),
            (new IntVec2(-4, 2), new IntVec2(-3, 2), PassageKind.Open),
            (new IntVec2(-4, 2), new IntVec2(-4, 3), PassageKind.Open),
            (new IntVec2(-4, 3), new IntVec2(-3, 3), PassageKind.Open),
            (new IntVec2(-3, 3), new IntVec2(-3, 4), PassageKind.Open),
            (new IntVec2(-3, 4), new IntVec2(-2, 4), PassageKind.Open),
            (new IntVec2(-3, 3), new IntVec2(-2, 3), PassageKind.CreamBlockade),
            (new IntVec2(-2, 3), new IntVec2(-1, 3), PassageKind.Open),
            (new IntVec2(-1, 3), new IntVec2(-1, 2), PassageKind.Open),
            (new IntVec2(-1, 2), new IntVec2(0, 2), PassageKind.Open),
            (new IntVec2(-1, 3), new IntVec2(-1, 4), PassageKind.Open),
            (new IntVec2(-1, 4), new IntVec2(0, 4), PassageKind.CreamBlockade),

            // 동쪽 우회로 및 막다른 길
            (new IntVec2(0, 0), new IntVec2(1, 0), PassageKind.Open),
            (new IntVec2(1, 0), new IntVec2(2, 0), PassageKind.Open),
            (new IntVec2(1, 0), new IntVec2(1, 1), PassageKind.CreamBlockade),
            (new IntVec2(1, 1), new IntVec2(1, 2), PassageKind.Open),
            (new IntVec2(1, 2), new IntVec2(2, 2), PassageKind.Open),
            (new IntVec2(2, 0), new IntVec2(3, 0), PassageKind.Open),
            (new IntVec2(3, 0), new IntVec2(3, 1), PassageKind.Open),
            (new IntVec2(3, 1), new IntVec2(2, 1), PassageKind.Open),
            (new IntVec2(3, 1), new IntVec2(4, 1), PassageKind.CreamBlockade),
            (new IntVec2(4, 1), new IntVec2(4, 2), PassageKind.Open),
            (new IntVec2(4, 2), new IntVec2(4, 3), PassageKind.Open),
            (new IntVec2(4, 3), new IntVec2(3, 3), PassageKind.Open),
            (new IntVec2(3, 3), new IntVec2(3, 2), PassageKind.Open),
            (new IntVec2(3, 3), new IntVec2(2, 3), PassageKind.CreamBlockade),
            (new IntVec2(2, 3), new IntVec2(1, 3), PassageKind.Open),
            (new IntVec2(1, 3), new IntVec2(0, 3), PassageKind.Open),
            (new IntVec2(2, 3), new IntVec2(2, 4), PassageKind.Open),
            (new IntVec2(2, 4), new IntVec2(3, 4), PassageKind.Open),
            (new IntVec2(2, 4), new IntVec2(1, 4), PassageKind.Open),
            (new IntVec2(1, 4), new IntVec2(0, 4), PassageKind.CreamBlockade),

            // 중앙 지름길 봉쇄
            (new IntVec2(0, 2), new IntVec2(0, 3), PassageKind.CreamBlockade),
        };

        public static CellRect FootprintBounds(IntVec3 origin)
        {
            return new CellRect(origin.x - 24, origin.z - 46, 49, 94);
        }

        public static IntVec3 RingCenter(IntVec3 origin) => new IntVec3(origin.x, 0, origin.z + 26);

        public static IntVec3 CentralAltarCell(IntVec3 origin) => new IntVec3(origin.x, 0, origin.z + 27);

        public static IntVec3 RuinDoomFlowerCell(IntVec3 origin) => new IntVec3(origin.x, 0, origin.z - 12);

        public static IntVec3 RuinDoomReturnCell(IntVec3 origin) => new IntVec3(origin.x, 0, origin.z - 13);

        public static CellRect TrialRect(IntVec3 origin) => new CellRect(origin.x - 5, origin.z - 10, 11, 13);

        public static CellRect Zone3BlastRect(IntVec3 origin) => new CellRect(origin.x - 10, origin.z - 14, 21, 21);

        public static IntVec3 EchoFlowerCell(int index, IntVec3 origin)
        {
            switch (index)
            {
                case 1: return new IntVec3(origin.x, 0, origin.z - 38);
                case 2: return new IntVec3(origin.x, 0, origin.z - 17);
                case 3: return new IntVec3(origin.x, 0, origin.z + 5);
                case 4: return new IntVec3(origin.x, 0, origin.z + 22);
                default: return origin;
            }
        }

        public static IntVec3[] GetHexagonPotCells(IntVec3 origin)
        {
            var result = new IntVec3[HexagonPotOffsets.Length];
            for (int i = 0; i < HexagonPotOffsets.Length; i++)
            {
                result[i] = new IntVec3(origin.x + HexagonPotOffsets[i].x, 0, origin.z + HexagonPotOffsets[i].z);
            }
            return result;
        }

        // 봉인 단계: 0 = 외부(항상 개방), 1 = 미로, 2 = 숨겨진 길 회랑, 3 = 원형 회랑, 4 = 중심 제단 성소
        public static int GetSealStage(IntVec3 cell, IntVec3 origin)
        {
            var dx = cell.x - origin.x;
            var dz = cell.z - origin.z;

            var ringDx = dx;
            var ringDz = dz - 26;
            var ringDistSq = ringDx * ringDx + ringDz * ringDz;

            if (ringDistSq <= SanctumInnerRadius * SanctumInnerRadius)
            {
                return 4;
            }

            if (InZone2Rel(dx, dz))
            {
                return 1;
            }

            if (InZone3Rel(dx, dz))
            {
                return 2;
            }

            if (dz >= 7 && ringDistSq <= RingOuterRadius * RingOuterRadius)
            {
                return 3;
            }

            return 0;
        }

        public static bool InZone2Rel(int dx, int dz)
        {
            if (dz < -35 || dz > -15) { return false; }
            var adx = Mathf.Abs(dx);
            if (adx > 18) { return false; }
            if (adx > 14 && (dz < -31 || dz > -19)) { return false; }
            return true;
        }

        public static bool InZone3Rel(int dx, int dz)
        {
            if (dz < -14 || dz > 7) { return false; }
            var adx = Mathf.Abs(dx);
            if (adx <= 9) { return true; }
            if (adx <= 11 && dz >= -8 && dz <= 0) { return true; }
            return false;
        }

        public static List<IntVec3> GenerateHiddenPath(IntVec3 origin, int seed)
        {
            var template = HiddenPathTemplates[Mathf.Abs(seed) % HiddenPathTemplates.Length];
            var path = new List<IntVec3>(32);

            for (int i = 0; i < template.Length; i++)
            {
                var target = new IntVec3(origin.x + template[i].x, 0, origin.z + template[i].z);
                if (path.Count == 0)
                {
                    path.Add(target);
                    continue;
                }

                var cur = path[path.Count - 1];
                while (cur != target)
                {
                    var stepX = Math.Sign(target.x - cur.x);
                    var stepZ = stepX == 0 ? Math.Sign(target.z - cur.z) : 0;
                    cur = new IntVec3(cur.x + stepX, 0, cur.z + stepZ);
                    path.Add(cur);
                }
            }

            return path;
        }

        private static bool IsInSanctuaryGlade(Map map, IntVec3 cell, IntVec3 origin)
        {
            if (GetSealStage(cell, origin) > 0) { return true; }

            var dx = cell.x - origin.x;
            var dz = cell.z - origin.z;

            // 남쪽 전정 광장 및 남쪽 맵 가장자리 진입 통로 확보
            if (Mathf.Abs(dx) <= 15 && dz >= -47 && dz <= -34) { return true; }
            if (Mathf.Abs(dx) <= 5 && cell.z <= origin.z - 45) { return true; }

            // 결계 외곽을 따라 자연스러운 곡선 공터를 형성한다.
            int jitter = Math.Abs(cell.x * 37 + cell.z * 17) % 2;
            int radius = 4 + jitter;
            for (int oz = -radius; oz <= radius; oz++)
            {
                for (int ox = -radius; ox <= radius; ox++)
                {
                    if (ox * ox + oz * oz > radius * radius) { continue; }
                    var check = new IntVec3(cell.x + ox, 0, cell.z + oz);
                    if (check.InBounds(map) && GetSealStage(check, origin) > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // 봉인 구역 및 전정 주변의 자연 지형·바위·수목·간헐천을 정리해 지형만 자연스럽게 보이게 한다.
        public static void PrepareFootprintGround(Map map, IntVec3 origin)
        {
            var bounds = new CellRect(origin.x - 31, 0, 63, origin.z + 53).ClipInsideMap(map);
            foreach (var cell in bounds)
            {
                if (!IsInSanctuaryGlade(map, cell, origin)) { continue; }

                ClearCellObstacles(map, cell, removePlants: true);
                EnsureSolidNaturalGround(map, cell);
                map.roofGrid.SetRoof(cell, null);
            }
        }

        // 후속 바닐라 GenStep(750~1200: 고대 잔해 차량, 일반 폐허, 바위 덩어리, 수목 등)이 성소 영역에 뿌린 방해물을 정리한다.
        public static void CleanupPostGenDebris(Map map, IntVec3 origin, Func<int, bool> isStageUnsealed)
        {
            var bounds = new CellRect(origin.x - 31, 0, 63, origin.z + 53).ClipInsideMap(map);
            foreach (var cell in bounds)
            {
                if (!IsInSanctuaryGlade(map, cell, origin)) { continue; }

                var stage = GetSealStage(cell, origin);
                bool isSealed = stage > 0 && !isStageUnsealed(stage);
                var dx = cell.x - origin.x;
                var dz = cell.z - origin.z;
                bool inForecourtCore = Mathf.Abs(dx) <= 10 && dz >= -45 && dz <= -35;
                bool onPavedPath = Mathf.Abs(dx) <= 4 && dz >= -45 && dz <= -35;

                if (stage == 0 || isSealed)
                {
                    map.roofGrid.SetRoof(cell, null);
                }

                var things = cell.GetThingList(map);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    var t = things[i];
                    if (t is Pawn) { continue; }
                    if (t.def.defName != null && t.def.defName.StartsWith("VV_")) { continue; }

                    if (t.def.category == ThingCategory.Plant)
                    {
                        // 봉인 지형 위에는 잔디·들꽃만 남겨 왜곡 굴절이 살아 숨 쉬게 하고, 큰 나무나 포장길 위 잡초만 제거한다.
                        bool isTree = t.def.plant != null && t.def.plant.IsTree;
                        if (onPavedPath || (stage > 0 && (!isSealed || isTree)) || (inForecourtCore && isTree))
                        {
                            DestroyForce(t);
                        }
                        continue;
                    }

                    // 바닐라 ScatterRuinsSimple 벽, 고대 차량 잔해, 바위 덩어리, 간헐천 등을 제거한다.
                    if (t.def.category == ThingCategory.Building
                        || t.def.category == ThingCategory.Filth
                        || (t.def.category == ThingCategory.Item && (stage > 0 || inForecourtCore)))
                    {
                        DestroyForce(t);
                    }
                }

                if (isSealed)
                {
                    EnsureSolidNaturalGround(map, cell);
                }
            }
        }

        // 제1구간(외부 전정): 풍화된 유적 파편과 1번 고대 요정꽃, 떠도는 소수의 거대 말벌을 배치한다.
        public static void BuildZone1Forecourt(Map map, IntVec3 origin, int investigated, bool coreExtracted, bool spawnHornets)
        {
            var flowerCell = EchoFlowerCell(1, origin);

            // 부서진 고대 진입로 바닥
            for (int dz = -44; dz <= -36; dz++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    var c = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (!c.InBounds(map)) { continue; }

                    var adx = Mathf.Abs(dx);
                    if (adx <= 1)
                    {
                        SetFloor(map, c, (dz + adx) % 3 == 0 ? VVTerrainDefOf.VV_ViviCreamFloor : VVTerrainDefOf.VV_HoneycombTile);
                    }
                    else if (adx <= 2 && Math.Abs(dz * 7 + dx * 3) % 5 != 0)
                    {
                        SetFloor(map, c, VVTerrainDefOf.VV_ViviCreamFloor);
                    }
                }
            }

            // 1번 요정꽃 주변 작은 제단 바닥
            foreach (var c in CellRect.CenteredOn(flowerCell, 3, 3))
            {
                if (!c.InBounds(map)) { continue; }
                SetFloor(map, c, c == flowerCell ? VVTerrainDefOf.VV_SterileHoneycombTile : VVTerrainDefOf.VV_HoneycombTile);
            }

            // 풍화된 옛 기둥·벽 잔해
            (int dx, int dz, float hpPct)[] pillars =
            {
                (-6, -42, 0.45f), (-6, -41, 0.65f), (6, -42, 0.55f), (6, -41, 0.40f),
                (-8, -38, 0.50f), (-7, -38, 0.35f), (8, -38, 0.45f), (7, -38, 0.60f),
                (-5, -36, 0.70f), (5, -36, 0.65f),
            };

            for (int i = 0; i < pillars.Length; i++)
            {
                var p = pillars[i];
                var cell = new IntVec3(origin.x + p.dx, 0, origin.z + p.dz);
                if (!cell.InBounds(map)) { continue; }

                ClearCellObstacles(map, cell, removePlants: true);
                var wall = GenSpawn.Spawn(ThingMaker.MakeThing(VVThingDefOf.VV_ViviHoneycombWall), cell, map, WipeMode.Vanish);
                wall.HitPoints = Mathf.Max(20, Mathf.RoundToInt(wall.MaxHitPoints * p.hpPct));
            }

            // 주변 비비아 꽃과 잔해 연출
            IntVec2[] flowerSpots =
            {
                new IntVec2(-3, -40), new IntVec2(3, -40), new IntVec2(-4, -37), new IntVec2(4, -37),
                new IntVec2(-9, -40), new IntVec2(9, -40),
            };
            for (int i = 0; i < flowerSpots.Length; i++)
            {
                var cell = new IntVec3(origin.x + flowerSpots[i].x, 0, origin.z + flowerSpots[i].z);
                if (cell.InBounds(map) && cell.GetEdifice(map) == null)
                {
                    ArcanePlantUtility.TrySpawnViviFlower(map, cell, out _);
                }
            }

            if (investigated < 1)
            {
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientRuinFairyFlowerI, flowerCell);
            }

            if (spawnHornets && !coreExtracted)
            {
                SpawnExteriorHornets(map, origin);
            }
        }

        private static void SpawnExteriorHornets(Map map, IntVec3 origin)
        {
            if (VVPawnKindDefOf.VV_TitanicHornet == null) { return; }

            IntVec2[] anchors =
            {
                new IntVec2(-13, -39),
                new IntVec2(13, -39),
                new IntVec2(-23, -20),
                new IntVec2(23, -20),
            };

            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = new IntVec3(origin.x + anchors[i].x, 0, origin.z + anchors[i].z).ClampInsideMap(map);
                if (!CellFinder.TryFindRandomCellNear(anchor, map, 4, c => c.InBounds(map) && c.Standable(map) && GetSealStage(c, origin) == 0, out var spawnCell))
                {
                    continue;
                }

                var hornet = PawnGenerator.GeneratePawn(VVPawnKindDefOf.VV_TitanicHornet, null);
                GenSpawn.Spawn(hornet, spawnCell, map, Rot4.South);
                hornet.mindState?.mentalStateHandler?.TryStartMentalState(VVMentalStateDefOf.VV_HornetBerserk, forceWake: true);

                var lordJob = new LordJob_RuinHornetWander(spawnCell, wanderRadius: 6.5f, guardRadius: 9.5f, retaliationRadius: 26f);
                LordMaker.MakeNewLord(null, lordJob, map, new[] { hornet });
            }
        }

        // 제2구간(미로): 고대 벌집 벽 미로와 중간중간 막고 있는 파괴 가능한 크림벽, 출구의 2번 요정꽃.
        public static void BuildZone2Maze(Map map, IntVec3 origin, int investigated, bool zone3Open)
        {
            var passageMap = new Dictionary<(IntVec2, IntVec2), PassageKind>();
            for (int i = 0; i < MazePassages.Length; i++)
            {
                var p = MazePassages[i];
                passageMap[(p.a, p.b)] = p.kind;
                passageMap[(p.b, p.a)] = p.kind;
            }

            // 전체 Zone 2 영역 초기화 및 기본 고대 벽 채우기
            for (int dz = -35; dz <= -15; dz++)
            {
                for (int dx = -18; dx <= 18; dx++)
                {
                    if (!InZone2Rel(dx, dz)) { continue; }

                    var cell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (!cell.InBounds(map)) { continue; }

                    DisplacePawnsAndClear(map, cell);

                    // 북쪽으로 갈수록 크림 바닥에서 정돈된 벌집 타일로 자연스럽게 전이된다.
                    TerrainDef floor = VVTerrainDefOf.VV_ViviCreamFloor;
                    if (dz >= -21 || (dz >= -26 && Math.Abs(dx * 5 + dz * 3) % 3 == 0))
                    {
                        floor = VVTerrainDefOf.VV_HoneycombTile;
                    }
                    SetFloor(map, cell, floor);

                    // 입구(남쪽 문)와 출구(Zone 3 개방 시 북쪽 문)를 제외한 모든 칸에 일단 벽을 둔 뒤 방과 통로를 뚫는다.
                    bool isSouthGate = dz == -35 && Mathf.Abs(dx) <= 1;
                    bool isNorthGate = dz == -15 && Mathf.Abs(dx) <= 1 && zone3Open;
                    if (!isSouthGate && !isNorthGate)
                    {
                        var wallDef = dz == -15 && Mathf.Abs(dx) <= 9
                            ? VVThingDefOf.VV_AncientSanctumWall
                            : VVThingDefOf.VV_AncientRuinWall;
                        SpawnBuildingClean(map, wallDef, cell);
                    }

                    // 미로 전체에 지붕을 얹어 그늘진 외곽 미궁 분위기를 통일한다.
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                }
            }

            // 3x3 방 공간 확보
            for (int row = 0; row <= 4; row++)
            {
                for (int col = -4; col <= 4; col++)
                {
                    if (!IsValidMazeRoom(col, row)) { continue; }

                    var center = MazeRoomCenter(origin, col, row);
                    foreach (var cell in CellRect.CenteredOn(center, 3, 3))
                    {
                        if (!cell.InBounds(map)) { continue; }
                        DestroyEdifice(map, cell);

                        if (col == 0 && row == 4)
                        {
                            SetFloor(map, cell, cell == center ? VVTerrainDefOf.VV_SterileHoneycombTile : VVTerrainDefOf.VV_HoneycombTile);
                        }
                    }
                }
            }

            // 인접한 3x3 방 사이의 통로(열림 또는 파괴 가능한 크림벽) 생성
            foreach (var entry in MazePassages)
            {
                if (!IsValidMazeRoom(entry.a.x, entry.a.z) || !IsValidMazeRoom(entry.b.x, entry.b.z)) { continue; }

                foreach (var cell in GetMazeDoorCells(origin, entry.a, entry.b))
                {
                    if (!cell.InBounds(map)) { continue; }
                    DestroyEdifice(map, cell);

                    if (entry.kind == PassageKind.CreamBlockade)
                    {
                        SpawnBuildingClean(map, VVThingDefOf.VV_ViviCreamWall, cell);
                    }
                }
            }

            // 미로 곳곳의 남아 있는 희미한 성소 등불과 출구 등불 장식
            (int dx, int dz)[] mazeLamps =
            {
                (-12, -29), (12, -29),
                (-8, -21), (8, -21),
                (-1, -16), (1, -16),
            };
            for (int i = 0; i < mazeLamps.Length; i++)
            {
                var lc = new IntVec3(origin.x + mazeLamps[i].dx, 0, origin.z + mazeLamps[i].dz);
                if (lc.InBounds(map) && lc.GetEdifice(map) == null)
                {
                    SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, lc);
                }
            }

            if (investigated < 2)
            {
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientRuinFairyFlowerII, EchoFlowerCell(2, origin));
            }
        }

        private static bool IsValidMazeRoom(int col, int row)
        {
            if (row < 0 || row > 4 || col < -4 || col > 4) { return false; }
            if ((row == 0 || row == 4) && Mathf.Abs(col) > 3) { return false; }
            return true;
        }

        private static IntVec3 MazeRoomCenter(IntVec3 origin, int col, int row)
        {
            return new IntVec3(origin.x + col * 4, 0, origin.z - 33 + row * 4);
        }

        private static IEnumerable<IntVec3> GetMazeDoorCells(IntVec3 origin, IntVec2 a, IntVec2 b)
        {
            var ca = MazeRoomCenter(origin, a.x, a.z);
            var cb = MazeRoomCenter(origin, b.x, b.z);
            var mid = new IntVec3((ca.x + cb.x) / 2, 0, (ca.z + cb.z) / 2);

            if (a.x != b.x)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    yield return new IntVec3(mid.x, 0, mid.z + dz);
                }
            }
            else
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    yield return new IntVec3(mid.x + dx, 0, mid.z);
                }
            }
        }

        // 제3구간(숨겨진 길 회랑): 미로 너머 온전히 보존된 성소 회랑, 잔향의 멸망꽃, 시련 격자, 3번 요정꽃.
        public static void BuildZone3Hall(Map map, IntVec3 origin, int investigated, bool zone4Open, IReadOnlyCollection<IntVec3> safePath)
        {
            // Zone 2 북쪽 경계 문 개방
            for (int dx = -1; dx <= 1; dx++)
            {
                var gateCell = new IntVec3(origin.x + dx, 0, origin.z - 15);
                if (gateCell.InBounds(map))
                {
                    DestroyEdifice(map, gateCell);
                    SetFloor(map, gateCell, VVTerrainDefOf.VV_SterileHoneycombTile);
                }
            }

            for (int dz = -14; dz <= 7; dz++)
            {
                for (int dx = -11; dx <= 11; dx++)
                {
                    if (!InZone3Rel(dx, dz)) { continue; }

                    var cell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (!cell.InBounds(map)) { continue; }

                    DisplacePawnsAndClear(map, cell);
                    SetFloor(map, cell, VVTerrainDefOf.VV_HoneycombTile);
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);

                    bool isNorthGate = dz == 7 && Mathf.Abs(dx) <= 1 && zone4Open;
                    if (!isNorthGate && IsZone3OuterWall(dx, dz))
                    {
                        SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumWall, cell);
                    }
                }
            }

            // 시련 구역 남·북 경계선 및 측면 열주·등불 (지붕 지지 반경 6.9 보장 + 행 위치 가늠자 역할)
            for (int dx = -5; dx <= 5; dx++)
            {
                SetFloor(map, new IntVec3(origin.x + dx, 0, origin.z - 11), VVTerrainDefOf.VV_SterileHoneycombTile);
                SetFloor(map, new IntVec3(origin.x + dx, 0, origin.z + 3), VVTerrainDefOf.VV_SterileHoneycombTile);
            }

            int[] pillarZ = { -10, -7, -4, -1, 2 };
            for (int i = 0; i < pillarZ.Length; i++)
            {
                var z = origin.z + pillarZ[i];
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumWall, new IntVec3(origin.x - 6, 0, z));
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumWall, new IntVec3(origin.x + 6, 0, z));

                if (i == 1 || i == 3)
                {
                    SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x - 7, 0, z));
                    SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x + 7, 0, z));
                }
            }

            SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x - 5, 0, origin.z - 13));
            SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x + 5, 0, origin.z - 13));
            SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x - 5, 0, origin.z + 5));
            SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x + 5, 0, origin.z + 5));

            var flower3Cell = EchoFlowerCell(3, origin);
            SetFloor(map, flower3Cell, VVTerrainDefOf.VV_SterileHoneycombTile);

            if (investigated < 3)
            {
                var doomCell = RuinDoomFlowerCell(origin);
                SetFloor(map, doomCell, VVTerrainDefOf.VV_SterileHoneycombTile);
                SpawnBuildingClean(map, VVThingDefOf.VV_RuinDoomFlower, doomCell);
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientRuinFairyFlowerIII, flower3Cell);
            }
            else if (safePath != null)
            {
                RevealSafePathFloor(map, safePath);
            }
        }

        public static void RevealSafePathFloor(Map map, IEnumerable<IntVec3> safePath)
        {
            if (map == null || safePath == null) { return; }

            foreach (var cell in safePath)
            {
                if (cell.InBounds(map) && cell.GetEdifice(map) == null)
                {
                    SetFloor(map, cell, VVTerrainDefOf.VV_SterileHoneycombTile);
                }
            }
        }

        private static bool IsZone3OuterWall(int dx, int dz)
        {
            if (dz == 7) { return true; }

            for (int i = 0; i < 4; i++)
            {
                var ndx = dx + GenAdj.CardinalDirections[i].x;
                var ndz = dz + GenAdj.CardinalDirections[i].z;
                if (ndz == -15 && Mathf.Abs(ndx) <= 9) { continue; }
                if (!InZone3Rel(ndx, ndz)) { return true; }
            }

            return false;
        }

        // 제4구간(원형 회랑): 중심 제단 구역을 둘러싼 원형 성소 + 육각형 배치 화분 6개와 공명 요정꽃 6개.
        public static List<Building_RuinPuzzleFairyFlower> BuildZone4Ring(Map map, IntVec3 origin, bool hexagonSolved, int seed)
        {
            // Zone 3 북쪽 경계 문 개방
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = 6; dz <= 8; dz++)
                {
                    var gateCell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (gateCell.InBounds(map))
                    {
                        DestroyEdifice(map, gateCell);
                        SetFloor(map, gateCell, VVTerrainDefOf.VV_SterileHoneycombTile);
                    }
                }
            }

            var ringCenter = RingCenter(origin);

            for (int dz = 7; dz <= 46; dz++)
            {
                for (int dx = -21; dx <= 21; dx++)
                {
                    var cell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (!cell.InBounds(map)) { continue; }
                    if (GetSealStage(cell, origin) != 3) { continue; }

                    var isSouthDoor = dz <= 8 && Mathf.Abs(dx) <= 1;
                    if (!isSouthDoor)
                    {
                        DisplacePawnsAndClear(map, cell);
                    }

                    var dist = Mathf.Sqrt(dx * dx + (dz - 26) * (dz - 26));
                    bool isOuterWall = !isSouthDoor && IsZone4OuterWall(cell, origin);

                    // 바깥 원형 회랑 아케이드(13.2 ~ 외벽)만 지붕을 얹어 지붕 붕괴 없이 하늘이 열린 성소 중정을 이룬다.
                    if (dist >= 13.2f)
                    {
                        map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                    }
                    else
                    {
                        map.roofGrid.SetRoof(cell, null);
                    }

                    bool isInlay = (dist >= 15.6f && dist <= 16.5f)
                        || (dist >= 8.2f && dist <= 9.0f)
                        || (Mathf.Abs(dx) <= 1 && dz >= 7 && dz <= 18);
                    SetFloor(map, cell, isInlay ? VVTerrainDefOf.VV_SterileHoneycombTile : VVTerrainDefOf.VV_HoneycombTile);

                    if (isOuterWall)
                    {
                        SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumWall, cell);
                    }
                }
            }

            // 원형 회랑 둘레 고대 마나 등불 8개
            (int dx, int dz)[] lampOffsets =
            {
                (-5, 9), (5, 9),
                (-16, 18), (16, 18),
                (-16, 34), (16, 34),
                (-6, 42), (6, 42),
            };
            for (int i = 0; i < lampOffsets.Length; i++)
            {
                var lampCell = new IntVec3(origin.x + lampOffsets[i].dx, 0, origin.z + lampOffsets[i].dz);
                if (lampCell.InBounds(map) && lampCell.GetEdifice(map) == null)
                {
                    SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, lampCell);
                }
            }

            // 육각형 꼭짓점 6개 화분 및 공명 요정꽃 배치
            var potCells = GetHexagonPotCells(origin);
            int[] baseScramble = { 0, 3, 1, 4, 2, 5 };
            int rot = Mathf.Abs(seed) % 6;

            var flowers = new List<Building_RuinPuzzleFairyFlower>(6);
            for (int nodeIdx = 0; nodeIdx < 6; nodeIdx++)
            {
                int potIdx = hexagonSolved ? nodeIdx : (baseScramble[nodeIdx] + rot) % 6;
                var potCell = potCells[potIdx];

                foreach (var c in CellRect.CenteredOn(potCell, 3, 3))
                {
                    if (c.InBounds(map) && GetSealStage(c, origin) == 3 && c.GetEdifice(map) == null)
                    {
                        SetFloor(map, c, c == potCell ? VVTerrainDefOf.VV_SterileHoneycombTile : VVTerrainDefOf.VV_HoneycombTile);
                    }
                }

                SpawnBuildingClean(map, VVThingDefOf.VV_RuinPuzzlePot, potCell);

                var flower = (Building_RuinPuzzleFairyFlower)ThingMaker.MakeThing(VVThingDefOf.VV_RuinPuzzleFairyFlower);
                flower.NodeIndex = nodeIdx;
                GenSpawn.Spawn(flower, potCell, map, WipeMode.Vanish);
                flowers.Add(flower);
            }

            return flowers;
        }

        private static bool IsZone4OuterWall(IntVec3 cell, IntVec3 origin)
        {
            for (int i = 0; i < 4; i++)
            {
                var adj = cell + GenAdj.CardinalDirections[i];
                var stage = GetSealStage(adj, origin);
                if (stage == 0 || stage == 1)
                {
                    return true;
                }
            }

            return false;
        }

        // 제5구간(중심 제단 성소): 육각형 퍼즐 완료 시 봉인이 풀리며 중심 제단과 마지막 4번 요정꽃이 드러난다.
        public static void BuildZone5AltarSanctum(Map map, IntVec3 origin, int investigated, bool coreExtracted)
        {
            var ringCenter = RingCenter(origin);
            for (int dz = 18; dz <= 34; dz++)
            {
                for (int dx = -8; dx <= 8; dx++)
                {
                    var cell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (!cell.InBounds(map) || GetSealStage(cell, origin) != 4) { continue; }

                    DisplacePawnsAndClear(map, cell);
                    map.roofGrid.SetRoof(cell, null);

                    var dist = (cell - ringCenter).LengthHorizontal;
                    bool sterile = dist <= 3.8f || (dist >= 6.4f && dist <= 7.5f) || Mathf.Abs(dx) <= 1;
                    SetFloor(map, cell, sterile ? VVTerrainDefOf.VV_SterileHoneycombTile : VVTerrainDefOf.VV_HoneycombTile);
                }
            }

            if (!coreExtracted)
            {
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientRuinAltar, CentralAltarCell(origin));
            }
            else
            {
                // 이미 추출한 뒤라도 맵 안에서 제단은 남아 있는다.
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientRuinAltar, CentralAltarCell(origin));
            }

            if (investigated < 4)
            {
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientRuinFairyFlowerIV, EchoFlowerCell(4, origin));
            }
        }

        public static void RefreshSpatialBarriers(Map map, IntVec3 origin, Func<int, bool> isStageUnsealed)
        {
            if (map == null || VVThingDefOf.VV_RuinSpatialBarrier == null) { return; }

            var existing = map.listerThings.ThingsOfDef(VVThingDefOf.VV_RuinSpatialBarrier).ToList();
            for (int i = 0; i < existing.Count; i++)
            {
                if (!existing[i].Destroyed)
                {
                    existing[i].Destroy(DestroyMode.Vanish);
                }
            }

            bool IsSealed(IntVec3 c)
            {
                if (!c.InBounds(map)) { return false; }
                var st = GetSealStage(c, origin);
                return st > 0 && !isStageUnsealed(st);
            }

            var bounds = FootprintBounds(origin).ClipInsideMap(map);
            foreach (var cell in bounds)
            {
                if (!IsSealed(cell)) { continue; }

                bool onPerimeter = false;
                for (int i = 0; i < 8; i++)
                {
                    var adj = cell + GenAdj.AdjacentCells[i];
                    if (!IsSealed(adj))
                    {
                        onPerimeter = true;
                        break;
                    }
                }

                if (onPerimeter && cell.GetEdifice(map) == null)
                {
                    GenSpawn.Spawn(ThingMaker.MakeThing(VVThingDefOf.VV_RuinSpatialBarrier), cell, map, WipeMode.Vanish);
                }
            }
        }

        public static void UnfogSanctuary(Map map, IntVec3 origin, bool forceMeshDirty = false)
        {
            if (map?.fogGrid == null) { return; }

            var bounds = new CellRect(origin.x - 32, 0, 65, origin.z + 54).ClipInsideMap(map);
            bool canDirty = forceMeshDirty && Current.ProgramState == ProgramState.Playing && map.mapDrawer != null;
            foreach (var cell in bounds)
            {
                if (!IsInSanctuaryGlade(map, cell, origin)) { continue; }

                if (map.fogGrid.IsFogged(cell))
                {
                    map.fogGrid.Unfog(cell);
                }
                else if (canDirty)
                {
                    map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.FogOfWar | MapMeshFlagDefOf.Things);
                }
            }
        }

        private static void SetFloor(Map map, IntVec3 cell, TerrainDef floorDef)
        {
            if (floorDef == null || !cell.InBounds(map)) { return; }

            EnsureSolidNaturalGround(map, cell);
            map.terrainGrid.SetTerrain(cell, floorDef);
        }

        private static void EnsureSolidNaturalGround(Map map, IntVec3 cell)
        {
            var cur = cell.GetTerrain(map);
            if (cur == null || cur.IsWater || !cur.affordances.Contains(TerrainAffordanceDefOf.Medium))
            {
                map.terrainGrid.RemoveTopLayer(cell, doLeavings: false);
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
            }
        }

        private static void DestroyEdifice(Map map, IntVec3 cell)
        {
            var edifice = cell.GetEdifice(map);
            if (edifice != null && !edifice.Destroyed)
            {
                DestroyForce(edifice);
            }
        }

        private static void DisplacePawnsAndClear(Map map, IntVec3 cell)
        {
            var things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                var t = things[i];
                if (t is Pawn pawn)
                {
                    if (CellFinder.TryFindRandomCellNear(cell, map, 6, c => c != cell && c.Standable(map), out var safe))
                    {
                        pawn.Position = safe;
                        pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: true);
                    }
                    continue;
                }

                if (t.def.category == ThingCategory.Item) { continue; }
                DestroyForce(t);
            }
        }

        public static void ClearCellObstacles(Map map, IntVec3 cell, bool removePlants)
        {
            var things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                var t = things[i];
                if (t is Pawn || t.def.category == ThingCategory.Item) { continue; }
                if (!removePlants && t.def.category == ThingCategory.Plant) { continue; }

                DestroyForce(t);
            }
        }

        public static Thing SpawnBuildingClean(Map map, ThingDef def, IntVec3 cell)
        {
            if (def == null || !cell.InBounds(map)) { return null; }

            foreach (var c in GenAdj.OccupiedRect(cell, Rot4.North, def.size))
            {
                if (!c.InBounds(map)) { continue; }
                DisplacePawnsAndClear(map, c);
            }

            return GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map, WipeMode.Vanish);
        }

        private static void DestroyForce(Thing thing)
        {
            if (thing == null || thing.Destroyed) { return; }

            var prev = Thing.allowDestroyNonDestroyable;
            Thing.allowDestroyNonDestroyable = true;
            try
            {
                thing.Destroy(DestroyMode.Vanish);
            }
            finally
            {
                Thing.allowDestroyNonDestroyable = prev;
            }
        }
    }
}
