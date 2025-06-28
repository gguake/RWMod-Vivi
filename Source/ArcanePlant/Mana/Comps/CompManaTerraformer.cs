using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class TerraformData
    {
        public TerrainDef from;
        public TerrainDef to;
    }

    public class CompProperties_ManaTerraformer : CompProperties
    {
        [Unsaved]
        private Dictionary<TerrainDef, TerrainDef> _cachedDataDictionary;

        public List<TerraformData> terraforms = new List<TerraformData>();
        public IntRange cooldownTicks;
        public FloatRange radiusRange;
        public float radiusGrowth;

        public bool removePollution;
        public bool showRadius = true;
        
        public CompProperties_ManaTerraformer()
        {
            compClass = typeof(CompManaTerraformer);
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

    public class CompManaTerraformer : ThingComp
    {
        public CompProperties_ManaTerraformer Props => (CompProperties_ManaTerraformer)props;

        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null) { _manaComp = parent.GetComp<CompMana>(); }
                return _manaComp;
            }
        }
        private CompMana _manaComp;

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

        public override void CompTickInterval(int delta)
        {
            if (!parent.Spawned || parent.Destroyed || !ManaComp.Active) { return; }

            if (_remainedCooldown > 0)
            {
                _remainedCooldown -= delta;
            }
            else
            {
                TryTerraform();
                _remainedCooldown = Props.cooldownTicks.RandomInRange;
            }
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            if (Props.showRadius)
            {
                GenDraw.DrawRadiusRing(parent.Position, Props.radiusRange.TrueMax);
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
