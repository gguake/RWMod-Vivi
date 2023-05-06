using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Gizmo_MindLinkConnect : Command_Toggle
    {
        public List<ViviMindLinkSettings> mindLinkSettings;
        public bool currentlyIsActive;

        public Gizmo_MindLinkConnect(ViviMindLinkSettings mindLinkSetting)
        {
            mindLinkSettings = new List<ViviMindLinkSettings>() { mindLinkSetting };
            currentlyIsActive = mindLinkSetting.ReservedToConnectTarget != null;

            isActive = () => currentlyIsActive;
        }

        public override bool GroupsWith(Gizmo other)
        {
            if (other is Gizmo_MindLinkConnect otherGizmo)
            {
                return currentlyIsActive == otherGizmo.currentlyIsActive;
            }

            return false;
        }

        public override void MergeWith(Gizmo other)
        {
            if (other is Gizmo_MindLinkConnect otherGizmo)
            {
                mindLinkSettings.AddRange(otherGizmo.mindLinkSettings);
            }
        }
    }
}
