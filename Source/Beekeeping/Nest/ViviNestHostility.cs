using RimWorld;
using Verse;

namespace VVRace
{
    // 둥지 경계심(0~100). 둥지 방 안에 플레이어 폰이 있으면 빠르게 오르고, 아무도 없으면 천천히 내려간다.
    // 물건을 가져가는 것은 상관없다. 가득 차면 임시 세력이 플레이어와 적대가 되고 둥지 로드의 적대 전이로 달아난다.
    // 둥지는 문을 금지하지 않으므로 정착민이 들어가지 않게 막는 것(문 금지·허가구역)은 플레이어 몫이다.
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

        // 둥지 방 칸(벽이 뚫린 동안은 원래 방 칸)에 있는 플레이어 세력 폰(정착민·노예·기계체, 동물 제외) 수. 정착 순간부터 세고,
        // 소집·직접 지시·정신 이상·쓰러짐과 상관없이 모두 센다. 예전에는 둥지가 방 문을 금지하고 소집·직접 지시로 들여보낸 폰만 셌다
        // (스스로 들어온 폰은 나갈 때까지 유예). 사용자가 문 금지를 없애고 플레이어가 조심하는 방향을 골랐다.
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

        // 플레이어가 구성원을 노려 다치게 하면 크게, 빗나간 공격에 맞으면 피해만큼(최대 15) 오른다. 바닐라 호감도가 봐주는 피해는 치지 않는다.
        public void Notify_MemberDamaged(Pawn victim, DamageInfo dinfo)
        {
            if (!dinfo.Def.ExternalViolenceFor(victim) || !LordJob_ViviNest.IsPlayerHarm(victim, dinfo)) { return; }

            Add(LordJob_ViviNest.IsIntended(dinfo, victim) ? HarmGain : UnityEngine.Mathf.Min(AccidentalHarmGain, dinfo.Amount));
        }

        // 쓰러진 구성원을 정착민이 구해 주면, 나아서 둥지로 돌아올 때 손님 신분을 풀고 경계심을 누그러뜨린다.
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

        // 임시 세력을 플레이어와 적대로 돌린다. 숨김 세력은 호감도가 없어 관계를 직접 바꾼다.
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

        // 처음 25·60을 넘을 때 메시지, 85를 넘을 때 편지. 다시 내려가면 다음에 또 알린다.
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
