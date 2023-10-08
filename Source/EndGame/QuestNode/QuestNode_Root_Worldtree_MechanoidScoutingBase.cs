using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class QuestNode_Root_Worldtree_MechanoidScoutingBase : QuestNode
    {
        private static readonly SimpleCurve ExteriorThreatPointsOverPoints = new SimpleCurve
        {
            new CurvePoint(0f, 500f),
            new CurvePoint(500f, 500f),
            new CurvePoint(10000f, 10000f)
        };

        private static readonly SimpleCurve InteriorThreatPointsOverPoints = new SimpleCurve
        {
            new CurvePoint(0f, 300f),
            new CurvePoint(300f, 300f),
            new CurvePoint(10000f, 5000f)
        };

        private static readonly IntRange HackDefenceRange = new IntRange(1800, 3000);

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
            var complexSketch = GenerateSketch(points);
            complexSketch.thingDiscoveredMessage = LocalizeTexts.MessageAncientTerminalWorldtreeDiscovered.Translate();

            var list = new List<string>();
            for (int i = 0; i < complexSketch.thingsToSpawn.Count; i++)
            {
                var thing = complexSketch.thingsToSpawn[i];
                var terminalQuestTag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID("terminal" + i);
                QuestUtility.AddQuestTag(thing, terminalQuestTag);

                var signalHacked = QuestGenUtility.HardcodedSignalWithQuestID(terminalQuestTag + ".Hacked");
                list.Add(signalHacked);

                thing.TryGetComp<CompHackable>().defence = (Rand.Chance(0.5f) ? HackDefenceRange.min : HackDefenceRange.max);
            }

            var site = QuestGen_Sites.GenerateSite(
                Gen.YieldSingle(new SitePartDefWithParams(
                    VVSitePartDefOf.VV_MechanoidScoutingBase, 
                    new SitePartParams
                    {
                        ancientComplexSketch = complexSketch,
                        ancientComplexRewardMaker = VVThingSetMakerDefOf.VV_MapGen_EndGame_AncientComplexRoomLoot,
                        points = points,
                        threatPoints = InteriorThreatPointsOverPoints.Evaluate(points),
                        exteriorThreatPoints = ExteriorThreatPointsOverPoints.Evaluate(points),
                        interiorThreatPoints = InteriorThreatPointsOverPoints.Evaluate(points),
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
            var signalAllTerminalHacked = QuestGen.GenerateNewSignal("AllTerminalsHacked");
            var questPart_PassAllActivable = new QuestPart_PassAllActivable();
            questPart_PassAllActivable.inSignalEnable = QuestGen.slate.Get<string>("inSignal");
            questPart_PassAllActivable.inSignals = list;
            questPart_PassAllActivable.outSignalAny = signalTerminalHacked;
            questPart_PassAllActivable.outSignalsCompleted.Add(signalAllTerminalHacked);
            questPart_PassAllActivable.outSignalsCompleted.Add(QuestGen.GenerateNewSignal("OuterNodeCompleted"));
            questPart_PassAllActivable.expiryInfoPartKey = "TerminalsHacked";
            quest.AddPart(questPart_PassAllActivable);
            quest.Message("[terminalHackedMessage]", getLookTargetsFromSignal: true, inSignal: signalTerminalHacked);
            quest.Message("[allTerminalsHackedMessage]", MessageTypeDefOf.PositiveEvent, inSignal: signalAllTerminalHacked);

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
            quest.End(QuestEndOutcome.Success, 0, null, signalAllTerminalHacked, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);

            // site가 파괴된 경우 실패
            quest.End(QuestEndOutcome.Fail, 0, null, QuestGenUtility.HardcodedSignalWithQuestID("site.Destroyed"), QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);

            // 터미널이 파괴된 경우 실패
            quest.End(QuestEndOutcome.Fail, 0, null, questPart_Filter_Hacked.outSignalElse, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
            slate.Set("terminals", complexSketch.thingsToSpawn);
            slate.Set("terminalCount", complexSketch.thingsToSpawn.Count);
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

        private ComplexSketch GenerateSketch(float points)
        {
            var complexSize = (int)new SimpleCurve
            {
                new CurvePoint(0f, 10f),
                new CurvePoint(10000f, 30f)

            }.Evaluate(points);

            var complexSketch = VVComplexDefOf.VV_MechanoidScoutingBase.Worker.GenerateSketch(new IntVec2(complexSize, complexSize));
            var terminalCount = Mathf.FloorToInt(new SimpleCurve
                {
                    new CurvePoint(0f, 1f),
                    new CurvePoint(10f, 4f),
                    new CurvePoint(20f, 6f),
                    new CurvePoint(50f, 10f)

                }.Evaluate(complexSketch.layout.Rooms.Count));

            for (int i = 0; i < terminalCount; i++)
            {
                complexSketch.thingsToSpawn.Add(ThingMaker.MakeThing(ThingDefOf.AncientTerminal));
            }

            return complexSketch;
        }

        private bool TryFindSiteTile(out int tile)
        {
            return TileFinder.TryFindNewSiteTile(out tile, 10, 30);
        }

        private bool TryFindEnemyFaction(out Faction enemyFaction)
        {
            enemyFaction = Find.FactionManager.OfMechanoids;
            return enemyFaction != null;
        }
    }
}
