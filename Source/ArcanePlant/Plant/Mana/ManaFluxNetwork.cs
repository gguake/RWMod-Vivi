using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public struct ManaFluxNetworkHistory
    {
        public int tick;
        public int generated;
        public int consumed;
        public int transfered;
        public int exceeded;

        public override string ToString()
        {
            return $"{tick}|{generated}|{consumed}|{transfered}|{exceeded}";
        }
    }

    public class ManaFluxNetwork : IEnumerable<ManaAcceptor>
    {
        public const int MaxHistoryLength = 150;

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

                var tickInterval = tick - _lastRefreshTick;
                int totalGeneratedDaily = 0;
                int totalConsumedDaily = 0;
                float totalOvergenerated = 0f;

                for (int i = 0; i < _nodesList.Count; ++i)
                {
                    var node = _nodesList[i];
                    if (node.manaAcceptor == null || !node.manaAcceptor.HasManaFlux || !node.manaAcceptor.Spawned) { continue; }

                    var extension = node.manaAcceptor.ManaExtension;

                    var generatedDaily = extension.manaGenerateRule?.CalcManaFlux(node.manaAcceptor) ?? 0;
                    var consumedDaily = extension.manaConsumeRule?.CalcManaFlux(node.manaAcceptor) ?? 0;

                    totalGeneratedDaily += generatedDaily;
                    totalConsumedDaily += consumedDaily;

                    var manaCapacity = extension.manaCapacity;
                    var manaChange = (generatedDaily - consumedDaily) * tickInterval / 60000f;

                    var afterMana = node.mana + manaChange;
                    node.mana = Mathf.Clamp(afterMana, 0f, manaCapacity);

                    var overgenerated = afterMana - manaCapacity;
                    if (overgenerated > 0)
                    {
                        // Log.Message($"{node.manaAcceptor} overgenerated:{overgenerated} totalOvergenerated:{totalOvergenerated} manaChange:{manaChange} afterMana:{afterMana} manaCapacity:{manaCapacity}");
                        totalOvergenerated += overgenerated;
                    }
                    else if (overgenerated < 0f)
                    {
                        _tempDistributeManaFluxNodeCandidateIndices.Add((i, -overgenerated));
                    }
                }

                var totalOvergeneratedDaily = Mathf.CeilToInt(totalOvergenerated * 60000 / tickInterval);
                var transferable = totalOvergenerated;
                if (_nodesList.Count > 1)
                {
                    int loopCount = 0;
                    while (transferable > 0f && _tempDistributeManaFluxNodeCandidateIndices.Count > 0 && loopCount < 10)
                    {
                        loopCount++;
                        var dividedMana = transferable / _tempDistributeManaFluxNodeCandidateIndices.Count;
                        if (dividedMana < 1f / 60000f) { break; }

                        for (int i = 0; i < _tempDistributeManaFluxNodeCandidateIndices.Count; ++i)
                        {
                            var t = _tempDistributeManaFluxNodeCandidateIndices[i];
                            var node = _nodesList[t.index];
                            if (dividedMana >= t.lackedMana)
                            {
                                // 균등 분배된 마나가 부족량보다 많은경우 최대치로 채우고 리스트에서 제거
                                transferable -= t.lackedMana;
                                node.mana += t.lackedMana;

                                _tempDistributeManaFluxNodeCandidateIndices.RemoveAt(i--);
                            }
                            else
                            {
                                // 균등 분배된 마나가 부족량보다 적은경우 분배된 양만큼만 채우고 다음 루프로
                                transferable -= dividedMana;
                                node.mana += dividedMana;

                                _tempDistributeManaFluxNodeCandidateIndices[i] = (t.index, t.lackedMana - dividedMana);
                            }
                        }
                    }

                    if (loopCount >= 10)
                    {
                        Log.Warning($"manaflux network calculation loopCount over 10; {totalOvergenerated} {string.Join(",", _tempDistributeManaFluxNodeCandidateIndices.Select(v => $"{_nodesList[v.index].manaAcceptor}_{v.lackedMana}"))}");
                    }
                }

                var exceeded = Mathf.CeilToInt(transferable * 60000 / tickInterval);
                if (tick - _lastRefreshHistoryTick > 3000)
                {
                    var history = new ManaFluxNetworkHistory()
                    {
                        tick = tick,
                        generated = totalGeneratedDaily,
                        consumed = totalConsumedDaily,
                        transfered = totalOvergeneratedDaily - exceeded,
                        exceeded = exceeded,
                    };

                    if (FluxHistory.Count > MaxHistoryLength) { FluxHistory.Dequeue(); }

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
            _lastRefreshTick = GenTicks.TicksGame;
        }
    }
}
