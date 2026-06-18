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
        public CompProperties_ViviHolder Props => (CompProperties_ViviHolder)props;

        private List<ViviFairy> _activeFairies = new List<ViviFairy>();
        private int _nextJobId = 1;

        public bool CanJoin => FairyficatedPawnCount < Props.maxCount;

        public IEnumerable<Pawn> FairyficatedPawns => Current.Game.GetComponent<GameComponent_Mana>().GetFairyficatedPawns((Pawn)parent);
        public int FairyficatedPawnCount => FairyficatedPawns.Count();
        public Pawn Royal => (Pawn)parent;

        public IReadOnlyList<ViviFairy> ActiveFairies => _activeFairies;
        public int MaterializedCount => _activeFairies.Count;
        public int AvailableCount => _activeFairies.Count(f => f != null && !f.Destroyed && f.IsAvailable);

        public CompViviHolder()
        {
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            ThingOwner<Pawn> innerContainer = null;
            Scribe_Deep.Look(ref innerContainer, "innerViviContainer", new object[] { this });
            Scribe_Collections.Look(ref _activeFairies, "activeFairies", LookMode.Reference);
            Scribe_Values.Look(ref _nextJobId, "nextJobId", 1);

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

            foreach (var job in ActiveJobs().ToList())
            {
                job.Interrupt(FairyJobInterruptReason.OwnerUnavailable);
            }

            DestroyAllFairiesImmediate();

            if (mode != DestroyMode.WillReplace)
            {
                Current.Game.GetComponent<GameComponent_Mana>().UnregisterAllFairyByLinker((Pawn)parent);
            }
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

        public override void Notify_Downed()
        {
            base.Notify_Downed();

            foreach (var job in ActiveJobs().ToList())
            {
                job.Interrupt(FairyJobInterruptReason.OwnerUnavailable);
            }

            DestroyAllFairiesImmediate();
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);

            foreach (var job in ActiveJobs().ToList())
            {
                job.Interrupt(FairyJobInterruptReason.OwnerUnavailable);
            }
            DestroyAllFairiesImmediate();

            var gameCompMana = Current.Game.GetComponent<GameComponent_Mana>();
            foreach (var fairyPawn in gameCompMana.GetFairyficatedPawns((Pawn)parent))
            {
                fairyPawn.Kill(dinfo);
            }

            gameCompMana.UnregisterAllFairyByLinker((Pawn)parent);
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

        public ViviFairy MaterializeFairyAt(IntVec3 cell)
        {
            var pawn = (Pawn)parent;
            if (pawn.Map == null) { return null; }

            var fairyDef = VVThingDefOf.VV_ViviFairy;
            var fairy = (ViviFairy)ThingMaker.MakeThing(fairyDef);
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
            _nextJobId = Mathf.Max(_nextJobId, fairy.CurrentJob.id + 1);
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

        public void RefreshFairyMastery()
        {
            var pawn = (Pawn)parent;
            if (pawn.health == null) { return; }

            var compVivi = parent.GetComp<CompVivi>();
            var linkedEverflower = compVivi?.LinkedEverflower;
            var existing = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyMastery);
            bool shouldHave = pawn.IsRoyalVivi() && linkedEverflower != null && linkedEverflower.EverflowerComp.AttunementLevel >= 4;

            if (shouldHave)
            {
                if (existing == null)
                {
                    pawn.health.AddHediff(VVHediffDefOf.VV_FairyMastery);
                }
                return;
            }

            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }

            DematerializeAll();
        }

        public void Notify_EverflowerLinked()
        {
            Refresh();
            RefreshFairyMastery();
        }

        public void Notify_EverflowerUnlinked()
        {
            Refresh();
            RefreshFairyMastery();
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
