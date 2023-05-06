using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Gizmo_MindLinkDisconnect : Command_Toggle
    {
        public List<ViviMindLinkSettings> mindLinkSettings;
        public bool currentlyIsActive;

        public Gizmo_MindLinkDisconnect(ViviMindLinkSettings mindLinkSetting)
        {
            mindLinkSettings = new List<ViviMindLinkSettings>() { mindLinkSetting };
            currentlyIsActive = mindLinkSetting.ReservedToDisconnect;

            isActive = () => currentlyIsActive;
        }

        public override bool GroupsWith(Gizmo other)
        {
            if (other is Gizmo_MindLinkDisconnect otherGizmo)
            {
                return currentlyIsActive == otherGizmo.currentlyIsActive;
            }

            return false;
        }

        public override void MergeWith(Gizmo other)
        {
            if (other is Gizmo_MindLinkDisconnect otherGizmo)
            {
                mindLinkSettings.AddRange(otherGizmo.mindLinkSettings);
            }
        }
    }
}
