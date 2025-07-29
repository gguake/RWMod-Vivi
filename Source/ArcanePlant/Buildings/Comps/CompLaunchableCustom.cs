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

        public CompProperties_LaunchableCustom()
        {
            compClass = typeof(CompLaunchableCustom);
        }
    }

    public class CompLaunchableCustom : CompLaunchable
    {
        public override bool RequiresFuelingPort => false;

        public override CompRefuelable Refuelable => null;

        public override float FuelLevel => 0;

        public override float MaxFuelLevel => 0;

        public CompLaunchableCustom()
        {
        }
    }
}
