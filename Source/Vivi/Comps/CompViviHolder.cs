using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ViviHolder : CompProperties
    {
        public int maxCount;
        public int fairyLifespanTicks = 30000;

        public SimpleCurve preventDamageChanceByInnerCount;
        public EffecterDef preventDamageEffect;

        public CompProperties_ViviHolder()
        {
            compClass = typeof(CompViviHolder);
        }
    }

    public class CompViviHolder : ThingComp
    {
        private const int AutoMaterializeIntervalTicks = 15;
        private const float MaterializeSpawnRadius = 2f;

        public CompProperties_ViviHolder Props => (CompProperties_ViviHolder)props;

        public bool CanJoin => FairyficatedPawnCount < Props.maxCount;

        public IEnumerable<Pawn> FairyficatedPawns => Current.Game.GetComponent<GameComponent_Mana>().GetFairyficatedPawns((Pawn)parent);
        public int FairyficatedPawnCount => FairyficatedPawns.Count();
        public Pawn Royal => (Pawn)parent;

        private List<ViviFairy> _activeFairies = new List<ViviFairy>();
        public IReadOnlyList<ViviFairy> ActiveFairies => _activeFairies;
        public int MaterializedCount => _activeFairies.Count;
        public int PendingMaterializeCount => _pendingMaterializeCount;
        public int AvailableCount => _activeFairies.Count(f => f != null && !f.Destroyed && f.IsAvailable);

        private int _nextFairyJobId = 1;
        public int NextFairyJobId() => _nextFairyJobId++;
        private int _pendingMaterializeCount;
        private int _ticksUntilNextMaterialize;

        public CompViviHolder()
        {
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            ThingOwner<Pawn> innerContainer = null;
            Scribe_Deep.Look(ref innerContainer, "innerViviContainer", new object[] { this });
            Scribe_Collections.Look(ref _activeFairies, "activeFairies", LookMode.Reference);
            Scribe_Values.Look(ref _nextFairyJobId, "nextFairyJobId", 1);
            Scribe_Values.Look(ref _pendingMaterializeCount, "pendingMaterializeCount");
            Scribe_Values.Look(ref _ticksUntilNextMaterialize, "ticksUntilNextMaterialize");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_activeFairies == null)
                {
                    _activeFairies = new List<ViviFairy>();
                }
                else
                {
                    _activeFairies.RemoveAll(f => f == null || f.Destroyed);
                }

                foreach (var fairy in _activeFairies)
                {
                    fairy?.EnsureJob();
                }
            }
        }

        public override void CompTickInterval(int delta)
        {
            TickQueuedMaterialize(delta);
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

            if (!DebugSettings.godMode || !(parent is Pawn pawn) || !pawn.IsRoyalVivi())
            {
                yield break;
            }

            var command = new Command_Action
            {
                defaultLabel = "DEV: Add random fairyficated Vivi",
                action = () => AddRandomFairyficatedVivi(pawn)
            };

            if (!CanJoin)
            {
                command.Disable($"Fairyfication stage is already at max ({Props.maxCount}).");
            }

            yield return command;
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();

            if (parent.Spawned && parent.Map != null && GetActiveJob<FairyJob_Expansion>() != null)
            {
                GenDraw.DrawRadiusRing(parent.Position, FairyJob_Expansion.ExpansionRadius);
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!respawningAfterLoad)
            {
                if (parent.TryGetComp<CompVivi>(out var compVivi) && compVivi.AttunementActive)
                {
                    Refresh();
                }
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            CancelMaterializeQueue();
            foreach (var job in ActiveJobs().ToList())
            {
                job.Interrupt(FairyJobInterruptReason.OwnerUnavailable);
            }

            DestroyAllActiveFairies();

            if (mode != DestroyMode.WillReplace)
            {
                Current.Game.GetComponent<GameComponent_Mana>().UnregisterAllFairy((Pawn)parent);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            if (mode != DestroyMode.WillReplace)
            {
                CancelMaterializeQueue();
                foreach (var job in ActiveJobs().ToList())
                {
                    job.Interrupt(FairyJobInterruptReason.OwnerUnavailable);
                }

                DestroyAllActiveFairies();
            }
        }

        public override void Notify_Downed()
        {
            base.Notify_Downed();

            CancelMaterializeQueue();
            foreach (var job in ActiveJobs().ToList())
            {
                job.Interrupt(FairyJobInterruptReason.OwnerUnavailable);
            }

            DestroyAllActiveFairies();
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);

            CancelMaterializeQueue();
            foreach (var job in ActiveJobs().ToList())
            {
                job.Interrupt(FairyJobInterruptReason.OwnerUnavailable);
            }
            DestroyAllActiveFairies();

            var gameCompMana = Current.Game.GetComponent<GameComponent_Mana>();
            foreach (var fairyPawn in gameCompMana.GetFairyficatedPawns((Pawn)parent))
            {
                fairyPawn.Kill(dinfo);
            }

            gameCompMana.UnregisterAllFairy((Pawn)parent);
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            if (Props.preventDamageChanceByInnerCount != null && Rand.Chance(Props.preventDamageChanceByInnerCount.Evaluate(FairyficatedPawnCount)))
            {
                if (Props.preventDamageEffect != null)
                {
                    Props.preventDamageEffect.SpawnAttached(parent, parent.Map);
                }

                MoteMaker.ThrowText(
                    new Vector3(parent.Position.x + 1f, parent.Position.y, parent.Position.z + 1f), 
                    parent.Map, 
                    LocalizeString_Etc.VV_TextMote_DamageBlockedByFairy.Translate());

                absorbed = true;
            }
        }

        public void JoinVivi(Pawn vivi)
        {
            if (!vivi.IsVivi()) { return; }

            if (vivi.Spawned)
            {
                vivi.jobs.StopAll();
                vivi.DeSpawnOrDeselect(DestroyMode.Vanish);
            }

            Current.Game.GetComponent<GameComponent_Mana>().RegisterFairy((Pawn)parent, vivi);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_FairyficationCompleteLabel.Translate(vivi.Named("TARGET")),
                LocalizeString_Letter.VV_Letter_FairyficationComplete.Translate(vivi.Named("TARGET"), parent.Named("CASTER")),
                LetterDefOf.NeutralEvent,
                parent);

            Refresh();
        }

        private void AddRandomFairyficatedVivi(Pawn royal)
        {
            if (royal == null || !royal.IsRoyalVivi() || !CanJoin) { return; }

            var request = new PawnGenerationRequest(
                VVPawnKindDefOf.VV_PlayerVivi,
                faction: royal.Faction ?? Faction.OfPlayer,
                allowDowned: true,
                forceGenerateNewPawn: true,
                developmentalStages: DevelopmentalStage.Adult,
                forcedXenotype: VVXenotypeDefOf.VV_Vivi);

            var vivi = PawnGenerator.GeneratePawn(request);
            vivi.relations?.AddDirectRelation(PawnRelationDefOf.Parent, royal);
            JoinVivi(vivi);
        }

        public Pawn DetachVivi()
        {
            var gameCompMana = Current.Game.GetComponent<GameComponent_Mana>();
            var vivi = gameCompMana.GetFairyficatedPawns((Pawn)parent).RandomElement();

            if (GenPlace.TryPlaceThing(vivi, parent.Position, parent.Map, ThingPlaceMode.Near))
            {
                gameCompMana.UnregisterFairy(vivi);
                return (Pawn)vivi;
            }

            return null;
        }

        public bool TryQueueMaterializeAllAvailable()
        {
            if (!CanMaterializeFairy()) { return false; }

            int count = FairyficatedPawnCount - MaterializedCount - _pendingMaterializeCount;
            if (count <= 0) { return false; }

            bool wasIdle = _pendingMaterializeCount <= 0;
            _pendingMaterializeCount += count;
            if (wasIdle)
            {
                _ticksUntilNextMaterialize = 0;
                TickQueuedMaterialize(0);
            }

            return true;
        }

        public void CancelMaterializeQueue()
        {
            _pendingMaterializeCount = 0;
            _ticksUntilNextMaterialize = 0;
        }

        private void TickQueuedMaterialize(int delta)
        {
            if (_pendingMaterializeCount <= 0) { return; }
            if (!CanMaterializeFairy())
            {
                CancelMaterializeQueue();
                return;
            }

            _ticksUntilNextMaterialize -= delta;
            while (_pendingMaterializeCount > 0 && _ticksUntilNextMaterialize <= 0)
            {
                if (FairyficatedPawnCount <= MaterializedCount)
                {
                    CancelMaterializeQueue();
                    return;
                }

                if (MaterializeFairy() == null)
                {
                    CancelMaterializeQueue();
                    return;
                }

                _pendingMaterializeCount--;
                _ticksUntilNextMaterialize += AutoMaterializeIntervalTicks;
            }

            if (_pendingMaterializeCount <= 0)
            {
                _ticksUntilNextMaterialize = 0;
            }
        }

        private bool CanMaterializeFairy()
        {
            var pawn = (Pawn)parent;
            return pawn.Spawned && !pawn.Dead && pawn.Map != null && pawn.health?.hediffSet.HasHediff(VVHediffDefOf.VV_FairyMastery) == true;
        }

        public ViviFairy MaterializeFairy()
        {
            var pawn = (Pawn)parent;
            if (pawn.Map == null) { return null; }

            var fairyDef = VVThingDefOf.VV_ViviFairy;
            var fairy = (ViviFairy)ThingMaker.MakeThing(fairyDef);
            fairy.Initialize(pawn, Props.fairyLifespanTicks);
            GenSpawn.Spawn(fairy, RandomMaterializeCellNear(pawn), pawn.Map);

            RegisterFairy(fairy);
            fairy.StartJob(new FairyJob_Materialize(fairy.Owner));
            return fairy;
        }

        private IntVec3 RandomMaterializeCellNear(Pawn pawn)
        {
            var map = pawn.Map;
            if (map == null)
            {
                return pawn.Position;
            }

            var candidates = GenRadial.RadialCellsAround(pawn.Position, MaterializeSpawnRadius, useCenter: false)
                .Where(c => IsValidMaterializeCell(c, map))
                .ToList();

            return candidates.Count > 0 ? candidates.RandomElement() : pawn.Position;
        }

        private static bool IsValidMaterializeCell(IntVec3 cell, Map map)
        {
            return cell.InBounds(map) &&
                cell.Standable(map) &&
                cell.GetFirstPawn(map) == null &&
                cell.GetFirstBuilding(map) == null;
        }

        public void RegisterFairy(ViviFairy fairy)
        {
            if (fairy == null) { return; }
            if (!_activeFairies.Contains(fairy))
            {
                _activeFairies.Add(fairy);
            }

            fairy.EnsureJob();
            _nextFairyJobId = Mathf.Max(_nextFairyJobId, fairy.CurrentJob.id + 1);
        }

        public void Notify_FairyDestroyed(ViviFairy fairy)
        {
            _activeFairies.Remove(fairy);
        }

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

        public bool TryReserveIdleFairies(int count, out List<ViviFairy> reserved)
        {
            reserved = _activeFairies
                .Where(f => f != null && !f.Destroyed && f.IsAvailable)
                .OrderBy(f => f.thingIDNumber)
                .Take(count)
                .ToList();

            if (reserved.Count >= count)
            {
                return true;
            }

            reserved = null;
            return false;
        }

        public void DematerializeAll()
        {
            CancelMaterializeQueue();
            foreach (var job in ActiveJobs().Where(j => j.Kind != FairyJobKind.Dematerialize).ToList())
            {
                job.Interrupt(FairyJobInterruptReason.DematerializeAll);
            }
            foreach (var f in _activeFairies.ToList())
            {
                if (f != null && !f.Destroyed && f.CurrentJob.Kind != FairyJobKind.Dematerialize)
                {
                    f.BeginDematerialize(false);
                }
            }
        }

        public void Notify_EverflowerLinked()
        {
            Refresh();
        }

        public void Notify_EverflowerUnlinked()
        {
            Refresh();
        }

        private IEnumerable<FairyJob> ActiveJobs()
        {
            return _activeFairies
                .Where(f => f != null && !f.Destroyed)
                .Select(f => f.CurrentJob)
                .Where(j => j != null && !j.Ended);
        }

        private void DestroyAllActiveFairies()
        {
            CancelMaterializeQueue();
            foreach (var f in _activeFairies.ToList())
            {
                if (f != null && !f.Destroyed)
                {
                    f.Destroy();
                }
            }
            _activeFairies.Clear();
        }

        private void Refresh()
        {
            var pawn = (Pawn)parent;
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_ViviFairyFollow);
            var severity = FairyficatedPawnCount;
            if (severity > 0)
            {
                if (hediff == null)
                {
                    hediff = pawn.health.AddHediff(VVHediffDefOf.VV_ViviFairyFollow);
                }

                hediff.Severity = severity;
            }
            else
            {
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }
    }
}
