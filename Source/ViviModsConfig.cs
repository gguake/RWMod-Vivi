using UnityEngine;
using Verse;

namespace VVRace
{
    public class VVRaceModSettings : ModSettings
    {
        public bool allowSelectModGenes = false;

        public float royalJellyMultiplier = 1f;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref allowSelectModGenes, "allowSelectModGenes");
            Scribe_Values.Look(ref royalJellyMultiplier, "royalJellyMultiplier");
        }
    }

    public class VVRaceMod : Mod
    {
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
                0.1f, 
                10f, 
                tooltip: LocalizeString_Etc.VV_ModSettings_RoyalJellyNeedMultiplierDesc.Translate());

            listing.End();

            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Vivi Race";
        }

        private VVRaceModSettings _settings;
    }
}
