using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace VVRace
{
    public class Reward_AncientWorldtreeLocation : Reward
    {
        private static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("UI/Icons/QuestionMark");

        public Quest quest;

        public override IEnumerable<GenUI.AnonymousStackElement> StackElements
        {
            get
            {
                yield return QuestPartUtility.GetStandardRewardStackElement(
                    LocalizeTexts.RewardAncientWorldtreeLocationLabel.Translate(),
                    Icon,
                    () => GetDescription(default).CapitalizeFirst() + ".");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref quest, "quest");
        }

        public override void InitFromValue(float rewardValue, RewardsGeneratorParams parms, out float valueActuallyUsed)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<QuestPart> GenerateQuestParts(int index, RewardsGeneratorParams parms, string customLetterLabel, string customLetterText, RulePack customLetterLabelRules, RulePack customLetterTextRules)
        {
            throw new NotImplementedException();
        }

        public override string GetDescription(RewardsGeneratorParams parms)
        {
            return LocalizeTexts.RewardAncientWorldtreeLocation.Translate();
        }
    }
}
