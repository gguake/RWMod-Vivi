using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class GerminateScheduleDef : Def
    {
        public int uiOrder;
        public Color uiColor = Color.white;
        public Color uiTextColor = Color.white;
        [NoTranslate]
        public string uiIconPath;

        [Unsaved(false)]
        public Texture2D uiIcon = BaseContent.BadTex;

        public int workAmount;
        public List<ThingDefCountClass> ingredients;

        public JobDef germinateJob;

        public FloatRange bonusAddGerminateResult = FloatRange.Zero;
        public FloatRange bonusMultiplierGerminateSuccessChance = FloatRange.Zero;
        public FloatRange bonusMultiplierGerminateRareChance = FloatRange.Zero;
        public FloatRange bonusMutateAnotherArcanePlantChance = FloatRange.Zero;

        public SimpleCurve curveQualityMinBySkillLevel;
        public SimpleCurve curveQualityMaxBySkillLevel;

        public override void PostLoad()
        {
            base.PostLoad();
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                if (!uiIconPath.NullOrEmpty())
                {
                    uiIcon = ContentFinder<Texture2D>.Get(uiIconPath);
                }
            });
        }
    }
}
