using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
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
                if (Rand.Chance(0.6f))
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
                // 중앙방은 위 아래로 연결
                hexaRoomSystem.TryAddConnector(new IntVec2(0, 0), new IntVec2(0, 1));
                hexaRoomSystem.TryAddConnector(new IntVec2(0, 0), new IntVec2(0, 1));

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
                    // 수직으로 연결된 방이 하나라도 없다면 외부와 연결할 수 있음
                    foreach (var v in room.hexaCoord.HexagonalVerticalNeighbors())
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
                    var neighbor = room.hexaCoord.HexagonalVerticalNeighbors().Where(v => hexaRoomSystem[v] == null).RandomElement();

                    var p = resolveParams;
                    if (neighbor.z - room.hexaCoord.z == -1)
                    {
                        p.rect = new CellRect(room.cellRect.CenterCell.x, room.cellRect.maxZ, 1, 1);
                    }
                    else if (neighbor.z - room.hexaCoord.z == 1)
                    {
                        p.rect = new CellRect(room.cellRect.CenterCell.x, room.cellRect.minZ, 1, 1);
                    }

                    if (p.rect.Width == 1 && p.rect.Height == 1)
                    {
                        p.singleThingDef = ThingDefOf.Door;
                        p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                        p.faction = faction;
                        BaseGen.symbolStack.Push("thing", p);
                    }
                }
            }
            #endregion

            #region 커넥터 생성
            foreach (var connectorInfo in hexaRoomSystem.ConnectorRects)
            {
                if (connectorInfo.edgeType == HexagonalRoomEdgeType.Door)
                {
                    var p = resolveParams;
                    p.rect = connectorInfo.cellRect;
                    p.singleThingDef = ThingDefOf.Door;
                    p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                    p.faction = faction;
                    BaseGen.symbolStack.Push("thing", p);
                }
                else if (connectorInfo.edgeType == HexagonalRoomEdgeType.Open)
                {
                    var p = resolveParams;
                    p.rect = connectorInfo.cellRect;
                    BaseGen.symbolStack.Push("vv_hexa_connector", p);
                }
            }
            #endregion

            #region 방 생성
            foreach (var room in hexaRoomSystem.Rooms)
            {
                var p = resolveParams;
                p.rect = room.cellRect;
                p.floorDef = room.hexaCoord.x == 0 && room.hexaCoord.z == 0 ? VVTerrainDefOf.VV_ViviCreamFloor : VVTerrainDefOf.VV_HoneycombTile;
                p.faction = faction;
                BaseGen.symbolStack.Push("vv_hexa_empty_room", p);
            }
            #endregion
        }
    }
}
