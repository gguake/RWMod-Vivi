using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_FermentItem : CompProperties
    {
        public int TotalFermentTicks => (int)(totalFermentDays * 60000);

        public float minSafeTemperature;
        public float maxSafeTemperature;
        public float damageProgressPerDegreePerTick;

        public float totalFermentDays;
        public List<ThingDefCountClass> fermentedThings;

        public CompProperties_FermentItem()
        {
            compClass = typeof(CompFermentItem);
        }
    }

    public class CompFermentItem : ThingComp
    {
        public CompProperties_FermentItem Props => (CompProperties_FermentItem)props;

        public bool Ruined => _temperatureDamagedProgress >= 1f;

        private float _fermentedProgress;
        private float _temperatureDamagedProgress;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref _fermentedProgress, "_fermentedProgress");
            Scribe_Values.Look(ref _temperatureDamagedProgress, "_temperatureDamagedProgress");
        }

        public override void CompTick()
        {
            Tick(1);
        }

        public override void CompTickRare()
        {
            Tick(GenTicks.TickRareInterval);
        }

        public void Tick(int ticks)
        {
            if (Ruined)
            {
                parent.TakeDamage(new DamageInfo(DamageDefOf.Rotting, Rand.Range(1f, 3f)));
                return;
            }

            if (parent.Spawned)
            {
                float ambientTemperature = parent.AmbientTemperature;
                if (ambientTemperature > Props.maxSafeTemperature)
                {
                    _temperatureDamagedProgress = Mathf.Clamp01(_temperatureDamagedProgress + (ambientTemperature - Props.maxSafeTemperature) * Props.damageProgressPerDegreePerTick * ticks);
                }
                else if (ambientTemperature < Props.minSafeTemperature)
                {
                    _temperatureDamagedProgress = Mathf.Clamp01(_temperatureDamagedProgress - (ambientTemperature - Props.minSafeTemperature) * Props.damageProgressPerDegreePerTick * ticks);
                }
                else
                {
                    _fermentedProgress = Mathf.Clamp01(_fermentedProgress + (float)ticks / Props.TotalFermentTicks);
                }

                if (_fermentedProgress >= 1f)
                {
                    var map = parent.Map;
                    var position = parent.Position;
                    parent.Destroy();

                    foreach (var tdc in Props.fermentedThings)
                    {
                        var thingDef = tdc.thingDef;
                        var count = tdc.count;

                        var stackLimit = thingDef.stackLimit;
                        while (count > 0)
                        {
                            var stackCount = Mathf.Clamp(count, 1, stackLimit);
                            var thing = ThingMaker.MakeThing(thingDef);
                            thing.stackCount = stackCount;

                            GenPlace.TryPlaceThing(thing, position, map, ThingPlaceMode.Direct);
                            count -= stackCount;
                        }
                    }
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            var sb = new StringBuilder();

            if (Ruined)
            {
                sb.AppendLine("RuinedByTemperature".Translate());
            }
            else
            {
                sb.AppendLine(LocalizeTexts.InspectorProperTemperature.Translate(
                    Props.minSafeTemperature.ToStringTemperature(),
                    Props.maxSafeTemperature.ToStringTemperature()));

                if (_temperatureDamagedProgress > 0f)
                {
                    float ambientTemperature = parent.AmbientTemperature;
                    if (ambientTemperature > Props.maxSafeTemperature)
                    {
                        sb.AppendLine("Overheating".Translate() + ": " + _temperatureDamagedProgress.ToStringPercent());
                    }
                    else if (ambientTemperature < Props.minSafeTemperature)
                    {
                        sb.AppendLine("Freezing".Translate() + ": " + _temperatureDamagedProgress.ToStringPercent());
                    }
                }

                sb.AppendLine("FermentationProgress".Translate(
                    _fermentedProgress.ToStringPercent(),
                    ((int)((1f - _fermentedProgress) * Props.TotalFermentTicks)).ToStringTicksToPeriod(shortForm: true)));
            }

            return sb.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (DebugSettings.godMode)
            {
                var command_addTemperatureDamaged = new Command_Action();
                command_addTemperatureDamaged.defaultLabel = "DEV: Temperature Damaged +10%";
                command_addTemperatureDamaged.action = () =>
                {
                    _temperatureDamagedProgress += 0.1f;
                };

                yield return command_addTemperatureDamaged;

                var command_addFermentProgress = new Command_Action();
                command_addFermentProgress.defaultLabel = "DEV: Ferment Progress +10%";
                command_addFermentProgress.action = () =>
                {
                    _fermentedProgress += 0.1f;
                };

                yield return command_addFermentProgress;
            }
        }
    }
}
