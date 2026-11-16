using RimWorld;
using Verse;

namespace VVRace
{
    // 끝난 퀘스트의 부품은 신호·틱을 받지 않으므로 엔딩 이후 편지는 여기서 보낸다.
    public class ViviEndingEpilogue : IExposable
    {
        private static readonly IntRange FederationReactionDelayTicks = new IntRange(60000, 180000);

        private readonly ViviEndingQuest _ending;

        private int _reactionDueTick = -1;
        private bool _reactionSent;
        private bool _witnessSent;

        public bool ReactionSent => _reactionSent;
        public bool WitnessSent => _witnessSent;

        public ViviEndingEpilogue(ViviEndingQuest ending)
        {
            _ending = ending;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _reactionDueTick, "reactionDueTick", -1);
            Scribe_Values.Look(ref _reactionSent, "reactionSent", false);
            Scribe_Values.Look(ref _witnessSent, "witnessSent", false);
        }

        public void Tick(int now)
        {
            if (_reactionSent || !_ending.IsCompleted) { return; }

            if (_reactionDueTick < 0)
            {
                _reactionDueTick = now + FederationReactionDelayTicks.RandomInRange;
                return;
            }

            if (now < _reactionDueTick) { return; }

            var federation = ViviEndingUtility.FindFederation(requireNonHostile: true);
            if (federation == null) { return; }

            SendFederationReaction(federation);
        }

        public void SendFederationReaction(Faction federation)
        {
            _reactionSent = true;

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_FederationReactionLabel.Translate(),
                LocalizeString_Letter.VV_Letter_FederationReaction.Translate(),
                LetterDefOf.PositiveEvent,
                LookTargets.Invalid,
                federation);
        }

        public void Notify_AncientReturned(Pawn pawn)
        {
            if (_witnessSent || pawn == null) { return; }

            _witnessSent = true;

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_AncientWitnessLabel.Translate(),
                LocalizeString_Letter.VV_Letter_AncientWitness.Translate(pawn.Named("PAWN")),
                LetterDefOf.NeutralEvent,
                pawn);
        }
    }
}
