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

        public IEnumerable<Pawn> FairyficatedPawns => GameComponent_Mana.Of(Current.Game).GetFairyficatedPawns((Pawn)parent);
        public int FairyficatedPawnCount => GameComponent_Mana.Of(Current.Game)?.GetFairyficatedPawnCount((Pawn)parent) ?? 0;
        public Pawn Royal => (Pawn)parent;

        private List<ViviFairy> _activeFairies = new List<ViviFairy>();
        public IReadOnlyList<ViviFairy> ActiveFairies => _activeFairies;
        public int MaterializedCount => _activeFairies.Count;
        public int PendingMaterializeCount => _pendingMaterializeCount;
        // 기즈모가 매 프레임 부르므로 LINQ 반복기 없이 센다.
        public int AvailableCount
        {
            get
            {
                var count = 0;
                for (int i = 0; i < _activeFairies.Count; ++i)
                {
                    var f = _activeFairies[i];
                    if (f != null && !f.Destroyed && f.IsAvailable) { count++; }
                }
                return count;
            }
        }

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
            }
        }

        public override void CompTickInterval(int delta)
        {
            TickQueuedMaterialize(delta);
        }

        [Unsaved]
        private Gizmo_ViviFairyStatus _cachedFairyStatusGizmo;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (MaterializedCount == 0 && !DebugSettings.godMode)
            {
                return base.CompGetGizmosExtra();
            }

            return CompGetGizmosExtraIterator();
        }

        private IEnumerable<Gizmo> CompGetGizmosExtraIterator()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (MaterializedCount > 0)
            {
                if (_cachedFairyStatusGizmo == null)
                {
                    _cachedFairyStatusGizmo = new Gizmo_ViviFairyStatus(this);
                }
                yield return _cachedFairyStatusGizmo;
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

            DestroyAllFairies();

            // 사망(KillFinalize)이면 바로 뒤에 불리는 Notify_Killed가 요정 비비를 사망 처리한다. 여기서 등록을 풀면 그 목록이 비어 버린다.
            if (mode != DestroyMode.WillReplace && mode != DestroyMode.KillFinalize)
            {
                ReleaseFairyficatedPawns(kill: false, dinfo: null);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            if (mode != DestroyMode.WillReplace)
            {
                DestroyAllFairies();
            }
        }

        public override void Notify_Downed()
        {
            base.Notify_Downed();

            DestroyAllFairies();
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);

            DestroyAllFairies();
            ReleaseFairyficatedPawns(kill: true, dinfo);
        }

        // 로열이 사라질 때 귀속된 요정 비비를 함께 정리한다. 로열이 죽었으면 함께 죽이고, 그 밖의 소멸이면 조용히 함께 소멸시킨다.
        private void ReleaseFairyficatedPawns(bool kill, DamageInfo? dinfo)
        {
            var gameCompMana = GameComponent_Mana.Of(Current.Game);
            if (gameCompMana == null) { return; }

            // Kill의 사망 처리 훅이 등록 목록을 바꿀 수 있으므로 지연 열거로 순회하지 않는다.
            foreach (var fairyPawn in gameCompMana.GetFairyficatedPawns((Pawn)parent).ToList())
            {
                // 파괴된 폰은 월드 폰 목록이 저장하므로, 등록을 먼저 풀어 두 곳에 저장되지 않게 한다.
                gameCompMana.UnregisterFairy(fairyPawn);
                if (fairyPawn == null || fairyPawn.Destroyed) { continue; }

                try
                {
                    if (kill && !fairyPawn.Dead)
                    {
                        fairyPawn.Kill(dinfo);

                        // 맵에 없는 폰이 죽으면 어디에도 놓이지 않는 시체가 만들어진다.
                        var corpse = fairyPawn.Corpse;
                        if (corpse != null && !corpse.Spawned && !corpse.Destroyed)
                        {
                            corpse.Destroy();
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[ViViRace] failed to kill fairyficated vivi {fairyPawn.ToStringSafe()} of {parent.ToStringSafe()}: {ex}");
                }

                // 살아 있는 채로 월드에 넘기면 다른 세력으로 편입되므로, 파괴한 뒤 넘어가게 한다.
                if (!fairyPawn.Destroyed)
                {
                    fairyPawn.Destroy();
                }
            }

            gameCompMana.UnregisterAllFairy((Pawn)parent);
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            if (absorbed || !parent.Spawned || parent.Map == null) { return; }
            if (dinfo.Def == null || !dinfo.Def.harmsHealth) { return; }

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

            GameComponent_Mana.Of(Current.Game).RegisterFairy((Pawn)parent, vivi);

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
            var gameCompMana = GameComponent_Mana.Of(Current.Game);
            var vivi = gameCompMana.GetFairyficatedPawns((Pawn)parent).RandomElement();

            if (GenPlace.TryPlaceThing(vivi, parent.Position, parent.Map, ThingPlaceMode.Near))
            {
                gameCompMana.UnregisterFairy(vivi);

                Refresh();
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

        // 아래 두 조회는 선택 오버레이·기즈모·헤디프 주기 검사에서 자주 불리므로 ActiveJobs()와 같은 순서·조건의 for 루프로 찾는다.
        public bool HasActiveJob(int id)
        {
            for (int i = 0; i < _activeFairies.Count; ++i)
            {
                var f = _activeFairies[i];
                if (f == null || f.Destroyed) { continue; }

                var job = f.CurrentJob;
                if (job != null && job.id == id) { return true; }
            }
            return false;
        }

        public T GetActiveJob<T>() where T : FairyJob
        {
            for (int i = 0; i < _activeFairies.Count; ++i)
            {
                var f = _activeFairies[i];
                if (f == null || f.Destroyed) { continue; }

                if (f.CurrentJob is T job) { return job; }
            }
            return null;
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
                if (f != null && !f.Destroyed && f.CurrentJob?.Kind != FairyJobKind.Dematerialize)
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

        public void RefreshBondHediff()
        {
            Refresh();
        }

        private IEnumerable<FairyJob> ActiveJobs()
        {
            return _activeFairies
                .Where(f => f != null && !f.Destroyed)
                .Select(f => f.CurrentJob)
                .Where(j => j != null);
        }

        private void DestroyAllFairies()
        {
            CancelMaterializeQueue();
            // 작업을 먼저 끝내야 호위 헤디프처럼 작업이 남긴 상태가 정리된다.
            foreach (var job in ActiveJobs().ToList())
            {
                job.Interrupt(FairyJobInterruptReason.OwnerUnavailable);
            }

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
