using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class GameCondition_DreamumHaze : GameCondition
    {
        private SkyColorSet HazeColorSet = new SkyColorSet(
            new ColorInt(254, 235, 143).ToColor, 
            new ColorInt(200, 228, 255).ToColor, 
            new Color(1.0f, 0.9f, 0.4f), 
            0.85f);

        private List<SkyOverlay> overlays = new List<SkyOverlay>
        {
            new WeatherOverlay_Fallout()
        };

        public override int TransitionTicks => 5000;

        public override void Init()
        {
        }

        public override void GameConditionTick()
        {
            List<Map> affectedMaps = base.AffectedMaps;
            if (Find.TickManager.TicksGame % 4000 == 0)
            {
                for (int i = 0; i < affectedMaps.Count; i++)
                {
                    foreach (var pawn in affectedMaps[i].mapPawns.AllPawnsSpawned)
                    {
                        if (!pawn.kindDef.immuneToGameConditionEffects)
                        {
                            AdjustSeverityToPawn(pawn);
                        }
                    }
                }
            }
            for (int j = 0; j < overlays.Count; j++)
            {
                for (int k = 0; k < affectedMaps.Count; k++)
                {
                    overlays[j].TickOverlay(affectedMaps[k]);
                }
            }
        }

        public static void AdjustSeverityToPawn(Pawn p, float extraFactor = 1f)
        {
            if (p.IsVivi()) { return; }

            if (p.Spawned)
            {
                var severity = 0.035f;
                severity *= Mathf.Max(1f - p.GetStatValue(StatDefOf.ToxicResistance), 0f);
                severity *= Mathf.Max(1f - p.GetStatValue(StatDefOf.ToxicEnvironmentResistance), 0f);

                severity *= extraFactor;

                if (severity != 0f)
                {
                    HealthUtility.AdjustSeverity(p, VVHediffDefOf.VV_DreamumTranquilize, severity);
                }
            }
        }

        public override void GameConditionDraw(Map map)
        {
            for (int i = 0; i < overlays.Count; i++)
            {
                overlays[i].DrawOverlay(map);
            }
        }

        public override float SkyTargetLerpFactor(Map map)
        {
            return GameConditionUtility.LerpInOutValue(this, TransitionTicks, 0.5f);
        }

        public override SkyTarget? SkyTarget(Map map)
        {
            return new SkyTarget(0.85f, HazeColorSet, 1f, 1f);
        }

        public override List<SkyOverlay> SkyOverlays(Map map)
        {
            return overlays;
        }
    }
}
