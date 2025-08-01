﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
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

    public class CompManaFilthMaker : ThingComp
    {
        public CompProperties_ManaFilthMaker Props => (CompProperties_ManaFilthMaker)props;

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

        private List<IntVec3> _tmpCellCandidates = new List<IntVec3>();
        public override void CompTick()
        {
            if (GenTicks.TicksGame >= _nextRefreshTick)
            {
                if (parent.Spawned && !parent.Destroyed && ManaComp.Active)
                {
                    try
                    {
                        _tmpCellCandidates.AddRange(GenRadial.RadialCellsAround(parent.Position, Props.radius, !Props.exceptCenter).Where(c => c.InBounds(parent.Map) && c.Walkable(parent.Map)));
                        if (_tmpCellCandidates.Count > 0)
                        {
                            var cell = _tmpCellCandidates.RandomElement();
                            if (cell.IsValid)
                            {
                                SpawnEffect();
                                FilthMaker.TryMakeFilth(cell, parent.Map, Props.filthDef, parent.LabelShort);
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
