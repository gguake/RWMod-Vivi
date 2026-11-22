using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaDoomWave : IExposable
    {
        private const int RetryTicks = 600;

        private const float WaveDurationSecs = 2.4f;

        private readonly CompApocrypha _comp;

        private int _nextTick = -1;
        private int _wavesCast;
        private List<IntVec3> _pendingCells = new List<IntVec3>();
        private List<int> _pendingTicks = new List<int>();

        private CompProperties_Apocrypha Props => _comp.Props;

        public int BloomCount => Mathf.Min(Props.doomWaveBloomMax, Props.doomWaveBloomStart + _wavesCast / Mathf.Max(1, Props.doomWaveBloomGrowthEvery));
        public int PendingCount => _pendingCells.Count;
        public int NextTick => _nextTick;

        public ApocryphaDoomWave(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _nextTick, "nextTick", -1);
            Scribe_Values.Look(ref _wavesCast, "wavesCast", 0);
            Scribe_Collections.Look(ref _pendingCells, "pendingCells", LookMode.Value);
            Scribe_Collections.Look(ref _pendingTicks, "pendingTicks", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _pendingCells ??= new List<IntVec3>();
                _pendingTicks ??= new List<int>();
            }
        }

        public void SetReadyTick(int tick)
        {
            _nextTick = tick;
        }

        public void Tick(int now, bool canCast)
        {
            TickPending(now);

            if (!canCast || _nextTick < 0 || now < _nextTick) { return; }

            _nextTick = Cast(now) ? now + Props.doomWaveIntervalTicks.RandomInRange : now + RetryTicks;
        }

        public void Clear()
        {
            _pendingCells.Clear();
            _pendingTicks.Clear();
        }

        private bool Cast(int now)
        {
            var boss = _comp.Pawn;
            var map = boss.Map;
            var room = Props.maxDoomFlowers - map.listerThings.ThingsOfDef(VVThingDefOf.VV_DoomFlower).Count - _pendingCells.Count;
            var cells = ApocryphaGimmicks.FindDoomBloomCells(map, Mathf.Min(BloomCount, room), _pendingCells);
            if (cells.Count == 0) { return false; }

            var origin = boss.Position.ToVector3Shifted();
            var waveHalfSize = EndingVfxUtility.ShockWaveHalfSize(EndingVfxUtility.MaxDistanceToMapCorner(map, origin));
            EndingVfxUtility.SpawnShockWave(map, origin, VVFleckDefOf.VV_Fleck_GimmickDoomBloomWave, waveHalfSize);

            _comp.SpawnEffecter(VVEffecterDefOf.VV_Effecter_DoomWaveCast, ApocryphaGimmicks.GetBossBodyOffset(boss));
            ApocryphaUtility.ShakeCamera(map, 0.8f);

            for (int i = 0; i < cells.Count; i++)
            {
                _pendingCells.Add(cells[i]);
                _pendingTicks.Add(now + Mathf.Max(1, EndingVfxUtility.ShockWaveArrivalTicks(cells[i].DistanceTo(boss.Position), waveHalfSize, WaveDurationSecs)));
            }

            _wavesCast++;
            return true;
        }

        private void TickPending(int now)
        {
            if (_pendingCells.Count == 0) { return; }

            var boss = _comp.Pawn;
            var map = boss.Map;
            for (int i = _pendingCells.Count - 1; i >= 0; i--)
            {
                if (now < _pendingTicks[i]) { continue; }

                if (ApocryphaGimmicks.TryFindDoomBloomCellNear(_pendingCells[i], map, out var cell))
                {
                    ApocryphaGimmicks.SpawnFlower(ThingMaker.MakeThing(VVThingDefOf.VV_DoomFlower), cell, map, boss);
                }

                _pendingCells.RemoveAt(i);
                _pendingTicks.RemoveAt(i);
            }
        }
    }
}
