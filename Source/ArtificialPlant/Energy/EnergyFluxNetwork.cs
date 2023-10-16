using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class EnergyFluxNetwork : IEnumerable<ArtificialPlant>
    {
        private static int HashCounter = 1;

        public int NetworkHash { get; private set; }
        public int ShouldRegenerateNetworkTick { get; set; } = -1;

        private Dictionary<ArtificialPlant, EnergyFluxNetworkNode> _nodes { get; }
        private List<EnergyFluxNetworkNode> _nodesList { get; }
        private int _lastRefreshTick;

        private List<int> _tempDistributeEnergyNodeCandidateIndices = new List<int>();
        private List<int> _tempDistributeEnergyNodeIndices = new List<int>();

        public EnergyFluxNetwork()
        {
            _nodes = new Dictionary<ArtificialPlant, EnergyFluxNetworkNode>();
            _nodesList = new List<EnergyFluxNetworkNode>();

            NetworkHash = HashCounter++.HashOffset();
        }

        public IEnumerator<ArtificialPlant> GetEnumerator()
        {
            return _nodes.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _nodes.Keys.GetEnumerator();
        }

        public EnergyFluxNetworkNode this[ArtificialPlant plant]
        {
            get
            {
                return _nodes.TryGetValue(plant, out var node) ? node : null;
            }
        }

        public void AddPlant(ArtificialPlant plant, EnergyFluxNetworkNode node)
        {
            _nodes.Add(plant, node);
            _nodesList.Add(node);
            _lastRefreshTick = GenTicks.TicksGame;
        }

        public void RemovePlant(ArtificialPlant plant)
        {
            if (_nodes.TryGetValue(plant, out var node))
            {
                _nodesList.Remove(node);
                _nodes.Remove(plant);

                ShouldRegenerateNetworkTick = GenTicks.TicksGame + 1;
            }
        }

        public void Tick()
        {
            var tick = GenTicks.TicksGame;
            if (tick > _lastRefreshTick)
            {
                _tempDistributeEnergyNodeCandidateIndices.Clear();

                float totalOvergeneratedEnergy = 0f;
                for (int i = 0; i < _nodesList.Count; ++i)
                {
                    var node = _nodesList[i];
                    var plant = node.plant;
                    var extension = plant.ArtificialPlantModExtension;

                    var tickInterval = tick - _lastRefreshTick;
                    var consumed = extension.energyConsumeRule?.CalcEnergy(plant, tickInterval) ?? 0f;
                    var generated = extension.energyGenerateRule?.CalcEnergy(plant, tickInterval) ?? 0f;

                    var energyCapacity = extension.energyCapacity;
                    var afterEnergy = node.energy - consumed + generated;

                    node.energy = Mathf.Clamp(afterEnergy, 0f, energyCapacity);

                    var overgenerated = afterEnergy - energyCapacity;
                    if (overgenerated > 0f)
                    {
                        totalOvergeneratedEnergy += overgenerated;
                    }
                    else
                    {
                        _tempDistributeEnergyNodeCandidateIndices.Add(i);
                    }
                }

                if (totalOvergeneratedEnergy > 0f && _tempDistributeEnergyNodeCandidateIndices.Count > 0)
                {
                    _tempDistributeEnergyNodeIndices.Clear();
                    var maxDividedEnergy = totalOvergeneratedEnergy / _tempDistributeEnergyNodeCandidateIndices.Count;
                    for (int i = 0; i < _tempDistributeEnergyNodeCandidateIndices.Count; ++i)
                    {
                        var node = _nodesList[_tempDistributeEnergyNodeCandidateIndices[i]];
                        if (node.plant.ArtificialPlantModExtension.energyCapacity - node.energy >= maxDividedEnergy)
                        {
                            _tempDistributeEnergyNodeIndices.Add(_tempDistributeEnergyNodeCandidateIndices[i]);
                        }
                    }

                    if (_tempDistributeEnergyNodeIndices.Count > 0)
                    {
                        var realDividedEnergy = totalOvergeneratedEnergy / _tempDistributeEnergyNodeIndices.Count;
                        for (int i = 0; i < _tempDistributeEnergyNodeIndices.Count; ++i)
                        {
                            var node = _nodesList[_tempDistributeEnergyNodeIndices[i]];
                            node.energy = Mathf.Clamp(node.energy + realDividedEnergy, 0f, node.plant.ArtificialPlantModExtension.energyCapacity);
                        }
                    }
                }

                _lastRefreshTick = tick;
            }
        }

        public void MergeNetworks(IEnumerable<EnergyFluxNetwork> networks)
        {
            if (networks.EnumerableNullOrEmpty()) { return; }

            foreach (var network in networks)
            {
                _nodes.AddRange(network._nodes);
                _nodesList.AddRange(network._nodesList);
                foreach (var node in network._nodes.Values)
                {
                    node.plant.EnergyFluxNetwork = this;
                }
            }

            _lastRefreshTick = GenTicks.TicksGame;
        }

        public void SplitNetworks()
        {
            if (_nodes.Count <= 1) { return; }

            var remainingNodes = new HashSet<EnergyFluxNetworkNode>(_nodes.Values);
            while (remainingNodes.Any())
            {
                var element = remainingNodes.First();
                remainingNodes.Remove(element);

                var newConnectedSet = new HashSet<EnergyFluxNetworkNode>();
                var queue = new Queue<EnergyFluxNetworkNode>();
                queue.Enqueue(element);

                while (true)
                {
                    if (queue.Count == 0) { break; }

                    var current = queue.Dequeue();
                    newConnectedSet.Add(current);

                    foreach (var connected in current.connectedNodes)
                    {
                        if (remainingNodes.Contains(connected))
                        {
                            remainingNodes.Remove(connected);
                            queue.Enqueue(connected);
                        }
                    }
                }

                var newNetwork = new EnergyFluxNetwork();
                foreach (var node in newConnectedSet)
                {
                    newNetwork.AddPlant(node.plant, node);
                    node.plant.EnergyFluxNetwork = newNetwork;
                }
            }

            ShouldRegenerateNetworkTick = -1;
        }
    }
}
