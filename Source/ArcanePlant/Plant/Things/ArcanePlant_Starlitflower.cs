using RimWorld;
using Verse;

namespace VVRace
{
    public class ArcanePlant_Starlitflower : ArcanePlant
    {
        public const float VaccumResistRange = 5.7f;

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

        private Effecter _curEffecter;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            _mapComponent.Notify_StalitflowerSpawned(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (_curEffecter != null)
            {
                _curEffecter.ForceEnd();
                _curEffecter = null;
            }

            _mapComponent.Notify_StalitflowerDespawned(this);
            base.DeSpawn(mode);
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if (!Spawned || Destroyed)
            {
                return;
            }

            CompGlower.UpdateLit(Map);

            if (this.IsHashIntervalTick(450, delta))
            {
                if (Map.Tile.LayerDef == PlanetLayerDefOf.Orbit)
                {
                    _curEffecter = VVEffecterDefOf.VV_StalitFlowerAura.SpawnMaintained(Position, Map, VaccumResistRange);
                }
            }
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (Spawned && Map.Tile.LayerDef == PlanetLayerDefOf.Orbit)
            {
                GenDraw.DrawRadiusRing(Position, VaccumResistRange);
            }
        }

        public override int? OverrideGraphicIndex => CompGlower.Glows ? 1 : 0;
    }
}
