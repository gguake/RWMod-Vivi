using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class RichFlower : ArtificialPlant
    {
        private int _nextTerraformTick = -1;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                _nextTerraformTick = GenTicks.TicksGame + ArtificialPlantModExtension.terraformingTick;
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);

            _nextTerraformTick = -1;
        }

        public override void Tick()
        {
            base.Tick();

            if (Spawned)
            {
                if (this.IsHashIntervalTick(60))
                {
                    if (_nextTerraformTick >= 0 && GenTicks.TicksGame > _nextTerraformTick && EnergyChargeRatio > 0f)
                    {
                        TerraformNear();
                    }
                }
            }
        }

        public override void TickRare()
        {
            base.TickRare();

            if (Spawned)
            {
                if (_nextTerraformTick >= 0 && GenTicks.TicksGame > _nextTerraformTick && EnergyChargeRatio > 0f)
                {
                    TerraformNear();
                }
            }
        }

        public override void TickLong()
        {
            base.TickLong();

            if (Spawned)
            {
                if (_nextTerraformTick >= 0 && GenTicks.TicksGame > _nextTerraformTick && EnergyChargeRatio > 0f)
                {
                    TerraformNear();
                }
            }
        }

        private void TerraformNear()
        {
            var cells = GenRadial.RadialCellsAround(Position, 1.8f, true).OrderBy(v => v.IsPolluted(Map) ? -1f : v.GetFertility(Map));
            foreach (var cell in cells)
            {
                if (cell.InBounds(Map) && TryTerraformCell(cell))
                {
                    break;
                }
            }

            _nextTerraformTick = GenTicks.TicksGame + ArtificialPlantModExtension.terraformingTick;
        }

        private bool TryTerraformCell(IntVec3 cell)
        {
            if (cell.IsPolluted(Map))
            {
                cell.Unpollute(Map);
                return true;
            }

            var terrain = cell.GetTerrain(Map);
            if (terrain == TerrainDefOf.Sand)
            {
                Map.terrainGrid.SetTerrain(cell, TerrainDefOf.Gravel);
                return true;
            }
            if (terrain == TerrainDefOf.Gravel)
            {
                Map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                return true;
            }

            return false;
        }
    }
}
