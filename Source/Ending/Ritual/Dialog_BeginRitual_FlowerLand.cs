using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Dialog_BeginRitual_FlowerLand : Dialog_BeginRitual
    {
        private readonly IntVec3 _cell;
        private readonly float _radius;

        public Dialog_BeginRitual_FlowerLand(
            IntVec3 cell,
            float radius,
            string ritualLabel,
            Precept_Ritual ritual,
            TargetInfo target,
            Map map,
            ActionCallback action,
            Pawn organizer,
            RitualObligation obligation,
            PawnFilter filter = null,
            string okButtonText = null,
            List<Pawn> requiredPawns = null,
            Dictionary<string, Pawn> forcedForRole = null,
            RitualOutcomeEffectDef outcome = null,
            List<string> extraInfoText = null,
            Pawn selectedPawn = null)
            : base(ritualLabel, ritual, target, map, action, organizer, obligation, filter, okButtonText, requiredPawns, forcedForRole, outcome, extraInfoText, selectedPawn)
        {
            _cell = cell;
            _radius = radius;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            if (Find.CurrentMap == map)
            {
                FlowerLandUtility.DrawPreview(map, _cell, _radius);
            }
        }
    }
}
