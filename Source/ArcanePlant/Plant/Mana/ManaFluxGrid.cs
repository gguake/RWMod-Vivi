using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public struct ManaFluxGridCell
    {
        public ManaTransmitter transmitter;
        public ManaAcceptor provider;
        public int networkIndex;

        public bool IsEmpty => transmitter == null && provider == null;

        public ManaFluxGridCell WithTransmitter(ManaTransmitter transmitter)
        {
            return new ManaFluxGridCell
            {
                transmitter = transmitter,
                provider = provider,
                networkIndex = networkIndex,
            };
        }

        public ManaFluxGridCell WithProvider(ManaAcceptor provider)
        {
            return new ManaFluxGridCell
            {
                transmitter = transmitter,
                provider = provider,
                networkIndex = networkIndex,
            };
        }

        public ManaFluxGridCell WithNetworkIndex(int index)
        {
            return new ManaFluxGridCell
            {
                transmitter = transmitter,
                provider = provider,
                networkIndex = index,
            };
        }

        public void ChangeFluxNetwork(ManaFluxNetwork network)
        {
            if (provider != null)
            {
                if (provider.ManaFluxNetwork != null)
                {
                    provider.ManaFluxNetwork.RemoveManaFluxNode(provider);
                }

                network.AddManaFluxNode(provider, provider.ManaFluxNode);
            }
        }
    }

    public class ManaFluxGrid
    {
        private static Dictionary<Map, ManaFluxGrid> _grids = new Dictionary<Map, ManaFluxGrid>();
        private static Dictionary<Map, int> _gridSpawnedCount = new Dictionary<Map, int>();

        public static void Notify_SpawnManaAcceptor(ManaAcceptor acceptor)
        {
            var map = acceptor.Map;
            if (!_grids.TryGetValue(map, out var grid))
            {
                grid = new ManaFluxGrid(map);
                _grids.Add(map, grid);
                _gridSpawnedCount.Add(map, 0);
            }

            grid.AddManaAcceptor(acceptor);
            _gridSpawnedCount[map]++;
        }

        public static void Notify_DespawnManaAcceptor(ManaAcceptor acceptor)
        {
            var map = acceptor.Map;
            if (_gridSpawnedCount.ContainsKey(map))
            {
                _gridSpawnedCount[map]--;
                if (_gridSpawnedCount[map] == 0)
                {
                    _gridSpawnedCount.Remove(map);
                    _grids.Remove(map);
                }
                else
                {
                    _grids[map].RemoveManaAcceptor(acceptor);
                }
            }
        }

        public static ManaFluxGrid GetFluxGrid(Map map) => _grids.TryGetValue(map, out var grid) ? grid : null;

        public bool ShouldRefreshNetwork => GenTicks.TicksGame >= _shouldRefreshNetworkTicks;

        private Map _map;
        private Dictionary<IntVec3, ManaFluxGridCell> _manaFluxGridCells;
        private List<ManaFluxNetwork> _manaFluxNetworks;
        private int _shouldRefreshNetworkTicks = int.MaxValue;

        public ManaFluxGrid(Map map)
        {
            _map = map;
            _manaFluxGridCells = new Dictionary<IntVec3, ManaFluxGridCell>();
            _manaFluxNetworks = new List<ManaFluxNetwork>();
        }

        public void AddManaAcceptor(ManaAcceptor acceptor)
        {
            foreach (var pos in acceptor.OccupiedRect().Cells)
            {
                if (!_manaFluxGridCells.TryGetValue(pos, out var gridCell))
                {
                    _manaFluxGridCells.Add(pos, gridCell);
                }

                if (acceptor.HasManaFlux)
                {
                    _manaFluxGridCells[pos] = gridCell.WithProvider(acceptor);
                }
                else if (acceptor is ManaTransmitter transmitter)
                {
                    _manaFluxGridCells[pos] = gridCell.WithTransmitter(transmitter);
                }
            }

            _shouldRefreshNetworkTicks = GenTicks.TicksGame + 1;
        }

        public void RemoveManaAcceptor(ManaAcceptor acceptor)
        {
            foreach (var pos in acceptor.OccupiedRect().Cells)
            {
                if (_manaFluxGridCells.TryGetValue(pos, out var gridCell))
                {
                    if (acceptor.HasManaFlux)
                    {
                        _manaFluxGridCells[pos] = gridCell.WithProvider(null);
                    }
                    else if (acceptor is ManaTransmitter)
                    {
                        _manaFluxGridCells[pos] = gridCell.WithTransmitter(null);
                    }

                    if (_manaFluxGridCells[pos].IsEmpty)
                    {
                        _manaFluxGridCells.Remove(pos);
                    }
                }
            }

            _shouldRefreshNetworkTicks = GenTicks.TicksGame + 1;
        }

        public void RefreshNetworks()
        {
            foreach (var network in _manaFluxNetworks)
            {
                network.Clear();
            }

            int generatedTree = 0;
            var remainingGridCells = _manaFluxGridCells.Keys.ToHashSet();
            while (remainingGridCells.Count > 0)
            {
                var root = remainingGridCells.First();
                remainingGridCells.Remove(root);

                var connectedSet = new HashSet<IntVec3>();
                var queue = new Queue<IntVec3>();
                queue.Enqueue(root);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    connectedSet.Add(current);

                    foreach (var adjCell in GenAdj.CardinalDirections.Select(d => current + d).Where(v => v.InBounds(_map) && remainingGridCells.Contains(v)))
                    {
                        remainingGridCells.Remove(adjCell);
                        queue.Enqueue(adjCell);
                    }
                }

                var networkIndex = GetEmptyNetworkIndex();
                foreach (var cell in connectedSet)
                {
                    var gridCell = _manaFluxGridCells[cell].WithNetworkIndex(networkIndex);
                    _manaFluxGridCells[cell] = gridCell;

                    var network = _manaFluxNetworks[networkIndex];
                    if (gridCell.provider != null && cell == gridCell.provider.Position)
                    {
                        gridCell.provider.ManaFluxNetwork = network;
                        network.AddManaFluxNode(gridCell.provider, gridCell.provider.ManaFluxNode);
                    }
                    if (gridCell.transmitter != null)
                    {
                        gridCell.transmitter.ManaFluxNetwork = network;
                    }
                }

                generatedTree++;
            }

            if (DebugSettings.godMode)
            {
                Log.Message($"[VVRace] Tried to refresh mana flux networks: node counts: {_manaFluxGridCells.Count} tree: {generatedTree}");
            }

            _shouldRefreshNetworkTicks = int.MaxValue;
        }

        public ManaFluxGridCell this[IntVec3 pos] => _manaFluxGridCells.TryGetValue(pos, out var gridCell) ? gridCell : default;

        private int GetEmptyNetworkIndex()
        {
            for (int i = 0; i < _manaFluxNetworks.Count; ++i)
            {
                if (_manaFluxNetworks[i].Count() == 0)
                {
                    return i;
                }
            }

            var newNetwork = new ManaFluxNetwork();
            _manaFluxNetworks.Add(newNetwork);
            return _manaFluxNetworks.Count - 1;
        }
    }
}
