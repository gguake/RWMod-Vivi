using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace VVRace
{
    public static class ApocryphaUtility
    {
        private const float FarEntryDistanceFraction = 0.9f;

        public static bool IsApocrypha(Pawn pawn)
        {
            return pawn != null && pawn.def == VVThingDefOf.VV_Apocrypha;
        }

        public static bool TryFindEntryCell(Map map, IntVec3 target, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (!CanEnterFromEdge(map)) { return false; }

            var reachParms = TraverseParms.For(TraverseMode.PassAllDestroyableThings);
            var hasTarget = target.IsValid && target.InBounds(map);
            var candidates = CellRect.WholeMap(map).EdgeCells
                .Where(c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map)
                    && (!hasTarget || map.reachability.CanReach(c, target, PathEndMode.Touch, reachParms)))
                .ToList();
            if (candidates.Count == 0) { return false; }
            if (!hasTarget) { return candidates.TryRandomElement(out cell); }

            var farthest = candidates.Max(c => c.DistanceToSquared(target));
            var minDistance = farthest * FarEntryDistanceFraction * FarEntryDistanceFraction;
            return candidates.Where(c => c.DistanceToSquared(target) >= minDistance).TryRandomElement(out cell);
        }

        public static bool TryFindBreachEntryCell(Map map, Thing target, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (target == null || !CanEnterFromEdge(map)) { return false; }

            var targetRect = StealTargetRect(map, target);
            var targetCell = targetRect.CenterCell;
            var bestTier = int.MaxValue;
            var bestDistance = int.MaxValue;
            foreach (var c in CellRect.WholeMap(map).EdgeCells)
            {
                if (!c.Standable(map)) { continue; }

                var tier = (c.Fogged(map) ? 1 : 0) + (IsBreachableLine(map, c, targetRect) ? 0 : 2);
                if (tier > bestTier) { continue; }

                var distance = c.DistanceToSquared(targetCell);
                if (tier < bestTier || distance < bestDistance)
                {
                    cell = c;
                    bestTier = tier;
                    bestDistance = distance;
                }
            }

            return cell.IsValid;
        }

        public static bool IsBreachableLine(Map map, IntVec3 from, CellRect targetRect)
        {
            foreach (var c in GenSight.PointsOnLineOfSight(from, targetRect.CenterCell))
            {
                if (targetRect.Contains(c)) { return true; }
                if (!IsBreachableCell(map, c)) { return false; }
            }

            return true;
        }

        public static bool IsBreachableCell(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map) || cell.GetTerrain(map).passability == Traversability.Impassable) { return false; }

            var edifice = cell.GetEdifice(map);
            return edifice == null || !BlocksPath(edifice) || JobGiver_ApocryphaMain.CanSmash(edifice, null);
        }

        public static bool BlocksPath(Thing thing)
        {
            return thing is Building && (thing.def.passability == Traversability.Impassable || thing is Building_Door);
        }

        public static bool CanEnterFromEdge(Map map)
        {
            if (map == null || map.IsPocketMap || map.IsTempIncidentMap || !map.CanEverExit || IsSpaceOrVacuum(map)) { return false; }

            return PawnsArrivalModeDefOf.EdgeWalkIn.Worker.CanUseOnMap(map);
        }

        public static bool CanBreachReach(Map map, IntVec3 from, LocalTargetInfo target)
        {
            return map.reachability.CanReach(from, target, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassAllDestroyableThings));
        }

        public static bool TryFindInnerEntryCell(Map map, Thing target, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map == null || target == null || IsSpaceOrVacuum(map)) { return false; }

            return TryFindFarthestLinkedCell(map, target, passDestroyable: false, out cell)
                || TryFindFarthestLinkedCell(map, target, passDestroyable: true, out cell);
        }

        private static bool IsSpaceOrVacuum(Map map)
        {
            if (map.Biome != null && map.Biome.inVacuum) { return true; }

            return map.Tile.Valid && map.Tile.LayerDef != null && map.Tile.LayerDef.isSpace;
        }

        private static bool TryFindFarthestLinkedCell(Map map, Thing target, bool passDestroyable, out IntVec3 cell)
        {
            var rect = StealTargetRect(map, target);
            var center = rect.CenterCell;
            var best = IntVec3.Invalid;
            var bestOpen = false;
            var bestDistance = -1;
            System.Action<IntVec3> processor = c =>
            {
                if (rect.Contains(c) || !c.Standable(map) || c.Fogged(map)) { return; }

                var open = !c.Roofed(map);
                if (bestOpen && !open) { return; }

                var distance = c.DistanceToSquared(center);
                if ((open && !bestOpen) || distance > bestDistance)
                {
                    best = c;
                    bestOpen = open;
                    bestDistance = distance;
                }
            };
            FloodFromStealTarget(map, rect, passDestroyable, processor);

            cell = best;
            return cell.IsValid;
        }

        public static bool TryFindSkipCell(Map map, Thing target, IntVec3 from, float stealRange, out IntVec3 cell)
        {
            var rect = StealTargetRect(map, target);
            var center = rect.CenterCell;
            var best = IntVec3.Invalid;
            var bestOutside = false;
            var bestDistance = int.MaxValue;
            System.Action<IntVec3> processor = c =>
            {
                if (rect.Contains(c) || !c.Standable(map) || c.Fogged(map)) { return; }

                var outside = !c.InHorDistOf(center, stealRange);
                if (bestOutside && !outside) { return; }

                var distance = c.DistanceToSquared(from);
                if ((outside && !bestOutside) || distance < bestDistance)
                {
                    best = c;
                    bestOutside = outside;
                    bestDistance = distance;
                }
            };
            FloodFromStealTarget(map, rect, passDestroyable: true, processor);

            cell = best;
            return cell.IsValid;
        }

        private static CellRect StealTargetRect(Map map, Thing target)
        {
            return target.Spawned && target.Map == map ? target.OccupiedRect() : CellRect.SingleCell(target.PositionHeld);
        }

        private static void FloodFromStealTarget(Map map, CellRect rect, bool passDestroyable, System.Action<IntVec3> processor)
        {
            var roots = new List<IntVec3>();
            foreach (var root in rect)
            {
                if (root.InBounds(map)) { roots.Add(root); }
            }

            if (roots.Count == 0) { return; }

            System.Predicate<IntVec3> passCheck = c =>
            {
                if (c.WalkableByNormal(map)) { return true; }
                if (!passDestroyable) { return false; }

                var edifice = c.GetEdifice(map);
                return edifice != null && PathUtility.IsDestroyable(edifice);
            };

            map.floodFiller.FloodFill(IntVec3.Invalid, passCheck, processor, int.MaxValue, false, roots);
        }

        public static Pawn GenerateApocrypha(Map map)
        {
            var boss = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                VVPawnKindDefOf.VV_Apocrypha,
                ResolveBossFaction(),
                PawnGenerationContext.NonPlayer,
                map.Tile,
                forceGenerateNewPawn: true,
                forceNoGear: true));

            boss.ageTracker.LockCurrentLifeStageIndex(0);
            return boss;
        }

        public static void SpawnApocrypha(Pawn boss, Map map, IntVec3 entryCell)
        {
            GenSpawn.Spawn(boss, entryCell, map, EntryRotation(map, entryCell));

            if (boss.Faction == null)
            {
                boss.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent, forced: true, transitionSilently: true);
            }
        }

        public static void SpawnArrival(Pawn boss)
        {
            var count = boss.TryGetComp<CompApocrypha>()?.ArrivalHornetCount ?? 0;
            HornetManifestation.SummonAround(boss, count, HornetSummonRole.Escort);

            VVEffecterDefOf.VV_Effecter_ApocryphaArrive.Spawn(boss.Position, boss.Map, 1.5f).Cleanup();
            Find.TickManager.slower.SignalForceNormalSpeedShort();
        }

        public static Rot4 EntryRotation(Map map, IntVec3 entryCell)
        {
            return Rot4.FromAngleFlat((map.Center - entryCell).AngleFlat);
        }

        private static Faction ResolveBossFaction()
        {
            var insects = Find.FactionManager.OfInsects;
            if (insects != null && !insects.defeated)
            {
                return insects;
            }

            return Find.FactionManager.RandomEnemyFaction(allowHidden: true, allowDefeated: false, allowNonHumanlike: true);
        }

        public static Pawn SpawnBossFightHornet(Map map, IntVec3 cell, Rot4 rotation)
        {
            var hornet = PawnGenerator.GeneratePawn(new PawnGenerationRequest(VVPawnKindDefOf.VV_TitanicHornet, null, PawnGenerationContext.NonPlayer, map.Tile));
            hornet.health.AddHediff(VVHediffDefOf.VV_DoomPhantasm);
            GenSpawn.Spawn(hornet, cell, map, rotation);

            hornet.mindState.mentalStateHandler.TryStartMentalState(VVMentalStateDefOf.VV_HornetBerserk, transitionSilently: true);
            return hornet;
        }

        public static void JoinEscort(Pawn boss, Pawn hornet)
        {
            var lord = FindEscortLord(boss);
            if (lord != null)
            {
                lord.AddPawn(hornet);
                return;
            }

            var props = boss.TryGetComp<CompApocrypha>()?.Props;
            var escortJob = new LordJob_ApocryphaEscort(boss, props?.escortGuardRadius ?? 10f, props?.escortRetaliationRadius ?? 25f);
            LordMaker.MakeNewLord(null, escortJob, hornet.Map, new List<Pawn> { hornet });
        }

        public static void JoinFlowerGuard(Pawn boss, Thing flower, Pawn hornet)
        {
            var lord = FindFlowerGuardLord(boss);
            if (lord != null)
            {
                ((LordJob_ApocryphaFlowerGuard)lord.LordJob).AssignFlower(hornet, flower);
                lord.AddPawn(hornet);
                return;
            }

            var props = boss.TryGetComp<CompApocrypha>()?.Props;
            var guardJob = new LordJob_ApocryphaFlowerGuard(boss, props?.flowerGuardRadius ?? 6f, props?.flowerGuardRetaliationRadius ?? 20f);
            guardJob.AssignFlower(hornet, flower);
            LordMaker.MakeNewLord(null, guardJob, hornet.Map, new List<Pawn> { hornet });
        }

        public static void JoinHornetAssault(Map map, List<Pawn> hornets)
        {
            if (map == null || hornets.NullOrEmpty()) { return; }

            var joining = new List<Pawn>();
            foreach (var hornet in hornets)
            {
                var previous = hornet.GetLord();
                previous?.Notify_PawnLost(hornet, PawnLostCondition.ForcedToJoinOtherLord);
                if (hornet.GetLord() == null)
                {
                    joining.Add(hornet);
                }
            }

            if (joining.Count == 0) { return; }

            var lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                if (lords[i].LordJob is LordJob_HornetAssault)
                {
                    lords[i].AddPawns(joining);
                    return;
                }
            }

            LordMaker.MakeNewLord(null, new LordJob_HornetAssault(), map, joining);
        }

        public static Lord FindEscortLord(Pawn boss)
        {
            return FindBossLord(boss, lordJob => lordJob is LordJob_ApocryphaEscort escortJob && escortJob.Boss == boss);
        }

        public static Lord FindFlowerGuardLord(Pawn boss)
        {
            return FindBossLord(boss, lordJob => lordJob is LordJob_ApocryphaFlowerGuard guardJob && guardJob.Boss == boss);
        }

        private static Lord FindBossLord(Pawn boss, System.Predicate<LordJob> match)
        {
            if (boss == null) { return null; }

            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var lords = maps[i].lordManager.lords;
                for (int j = 0; j < lords.Count; j++)
                {
                    if (match(lords[j].LordJob))
                    {
                        return lords[j];
                    }
                }
            }

            return null;
        }

        public static void ClearEscortFocus(Pawn boss)
        {
            (FindEscortLord(boss)?.LordJob as LordJob_ApocryphaEscort)?.ClearFocus();
        }

        public static List<Pawn> GetEscorts(Pawn boss)
        {
            var lord = FindEscortLord(boss);
            return lord == null ? new List<Pawn>() : new List<Pawn>(lord.ownedPawns);
        }

        public static void ReleaseEscorts(Pawn boss)
        {
            GotoAssault(FindEscortLord(boss));
            GotoAssault(FindFlowerGuardLord(boss));
        }

        private static void GotoAssault(Lord lord)
        {
            if (lord?.Graph == null) { return; }

            foreach (var toil in lord.Graph.lordToils)
            {
                if (toil is LordToil_HornetAssault && lord.CurLordToil != toil)
                {
                    lord.GotoToil(toil);
                    break;
                }
            }
        }

        public static bool IsDefeatInProgress(Map map)
        {
            return map != null && (map.listerThings.ThingsOfDef(VVThingDefOf.VV_DyingApocrypha).Count > 0 || map.listerThings.ThingsOfDef(VVThingDefOf.VV_ApocryphaRebirth).Count > 0);
        }

        public static void ReleaseAsphodelBound(Map map, IntVec3 fallbackCell)
        {
            var mana = GameComponent_Mana.Of(Current.Game);
            if (map == null || mana == null || mana.AsphodelBoundVivis.Count == 0) { return; }

            var anchor = ViviEndingQuest.Current?.AnchorEverflower;
            var root = anchor != null && anchor.Spawned && anchor.Map == map ? EverflowerPawnHolder.FindReturnCell(anchor, map, 4f) : IntVec3.Invalid;
            if (!root.IsValid)
            {
                root = fallbackCell.ClampInsideMap(map);
            }

            var released = new List<Pawn>();
            foreach (var vivi in mana.AsphodelBoundVivis.ToList())
            {
                if (vivi == null || vivi.Destroyed || vivi.Dead)
                {
                    mana.UnbindFromAsphodel(vivi);
                    continue;
                }

                if (!GenPlace.TryPlaceThing(vivi, root, map, ThingPlaceMode.Near)) { continue; }

                mana.UnbindFromAsphodel(vivi);
                VVEffecterDefOf.VV_Effecter_FairyReturn.SpawnMaintained(vivi, map);
                released.Add(vivi);
            }

            if (released.Count == 0) { return; }

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_AsphodelReleasedLabel.Translate(),
                LocalizeString_Letter.VV_Letter_AsphodelReleased.Translate(released.Select(v => v.LabelShortCap).ToLineList("  - ").Named("PAWNS")),
                LetterDefOf.PositiveEvent,
                new LookTargets(released));
        }

        public static bool HealNonPermanentInjuries(Pawn pawn)
        {
            if (pawn?.health == null || pawn.Dead) { return false; }

            var injuries = new List<Hediff_Injury>();
            pawn.health.hediffSet.GetHediffs(ref injuries, v => !v.IsPermanent());
            for (int i = 0; i < injuries.Count; i++)
            {
                pawn.health.RemoveHediff(injuries[i]);
            }

            if (injuries.Count > 0 && pawn.Spawned)
            {
                FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.HealingCross);
            }

            return injuries.Count > 0;
        }

        public static void BindToAsphodel(List<Pawn> vivis, Map map, IntVec3 soundCell)
        {
            if (vivis.Count == 0) { return; }

            VVSoundDefOf.VV_Fairyfication.PlayOneShot(new TargetInfo(soundCell, map));
            var mana = GameComponent_Mana.Of(Current.Game);
            foreach (var vivi in vivis)
            {
                if (vivi.Spawned)
                {
                    VVEffecterDefOf.VV_Effecter_FairyPhase.Spawn(vivi.Position, vivi.Map).Cleanup();
                }
                mana.BindToAsphodel(vivi);
            }
        }

        public static void DissolvePhantasms(Map map)
        {
            if (map == null) { return; }

            foreach (var pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn.health?.hediffSet?.HasHediff(VVHediffDefOf.VV_DoomPhantasm) == true)
                {
                    Hediff_DoomPhantasm.Dissolve(pawn);
                }
            }
        }

        public static void ShakeCamera(Map map, float magnitude)
        {
            if (map != null && map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(magnitude);
            }
        }

        public static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
