﻿using RimWorld;
using System.Collections.Generic;
using System.Runtime;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ViviEggSettings : IExposable
    {
        public float EggProgress => eggProgress;

        public float EggLayIntervalDays
        {
            get
            {
                if (eggLayIntervalDays < 0)
                {
                    eggLayIntervalDays = gene.Def.eggLayIntervalDays;
                }
                return eggLayIntervalDays;
            }
        }
        private float eggLayIntervalDays = -1f;

        public float EggProgressPerDays => Mathf.Clamp01(PawnUtility.BodyResourceGrowthSpeed(gene.pawn) / EggLayIntervalDays);

        public bool CanLayNow
        {
            get
            {
                return eggProgress >= 1f;
            }
        }

        public Gene_Vivi gene;
        private float eggProgress;
        private HashSet<GeneDef> storedGenes;

        public ViviEggSettings(Gene_Vivi gene)
        {
            this.gene = gene;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref eggProgress, "eggProgress");
            Scribe_Collections.Look(ref storedGenes, "storedGenes", LookMode.Def);
        }

        public void ResolveReferences(Gene_Vivi gene)
        {
            this.gene = gene;
        }

        public void StoreGene(GeneDef geneDef)
        {
            storedGenes.Add(geneDef);
        }

        public void Tick(int tick)
        {
            if (eggProgress >= 1f) { return; }

            eggProgress = Mathf.Clamp01(eggProgress + EggProgressPerDays / 60000f * tick);
        }

        public Thing ProduceEgg()
        {
            if (!CanLayNow) { return null; }

            try
            {
                var egg = ThingMaker.MakeThing(VVThingDefOf.VV_ViviEgg);
                var hatcher = egg.TryGetComp<CompViviHatcher>();
                hatcher.hatcheeParent = gene.pawn;

                return egg;
            }
            finally
            {
                eggProgress = 0f;
            }
        }

        public void AddEggProgressDirectlyForDebug(float progress)
        {
            eggProgress = Mathf.Clamp01(eggProgress + progress);
        }

        public IEnumerable<Gizmo> GetGizmos()
        {
            yield return new EggProgressGizmo(this);

            if (DebugSettings.godMode)
            {
                Command_Action command_addEggProgress = new Command_Action();
                command_addEggProgress.defaultLabel = "DEV: Add Egg Progress";
                command_addEggProgress.action = () =>
                {
                    AddEggProgressDirectlyForDebug(0.1f);
                };

                yield return command_addEggProgress;
            }
        }
    }
}
