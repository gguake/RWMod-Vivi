using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public static class DoomBlightUtility
    {
        public const int MaxBlightLevel = 5;

        private static IEnumerable<HediffDef> BlightDefs
        {
            get
            {
                yield return ApocryphaGimmickDefOf.VV_DoomBlight_Fear;
                yield return ApocryphaGimmickDefOf.VV_DoomBlight_Hunger;
                yield return ApocryphaGimmickDefOf.VV_DoomBlight_Plague;
            }
        }

        public static void ApplyDoomBlight(Map map, IntVec3 sourceCell)
        {
            if (map == null) { return; }

            var colonists = new List<Pawn>(map.mapPawns.FreeColonistsSpawned);
            if (colonists.Count == 0) { return; }

            if (!BlightDefs.Where(def => AnyBelowMaxLevel(colonists, def)).TryRandomElement(out var blightDef)) { return; }

            foreach (var pawn in colonists)
            {
                AddOrStackBlight(pawn, blightDef);
            }

            Messages.Message(
                LocalizeString_EndingGimmicks.VV_Message_DoomFlowerBloomed.Translate(blightDef.label.Named("BLIGHT")),
                new LookTargets(new TargetInfo(sourceCell, map)),
                MessageTypeDefOf.NegativeEvent);
        }

        public static bool ClearDoomBlights(Map map)
        {
            if (map == null) { return false; }

            var anyCleared = false;
            var pawns = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < pawns.Count; i++)
            {
                anyCleared |= ClearDoomBlights(pawns[i]);
            }

            return anyCleared;
        }

        public static bool ClearDoomBlights(Pawn pawn)
        {
            if (pawn?.health == null) { return false; }

            var anyCleared = false;
            foreach (var def in BlightDefs)
            {
                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                    anyCleared = true;
                }
            }

            return anyCleared;
        }

        private static bool AnyBelowMaxLevel(List<Pawn> pawns, HediffDef def)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                var hediff = pawns[i].health.hediffSet.GetFirstHediffOfDef(def);
                if (hediff == null || hediff.Severity < MaxBlightLevel) { return true; }
            }

            return false;
        }

        private static void AddOrStackBlight(Pawn pawn, HediffDef def)
        {
            if (pawn.Dead) { return; }

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(def, pawn);
                hediff.Severity = 1f;
                pawn.health.AddHediff(hediff);
            }
            else
            {
                hediff.Severity = System.Math.Min(MaxBlightLevel, hediff.Severity + 1f);
            }
        }
    }
}
