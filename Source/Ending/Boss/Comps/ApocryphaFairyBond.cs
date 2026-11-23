using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaFairyBond : IExposable
    {
        private const int DrainStreamTicks = 30;
        private const int DecayStreamInterval = 7;
        private const int DrainStreamInterval = 3;
        private const int RecoverStreamInterval = 2;
        private const int RecoverFlowTicks = 150;
        private const float StreamSpeed = 7f;
        private const float SourceJitter = 0.8f;
        private const float TargetJitter = 1.4f;

        private static readonly FloatRange DecayScale = new FloatRange(0.35f, 0.55f);
        private static readonly FloatRange DrainScale = new FloatRange(0.45f, 0.75f);
        private static readonly FloatRange RecoverScale = new FloatRange(0.4f, 0.7f);
        private static readonly Color FairyLightColor = new Color(1f, 0.97f, 0.86f, 1f);
        private static readonly Color RecoverLightColor = new Color(1f, 0.9f, 0.62f, 1f);

        private readonly CompApocrypha _comp;

        private float _bond = -1f;
        private int _lastDrainTick = -99999;
        private int _recoverFlowUntilTick = -1;

        private CompProperties_Apocrypha Props => _comp.Props;

        public float Max => Props.fairyBondInitial * _comp.FairyBondDifficultyFactor;
        public float Value => Mathf.Max(0f, _bond);
        public float Fraction => Max > 0f ? Mathf.Clamp01(Value / Max) : 0f;
        public bool Draining => GenTicks.TicksGame - _lastDrainTick <= DrainStreamTicks;

        public float DecayPerHour
        {
            get
            {
                var fraction = Fraction;
                if (fraction >= Props.fairyBondDecayStart) { return 0f; }

                var t = Mathf.Clamp01((Props.fairyBondDecayStart - fraction) / Props.fairyBondDecayRampWidth);
                return Props.fairyBondInitial * Props.fairyBondDecayPerHour.LerpThroughRange(t);
            }
        }

        public ApocryphaFairyBond(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _bond, "bond", -1f);
            Scribe_Values.Look(ref _lastDrainTick, "lastDrainTick", -99999);
            Scribe_Values.Look(ref _recoverFlowUntilTick, "recoverFlowUntilTick", -1);
        }

        public void Reset()
        {
            _bond = Max;
            _lastDrainTick = -99999;
            _recoverFlowUntilTick = -1;
        }

        public void Tick(int now)
        {
            var decayPerHour = DecayPerHour;
            if (decayPerHour > 0f)
            {
                _bond = Mathf.Max(0f, _bond - decayPerHour / GenDate.TicksPerHour);
                CheckExhausted();
            }

            TickStream(now, decayPerHour > 0f);
        }

        public void Drain(float amount)
        {
            if (amount <= 0f) { return; }

            _bond = Mathf.Max(0f, _bond - amount);
            _lastDrainTick = GenTicks.TicksGame;
            CheckExhausted();
        }

        public void DrainMaxFraction(float fraction)
        {
            if (fraction <= 0f) { return; }

            Drain(Max * fraction);
        }

        public void Recover(float fraction)
        {
            if (fraction <= 0f || _bond >= Max) { return; }

            _bond = Mathf.Min(Max, _bond + Props.fairyBondInitial * fraction);
            _recoverFlowUntilTick = GenTicks.TicksGame + RecoverFlowTicks;
        }

        public void RecoverMaxFraction(float fraction, bool showStream = true)
        {
            if (fraction <= 0f || _bond >= Max) { return; }

            _bond = Mathf.Min(Max, _bond + Max * fraction);
            if (showStream)
            {
                _recoverFlowUntilTick = GenTicks.TicksGame + RecoverFlowTicks;
            }
        }

        private void CheckExhausted()
        {
            if (Fraction <= Props.fairyBondClear)
            {
                _comp.Notify_BondExhausted();
            }
        }

        private void TickStream(int now, bool decaying)
        {
            var map = _comp.Pawn.Map;
            if (!ApocryphaGimmicks.TryGetEverflowerBudPos(map, out var bud)) { return; }

            var body = _comp.BodyCenter.Yto0();
            if (Draining)
            {
                if (now % DrainStreamInterval == 0)
                {
                    ApocryphaGimmicks.ThrowFairyLight(map, body + Gen.RandomHorizontalVector(SourceJitter), bud + Gen.RandomHorizontalVector(TargetJitter), StreamSpeed * Rand.Range(0.85f, 1.15f), DrainScale.RandomInRange, FairyLightColor);
                }
            }
            else if (decaying && now % DecayStreamInterval == 0)
            {
                ApocryphaGimmicks.ThrowFairyLight(map, body + Gen.RandomHorizontalVector(SourceJitter), bud + Gen.RandomHorizontalVector(TargetJitter), StreamSpeed * Rand.Range(0.8f, 1.1f), DecayScale.RandomInRange, FairyLightColor);
            }

            if (now < _recoverFlowUntilTick && now % RecoverStreamInterval == 0)
            {
                ApocryphaGimmicks.ThrowFairyLight(map, bud + Gen.RandomHorizontalVector(TargetJitter), body + Gen.RandomHorizontalVector(SourceJitter), StreamSpeed * Rand.Range(0.9f, 1.2f), RecoverScale.RandomInRange, RecoverLightColor);
            }
        }
    }
}
