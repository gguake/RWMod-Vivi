using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public enum ViviBaseRoomType
    {
        Empty,
        Barrack,
        Dining,
        Resting,
        GatheringHoney,
        GreenHouse,
        Crafting,
        Refining,
        Trap,
        Hatchery,
    }

    public class SymbolResolver_ViviBasePart : SymbolResolver
    {
        public override void Resolve(ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;
            var faction = resolveParams.faction ?? Find.FactionManager.RandomEnemyFaction();
            var hexaRoomSystem = new HexagonalRoomSystem(map, resolveParams.rect);

            var innerRingRooms = new List<HexagonalRoom>();
            var innerRing = HexagonalRoomUtility.HexagonalRingWithRadius(1);
            var outerRingRooms = new List<HexagonalRoom>();
            var outerRing = HexagonalRoomUtility.HexagonalRingWithRadius(2);
            var outerRingLines = new List<List<HexagonalRoom>>();
            var outerRingLine = new List<HexagonalRoom>();

            #region 중앙 방 생성
            if (!hexaRoomSystem.TryAddRoom(new IntVec2(0, 0), out _))
            {
                return;
            }
            #endregion

            #region 안쪽 테두리 방 생성
            foreach (var coord in innerRing)
            {
                if (hexaRoomSystem.TryAddRoom(coord, out var room))
                {
                    innerRingRooms.Add(room);
                }
            }
            #endregion

            #region 바깥쪽 테두리 방 생성
            foreach (var coord in outerRing)
            {
                if (Rand.Chance(0.35f))
                {
                    if (outerRingLine.Count > 0)
                    {
                        outerRingLines.Add(outerRingLine);
                        outerRingLine = new List<HexagonalRoom>();
                    }
                }
                else
                {
                    if (hexaRoomSystem.TryAddRoom(coord, out var room))
                    {
                        outerRingRooms.Add(room);
                        outerRingLine.Add(room);
                    }
                }
            }
            if (outerRingLine.Count > 0)
            {
                outerRingLines.Add(outerRingLine);
            }
            #endregion

            #region 커넥터 생성
            {
                hexaRoomSystem.TryAddConnector(new IntVec2(0, 0), new IntVec2(1, 0));
                hexaRoomSystem.TryAddConnector(new IntVec2(0, 0), new IntVec2(-1, 0));

                // 안쪽 링 커넥터
                for (int i = 0; i < innerRing.Length - 1; ++i)
                {
                    hexaRoomSystem.TryAddConnector(innerRing[i], innerRing[i + 1]);
                }
                hexaRoomSystem.TryAddConnector(innerRing[innerRing.Length - 1], innerRing[0]);

                // 바깥쪽 링 커넥터
                foreach (var line in outerRingLines)
                {
                    var connector = line.RandomElement();
                    if (connector == null)
                    {
                        break;
                    }

                    foreach (var v in connector.hexaCoord.HexagonalNeighbors())
                    {
                        var adjacentRoom = hexaRoomSystem[v];
                        if (adjacentRoom != null)
                        {
                            hexaRoomSystem.TryAddConnector(connector.hexaCoord, adjacentRoom.hexaCoord);
                        }
                    }

                    for (int i = 0; i < line.Count - 1; ++i)
                    {
                        hexaRoomSystem.TryAddConnector(line[i].hexaCoord, line[i + 1].hexaCoord);
                    }
                }
            }
            #endregion

            #region 외부 출입구 생성
            {
                var candidateRoomsCanBeEntrance = new List<HexagonalRoom>();
                foreach (var room in hexaRoomSystem.Rooms)
                {
                    foreach (var v in room.hexaCoord.HexagonalNeighbors())
                    {
                        var connected = hexaRoomSystem[v];
                        if (connected == null)
                        {
                            candidateRoomsCanBeEntrance.Add(room);
                            break;
                        }
                    }
                }

                if (candidateRoomsCanBeEntrance.NullOrEmpty())
                {
                    Log.Warning("there is no entrance for base.");
                }
                else
                {
                    var room = candidateRoomsCanBeEntrance.RandomElement();
                    var neighbor = room.hexaCoord.HexagonalNeighbors().Where(v => hexaRoomSystem[v] == null).RandomElement();

                    var p = resolveParams;
                    p.rect = HexagonalRoomUtility.CalcConnectorRect(room, neighbor);
                    p.singleThingDef = ThingDefOf.Door;
                    p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                    p.faction = faction;
                    BaseGen.symbolStack.Push("thing", p);
                }
            }
            #endregion

            #region 커넥터 생성
            foreach (var connectorRect in hexaRoomSystem.ConnectorRects)
            {
                var p = resolveParams;
                p.rect = connectorRect;
                p.singleThingDef = ThingDefOf.Door;
                p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                p.faction = faction;
                BaseGen.symbolStack.Push("thing", p);
            }
            #endregion

            #region 방 생성
            {
                var dictRoomCounter = new Dictionary<ViviBaseRoomType, int>()
                {
                    { ViviBaseRoomType.Barrack, Mathf.Clamp(resolveParams.bedCount.HasValue ? Mathf.CeilToInt(resolveParams.bedCount.Value / 8f) : 1, 1, 5) },
                    { ViviBaseRoomType.Dining, 1 },
                    { ViviBaseRoomType.GatheringHoney, 2 },
                    { ViviBaseRoomType.Refining, 2 },
                    { ViviBaseRoomType.GreenHouse, 2 },
                };

                var rooms = hexaRoomSystem.Rooms.ToList();
                var centerRoom = hexaRoomSystem[new IntVec2(0, 0)];
                rooms.Remove(centerRoom);
                rooms.Shuffle();

                var requiredRoomCount = Mathf.Max(rooms.Count - dictRoomCounter.Values.Sum(), 0);
                if (requiredRoomCount > 0)
                {
                    var candidates = Enum.GetValues(typeof(ViviBaseRoomType)).Cast<ViviBaseRoomType>().ToHashSet();
                    candidates.Remove(ViviBaseRoomType.Empty);
                    candidates.Remove(ViviBaseRoomType.Hatchery);
                    candidates.Remove(ViviBaseRoomType.Barrack);

                    while (requiredRoomCount > 0)
                    {
                        var roomType = candidates.RandomElement();
                        if (dictRoomCounter.TryGetValue(roomType, out var count))
                        {
                            dictRoomCounter[roomType] = count + 1;
                        }
                        else
                        {
                            dictRoomCounter.Add(roomType, 1);
                        }

                        requiredRoomCount--;
                    }
                }

                var roomQueue = new Queue<ViviBaseRoomType>();
                while (dictRoomCounter.Count > 0)
                {
                    var keys = dictRoomCounter.Keys.ToList();
                    foreach (var key in keys)
                    {
                        roomQueue.Enqueue(key);

                        dictRoomCounter[key]--;
                        if (dictRoomCounter[key] == 0)
                        {
                            dictRoomCounter.Remove(key);
                        }
                    }
                }

                // 중앙에는 항상 부화실
                {
                    var p = resolveParams;
                    p.rect = centerRoom.cellRect;
                    p.floorDef = VVTerrainDefOf.VV_SterileHoneycombTile;
                    p.faction = faction;
                    BaseGen.symbolStack.Push(GetSymbolForRoom(ViviBaseRoomType.Hatchery), p);
                }

                for (int i = 0; i < rooms.Count; ++i)
                {
                    var room = rooms[i];
                    var roomType = roomQueue.Count > 0 ? roomQueue.Dequeue() : ViviBaseRoomType.Empty;
                    var symbol = GetSymbolForRoom(roomType);
                    if (symbol != null)
                    {
                        var p = resolveParams;
                        p.rect = room.cellRect;
                        p.floorDef = Rand.Chance(0.8f) ? VVTerrainDefOf.VV_HoneycombTile : VVTerrainDefOf.VV_ViviCreamFloor;
                        p.faction = faction;
                        BaseGen.symbolStack.Push(symbol, p);
                    }
                }
            }
            #endregion
        }

        private string GetSymbolForRoom(ViviBaseRoomType roomType)
        {
            switch (roomType)
            {
                case ViviBaseRoomType.Barrack:
                    return "vv_hexa_room_barracks";

                case ViviBaseRoomType.Dining:
                    return "vv_hexa_room_dining";

                case ViviBaseRoomType.Resting:
                    return "vv_hexa_room_resting";

                case ViviBaseRoomType.GatheringHoney:
                    return "vv_hexa_room_gathering_honey";

                case ViviBaseRoomType.GreenHouse:
                    return "vv_hexa_room_greenhouse";

                case ViviBaseRoomType.Crafting:
                    return "vv_hexa_room_crafting";

                case ViviBaseRoomType.Refining:
                    return "vv_hexa_room_refining";

                case ViviBaseRoomType.Trap:
                    return "vv_hexa_room_trap";

                case ViviBaseRoomType.Hatchery:
                    return "vv_hexa_room_hatchery";

                case ViviBaseRoomType.Empty:
                default:
                    return "vv_hexa_empty_room";
            }
        }
    }
}
