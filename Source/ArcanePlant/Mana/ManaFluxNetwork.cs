using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public struct ManaFluxNetworkHistory
    {
        public int tick;
        public int generated;
        public int consumed;
        public int exceeded;
    }

    public class ManaFluxNetwork : IEnumerable<ManaAcceptor>
    {
        private static int HashCounter = 1;

        public int NetworkHash { get; private set; }
        public Queue<ManaFluxNetworkHistory> FluxHistory { get; private set; } = new Queue<ManaFluxNetworkHistory>();

        private Dictionary<ManaAcceptor, ManaFluxNetworkNode> _nodes { get; }
        private List<ManaFluxNetworkNode> _nodesList { get; }
        private int _lastRefreshTick;
        private int _lastRefreshHistoryTick;

        private List<(int index, float lackedMana)> _tempDistributeManaFluxNodeCandidateIndices = new List<(int index, float lackedMana)>();

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

                var totalGeneratedAtTick = 0f;
                var totalConsumedAtTick = 0f;

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

                    totalGeneratedAtTick += generated;
                    totalConsumedAtTick += consumed;

                    var manaCapacity = extension.manaCapacity;
                    var afterMana = node.mana - consumed + generated;

                    node.mana = Mathf.Clamp(afterMana, 0f, manaCapacity);

                    var overgenerated = afterMana - manaCapacity;
                    if (overgenerated > 0f)
                    {
                        totalOvergeneratedMana += overgenerated;
                    }
                    else if (overgenerated < 0f)
                    {
                        _tempDistributeManaFluxNodeCandidateIndices.Add((i, -overgenerated));
                    }
                }

                if (_nodesList.Count > 1)
                {
                    while (totalOvergeneratedMana > 0f && _tempDistributeManaFluxNodeCandidateIndices.Count > 0)
                    {
                        var dividedMana = totalOvergeneratedMana / _tempDistributeManaFluxNodeCandidateIndices.Count;

                        for (int i = 0; i < _tempDistributeManaFluxNodeCandidateIndices.Count; ++i)
                        {
                            var t = _tempDistributeManaFluxNodeCandidateIndices[i];
                            var node = _nodesList[t.index];
                            if (dividedMana >= t.lackedMana)
                            {
                                // 균등 분배된 마나가 부족량보다 많은경우 최대치로 채우고 리스트에서 제거
                                totalOvergeneratedMana -= t.lackedMana;
                                node.mana += t.lackedMana;

                                _tempDistributeManaFluxNodeCandidateIndices.RemoveAt(i--);
                            }
                            else
                            {
                                // 균등 분배된 마나가 부족량보다 적은경우 분배된 양만큼만 채우고 다음 루프로
                                totalOvergeneratedMana -= dividedMana;
                                node.mana += dividedMana;

                                _tempDistributeManaFluxNodeCandidateIndices[i] = (t.index, t.lackedMana - dividedMana);
                            }
                        }
                    }
                }

                if (tick > _lastRefreshHistoryTick + 2000)
                {
                    var history = new ManaFluxNetworkHistory()
                    {
                        tick = tick,
                        generated = (int)totalGeneratedAtTick,
                        consumed = (int)totalConsumedAtTick,
                        exceeded = (int)totalOvergeneratedMana,
                    };

                    if (FluxHistory.Count > 30) { FluxHistory.Dequeue(); }

                    FluxHistory.Enqueue(history);
                    _lastRefreshHistoryTick = tick;
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
