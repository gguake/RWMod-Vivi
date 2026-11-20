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
        public const int RecommendedMapSize = 220;
        public const float HexOuterApothem = 58.0f;
        public const float HexShellInnerApothem = 56.2f;
        public const float RingOuterRadius = 22.5f;
        public const float SanctumInnerRadius = 8.5f;
        private const int MazePitch = 6;
        private const float Sqrt3Over2 = 0.8660254f;

        // 중앙 원형 회랑(Stage 3, origin 중심) 정육각형 화분 6개 위치 (반지름 약 14)
        private static readonly IntVec2[] HexagonPotOffsets =
        {
            new IntVec2(-7, 12),
            new IntVec2(7, 12),
            new IntVec2(14, 0),
            new IntVec2(7, -12),
            new IntVec2(-7, -12),
            new IntVec2(-14, 0),
        };

        // 제2구간(북쪽 장대 복도, dz: +46 -> +26) 숨겨진 안전 경로 템플릿 4종
        private static readonly IntVec2[][] HiddenPathTemplates =
        {
            new[]
            {
                new IntVec2(0, 46), new IntVec2(0, 43), new IntVec2(-4, 43), new IntVec2(-4, 38),
                new IntVec2(4, 38), new IntVec2(4, 34), new IntVec2(-3, 34), new IntVec2(-3, 29),
                new IntVec2(2, 29), new IntVec2(2, 26), new IntVec2(0, 26),
            },
            new[]
            {
                new IntVec2(0, 46), new IntVec2(0, 43), new IntVec2(4, 43), new IntVec2(4, 39),
                new IntVec2(-4, 39), new IntVec2(-4, 34), new IntVec2(3, 34), new IntVec2(3, 30),
                new IntVec2(-2, 30), new IntVec2(-2, 26), new IntVec2(0, 26),
            },
            new[]
            {
                new IntVec2(0, 46), new IntVec2(-3, 46), new IntVec2(-3, 41), new IntVec2(5, 41),
                new IntVec2(5, 36), new IntVec2(-4, 36), new IntVec2(-4, 31), new IntVec2(3, 31),
                new IntVec2(3, 26), new IntVec2(0, 26),
            },
            new[]
            {
                new IntVec2(0, 46), new IntVec2(3, 46), new IntVec2(3, 41), new IntVec2(-5, 41),
                new IntVec2(-5, 36), new IntVec2(4, 36), new IntVec2(4, 31), new IntVec2(-3, 31),
                new IntVec2(-3, 26), new IntVec2(0, 26),
            },
        };

        public static float HexDist(float dx, float dz)
        {
            var adx = Mathf.Abs(dx);
            var adz = Mathf.Abs(dz);
            return Mathf.Max(adz, adz * 0.5f + adx * Sqrt3Over2);
        }

        public static CellRect FootprintBounds(IntVec3 origin)
        {
            // 전체 거대 육각형(dx: -68..+68, dz: -59..+59) + 남쪽 전정(dz: -73..-59)
            return new CellRect(origin.x - 68, origin.z - 73, 137, 133);
        }

        public static CellRect GladeBounds(IntVec3 origin)
        {
            // 육각형 외곽 공터(dx: -76..+76, dz: -67..+67) + 남쪽 전정 광장(dz: -78..-57)
            return new CellRect(origin.x - 76, origin.z - 78, 153, 146);
        }

        public static IntVec3 ForecourtAnchorCell(IntVec3 origin) => new IntVec3(origin.x, 0, origin.z - 66);

        public static IntVec3 RingCenter(IntVec3 origin) => origin;

        public static IntVec3 CentralAltarCell(IntVec3 origin) => origin;

        public static IntVec3 RuinDoomFlowerCell(IntVec3 origin) => new IntVec3(origin.x, 0, origin.z + 48);

        public static IntVec3 RuinDoomReturnCell(IntVec3 origin) => new IntVec3(origin.x, 0, origin.z + 49);

        // 복도 전체 폭(dx: -9..+9, dz: +26..+46)을 시련 구역으로 지정해 측면 열주 우회를 원천 차단한다.
        public static CellRect TrialRect(IntVec3 origin) => new CellRect(origin.x - 9, origin.z + 26, 19, 21);

        public static CellRect Zone3BlastRect(IntVec3 origin) => new CellRect(origin.x - 10, origin.z + 21, 21, 31);

        public static IntVec3 EchoFlowerCell(int index, IntVec3 origin)
        {
            switch (index)
            {
                case 1: return new IntVec3(origin.x, 0, origin.z - 62);
                case 2: return new IntVec3(origin.x, 0, origin.z + 54);
                case 3: return new IntVec3(origin.x, 0, origin.z + 24);
                case 4: return new IntVec3(origin.x, 0, origin.z + 5);
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

        // 봉인 단계:
        // 0 = 외부(항상 개방)
        // 1 = 중앙과 복도를 둘러싼 거대 육각형 미로
        // 2 = 북쪽에서 중앙으로 향하는 길쭉하고 넓은 시련 복도
        // 3 = 정중앙 원형 회랑 & 육각형 화분 퍼즐
        // 4 = 정중앙 제단 성소
        public static int GetSealStage(IntVec3 cell, IntVec3 origin)
        {
            var dx = cell.x - origin.x;
            var dz = cell.z - origin.z;
            var distSq = dx * dx + dz * dz;

            if (distSq <= SanctumInnerRadius * SanctumInnerRadius)
            {
                return 4;
            }

            if (distSq <= RingOuterRadius * RingOuterRadius)
            {
                return 3;
            }

            if (InCorridorRel(dx, dz))
            {
                return 2;
            }

            if (HexDist(dx, dz) <= HexOuterApothem)
            {
                return 1;
            }

            return 0;
        }

        public static bool InCorridorRel(int dx, int dz)
        {
            return Mathf.Abs(dx) <= 10 && dz >= 20 && dz <= 51;
        }

        public static List<IntVec3> GenerateHiddenPath(IntVec3 origin, int seed)
        {
            var template = HiddenPathTemplates[Mathf.Abs(seed) % HiddenPathTemplates.Length];
            var path = new List<IntVec3>(64);

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
            var dx = cell.x - origin.x;
            var dz = cell.z - origin.z;
            var hex = HexDist(dx, dz);

            // 거대 육각형 외곽을 따라 자연스러운 곡선 공터(여유 6~7칸) 형성
            int jitter = Math.Abs(cell.x * 37 + cell.z * 17) % 2;
            if (hex <= HexOuterApothem + 5.5f + jitter) { return true; }

            // 남쪽 전정 광장 (가장자리 진입 연결은 후처리에서 지형에 맞춰 자동 보장한다)
            if (Mathf.Abs(dx) <= 18 && dz >= -75 && dz <= -57) { return true; }

            return false;
        }

        // 봉인 구역 및 전정 주변의 자연 지형·바위·수목·간헐천을 정리해 지형만 자연스럽게 보이게 한다.
        public static void PrepareFootprintGround(Map map, IntVec3 origin)
        {
            var bounds = GladeBounds(origin).ClipInsideMap(map);
            foreach (var cell in bounds)
            {
                if (!IsInSanctuaryGlade(map, cell, origin)) { continue; }

                ClearCellObstacles(map, cell, removePlants: true);
                EnsureSolidNaturalGround(map, cell, stripArtificialFloor: true);
                map.roofGrid.SetRoof(cell, null);
            }
        }

        // 후속 바닐라·DLC GenStep(500~1700: 변이 구조물, 고대 잔해, 일반 폐허, 바위 덩어리, 수목 등)이 성소 영역에 뿌린 방해물을 정리한다.
        public static void CleanupPostGenDebris(Map map, IntVec3 origin, Func<int, bool> isStageUnsealed)
        {
            var bounds = GladeBounds(origin).ClipInsideMap(map);
            foreach (var cell in bounds)
            {
                if (!IsInSanctuaryGlade(map, cell, origin)) { continue; }

                var stage = GetSealStage(cell, origin);
                bool isSealed = stage > 0 && !isStageUnsealed(stage);
                var dx = cell.x - origin.x;
                var dz = cell.z - origin.z;
                bool inForecourtCore = Mathf.Abs(dx) <= 14 && dz >= -73 && dz <= -58;
                bool onPavedPath = Mathf.Abs(dx) <= 5 && dz >= -73 && dz <= -58;

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
                        bool isTree = t.def.plant != null && t.def.plant.IsTree;
                        if (onPavedPath || (stage > 0 && (!isSealed || isTree)) || (inForecourtCore && isTree))
                        {
                            DestroyForce(t);
                        }
                        continue;
                    }

                    if (t.def.category == ThingCategory.Building
                        || t.def.category == ThingCategory.Filth
                        || (t.def.category == ThingCategory.Item && (stage > 0 || inForecourtCore)))
                    {
                        DestroyForce(t);
                    }
                }

                if (stage == 0 || isSealed)
                {
                    EnsureSolidNaturalGround(map, cell, stripArtificialFloor: isSealed);
                }
            }
        }

        // 산악·깊은 강·호수·용암·해안 등으로 남쪽 전정과 원정대 진입 가장자리(최대 가장자리 구역 포함)가 단절되지 않도록 자연 통로를 보장한다.
        public static void EnsureExteriorApproachReachability(Map map, IntVec3 origin)
        {
            if (map == null || !origin.IsValid) { return; }

            var anchor = ForecourtAnchorCell(origin).ClampInsideMap(map);
            var flower1 = EchoFlowerCell(1, origin).ClampInsideMap(map);

            // 전정 핵심 진입 지점과 1번 요정꽃 주변이 절대 물·용암·통행불가 지형이 아니도록 재검증한다.
            foreach (var core in new[] { anchor, flower1 })
            {
                foreach (var c in CellRect.CenteredOn(core, 3, 3))
                {
                    if (!c.InBounds(map) || GetSealStage(c, origin) != 0) { continue; }
                    ClearNonViviObstacles(map, c, removePlants: true);
                    map.roofGrid.SetRoof(c, null);
                    EnsureSolidNaturalGround(map, c);
                }
            }

            int w = map.Size.x;
            int h = map.Size.z;
            int total = w * h;
            int Idx(int x, int z) => z * w + x;

            var passable = new bool[total];
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    passable[Idx(x, z)] = IsExteriorPassableForCaravan(map, new IntVec3(x, 0, z), origin);
                }
            }

            var fromForecourt = new bool[total];
            var queue = new Queue<int>(4096);
            int anchorIdx = Idx(anchor.x, anchor.z);
            if (!passable[anchorIdx])
            {
                CarveApproachCell(map, anchor, origin, passable, w);
            }

            fromForecourt[anchorIdx] = true;
            queue.Enqueue(anchorIdx);
            FloodFillExterior(queue, passable, fromForecourt, w, h);

            // 맵 4면 가장자리에 접한 모든 통행 가능 연결 요소를 수집한다.
            var edgeCompId = new int[total];
            var edgeComponents = new List<(int id, int size, bool connected)>();
            int nextCompId = 1;

            void TryScanBorderCell(int x, int z)
            {
                int start = Idx(x, z);
                if (!passable[start] || edgeCompId[start] != 0) { return; }

                int cid = nextCompId++;
                int size = 0;
                bool connected = false;
                edgeCompId[start] = cid;
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    size++;
                    if (fromForecourt[cur]) { connected = true; }

                    int cx = cur % w;
                    int cz = cur / w;
                    if (cx > 0) { TryEnqueueComp(cur - 1, cid); }
                    if (cx + 1 < w) { TryEnqueueComp(cur + 1, cid); }
                    if (cz > 0) { TryEnqueueComp(cur - w, cid); }
                    if (cz + 1 < h) { TryEnqueueComp(cur + w, cid); }
                }

                edgeComponents.Add((cid, size, connected));
            }

            void TryEnqueueComp(int nxt, int cid)
            {
                if (passable[nxt] && edgeCompId[nxt] == 0)
                {
                    edgeCompId[nxt] = cid;
                    queue.Enqueue(nxt);
                }
            }

            for (int x = 0; x < w; x++)
            {
                TryScanBorderCell(x, 0);
                TryScanBorderCell(x, h - 1);
            }
            for (int z = 1; z < h - 1; z++)
            {
                TryScanBorderCell(0, z);
                TryScanBorderCell(w - 1, z);
            }

            bool carvedAny = false;
            if (edgeComponents.Count > 0)
            {
                // CaravanEnterMapUtility.FindNearEdgeCell이 사용하는 최대 가장자리 구역을 최우선으로,
                // 단절된 주요 가장자리 구역(최대 3곳)을 최소 비용의 자연 여울·산길로 전정과 연결한다.
                edgeComponents.Sort((a, b) => b.size.CompareTo(a.size));

                int bridgesCarved = 0;
                for (int i = 0; i < edgeComponents.Count; i++)
                {
                    var comp = edgeComponents[i];
                    if (comp.connected) { continue; }
                    if (i > 0 && (comp.size < 120 || bridgesCarved >= 3)) { continue; }

                    if (CarveMinimumCostApproach(map, origin, passable, fromForecourt, w, h,
                        idx => edgeCompId[idx] == comp.id, allowOcean: i == 0, out var reachedIdx))
                    {
                        carvedAny = true;
                        bridgesCarved++;
                        if (!fromForecourt[reachedIdx])
                        {
                            fromForecourt[reachedIdx] = true;
                            queue.Enqueue(reachedIdx);
                        }
                        FloodFillExterior(queue, passable, fromForecourt, w, h);
                    }
                }
            }
            else
            {
                // 극단적 사면 봉쇄(사방 산맥/절벽/깊은 물) 시 가장 비용이 낮은 맵 가장자리로 진입 통로를 개척한다.
                if (CarveMinimumCostApproach(map, origin, passable, fromForecourt, w, h,
                    idx =>
                    {
                        int bx = idx % w;
                        int bz = idx / w;
                        return bx == 0 || bx == w - 1 || bz == 0 || bz == h - 1;
                    }, allowOcean: true, out var reachedIdx))
                {
                    carvedAny = true;
                    if (!fromForecourt[reachedIdx])
                    {
                        fromForecourt[reachedIdx] = true;
                        queue.Enqueue(reachedIdx);
                    }
                    FloodFillExterior(queue, passable, fromForecourt, w, h);
                }
            }

            // PlayerStartSpot이 단절 영역이나 무효 위치에 잡혔다면 전정으로 맞춘다.
            if (!MapGenerator.PlayerStartSpotValid
                || !MapGenerator.PlayerStartSpot.InBounds(map)
                || !fromForecourt[Idx(MapGenerator.PlayerStartSpot.x, MapGenerator.PlayerStartSpot.z)])
            {
                MapGenerator.PlayerStartSpot = anchor;
            }

            if (carvedAny)
            {
                map.regionAndRoomUpdater?.RebuildAllRegionsAndRooms();
            }
        }

        private static void FloodFillExterior(Queue<int> queue, bool[] passable, bool[] visited, int w, int h)
        {
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int cx = cur % w;
                int cz = cur / w;

                if (cx > 0 && passable[cur - 1] && !visited[cur - 1])
                {
                    visited[cur - 1] = true;
                    queue.Enqueue(cur - 1);
                }
                if (cx + 1 < w && passable[cur + 1] && !visited[cur + 1])
                {
                    visited[cur + 1] = true;
                    queue.Enqueue(cur + 1);
                }
                if (cz > 0 && passable[cur - w] && !visited[cur - w])
                {
                    visited[cur - w] = true;
                    queue.Enqueue(cur - w);
                }
                if (cz + 1 < h && passable[cur + w] && !visited[cur + w])
                {
                    visited[cur + w] = true;
                    queue.Enqueue(cur + w);
                }
            }
        }

        private static bool CarveMinimumCostApproach(
            Map map,
            IntVec3 origin,
            bool[] passable,
            bool[] fromForecourt,
            int w,
            int h,
            Func<int, bool> isGoal,
            bool allowOcean,
            out int goalIdx)
        {
            int total = w * h;
            var dist = new int[total];
            var parent = new int[total];
            for (int i = 0; i < total; i++)
            {
                dist[i] = int.MaxValue;
                parent[i] = -1;
            }

            var heap = new List<(int cost, int idx)>(4096);
            void Push(int cost, int idx)
            {
                heap.Add((cost, idx));
                int ci = heap.Count - 1;
                while (ci > 0)
                {
                    int pi = (ci - 1) >> 1;
                    if (heap[pi].cost <= heap[ci].cost) { break; }
                    var tmp = heap[pi];
                    heap[pi] = heap[ci];
                    heap[ci] = tmp;
                    ci = pi;
                }
            }

            (int cost, int idx) Pop()
            {
                var top = heap[0];
                int last = heap.Count - 1;
                heap[0] = heap[last];
                heap.RemoveAt(last);
                int pi = 0;
                while (true)
                {
                    int li = (pi << 1) + 1;
                    if (li >= heap.Count) { break; }
                    int ri = li + 1;
                    int best = (ri < heap.Count && heap[ri].cost < heap[li].cost) ? ri : li;
                    if (heap[pi].cost <= heap[best].cost) { break; }
                    var tmp = heap[pi];
                    heap[pi] = heap[best];
                    heap[best] = tmp;
                    pi = best;
                }
                return top;
            }

            // 현재 전정에서 도달 가능한 모든 셀(경계 셀 우선)을 거리 0 출발점으로 등록한다.
            for (int i = 0; i < total; i++)
            {
                if (!fromForecourt[i]) { continue; }
                dist[i] = 0;
                int x = i % w;
                int z = i / w;
                bool onFrontier = (x > 0 && !fromForecourt[i - 1])
                    || (x + 1 < w && !fromForecourt[i + 1])
                    || (z > 0 && !fromForecourt[i - w])
                    || (z + 1 < h && !fromForecourt[i + w]);
                if (onFrontier)
                {
                    Push(0, i);
                }
            }

            goalIdx = -1;
            int[] dxs = { -1, 1, 0, 0 };
            int[] dzs = { 0, 0, -1, 1 };

            while (heap.Count > 0)
            {
                var (curCost, cur) = Pop();
                if (curCost > dist[cur]) { continue; }

                if (isGoal(cur) && !fromForecourt[cur])
                {
                    goalIdx = cur;
                    break;
                }

                int cx = cur % w;
                int cz = cur / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dxs[d];
                    int nz = cz + dzs[d];
                    if (nx < 0 || nx >= w || nz < 0 || nz >= h) { continue; }

                    int nxt = nz * w + nx;
                    if (fromForecourt[nxt]) { continue; }

                    int stepCost = GetApproachCarveStepCost(map, new IntVec3(nx, 0, nz), origin, passable[nxt], allowOcean);
                    if (stepCost < 0) { continue; }

                    int nextCost = curCost + stepCost;
                    if (nextCost < dist[nxt])
                    {
                        dist[nxt] = nextCost;
                        parent[nxt] = cur;
                        Push(nextCost, nxt);
                    }
                }
            }

            if (goalIdx < 0) { return false; }

            // 찾아낸 최소 비용 경로를 따라 3~5칸 폭의 자연 통로(산길 / 얕은 여울)를 개척한다.
            int trace = goalIdx;
            while (trace >= 0 && !fromForecourt[trace])
            {
                var center = new IntVec3(trace % w, 0, trace / w);
                bool needsCarve = !passable[trace];
                if (needsCarve)
                {
                    foreach (var radial in GenRadial.RadialCellsAround(center, 2.15f, useCenter: true))
                    {
                        if (!radial.InBounds(map) || GetSealStage(radial, origin) != 0) { continue; }
                        if (HasViviThing(map, radial)) { continue; }

                        CarveApproachCell(map, radial, origin, passable, w);
                    }
                }

                trace = parent[trace];
            }

            // 가장자리 목표 지점 자체가 통행 불가였다면 확실히 개방한다.
            var goalCell = new IntVec3(goalIdx % w, 0, goalIdx / w);
            if (!passable[goalIdx])
            {
                CarveApproachCell(map, goalCell, origin, passable, w);
            }

            return true;
        }

        private static int GetApproachCarveStepCost(Map map, IntVec3 cell, IntVec3 origin, bool alreadyPassable, bool allowOcean)
        {
            if (GetSealStage(cell, origin) != 0) { return -1; }
            if (HasViviThing(map, cell)) { return -1; }
            if (alreadyPassable) { return 1; }

            var terr = cell.GetTerrain(map);
            if (terr == null) { return 40; }
            if (terr.IsOcean) { return allowOcean ? 300 : -1; }
            if (terr.IsWater) { return 15; }
            if (terr.dangerous || terr.exposesToVacuum || terr.burnDamage > 0) { return 35; }
            return 25;
        }

        private static void CarveApproachCell(Map map, IntVec3 cell, IntVec3 origin, bool[] passable, int w)
        {
            if (!cell.InBounds(map) || GetSealStage(cell, origin) != 0) { return; }

            ClearNonViviObstacles(map, cell, removePlants: true);
            map.roofGrid.SetRoof(cell, null);

            var terr = cell.GetTerrain(map);
            if (terr == null || terr.dangerous || terr.exposesToVacuum || terr.burnDamage > 0)
            {
                map.terrainGrid.RemoveTopLayer(cell, doLeavings: false);
                map.terrainGrid.SetTerrain(cell, GetBiomeNaturalGround(map));
            }
            else if (terr.IsWater && terr.passability == Traversability.Impassable)
            {
                TerrainDef ford;
                if (terr == TerrainDefOf.ToxicWaterDeep)
                {
                    ford = GetBiomeNaturalGround(map);
                }
                else if (terr.IsRiver)
                {
                    ford = map.Biome?.waterMovingShallowTerrain ?? TerrainDefOf.WaterMovingShallow;
                }
                else
                {
                    ford = map.Biome?.waterShallowTerrain ?? TerrainDefOf.WaterShallow;
                }

                map.terrainGrid.RemoveTopLayer(cell, doLeavings: false);
                map.terrainGrid.SetTerrain(cell, ford ?? TerrainDefOf.WaterShallow);
            }
            else if (terr.passability == Traversability.Impassable)
            {
                map.terrainGrid.RemoveTopLayer(cell, doLeavings: false);
                map.terrainGrid.SetTerrain(cell, GetBiomeNaturalGround(map));
            }

            if (map.fogGrid != null && map.fogGrid.IsFogged(cell))
            {
                map.fogGrid.Unfog(cell);
            }

            passable[cell.z * w + cell.x] = IsExteriorPassableForCaravan(map, cell, origin);
        }

        private static bool IsExteriorPassableForCaravan(Map map, IntVec3 cell, IntVec3 origin)
        {
            if (!cell.InBounds(map) || GetSealStage(cell, origin) != 0) { return false; }

            var terr = cell.GetTerrain(map);
            if (terr == null || terr.passability == Traversability.Impassable
                || terr.dangerous || terr.exposesToVacuum || terr.burnDamage > 0)
            {
                return false;
            }

            var things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].def.passability == Traversability.Impassable)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasViviThing(Map map, IntVec3 cell)
        {
            var things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                var defName = things[i].def?.defName;
                if (defName != null && defName.StartsWith("VV_"))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ClearNonViviObstacles(Map map, IntVec3 cell, bool removePlants)
        {
            var things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                var t = things[i];
                if (t is Pawn || t.def.category == ThingCategory.Item) { continue; }
                if (t.def.defName != null && t.def.defName.StartsWith("VV_")) { continue; }
                if (!removePlants && t.def.category == ThingCategory.Plant) { continue; }

                DestroyForce(t);
            }
        }

        // 외부 전정: 거대 육각형 남쪽 입구 앞 풍화된 유적 파편, 1번 고대 요정꽃, 떠도는 소수의 거대 말벌.
        public static void BuildZone1Forecourt(Map map, IntVec3 origin, int investigated, bool coreExtracted, bool spawnHornets)
        {
            var flowerCell = EchoFlowerCell(1, origin);

            // 부서진 고대 진입로 바닥 (dz: -71..-59)
            for (int dz = -71; dz <= -59; dz++)
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
                (-7, -69, 0.45f), (-7, -68, 0.65f), (7, -69, 0.55f), (7, -68, 0.40f),
                (-10, -64, 0.50f), (-9, -64, 0.35f), (10, -64, 0.45f), (9, -64, 0.60f),
                (-6, -60, 0.70f), (6, -60, 0.65f),
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

            // 주변 비비아 꽃 연출
            IntVec2[] flowerSpots =
            {
                new IntVec2(-4, -66), new IntVec2(4, -66), new IntVec2(-5, -62), new IntVec2(5, -62),
                new IntVec2(-12, -63), new IntVec2(12, -63),
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
                new IntVec2(-16, -64),
                new IntVec2(16, -64),
                new IntVec2(-44, -44),
                new IntVec2(44, -44),
            };

            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = new IntVec3(origin.x + anchors[i].x, 0, origin.z + anchors[i].z).ClampInsideMap(map);
                if (!CellFinder.TryFindRandomCellNear(anchor, map, 5, c => c.InBounds(map) && c.Standable(map) && GetSealStage(c, origin) == 0, out var spawnCell))
                {
                    continue;
                }

                var hornet = PawnGenerator.GeneratePawn(VVPawnKindDefOf.VV_TitanicHornet, null);
                GenSpawn.Spawn(hornet, spawnCell, map, Rot4.South);
                hornet.mindState?.mentalStateHandler?.TryStartMentalState(VVMentalStateDefOf.VV_HornetBerserk, forceWake: true);

                var lordJob = new LordJob_RuinHornetWander(spawnCell, wanderRadius: 7.5f, guardRadius: 10.5f, retaliationRadius: 28f);
                LordMaker.MakeNewLord(null, lordJob, map, new[] { hornet });
            }
        }

        // 제1구간(거대 육각형 미로): 중앙 성소와 북쪽 복도를 빙 둘러싼 대규모 미궁, 중간중간 파괴 가능한 크림벽, 북단 출구의 2번 요정꽃.
        public static void BuildZone2Maze(Map map, IntVec3 origin, int investigated, bool zone3Open, int seed)
        {
            // 1) 전체 Stage 1 육각형 링 초기화: 바닥·지붕·고대 벽 채우기
            for (int dz = -59; dz <= 59; dz++)
            {
                for (int dx = -68; dx <= 68; dx++)
                {
                    var cell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (!cell.InBounds(map) || GetSealStage(cell, origin) != 1) { continue; }

                    DisplacePawnsAndClear(map, cell);

                    // 남쪽 입구 쪽은 풍화된 크림 바닥, 동·서 날개를 지나 북쪽 출구로 갈수록 정돈된 벌집 타일로 전이
                    TerrainDef floor = VVTerrainDefOf.VV_ViviCreamFloor;
                    if (dz >= 34 || (dz >= 8 && Math.Abs(dx * 5 + dz * 3) % 3 != 0) || (dz >= -18 && Math.Abs(dx * 7 + dz * 5) % 4 == 0))
                    {
                        floor = VVTerrainDefOf.VV_HoneycombTile;
                    }
                    SetFloor(map, cell, floor);
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);

                    bool isSouthGate = dz <= -56 && Mathf.Abs(dx) <= 1;
                    if (!isSouthGate)
                    {
                        SpawnBuildingClean(map, VVThingDefOf.VV_AncientRuinWall, cell);
                    }
                }
            }

            // 2) 유효한 5x5 미로 방 격자 노드 수집 (pitch = 6, 육각형 외벽 2칸 두께 보존 + 중앙/복도 침범 금지)
            var validNodes = new HashSet<IntVec2>();
            for (int row = -9; row <= 9; row++)
            {
                for (int col = -10; col <= 10; col++)
                {
                    if (IsValidMazeRoom(origin, col, row))
                    {
                        validNodes.Add(new IntVec2(col, row));
                    }
                }
            }

            var startNode = new IntVec2(0, -9);
            var goalNode = new IntVec2(0, 9);
            validNodes.Add(startNode);
            validNodes.Add(goalNode);

            // 시작 방과 연결된 주 연결 요소만 유지해 고립 방을 원천 차단
            var connected = CollectConnectedNodes(validNodes, startNode);

            // 3) 결정론적 시드 기반 미로 신장 트리 + 양쪽 날개 우회 루프 생성
            var rng = new System.Random(Gen.HashCombineInt(seed, 71933));
            var passages = BuildMazePassages(connected, startNode, goalNode, rng);

            // 4) 방 내부(5x5) 파내기
            foreach (var node in connected)
            {
                var center = MazeRoomCenter(origin, node.x, node.z);
                foreach (var cell in CellRect.CenteredOn(center, 5, 5))
                {
                    if (!cell.InBounds(map) || GetSealStage(cell, origin) != 1) { continue; }
                    DestroyEdifice(map, cell);

                    if (node == goalNode)
                    {
                        SetFloor(map, cell, cell == center ? VVTerrainDefOf.VV_SterileHoneycombTile : VVTerrainDefOf.VV_HoneycombTile);
                    }
                }
            }

            // 남쪽 대문(dz: -58..-57)과 시작 방(0, -9) 완전 연결
            for (int dz = -58; dz <= -56; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var gateCell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (gateCell.InBounds(map) && GetSealStage(gateCell, origin) == 1)
                    {
                        DestroyEdifice(map, gateCell);
                    }
                }
            }

            // 5) 통로 개방 및 파괴 가능한 비비 크림벽 봉쇄 설치 (3칸 폭 출입구)
            foreach (var kv in passages)
            {
                var (a, b) = kv.Key;
                bool isCream = kv.Value;
                foreach (var cell in GetMazeDoorCells(origin, a, b))
                {
                    if (!cell.InBounds(map) || GetSealStage(cell, origin) != 1) { continue; }
                    DestroyEdifice(map, cell);

                    if (isCream)
                    {
                        SpawnBuildingClean(map, VVThingDefOf.VV_ViviCreamWall, cell);
                    }
                }
            }

            // 6) 미로 내 주요 거점 등불 배치 (어두운 미궁 분위기 유지 + 크림벽 시인성 확보)
            foreach (var node in connected)
            {
                bool isMilestone = node == startNode
                    || (Math.Abs(node.x) % 3 == 0 && Math.Abs(node.z) % 3 == 0);
                if (!isMilestone || node == goalNode) { continue; }

                var center = MazeRoomCenter(origin, node.x, node.z);
                var lampCell = new IntVec3(center.x + (node.x >= 0 ? -2 : 2), 0, center.z + (node.z >= 0 ? -2 : 2));
                if (lampCell.InBounds(map) && GetSealStage(lampCell, origin) == 1 && lampCell.GetEdifice(map) == null)
                {
                    SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, lampCell);
                }
            }

            // 북단 출구 성소(0, +9) 장식 등불 및 2번 요정꽃
            SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x - 2, 0, origin.z + 56));
            SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x + 2, 0, origin.z + 56));

            if (investigated < 2)
            {
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientRuinFairyFlowerII, EchoFlowerCell(2, origin));
            }

            if (zone3Open)
            {
                OpenZone2NorthGate(map, origin);
            }
        }

        private static void OpenZone2NorthGate(Map map, IntVec3 origin)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = 51; dz <= 52; dz++)
                {
                    var gateCell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (gateCell.InBounds(map))
                    {
                        DestroyEdifice(map, gateCell);
                        SetFloor(map, gateCell, VVTerrainDefOf.VV_SterileHoneycombTile);
                    }
                }
            }
        }

        private static bool IsValidMazeRoom(IntVec3 origin, int col, int row)
        {
            var center = MazeRoomCenter(origin, col, row);
            for (int oz = -2; oz <= 2; oz++)
            {
                for (int ox = -2; ox <= 2; ox++)
                {
                    var c = new IntVec3(center.x + ox, 0, center.z + oz);
                    if (GetSealStage(c, origin) != 1) { return false; }
                    if (HexDist(c.x - origin.x, c.z - origin.z) > HexShellInnerApothem) { return false; }
                }
            }

            return true;
        }

        private static IntVec3 MazeRoomCenter(IntVec3 origin, int col, int row)
        {
            return new IntVec3(origin.x + col * MazePitch, 0, origin.z + row * MazePitch);
        }

        private static HashSet<IntVec2> CollectConnectedNodes(HashSet<IntVec2> validNodes, IntVec2 start)
        {
            var visited = new HashSet<IntVec2>();
            if (!validNodes.Contains(start)) { return visited; }

            var queue = new Queue<IntVec2>();
            visited.Add(start);
            queue.Enqueue(start);

            IntVec2[] dirs = { new IntVec2(1, 0), new IntVec2(-1, 0), new IntVec2(0, 1), new IntVec2(0, -1) };
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                for (int i = 0; i < dirs.Length; i++)
                {
                    var nxt = new IntVec2(cur.x + dirs[i].x, cur.z + dirs[i].z);
                    if (validNodes.Contains(nxt) && visited.Add(nxt))
                    {
                        queue.Enqueue(nxt);
                    }
                }
            }

            return visited;
        }

        private static (IntVec2, IntVec2) CanonicalEdge(IntVec2 a, IntVec2 b)
        {
            if (a.x < b.x || (a.x == b.x && a.z <= b.z))
            {
                return (a, b);
            }
            return (b, a);
        }

        private static Dictionary<(IntVec2, IntVec2), bool> BuildMazePassages(HashSet<IntVec2> nodes, IntVec2 start, IntVec2 goal, System.Random rng)
        {
            var passages = new Dictionary<(IntVec2, IntVec2), bool>();
            var visited = new HashSet<IntVec2> { start };
            var stack = new Stack<(IntVec2 node, IntVec2 lastDir)>();
            stack.Push((start, new IntVec2(0, 0)));

            IntVec2[] dirs = { new IntVec2(1, 0), new IntVec2(-1, 0), new IntVec2(0, 1), new IntVec2(0, -1) };

            // 직진 편향 DFS로 길고 구불구불한 회랑 감각을 살린다.
            while (stack.Count > 0)
            {
                var (cur, lastDir) = stack.Peek();
                var unvisited = new List<IntVec2>(4);
                for (int i = 0; i < dirs.Length; i++)
                {
                    var nxt = new IntVec2(cur.x + dirs[i].x, cur.z + dirs[i].z);
                    if (nodes.Contains(nxt) && !visited.Contains(nxt))
                    {
                        unvisited.Add(dirs[i]);
                    }
                }

                if (unvisited.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                IntVec2 chosenDir;
                if (lastDir != default && unvisited.Contains(lastDir) && rng.NextDouble() < 0.55)
                {
                    chosenDir = lastDir;
                }
                else
                {
                    chosenDir = unvisited[rng.Next(unvisited.Count)];
                }

                var next = new IntVec2(cur.x + chosenDir.x, cur.z + chosenDir.z);
                visited.Add(next);
                passages[CanonicalEdge(cur, next)] = false;
                stack.Push((next, chosenDir));
            }

            // 북단 출구 방(0, +9)은 동·서 양쪽 날개 모두에서 도달 가능하도록 인접 통로 추가
            var goalWest = new IntVec2(goal.x - 1, goal.z);
            var goalEast = new IntVec2(goal.x + 1, goal.z);
            if (nodes.Contains(goalWest)) { passages[CanonicalEdge(goal, goalWest)] = true; }
            if (nodes.Contains(goalEast)) { passages[CanonicalEdge(goal, goalEast)] = true; }

            // 시작 방(0, -9) 양쪽 진입로 확보 (입구에서 바로 좌·우·전방 탐색 가능)
            var startWest = new IntVec2(start.x - 1, start.z);
            var startEast = new IntVec2(start.x + 1, start.z);
            if (nodes.Contains(startWest)) { passages[CanonicalEdge(start, startWest)] = false; }
            if (nodes.Contains(startEast)) { passages[CanonicalEdge(start, startEast)] = false; }

            // 일부 추가 우회로(약 12%)를 뚫어 동·서 날개 양방향 탐색을 허용하되 미궁의 분기감을 유지
            var allPossible = new List<(IntVec2, IntVec2)>();
            foreach (var n in nodes)
            {
                var right = new IntVec2(n.x + 1, n.z);
                var up = new IntVec2(n.x, n.z + 1);
                if (nodes.Contains(right))
                {
                    var e = CanonicalEdge(n, right);
                    if (!passages.ContainsKey(e)) { allPossible.Add(e); }
                }
                if (nodes.Contains(up))
                {
                    var e = CanonicalEdge(n, up);
                    if (!passages.ContainsKey(e)) { allPossible.Add(e); }
                }
            }

            for (int i = 0; i < allPossible.Count; i++)
            {
                if (rng.NextDouble() < 0.11)
                {
                    passages[allPossible[i]] = rng.NextDouble() < 0.45;
                }
            }

            // 동·서 날개와 남쪽 접근로의 핵심 관문에 파괴 가능한 비비 크림벽 봉쇄를 배치한다.
            var keys = passages.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                var edge = keys[i];
                var a = edge.Item1;
                var b = edge.Item2;

                // 시작 방 바로 옆 통로는 열어두어 입구에서 바로 걸어 들어갈 수 있게 한다.
                if (a == start || b == start)
                {
                    passages[edge] = false;
                    continue;
                }

                // 북단 출구 방(0, +9)으로 들어가는 모든 문은 반드시 크림벽으로 봉쇄한다.
                if (a == goal || b == goal)
                {
                    passages[edge] = true;
                    continue;
                }

                // 남쪽 관문선(row -7 -> -6), 적도 관문선(row -1 -> 0), 북쪽 관문선(row 5 -> 6)을 넘는 세로 통로에 크림벽 배치
                if (a.x == b.x && ((a.z == -7 && b.z == -6) || (a.z == -1 && b.z == 0) || (a.z == 5 && b.z == 6)))
                {
                    passages[edge] = true;
                    continue;
                }

                // 시작 방 양옆의 2차 관문(±1,-9 <-> ±2,-9)에 크림벽을 두어 초반부터 기믹을 인지시킨다.
                if (a.z == start.z && b.z == start.z && ((a.x == -2 && b.x == -1) || (a.x == 1 && b.x == 2)))
                {
                    passages[edge] = true;
                }
            }

            return passages;
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

        // 제2구간(길쭉하고 넓은 시련 복도): 미로 북단에서 정중앙 원형 성소로 이어지는 21x30 온전한 제국 대복도.
        public static void BuildZone3Hall(Map map, IntVec3 origin, int investigated, bool zone4Open, IReadOnlyCollection<IntVec3> safePath)
        {
            OpenZone2NorthGate(map, origin);

            for (int dz = 20; dz <= 51; dz++)
            {
                for (int dx = -10; dx <= 10; dx++)
                {
                    var cell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (!cell.InBounds(map) || GetSealStage(cell, origin) != 2) { continue; }

                    DisplacePawnsAndClear(map, cell);
                    SetFloor(map, cell, VVTerrainDefOf.VV_HoneycombTile);
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);

                    bool isNorthGate = dz == 51 && Mathf.Abs(dx) <= 1;
                    bool isSouthGate = zone4Open && dz <= 23 && Mathf.Abs(dx) <= 2;
                    if (!isNorthGate && !isSouthGate && IsCorridorOuterWall(cell, origin))
                    {
                        SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumWall, cell);
                    }
                }
            }

            // 시련 구역 북·남 경계 장식선 (dz = 47, dz = 25)
            for (int dx = -9; dx <= 9; dx++)
            {
                var northEdge = new IntVec3(origin.x + dx, 0, origin.z + 47);
                var southEdge = new IntVec3(origin.x + dx, 0, origin.z + 25);
                if (northEdge.InBounds(map) && GetSealStage(northEdge, origin) == 2 && northEdge.GetEdifice(map) == null)
                {
                    SetFloor(map, northEdge, VVTerrainDefOf.VV_SterileHoneycombTile);
                }
                if (southEdge.InBounds(map) && GetSealStage(southEdge, origin) == 2 && southEdge.GetEdifice(map) == null)
                {
                    SetFloor(map, southEdge, VVTerrainDefOf.VV_SterileHoneycombTile);
                }
            }

            // 좌·우 대칭 황금 열주(dx = ±6, 4칸 간격: 최대 지붕 거리 6.32 < 6.9로 지붕 붕괴 0) + 측면 성소 등불
            int[] pillarZ = { 26, 30, 34, 38, 42, 46 };
            for (int i = 0; i < pillarZ.Length; i++)
            {
                var z = origin.z + pillarZ[i];
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumWall, new IntVec3(origin.x - 6, 0, z));
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumWall, new IntVec3(origin.x + 6, 0, z));

                if (i == 0 || i == 2 || i == 4)
                {
                    SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x - 8, 0, z + 2));
                    SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x + 8, 0, z + 2));
                }
            }

            // 북단 입구 전실 및 남단 단상 등불
            SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x - 5, 0, origin.z + 49));
            SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x + 5, 0, origin.z + 49));
            SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x - 4, 0, origin.z + 24));
            SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumLamp, new IntVec3(origin.x + 4, 0, origin.z + 24));

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

        private static bool IsCorridorOuterWall(IntVec3 cell, IntVec3 origin)
        {
            var dx = cell.x - origin.x;
            var dz = cell.z - origin.z;
            if (dz == 51 || Mathf.Abs(dx) == 10) { return true; }

            for (int i = 0; i < 4; i++)
            {
                var adj = cell + GenAdj.CardinalDirections[i];
                if (GetSealStage(adj, origin) != 2)
                {
                    return true;
                }
            }

            return false;
        }

        // 제3구간(정중앙 원형 회랑): 거대 육각형의 정중앙(origin)에 배치된 원형 성소 + 육각형 화분 6개와 공명 요정꽃 6개.
        public static List<Building_RuinPuzzleFairyFlower> BuildZone4Ring(Map map, IntVec3 origin, bool hexagonSolved, int seed)
        {
            // 제2구간 남쪽 경계 대문(dx: -2..+2, dz: 21..23) 개방
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = 21; dz <= 23; dz++)
                {
                    var gateCell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (gateCell.InBounds(map))
                    {
                        DestroyEdifice(map, gateCell);
                        SetFloor(map, gateCell, VVTerrainDefOf.VV_SterileHoneycombTile);
                    }
                }
            }

            for (int dz = -23; dz <= 23; dz++)
            {
                for (int dx = -23; dx <= 23; dx++)
                {
                    var cell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (!cell.InBounds(map) || GetSealStage(cell, origin) != 3) { continue; }

                    bool isNorthDoor = dz >= 20 && Mathf.Abs(dx) <= 2;
                    if (!isNorthDoor)
                    {
                        DisplacePawnsAndClear(map, cell);
                    }

                    var dist = Mathf.Sqrt(dx * dx + dz * dz);
                    bool isOuterWall = !isNorthDoor && IsRingOuterWall(cell, origin);

                    // 외곽 아케이드(15.8 ~ 22.5, 외벽까지 최대 6.7 < 6.9)만 지붕을 얹어 중앙 성소 광장은 하늘로 열어 둔다.
                    if (dist >= 15.8f)
                    {
                        map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                    }
                    else
                    {
                        map.roofGrid.SetRoof(cell, null);
                    }

                    bool isInlay = (dist >= 17.5f && dist <= 18.5f)
                        || (dist >= 9.2f && dist <= 10.1f)
                        || (Mathf.Abs(dx) <= 1 && dz >= 9 && dz <= 22);
                    SetFloor(map, cell, isInlay ? VVTerrainDefOf.VV_SterileHoneycombTile : VVTerrainDefOf.VV_HoneycombTile);

                    if (isOuterWall)
                    {
                        SpawnBuildingClean(map, VVThingDefOf.VV_AncientSanctumWall, cell);
                    }
                }
            }

            // 원형 회랑 둘레 고대 마나 등불 10개 (대칭 배치)
            (int dx, int dz)[] lampOffsets =
            {
                (-6, 19), (6, 19),
                (-16, 11), (16, 11),
                (-19, 0), (19, 0),
                (-16, -11), (16, -11),
                (-7, -18), (7, -18),
            };
            for (int i = 0; i < lampOffsets.Length; i++)
            {
                var lampCell = new IntVec3(origin.x + lampOffsets[i].dx, 0, origin.z + lampOffsets[i].dz);
                if (lampCell.InBounds(map) && GetSealStage(lampCell, origin) == 3 && lampCell.GetEdifice(map) == null)
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

        private static bool IsRingOuterWall(IntVec3 cell, IntVec3 origin)
        {
            for (int i = 0; i < 4; i++)
            {
                var adj = cell + GenAdj.CardinalDirections[i];
                var stage = GetSealStage(adj, origin);
                if (stage == 0 || stage == 1 || stage == 2)
                {
                    return true;
                }
            }

            return false;
        }

        // 제4구간(정중앙 제단 성소): 육각형 퍼즐 완료 시 봉인이 풀리며 중심 제단과 마지막 4번 요정꽃이 드러난다.
        public static void BuildZone5AltarSanctum(Map map, IntVec3 origin, int investigated, bool coreExtracted)
        {
            for (int dz = -9; dz <= 9; dz++)
            {
                for (int dx = -9; dx <= 9; dx++)
                {
                    var cell = new IntVec3(origin.x + dx, 0, origin.z + dz);
                    if (!cell.InBounds(map) || GetSealStage(cell, origin) != 4) { continue; }

                    DisplacePawnsAndClear(map, cell);
                    map.roofGrid.SetRoof(cell, null);

                    var dist = Mathf.Sqrt(dx * dx + dz * dz);
                    bool sterile = dist <= 4.0f || (dist >= 7.2f && dist <= 8.5f) || (Mathf.Abs(dx) <= 1 && dz >= 0);
                    SetFloor(map, cell, sterile ? VVTerrainDefOf.VV_SterileHoneycombTile : VVTerrainDefOf.VV_HoneycombTile);
                }
            }

            SpawnBuildingClean(map, VVThingDefOf.VV_AncientRuinAltar, CentralAltarCell(origin));

            if (investigated < 4 && !coreExtracted)
            {
                SpawnBuildingClean(map, VVThingDefOf.VV_AncientRuinFairyFlowerIV, EchoFlowerCell(4, origin));
            }
        }

        // 중력선 체류 등 맵이 남는 경우, 거대 육각형 내부 전체를 다시 자연 지형·결계 장벽으로 되돌린다.
        public static void ResealAllStagesToNaturalGround(Map map, IntVec3 origin)
        {
            var bounds = FootprintBounds(origin).ClipInsideMap(map);
            foreach (var cell in bounds)
            {
                if (GetSealStage(cell, origin) <= 0) { continue; }

                ClearCellObstacles(map, cell, removePlants: true);
                map.roofGrid.SetRoof(cell, null);
                map.terrainGrid.RemoveTopLayer(cell, doLeavings: false);
                EnsureSolidNaturalGround(map, cell, stripArtificialFloor: true);
            }

            RefreshSpatialBarriers(map, origin, _ => false);
            UnfogSanctuary(map, origin, forceMeshDirty: true);
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

            var bounds = GladeBounds(origin).ClipInsideMap(map);
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

        private static bool IsSafeSolidNaturalTerrain(TerrainDef t)
        {
            if (t == null) { return false; }
            if (t.IsWater || t.IsFlood || t.dangerous || t.exposesToVacuum || t.burnDamage > 0) { return false; }
            if (t.passability == Traversability.Impassable) { return false; }
            return t.affordances != null && t.affordances.Contains(TerrainAffordanceDefOf.Medium);
        }

        private static TerrainDef GetBiomeNaturalGround(Map map)
        {
            var biome = map?.Biome;
            if (biome != null)
            {
                if (!biome.terrainsByFertility.NullOrEmpty())
                {
                    float[] sampleFertilities = { 0.5f, 0.25f, 0.8f, 0.05f };
                    for (int i = 0; i < sampleFertilities.Length; i++)
                    {
                        var sample = TerrainThreshold.TerrainAtValue(biome.terrainsByFertility, sampleFertilities[i]);
                        if (IsSafeSolidNaturalTerrain(sample))
                        {
                            return sample;
                        }
                    }

                    for (int i = 0; i < biome.terrainsByFertility.Count; i++)
                    {
                        var cand = biome.terrainsByFertility[i].terrain;
                        if (IsSafeSolidNaturalTerrain(cand))
                        {
                            return cand;
                        }
                    }
                }

                if (IsSafeSolidNaturalTerrain(biome.gravelTerrain))
                {
                    return biome.gravelTerrain;
                }

                var heavy = biome.TerrainForAffordance(TerrainAffordanceDefOf.Heavy);
                if (IsSafeSolidNaturalTerrain(heavy))
                {
                    return heavy;
                }
            }

            return TerrainDefOf.Soil;
        }

        private static void EnsureSolidNaturalGround(Map map, IntVec3 cell, bool stripArtificialFloor = false)
        {
            var cur = cell.GetTerrain(map);
            if (!stripArtificialFloor && cur != null && cur.defName != null && cur.defName.StartsWith("VV_"))
            {
                var under = map.terrainGrid.UnderTerrainAt(cell);
                if (IsSafeSolidNaturalTerrain(under))
                {
                    return;
                }

                map.terrainGrid.SetUnderTerrain(cell, GetBiomeNaturalGround(map));
                return;
            }

            if (stripArtificialFloor && cur != null && (!cur.natural || cur.IsFloor || cur.IsRoad))
            {
                map.terrainGrid.RemoveTopLayer(cell, doLeavings: false);
                cur = cell.GetTerrain(map);
                if (cur == null || !cur.natural || cur.IsFloor || cur.IsRoad || !IsSafeSolidNaturalTerrain(cur))
                {
                    map.terrainGrid.SetTerrain(cell, GetBiomeNaturalGround(map));
                }
                return;
            }

            if (IsSafeSolidNaturalTerrain(cur)) { return; }

            map.terrainGrid.RemoveTopLayer(cell, doLeavings: false);
            if (!IsSafeSolidNaturalTerrain(cell.GetTerrain(map)))
            {
                map.terrainGrid.SetTerrain(cell, GetBiomeNaturalGround(map));
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
