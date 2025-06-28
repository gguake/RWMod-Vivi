using RimWorld;
using Verse;

namespace VVRace
{
    public class ActiveDropPodInfoCustom : ActiveTransporterInfo
    {
        public ThingDef activeDropPod;
        public ThingDef incomingDropPod;

        public ActiveDropPodInfoCustom(CompProperties_LaunchableCustom compProps)
        {
            activeDropPod = compProps.activeDropPod;
            incomingDropPod = compProps.incomingDropPod;
        }
    }

    public class CompProperties_LaunchableCustom : CompProperties_Launchable
    {
        public ThingDef activeDropPod;
        public ThingDef incomingDropPod;
    }
}
