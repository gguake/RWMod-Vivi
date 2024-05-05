using UnityEngine;
using Verse;

namespace VVRace
{
    public class VVRaceModSettings : ModSettings
    {
        public bool allowSelectModGenes = false;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref allowSelectModGenes, "allowSelectModGenes");
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
