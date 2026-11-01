using RimWorld;
using Verse;

namespace VVRace
{
    public partial class ViviEndingQuest
    {
        public const int MaxEchoStage = 5;

        private const float CurrentLevelEchoChance = 0.15f;

        private static readonly string[] EchoLetterLabelKeys =
        {
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho1Label,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho2Label,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho3Label,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho4Label,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho5Label,
        };

        private static readonly string[] EchoLetterTextKeys =
        {
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho1,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho2,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho3,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho4,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho5,
        };

        private int _lastEchoStage;

        public int LastEchoStage => _lastEchoStage;

        public void Notify_EverflowerRitualEcho(ArcanePlant_Everflower everflower, Pawn resonator)
        {
            if (everflower == null || resonator == null) { return; }

            var next = _lastEchoStage + 1;
            if (next > MaxEchoStage) { return; }

            var level = everflower.AttunementLevel;
            if (next > level) { return; }
            if (next == level && next < MaxEchoStage && !Rand.Chance(CurrentLevelEchoChance)) { return; }

            SendEchoLetter(next, everflower, resonator);
        }

        private void SendEchoLetter(int stage, ArcanePlant_Everflower everflower, Pawn resonator)
        {
            _lastEchoStage = stage;

            Find.LetterStack.ReceiveLetter(
                EchoLetterLabelKeys[stage - 1].Translate(resonator.Named("PAWN")),
                EchoLetterTextKeys[stage - 1].Translate(resonator.Named("PAWN")),
                LetterDefOf.NeutralEvent,
                new LookTargets(new TargetInfo[] { resonator, everflower }));
        }
    }
}
