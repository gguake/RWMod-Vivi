using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Grammar;

namespace VVRace
{
    public static class ViviAmberCoreQuestGen
    {
        public const int ChallengeRating = 3;
        private const int MaxAttempts = 20;

        private static readonly HashSet<string> ExcludedScripts = new HashSet<string>()
        {
            "BuildMonument_Basic",
            "BuildMonument_TimeProtect",
        };

        private static bool _generating;
        private static bool _rewardGiven;
        private static bool _rejected;
        private static Faction _askerFaction;

        internal static bool Generating => _generating;

        public static Quest TryGenerate(Faction askerFaction)
        {
            if (EndingCoreDefOf.VV_AncientAmberCore == null) { return null; }

            var points = StorytellerUtility.DefaultThreatPointsNow(Find.World);
            var asker = askerFaction?.leader;

            // 합류형은 보상 생성을 호출하지 않아 제외한다.
            // 고정 난이도는 별 가중치를 무시하므로 3성이 아닌 경우에만 제외한다.
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

            // Util_RandomizePointsChallengeRating은 기존 별 가중치를 덮어쓰지 않으므로 3성 가중치만 남긴다.
            slate.Set("selectionWeightOneStar", 0f);
            slate.Set("selectionWeightTwoStar", 0f);
            slate.Set("selectionWeightThreeStar", 1f);
            return slate;
        }

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

            // 바닐라 개발자 도구의 보상 표본처럼 퀘스트 매니저에 등록하지 않으면 버린 퀘스트는 실행되지 않는다.
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

            // Reward_Items.GenerateQuestParts가 읽을 수 있도록 보상 신호를 슬레이트의 inSignal에 임시 저장한다.
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
