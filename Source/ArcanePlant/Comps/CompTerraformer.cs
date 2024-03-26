using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class TerraformData
    {
        public TerrainDef from;
        public TerrainDef to;
    }

    public class CompProperties_Terraformer : CompProperties
    {
        [Unsaved]
        private Dictionary<TerrainDef, TerrainDef> _cachedDataDictionary;

        public List<TerraformData> terraforms = new List<TerraformData>();
        public IntRange cooldownTicks;
        public float radius;
        public bool removePollution;
        public bool showRadius = true;

        public CompProperties_Terraformer()
        {
            compClass = typeof(CompTerraformer);
        }

        public TerrainDef TryGetTerraformResult(TerrainDef current)
        {
            if (_cachedDataDictionary == null)
            {
                _cachedDataDictionary = new Dictionary<TerrainDef, TerrainDef>();
                
                foreach (var v in terraforms)
                {
                    _cachedDataDictionary.Add(v.from, v.to);
                }
            }

            return _cachedDataDictionary.TryGetValue(current, out var result) ? result : null;
        }
    }

    public class CompTerraformer : ThingComp
    {
        public CompProperties_Terraformer Props => (CompProperties_Terraformer)props;

        private int _remainedCooldown = -1;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref _remainedCooldown, "remainedCooldown", defaultValue: -1);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!respawningAfterLoad && _remainedCooldown < 0)
            {
                _remainedCooldown = Props.cooldownTicks.RandomInRange;
            }
        }

        public override void PostDeSpawn(Map map)
        {
            _remainedCooldown = -1;
        }

        public override void CompTick()
        {
            if (parent.IsHashIntervalTick(60))
            {
                Tick(60);
            }
        }

        public override void CompTickRare()
        {
            Tick(GenTicks.TickRareInterval);
        }

        public override void CompTickLong()
        {
            Tick(GenTicks.TickLongInterval);
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            if (Props.showRadius)
            {
                GenDraw.DrawRadiusRing(parent.Position, Props.radius);
            }
        }

        public void Tick(int ticks = 1)
        {
            bool hasMana = false;
            var plant = parent as ArcanePlant;
            if (plant != null)
            {
                hasMana = plant.ManaChargeRatio > 0f;
            }

            if (hasMana)
            {
                if (_remainedCooldown > 0)
                {
                    _remainedCooldown -= ticks;
                }
                else
                {
                    TerraformNear();
                    _remainedCooldown = Props.cooldownTicks.RandomInRange;
                }
            }
        }

        private void TerraformNear()
        {
            var map = parent.Map;

            foreach (var cell in GenRadial.RadialPatternInRadius(Props.radius))
            {
                var position = parent.Position + cell;
                if (position.InBounds(map) && !position.Impassable(map) && TryTerraformCell(map, position))
                {
                    break;
                }
            }
        }

        private bool TryTerraformCell(Map map, IntVec3 cell)
        {
            if (Props.removePollution && cell.IsPolluted(map))
            {
                cell.Unpollute(map);
                return true;
            }
            else if (Props.terraforms?.Count > 0)
            {
                var terrain = cell.GetTerrain(map);
                var terraformed = Props.TryGetTerraformResult(terrain);
                if (terraformed != null)
                {
                    map.terrainGrid.SetTerrain(cell, terraformed);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }
    }
}
