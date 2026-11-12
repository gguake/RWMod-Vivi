using RimWorld;
using Verse;

namespace VVRace
{
    public class ViviNestHostility : IExposable
    {
        public const int CheckInterval = 250;
        public const float Max = 100f;
        public const float IntruderGainPerCheck = 10f;
        public const float DecayPerDay = 20f;
        public const float HarmGain = 50f;
        public const float AccidentalHarmGain = 15f;
        public const float RescueRelief = 20f;
        public const float FirstWarning = 25f;
        public const float SecondWarning = 60f;
        public const float LetterWarning = 85f;

        private readonly LordJob_ViviNest _owner;

        private float _level;
        private int _warned;

        public ViviNestHostility(LordJob_ViviNest owner)
        {
            _owner = owner;
        }

        public float Level => _level;

        public void Tick()
        {
            var intruders = CountIntruders();
            if (intruders > 0)
            {
                Add(intruders * IntruderGainPerCheck);
            }
            else
            {
                _level = UnityEngine.Mathf.Max(0f, _level - DecayPerDay * CheckInterval / GenDate.TicksPerDay);
            }

            WelcomeBackRescued();
            UpdateWarnings();
        }

        private int CountIntruders()
        {
            var territory = _owner.Territory;
            var count = 0;
            foreach (var pawn in _owner.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (pawn.RaceProps.Animal || pawn.Dead) { continue; }
                if (territory.IsNestRoomCell(pawn.Position)) { count++; }
            }
            return count;
        }

        public void Notify_MemberDamaged(Pawn victim, DamageInfo dinfo)
        {
            if (!dinfo.Def.ExternalViolenceFor(victim) || !LordJob_ViviNest.IsPlayerHarm(victim, dinfo)) { return; }

            Add(LordJob_ViviNest.IsIntended(dinfo, victim) ? HarmGain : UnityEngine.Mathf.Min(AccidentalHarmGain, dinfo.Amount));
        }

        private void WelcomeBackRescued()
        {
            var pawns = _owner.lord.ownedPawns;
            for (int i = 0; i < pawns.Count; ++i)
            {
                var pawn = pawns[i];
                if (pawn.HostFaction != Faction.OfPlayer || pawn.Downed || pawn.InBed()) { continue; }

                pawn.guest.SetGuestStatus(null);
                _level = UnityEngine.Mathf.Max(0f, _level - RescueRelief);
                Messages.Message(LocalizeString_Message.VV_Message_ViviNestRescuedReturned.Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        private void Add(float amount)
        {
            if (_owner.Faction.HostileTo(Faction.OfPlayer)) { return; }

            _level = UnityEngine.Mathf.Min(Max, _level + amount);
            if (_level >= Max)
            {
                TurnHostile();
            }
        }

        public void TurnHostile()
        {
            var faction = _owner.Faction;
            if (faction == null || faction.HostileTo(Faction.OfPlayer)) { return; }

            _level = Max;
            faction.SetRelationDirect(Faction.OfPlayer, FactionRelationKind.Hostile, canSendHostilityLetter: false);
            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ViviNestHostileLabel.Translate(),
                LocalizeString_Letter.VV_Letter_ViviNestHostile.Translate(faction.Name),
                LetterDefOf.NegativeEvent,
                new LookTargets(_owner.lord.ownedPawns));
        }

        private void UpdateWarnings()
        {
            var target = new LookTargets(_owner.lord.ownedPawns);
            Warn(1, FirstWarning, () => Messages.Message(LocalizeString_Message.VV_Message_ViviNestWary.Translate(_owner.Faction.Name), target, MessageTypeDefOf.CautionInput));
            Warn(2, SecondWarning, () => Messages.Message(LocalizeString_Message.VV_Message_ViviNestVeryWary.Translate(_owner.Faction.Name), target, MessageTypeDefOf.ThreatSmall));
            Warn(4, LetterWarning, () => Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ViviNestAlmostHostileLabel.Translate(),
                LocalizeString_Letter.VV_Letter_ViviNestAlmostHostile.Translate(_owner.Faction.Name),
                LetterDefOf.ThreatSmall,
                target));
        }

        private void Warn(int flag, float threshold, System.Action notify)
        {
            if (_level >= threshold)
            {
                if ((_warned & flag) == 0)
                {
                    _warned |= flag;
                    notify();
                }
            }
            else if (_level < threshold * 0.8f)
            {
                _warned &= ~flag;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _level, "level");
            Scribe_Values.Look(ref _warned, "warned");
        }
    }
}
