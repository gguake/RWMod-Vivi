using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class Reward_PurgeOutlanders : Reward
    {
        private static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("UI/Icons/Stars");

        public override IEnumerable<GenUI.AnonymousStackElement> StackElements
        {
            get
            {
                yield return QuestPartUtility.GetStandardRewardStackElement(
                    LocalizeString_Etc.VV_Reward_PurgeOutlanders.Translate(), 
                    Icon, 
                    () => GetDescription(default).CapitalizeFirst() + ".");
            }
        }
        public override string GetDescription(RewardsGeneratorParams parms)
        {
            return LocalizeString_Etc.VV_Reward_PurgeOutlandersDesc.Translate().Resolve();
        }

        public override IEnumerable<QuestPart> GenerateQuestParts(int index, RewardsGeneratorParams parms, string customLetterLabel, string customLetterText, RulePack customLetterLabelRules, RulePack customLetterTextRules)
        {
            throw new NotImplementedException();
        }

        public override void InitFromValue(float rewardValue, RewardsGeneratorParams parms, out float valueActuallyUsed)
        {
            throw new NotImplementedException();
        }
    }
}
