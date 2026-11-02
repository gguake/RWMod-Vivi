using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ViviEndingEchoLetters : IExposable
    {
        public const int MaxStage = 5;

        private const float CurrentLevelEchoChance = 0.15f;

        private static readonly string[] LabelKeys =
        {
            LocalizeString_Letter.VV_Letter_EverflowerEcho1Label,
            LocalizeString_Letter.VV_Letter_EverflowerEcho2Label,
            LocalizeString_Letter.VV_Letter_EverflowerEcho3Label,
            LocalizeString_Letter.VV_Letter_EverflowerEcho4Label,
            LocalizeString_Letter.VV_Letter_EverflowerEcho5Label,
        };

        private static readonly string[] TextKeys =
        {
            LocalizeString_Letter.VV_Letter_EverflowerEcho1,
            LocalizeString_Letter.VV_Letter_EverflowerEcho2,
            LocalizeString_Letter.VV_Letter_EverflowerEcho3,
            LocalizeString_Letter.VV_Letter_EverflowerEcho4,
            LocalizeString_Letter.VV_Letter_EverflowerEcho5,
        };

        private int _lastStage;

        public int LastStage => _lastStage;

        public void ExposeData()
        {
            Scribe_Values.Look(ref _lastStage, "lastStage", 0);
        }

        public void Notify_EverflowerRitualEcho(ArcanePlant_Everflower everflower, Pawn resonator)
        {
            if (everflower == null || resonator == null) { return; }

            var next = _lastStage + 1;
            if (next > MaxStage) { return; }

            var level = everflower.AttunementLevel;
            if (next > level) { return; }
            if (next == level && next < MaxStage && !Rand.Chance(CurrentLevelEchoChance)) { return; }

            Send(next, everflower, resonator);
        }

        public void DebugSetLastStage(int stage)
        {
            _lastStage = Mathf.Clamp(stage, 0, MaxStage);
        }

        public void DebugSendNext(ArcanePlant_Everflower everflower)
        {
            if (_lastStage >= MaxStage) { return; }

            var resonator = everflower?.EverflowerComp.LinkedPawns.FirstOrDefault(v => v.Spawned && v.IsRoyalVivi());
            if (resonator == null)
            {
                Messages.Message("DEV: no linked royal vivi on the map to receive the echo letter.", everflower, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Send(_lastStage + 1, everflower, resonator);
        }

        private void Send(int stage, ArcanePlant_Everflower everflower, Pawn resonator)
        {
            _lastStage = stage;

            Find.LetterStack.ReceiveLetter(
                LabelKeys[stage - 1].Translate(resonator.Named("PAWN")),
                TextKeys[stage - 1].Translate(resonator.Named("PAWN")),
                LetterDefOf.NeutralEvent,
                new LookTargets(new TargetInfo[] { resonator, everflower }));
        }
    }
}
