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

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref allowSelectModGenes, "allowSelectModGenes", defaultValue: false);
            Scribe_Values.Look(ref royalJellyMultiplier, "royalJellyMultiplier", defaultValue: 1f);
            Scribe_Values.Look(ref alwaysShowManaIfSelected, "alwaysShowManaIfSelected", defaultValue: true);
            Scribe_Values.Look(ref manaGridOpacity, "manaGridOpacity", defaultValue: 0.5f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (royalJellyMultiplier < 1f)
                {
                    royalJellyMultiplier = 1f;
                }
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
    }
}
