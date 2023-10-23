using Verse;

namespace VVRace
{
    public abstract class EnergyAcceptor : Building
    {
        public abstract EnergyFluxNetwork EnergyFluxNetwork { get; set; }
        public abstract EnergyFluxNetworkNode EnergyFluxNode { get; }

        protected EnergyFluxGrid EnergyFluxGrid { get; private set; }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            EnergyFluxGrid.Notify_SpawnEnergyAcceptor(this);
            EnergyFluxGrid = EnergyFluxGrid.GetFluxGrid(map);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            EnergyFluxGrid.Notify_DespawnEnergyAcceptor(this);
            EnergyFluxGrid = null;

            base.DeSpawn(mode);
        }

        public override void Tick()
        {
            if (Spawned && EnergyFluxGrid != null && EnergyFluxGrid.ShouldRefreshNetwork)
            {
                EnergyFluxGrid.RefreshNetworks();
            }

            base.Tick();
        }

        public override void TickRare()
        {
            if (Spawned && EnergyFluxGrid != null && EnergyFluxGrid.ShouldRefreshNetwork)
            {
                EnergyFluxGrid.RefreshNetworks();
            }

            base.TickRare();
        }
    }

}
