using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordToil_RuinHornetWander : LordToil
    {
        private const int FeedIntervalTicks = 2500;
        private const int ReactionCheckIntervalTicks = 30;

        private readonly IntVec3 _anchor;
        private readonly float _wanderRadius;

        public override bool AllowSatisfyLongNeeds => false;

        public LordToil_RuinHornetWander(IntVec3 anchor, float wanderRadius)
        {
            _anchor = anchor;
            _wanderRadius = wanderRadius;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                lord.ownedPawns[i].mindState.duty = new PawnDuty(DutyDefOf.WanderClose, _anchor, _wanderRadius);
            }
        }

        public override void LordToilTick()
        {
            var now = Find.TickManager.TicksGame;
            if (now % FeedIntervalTicks == 0)
            {
                for (int i = 0; i < lord.ownedPawns.Count; i++)
                {
                    var food = lord.ownedPawns[i].needs?.food;
                    if (food != null)
                    {
                        food.CurLevelPercentage = 1f;
                    }
                }
            }

            if (now % ReactionCheckIntervalTicks != 0) { return; }

            if (!(lord.LordJob is LordJob_RuinHornetWander roamJob)) { return; }

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                var pawn = lord.ownedPawns[i];
                if (!pawn.Spawned || pawn.Downed || pawn.MentalStateDef != VVMentalStateDefOf.VV_HornetBerserk) { continue; }
                if (pawn.CurJob == null || pawn.CurJob.jobGiver is JobGiver_HornetBerserk) { continue; }
                if (!roamJob.AnyPotentialTargetNear(pawn)) { continue; }

                if (JobGiver_HornetBerserk.TryPrepareAttackJob(pawn))
                {
                    pawn.jobs.CheckForJobOverride();
                }
            }
        }
    }

    public class LordJob_RuinHornetWander : LordJob
    {
        private const float DefaultWanderRadius = 7f;
        private const float DefaultGuardRadius = 9.5f;
        private const float DefaultRetaliationRadius = 26f;

        private IntVec3 _anchor;
        private float _wanderRadius = DefaultWanderRadius;
        private float _guardRadius = DefaultGuardRadius;
        private float _retaliationRadius = DefaultRetaliationRadius;

        public IntVec3 Anchor => _anchor;
        public float SearchRadius => Mathf.Max(_guardRadius, _retaliationRadius);

        public override bool AddFleeToil => false;

        public LordJob_RuinHornetWander()
        {
        }

        public LordJob_RuinHornetWander(IntVec3 anchor, float wanderRadius = DefaultWanderRadius, float guardRadius = DefaultGuardRadius, float retaliationRadius = DefaultRetaliationRadius)
        {
            _anchor = anchor;
            _wanderRadius = wanderRadius;
            _guardRadius = guardRadius;
            _retaliationRadius = retaliationRadius;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            graph.AddToil(new LordToil_RuinHornetWander(_anchor, _wanderRadius));
            return graph;
        }

        public override bool ShouldRemovePawn(Pawn p, PawnLostCondition reason)
        {
            if (reason == PawnLostCondition.InMentalState && p.MentalStateDef == VVMentalStateDefOf.VV_HornetBerserk)
            {
                return false;
            }

            return true;
        }

        public override bool ValidateAttackTarget(Pawn searcher, Thing target)
        {
            if (target != null && searcher != null && target.Map == searcher.Map)
            {
                var radius = _guardRadius + (searcher.CurrentEffectiveVerb?.EffectiveRange ?? 0f);
                bool protectedAttacked = LordJob_ApocryphaHornetGuard.RecentlyAttacked(target, victim => victim is Pawn p && lord != null && p.GetLord() == lord);
                if (!target.Position.InHorDistOf(_anchor, radius) && !protectedAttacked)
                {
                    return false;
                }
            }

            return base.ValidateAttackTarget(searcher, target);
        }

        public bool AnyPotentialTargetNear(Pawn searcher)
        {
            if (searcher?.Map == null) { return false; }

            var radius = _guardRadius + 2f;
            var radiusSq = radius * radius;
            var targets = searcher.Map.listerThings.ThingsInGroup(ThingRequestGroup.AttackTarget);
            for (int i = 0; i < targets.Count; i++)
            {
                var thing = targets[i];
                if (thing is Pawn pawn && !pawn.Dead && !pawn.Downed && pawn.kindDef != searcher.kindDef)
                {
                    if ((pawn.Position - _anchor).LengthHorizontalSquared <= radiusSq
                        || LordJob_ApocryphaHornetGuard.RecentlyAttacked(pawn, victim => victim is Pawn v && lord != null && v.GetLord() == lord))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _anchor, "anchor", IntVec3.Invalid);
            Scribe_Values.Look(ref _wanderRadius, "wanderRadius", DefaultWanderRadius);
            Scribe_Values.Look(ref _guardRadius, "guardRadius", DefaultGuardRadius);
            Scribe_Values.Look(ref _retaliationRadius, "retaliationRadius", DefaultRetaliationRadius);
        }
    }
}
