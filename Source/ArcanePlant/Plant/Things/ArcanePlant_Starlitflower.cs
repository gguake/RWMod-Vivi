using Verse;
using Verse.AI;

namespace VVRace
{
    public class ArcanePlant_Starlitflower : ArcanePlant
    {
        public const float VaccumResistRange = 3.4f;

        public int GraphicIndex => CompGlower.Glows ? 1 : 0;

        private CompManaGlower _compGlower;
        public CompManaGlower CompGlower
        {
            get
            {
                if (_compGlower == null)
                {
                    _compGlower = this.TryGetComp<CompManaGlower>();
                }

                return _compGlower;
            }
        }

        private bool? _lastCompGlowerState = null;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            _mapComponent.Notify_StalitflowerSpawned(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            _mapComponent.Notify_StalitflowerDespawned(this);
            base.DeSpawn(mode);
        }

        protected override void TickInterval(int delta)
        {
            if (!Spawned || Destroyed)
            {
                return;
            }

            if (_lastCompGlowerState != CompGlower.Glows)
            {
                _lastCompGlowerState = CompGlower.Glows;
                DirtyMapMesh(Map);
            }

            if (Map.Biome.inVacuum)
            {
                VVEffecterDefOf.VV_StalitFlowerAura.SpawnMaintained(Position, Map, VaccumResistRange);
            }
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (Spawned && Map.Biome.inVacuum)
            {
                GenDraw.DrawRadiusRing(Position, VaccumResistRange);
            }
        }

        public override int? OverrideGraphicIndex => CompGlower.Glows ? 1 : 0;
    }
}
