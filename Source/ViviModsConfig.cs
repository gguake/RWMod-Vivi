using System;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class VVRaceModSettings : ModSettings
    {
        public bool allowSelectModGenes = false;
        public float royalJellyMultiplier = 1f;
        public bool alwaysShowManaIfSelected = true;
        public float manaGridOpacity = 0.5f;
        public bool useVanillaHeadOnly = false;
        public bool randomGenesForStartingVivi = false;
        public float viviMealContinuationNutritionGap = 0.85f;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref allowSelectModGenes, "allowSelectModGenes", defaultValue: false);
            Scribe_Values.Look(ref royalJellyMultiplier, "royalJellyMultiplier", defaultValue: 1f);
            Scribe_Values.Look(ref alwaysShowManaIfSelected, "alwaysShowManaIfSelected", defaultValue: true);
            Scribe_Values.Look(ref manaGridOpacity, "manaGridOpacity", defaultValue: 0.5f);
            Scribe_Values.Look(ref useVanillaHeadOnly, "useVanillaHeadOnly", defaultValue: false);
            Scribe_Values.Look(ref randomGenesForStartingVivi, "randomGenesForStartingVivi", defaultValue: false);
            Scribe_Values.Look(ref viviMealContinuationNutritionGap, "viviMealContinuationNutritionGap", defaultValue: 0.85f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (royalJellyMultiplier < 1f)
                {
                    royalJellyMultiplier = 1f;
                }

                viviMealContinuationNutritionGap = Mathf.Clamp(viviMealContinuationNutritionGap, 0.05f, 2f);
            }
        }
    }

    public class VVRaceMod : Mod
    {
        public Action OnWriteSettings;

        public VVRaceMod(ModContentPack content) : base(content)
        {
            ViviHarmonyPatcher.PrePatchAll();

            _settings = base.GetSettings<VVRaceModSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled(
                LocalizeString_Etc.VV_ModSettings_AllowSelectModGenes.Translate(),
                ref _settings.allowSelectModGenes,
                LocalizeString_Etc.VV_ModSettings_AllowSelectModGenesDesc.Translate());

            _settings.royalJellyMultiplier = listing.SliderLabeled(
                LocalizeString_Etc.VV_ModSettings_RoyalJellyNeedMultiplier.Translate(), 
                _settings.royalJellyMultiplier, 
                1f, 
                10f, 
                tooltip: LocalizeString_Etc.VV_ModSettings_RoyalJellyNeedMultiplierDesc.Translate());

            listing.CheckboxLabeled(
                LocalizeString_Etc.VV_ModSettings_AlwaysShowManaGridOnSelect.Translate(),
                ref _settings.alwaysShowManaIfSelected,
                LocalizeString_Etc.VV_ModSettings_AlwaysShowManaGridOnSelectDesc.Translate());

            _settings.manaGridOpacity = listing.SliderLabeled(
                LocalizeString_Etc.VV_ModSettings_ManaGridOpacity.Translate(),
                _settings.manaGridOpacity,
                0.1f,
                0.5f,
                tooltip: LocalizeString_Etc.VV_ModSettings_ManaGridOpacityDesc.Translate());

            listing.CheckboxLabeled(
                LocalizeString_Etc.VV_ModSettings_UseVanillaHeadOnly.Translate(),
                ref _settings.useVanillaHeadOnly,
                LocalizeString_Etc.VV_ModSettings_UseVanillaHeadOnlyDesc.Translate());

            listing.CheckboxLabeled(
                LocalizeString_Etc.VV_ModSettings_RandomGenesForStartingVivi.Translate(),
                ref _settings.randomGenesForStartingVivi,
                LocalizeString_Etc.VV_ModSettings_RandomGenesForStartingViviDesc.Translate());

            var mealContinuationRect = listing.GetRect(30f);
            var mealContinuationLabelRect = mealContinuationRect.LeftPart(0.5f);
            var mealContinuationControlRect = mealContinuationRect.RightPart(0.5f);
            var mealContinuationInputRect = new Rect(
                mealContinuationControlRect.xMax - 60f,
                mealContinuationControlRect.y + 3f,
                60f,
                24f);
            var mealContinuationSliderRect = mealContinuationControlRect;
            mealContinuationSliderRect.xMax = mealContinuationInputRect.xMin - 6f;

            var previousTextAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(
                mealContinuationLabelRect,
                LocalizeString_Etc.VV_ModSettings_ViviMealContinuationNutritionGap.Translate());
            Text.Anchor = previousTextAnchor;
            TooltipHandler.TipRegion(
                mealContinuationLabelRect,
                LocalizeString_Etc.VV_ModSettings_ViviMealContinuationNutritionGapDesc.Translate());

            var sliderValue = Widgets.HorizontalSlider(
                mealContinuationSliderRect,
                _settings.viviMealContinuationNutritionGap,
                0.05f,
                2f,
                middleAlignment: true,
                roundTo: 0.01f);
            if (!Mathf.Approximately(sliderValue, _settings.viviMealContinuationNutritionGap))
            {
                _settings.viviMealContinuationNutritionGap = sliderValue;
                _viviMealContinuationNutritionGapBuffer = sliderValue.ToString("0.##");
            }

            Widgets.TextFieldNumeric(
                mealContinuationInputRect,
                ref _settings.viviMealContinuationNutritionGap,
                ref _viviMealContinuationNutritionGapBuffer,
                0.05f,
                2f);
            listing.Gap(listing.verticalSpacing);


            listing.End();

            base.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();

            if (OnWriteSettings != null)
            {
                OnWriteSettings();
            }
        }

        public override string SettingsCategory()
        {
            return "Vivi Race";
        }

        private VVRaceModSettings _settings;
        private string _viviMealContinuationNutritionGapBuffer;
    }
}
