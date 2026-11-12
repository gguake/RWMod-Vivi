using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class ApocryphaNeedleAttack : IExposable
    {
        private const int TargetScanInterval = 30;
        private const int SpawnCheckInterval = 60;
        private const int StuckSyncInterval = 30;
        private const int StunRecoverTicks = 120;
        // 돌파는 접근 차례마다 한 번이다. 막히기 직전 칸에 도착한 뒤 이 시간 동안만 돌파 바늘을 쏘고, 길이 열리거나 시간이 다하면 평소 맴돌기로 돌아간다.
        private const int BreachHoldTicks = 600;
        // 도착하지 못해도 이 시간이 지나면 돌파를 접는다.
        private const int BreachDeadlineTicks = 5000;
        private const float BreachStandRadius = 2f;

        private readonly CompApocrypha _comp;

        private List<ApocryphaNeedle> _needles = new List<ApocryphaNeedle>();
        private int _readyTick;
        private bool _stunned;
        private IntVec3 _breachTarget = IntVec3.Invalid;
        private IntVec3 _breachStandCell = IntVec3.Invalid;
        private IntVec3 _breachBlockedCell = IntVec3.Invalid;
        private int _breachDeadlineTick = -1;
        private int _breachUntilTick = -1;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public IReadOnlyList<ApocryphaNeedle> Needles => _needles;
        public bool BreachInProgress => _breachBlockedCell.IsValid;
        public int NeedleCount => _comp.Phase == ApocryphaPhase.Unbound ? Props.unboundNeedleCount : Props.needleCount;

        public int StuckNeedleCount
        {
            get
            {
                var count = 0;
                for (int i = 0; i < _needles.Count; i++)
                {
                    if (_needles[i] != null && !_needles[i].Destroyed && _needles[i].IsStuck) { count++; }
                }

                return count;
            }
        }

        public ApocryphaNeedleAttack(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _readyTick, "readyTick", 0);
            Scribe_Values.Look(ref _stunned, "stunned", false);
            Scribe_Values.Look(ref _breachTarget, "breachTarget", IntVec3.Invalid);
            Scribe_Values.Look(ref _breachStandCell, "breachStandCell", IntVec3.Invalid);
            Scribe_Values.Look(ref _breachBlockedCell, "breachBlockedCell", IntVec3.Invalid);
            Scribe_Values.Look(ref _breachDeadlineTick, "breachDeadlineTick", -1);
            Scribe_Values.Look(ref _breachUntilTick, "breachUntilTick", -1);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _needles.RemoveAll(v => v == null || v.Destroyed);
            }

            Scribe_Collections.Look(ref _needles, "needles", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _needles.RemoveAll(v => v == null);
            }
        }

        public void SetReadyTick(int tick)
        {
            _readyTick = tick;
        }

        public void ClearStun()
        {
            _stunned = false;
        }

        public void TickBound(int now, bool stunned)
        {
            if (Props.needleCount <= 0) { return; }

            if (Pawn.IsHashIntervalTick(SpawnCheckInterval))
            {
                EnsureNeedles();
            }

            if (stunned)
            {
                if (CancelAims())
                {
                    _readyTick = Mathf.Max(_readyTick, now + StunRecoverTicks);
                }
                return;
            }

            if (now < _readyTick || _comp.Channel.IsChanneling || _comp.Channel.PendingSteal) { return; }

            var idle = _needles.Where(v => v.Spawned && v.IsIdle).ToList();
            if (idle.Count > 0 && TryGetBreachAim(now, out var breachCell))
            {
                AimVolley(idle, breachCell, idle.Count, breach: true);
                _readyTick = now + Props.needleCastIntervalTicks.RandomInRange;
                return;
            }

            if (idle.Count == 0 || !_comp.Targeting.TryFindTargetCell(Props.needleTargetRange, IsInAimedLane, null, out var cell))
            {
                _readyTick = now + TargetScanInterval;
                return;
            }

            idle.Shuffle();
            AimVolley(idle, cell, Mathf.Clamp(Props.needleVolleyCount.RandomInRange, 1, idle.Count), breach: false);
            _readyTick = now + Props.needleCastIntervalTicks.RandomInRange;
        }

        public void TickUnbound(bool stunned)
        {
            // 바늘이 뽑기 외의 경로(소멸 등)로 사라져도 겹 수가 맞도록 주기적으로 맞춘다.
            if (Pawn.IsHashIntervalTick(StuckSyncInterval))
            {
                SyncStuckNeedleHediff();
            }

            if (_stunned && !stunned)
            {
                PullOutNeedles();
            }

            if (!_comp.IsQuestBossFight)
            {
                DestroyAll();
                return;
            }

            if (Props.unboundNeedleCount <= 0 || !Pawn.IsHashIntervalTick(SpawnCheckInterval)) { return; }

            EnsureNeedles();

            var idle = _needles.Where(v => v != null && v.Spawned && v.IsIdle).ToList();
            if (idle.Count > 0)
            {
                Scatter(idle);
            }
        }

        // 접근 차례에 찬탈 대상까지 길이 막혀 있으면 대상까지 그은 직선에서 막히기 직전 칸(standCell)을 찾아 돌파를 시작한다.
        // 막힌 원인이 깊은 물·용암 같은 지형이나 부술 수 없는 건물이면 돌파하지 않는다.
        public bool TryStartBreach(Thing target, out IntVec3 standCell)
        {
            standCell = IntVec3.Invalid;
            EndBreach();
            if (_comp.Phase != ApocryphaPhase.Bound || target == null || !target.Spawned || target.Map != Pawn.Map) { return false; }
            if (Pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly)) { return false; }

            var map = Pawn.Map;
            var origin = Pawn.Position;
            var targetRect = target.OccupiedRect();
            var blocked = IntVec3.Invalid;
            standCell = origin;
            foreach (var c in GenSight.PointsOnLineOfSight(origin, target.Position))
            {
                if (c == origin) { continue; }
                if (targetRect.Contains(c)) { break; }
                if (!ApocryphaUtility.IsBreachableCell(map, c)) { return false; }

                // 벽·문은 막힌 칸이다. 설 수 있는데 갈 수 없는 칸은 갇힌 구역이다. 나무·모래주머니처럼 지나갈 수만 있는 칸은 건너뛴다.
                var edifice = c.GetEdifice(map);
                if (edifice != null && ApocryphaUtility.BlocksPath(edifice))
                {
                    blocked = c;
                    break;
                }

                if (!c.Standable(map)) { continue; }

                if (!Pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly))
                {
                    blocked = c;
                    break;
                }

                standCell = c;
            }

            if (!blocked.IsValid) { return false; }

            _breachTarget = target.Position;
            _breachStandCell = standCell;
            _breachBlockedCell = blocked;
            _breachDeadlineTick = GenTicks.TicksGame + BreachDeadlineTicks;
            _breachUntilTick = -1;
            return true;
        }

        // 진행 중인 돌파를 이어 간다. 막힌 칸이 열렸거나 사격 시간·기한이 다하면 돌파를 접고 false를 돌려준다.
        public bool TryContinueBreach(out IntVec3 standCell)
        {
            standCell = _breachStandCell;
            if (!BreachInProgress) { return false; }

            var now = GenTicks.TicksGame;
            if (_comp.Phase != ApocryphaPhase.Bound || now > _breachDeadlineTick || (_breachUntilTick >= 0 && now > _breachUntilTick)
                || Pawn.CanReach(_breachBlockedCell, PathEndMode.OnCell, Danger.Deadly))
            {
                EndBreach();
                return false;
            }

            if (_breachUntilTick < 0 && Pawn.Position.InHorDistOf(_breachStandCell, BreachStandRadius))
            {
                _breachUntilTick = now + BreachHoldTicks;
            }

            return true;
        }

        public void EndBreach()
        {
            _breachTarget = IntVec3.Invalid;
            _breachStandCell = IntVec3.Invalid;
            _breachBlockedCell = IntVec3.Invalid;
            _breachDeadlineTick = -1;
            _breachUntilTick = -1;
        }

        public void Notify_NeedleStuck(ApocryphaNeedle needle)
        {
            if (_comp.Phase != ApocryphaPhase.Unbound || !Pawn.Spawned) { return; }

            SyncStuckNeedleHediff();
            _comp.TakeVitalityDamage(_comp.MaxVitality * Props.needleStickVitalityDamage);

            var stuck = StuckNeedleCount;
            if (stuck >= Props.unboundNeedleCount && !_stunned)
            {
                _stunned = true;
                _comp.GimmickStun(Props.allNeedlesStuckStunTicks, null);
                _comp.Channel.Interrupt();
                _comp.Breath.Cancel();

                _comp.SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaStaggered, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
                ApocryphaUtility.ShakeCamera(Pawn.Map, 1.5f);
                Messages.Message(LocalizeString_Message.VV_Message_ApocryphaAllNeedlesStuck.Translate(), Pawn, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                _comp.GimmickStun(Props.needleStickStunTicks, null);
                _comp.Channel.Interrupt();
                _comp.Breath.Cancel();

                Messages.Message(
                    LocalizeString_Message.VV_Message_ApocryphaNeedleStuck.Translate(),
                    Pawn,
                    MessageTypeDefOf.PositiveEvent);
            }
        }

        public void SyncStuckNeedleHediff()
        {
            var count = _comp.Phase == ApocryphaPhase.Unbound ? StuckNeedleCount : 0;
            var hediff = Pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_ApocryphaStuckNeedle);
            if (count <= 0)
            {
                if (hediff != null)
                {
                    Pawn.health.RemoveHediff(hediff);
                }
                return;
            }

            if (hediff == null)
            {
                hediff = Pawn.health.AddHediff(VVHediffDefOf.VV_ApocryphaStuckNeedle);
            }

            if (hediff.Severity != count)
            {
                hediff.Severity = count;
            }
        }

        public void ScatterAll()
        {
            _needles.RemoveAll(v => v == null || v.Destroyed);
            Scatter(_needles.ToList());
        }

        public bool CancelAims()
        {
            var cancelled = false;
            for (int i = 0; i < _needles.Count; i++)
            {
                if (_needles[i] != null && !_needles[i].Destroyed && _needles[i].CancelAim())
                {
                    cancelled = true;
                }
            }

            return cancelled;
        }

        public void DestroyAll()
        {
            if (_needles.Count == 0) { return; }

            for (int i = 0; i < _needles.Count; i++)
            {
                if (_needles[i] != null && !_needles[i].Destroyed)
                {
                    _needles[i].Vanish();
                }
            }

            _needles.Clear();
        }

        public void DebugStickNeedle()
        {
            if (_comp.Phase != ApocryphaPhase.Unbound) { return; }

            for (int i = 0; i < _needles.Count; i++)
            {
                if (_needles[i] != null && !_needles[i].Destroyed && !_needles[i].IsStuck && !_needles[i].IsFlying)
                {
                    _needles[i].DebugStick();
                    return;
                }
            }
        }

        private void AimVolley(List<ApocryphaNeedle> idle, IntVec3 cell, int count, bool breach)
        {
            if (!cell.InHorDistOf(Pawn.Position, Props.needleFanMinTargetDistance))
            {
                for (int i = 0; i < count; i++)
                {
                    idle[i].StartAim(cell, Props.needleAimTicks, FanAngle(i, count), breach);
                }
            }
            else
            {
                idle[0].StartAim(cell, Props.needleAimTicks, breach: breach);
            }
        }

        private bool TryGetBreachAim(int now, out IntVec3 cell)
        {
            cell = _breachTarget;
            if (_comp.Phase != ApocryphaPhase.Bound || !BreachInProgress || _breachUntilTick < 0 || now > _breachUntilTick) { return false; }

            return Pawn.Position.InHorDistOf(_breachStandCell, BreachStandRadius);
        }

        private float? FanAngle(int index, int count)
        {
            if (count <= 1) { return null; }

            var half = count == 2 ? Props.needleFanHalfAngle2 : Props.needleFanHalfAngle3;
            return Mathf.Lerp(-half, half, index / (float)(count - 1));
        }

        private void Scatter(List<ApocryphaNeedle> needles)
        {
            var map = Pawn.Map;
            var center = Pawn.Position;
            var chosen = new List<IntVec3>();
            for (int i = 0; i < needles.Count; i++)
            {
                var needle = needles[i];
                if (needle == null || !needle.Spawned) { continue; }

                var cell = FindPlantCell(map, center, chosen);
                chosen.Add(cell);

                var outDir = (cell.ToVector3Shifted() - _comp.BodyCenter).Yto0();
                needle.ScatterTo(cell, outDir.sqrMagnitude > 0.01f ? outDir.normalized : Vector3.forward);
            }
        }

        private IntVec3 FindPlantCell(Map map, IntVec3 center, List<IntVec3> chosen)
        {
            var radius = Props.unboundNeedlePlantRadius;
            bool Valid(IntVec3 c, bool spaced)
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map)) { return false; }

                var distance = c.DistanceTo(center);
                if (distance < radius.min || distance > radius.max) { return false; }

                return !spaced || !chosen.Any(v => v.InHorDistOf(c, 3f));
            }

            if (CellFinder.TryFindRandomCellNear(center, map, Mathf.CeilToInt(radius.max), c => Valid(c, true), out var cell)
                || CellFinder.TryFindRandomCellNear(center, map, Mathf.CeilToInt(radius.max), c => Valid(c, false), out cell))
            {
                return cell;
            }

            cell = CellFinder.StandableCellNear(center, map, radius.max + 5f, c => c.InBounds(map) && !c.Fogged(map));
            return cell.IsValid ? cell : center;
        }

        private void PullOutNeedles()
        {
            _stunned = false;

            var map = Pawn.Map;
            var chosen = new List<IntVec3>();
            var pulled = false;
            for (int i = 0; i < _needles.Count; i++)
            {
                var needle = _needles[i];
                if (needle == null || needle.Destroyed || !needle.IsStuck) { continue; }

                var cell = FindPlantCell(map, Pawn.Position, chosen);
                chosen.Add(cell);
                if (needle.PullOut(cell))
                {
                    pulled = true;
                }
            }

            if (pulled)
            {
                ApocryphaUtility.ShakeCamera(Pawn.Map, 0.8f);
                Messages.Message(LocalizeString_Message.VV_Message_ApocryphaNeedlesPulled.Translate(), Pawn, MessageTypeDefOf.NeutralEvent);
            }

            SyncStuckNeedleHediff();
        }

        private bool IsInAimedLane(IntVec3 cell)
        {
            for (int i = 0; i < _needles.Count; i++)
            {
                if (_needles[i].IsAiming && _needles[i].LaneContains(cell.ToVector3Shifted())) { return true; }
            }

            return false;
        }

        private void EnsureNeedles()
        {
            _needles.RemoveAll(v => v == null || v.Destroyed);
            var count = NeedleCount;

            for (int i = _needles.Count - 1; i >= 0; i--)
            {
                if (_needles[i].Slot >= count)
                {
                    _needles[i].Vanish();
                    _needles.RemoveAt(i);
                }
            }

            if (_needles.Count >= count) { return; }

            for (int slot = 0; slot < count; slot++)
            {
                if (_needles.Any(v => v.Slot == slot)) { continue; }

                var needle = (ApocryphaNeedle)ThingMaker.MakeThing(VVThingDefOf.VV_ApocryphaNeedle);
                needle.Init(Pawn, slot);
                GenSpawn.Spawn(needle, Pawn.Position, Pawn.Map);
                _needles.Add(needle);
            }
        }
    }
}
