using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class ApocryphaFairySwarm : IExposable
    {
        private const int LifespanTicks = int.MaxValue / 2;
        private const int SpawnRadius = 2;

        private readonly CompApocrypha _comp;

        private List<ViviFairy> _fairies = new List<ViviFairy>();

        private Pawn Pawn => _comp.Pawn;

        public IReadOnlyList<ViviFairy> Fairies => _fairies;

        public ApocryphaFairySwarm(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _fairies.RemoveAll(v => v == null || v.Destroyed);
            }

            Scribe_Collections.Look(ref _fairies, "fairies", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _fairies ??= new List<ViviFairy>();
                _fairies.RemoveAll(v => v == null);
            }
        }

        public void EnsureSummoned()
        {
            _fairies.RemoveAll(v => v == null || v.Destroyed || v.State == FairyState.Dematerializing);
            if (!Pawn.Spawned) { return; }

            var map = Pawn.Map;
            for (int i = _fairies.Count; i < _comp.Props.swarmFairyCount; i++)
            {
                var fairy = (ViviFairy)ThingMaker.MakeThing(VVThingDefOf.VV_ViviFairy);
                fairy.Initialize(Pawn, LifespanTicks);
                if (!CellFinder.TryFindRandomCellNear(Pawn.Position, map, SpawnRadius, c => c.Standable(map), out var cell))
                {
                    cell = Pawn.Position;
                }

                GenSpawn.Spawn(fairy, cell, map);
                fairy.StartJob(new FairyJob_ApocryphaSwarm(Pawn));
                _fairies.Add(fairy);
            }
        }

        public void Disperse()
        {
            for (int i = 0; i < _fairies.Count; i++)
            {
                if (_fairies[i] != null && !_fairies[i].Destroyed)
                {
                    _fairies[i].BeginDematerialize(applyAssimilation: false);
                }
            }

            _fairies.Clear();
        }
    }
}
