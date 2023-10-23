using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public struct EnergyFluxGridCell
    {
        public EnergyTransmitter transmitter;
        public ArtificialPlant artificialPlant;
        public int networkIndex;

        public bool IsEmpty => transmitter == null && artificialPlant == null;

        public EnergyFluxGridCell WithTransmitter(EnergyTransmitter transmitter)
        {
            return new EnergyFluxGridCell
            {
                transmitter = transmitter,
                artificialPlant = artificialPlant,
                networkIndex = networkIndex,
            };
        }

        public EnergyFluxGridCell WithArtificialPlant(ArtificialPlant artificialPlant)
        {
            return new EnergyFluxGridCell
            {
                transmitter = transmitter,
                artificialPlant = artificialPlant,
                networkIndex = networkIndex,
            };
        }

        public EnergyFluxGridCell WithNetworkIndex(int index)
        {
            return new EnergyFluxGridCell
            {
                transmitter = transmitter,
                artificialPlant = artificialPlant,
                networkIndex = index,
            };
        }

        public void ChangeFluxNetwork(EnergyFluxNetwork network)
        {
            if (artificialPlant != null)
            {
                if (artificialPlant.EnergyFluxNetwork != null)
                {
                    artificialPlant.EnergyFluxNetwork.RemoveEnergyNode(artificialPlant);
                }

                network.AddEnergyNode(artificialPlant, artificialPlant.EnergyFluxNode);
            }
        }
    }

    public class EnergyFluxGrid
    {
        private static Dictionary<Map, EnergyFluxGrid> _grids = new Dictionary<Map, EnergyFluxGrid>();
        private static Dictionary<Map, int> _gridSpawnedCount = new Dictionary<Map, int>();

        public static void Notify_SpawnEnergyAcceptor(EnergyAcceptor energyAcceptor)
        {
            var map = energyAcceptor.Map;
            if (!_grids.TryGetValue(map, out var grid))
            {
                grid = new EnergyFluxGrid(map);
                _grids.Add(map, grid);
                _gridSpawnedCount.Add(map, 0);
            }

            grid.AddEnergyAcceptor(energyAcceptor);
            _gridSpawnedCount[map]++;
        }

        public static void Notify_DespawnEnergyAcceptor(EnergyAcceptor energyAcceptor)
        {
            var map = energyAcceptor.Map;
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
                    _grids[map].RemoveEnergyAcceptor(energyAcceptor);
                }
            }
        }

        public static EnergyFluxGrid GetFluxGrid(Map map) => _grids.TryGetValue(map, out var grid) ? grid : null;

        public bool ShouldRefreshNetwork => GenTicks.TicksGame >= _shouldRefreshNetworkTicks;

        private Map _map;
        private Dictionary<IntVec3, EnergyFluxGridCell> _energyFluxGridCells;
        private List<EnergyFluxNetwork> _energyFluxNetworks;
        private int _shouldRefreshNetworkTicks = int.MaxValue;

        public EnergyFluxGrid(Map map)
        {
            _map = map;
            _energyFluxGridCells = new Dictionary<IntVec3, EnergyFluxGridCell>();
            _energyFluxNetworks = new List<EnergyFluxNetwork>();
        }

        public void AddEnergyAcceptor(EnergyAcceptor energyAcceptor)
        {
            var pos = energyAcceptor.Position;
            if (!_energyFluxGridCells.TryGetValue(pos, out var gridCell))
            {
                _energyFluxGridCells.Add(pos, gridCell);
            }

            if (energyAcceptor is ArtificialPlant artificialPlant)
            {
                _energyFluxGridCells[pos] = gridCell.WithArtificialPlant(artificialPlant);
            }
            else if (energyAcceptor is EnergyTransmitter transmitter)
            {
                _energyFluxGridCells[pos] = gridCell.WithTransmitter(transmitter);
            }

            _shouldRefreshNetworkTicks = GenTicks.TicksGame + 1;
        }

        public void RemoveEnergyAcceptor(EnergyAcceptor energyAcceptor)
        {
            var pos = energyAcceptor.Position;

            if (_energyFluxGridCells.TryGetValue(pos, out var gridCell))
            {
                if (energyAcceptor is ArtificialPlant)
                {
                    _energyFluxGridCells[pos] = gridCell.WithArtificialPlant(null);
                }
                else if (energyAcceptor is EnergyTransmitter)
                {
                    _energyFluxGridCells[pos] = gridCell.WithTransmitter(null);
                }

                if (_energyFluxGridCells[pos].IsEmpty)
                {
                    _energyFluxGridCells.Remove(pos);
                }
            }

            _shouldRefreshNetworkTicks = GenTicks.TicksGame + 1;
        }

        public void RefreshNetworks()
        {
            foreach (var network in _energyFluxNetworks)
            {
                network.Clear();
            }

            int generatedTree = 0;
            var remainingGridCells = _energyFluxGridCells.Keys.ToHashSet();
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
                    var gridCell = _energyFluxGridCells[cell].WithNetworkIndex(networkIndex);
                    _energyFluxGridCells[cell] = gridCell;

                    var network = _energyFluxNetworks[networkIndex];
                    if (gridCell.artificialPlant != null)
                    {
                        gridCell.artificialPlant.EnergyFluxNetwork = network;
                        network.AddEnergyNode(gridCell.artificialPlant, gridCell.artificialPlant.EnergyFluxNode);
                    }
                    if (gridCell.transmitter != null)
                    {
                        gridCell.transmitter.EnergyFluxNetwork = network;
                    }
                }

                generatedTree++;
            }

            if (DebugSettings.godMode)
            {
                Log.Message($"[VVRace] Tried to refresh energy flux networks: node counts: {_energyFluxGridCells.Count} tree: {generatedTree}");
            }

            _shouldRefreshNetworkTicks = int.MaxValue;
        }

        private int GetEmptyNetworkIndex()
        {
            for (int i = 0; i < _energyFluxNetworks.Count; ++i)
            {
                if (_energyFluxNetworks[i].Count() == 0)
                {
                    return i;
                }
            }

            var newNetwork = new EnergyFluxNetwork();
            _energyFluxNetworks.Add(newNetwork);
            return _energyFluxNetworks.Count - 1;
        }
    }
}
