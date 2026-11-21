using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    internal static class EndingVfxUtility
    {
        public const int ShockWaveAgeLeadTicks = 1;

        private const float ShockWaveFrontStart = 0.02f;
        private const float ShockWaveFrontReach = 0.96f;
        private const float ShockWaveCoverMargin = 1.05f;

        public static float ShockWaveHalfSize(float reach)
        {
            return reach * ShockWaveCoverMargin / ShockWaveFrontReach;
        }

        public static void SpawnShockWave(Map map, Vector3 origin, FleckDef fleckDef, float halfSize)
        {
            var data = FleckMaker.GetDataStatic(origin, map, fleckDef, halfSize * 2f / fleckDef.graphicData.drawSize.x);
            data.rotation = Rand.Range(0f, 360f);
            map.flecks.CreateFleck(data);
        }

        public static float ShockWaveFrontAt(float halfSize, float secs, float durationSecs)
        {
            var t = Mathf.Clamp01(secs / durationSecs);
            return halfSize * Mathf.Lerp(ShockWaveFrontStart, ShockWaveFrontReach, 1f - Mathf.Pow(1f - t, 3f));
        }

        public static int ShockWaveArrivalTicks(float distance, float halfSize, float durationSecs)
        {
            var u = Mathf.Clamp01((distance / halfSize - ShockWaveFrontStart) / (ShockWaveFrontReach - ShockWaveFrontStart));
            var t = 1f - Mathf.Pow(1f - u, 1f / 3f);
            return Mathf.CeilToInt(t * durationSecs * 60f) - ShockWaveAgeLeadTicks;
        }

        public static Vector3 HorizontalPerpendicular(Vector3 dir)
        {
            return new Vector3(dir.z, 0f, -dir.x);
        }

        public static Vector3 SafeNormalizeHorizontal(Vector3 vector, Vector3 fallback, float minSqrMagnitude = 0.01f)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > minSqrMagnitude ? vector.normalized : fallback;
        }

        public static Vector3 OffsetFromCell(Vector3 point, IntVec3 cell)
        {
            return (point - cell.ToVector3Shifted()).WithY(0f);
        }

        public static Vector3 RandomRingOffset(float radius)
        {
            return Vector3Utility.FromAngleFlat(Rand.Range(0f, 360f)) * radius;
        }

        public static Vector3 RandomRingOffset(FloatRange radius)
        {
            return RandomRingOffset(radius.RandomInRange);
        }

        public static Vector3 RandomPointInRect(CellRect rect)
        {
            return new Vector3(Rand.Range(rect.minX, rect.maxX + 1f), 0f, Rand.Range(rect.minZ, rect.maxZ + 1f));
        }

        public static float MaxDistanceToMapCorner(Map map, Vector3 center)
        {
            var size = map.Size;
            var farX = Mathf.Max(center.x, size.x - center.x);
            var farZ = Mathf.Max(center.z, size.z - center.z);
            return Mathf.Sqrt(farX * farX + farZ * farZ);
        }

        public static void PlayEffectAt(Map map, EffecterDef effecterDef, Vector3 point, float scale = 1f)
        {
            if (effecterDef == null || map == null) { return; }

            var cell = point.ToIntVec3().ClampInsideMap(map);
            ApocryphaGimmicks.PlayEffect(effecterDef, cell, map, OffsetFromCell(point, cell), scale);
        }

        public static void MaintainEffecter(ref Effecter effecter, EffecterDef def, TargetInfo a, TargetInfo b)
        {
            if (def == null) { return; }

            if (effecter == null)
            {
                effecter = def.Spawn();
                effecter.Trigger(a, b);
            }

            effecter.EffectTick(a, b);
        }

        public static void CleanupEffecter(ref Effecter effecter)
        {
            if (effecter == null) { return; }

            effecter.Cleanup();
            effecter = null;
        }

        public static Command_Action DevAction(string label, Action action, string disabledReason = null)
        {
            var command = new Command_Action()
            {
                defaultLabel = label,
                action = action,
            };
            if (disabledReason != null)
            {
                command.Disable(disabledReason);
            }

            return command;
        }

        public static string AppendTipLine(string baseTip, string extra)
        {
            return baseTip.NullOrEmpty() ? extra : baseTip + "\n" + extra;
        }

        public static void RemoveSelfIfPresent(Pawn pawn, Hediff hediff)
        {
            if (pawn?.health != null && pawn.health.hediffSet.hediffs.Contains(hediff))
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        public static void DestroyAllOfDef(Map map, ThingDef def)
        {
            if (map == null || def == null) { return; }

            var things = new List<Thing>(map.listerThings.ThingsOfDef(def));
            for (int i = 0; i < things.Count; i++)
            {
                if (!things[i].Destroyed)
                {
                    things[i].Destroy(DestroyMode.Vanish);
                }
            }
        }
    }
}
