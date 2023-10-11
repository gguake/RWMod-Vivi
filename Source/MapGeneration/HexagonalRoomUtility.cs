using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public enum HexagonalRoomDirection
    {
        Up,
        UpRight,
        DownRight,
        Down,
        DownLeft,
        UpLeft,
    }

    public enum HexagonalRoomEdgeType
    {
        Blocked,
        Door,
        Open,
    }

    public class HexagonalRoom
    {
        public IntVec2 hexaCoord;
        public CellRect cellRect;

        public HexagonalRoom(IntVec3 mainCenter, IntVec2 hexaCoordinate)
        {
            hexaCoord = hexaCoordinate;

            var v = hexaCoord.x * HexagonalRoomUtility.XBasis + hexaCoord.z * HexagonalRoomUtility.ZBasis;
            var centerX = mainCenter.x + v.x;
            var centerZ = mainCenter.z + v.z;
            cellRect = CellRect.CenteredOn(new IntVec3(centerX, 0, centerZ), HexagonalRoomUtility.RoomCellSize.x, HexagonalRoomUtility.RoomCellSize.z);
        }
    }

    public class HexagonalRoomSystem
    {
        private Map _map;
        private CellRect _mainRect;
        private IntVec3 _mainCenter;

        private Dictionary<IntVec2, HexagonalRoom> _rooms = new Dictionary<IntVec2, HexagonalRoom>();
        public IEnumerable<HexagonalRoom> Rooms => _rooms.Values;

        public HexagonalRoom this[IntVec2 coord]
        {
            get
            {
                return _rooms.TryGetValue(coord, out var v) ? v : null;
            }
        }

        private HashSet<(IntVec2, IntVec2)> _connectedEdges = new HashSet<(IntVec2, IntVec2)>();
        private List<(CellRect, HexagonalRoomEdgeType)> _connectorRects = new List<(CellRect, HexagonalRoomEdgeType)>();
        public IEnumerable<(CellRect cellRect, HexagonalRoomEdgeType edgeType)> ConnectorRects => _connectorRects;

        public HexagonalRoomSystem(Map map, CellRect mainRect)
        {
            _map = map;
            _mainRect = mainRect;
            _mainCenter = mainRect.CenterCell;
        }

        public bool TryAddRoom(IntVec2 hexaCoordinate, out HexagonalRoom room)
        {
            if (_rooms.ContainsKey(hexaCoordinate))
            {
                room = null;
                return false;
            }

            room = new HexagonalRoom(_mainCenter, hexaCoordinate);
            if (!_mainRect.Contains(new IntVec3(room.cellRect.minX, 0, room.cellRect.minZ)) || !_mainRect.Contains(new IntVec3(room.cellRect.maxX, 0, room.cellRect.maxZ)))
            {
                room = null;
                return false;
            }

            if (!room.cellRect.InBounds(_map))
            {
                room = null;
                return false;
            }

            _rooms.Add(hexaCoordinate, room);
            return true;
        }

        public bool TryAddConnector(IntVec2 a, IntVec2 b)
        {
            if (_connectedEdges.Contains((a, b)))
            {
                return false;
            }

            if (!_rooms.TryGetValue(a, out var roomA) || !_rooms.TryGetValue(b, out var roomB))
            {
                return false;
            }

            var connectorRect = HexagonalRoomUtility.CalcConnectorRect(roomA, roomB, out var edgeType);
            if (connectorRect.IsEmpty)
            {
                return false;
            }

            _connectedEdges.Add((a, b));
            _connectedEdges.Add((b, a));
            _connectorRects.Add((connectorRect, edgeType));
            return true;
        }

    }

    public static class HexagonalRoomUtility
    {
        public static IEnumerable<HexagonalRoomDirection> HexagonalRoomDirections
        {
            get
            {
                yield return HexagonalRoomDirection.Up;
                yield return HexagonalRoomDirection.UpRight;
                yield return HexagonalRoomDirection.DownRight;
                yield return HexagonalRoomDirection.Down;
                yield return HexagonalRoomDirection.DownLeft;
                yield return HexagonalRoomDirection.UpLeft;
            }
        }

        public static IEnumerable<IntVec2> HexagonalCoordinatesOperation
        {
            get
            {
                yield return new IntVec2(0, -1);    // Up
                yield return new IntVec2(1, -1);    // UpRight
                yield return new IntVec2(1, 0);     // DownRight
                yield return new IntVec2(0, 1);     // Down
                yield return new IntVec2(-1, 1);    // DownLeft
                yield return new IntVec2(-1, 0);    // UpLeft
            }
        }

        public static IEnumerable<IntVec2> HexagonalNeighbors(this IntVec2 coord)
        {
            foreach (var v in HexagonalCoordinatesOperation)
            {
                yield return coord + v;
            }
        }

        public static IEnumerable<IntVec2> HexagonalVerticalNeighbors(this IntVec2 coord)
        {
            yield return coord + new IntVec2(0, -1);
            yield return coord + new IntVec2(0, 1);
        }

        public static readonly IntVec2 RoomCellSize = new IntVec2(17, 11);
        public static readonly IntVec3 XBasis = new IntVec3(13, 0, -5);         // = (1, 0)
        public static readonly IntVec3 ZBasis = new IntVec3(0, 0, -10);         // = (0, 1)

        #region Ring Coordinates
        private static readonly IntVec2[] _hexagonalRingWithRadius1 = new IntVec2[]
        {
            new IntVec2(1, 0),
            new IntVec2(1, -1),
            new IntVec2(0, -1),
            new IntVec2(-1, 0),
            new IntVec2(-1, 1),
            new IntVec2(0, 1),
        };
        private static readonly IntVec2[] _hexagonalRingWithRadius2 = new IntVec2[]
        {
            new IntVec2(2, 0),
            new IntVec2(2, -1),
            new IntVec2(2, -2),
            new IntVec2(1, -2),
            new IntVec2(0, -2),
            new IntVec2(-1, -1),
            new IntVec2(-2, 0),
            new IntVec2(-2, 1),
            new IntVec2(-2, 2),
            new IntVec2(-1, 2),
            new IntVec2(0, 2),
            new IntVec2(1, 1),
        };
        private static readonly IntVec2[] _hexagonalRingWithRadius3 = new IntVec2[]
        {
            new IntVec2(3, 0),
            new IntVec2(3, -1),
            new IntVec2(3, -2),
            new IntVec2(3, -3),
            new IntVec2(2, -3),
            new IntVec2(1, -3),
            new IntVec2(0, -3),
            new IntVec2(-1, -2),
            new IntVec2(-2, -1),
            new IntVec2(-3, 0),
            new IntVec2(-3, 1),
            new IntVec2(-3, 2),
            new IntVec2(-3, 3),
            new IntVec2(-2, 3),
            new IntVec2(-1, 3),
            new IntVec2(0, 3),
            new IntVec2(1, 2),
            new IntVec2(2, 1),
        };
        #endregion

        public static IntVec2[] HexagonalRingWithRadius(int radius)
        {
            switch (radius)
            {
                case 0:
                    return new IntVec2[] { new IntVec2(0, 0) };

                case 1:
                    return _hexagonalRingWithRadius1;

                case 2:
                    return _hexagonalRingWithRadius2;

                case 3:
                    return _hexagonalRingWithRadius3;
            }

            throw new NotImplementedException();
        }

        public static HexagonalRoomDirection Inverse(this HexagonalRoomDirection direction)
        {
            switch (direction)
            {
                case HexagonalRoomDirection.Up:
                    return HexagonalRoomDirection.Down;

                case HexagonalRoomDirection.UpRight:
                    return HexagonalRoomDirection.DownLeft;

                case HexagonalRoomDirection.DownRight:
                    return HexagonalRoomDirection.UpLeft;

                case HexagonalRoomDirection.Down:
                    return HexagonalRoomDirection.Up;

                case HexagonalRoomDirection.DownLeft:
                    return HexagonalRoomDirection.UpRight;

                case HexagonalRoomDirection.UpLeft:
                    return HexagonalRoomDirection.DownRight;
            }

            throw new NotImplementedException();
        }

        public static IntVec2 DirectionToHexCoordinateVector(this HexagonalRoomDirection direction)
        {
            switch (direction)
            {
                case HexagonalRoomDirection.Up:
                    return new IntVec2(0, -1);          // (0, 10)

                case HexagonalRoomDirection.UpRight:
                    return new IntVec2(1, -1);          // (13, -5) + (0, 10) = (13, 5)

                case HexagonalRoomDirection.DownRight:
                    return new IntVec2(1, 0);           // (13, -5)

                case HexagonalRoomDirection.Down:
                    return new IntVec2(0, 1);           // (0, -10)

                case HexagonalRoomDirection.DownLeft:
                    return new IntVec2(-1, 1);          // (-13, 5) + (0, -10) = (-13, -5)

                case HexagonalRoomDirection.UpLeft:
                    return new IntVec2(-1, 0);          // (-13, 5)
            }

            throw new NotImplementedException();
        }

        public static CellRect GetHexagonalRoomRect(this IntVec2 center)
        {
            return new CellRect(center.x - RoomCellSize.x / 2, center.z - RoomCellSize.z / 2, RoomCellSize.x, RoomCellSize.z);
        }

        public static IEnumerable<IntVec2> GetHexagonalEdges(this CellRect cellRect)
        {
            if (cellRect.Width < RoomCellSize.x || cellRect.Height < RoomCellSize.z)
            {
                yield break;
            }

            cellRect.maxX = cellRect.minX + RoomCellSize.x - 1;
            cellRect.maxZ = cellRect.minZ + RoomCellSize.z - 1;

            var data = new List<(int centerX, int centerZ, int signX, int signZ)>
            {
                (cellRect.minX, cellRect.minZ, 1, 1),
                (cellRect.minX, cellRect.maxZ, 1, -1),
                (cellRect.maxX, cellRect.minZ, -1, 1),
                (cellRect.maxX, cellRect.maxZ, -1, -1)
            };

            foreach (var v in data)
            {
                for (int x = 3; x < 8; ++x)
                {
                    yield return new IntVec2(v.centerX + x * v.signX, v.centerZ);
                }

                yield return new IntVec2(v.centerX + 3 * v.signX, v.centerZ + 1 * v.signZ);
                yield return new IntVec2(v.centerX + 2 * v.signX, v.centerZ + 1 * v.signZ);

                yield return new IntVec2(v.centerX + 2 * v.signX, v.centerZ + 2 * v.signZ);

                yield return new IntVec2(v.centerX + 2 * v.signX, v.centerZ + 3 * v.signZ);
                yield return new IntVec2(v.centerX + 1 * v.signX, v.centerZ + 3 * v.signZ);

                yield return new IntVec2(v.centerX + 1 * v.signX, v.centerZ + 4 * v.signZ);
                yield return new IntVec2(v.centerX + 0 * v.signX, v.centerZ + 4 * v.signZ);
            }
            
            foreach (var doorCell in GetHexagonalOrthogonalCell(cellRect))
            {
                yield return doorCell;
            }
        }

        public static IEnumerable<IntVec2> GetHexagonalOrthogonalCell(this CellRect cellRect)
        {
            if (cellRect.Width < RoomCellSize.x || cellRect.Height < RoomCellSize.z)
            {
                yield break;
            }

            yield return new IntVec2(cellRect.minX, cellRect.minZ + cellRect.Height / 2);
            yield return new IntVec2(cellRect.maxX, cellRect.minZ + cellRect.Height / 2);
            yield return new IntVec2(cellRect.minX + cellRect.Width / 2, cellRect.minZ);
            yield return new IntVec2(cellRect.minX + cellRect.Width / 2, cellRect.maxZ);
        }

        public static IEnumerable<IntVec2> GetHexagonalFills(this CellRect cellRect)
        {
            if (cellRect.Width < RoomCellSize.x || cellRect.Height < RoomCellSize.z)
            {
                yield break;
            }

            cellRect.maxX = cellRect.minX + RoomCellSize.x - 1;
            cellRect.maxZ = cellRect.minZ + RoomCellSize.z - 1;

            for (int x = cellRect.minX + 3; x <= cellRect.maxX - 3; ++x)
            {
                yield return new IntVec2(x, cellRect.minZ);
                yield return new IntVec2(x, cellRect.maxZ);
            }


            for (int x = cellRect.minX + 2; x <= cellRect.maxX - 2; ++x)
            {
                yield return new IntVec2(x, cellRect.minZ + 1);
                yield return new IntVec2(x, cellRect.minZ + 2);
                yield return new IntVec2(x, cellRect.maxZ - 1);
                yield return new IntVec2(x, cellRect.maxZ - 2);
            }

            for (int x = cellRect.minX + 1; x <= cellRect.maxX - 1; ++x)
            {
                yield return new IntVec2(x, cellRect.minZ + 3);
                yield return new IntVec2(x, cellRect.maxZ - 3);
            }

            for (int x = cellRect.minX; x <= cellRect.maxX; ++x)
            {
                yield return new IntVec2(x, cellRect.minZ + 4);
                yield return new IntVec2(x, cellRect.maxZ - 4);
                yield return new IntVec2(x, cellRect.minZ + 5);
            }
        }

        public static CellRect CalcConnectorRect(HexagonalRoom root, HexagonalRoom connected, out HexagonalRoomEdgeType edgeType)
        {
            var directionByRootVector = connected.hexaCoord - root.hexaCoord;

            if (directionByRootVector.x == 0)
            {
                var x = root.cellRect.CenterCell.x;
                var z = directionByRootVector.z < 0 ? root.cellRect.maxZ : root.cellRect.minZ;

                edgeType = HexagonalRoomEdgeType.Door;
                return new CellRect(x, z, 1, 1);
            }
            else if (Mathf.Abs(directionByRootVector.x + directionByRootVector.z) <= 1)
            {
                IntVec3 v1, v2;
                if (directionByRootVector.x > 0)
                {
                    v1 = new IntVec3(root.cellRect.maxX, 0, directionByRootVector.z == 0 ? root.cellRect.minZ : root.cellRect.maxZ);
                    v2 = new IntVec3(connected.cellRect.minX, 0, directionByRootVector.z == 0 ? connected.cellRect.maxZ : connected.cellRect.minZ);
                }
                else
                {
                    v1 = new IntVec3(root.cellRect.minX, 0, directionByRootVector.z == 0 ? root.cellRect.maxZ : root.cellRect.minZ);
                    v2 = new IntVec3(connected.cellRect.maxX, 0, directionByRootVector.z == 0 ? connected.cellRect.minZ : connected.cellRect.maxZ);
                }

                edgeType = HexagonalRoomEdgeType.Open;
                return CellRect.FromLimits(v1, v2).ContractedBy(1);
            }

            edgeType = HexagonalRoomEdgeType.Blocked;
            return CellRect.Empty;
        }

    }
}
