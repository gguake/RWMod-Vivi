using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Noise;

namespace VVRace
{
    public class CompProperties_ViviFairyController : CompProperties
    {
        public ThingDef fairyDef;
        public int fairyLifespanTicks = 30000;

        public float orbitRadiusX = 0.6f;
        public float orbitRadiusZ = 0.3f;
        public float orbitDepth = 0.06f;
        public float orbitAngularSpeed = 0.008f;

        public CompProperties_ViviFairyController()
        {
            compClass = typeof(CompViviFairyController);
        }
    }

    public class CompViviFairyController : ThingComp
    {
        public CompProperties_ViviFairyController Props => (CompProperties_ViviFairyController)props;

        private List<ViviFairy> _activeFairies = new List<ViviFairy>();
        private int _nextJobId = 1;

        [Unsaved]
        private int _lastFairyDrivenTick = -1;

        public Pawn Royal => (Pawn)parent;
        public IReadOnlyList<ViviFairy> ActiveFairies => _activeFairies;

        public int MaterializedCount => _activeFairies.Count;

        public int FairyPoolCount
        {
            get
            {
                var holder = parent.GetComp<CompViviHolder>();
                return holder != null ? holder.FairyficatedPawnCount : 0;
            }
        }

        public int AvailableCount => _activeFairies.Count(f => f != null && !f.Destroyed && f.IsAvailable);

        public ViviFairy MaterializeFairyAt(IntVec3 cell)
        {
            var pawn = (Pawn)parent;
            if (pawn.Map == null) { return null; }

            var fairy = (ViviFairy)ThingMaker.MakeThing(Props.fairyDef);
            fairy.Initialize(pawn, Props.fairyLifespanTicks);
            GenSpawn.Spawn(fairy, cell, pawn.Map);

            RegisterFairy(fairy);
            fairy.StartJob(new FairyJob_Materialize(fairy.Owner));
            return fairy;
        }

        public void RegisterFairy(ViviFairy fairy)
        {
            if (fairy == null) { return; }
            if (!_activeFairies.Contains(fairy))
            {
                _activeFairies.Add(fairy);
            }
            fairy.EnsureJob();
        }

        public void Notify_FairyGone(ViviFairy fairy)
        {
            _activeFairies.Remove(fairy);
        }

        public int NextJobId() => _nextJobId++;

        public void InterruptJob(int id, FairyJobInterruptReason reason)
        {
            foreach (var job in ActiveJobs().Where(j => j.id == id).ToList())
            {
                job.Interrupt(reason);
            }
        }

        public T GetActiveJob<T>() where T : FairyJob
        {
            return ActiveJobs().OfType<T>().FirstOrDefault(j => !j.Ended);
        }

        public bool HasActiveJobInGroup(int id, FairyJobKind kind, FairyJob except = null)
        {
            return ActiveJobs().Any(j => j != null && j != except && !j.Ended && j.id == id && j.Kind == kind);
        }

        public bool TryReserveIdleFairies(int n, out List<ViviFairy> reserved)
        {
            reserved = _activeFairies
                .Where(f => f != null && !f.Destroyed && f.IsAvailable)
                .OrderBy(f => f.thingIDNumber)
                .Take(n)
                .ToList();

            if (reserved.Count >= n)
            {
                return true;
            }

            reserved = null;
            return false;
        }

        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            PruneActiveFairies();
        }

        public void Notify_FairyTick(ViviFairy fairy)
        {
            if (fairy == null || fairy.Destroyed || !_activeFairies.Contains(fairy)) { return; }

            int tick = GenTicks.TicksGame;
            if (_lastFairyDrivenTick == tick) { return; }
            _lastFairyDrivenTick = tick;

            PruneActiveFairies();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (MaterializedCount > 0)
            {
                yield return new Gizmo_ViviFairyStatus(this);
            }
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            foreach (var job in ActiveJobs().ToList())
            {
                job.Interrupt(FairyJobInterruptReason.OwnerUnavailable);
            }
            DestroyAllFairiesImmediate();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            if (mode != DestroyMode.WillReplace)
            {
                foreach (var job in ActiveJobs().ToList())
                {
                    job.Interrupt(FairyJobInterruptReason.OwnerUnavailable);
                }
                DestroyAllFairiesImmediate();
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            foreach (var job in ActiveJobs().ToList())
            {
                job.Interrupt(FairyJobInterruptReason.OwnerUnavailable);
            }
            DestroyAllFairiesImmediate();
        }

        public void DematerializeAll()
        {
            foreach (var job in ActiveJobs().ToList())
            {
                job.Interrupt(FairyJobInterruptReason.DematerializeAll);
            }
            foreach (var f in _activeFairies.ToList())
            {
                if (f != null && !f.Destroyed)
                {
                    f.BeginDematerialize(false);
                }
            }
        }

        private IEnumerable<FairyJob> ActiveJobs()
        {
            return _activeFairies
                .Where(f => f != null && !f.Destroyed)
                .Select(f => f.CurrentJob)
                .Where(j => j != null && !j.Ended);
        }

        private void PruneActiveFairies()
        {
            for (int i = _activeFairies.Count - 1; i >= 0; i--)
            {
                var f = _activeFairies[i];
                if (f == null || f.Destroyed || !f.Spawned)
                {
                    _activeFairies.RemoveAt(i);
                }
            }
        }

        private void DestroyAllFairiesImmediate()
        {
            foreach (var f in _activeFairies.ToList())
            {
                if (f != null && !f.Destroyed)
                {
                    f.Destroy();
                }
            }
            _activeFairies.Clear();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Collections.Look(ref _activeFairies, "activeFairies", LookMode.Reference);
            Scribe_Values.Look(ref _nextJobId, "nextSessionId", 1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_activeFairies == null) { _activeFairies = new List<ViviFairy>(); }
                _activeFairies.RemoveAll(f => f == null || f.Destroyed);
                foreach (var fairy in _activeFairies)
                {
                    fairy?.EnsureJob();
                }
            }
        }
    }
}
