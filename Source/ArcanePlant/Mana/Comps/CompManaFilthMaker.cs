using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompProperties_ManaFilthMaker : CompProperties
    {
        public ThingDef filthDef;
        public FloatRange makeAmountPerDays = new FloatRange(1f, 1f);
        public float radius = 1f;
        public bool exceptCenter = true;

        public EffecterDef spawnEffecter;

        public CompProperties_ManaFilthMaker()
        {
            compClass = typeof(CompManaFilthMaker);
        }
    }

    public class CompManaFilthMaker : ThingComp, IArcanePlantFunctionProvider
    {
        public CompProperties_ManaFilthMaker Props => (CompProperties_ManaFilthMaker)props;

        public IEnumerable<string> GetFunctionDescriptions()
        {
            var countString = Props.makeAmountPerDays.min == Props.makeAmountPerDays.max ?
                Props.makeAmountPerDays.min.ToString("0.#") :
                $"{Props.makeAmountPerDays.min:0.#}~{Props.makeAmountPerDays.max:0.#}";

            yield return LocalizeString_PlantFunction.VV_PlantFunction_FilthMaker.Translate(
                Props.filthDef.label,
                Props.radius.ToString("0.#"),
                countString);
        }

        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null) { _manaComp = parent.GetComp<CompMana>(); }
                return _manaComp;
            }
        }
        private CompMana _manaComp;

        private int _nextRefreshTick;

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref _nextRefreshTick, "nextRefreshTick");
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                _nextRefreshTick = GenTicks.TicksGame + (int)(60000f / Props.makeAmountPerDays.RandomInRange);
            }
        }

        private readonly List<IntVec3> _tmpCellCandidates = new List<IntVec3>();
        public override void CompTick()
        {
            if (GenTicks.TicksGame >= _nextRefreshTick)
            {
                if (parent.Spawned && !parent.Destroyed && ManaComp.Active)
                {
                    try
                    {
                        var map = parent.Map;
                        var center = parent.Position;
                        int start = Props.exceptCenter ? 1 : 0;
                        int count = GenRadial.NumCellsInRadius(Props.radius);
                        for (int i = start; i < count; i++)
                        {
                            var c = center + GenRadial.RadialPattern[i];
                            if (c.InBounds(map) && c.Walkable(map))
                            {
                                _tmpCellCandidates.Add(c);
                            }
                        }

                        if (_tmpCellCandidates.Count > 0)
                        {
                            var cell = _tmpCellCandidates.RandomElement();
                            if (cell.IsValid)
                            {
                                SpawnEffect();
                                FilthMaker.TryMakeFilth(cell, map, Props.filthDef, parent.LabelShort);
                            }
                        }
                    }
                    finally
                    {
                        _tmpCellCandidates.Clear();
                    }
                }

                _nextRefreshTick = GenTicks.TicksGame + (int)(60000f / Props.makeAmountPerDays.RandomInRange);
            }
        }

        public void SpawnEffect()
        {
            if (Props.spawnEffecter == null) { return; }

            Props.spawnEffecter.SpawnMaintained(parent, parent.Map);
        }
    }
}
