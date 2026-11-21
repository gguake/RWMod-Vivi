using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaEscortSummon : IExposable
    {
        private const int RecallCheckInterval = 30;
        private const int StunRecoverTicks = 300;
        private const float WispTravelSecs = 0.7f;
        private const float WispRate = 1.4f;

        private static readonly FloatRange WispRadius = new FloatRange(4f, 9f);
        private static readonly FloatRange WispScale = new FloatRange(0.6f, 1.1f);
        private static readonly FloatRange SparkScale = new FloatRange(0.25f, 0.45f);
        private static readonly Color SparkColor = new Color(0.9f, 0.18f, 0.7f, 1f);

        private readonly CompApocrypha _comp;

        private int _nextPeriodicTick = -1;
        private int _recallReadyTick;
        private int _castEndTick = -1;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public bool IsCasting => _castEndTick >= 0;
        public int CastTicksLeft => IsCasting ? Mathf.Max(0, _castEndTick - GenTicks.TicksGame) : 0;
        public int NextPeriodicTick => _nextPeriodicTick;
        public int RecallReadyTick => _recallReadyTick;

        public ApocryphaEscortSummon(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _nextPeriodicTick, "nextPeriodicTick", -1);
            Scribe_Values.Look(ref _recallReadyTick, "recallReadyTick", 0);
            Scribe_Values.Look(ref _castEndTick, "castEndTick", -1);
        }

        public void SetPeriodicTick(int tick)
        {
            _nextPeriodicTick = tick;
        }

        public void SetRecallReadyTick(int tick)
        {
            _recallReadyTick = tick;
        }

        public void Tick(int now, bool stunned)
        {
            if (IsCasting)
            {
                TickCast(now, stunned);
            }

            if (_nextPeriodicTick >= 0 && now >= _nextPeriodicTick)
            {
                _nextPeriodicTick = now + Props.escortSummonIntervalTicks.RandomInRange;
                HornetManifestation.SummonAround(Pawn, _comp.ScaledHornetCount(Props.escortSummonCount), HornetSummonRole.Escort);
            }

            if (IsCasting || stunned || now < _recallReadyTick || !Pawn.IsHashIntervalTick(RecallCheckInterval)) { return; }
            if (!_comp.Targeting.InCombat || _comp.Breath.IsBreathing || _comp.Channel.IsChanneling || _comp.Channel.PendingSteal) { return; }
            if (AnyEscortNear()) { return; }

            StartCast(now);
        }

        public void StartCast(int now)
        {
            _castEndTick = now + Props.escortRecallCastTicks;
            _comp.InterruptCurrentJob();
            _comp.SpawnEffecter(VVEffecterDefOf.VV_Effecter_HornetManifestBegin, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
        }

        public void Cancel()
        {
            _castEndTick = -1;
        }

        private void TickCast(int now, bool stunned)
        {
            if (stunned)
            {
                Cancel();
                _recallReadyTick = now + StunRecoverTicks;
                return;
            }

            if (now >= _castEndTick)
            {
                Cancel();
                _recallReadyTick = now + Props.escortRecallCooldownTicks;
                HornetManifestation.SummonAround(Pawn, _comp.ScaledHornetCount(Props.escortRecallCount), HornetSummonRole.Escort);
                _comp.SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaArrive, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
                return;
            }

            var map = Pawn.Map;
            var body = _comp.BodyCenter;
            if (!body.ShouldSpawnMotesAt(map)) { return; }

            var count = GenMath.RoundRandom(WispRate);
            for (int i = 0; i < count; i++)
            {
                var start = body + EndingVfxUtility.RandomRingOffset(WispRadius);
                ManifestationRenderer.ThrowWispWithSpark(map, start, body, WispTravelSecs, WispScale, 0.25f, SparkScale, SparkColor);
            }
        }

        private bool AnyEscortNear()
        {
            var map = Pawn.Map;
            var center = Pawn.Position;
            var radius = Props.escortRecallRadius;

            foreach (var hornet in ApocryphaUtility.GetEscorts(Pawn))
            {
                if (hornet.Spawned && !hornet.Dead && hornet.Map == map && hornet.Position.InHorDistOf(center, radius)) { return true; }
            }

            var manifestations = map.listerThings.ThingsOfDef(VVThingDefOf.VV_HornetManifestation);
            for (int i = 0; i < manifestations.Count; i++)
            {
                if (((HornetManifestation)manifestations[i]).IsEscortOf(Pawn) && manifestations[i].Position.InHorDistOf(center, radius)) { return true; }
            }

            return false;
        }
    }
}
