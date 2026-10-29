using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Grammar;

namespace VVRace
{
    // 고대 호박 코어 의뢰 퀘스트 생성. 무작위로 고를 수 있는 루트 퀘스트(바닐라·모드) 하나를 3성으로 만들고,
    // 생성하는 동안만 바닐라 보상 생성(QuestGen_Rewards.GiveRewards, EndingCorePatch의 prefix)을 가로채 보상을 고대 호박 코어 1개로 바꾼다.
    // 3성이 아니거나 보상을 두 번 이상 주는(결과마다 보상이 다른) 퀘스트, 보상이 없는 퀘스트, 의뢰인이 연방이 아닌 퀘스트
    // (예: 칭호 있는 의뢰인이 필요한 제국 접대)는 버리고 다른 퀘스트로 다시 시도한다.
    public static class ViviAmberCoreQuestGen
    {
        public const int ChallengeRating = 3;
        private const int MaxAttempts = 20;

        // 의뢰 후보에서 빼는 퀘스트: 제국 기념비 건설(Royalty).
        private static readonly HashSet<string> ExcludedScripts = new HashSet<string>()
        {
            "BuildMonument_Basic",
            "BuildMonument_TimeProtect",
        };

        private static bool _generating;
        private static bool _rewardGiven;
        private static bool _rejected;
        private static Faction _askerFaction;

        // 의뢰 퀘스트를 만드는 중이면 GiveRewards prefix가 보상을 코어로 바꾼다.
        internal static bool Generating => _generating;

        // 의뢰 퀘스트를 만든다(퀘스트 매니저에는 넣지 않음). 맡길 퀘스트를 찾지 못하면 null.
        // askerFaction이 있으면 그 지도자를 의뢰인으로 미리 정한다(바닐라 QuestNode_GetPawn은 미리 정한 폰이 조건에 맞으면 그대로 쓴다).
        public static Quest TryGenerate(Faction askerFaction)
        {
            if (EndingCoreDefOf.VV_AncientAmberCore == null) { return null; }

            var points = StorytellerUtility.DefaultThreatPointsNow(Find.World);
            var asker = askerFaction?.leader;

            // 코어 보상을 실을 수 없는 퀘스트는 미리 뺀다: 합류형(보상이 폰 합류라 보상 생성을 부르지 않음),
            // 3성이 아닌 고정 난이도(defaultChallengeRating은 슬레이트 별 가중치로 바뀌지 않음).
            var candidates = DefDatabase<QuestScriptDef>.AllDefsListForReading
                .Where(v => v.IsRootRandomSelected
                    && !ExcludedScripts.Contains(v.defName)
                    && !v.rootIncreasesPopulation
                    && (v.defaultChallengeRating <= 0 || v.defaultChallengeRating == ChallengeRating)
                    && v.CanRun(MakeSlate(points, asker), Find.World))
                .ToList();

            _askerFaction = askerFaction;
            try
            {
                for (int attempt = 0; attempt < MaxAttempts && candidates.Count > 0; ++attempt)
                {
                    var script = ChooseScript(candidates, points);
                    candidates.Remove(script);

                    var quest = GenerateWithCoreReward(script, MakeSlate(points, asker));
                    if (quest != null) { return quest; }
                }
            }
            finally
            {
                _askerFaction = null;
            }

            return null;
        }

        private static Slate MakeSlate(float points, Pawn asker)
        {
            var slate = new Slate();
            slate.Set("points", points);
            if (asker != null)
            {
                slate.Set("asker", asker);
            }

            // 바닐라 Util_RandomizePointsChallengeRating은 이미 정해진 별 가중치를 덮지 않으므로 3성만 남긴다.
            slate.Set("selectionWeightOneStar", 0f);
            slate.Set("selectionWeightTwoStar", 0f);
            slate.Set("selectionWeightThreeStar", 1f);
            return slate;
        }

