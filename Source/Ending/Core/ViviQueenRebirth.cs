using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    // 되살린 여왕은 기존 귀환 대기(ViviEndingPawnHolder)에 맡겨 저장·게임 오버 예외 처리를 그대로 쓴다.
    public class ViviQueenRebirth
    {
        public const int RebirthTicks = 60000;

        private ViviEndingQuest _ending;

        // 같은 틱에 비우는 대기열이므로 저장하지 않는다.
        private readonly List<Pawn> _pending = new List<Pawn>();

        public bool CanRebirth
        {
            get
            {
                var anchor = _ending.AnchorEverflower;
                return anchor != null && !anchor.Destroyed && anchor.Spawned && anchor.AttunementLevel >= CompEverflower.MaxAttunementLevel;
            }
        }

        public ViviQueenRebirth(ViviEndingQuest ending)
        {
            _ending = ending;
        }

        // 폰이 죽는 도중(시체가 생기기 전)에 불리므로 시체 처리와 부활은 다음 게임 컴포넌트 틱으로 미룬다.
        public void Notify_QueenDied(Pawn queen)
        {
            if (queen == null || !CanRebirth || _pending.Contains(queen)) { return; }

            _pending.Add(queen);
        }

        public bool IsRebirthPending(Pawn pawn)
        {
            return pawn != null && (_pending.Contains(pawn) || _ending.PawnHolder.HeldPawns.Any(v => v != null && v.rebirth && v.pawn == pawn));
        }

        public void Tick()
        {
            if (_pending.Count == 0) { return; }

            var pending = _pending.ToList();
            _pending.Clear();

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

        public string GetInspectString()
        {
            var held = _ending.PawnHolder.HeldPawns.FirstOrDefault(v => v != null && v.rebirth && v.pawn != null);
            if (held == null) { return null; }

            var ticksLeft = Math.Max(0, held.returnTick - Find.TickManager.TicksGame);
            return LocalizeString_Inspector.VV_Inspector_QueenRebirth.Translate(held.pawn.Named("PAWN"), ticksLeft.ToStringTicksToPeriod().Named("TIME")).Resolve();
        }

        private void BeginRebirth(Pawn queen)
        {
            if (queen == null || !queen.Dead || queen.Discarded || !CanRebirth) { return; }
            if (_ending.PawnHolder.IsHeld(queen)) { return; }

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

            var held = _ending.PawnHolder.Hold(queen, RebirthTicks, _ending.AnchorMap);
            if (held == null) { return; }

            held.rebirth = true;

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_QueenLifeResonanceLabel.Translate(queen.Named("PAWN")),
                LocalizeString_Letter.VV_Letter_QueenLifeResonance.Translate(queen.Named("PAWN"), RebirthTicks.ToStringTicksToPeriod().Named("TIME")),
                LetterDefOf.NeutralEvent,
                new LookTargets(_ending.AnchorEverflower));
        }
    }
}
