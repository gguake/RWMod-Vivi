using HarmonyLib;
using RimWorld;
using System.Reflection;
using Verse;

namespace VVRace
{
    public class Filth_Pollen : Filth
    {
        // TODO
        private static FieldInfo fieldInfo_Filth_growTick = AccessTools.Field(typeof(Filth), "growTick");

        private const int CleaningDelayTicks = 10000;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                var growTick = (int)fieldInfo_Filth_growTick.GetValue(this);
                fieldInfo_Filth_growTick.SetValue(this, growTick + CleaningDelayTicks);
            }
        }

        public override void ThickenFilth()
        {
            base.ThickenFilth();

            var growTick = (int)fieldInfo_Filth_growTick.GetValue(this);
            fieldInfo_Filth_growTick.SetValue(this, growTick + CleaningDelayTicks);
        }
    }
}
