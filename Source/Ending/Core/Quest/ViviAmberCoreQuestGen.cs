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
            if (VVThingDefOf.VV_AncientAmberCore == null) { return null; }

            var points = StorytellerUtility.DefaultThreatPointsNow(Find.World);
            var asker = askerFaction?.leader;

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

            var restoreInfo = slate.GetRestoreInfo("inSignal");
            if (!inSignal.NullOrEmpty())
            {
                slate.Set("inSignal", QuestGenUtility.HardcodedSignalWithQuestID(inSignal));
            }

            try
            {
                var reward = new Reward_Items();
                reward.items.Add(ThingMaker.MakeThing(VVThingDefOf.VV_AncientAmberCore));

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
