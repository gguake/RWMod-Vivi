using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxNetwork : IEnumerable<ManaAcceptor>
    {
        private static int HashCounter = 1;

        public int NetworkHash { get; private set; }

        private Dictionary<ManaAcceptor, ManaFluxNetworkNode> _nodes { get; }
        private List<ManaFluxNetworkNode> _nodesList { get; }
        private int _lastRefreshTick;

        private List<int> _tempDistributeManaFluxNodeCandidateIndices = new List<int>();
        private List<int> _tempDistributeManaFluxNodeIndices = new List<int>();

        public ManaFluxNetwork()
        {
            _nodes = new Dictionary<ManaAcceptor, ManaFluxNetworkNode>();
            _nodesList = new List<ManaFluxNetworkNode>();

            NetworkHash = HashCounter++.HashOffset();
        }

        public IEnumerator<ManaAcceptor> GetEnumerator()
        {
            return _nodes.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _nodes.Keys.GetEnumerator();
        }

        public ManaFluxNetworkNode this[ManaAcceptor acceptor]
        {
            get
            {
                return _nodes.TryGetValue(acceptor, out var node) ? node : null;
            }
        }

        public void AddManaFluxNode(ManaAcceptor acceptor, ManaFluxNetworkNode node)
        {
            if (node == null) { return; }

            _nodes.Add(acceptor, node);
            _nodesList.Add(node);
            _lastRefreshTick = GenTicks.TicksGame;
        }

        public void RemoveManaFluxNode(ManaAcceptor acceptor)
        {
            if (_nodes.TryGetValue(acceptor, out var node))
            {
                _nodesList.Remove(node);
                _nodes.Remove(acceptor);
                _lastRefreshTick = GenTicks.TicksGame;
            }
        }

        public void Tick()
        {
            var tick = GenTicks.TicksGame;
            if (tick > _lastRefreshTick)
            {
                _tempDistributeManaFluxNodeCandidateIndices.Clear();

                float totalOvergeneratedMana = 0f;
                for (int i = 0; i < _nodesList.Count; ++i)
                {
                    var node = _nodesList[i];
                    var plant = node.Plant;
                    if (plant == null) { continue; }

                    var extension = plant.ArcanePlantModExtension;

                    var tickInterval = tick - _lastRefreshTick;
                    var consumed = extension.manaConsumeRule?.CalcManaFlux(plant, tickInterval) ?? 0f;
                    var generated = extension.manaGenerateRule?.CalcManaFlux(plant, tickInterval) ?? 0f;

                    var manaCapacity = extension.manaCapacity;
                    var afterMana = node.mana - consumed + generated;

                    node.mana = Mathf.Clamp(afterMana, 0f, manaCapacity);

                    var overgenerated = afterMana - manaCapacity;
                    if (overgenerated > 0f)
                    {
                        totalOvergeneratedMana += overgenerated;
                    }
                    else
                    {
                        _tempDistributeManaFluxNodeCandidateIndices.Add(i);
                    }
                }

                if (totalOvergeneratedMana > 0f && _tempDistributeManaFluxNodeCandidateIndices.Count > 0)
                {
                    _tempDistributeManaFluxNodeIndices.Clear();
                    var maxDividedMana = totalOvergeneratedMana / _tempDistributeManaFluxNodeCandidateIndices.Count;
                    for (int i = 0; i < _tempDistributeManaFluxNodeCandidateIndices.Count; ++i)
                    {
                        var node = _nodesList[_tempDistributeManaFluxNodeCandidateIndices[i]];
                        var plant = node.Plant;
                        if (plant == null) { continue; }

                        if (plant.ArcanePlantModExtension.manaCapacity - node.mana >= maxDividedMana)
                        {
                            _tempDistributeManaFluxNodeIndices.Add(_tempDistributeManaFluxNodeCandidateIndices[i]);
                        }
                    }

                    if (_tempDistributeManaFluxNodeIndices.Count > 0)
                    {
                        var realDividedMana = totalOvergeneratedMana / _tempDistributeManaFluxNodeIndices.Count;
                        for (int i = 0; i < _tempDistributeManaFluxNodeIndices.Count; ++i)
                        {
                            var node = _nodesList[_tempDistributeManaFluxNodeIndices[i]];
                            var plant = node.Plant;
                            if (plant == null) { continue; }

                            node.mana = Mathf.Clamp(node.mana + realDividedMana, 0f, plant.ArcanePlantModExtension.manaCapacity);
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
