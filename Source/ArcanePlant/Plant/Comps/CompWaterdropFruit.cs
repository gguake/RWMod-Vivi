using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_WaterdropFruit : CompProperties
    {
        public ThingDef fruitDef;
        public FloatRange intervalDays;
        public IntRange count;

        public CompProperties_WaterdropFruit()
        {
            compClass = typeof(CompWaterdropFruit);
        }
    }

    public class CompWaterdropFruit : ThingComp, IArcanePlantFunctionProvider
    {
        public CompProperties_WaterdropFruit Props => (CompProperties_WaterdropFruit)props;
        public ArcanePlant Plant => (ArcanePlant)parent;

        public IEnumerable<string> GetFunctionDescriptions()
        {
            var intervalString = $"{Props.intervalDays.min:0.#}~{Props.intervalDays.max:0.#}";
            var countString = Props.count.min == Props.count.max ?
                Props.count.min.ToString() :
                $"{Props.count.min}~{Props.count.max}";

            yield return LocalizeString_PlantFunction.VV_PlantFunction_Fruit.Translate(
                Props.fruitDef.label,
                intervalString,
                countString);
        }

        private int _remainingTicks;
        private int _nextMatureTicks;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref _remainingTicks, "remainingFruitTicks");
            Scribe_Values.Look(ref _nextMatureTicks, "nextFruitMatureTicks");
        }

        public override void PostPostMake()
        {
            _nextMatureTicks = Mathf.CeilToInt(Props.intervalDays.RandomInRange * 60000f);
            _remainingTicks = _nextMatureTicks + _remainingTicks;
        }

        public override void CompTickInterval(int delta)
        {
            if (parent.Spawned)
            {
                if (Plant.ManaComp.Active)
                {
                    _remainingTicks -= delta;
                    if (_remainingTicks <= 0)
                    {
                        _nextMatureTicks = Mathf.CeilToInt(Props.intervalDays.RandomInRange * 60000f);
                        _remainingTicks = _nextMatureTicks + _remainingTicks;

                        var thing = ThingMaker.MakeThing(Props.fruitDef);
                        thing.stackCount = Props.count.RandomInRange;
                        if (!TryPlaceFruit(thing) && !thing.Destroyed)
                        {
                            thing.Destroy();
                        }
                    }
                }
            }
        }

        // 다른 세력 식물의 열매는 그 세력 몫이다. 둥지 세력 식물이면 둥지 몫으로 놓고, 그 밖의 세력이면 금지 표시해 정착민이 가져가지 않게 한다.
        private bool TryPlaceFruit(Thing fruit)
        {
            var map = parent.Map;
            var faction = parent.Faction;
            if (faction == null || faction.IsPlayer)
            {
                return GenPlace.TryPlaceThing(fruit, parent.Position, map, ThingPlaceMode.Near);
            }

            var nest = BeekeepingUtility.GetNestLordJob(map, faction);
            if (nest != null)
            {
                return nest.TryPlaceOwned(fruit, parent.Position);
            }

            // 바닐라 놓기는 그 칸의 같은 더미에 합치고 합친 더미의 금지 상태를 따르므로 금지되지 않은 같은 더미가 있는 칸은 피한다.
            var def = fruit.def;
            fruit.SetForbidden(true, warnOnFail: false);
            return GenPlace.TryPlaceThing(fruit, parent.Position, map, ThingPlaceMode.Near, out _, extraValidator: c => !HasUnforbiddenStack(map, c, def));
        }

        private static bool HasUnforbiddenStack(Map map, IntVec3 cell, ThingDef def)
        {
            var things = map.thingGrid.ThingsListAtFast(cell);
            for (int i = 0; i < things.Count; ++i)
            {
                if (things[i].def == def && !things[i].IsForbidden(Faction.OfPlayer)) { return true; }
            }
            return false;
        }

        public override string CompInspectStringExtra()
        {
            var pct = _nextMatureTicks > 0 ? Mathf.Clamp01(1f - (float)_remainingTicks / _nextMatureTicks) : 0f;
            return $"{Props.fruitDef.LabelCap}: {pct.ToStringPercent()}";
        }
    }
}
