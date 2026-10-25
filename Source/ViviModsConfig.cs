using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class VVRaceModSettings : ModSettings
    {
        public float royalJellyMultiplier = 1f;
        public bool alwaysShowManaIfSelected = true;
        public float manaGridOpacity = 0.5f;
        public bool useVanillaHeadOnly = false;
        public bool randomGenesForStartingVivi = false;
        public bool enableViviMealContinuation = true;
        public float viviMealContinuationNutritionGap = 0.7f;

        // 비비 무작위 유전자에서 제외할 모드 유전자의 defName (블랙리스트: 목록에 없는 모드 유전자는 활성)
        public HashSet<string> disabledModGenes = new HashSet<string>();

        // 구버전 '모드에서 추가된 유전자 허용'이 꺼진 설정(새 설치 포함)은 def 로드 후 모드 유전자를 모두 비활성화한다.
        private bool _disableAllModGenesPending = true;

        // 목록이 없는 구버전 설정(새 설치 포함)은 이전 결과를 바로 저장해서 구버전 옵션이 더 이상 쓰이지 않게 한다.
        private bool _modGeneMigrationUnsaved = true;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref royalJellyMultiplier, "royalJellyMultiplier", defaultValue: 1f);
            Scribe_Values.Look(ref alwaysShowManaIfSelected, "alwaysShowManaIfSelected", defaultValue: true);
            Scribe_Values.Look(ref manaGridOpacity, "manaGridOpacity", defaultValue: 0.5f);
            Scribe_Values.Look(ref useVanillaHeadOnly, "useVanillaHeadOnly", defaultValue: false);
            Scribe_Values.Look(ref randomGenesForStartingVivi, "randomGenesForStartingVivi", defaultValue: false);
            Scribe_Values.Look(ref enableViviMealContinuation, "enableViviMealContinuation", defaultValue: true);
            Scribe_Values.Look(ref viviMealContinuationNutritionGap, "viviMealContinuationNutritionGap", defaultValue: 0.7f);
            ExposeModGenes();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (royalJellyMultiplier < 1f)
                {
                    royalJellyMultiplier = 1f;
                }

                viviMealContinuationNutritionGap = Mathf.Clamp(viviMealContinuationNutritionGap, 0.05f, 2f);
            }
        }

        public bool IsGeneAllowed(GeneDef def)
        {
            return !ViviUtility.IsModGene(def) || IsModGeneEnabled(def);
        }

        public bool IsModGeneEnabled(GeneDef def)
        {
            ResolvePendingModGenes();
            return !disabledModGenes.Contains(def.defName);
        }

        public void SetModGeneEnabled(GeneDef def, bool enabled)
        {
            ResolvePendingModGenes();
            if (enabled)
            {
                disabledModGenes.Remove(def.defName);
            }
            else
            {
                disabledModGenes.Add(def.defName);
            }
        }

        // def 로드가 끝난 직후 한 번 호출: 구버전 옵션 값으로 이전한 결과를 새 형식으로 저장한다.
        public void MigrateLegacyModGeneSettings()
        {
            ResolvePendingModGenes();
            if (_modGeneMigrationUnsaved && !_disableAllModGenesPending)
            {
                Write();
            }
        }

        // 설정은 def 로드 전에 읽히므로 구버전 설정의 이전은 def가 로드된 뒤로 미룬다.
        public void ResolvePendingModGenes()
        {
            if (!_disableAllModGenesPending || !PlayDataLoader.Loaded) { return; }

            _disableAllModGenesPending = false;
            foreach (var def in ViviUtility.GetModGeneCandidates())
            {
                disabledModGenes.Add(def.defName);
            }
        }

        private void ExposeModGenes()
        {
            List<string> disabledModGeneList = null;
            switch (Scribe.mode)
            {
                case LoadSaveMode.Saving:
                    ResolvePendingModGenes();
                    if (_disableAllModGenesPending)
                    {
                        // def 로드 전이라 이전할 수 없으면 구버전 상태(목록 없음)를 그대로 둔다.
                        return;
                    }

                    disabledModGeneList = disabledModGenes.OrderBy(defName => defName, StringComparer.Ordinal).ToList();
                    Scribe_Collections.Look(ref disabledModGeneList, "disabledModGenes", LookMode.Value);
                    _modGeneMigrationUnsaved = false;
                    break;

                case LoadSaveMode.LoadingVars:
                    // 지금 없는 유전자(제거된 모드)도 그대로 보존해서 모드를 다시 켜면 설정이 돌아오게 한다.
                    Scribe_Collections.Look(ref disabledModGeneList, "disabledModGenes", LookMode.Value);
                    _modGeneMigrationUnsaved = disabledModGeneList == null;
                    if (disabledModGeneList != null)
                    {
                        disabledModGenes = new HashSet<string>(disabledModGeneList.Where(defName => !defName.NullOrEmpty()));
                        _disableAllModGenesPending = false;
                    }
                    else
                    {
                        var allowSelectModGenes = false;
                        Scribe_Values.Look(ref allowSelectModGenes, "allowSelectModGenes", defaultValue: false);

                        disabledModGenes = new HashSet<string>();
                        _disableAllModGenesPending = !allowSelectModGenes;
                    }
                    break;
            }
        }
    }

    public class VVRaceMod : Mod
    {
        private enum SettingsTab
        {
            General,
            Genes,
        }

        public static VVRaceModSettings Settings { get; private set; }

        public Action OnWriteSettings;

        public VVRaceMod(ModContentPack content) : base(content)
        {
            _settings = base.GetSettings<VVRaceModSettings>();
            Settings = _settings;
            LongEventHandler.ExecuteWhenFinished(_settings.MigrateLegacyModGeneSettings);

            ViviHarmonyPatcher.PrePatchAll();

            _viviMealContinuationPatched = _settings.enableViviMealContinuation;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var tabRect = inRect;
            tabRect.yMin += TabDrawer.TabHeight;

            _tabs.Clear();
            _tabs.Add(new TabRecord(
                LocalizeString_Etc.VV_ModSettings_TabGeneral.Translate(),
                () => _currentTab = SettingsTab.General,
                _currentTab == SettingsTab.General));
            _tabs.Add(new TabRecord(
                LocalizeString_Etc.VV_ModSettings_TabGenes.Translate(),
                () => _currentTab = SettingsTab.Genes,
                _currentTab == SettingsTab.Genes));

            Widgets.DrawMenuSection(tabRect);
            TabDrawer.DrawTabs(tabRect, _tabs);

            var contentRect = tabRect.ContractedBy(10f);
            switch (_currentTab)
            {
                case SettingsTab.General:
                    DoGeneralSettings(contentRect);
                    break;

                case SettingsTab.Genes:
                    _modGeneSettingsTab.Draw(contentRect, _settings);
                    break;
            }

            base.DoSettingsWindowContents(inRect);
        }

        private void DoGeneralSettings(Rect rect)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

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

            listing.CheckboxLabeled(
                LocalizeString_Etc.VV_ModSettings_EnableViviMealContinuation.Translate(),
                ref _settings.enableViviMealContinuation,
                LocalizeString_Etc.VV_ModSettings_EnableViviMealContinuationDesc.Translate());

            if (_settings.enableViviMealContinuation != _viviMealContinuationPatched)
            {
                var previousColor = GUI.color;
                GUI.color = Color.yellow;
                listing.Label(LocalizeString_Etc.VV_ModSettings_RestartRequired.Translate());
                GUI.color = previousColor;
            }

            if (_settings.enableViviMealContinuation)
            {
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
            }

            listing.End();
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
        private readonly bool _viviMealContinuationPatched;
        private readonly List<TabRecord> _tabs = new List<TabRecord>();
        private readonly ModGeneSettingsTab _modGeneSettingsTab = new ModGeneSettingsTab();
        private SettingsTab _currentTab;
    }
}
