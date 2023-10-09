using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class QuestNode_Root_Worldtree_MechanoidScoutingBase : QuestNode
    {
        private static readonly SimpleCurve ThreatPointsOverPoints = new SimpleCurve
        {
            new CurvePoint(0f, 500f),
            new CurvePoint(500f, 500f),
            new CurvePoint(10000f, 10000f)
        };

        private const float ThreatPointMultiplier = 1.5f;

        private static readonly IntRange HackDefenceRange = new IntRange(5000, 7000);

        protected override void RunInt()
        {
            var slate = QuestGen.slate;
            var quest = QuestGen.quest;
            var map = QuestGen_Get.GetMap();
            var points = slate.Get("points", 0f);

            TryFindSiteTile(out var tile);
            TryFindEnemyFaction(out var enemyFaction);

            QuestGen.GenerateNewSignal("RaidArrives");

            points *= 3f;

            // 맵 생성
            var terminalQuestTag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID("terminal");
            var signalHacked = QuestGenUtility.HardcodedSignalWithQuestID(terminalQuestTag + ".Hacked");

            var site = QuestGen_Sites.GenerateSite(
                Gen.YieldSingle(new SitePartDefWithParams(
                    VVSitePartDefOf.VV_MechanoidScoutingBase, 
                    new SitePartParams_MechanoidScoutingBase
                    {
                        points = points,
                        threatPoints = ThreatPointsOverPoints.Evaluate(points) * ThreatPointMultiplier,
                        terminalSpawned = (thing) =>
                        {
                            QuestUtility.AddQuestTag(thing, terminalQuestTag);
                            thing.TryGetComp<CompHackable>().defence = (Rand.Chance(0.5f) ? HackDefenceRange.min : HackDefenceRange.max);
                        }
                    })), 
                    tile, 
                    Faction.OfMechanoids);

            quest.SpawnWorldObject(site);

            var component = site.GetComponent<TimedDetectionRaids>();
            if (component != null)
            {
                component.alertRaidsArrivingIn = true;
            }

            // 터미널 해킹 체크
            var signalTerminalHacked = QuestGen.GenerateNewSignal("TerminalHacked");
            var signalQuestCompleted = QuestGen.GenerateNewSignal("AllTerminalsHacked");
            var questPart_PassAllActivable = new QuestPart_PassAllActivable();
            questPart_PassAllActivable.inSignalEnable = QuestGen.slate.Get<string>("inSignal");
            questPart_PassAllActivable.inSignals = new List<string> { signalHacked };
            questPart_PassAllActivable.outSignalAny = signalTerminalHacked;
            questPart_PassAllActivable.outSignalsCompleted.Add(signalQuestCompleted);
            questPart_PassAllActivable.outSignalsCompleted.Add(QuestGen.GenerateNewSignal("OuterNodeCompleted"));
            questPart_PassAllActivable.expiryInfoPartKey = "TerminalsHacked";
            quest.AddPart(questPart_PassAllActivable);
            quest.Message("[terminalHackedMessage]", getLookTargetsFromSignal: true, inSignal: signalTerminalHacked);

            // 보상 등록
            quest.RewardChoice().choices.Add(new QuestPart_Choice.Choice
            {
                rewards =
                {
                    new Reward_AncientWorldtreeLocation() { quest = quest, }
                }
            });

            // 터미널 파괴 체크
            var questPart_Filter_Hacked = new QuestPart_Filter_Hacked()
            {
                inSignal = QuestGenUtility.HardcodedSignalWithQuestID("terminals.Destroyed"),
                outSignalElse = QuestGen.GenerateNewSignal("FailQuestTerminalDestroyed")
            };
            quest.AddPart(questPart_Filter_Hacked);

            // 모든 터미널 해킹시 성공
            quest.End(QuestEndOutcome.Success, 0, null, signalTerminalHacked, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);

            // site가 파괴된 경우 실패
            quest.End(QuestEndOutcome.Fail, 0, null, QuestGenUtility.HardcodedSignalWithQuestID("site.Destroyed"), QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);

            // 터미널이 파괴된 경우 실패
            quest.End(QuestEndOutcome.Fail, 0, null, questPart_Filter_Hacked.outSignalElse, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
            //slate.Set("terminals", complexSketch.thingsToSpawn);
            //slate.Set("terminalCount", complexSketch.thingsToSpawn.Count);
            slate.Set("map", map);
            slate.Set("site", site);
        }

        protected override bool TestRunInt(Slate slate)
        {
            if (TryFindSiteTile(out var _))
            {
                return TryFindEnemyFaction(out _);
            }

            return false;
        }

        private bool TryFindSiteTile(out int tile)
        {
            return TileFinder.TryFindNewSiteTile(out tile, 17, 47);
        }

        private bool TryFindEnemyFaction(out Faction enemyFaction)
        {
            enemyFaction = Find.FactionManager.OfMechanoids;
            return enemyFaction != null;
        }
    }
}
