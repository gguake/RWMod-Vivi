using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class QuestPart_ViviFederationAid : QuestPartActivable
    {
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
