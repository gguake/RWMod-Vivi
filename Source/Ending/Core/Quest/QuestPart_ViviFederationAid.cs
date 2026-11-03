using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    // 바닐라 스토리텔러의 동맹 지원(StorytellerComp_FactionInteraction + RaidFriendly)과 같은 구조로 동작한다.
    // 앵커 맵의 위험도가 높을 때만 연방 지원군을 부르고, 구성과 규모는 RaidFriendly가 연방 전투 그룹으로 현재 위협 포인트만큼 생성한다.
    public class QuestPart_ViviFederationAid : QuestPartActivable
    {
        // 지원은 위협이 있으면 반드시 오므로 위협 검사를 드물게 해서 도착 시점이 들쭉날쭉하게 한다.
        // 위협을 발견하면 요청이 연방에 닿아 출발하기까지 1~2시간 뒤 도착하고, 그때 위협이 사라졌으면 오지 않는다.
        private const int CheckIntervalTicks = 10000;
        private const int MinSpacingTicks = 24000;
        private const float MinActiveThreatCombatPower = 120f;
        private static readonly IntRange ArrivalDelayTicks = new IntRange(2500, 5000);

        public ArcanePlant_Everflower anchor;

        private int _arrivalTick = -1;
        private int _cooldownUntilTick = -1;

        private Map AnchorMap => anchor != null && !anchor.Destroyed ? anchor.MapHeld : null;

        public override void QuestPartTick()
        {
            base.QuestPartTick();

            var now = Find.TickManager.TicksGame;
            if (_arrivalTick >= 0)
            {
                if (now < _arrivalTick) { return; }

                _arrivalTick = -1;
                var arrivalMap = AnchorMap;
                if (arrivalMap != null && IsUnderThreat(arrivalMap) && TrySendReinforcements(arrivalMap))
                {
                    _cooldownUntilTick = now + MinSpacingTicks;
                }
                return;
            }

            if (now % CheckIntervalTicks != 0 || now < _cooldownUntilTick) { return; }

            var map = AnchorMap;
            if (map != null && IsUnderThreat(map))
            {
                _arrivalTick = now + ArrivalDelayTicks.RandomInRange;
            }
        }

        // DangerWatcher의 High 판정과 RaidFriendly.CanFireNowSub의 활성 위협 전투력 조건을 함께 본다.
        private static bool IsUnderThreat(Map map)
        {
            if (map.dangerWatcher.DangerRating != StoryDanger.High) { return false; }

            var combatPower = map.attackTargetsCache.TargetsHostileToColony
                .Where(t => GenHostility.IsActiveThreatToPlayer(t))
                .Sum(t => t is Pawn pawn ? pawn.kindDef.combatPower : 0f);
            return combatPower > MinActiveThreatCombatPower;
        }

        private bool TrySendReinforcements(Map map)
        {
            var federation = ViviEndingUtility.FindFederation(requireNonHostile: true);
            if (federation == null) { return false; }

            // 도착 방식은 스토리텔러 지원처럼 습격 전략이 고르게 둔다(중앙 드롭포드는 포인트가 0.3~0.5배로 깎인다).
            var parms = new IncidentParms
            {
                target = map,
                faction = federation,
                points = StorytellerUtility.DefaultThreatPointsNow(map),
                quest = quest,
                customLetterLabel = LocalizeString_Letter.VV_Letter_EndingFederationAidLabel.Translate(),
                customLetterText = LocalizeString_Letter.VV_Letter_EndingFederationAid.Translate(),
            };

            return IncidentDefOf.RaidFriendly.Worker.TryExecute(parms);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref _arrivalTick, "arrivalTick", -1);
            Scribe_Values.Look(ref _cooldownUntilTick, "cooldownUntilTick", -1);
        }
    }
}
