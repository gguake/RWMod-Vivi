using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairySigilWard : Thing
    {
        public const float OrbitRadius = 3.5f;

        private const int FadeTicks = 60;
        private const float Intensity = 0.22f;

        private int _startTick = -1;
        private int _expireTick = -1;
        private List<ViviFairy> _fairies = new List<ViviFairy>();
        [Unsaved]
        private int _startDelayTicks = Building_FairyFlower.BurstTicks;

        internal void InitStartDelay(int ticks)
        {
            _startDelayTicks = Mathf.Max(0, ticks);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad || _expireTick >= 0) { return; }

            var boss = ApocryphaGimmicks.FindBoss(map);
            var props = boss?.TryGetComp<CompApocrypha>()?.Props;
            if (props == null) { return; }

            var now = Find.TickManager.TicksGame;
            _startTick = now + _startDelayTicks;
            _expireTick = now + Mathf.Max(FadeTicks + 1, props.sigilWardDurationTicks);

            for (int i = 0; i < props.sigilWardFairyCount; i++)
            {
                _fairies.Add(ApocryphaFairySwarm.SpawnFairy(boss, Position, this));
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var now = Find.TickManager.TicksGame;
            if (now >= _expireTick)
            {
                Destroy(DestroyMode.Vanish);
            }
            else if (now >= _expireTick - FadeTicks)
            {
                DisperseFairies();
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            DisperseFairies();
            base.Destroy(mode);
        }

        private void DisperseFairies()
        {
            for (int i = 0; i < _fairies.Count; i++)
            {
                if (_fairies[i] != null && !_fairies[i].Destroyed)
                {
                    _fairies[i].BeginDematerialize(applyAssimilation: false);
                }
            }

            _fairies.Clear();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var now = Find.TickManager.TicksGame;
            var fade = Mathf.Min(Mathf.InverseLerp(_startTick, _startTick + FadeTicks, now), Mathf.InverseLerp(_expireTick, _expireTick - FadeTicks, now));
            FairySigilRenderer.Draw(drawLoc, FairySigil.Daffodil, Building_FairyFlower.FairyCount, 1f, Intensity * fade);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _startTick, "startTick", -1);
            Scribe_Values.Look(ref _expireTick, "expireTick", -1);
            Scribe_Collections.Look(ref _fairies, "fairies", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _fairies ??= new List<ViviFairy>();
                _fairies.RemoveAll(v => v == null);
            }
        }
    }
}