        // 바닐라 자연 선택 가중치(최근 퀘스트·재발생 간격 반영)로 고른다. 모두 0이면 기본 가중치로 고른다.
        private static QuestScriptDef ChooseScript(List<QuestScriptDef> candidates, float points)
        {
            var storyState = Find.World.StoryState;
            if (candidates.TryRandomElementByWeight(v => NaturalRandomQuestChooser.GetNaturalRandomSelectionWeight(v, points, storyState), out var chosen))
            {
                return chosen;
            }

            return candidates.RandomElementByWeight(v => Math.Max(v.rootSelectionWeight, 0.01f));
        }

        private static Quest GenerateWithCoreReward(QuestScriptDef script, Slate slate)
        {
            Quest quest;
            _generating = true;
            _rewardGiven = false;
            _rejected = false;
            try
            {
                quest = QuestGen.Generate(script, slate);
            }
            finally
            {
                _generating = false;
            }

            // 버린 퀘스트는 퀘스트 매니저에 넣지 않으므로 아무 일도 일으키지 않는다(바닐라 개발자 도구의 보상 표본 생성과 같음).
            if (quest == null || quest.challengeRating != ChallengeRating || !_rewardGiven || _rejected)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[ViViRace] amber core quest: discarded {script.defName} (generated: {quest != null}, challenge rating: {quest?.challengeRating}, core reward: {_rewardGiven}, rejected: {_rejected}).");
                }
                return null;
            }

            return quest;
        }

        // [EndingCorePatch prefix] 바닐라 GiveRewards 대신 보상 선택지를 코어 1개로 만든다. 바꿨으면 true(바닐라를 건너뜀).
        // 두 번째 보상 호출(결과마다 보상이 다른 퀘스트), 받을 맵이 없는 보상, 연방이 아닌 세력이 의뢰인인 퀘스트면
        // 바닐라에 맡기고 그 퀘스트를 버린다.
        internal static bool TryGiveCoreReward(Quest quest, RewardsGeneratorParams parms, string inSignal, string customLetterLabel,
            string customLetterText, RulePack customLetterLabelRules, RulePack customLetterTextRules, out QuestPart_Choice result)
        {
            result = null;
            var slate = QuestGen.slate;
            var asker = slate.Get<Pawn>("asker");
            if (_rewardGiven
                || (!parms.giveToCaravan && slate.Get<Map>("map") == null)
                || (_askerFaction != null && asker != null && asker.Faction != _askerFaction))
            {
                _rejected = true;
                return false;
            }

            // 바닐라처럼 보상 신호를 잠시 슬레이트 inSignal에 넣는다(Reward_Items.GenerateQuestParts가 읽는다).
            var restoreInfo = slate.GetRestoreInfo("inSignal");
            if (!inSignal.NullOrEmpty())
            {
                slate.Set("inSignal", QuestGenUtility.HardcodedSignalWithQuestID(inSignal));
            }

            try
            {
                var reward = new Reward_Items();
                reward.items.Add(ThingMaker.MakeThing(EndingCoreDefOf.VV_AncientAmberCore));

                var choice = new QuestPart_Choice.Choice();
                choice.rewards.Add(reward);
                foreach (var part in reward.GenerateQuestParts(0, parms, customLetterLabel, customLetterText, customLetterLabelRules, customLetterTextRules))
                {
                    quest.AddPart(part);
                    choice.questParts.Add(part);
                }

                result = new QuestPart_Choice()
                {
                    inSignalChoiceUsed = slate.Get<string>("inSignal"),
                };
                result.choices.Add(choice);
                quest.AddPart(result);

                // 바닐라 보상 설명 규칙(물품 보상 하나뿐일 때와 같은 꼴).
                var description = reward.GetDescription(parms).UncapitalizeFirst().TrimEnd('.');
                QuestGen.AddQuestDescriptionRules(new List<Rule>()
                {
                    new Rule_String("allRewardsDescriptions", description),
                    new Rule_String("allRewardsDescriptionsExceptItems", ""),
                });

                _rewardGiven = true;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] amber core quest: failed to give the core reward in {quest?.root?.defName}: {ex}");
                _rejected = true;
                result = null;
                return false;
            }
            finally
            {
                slate.Restore(restoreInfo);
            }
        }
    }
}
