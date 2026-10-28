using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 요정꽃. 아포크리파에게 결속된 고대 요정 비비들의 파장이 응집된 꽃으로, 피해를 받지 않고 하루(60000틱) 뒤 시든다.
    // 아기가 아닌 플레이어 비비가 우클릭 메뉴로 결속하면 〈요정 간섭〉을 얻고 꽃은 사라진다(연결선이 비비에게 옮겨 감).
    public class Building_FairyFlower : Building_ApocryphaFlower
    {
        public const int LifespanTicks = 60000;

        private int _expireTick = -1;

        public int TicksUntilExpire => _expireTick < 0 ? LifespanTicks : System.Math.Max(0, _expireTick - Find.TickManager.TicksGame);

        // 요정꽃과 결속할 수 있는 폰인지(아기가 아니고 움직일 수 있는 플레이어 비비). 거부 사유가 있으면 우클릭 메뉴에 비활성 항목으로 보여 준다.
        public static AcceptanceReport CanBond(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed) { return false; }
            if (pawn.Faction != Faction.OfPlayer) { return false; }

            if (!pawn.IsVivi()) { return LocalizeString_EndingGimmicks.VV_FailReason_FairyFlowerNotVivi.Translate().Resolve(); }
            if (pawn.DevelopmentalStage.Baby()) { return LocalizeString_EndingGimmicks.VV_FailReason_FairyFlowerNotAdult.Translate().Resolve(); }
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
            {
                return LocalizeString_EndingGimmicks.VV_FailReason_FairyFlowerCannotMove.Translate().Resolve();
            }
            if (pawn.health.hediffSet.HasHediff(ApocryphaGimmickDefOf.VV_FairyInterference))
            {
                return LocalizeString_EndingGimmicks.VV_FailReason_FairyFlowerAlreadyInterfered.Translate().Resolve();
            }

            return true;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (_expireTick < 0)
            {
                _expireTick = Find.TickManager.TicksGame + LifespanTicks;
            }
        }

        protected override void Tick()
        {
            base.Tick();

            if (Spawned && _expireTick >= 0 && Find.TickManager.TicksGame >= _expireTick)
            {
                Wither();
            }
        }

        // 결속 작업이 끝났을 때(JobDriver_BondFairyFlower): 비비에게 요정 간섭을 주고 꽃은 사라진다.
        public void Notify_BondedBy(Pawn pawn)
        {
            if (!Spawned || pawn == null || pawn.Dead) { return; }

            if (!pawn.health.hediffSet.HasHediff(ApocryphaGimmickDefOf.VV_FairyInterference))
            {
                pawn.health.AddHediff(ApocryphaGimmickDefOf.VV_FairyInterference);
            }

            Wither();

            Messages.Message(
                LocalizeString_EndingGimmicks.VV_Message_FairyFlowerBonded.Translate(pawn.Named("PAWN")),
                pawn,
                MessageTypeDefOf.PositiveEvent);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (var option in base.GetFloatMenuOptions(selPawn))
            {
                yield return option;
            }

            var label = LocalizeString_EndingGimmicks.VV_FloatMenu_BondFairyFlower.Translate();

            var report = CanBond(selPawn);
            if (!report.Accepted)
            {
                if (!report.Reason.NullOrEmpty())
                {
                    yield return new FloatMenuOption($"{label} ({report.Reason})", null);
                }
                yield break;
            }

            if (!selPawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption($"{label} ({"NoPath".Translate()})", null);
                yield break;
            }

            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(label, () =>
                {
                    var job = JobMaker.MakeJob(ApocryphaGimmickDefOf.VV_BondFairyFlower, this);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }),
                selPawn,
                this);
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            if (sb.Length > 0) { sb.AppendLine(); }

            sb.Append(LocalizeString_EndingGimmicks.VV_Inspect_FairyFlowerFadeIn.Translate(TicksUntilExpire.ToStringTicksToPeriod()));
            sb.AppendLine();
            sb.Append(LocalizeString_EndingGimmicks.VV_Inspect_FairyFlowerHint.Translate());
            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (DebugSettings.godMode)
            {
                var vivi = FindNearestBondableVivi();
                var commandBond = new Command_Action()
                {
                    defaultLabel = "DEV: Bond nearest vivi",
                    action = () =>
                    {
                        var target = FindNearestBondableVivi();
                        if (target != null)
                        {
                            Notify_BondedBy(target);
                        }
                    },
                };
                if (vivi == null)
                {
                    commandBond.Disable("No bondable player vivi without fairy interference on this map.");
                }
                yield return commandBond;

                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Expire now",
                    action = Wither,
                };
            }
        }

        private Pawn FindNearestBondableVivi()
        {
            if (!Spawned) { return null; }

            Pawn nearest = null;
            var nearestDistance = float.MaxValue;
            var pawns = Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < pawns.Count; i++)
            {
                if (!CanBond(pawns[i]).Accepted) { continue; }

                var distance = pawns[i].Position.DistanceToSquared(Position);
                if (distance < nearestDistance)
                {
                    nearest = pawns[i];
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _expireTick, "expireTick", -1);
        }
    }
}
