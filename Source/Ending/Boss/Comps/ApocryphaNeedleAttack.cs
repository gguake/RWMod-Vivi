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
        private const int PatternCount = 3;
        private const float RepeatedPatternWeight = 0.25f;
        private const int RaidPattern = 2;
        private const int BoundSnipePattern = 3;

        public const int AsphodelNeedleCount = 6;

        private readonly CompApocrypha _comp;

        private List<ApocryphaNeedle> _needles = new List<ApocryphaNeedle>();
        private int _readyTick;
        private int _lastStickTick = -1;
        private int _lastPattern = -1;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public IReadOnlyList<ApocryphaNeedle> Needles => _needles;
        public int LastPattern => _lastPattern;
        public int NeedleCount => _comp.Phase == ApocryphaPhase.Asphodel ? AsphodelNeedleCount : Props.queenNeedleCount;
        public int LastStickTick => _lastStickTick;
        public int TicksUntilShakeOff => StuckNeedleCount > 0 ? Mathf.Max(0, _lastStickTick + Props.needleShakeOffTicks - GenTicks.TicksGame) : -1;

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
            Scribe_Values.Look(ref _lastStickTick, "lastStickTick", -1);
            Scribe_Values.Look(ref _lastPattern, "lastPattern", -1);

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

        public void TickAsphodel(int now, bool stunned)
        {
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

            if (now < _readyTick || _comp.Channel.IsChanneling || _comp.Channel.PendingSteal || _comp.Embrace.IsCasting) { return; }

            var idle = _needles.Where(v => v.Spawned && v.IsIdle).ToList();
            var targeting = _comp.Targeting;
            if (idle.Count < AsphodelNeedleCount)
            {
                _readyTick = now + TargetScanInterval;
                return;
            }

            if (targeting.TryFindRaidTargets(out var raider, out var secondRaider))
            {
                targeting.Notify_RaidTargeted(now);
                idle.Shuffle();
                StrikeHoming(idle.GetRange(0, 3), raider, requireBond: false);
                if (secondRaider != null)
                {
                    StrikeHoming(idle.GetRange(3, 3), secondRaider, requireBond: false);
                }

                _lastPattern = RaidPattern;
            }
            else if (targeting.TryFindBoundSnipeTargets(out var boundFirst, out var boundSecond))
            {
                var hasNormal = targeting.TryFindTargetCells(Props.needleTargetRange, out var first, out var second);
                if (!hasNormal || ShouldUseBoundSnipe())
                {
                    idle.Shuffle();
                    StrikeHoming(idle.GetRange(0, 3), boundFirst, requireBond: true);
                    if (boundSecond != null)
                    {
                        StrikeHoming(idle.GetRange(3, 3), boundSecond, requireBond: true);
                    }

                    _lastPattern = BoundSnipePattern;
                }
                else
                {
                    idle.Shuffle();
                    FirePattern(PickPattern(), idle, first, second);
                }
            }
            else if (targeting.TryFindTargetCells(Props.needleTargetRange, out var first, out var second))
            {
                idle.Shuffle();
                FirePattern(PickPattern(), idle, first, second);
            }
            else
            {
                _readyTick = now + TargetScanInterval;
                return;
            }

            _readyTick = now + Props.needleCastIntervalTicks.RandomInRange;
        }

        private void StrikeHoming(List<ApocryphaNeedle> needles, Pawn target, bool requireBond)
        {
            var center = target.DrawPos.Yto0();
            var offset = EndingVfxUtility.RandomRingOffset(Props.needleFanRadius);
            var aimTicks = _comp.EffectiveNeedleAimTicks;
            for (int i = 0; i < needles.Count; i++)
            {
                needles[i].StartHomingStrike(target, center + Quaternion.AngleAxis(360f / needles.Count * i, Vector3.up) * offset, aimTicks, requireBond);
            }
        }

        public void FirePattern(int pattern, List<ApocryphaNeedle> idle, IntVec3 first, IntVec3 second)
        {
            var startAngle = Rand.Range(0f, 360f);
            var aimTicks = _comp.EffectiveNeedleAimTicks;
            switch (pattern)
            {
                case 0:
                    AimFixed(idle, 6, startAngle, aimTicks);
                    break;

                case 1:
                    AimFixed(idle, 4, startAngle, aimTicks);
                    if (second.IsValid)
                    {
                        idle[4].StartAim(first, aimTicks);
                        idle[5].StartAim(second, aimTicks);
                    }
                    else
                    {
                        AimVolley(idle.GetRange(4, 2), first, 2, aimTicks);
                    }
                    break;

                default:
                    AimVolley(idle.GetRange(0, 3), first, 3, aimTicks);
                    if (second.IsValid)
                    {
                        AimVolley(idle.GetRange(3, 3), second, 3, aimTicks);
                    }
                    break;
            }

            _lastPattern = pattern;
        }

        private int PickPattern()
        {
            return Enumerable.Range(0, PatternCount).RandomElementByWeight(v => v == _lastPattern ? RepeatedPatternWeight : 1f);
        }

        private bool ShouldUseBoundSnipe()
        {
            var snipeWeight = _lastPattern == BoundSnipePattern ? RepeatedPatternWeight : 1f;
            return Rand.Value < snipeWeight / (snipeWeight + 1f);
        }

        private void AimFixed(List<ApocryphaNeedle> idle, int count, float startAngle, int aimTicks)
        {
            for (int i = 0; i < count; i++)
            {
                idle[i].StartAimDirection(Quaternion.AngleAxis(startAngle + 360f / count * i, Vector3.up) * Vector3.forward, aimTicks);
            }
        }

        public void TickQueen(int now, bool stunned)
        {
            if (Pawn.IsHashIntervalTick(StuckSyncInterval))
            {
                SyncStuckNeedleHediff();
            }

            if (!_comp.IsQuestBossFight)
            {
                DestroyAll();
                return;
            }

            if (Props.queenNeedleCount <= 0) { return; }

            if (_lastStickTick < 0)
            {
                AttachInitialNeedles(now);
                return;
            }

            if (!stunned && StuckNeedleCount > 0 && now - _lastStickTick >= Props.needleShakeOffTicks)
            {
                ShakeOff(now);
            }

            if (!Pawn.IsHashIntervalTick(SpawnCheckInterval)) { return; }

            EnsureNeedles();

            var idle = _needles.Where(v => v != null && v.Spawned && v.IsIdle).ToList();
            if (idle.Count > 0)
            {
                Scatter(idle);
            }
        }

        public void Notify_NeedleStuck()
        {
            if (_comp.Phase != ApocryphaPhase.Queen || !Pawn.Spawned) { return; }

            _lastStickTick = GenTicks.TicksGame;
            SyncStuckNeedleHediff();
            _comp.TakeVitalityDamage(_comp.MaxVitality * Props.needleStickVitalityDamage);

            _comp.GimmickStun(Props.needleStickStunTicks, null);
            _comp.Channel.Interrupt();
            _comp.Breath.Cancel();
            _comp.EscortSummon.Cancel();

            Messages.Message(LocalizeString_Message.VV_Message_ApocryphaNeedleStuck.Translate(), Pawn, MessageTypeDefOf.PositiveEvent);
        }

        public void SyncStuckNeedleHediff()
        {
            var count = _comp.Phase == ApocryphaPhase.Queen ? StuckNeedleCount : 0;
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

        public void ShakeOff(int now)
        {
            var map = Pawn.Map;
            var chosen = new List<IntVec3>();
            for (int i = 0; i < _needles.Count; i++)
            {
                var needle = _needles[i];
                if (needle == null || needle.Destroyed || !needle.IsStuck) { continue; }

                var cell = FindPlantCell(map, Pawn.Position, chosen);
                chosen.Add(cell);
                needle.PullOut(cell);
            }

            _lastStickTick = now;
            SyncStuckNeedleHediff();

            HornetManifestation.SummonAround(Pawn, _comp.ScaledHornetCount(Props.shakeOffHornetCount), HornetSummonRole.Assault);
            _comp.SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaStaggered, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
            ApocryphaUtility.ShakeCamera(map, 1.2f);
            Messages.Message(LocalizeString_Message.VV_Message_ApocryphaNeedlesShakenOff.Translate(), Pawn, MessageTypeDefOf.ThreatSmall);
        }

        public void DropAll()
        {
            _needles.RemoveAll(v => v == null || v.Destroyed);

            var map = Pawn.Map;
            var center = Pawn.Position;
            var chosen = new List<IntVec3>();
            for (int i = 0; i < _needles.Count; i++)
            {
                var needle = _needles[i];
                if (!needle.Spawned || needle.IsPlanted || needle.IsScattering) { continue; }

                var cell = FindPlantCell(map, center, chosen);
                chosen.Add(cell);
                if (!needle.PullOut(cell))
                {
                    needle.ScatterTo(cell, EndingVfxUtility.SafeNormalizeHorizontal(cell.ToVector3Shifted() - needle.DrawPos, Vector3.forward));
                }
            }

            SyncStuckNeedleHediff();
        }

        public void RecallAll()
        {
            _needles.RemoveAll(v => v == null || v.Destroyed);
            for (int i = 0; i < _needles.Count; i++)
            {
                _needles[i].Recall();
            }

            EnsureNeedles();
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
            if (_comp.Phase != ApocryphaPhase.Queen) { return; }

            for (int i = 0; i < _needles.Count; i++)
            {
                if (_needles[i] != null && !_needles[i].Destroyed && !_needles[i].IsStuck && !_needles[i].IsFlying)
                {
                    _needles[i].DebugStick();
                    return;
                }
            }
        }

        public void DebugShakeOffNow()
        {
            if (_comp.Phase != ApocryphaPhase.Queen || StuckNeedleCount <= 0) { return; }

            ShakeOff(GenTicks.TicksGame);
        }

        private void AttachInitialNeedles(int now)
        {
            EnsureNeedles();

            var baseAngle = Rand.Range(0f, 360f);
            for (int i = 0; i < _needles.Count; i++)
            {
                var angle = baseAngle + _needles[i].Slot * 360f / Mathf.Max(1, _needles.Count) + Rand.Range(-25f, 25f);
                _needles[i].AttachStuck(Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward);
            }

            _lastStickTick = now;
            SyncStuckNeedleHediff();
        }

        private void AimVolley(List<ApocryphaNeedle> idle, IntVec3 cell, int count, int aimTicks)
        {
            for (int i = 0; i < count; i++)
            {
                idle[i].StartAim(cell, aimTicks, FanAngle(i, count));
            }
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

                var outDir = EndingVfxUtility.SafeNormalizeHorizontal(cell.ToVector3Shifted() - _comp.BodyCenter, Vector3.forward);
                needle.ScatterTo(cell, outDir);
            }
        }

        private IntVec3 FindPlantCell(Map map, IntVec3 center, List<IntVec3> chosen)
        {
            var radius = Props.needlePlantRadius;
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
