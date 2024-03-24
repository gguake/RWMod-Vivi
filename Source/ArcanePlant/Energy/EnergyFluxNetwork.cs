using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class EnergyFluxNetwork : IEnumerable<EnergyAcceptor>
    {
        private static int HashCounter = 1;

        public int NetworkHash { get; private set; }

        private Dictionary<EnergyAcceptor, EnergyFluxNetworkNode> _nodes { get; }
        private List<EnergyFluxNetworkNode> _nodesList { get; }
        private int _lastRefreshTick;

        private List<int> _tempDistributeEnergyNodeCandidateIndices = new List<int>();
        private List<int> _tempDistributeEnergyNodeIndices = new List<int>();

        public EnergyFluxNetwork()
        {
            _nodes = new Dictionary<EnergyAcceptor, EnergyFluxNetworkNode>();
            _nodesList = new List<EnergyFluxNetworkNode>();

            NetworkHash = HashCounter++.HashOffset();
        }

        public IEnumerator<EnergyAcceptor> GetEnumerator()
        {
            return _nodes.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _nodes.Keys.GetEnumerator();
        }

        public EnergyFluxNetworkNode this[EnergyAcceptor energyAcceptor]
        {
            get
            {
                return _nodes.TryGetValue(energyAcceptor, out var node) ? node : null;
            }
        }

        public void AddEnergyNode(EnergyAcceptor energyAcceptor, EnergyFluxNetworkNode node)
        {
            if (node == null) { return; }

            _nodes.Add(energyAcceptor, node);
            _nodesList.Add(node);
            _lastRefreshTick = GenTicks.TicksGame;
        }

        public void RemoveEnergyNode(EnergyAcceptor energyAcceptor)
        {
            if (_nodes.TryGetValue(energyAcceptor, out var node))
            {
                _nodesList.Remove(node);
                _nodes.Remove(energyAcceptor);
                _lastRefreshTick = GenTicks.TicksGame;
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
                    var plant = node.Plant;
                    if (plant == null) { continue; }

                    var extension = plant.ArcanePlantModExtension;

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
                        var plant = node.Plant;
                        if (plant == null) { continue; }

                        if (plant.ArcanePlantModExtension.energyCapacity - node.energy >= maxDividedEnergy)
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
                            var plant = node.Plant;
                            if (plant == null) { continue; }

                            node.energy = Mathf.Clamp(node.energy + realDividedEnergy, 0f, plant.ArcanePlantModExtension.energyCapacity);
                        }
                    }
                }

                _lastRefreshTick = tick;
            }
        }

        public void Clear()
        {
            _nodes.Clear();
            _nodesList.Clear();
            _lastRefreshTick = 0;
        }
    }
}
