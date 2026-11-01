using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    // 되살린 여왕은 기존 귀환 대기(HeldPawn)에 맡겨 저장·게임 오버 예외 처리를 그대로 쓴다.
    public partial class ViviEndingQuest
    {
        public const int QueenRebirthTicks = 60000;
        private const float RebirthCellSearchRadius = 6f;

        // 같은 틱에 비우는 대기열이므로 저장하지 않는다.
        private readonly List<Pawn> _pendingRebirths = new List<Pawn>();

        public bool CanRebirthQueen
        {
            get
            {
                var anchor = _anchor;
                return anchor != null && !anchor.Destroyed && anchor.Spawned && anchor.AttunementLevel >= CompEverflower.MaxAttunementLevel;
            }
        }

        public static void GrantQueenPowers(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || pawn.health.hediffSet.HasHediff(QueenDefOf.VV_EverflowerQueen)) { return; }

            pawn.health.AddHediff(QueenDefOf.VV_EverflowerQueen);
        }

        // 폰이 죽는 도중(시체가 생기기 전)에 불리므로 시체 처리와 부활은 다음 게임 컴포넌트 틱으로 미룬다.
        public void Notify_QueenDied(Pawn queen)
        {
            if (queen == null || !CanRebirthQueen || _pendingRebirths.Contains(queen)) { return; }

            _pendingRebirths.Add(queen);
        }

        public bool IsQueenRebirthPending(Pawn pawn)
        {
            return pawn != null && (_pendingRebirths.Contains(pawn) || _heldPawns.Any(v => v != null && v.rebirth && v.pawn == pawn));
        }

        private void ProcessPendingRebirths()
        {
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

        private void BeginRebirth(Pawn queen)
        {
            if (queen == null || !queen.Dead || queen.Discarded || !CanRebirthQueen) { return; }
            if (_heldPawns.Any(v => v?.pawn == queen)) { return; }

            var corpse = queen.Corpse;
            if (corpse != null && corpse.Spawned)
            {
                QueenDefOf.VV_Effecter_QueenCorpseBloom?.Spawn(corpse.Position, corpse.Map).Cleanup();
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

            HoldDespawnedPawn(queen, QueenRebirthTicks, AnchorMap);

            var held = _heldPawns.LastOrDefault(v => v?.pawn == queen);
            if (held == null) { return; }

            held.rebirth = true;

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingQueen.VV_Letter_QueenLifeResonanceLabel.Translate(queen.Named("PAWN")),
                LocalizeString_EndingQueen.VV_Letter_QueenLifeResonance.Translate(queen.Named("PAWN"), QueenRebirthTicks.ToStringTicksToPeriod().Named("TIME")),
                LetterDefOf.NeutralEvent,
                new LookTargets(_anchor));
        }

        private IntVec3 FindRebirthCell(Map map)
        {
            var anchor = _anchor;
            if (anchor == null || !anchor.Spawned || anchor.Map != map) { return FindReturnCell(map); }

            var altar = AnchorAltar;
            var root = altar != null && altar.Spawned && altar.Map == map ? altar.FrontCell : anchor.Position;
            var cell = CellFinder.StandableCellNear(root, map, RebirthCellSearchRadius, c => c.InBounds(map) && !c.Fogged(map));
            return cell.IsValid ? cell : FindReturnCell(map);
        }

        private static void PlayRebirthEffect(Pawn pawn, Map map)
        {
            QueenDefOf.VV_Effecter_QueenRebirth?.Spawn(pawn, map).Cleanup();

            Messages.Message(
                LocalizeString_EndingQueen.VV_Message_QueenReborn.Translate(pawn.Named("PAWN")),
                pawn,
                MessageTypeDefOf.PositiveEvent);
        }

        private string GetQueenRebirthInspectString()
        {
            var held = _heldPawns.FirstOrDefault(v => v != null && v.rebirth && v.pawn != null);
            if (held == null) { return null; }

            var ticksLeft = Math.Max(0, held.returnTick - Find.TickManager.TicksGame);
            return LocalizeString_EndingQueen.VV_Inspector_QueenRebirth.Translate(held.pawn.Named("PAWN"), ticksLeft.ToStringTicksToPeriod().Named("TIME")).Resolve();
        }
    }
}
