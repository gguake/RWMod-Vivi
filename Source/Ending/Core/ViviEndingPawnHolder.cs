using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ViviEndingPawnHolder : IExposable
    {
        // 결속 해방 후 귀환을 기다리는 비비. 월드 폰에 넘기지 않고 여기서만 Deep 저장해 귀환 틱에 제단 앞에 스폰한다.
        public class HeldPawn : IExposable
        {
            public Pawn pawn;
            public int returnTick;
            public Map map;

            public bool rebirth;

            public void ExposeData()
            {
                // 대기 중 맵이 사라지면 유효하지 않은 참조를 저장하지 않는다. 귀환 시 엔딩 맵이나 다른 정착지로 대체한다.
                if (Scribe.mode == LoadSaveMode.Saving && map != null && !Find.Maps.Contains(map))
                {
                    map = null;
                }

                Scribe_Deep.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref returnTick, "returnTick", 0);
                Scribe_References.Look(ref map, "map");
                Scribe_Values.Look(ref rebirth, "rebirth", false);
            }
        }

        // 같은 틱에 비우는 대기열이므로 저장하지 않는다. 일시정지 중 개발자 기즈모로 추가한 항목은 다음 틱에 처리한다.
        private class PendingHold
        {
            public Pawn pawn;
            public int ticks;
            public Map map;
        }

        private const float ReturnCellSearchRadius = 8f;
        private const float RebirthCellSearchRadius = 6f;
        private const int RetryTicks = 2500;

        private ViviEndingQuest _ending;

        private List<HeldPawn> _heldPawns = new List<HeldPawn>();
        private readonly List<PendingHold> _pendingHolds = new List<PendingHold>();

        public IEnumerable<HeldPawn> HeldPawns => _heldPawns;

        public ViviEndingPawnHolder(ViviEndingQuest ending)
        {
            _ending = ending;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _heldPawns, "heldPawns", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _heldPawns.RemoveAll(v => v?.pawn == null);
            }
        }

        public bool IsHeld(Pawn pawn)
        {
            return _heldPawns.Any(v => v?.pawn == pawn);
        }

        public void HoldPawnUntilReturn(Pawn pawn, int ticks)
        {
            if (pawn == null || pawn.Destroyed) { return; }
            if (IsHeld(pawn)) { return; }

            var map = pawn.MapHeld ?? _ending.AnchorMap ?? _ending.Boss?.MapHeld ?? Find.AnyPlayerHomeMap;

            if (pawn.Spawned)
            {
                // 마지막 자유 정착민이 잠시라도 사라지면 게임 오버로 판정될 수 있으므로 대기 없이 제단 앞으로 보낸다.
                if (IsLastFreeColonist(pawn))
                {
                    TeleportToAltarFront(pawn);
                    return;
                }

                // 폰의 헤디프 틱에서 호출되므로 디스폰은 모든 Thing 틱이 끝난 뒤 게임 컴포넌트 틱으로 미룬다.
                if (!_pendingHolds.Any(v => v.pawn == pawn))
                {
                    _pendingHolds.Add(new PendingHold() { pawn = pawn, ticks = ticks, map = map });
                }
                return;
            }

            Hold(pawn, ticks, map);
        }

        public HeldPawn Hold(Pawn pawn, int ticks, Map map)
        {
            if (pawn.Spawned || pawn.Destroyed) { return null; }

            pawn.holdingOwner?.Remove(pawn);

            var held = new HeldPawn()
            {
                pawn = pawn,
                returnTick = Find.TickManager.TicksGame + Mathf.Max(1, ticks),
                map = map,
            };

            _heldPawns.Add(held);
            return held;
        }

        public void TickPendingHolds()
        {
            if (_pendingHolds.Count == 0) { return; }

            var pending = _pendingHolds.ToList();
            _pendingHolds.Clear();

            foreach (var hold in pending)
            {
                var pawn = hold.pawn;
                if (pawn == null || pawn.Destroyed || pawn.Dead) { continue; }
                if (IsHeld(pawn)) { continue; }

                if (pawn.Spawned)
                {
                    if (IsLastFreeColonist(pawn))
                    {
                        TeleportToAltarFront(pawn);
                        continue;
                    }

                    SkipUtility.SkipDeSpawn(pawn);
                }

                Hold(pawn, hold.ticks, hold.map ?? _ending.AnchorMap ?? Find.AnyPlayerHomeMap);
            }
        }

        public void TickHeldPawns(int now)
        {
            if (_heldPawns.Count == 0) { return; }

            // 보관 중인 정착민은 게임 오버 판정에서 제외되므로 남은 정착민이 모두 쓰러지면 귀환 대기를 생략한다.
            // 즉시 스폰하면 바닐라가 재판정해 400틱의 게임 오버 카운트다운을 취소한다.
            var gameEnding = Find.GameEnder.gameEnding;
            for (int i = _heldPawns.Count - 1; i >= 0; --i)
            {
                var held = _heldPawns[i];
                if (held?.pawn == null || held.pawn.Destroyed)
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
                    if (!pawn.Spawned && !pawn.Destroyed && pawn.holdingOwner == null && !Find.WorldPawns.Contains(pawn))
                    {
                        held.returnTick = now + RetryTicks;
                        _heldPawns.Add(held);
                    }
                }
            }
        }

        private void Return(HeldPawn held)
        {
            var pawn = held.pawn;
            var map = held.map;
            if (map == null || !Find.Maps.Contains(map))
            {
                map = _ending.AnchorMap ?? Find.AnyPlayerHomeMap ?? FindMapWithFreeColonists();
            }

            if (map == null)
            {
                var caravan = Find.WorldObjects.Caravans.FirstOrDefault(v => v.IsPlayerControlled);
                if (caravan != null)
                {
                    caravan.AddPawn(pawn, addCarriedPawnToWorldPawnsIfAny: true);
                    if (!pawn.IsWorldPawn())
                    {
                        Find.WorldPawns.PassToWorld(pawn);
                    }

                    Messages.Message(
                        LocalizeString_Message.VV_Message_EndingPawnReturnedToCaravan.Translate(pawn.Named("PAWN"), caravan.Named("CARAVAN")),
                        caravan,
                        MessageTypeDefOf.PositiveEvent);
                    return;
                }

                Find.WorldPawns.PassToWorld(pawn);
                return;
            }

            var cell = held.rebirth ? FindRebirthCell(map) : FindReturnCell(map);
            pawn.holdingOwner?.Remove(pawn);
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

        private static Map FindMapWithFreeColonists()
        {
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; ++i)
            {
                if (maps[i].mapPawns.FreeColonistsSpawnedCount > 0)
                {
                    return maps[i];
                }
            }

            return null;
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

        private void TeleportToAltarFront(Pawn pawn)
        {
            var map = pawn.Map;
            var cell = FindReturnCell(map);
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

        private IntVec3 FindReturnCell(Map map)
        {
            var altar = _ending.AnchorAltar;
            var root = altar != null && altar.Spawned && altar.Map == map ? altar.FrontCell : DropCellFinder.TradeDropSpot(map);

            var cell = CellFinder.StandableCellNear(root, map, ReturnCellSearchRadius, c => c.InBounds(map) && !c.Fogged(map));
            if (!cell.IsValid)
            {
                cell = CellFinder.RandomClosewalkCellNear(root, map, 12);
            }

            return cell;
        }

        private IntVec3 FindRebirthCell(Map map)
        {
            var anchor = _ending.AnchorEverflower;
            if (anchor == null || !anchor.Spawned || anchor.Map != map) { return FindReturnCell(map); }

            var altar = _ending.AnchorAltar;
            var root = altar != null && altar.Spawned && altar.Map == map ? altar.FrontCell : anchor.Position;
            var cell = CellFinder.StandableCellNear(root, map, RebirthCellSearchRadius, c => c.InBounds(map) && !c.Fogged(map));
            return cell.IsValid ? cell : FindReturnCell(map);
        }
    }
}
