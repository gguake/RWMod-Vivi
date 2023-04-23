using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Hediff_MindTransmitter : HediffWithComps
    {
        /// <summary> 총 사용가능한 대역폭 </summary>
        public int TotalBandWidth => (int)pawn.GetStatValue(VVStatDefOf.VV_MindLinkBandwidth);
        /// <summary> 현재 사용중인 대역폭</summary>
        public int UsedBandwidth => LinkedPawns.Count();

        public bool CanAddMindLink => UsedBandwidth < TotalBandWidth;

        public IEnumerable<Pawn> LinkedPawns => linkedPawns;

        public bool AnySelectedDraftedLinkedVivi => Find.Selector.SelectedPawns
            .Where(v => v.TryGetMindLink(out var mindLink) && mindLink.linker == pawn && v.Drafted)
            .Any();

        public AcceptanceReport CanControlLinkedPawnsNow
        {
            get
            {
                if (pawn.Downed)
                {
                    return LocalizeTexts.MindLinkerDowned.Translate(pawn.Named("PAWN"));
                }
                if (pawn.IsPrisoner)
                {
                    return LocalizeTexts.MindLinkerImprisoned.Translate(pawn.Named("PAWN"));
                }
                if (!pawn.Spawned)
                {
                    Thing spawnedParentOrMe = pawn.SpawnedParentOrMe;
                    if (spawnedParentOrMe is Building)
                    {
                        return LocalizeTexts.MindLinkerInsideContainer.Translate(pawn.Named("PAWN"), spawnedParentOrMe.Named("CONTAINER"));
                    }
                    return false;
                }
                if (pawn.InMentalState)
                {
                    return LocalizeTexts.MindLinkerMentalState.Translate(pawn.Named("PAWN"), pawn.MentalStateDef.Named("MENTALSTATE"));
                }
                return true;
            }
        }

        public float MindLinkStrength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (GenTicks.TicksGame >= _mindLinkNextCacheTick)
                {
                    RecalculateMindLinkStrength();
                }

                return _mindLinkStrengthCached;
            }
        }
        public float MindLinkCommandRadius
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (GenTicks.TicksGame >= _mindLinkNextCacheTick)
                {
                    RecalculateMindLinkStrength();
                }

                return _mindLinkCommandRadius;
            }
        }
        public float MindLinkCommandRadiusSquared
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (GenTicks.TicksGame >= _mindLinkNextCacheTick)
                {
                    RecalculateMindLinkStrength();
                }

                return _mindLinkCommandRadiusSqr;
            }
        }

        public List<Pawn> linkedPawns = new List<Pawn>();

        #region for cache
        private int _mindLinkNextCacheTick = 0;
        private float _mindLinkStrengthCached = 0f;
        private float _mindLinkCommandRadius = 0f;
        private float _mindLinkCommandRadiusSqr = 0f;
        #endregion

        #region overrides

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref linkedPawns, "linkedPawns", LookMode.Reference);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();

            UnassignPawnControlAll();
        }

        public override void Notify_PawnDied()
        {
            base.Notify_PawnDied();

            var linkedPawns = LinkedPawns;
            foreach (var linkedPawn in linkedPawns)
            {
                if (linkedPawn.TryGetViviGene(out var vivi))
                {
                    vivi.Notify_ForceBreakingMindLink();
                }
            }

            UnassignPawnControlAll(directlyRemoveHediff: false);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos() ?? Enumerable.Empty<Gizmo>())
            {
                yield return gizmo;
            }

            yield return new MindTransmitterBandwidthGizmo(pawn, this);
        }
        #endregion

        public void AssignPawnControl(Pawn linked)
        {
            if (linkedPawns.Contains(linked))
            {
                return;
            }
            linkedPawns.Add(linked);

            if (linked.TryGetViviGene(out var vivi))
            {
                vivi.Notify_MakeNewMindLink(pawn);

                if (linked.timetable != null && pawn.timetable?.times != null)
                {
                    for (int i = 0; i < 24; i++)
                    {
                        linked.timetable.SetAssignment(i, pawn.timetable.times[i]);
                    }
                }
            }

            UpdateSeverity();
            Find.ColonistBar.MarkColonistsDirty();

            pawn.relations?.AddDirectRelation(VVPawnRelationDefOf.VV_MindLink, linked);
        }

        public void UnassignPawnControlAll(bool directlyRemoveHediff = true)
        {
            foreach (var linked in linkedPawns)
            {
                if (linked.TryGetViviGene(out var vivi))
                {
                    vivi.Notify_RemoveMindLink(directlyRemoveHediff);
                }

                pawn.relations?.TryRemoveDirectRelation(VVPawnRelationDefOf.VV_MindLink, linked);
            }

            linkedPawns.Clear();
            UpdateSeverity();
            Find.ColonistBar.MarkColonistsDirty();

        }

        public void UnassignPawnControl(Pawn linked, bool directlyRemoveHediff = true)
        {
            if (!linkedPawns.Contains(linked))
            {
                return;
            }
            linkedPawns.Remove(linked);

            if (linked.TryGetViviGene(out var vivi))
            {
                vivi.Notify_RemoveMindLink(directlyRemoveHediff);
            }

            UpdateSeverity();
            Find.ColonistBar.MarkColonistsDirty();

            pawn.relations?.TryRemoveDirectRelation(VVPawnRelationDefOf.VV_MindLink, linked);
        }

        public bool CanCommandTo(LocalTargetInfo target)
        {
            if (!target.Cell.InBounds(pawn.MapHeld))
            {
                return false;
            }
            return pawn.Position.DistanceToSquared(target.Cell) < MindLinkCommandRadiusSquared;
        }

        public void UndraftAllLinked()
        {
            foreach (var drafted in LinkedPawns.Where(pawn => pawn.MapHeld == this.pawn.MapHeld && pawn.Drafted))
            {
                drafted.drafter.Drafted = false;
            }
        }

        public void RecalculateMindLinkStrength()
        {
            _mindLinkStrengthCached = pawn.GetStatValue(VVStatDefOf.VV_MindLinkStrength);
            _mindLinkCommandRadius = pawn.GetStatValue(VVStatDefOf.VV_MindLinkRange);
            _mindLinkCommandRadiusSqr = _mindLinkCommandRadius * _mindLinkCommandRadius;

            _mindLinkNextCacheTick = GenTicks.TicksGame + GenTicks.TickRareInterval;
        }

        private void UpdateSeverity()
        {
            if (pawn.health.ShouldBeDead()) { return; }

            Severity = UsedBandwidth;
        }

        #region Notifications
        public void Notify_DrawAt(Vector3 drawLoc, bool flip)
        {
            if (pawn.Spawned && AnySelectedDraftedLinkedVivi)
            {
                GenDraw.DrawRadiusRing(pawn.Position, MindLinkCommandRadius, Color.white, (IntVec3 c) => CanCommandTo(c));
            }
        }
        public void Notify_ChangedGuestStatus()
        {
            if (pawn.IsPrisoner)
            {
                UndraftAllLinked();
            }
        }
        public void Notify_ApparelChanged()
        {
            Notify_BandwidthChanged();
        }
        public void Notify_HediffStateChange(Hediff hediff)
        {
            if (hediff != null)
            {
                Notify_BandwidthChanged();
            }
        }
        public void Notify_Spawned(bool respawningAfterLoad)
        {
            if (respawningAfterLoad)
            {
                Notify_BandwidthChanged();
            }
        }
        public void Notify_BandwidthChanged()
        {
            // TODO
        }
        public void Notify_ChangedFaction()
        {
            foreach (var linked in LinkedPawns)
            {
                if (linked.Faction != pawn.Faction)
                {
                    linked.SetFaction(pawn.Faction);
                }
            }
        }
        public void Notify_Downed()
        {
            UndraftAllLinked();
        }
        public void Notify_DeSpawned(DestroyMode mode)
        {
            if (mode != DestroyMode.WillReplace)
            {
                UndraftAllLinked();
            }
        }
        #endregion
    }
}
