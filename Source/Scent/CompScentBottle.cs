using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class CompProperties_ScentBottle : CompProperties
    {
        public ThingDef pollenDef;
        public int pollenCostPerCharge = 100;
        public int gatherTicks = 1500;
        public int loadTicks = 300;
        public int maxSprays = 5;

        public string filledGraphicPath;
        public string emptyWornGraphicPath;
        public string filledWornGraphicPath;

        public CompProperties_ScentBottle()
        {
            compClass = typeof(CompScentBottle);
        }
    }

    [StaticConstructorOnStartup]
    public class CompScentBottle : ThingComp
    {
        private static readonly Texture2D CollectIcon = ContentFinder<Texture2D>.Get("UI/Commands/VV_GatherScent");

        // 꽃가루 스택을 필요한 양만큼 나눠 예약할 때 쓰는 동시 예약자 상한(바닐라 음식 예약과 같은 값).
        public const int PollenReserveMaxPawns = 10;

        private int pollenLoaded;
        private ThingDef scentFlower;
        private int remainingSprays;

        public CompProperties_ScentBottle Props => (CompProperties_ScentBottle)props;

        public Pawn WearingPawn => (parent as Apparel)?.Wearer;
        public bool PollenCharged => pollenLoaded >= Props.pollenCostPerCharge;
        public int PollenLoaded => pollenLoaded;
        public int PollenNeeded => Mathf.Max(0, Props.pollenCostPerCharge - pollenLoaded);
        public ThingDef ScentFlower => scentFlower;
        public int RemainingSprays => remainingSprays;
        public bool HasScent => scentFlower != null && remainingSprays > 0;

        public Color ScentColor
        {
            get
            {
                var ext = ScentUtility.GetScentExtension(scentFlower);
                return ext != null ? ext.scentColor : Color.white;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref pollenLoaded, "scentPollenLoaded");
            Scribe_Defs.Look(ref scentFlower, "scentFlower");
            Scribe_Values.Look(ref remainingSprays, "scentRemainingSprays");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pollenLoaded = Mathf.Clamp(pollenLoaded, 0, Props.pollenCostPerCharge);
                remainingSprays = Mathf.Clamp(remainingSprays, 0, Props.maxSprays);
                if (scentFlower != null && !ScentUtility.IsScentFlower(scentFlower))
                {
                    scentFlower = null;
                    remainingSprays = 0;
                }
            }
        }

        public void LoadPollenFrom(Thing pollen, int maxCount)
        {
            if (pollen == null || pollen.def != Props.pollenDef) { return; }

            var amount = Mathf.Min(Mathf.Min(pollen.stackCount, maxCount), PollenNeeded);
            if (amount <= 0) { return; }

            pollen.SplitOff(amount).Destroy();
            pollenLoaded += amount;
        }

        public void CompleteCollect(Thing flower)
        {
            if (!PollenCharged || flower == null || !ScentUtility.IsScentFlower(flower.def)) { return; }

            pollenLoaded -= Props.pollenCostPerCharge;
            scentFlower = flower.def;
            remainingSprays = Props.maxSprays;

            var wearer = WearingPawn;
            if (wearer != null)
            {
                ScentUtility.ApplyScent(wearer, scentFlower);
            }

            NotifyScentColorChanged();
        }

        public bool TrySpray(Pawn sprayer, float radius)
        {
            if (!HasScent || sprayer == null || WearingPawn != sprayer || !sprayer.Spawned)
            {
                return false;
            }

            var flowerDef = scentFlower;
            foreach (var target in GenRadial
                .RadialDistinctThingsAround(sprayer.Position, sprayer.Map, radius, true)
                .OfType<Pawn>())
            {
                ScentUtility.ApplyScent(target, flowerDef);
            }

            SpawnSprayEffect(sprayer);

            remainingSprays--;
            if (remainingSprays <= 0)
            {
                remainingSprays = 0;
                scentFlower = null;
                NotifyScentColorChanged();
            }

            return true;
        }

        private void SpawnSprayEffect(Pawn sprayer)
        {
            var effecter = new Effecter(VVEffecterDefOf.VV_ScentSpray);
            foreach (var child in effecter.children)
            {
                child.colorOverride = ScentColor;
            }

            var target = new TargetInfo(sprayer);
            effecter.Trigger(target, target);
            sprayer.Map.effecterMaintainer.AddEffecterToMaintain(
                effecter,
                target,
                target,
                VVEffecterDefOf.VV_ScentSpray.maintainTicks);
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            var wearer = WearingPawn;
            if (wearer == null || wearer.Faction != Faction.OfPlayerSilentFail) { yield break; }
            if (Find.Selector.SelectedPawns.Count > 1) { yield break; }

            yield return MakeCollectCommand(wearer);
        }

        private Command_Target MakeCollectCommand(Pawn wearer)
        {
            return new Command_Target
            {
                defaultLabel = LocalizeString_Scent.VV_Command_CollectScent.Translate(),
                defaultDesc = LocalizeString_Scent.VV_Command_CollectScentDesc.Translate(
                    Props.pollenCostPerCharge, Props.pollenDef.LabelCap, Props.maxSprays),
                icon = CollectIcon,
                // validator를 두면 무효 대상 클릭이 action에 도달하지 못해 거부 메시지를
                // 출력할 수 없다 (Targeter.ProcessInputEvents는 invalid 타겟이면 action을
                // 호출하지 않음). 검증과 메시지는 action 내부의 CanCollectFrom이 담당한다.
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = false,
                    canTargetPawns = false,
                    canTargetBuildings = true,
                    canTargetItems = false,
                    canTargetPlants = true
                },
                action = target =>
                {
                    if (!CanCollectFrom(wearer, target.Thing, out var reason))
                    {
                        if (!reason.NullOrEmpty())
                        {
                            Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                        }
                        return;
                    }

                    var job = JobMaker.MakeJob(VVJobDefOf.VV_CollectFloralScent, target.Thing);
                    if (!PollenCharged)
                    {
                        var pollen = new List<LocalTargetInfo>();
                        var counts = new List<int>();
                        if (!TryFindPollen(wearer, PollenNeeded, pollen, counts))
                        {
                            Messages.Message(
                                LocalizeString_Scent.VV_Scent_NotEnoughPollen.Translate(
                                    PollenNeeded, Props.pollenDef.LabelCap),
                                MessageTypeDefOf.RejectInput, false);
                            return;
                        }

                        job.targetQueueB = pollen;
                        job.countQueue = counts;
                    }

                    wearer.jobs.TryTakeOrderedJob(job);
                },
                onUpdate = target =>
                {
                    if (CanCollectFrom(wearer, target.Thing, out _))
                    {
                        GenDraw.DrawTargetHighlight(target);
                    }
                }
            };
        }

        public bool CanCollectFrom(Pawn collector, Thing target, out string reason)
        {
            reason = null;

            if (collector == null || WearingPawn != collector)
            {
                reason = LocalizeString_Scent.VV_Scent_MustBeWorn.Translate().Resolve();
                return false;
            }

            if (target == null || target.Destroyed || !target.Spawned || !ScentUtility.IsScentFlower(target.def))
            {
                reason = LocalizeString_Scent.VV_Scent_InvalidFlower.Translate().Resolve();
                return false;
            }

            if (target.IsForbidden(collector) || !collector.CanReserveAndReach(target, PathEndMode.Touch, Danger.Some))
            {
                reason = LocalizeString_Scent.VV_Scent_FlowerUnreachable.Translate().Resolve();
                return false;
            }

            return true;
        }

        // 바닐라 RefuelWorkGiverUtility.FindEnoughReservableThings는 스택 전체 예약만 검사해서,
        // 한 폰이 채집하는 동안 같은 스택의 남은 꽃가루를 다른 폰이 쓰지 못한다.
        // 음식처럼 필요한 양만 나눠 예약할 수 있도록 스택별로 가져갈 양을 함께 구한다.
        private bool TryFindPollen(Pawn pawn, int needed, List<LocalTargetInfo> outTargets, List<int> outCounts)
        {
            var candidates = new List<Thing>(pawn.Map.listerThings.ThingsOfDef(Props.pollenDef));
            candidates.SortBy(thing => thing.Position.DistanceToSquared(pawn.Position));

            var remaining = needed;
            for (int i = 0; i < candidates.Count && remaining > 0; ++i)
            {
                var thing = candidates[i];
                if (thing.Fogged() || thing.IsForbidden(pawn)) { continue; }

                var available = AvailablePollen(pawn, thing);
                if (available <= 0) { continue; }
                if (!pawn.CanReach(thing, PathEndMode.ClosestTouch, Danger.Deadly)) { continue; }

                var count = Mathf.Min(available, remaining);
                outTargets.Add(thing);
                outCounts.Add(count);
                remaining -= count;
            }

            return remaining <= 0;
        }

        // 바닐라 CanReserveStack은 다른 폰이 더미 전체를 예약하고 있으면 0을 돌려준다.
        // 작업대 작업(DoBill)은 재료를 조금만 쓸 때도 더미 전체를 예약하므로, 꽃가루가 큰 더미 하나에 몰려 있으면
        // 작업대가 돌 때마다 향 채집이 "꽃가루 부족"으로 막힌다.
        // 그래서 나눠 예약된 양(다른 향 채집)만 빼고, 더미 전체 예약은 없는 것으로 센다.
        public static int AvailablePollen(Pawn pawn, Thing pollen)
        {
            var map = pawn.Map;

            // 작업대에 올려놓은 재료는 건드리지 않는다.
            var physical = map.physicalInteractionReservationManager;
            if (physical.IsReserved(pollen) && !physical.IsReservedBy(pawn, pollen)) { return 0; }

            var available = pollen.stackCount;
            var reservations = map.reservationManager.ReservationsReadOnly;
            for (int i = 0; i < reservations.Count; ++i)
            {
                var reservation = reservations[i];
                if (reservation.Target.Thing != pollen || reservation.Claimant == pawn) { continue; }
                if (reservation.StackCount == ReservationManager.StackCount_All) { continue; }

                available -= reservation.StackCount;
            }

            return Mathf.Max(available, 0);
        }

        public void NotifyScentColorChanged()
        {
            parent.Notify_ColorChanged();
            WearingPawn?.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        public string GetStatusText()
        {
            if (HasScent)
            {
                return LocalizeString_Scent.VV_Scent_StatusScent.Translate(
                    scentFlower.LabelCap, remainingSprays, Props.maxSprays).Resolve();
            }

            if (PollenCharged)
            {
                return LocalizeString_Scent.VV_Scent_StatusCharged.Translate().Resolve();
            }

            if (pollenLoaded > 0)
            {
                return LocalizeString_Scent.VV_Scent_StatusLoading.Translate(
                    pollenLoaded, Props.pollenCostPerCharge).Resolve();
            }

            return LocalizeString_Scent.VV_Scent_StatusEmpty.Translate().Resolve();
        }

        public override string CompInspectStringExtra()
        {
            return GetStatusText();
        }

        public override string TransformLabel(string label)
        {
            return HasScent ? label + " (" + scentFlower.label + ")" : label;
        }
    }
}
