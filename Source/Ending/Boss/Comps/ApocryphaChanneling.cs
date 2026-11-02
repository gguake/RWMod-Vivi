using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class ApocryphaChanneling : IExposable
    {
        private const int ContactCheckInterval = 10;
        private const int InterruptMessageMinTicks = 120;
        private const int InterruptMessageInterval = 600;

        private static readonly Color BeamColor = new Color(1f, 0.32f, 0.82f, 1f);

        private CompApocrypha _comp;

        private int _ticks;
        private bool _pendingSteal;
        private bool _devForce;
        private int _lastInterruptMessageTick = -99999;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public bool IsChanneling => Pawn.Spawned && Pawn.CurJobDef == VVJobDefOf.VV_ApocryphaChannel;
        public bool PendingSteal => _pendingSteal;
        public int DurationTicks => _comp.Phase == ApocryphaPhase.Unbound ? Props.channelTicksUnbound : Props.channelTicksBound;
        public float Progress => IsChanneling ? Mathf.Clamp01((float)_ticks / DurationTicks) : 0f;
        public int TicksLeft => Mathf.Max(0, DurationTicks - _ticks);

        public ApocryphaChanneling(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _ticks, "ticks", 0);
            Scribe_Values.Look(ref _pendingSteal, "pendingSteal", false);
        }

        public bool TryGetStealTarget(out Thing target, out float range, out ArcanePlant_Everflower everflower)
        {
            target = null;
            range = 0f;
            everflower = null;
            if (!Pawn.Spawned || !_comp.IsQuestBossFight) { return false; }

            var map = Pawn.Map;
            var anchor = ViviEndingQuest.Current?.AnchorEverflower;
            if (anchor == null || !anchor.Spawned || anchor.Map != map) { return false; }

            everflower = anchor;

            var altar = everflower.EverflowerComp?.EnshrinedAltar;
            if (altar != null && altar.Spawned && altar.Map == map)
            {
                target = altar;
                range = Props.channelRangeAltar;
            }
            else
            {
                target = everflower;
                range = Props.channelRangeEverflower;
            }

            return true;
        }

        public bool InStealRange()
        {
            if (!TryGetStealTarget(out var target, out var range, out _)) { return false; }

            return _devForce || Pawn.Position.InHorDistOf(target.Position, range);
        }

        public void TickContactCheck()
        {
            if (!Pawn.IsHashIntervalTick(ContactCheckInterval)) { return; }
            if (IsChanneling || _comp.Breath.IsBreathing || _pendingSteal) { return; }
            if (Pawn.stances.stunner.Stunned) { return; }
            if (!InStealRange()) { return; }

            _comp.InterruptCurrentJob();
        }

        public void Notify_ChannelTick()
        {
            if (_pendingSteal || !_comp.IsQuestBossFight) { return; }

            _ticks++;
            if (_ticks >= DurationTicks)
            {
                _pendingSteal = true;
            }
        }

        // 보스가 사라질 때도 디스폰 직전에 작업 종료 알림이 오므로 Spawned만으로는 중단 메시지를 제어할 수 없다.
        // 살아 있고 퀘스트 보스전이 계속되는 경우에만 표시한다. 소실 처리는 알림 전에 보스를 퀘스트에서 분리한다.
        public void Notify_ChannelJobEnded()
        {
            if (_pendingSteal) { return; }

            var now = GenTicks.TicksGame;
            if (_ticks >= InterruptMessageMinTicks && Pawn.Spawned && !Pawn.Dead && _comp.IsQuestBossFight && now - _lastInterruptMessageTick > InterruptMessageInterval)
            {
                _lastInterruptMessageTick = now;
                Messages.Message(LocalizeString_Message.VV_Message_ApocryphaChannelInterrupted.Translate(), Pawn, MessageTypeDefOf.PositiveEvent);
            }

            _ticks = 0;
            _devForce = false;
        }

        public void Interrupt()
        {
            _pendingSteal = false;

            if (Pawn.Spawned && Pawn.CurJobDef == VVJobDefOf.VV_ApocryphaChannel)
            {
                Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            _ticks = 0;
        }

        public void Reset()
        {
            _ticks = 0;
            _pendingSteal = false;
            _devForce = false;
        }

        public void Draw()
        {
            if (IsChanneling && TryGetStealTarget(out _, out _, out var everflower))
            {
                var flowerPos = everflower.DrawPos + everflower.EverflowerComp.RitualEffecterOffset;
                ApocryphaGimmicks.DrawSoulLink(flowerPos, _comp.BodyCenter, 0.9f, BeamColor);
            }
        }

        public void DebugStart()
        {
            if (!Pawn.Spawned || !_comp.IsActive || !TryGetStealTarget(out _, out _, out _)) { return; }

            _devForce = true;
            Pawn.stances.stunner.StopStun();
            _comp.Shell.Cancel();
            _comp.NeedleAttack.CancelAims();
            _comp.Breath.Cancel();
            _comp.InterruptCurrentJob();
        }

        public void DebugFinish()
        {
            if (!Pawn.Spawned || !_comp.IsActive) { return; }

            if (!IsChanneling)
            {
                DebugStart();
            }

            _ticks = Mathf.Max(_ticks, DurationTicks - 1);
        }
    }
}
