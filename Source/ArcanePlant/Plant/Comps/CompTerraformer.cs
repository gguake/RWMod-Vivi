using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Noise;

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
        public FloatRange radiusRange;
        public float radiusGrowth;

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

        private float _terraformRadius;
        private int _remainedCooldown = -1;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref _terraformRadius, "terraformRadius", defaultValue: Props.radiusRange.min);
            Scribe_Values.Look(ref _remainedCooldown, "remainedCooldown", defaultValue: -1);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!respawningAfterLoad)
            {
                _terraformRadius = Props.radiusRange.min;
                _remainedCooldown = Props.cooldownTicks.RandomInRange;
            }
        }

        public override void CompTick()
        {
            if (parent.IsHashIntervalTick(GenTicks.TickRareInterval))
            {
                Tick(GenTicks.TickRareInterval);
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
                GenDraw.DrawRadiusRing(parent.Position, Props.radiusRange.TrueMax);
            }
        }

        public void Tick(int ticks = 1)
        {
            if (!parent.Spawned || parent.Destroyed) { return; }

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
                    TryTerraform();
                    _remainedCooldown = Props.cooldownTicks.RandomInRange;
                }
            }
        }

        private void TryTerraform()
        {
            var map = parent.Map;

            _terraformRadius = Props.radiusRange.ClampToRange(_terraformRadius + Props.radiusGrowth);
            foreach (var cell in GenRadial.RadialPatternInRadius(_terraformRadius))
            {
                var position = parent.Position + cell;
                if (position.InBounds(map) && !position.Impassable(map))
                {
                    TryTerraformCell(map, position);
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
