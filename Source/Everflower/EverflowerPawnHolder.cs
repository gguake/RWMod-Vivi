using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 영원꽃이 잠시 품었다가 돌려보내는 폰. 결속 해방된 요정 비비와 되살아나는 여제를 맡는다.
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

        // 다음 영원꽃 틱에 품을 귀환 보류 폰.
        public class PendingHold : IExposable
        {
            public Pawn pawn;
            public int ticks;

            public void ExposeData()
            {
                Scribe_References.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref ticks, "ticks", 0);
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
        private List<Pawn> _pendingRebirths = new List<Pawn>();

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

            // 대기열의 폰은 죽은 여제면 시체 안에, 귀환 보류 폰이면 맵에 깊게 저장되므로 폰은 참조로 저장한다.
            // 죽은 여제는 1.7에서 Destroyed 상태라 saveDestroyedThings가 없으면 참조가 null로 저장된다(시체가 폰을 깊게 저장하므로 불러올 때 풀린다).
            Scribe_Collections.Look(ref _pendingRebirths, "pendingRebirths", true, LookMode.Reference);
            Scribe_Collections.Look(ref _pendingHolds, "pendingHolds", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _heldPawns.RemoveAll(v => v?.pawn == null);
                _pendingRebirths.RemoveAll(v => v == null);
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

        // 폰이 죽는 도중(시체가 생기기 전)에 불리므로 시체 처리와 부활은 다음 영원꽃 틱으로 미룬다.
        public void Notify_QueenDied(Pawn queen)
        {
            if (queen == null || !CanRebirth || _pendingRebirths.Contains(queen)) { return; }

            _pendingRebirths.Add(queen);
        }

        public void Tick()
        {
            var now = Find.TickManager.TicksGame;

            TickPendingRebirths();
            TickPendingHolds();
            TickHeldPawns(now);
        }

        // 꽃이 파괴된 뒤에 불러야 품은 여제가 죽으면서 다시 부활을 예약하지 않는다.
        public void Notify_EverflowerDestroyed()
        {
            _pendingHolds.Clear();
            _pendingRebirths.Clear();
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

        private void TickPendingRebirths()
        {
            if (_pendingRebirths.Count == 0) { return; }

            var pending = _pendingRebirths.ToList();
            _pendingRebirths.Clear();

            foreach (var queen in pending)
            {
                try
                {
                    BeginRebirth(queen);
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] failed to begin the queen's rebirth for {queen}: {ex}");
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

        private void BeginRebirth(Pawn queen)
        {
            if (queen == null || !queen.Dead || queen.Discarded || !CanRebirth) { return; }
            if (IsHeld(queen)) { return; }

            var corpse = queen.Corpse;
            if (corpse != null && corpse.Spawned)
            {
                VVEffecterDefOf.VV_Effecter_QueenCorpseBloom?.Spawn(corpse.Position, corpse.Map).Cleanup();
            }

            // 선택된 폰이 죽으면 바닐라가 시체를 대신 선택한다. 시체가 선택된 채로 스폰 없이 되살리면 TryResurrect가
            // 맵이 없는 폰을 다시 선택하면서 현재 맵을 비워 행성 화면으로 넘어가므로, 곧 사라질 시체의 선택을 먼저 해제한다.
            if (corpse != null && Find.Selector.IsSelected(corpse))
            {
                Find.Selector.Deselect(corpse);
            }

            var parms = new ResurrectionParams()
            {
                dontSpawn = true,
                restoreMissingParts = true,
                removeDiedThoughts = true,
            };

            if (!ResurrectionUtility.TryResurrect(queen, parms)) { return; }

            if (queen.Spawned)
            {
                queen.DeSpawn();
            }

            if (Find.WorldPawns.Contains(queen))
            {
                Find.WorldPawns.RemovePawn(queen);
            }

            if (Hold(queen, RebirthTicks, rebirth: true) == null) { return; }

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_QueenLifeResonanceLabel.Translate(queen.Named("PAWN")),
                LocalizeString_Letter.VV_Letter_QueenLifeResonance.Translate(queen.Named("PAWN"), RebirthTicks.ToStringTicksToPeriod().Named("TIME")),
                LetterDefOf.NeutralEvent,
                new LookTargets(_everflower));
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
