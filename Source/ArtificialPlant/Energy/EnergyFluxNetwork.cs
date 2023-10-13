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
        public int NetworkHash { get; private set; }
        public int ShouldRegenerateNetworkTick { get; set; } = -1;

        private Dictionary<ArtificialPlant, EnergyFluxNetworkNode> _nodes { get; }
        private int _lastRefreshTick;

        private List<ArtificialPlant> _tempNodes = new List<ArtificialPlant>();

        public EnergyFluxNetwork()
        {
            _nodes = new Dictionary<ArtificialPlant, EnergyFluxNetworkNode>();

            NetworkHash = Rand.Int;
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
            _lastRefreshTick = 0;
        }

        public void RemovePlant(ArtificialPlant plant)
        {
            _nodes.Remove(plant);

            ShouldRegenerateNetworkTick = GenTicks.TicksGame + 1;
        }

        public void Tick()
        {
            var tick = GenTicks.TicksGame;
            if (tick > _lastRefreshTick)
            {
                float totalOvergeneratedEnergy = 0f;
                foreach (var kv in _nodes.Where(kv => kv.Value.nextRefreshTick <= tick))
                {
                    var plant = kv.Key;
                    var node = kv.Value;

                    var tickInterval = CalcTickInterval(plant);
                    var consumed = plant.ArtificialPlantModExtension.energyConsumeRule?.CalcEnergy(plant, tickInterval) ?? 0f;
                    var generated = plant.ArtificialPlantModExtension.energyGenerateRule?.CalcEnergy(plant, tickInterval) ?? 0f;

                    node.nextRefreshTick = tick + tickInterval;
                    node.energy = Mathf.Max(node.energy - consumed + generated, 0f);

                    var energyCapacity = plant.ArtificialPlantModExtension.energyCapacity;
                    if (node.energy > energyCapacity)
                    {
                        totalOvergeneratedEnergy += node.energy - energyCapacity;
                        node.energy = energyCapacity;
                    }
                }

                if (totalOvergeneratedEnergy > 0f)
                {
                    _tempNodes.Clear();
                    _tempNodes.AddRange(_nodes.Keys.Where(v => !v.IsFullEnergy));

                    if (_tempNodes.Count > 0)
                    {
                        var divided = totalOvergeneratedEnergy / _tempNodes.Count;
                        foreach (var plant in _tempNodes)
                        {
                            var node = _nodes[plant];
                            node.energy = Mathf.Clamp(node.energy + divided, 0f, plant.ArtificialPlantModExtension.energyCapacity);
                        }
                    }
                }

                _lastRefreshTick = tick;
            }
        }

        private int CalcTickInterval(ArtificialPlant plant)
        {
            switch (plant.def.tickerType)
            {
                case TickerType.Normal:
                    return 60;

                case TickerType.Rare:
                    return GenTicks.TickRareInterval;

                case TickerType.Long:
                    return GenTicks.TickLongInterval;

                default:
                    throw new ArgumentException($"invalid ticker type {plant.def.tickerType}");
            }
        }

        public void MergeNetworks(IEnumerable<EnergyFluxNetwork> networks)
        {
            if (networks.EnumerableNullOrEmpty()) { return; }

            foreach (var network in networks)
            {
                _nodes.AddRange(network._nodes);
                foreach (var node in network._nodes.Values)
                {
                    node.plant.EnergyFluxNetwork = this;
                }
            }

            _lastRefreshTick = 0;
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
