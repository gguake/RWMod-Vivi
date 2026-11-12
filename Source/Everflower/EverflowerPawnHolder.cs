using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    // 영원꽃이 잠시 품었다가 돌려보내는 폰. 결속 해방된 요정 비비와 죽음 대신 안치되는 여제를 맡는다.
    // 꽃이 파괴되면 품은 폰은 시체도 남기지 않고 함께 죽고, 맵이 사라지면 바닐라가 맵 안의 다른 폰처럼 실종 처리한다.
    public class EverflowerPawnHolder : IExposable
    {
        public class HeldPawn : IExposable
        {
            public Pawn pawn;
            public int returnTick;
            public bool rebirth;

            public void ExposeData()
            {
                Scribe_References.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref returnTick, "returnTick", 0);
                Scribe_Values.Look(ref rebirth, "rebirth", false);
            }
        }

        // 다음 영원꽃 틱에 품을 폰. rebirth면 죽음 대신 안치되는 여제다.
        public class PendingHold : IExposable
        {
            public Pawn pawn;
            public int ticks;
            public bool rebirth;

            public void ExposeData()
            {
                Scribe_References.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref ticks, "ticks", 0);
                Scribe_Values.Look(ref rebirth, "rebirth", false);
            }
        }

        public const int RebirthTicks = 60000;

        private const float ReturnCellSearchRadius = 8f;
        private const float RebirthCellSearchRadius = 6f;
        private const int RetryTicks = 2500;

        private readonly ArcanePlant_Everflower _everflower;

        // 꽃이 파괴될 때 품은 폰의 시체가 같은 용기에 들어와야 직접 없앨 수 있으므로 Pawn 전용으로 두지 않는다.
        private ThingOwner<Thing> _container;
        private List<HeldPawn> _heldPawns = new List<HeldPawn>();

        // 다음 영원꽃 틱에 처리하는 대기열. 그사이 저장될 수 있으므로 함께 저장한다.
        // 일시정지 중 개발자 기즈모로 추가한 항목도 다음 틱에 처리한다.
        private List<PendingHold> _pendingHolds = new List<PendingHold>();

        // 회복 중인 폰. 회복 도중의 건강 판정이 부르는 죽음을 넘기는 데 쓴다.
        private static Pawn _restoringHealth;
        private static readonly List<Hediff> _tmpHediffs = new List<Hediff>();

        public ThingOwner Container => _container;

        public bool CanRebirth => !_everflower.Destroyed && _everflower.Spawned && _everflower.AttunementLevel >= CompEverflower.MaxAttunementLevel;

        public EverflowerPawnHolder(ArcanePlant_Everflower everflower)
        {
            _everflower = everflower;
            _container = new ThingOwner<Thing>(everflower);
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref _container, "container", _everflower);
            Scribe_Collections.Look(ref _heldPawns, "heldPawns", LookMode.Deep);

            // 대기열의 폰은 맵에 살아 있는 채로 깊게 저장되므로 폰은 참조로 저장한다.
            Scribe_Collections.Look(ref _pendingHolds, "pendingHolds", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 대기열 저장 전 개발 빌드 세이브(2026-11-03~)는 노드가 없어 null로 불러와지므로 빈 목록으로 채운다.
                if (_pendingHolds == null) { _pendingHolds = new List<PendingHold>(); }

                _heldPawns.RemoveAll(v => v?.pawn == null);
                _pendingHolds.RemoveAll(v => v?.pawn == null);
            }
        }

        public bool IsHeld(Pawn pawn)
        {
            return _heldPawns.Any(v => v?.pawn == pawn);
        }

        public void HoldUntilReturn(Pawn pawn, int ticks)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || IsHeld(pawn)) { return; }

            // 영원꽃 안의 정착민은 게임 오버 판정에서 제외되므로 마지막 자유 정착민은 품지 않고 제단 앞으로 보낸다.
            if (IsLastFreeColonist(pawn))
            {
                TeleportToReturnCell(pawn);
                return;
            }

            // 폰의 헤디프 틱에서 호출되므로 디스폰은 영원꽃 틱으로 미룬다.
            if (!_pendingHolds.Any(v => v.pawn == pawn))
            {
                _pendingHolds.Add(new PendingHold() { pawn = pawn, ticks = ticks });
            }
        }

        public static bool IsRestoringHealth(Pawn pawn)
        {
            return pawn != null && pawn == _restoringHealth;
        }

        // 죽음을 가로채 회복시킨 뒤 아직 품기 전인 여제인지. 가로챈 호출이 끝나고 다음 영원꽃 틱이 올 때까지 참이다.
        public bool IsAwaitingEnshrinement(Pawn pawn)
        {
            for (int i = 0; i < _pendingHolds.Count; ++i)
            {
                if (_pendingHolds[i].rebirth && _pendingHolds[i].pawn == pawn) { return true; }
            }

            return false;
        }

        // Pawn.Kill 직전에 불린다. true면 죽음을 취소한다.
        // 그 자리에서 상처를 모두 회복시켜 죽을 상태에서 벗어나게 하고, 피해 처리·건강 틱 도중일 수 있으므로 디스폰과 보관만 다음 영원꽃 틱으로 미룬다.
        // 가로챌 수 없는 죽음은 여기서 false를 돌려 그대로 진행시킨다. 가로챈 뒤에 되돌리는 경로는 두지 않는다.
        public bool TryEnshrineInsteadOfDeath(Pawn queen)
        {
            if (!CanEnshrine(queen)) { return false; }

            RestoreHealth(queen);
            if (queen.Dead || queen.health.ShouldBeDead()) { return false; }

            var pending = _pendingHolds.FirstOrDefault(v => v.pawn == queen);
            if (pending == null)
            {
                _pendingHolds.Add(new PendingHold() { pawn = queen, ticks = RebirthTicks, rebirth = true });
            }
            else if (!pending.rebirth)
            {
                pending.ticks = RebirthTicks;
                pending.rebirth = true;
            }

            return true;
        }

        // 정착지 소속으로 맵에 있는(또는 맵의 폰이 안아 옮기는) 여제만 안치한다.
        // 포로·손님·노예처럼 억류된 상태, 다른 세력 소속, 캐러밴·수송 포드 등 맵 밖에서는 평소처럼 죽는다.
        private bool CanEnshrine(Pawn queen)
        {
            if (queen == null || queen.Dead || queen.Destroyed || !CanRebirth) { return false; }
            if (queen.Faction == null || !queen.Faction.IsPlayer || queen.HostFaction != null) { return false; }
            if (IsHeld(queen)) { return false; }

            if (queen.Spawned) { return true; }

            return queen.ParentHolder is Pawn_CarryTracker carrier && carrier.pawn != null && carrier.pawn.Spawned;
        }

        public void Tick()
        {
            var now = Find.TickManager.TicksGame;

            TickPendingHolds();
            TickHeldPawns(now);
        }

        // 꽃이 파괴된 뒤에 불러야 품은 여제가 죽으면서 다시 부활을 예약하지 않는다.
        public void Notify_EverflowerDestroyed()
        {
            // 안치를 기다리던 여제는 이미 회복한 채 맵에 살아 있으므로 예약만 지운다.
            _pendingHolds.Clear();
            _heldPawns.Clear();

            // 감싸는 용기 안에서 죽은 폰의 시체는 같은 용기에 남으므로 죽인 뒤 시체까지 함께 없앤다.
            foreach (var pawn in _container.InnerListForReading.OfType<Pawn>().ToList())
            {
                if (!pawn.Dead)
                {
                    pawn.Kill(null);
                }
            }

            _container.ClearAndDestroyContents();
        }

        public string GetInspectString()
        {
            var held = _heldPawns.FirstOrDefault(v => v != null && v.rebirth && v.pawn != null);
            if (held == null) { return null; }

            var ticksLeft = Math.Max(0, held.returnTick - Find.TickManager.TicksGame);
            return LocalizeString_Inspector.VV_Inspector_QueenRebirth.Translate(held.pawn.Named("PAWN"), ticksLeft.ToStringTicksToPeriod().Named("TIME")).Resolve();
        }

        private HeldPawn Hold(Pawn pawn, int ticks, bool rebirth)
        {
            pawn.holdingOwner?.Remove(pawn);
            if (!_container.TryAdd(pawn)) { return null; }

            var held = new HeldPawn()
            {
                pawn = pawn,
                returnTick = Find.TickManager.TicksGame + Mathf.Max(1, ticks),
                rebirth = rebirth,
            };

            _heldPawns.Add(held);
            return held;
        }

        private void TickPendingHolds()
        {
            if (_pendingHolds.Count == 0) { return; }

            var pending = _pendingHolds.ToList();
            _pendingHolds.Clear();

            foreach (var hold in pending)
            {
                var pawn = hold.pawn;
                if (hold.rebirth)
                {
                    try
                    {
                        Enshrine(hold);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[ViViRace] failed to enshrine the queen {pawn}: {ex}");
                    }

                    continue;
                }

                if (pawn == null || !pawn.Spawned || pawn.Dead || IsHeld(pawn)) { continue; }

                if (IsLastFreeColonist(pawn))
                {
                    TeleportToReturnCell(pawn);
                    continue;
                }

                try
                {
                    SkipUtility.SkipDeSpawn(pawn);
                    Hold(pawn, hold.ticks, rebirth: false);
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] failed to hold returning pawn {pawn}: {ex}");
                }
            }
        }

        private void TickHeldPawns(int now)
        {
            if (_heldPawns.Count == 0 || !_everflower.Spawned) { return; }

            // 남은 정착민이 모두 쓰러지면 귀환 대기를 생략한다.
            // 즉시 스폰하면 바닐라가 재판정해 400틱의 게임 오버 카운트다운을 취소한다.
            var gameEnding = Find.GameEnder.gameEnding;
            for (int i = _heldPawns.Count - 1; i >= 0; --i)
            {
                var held = _heldPawns[i];
                if (held?.pawn == null || held.pawn.holdingOwner != _container)
                {
                    _heldPawns.RemoveAt(i);
                    continue;
                }

                if (now < held.returnTick && !(gameEnding && held.pawn.IsFreeColonist)) { continue; }

                _heldPawns.RemoveAt(i);
                try
                {
                    Return(held);
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] failed to return held pawn {held.pawn}: {ex}");

                    var pawn = held.pawn;
                    if (!pawn.Spawned && !pawn.Destroyed && (pawn.holdingOwner == _container || (pawn.holdingOwner == null && _container.TryAdd(pawn))))
                    {
                        held.returnTick = now + RetryTicks;
                        _heldPawns.Add(held);
                    }
                }
            }
        }

        // 예약한 뒤 조건이 바뀌었으면(꽃이 힘을 잃음, 세력 변경, 맵을 떠남 등) 회복한 채 그대로 둔다.
        private void Enshrine(PendingHold hold)
        {
            var queen = hold.pawn;
            if (!CanEnshrine(queen)) { return; }

            // 가로챈 뒤 같은 피해 처리에서 더 들어온 부상까지 되돌린다.
            RestoreHealth(queen);
            if (queen.Dead || queen.Destroyed) { return; }

            if (queen.health.ShouldBeDead())
            {
                queen.health.CheckForStateChange(null, null);
                return;
            }

            var map = queen.MapHeld;
            if (map != null)
            {
                VVEffecterDefOf.VV_Effecter_QueenCorpseBloom?.Spawn(queen.PositionHeld, map).Cleanup();
            }

            if (queen.Spawned)
            {
                if (queen.carryTracker?.CarriedThing != null)
                {
                    queen.carryTracker.TryDropCarriedThing(queen.Position, ThingPlaceMode.Near, out _);
                }

                queen.GetLord()?.Notify_PawnLost(queen, PawnLostCondition.Vanished);
                queen.DeSpawnOrDeselect();
            }

            // 안겨 있던 여제는 Hold가 옮기던 폰의 품에서 꺼낸다(바닐라도 안긴 채 죽은 폰을 같은 방식으로 꺼낸다).
            if (Hold(queen, hold.ticks, rebirth: true) == null)
            {
                // 품지 못하면 회복한 채 영원꽃 앞으로 돌려보낸다.
                if (!queen.Spawned && queen.holdingOwner == null)
                {
                    GenSpawn.Spawn(queen, FindReturnCell(_everflower.Map, RebirthCellSearchRadius), _everflower.Map);
                }

                return;
            }

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_QueenLifeResonanceLabel.Translate(queen.Named("PAWN")),
                LocalizeString_Letter.VV_Letter_QueenLifeResonance.Translate(queen.Named("PAWN"), hold.ticks.ToStringTicksToPeriod().Named("TIME")),
                LetterDefOf.NeutralEvent,
                new LookTargets(_everflower));
        }

        // 살아 있는 폰의 피해를 모두 되돌린다: 부상(흉터 포함)·결손 부위와 목숨을 위협하는 질환을 없앤다. 생명에 지장이 없는 만성 질환·중독·이식물은 남는다.
        // 바닐라 RestorePart처럼 헤디프를 하나씩 떼면서 제거 훅을 부르고, 쓰러짐·일어남 판정은 끝난 뒤 한 번만 한다.
        private static void RestoreHealth(Pawn pawn)
        {
            var health = pawn.health;
            var hediffSet = health.hediffSet;
            var previous = _restoringHealth;
            _restoringHealth = pawn;
            try
            {
                while (true)
                {
                    var missing = hediffSet.GetMissingPartsCommonAncestors().FirstOrDefault(v => !hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(v.Part));
                    if (missing == null) { break; }

                    health.RestorePart(missing.Part, null, checkStateChange: false);
                }

                _tmpHediffs.Clear();
                _tmpHediffs.AddRange(hediffSet.hediffs);
                for (int i = 0; i < _tmpHediffs.Count; ++i)
                {
                    var hediff = _tmpHediffs[i];
                    if (!ShouldRemoveOnRestore(hediff) || !hediffSet.hediffs.Contains(hediff)) { continue; }

                    hediff.PreRemoved();
                    hediffSet.hediffs.Remove(hediff);
                    hediffSet.DirtyCache();
                    hediff.PostRemoved();
                }
            }
            finally
            {
                _tmpHediffs.Clear();
                _restoringHealth = previous;
            }

            hediffSet.DirtyCache();

            // 그래도 죽을 상태면 판정을 호출한 쪽(죽음 진행)에 맡긴다.
            if (pawn.Dead || health.ShouldBeDead()) { return; }

            health.CheckForStateChange(null, null);
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
        }

        private static bool ShouldRemoveOnRestore(Hediff hediff)
        {
            if (hediff is Hediff_Injury) { return true; }
            if (hediff is Hediff_MissingPart || !hediff.def.everCurableByItem) { return false; }

            return hediff.IsLethal || hediff.IsAnyStageLifeThreatening() || hediff.TryGetComp<HediffComp_Immunizable>() != null;
        }

        private void Return(HeldPawn held)
        {
            var pawn = held.pawn;
            var map = _everflower.Map;
            var cell = FindReturnCell(map, held.rebirth ? RebirthCellSearchRadius : ReturnCellSearchRadius);

            _container.Remove(pawn);
            GenSpawn.Spawn(pawn, cell, map);
            pawn.Notify_Teleported();

            if (held.rebirth)
            {
                PlayRebirthEffect(pawn, map);
                return;
            }

            if (pawn.drafter != null && pawn.wasDraftedBeforeSkip)
            {
                pawn.drafter.Drafted = true;
            }

            PlayReturnEffect(pawn, map);

            Messages.Message(
                LocalizeString_Message.VV_Message_EndingPawnReturned.Translate(pawn.Named("PAWN")),
                pawn,
                MessageTypeDefOf.PositiveEvent);
        }

        private static void PlayReturnEffect(Pawn pawn, Map map)
        {
            var returnEffect = VVEffecterDefOf.VV_Effecter_FairyReturn ?? EffecterDefOf.Skip_ExitNoDelay;
            returnEffect?.SpawnMaintained(pawn, map);
        }

        private static void PlayRebirthEffect(Pawn pawn, Map map)
        {
            VVEffecterDefOf.VV_Effecter_QueenRebirth?.Spawn(pawn, map).Cleanup();

            Messages.Message(
                LocalizeString_Message.VV_Message_QueenReborn.Translate(pawn.Named("PAWN")),
                pawn,
                MessageTypeDefOf.PositiveEvent);
        }

        private static bool IsLastFreeColonist(Pawn pawn)
        {
            if (!pawn.IsFreeColonist) { return false; }

            var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; ++i)
            {
                if (colonists[i] != pawn)
                {
                    return false;
                }
            }

            return true;
        }

        private void TeleportToReturnCell(Pawn pawn)
        {
            var map = pawn.Map;
            var cell = FindReturnCell(map, ReturnCellSearchRadius);
            if (!cell.IsValid || cell == pawn.Position) { return; }

            EffecterDefOf.Skip_EntryNoDelay.Spawn(pawn, map).Cleanup();
            pawn.Position = cell;
            pawn.Notify_Teleported();
            PlayReturnEffect(pawn, map);

            Messages.Message(
                LocalizeString_Message.VV_Message_EndingPawnReturned.Translate(pawn.Named("PAWN")),
                pawn,
                MessageTypeDefOf.PositiveEvent);
        }

        private IntVec3 FindReturnCell(Map map, float radius)
        {
            var root = FindReturnRoot(map);
            var cell = CellFinder.StandableCellNear(root, map, radius, c => c.InBounds(map) && !c.Fogged(map));
            if (!cell.IsValid)
            {
                cell = CellFinder.RandomClosewalkCellNear(root, map, 12);
            }

            return cell;
        }

        private IntVec3 FindReturnRoot(Map map)
        {
            var altar = ViviEndingUtility.AltarOf(_everflower);
            if (altar != null && altar.Spawned && altar.Map == map) { return altar.FrontCell; }

            return _everflower.Spawned && _everflower.Map == map ? _everflower.Position : DropCellFinder.TradeDropSpot(map);
        }
    }
}
