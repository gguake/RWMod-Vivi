using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class Building_FairyFlower : Building_ApocryphaFlower
    {
        public const int LifespanTicks = 1250;

        private const int FairyCount = 4;
        private const int BurstTicks = 50;
        private const int ResonatedTicks = 45;
        private const float VividFrom = 0.85f;
        private const float BondRecoverFraction = 0.06f;
        private const int ResonanceLightCount = 28;

        private static readonly Color ResonanceLightColor = new Color(1f, 0.9f, 0.6f, 1f);

        public SkillDef skill;

        private int _expireTick = -1;
        private int _endTick = -1;
        private bool _resonated;
        private List<Pawn> _failedPawns = new List<Pawn>();

        public FairySigil Sigil => FairySigil.For(skill);

        public bool Ending => _endTick >= 0;

        public int TicksUntilExpire => _expireTick < 0 ? LifespanTicks : Mathf.Max(0, _expireTick - Find.TickManager.TicksGame);

        public float Progress => 1f - TicksUntilExpire / (float)LifespanTicks;

        public override float LinkAlpha => Ending ? 0f : base.LinkAlpha;

        public float ResonanceChance(Pawn pawn) => Mathf.Clamp01(0.2f + 0.1f * (pawn.skills?.GetSkill(skill)?.Level ?? 0));

        public AcceptanceReport CanResonate(Pawn pawn)
        {
            if (Ending || pawn == null || pawn.Dead || pawn.Downed || pawn.Faction != Faction.OfPlayer) { return false; }

            if (!pawn.IsVivi()) { return LocalizeString_Etc.VV_FailReason_FairyFlowerNotVivi.Translate().Resolve(); }
            if (pawn.DevelopmentalStage.Baby()) { return LocalizeString_Etc.VV_FailReason_FairyFlowerNotAdult.Translate().Resolve(); }
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) { return LocalizeString_Etc.VV_FailReason_CannotMove.Translate().Resolve(); }
            if (_failedPawns.Contains(pawn)) { return LocalizeString_Etc.VV_FailReason_FairyFlowerResonanceFailed.Translate().Resolve(); }

            return true;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            skill ??= ViviEndingQuest.Current?.BossComp?.NextFairyFlowerSkill() ?? FairySigil.SupportedSkills.RandomElement();
            base.SpawnSetup(map, respawningAfterLoad);

            if (_expireTick < 0)
            {
                _expireTick = Find.TickManager.TicksGame + LifespanTicks;
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var now = Find.TickManager.TicksGame;
            if (Ending)
            {
                if (now >= _endTick)
                {
                    Destroy(DestroyMode.Vanish);
                }
            }
            else if (now >= _expireTick)
            {
                Burst();
            }
        }

        public void Notify_Resonated(Pawn pawn, bool success)
        {
            if (!Spawned || Ending || pawn == null || pawn.Dead) { return; }

            if (!success)
            {
                _failedPawns.Add(pawn);
                Messages.Message(LocalizeString_Message.VV_Message_FairyFlowerResonanceFailed.Translate(pawn.Named("PAWN")), new LookTargets(pawn, this), MessageTypeDefOf.NegativeEvent);
                return;
            }

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyAttunement);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
            pawn.health.AddHediff(VVHediffDefOf.VV_FairyAttunement);

            _resonated = true;
            _endTick = Find.TickManager.TicksGame + ResonatedTicks;
            ApocryphaGimmicks.PlayEffect(Extension?.witherEffecter, Position, Map);
            ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyFlowerResonated, pawn.Position, Map);

            var sigil = Sigil;
            if (sigil != null)
            {
                var center = this.TrueCenter();
                var target = pawn.DrawPos;
                for (int i = 0; i < ResonanceLightCount; i++)
                {
                    sigil.Sample(sigil.TotalLength * (i + Rand.Value) / ResonanceLightCount, out var local, out _);
                    var from = FairySigil.ToWorld(center, local);
                    ApocryphaGimmicks.ThrowFairyLight(Map, from, target + Gen.RandomHorizontalVector(0.3f), Rand.Range(5f, 8f), Rand.Range(0.6f, 1f), ResonanceLightColor);
                }
            }

            Messages.Message(LocalizeString_Message.VV_Message_FairyFlowerResonated.Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.PositiveEvent);
        }

        public void Burst()
        {
            if (!Spawned || Ending) { return; }

            var map = Map;
            var boss = ApocryphaGimmicks.FindBoss(map);

            _resonated = false;
            _endTick = Find.TickManager.TicksGame + BurstTicks;
            ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyFlowerBurst, Position, map);
            ApocryphaUtility.ShakeCamera(map, 0.8f);

            var victims = new List<Pawn>();
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (!pawn.Position.InHorDistOf(Position, FairySigil.WorldRadius)) { continue; }

                victims.Add(pawn);
                if (pawn.carryTracker?.CarriedThing is Pawn carried)
                {
                    victims.Add(carried);
                }
            }

            var bound = new List<Pawn>();
            foreach (var pawn in victims)
            {
                if (pawn.Dead || pawn == boss || pawn.TryGetComp<CompApocrypha>() != null) { continue; }
                if (pawn.kindDef == VVPawnKindDefOf.VV_TitanicHornet || pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_DoomPhantasm)) { continue; }

                if (pawn.IsVivi() && !pawn.IsRoyalVivi())
                {
                    bound.Add(pawn);
                    continue;
                }

                ApocryphaUtility.HealNonPermanentInjuries(pawn);
            }

            if (bound.Count > 0)
            {
                ApocryphaUtility.BindToAsphodel(bound, map, Position);

                Find.LetterStack.ReceiveLetter(
                    LocalizeString_Letter.VV_Letter_FairyFlowerBoundLabel.Translate(),
                    LocalizeString_Letter.VV_Letter_FairyFlowerBound.Translate(bound.Select(v => v.LabelShortCap).ToLineList("  - ").Named("PAWNS")),
                    LetterDefOf.NegativeEvent,
                    new LookTargets(new TargetInfo(Position, map)));
            }
            else
            {
                Messages.Message(LocalizeString_Message.VV_Message_FairyFlowerBurst.Translate(), new TargetInfo(Position, map), MessageTypeDefOf.NegativeEvent);
            }

            ViviEndingQuest.Current?.BossComp?.RecoverBond(BondRecoverFraction);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!Ending)
            {
                base.DrawAt(drawLoc, flip);
            }

            var sigil = Sigil;
            if (sigil == null) { return; }

            var center = this.TrueCenter();
            var now = Find.TickManager.TicksGame;
            if (Ending)
            {
                if (_resonated)
                {
                    var t = 1f - Mathf.Clamp01((_endTick - now) / (float)ResonatedTicks);
                    FairySigilRenderer.Draw(center, sigil, FairyCount, Progress, 1.6f * (1f - t), 0.5f * (1f - t));
                }
                else
                {
                    var t = 1f - Mathf.Clamp01((_endTick - now) / (float)BurstTicks);
                    FairySigilRenderer.Draw(center, sigil, FairyCount, 1f, 2.2f * (1f - t) * (1f - t), 1f - 0.4f * t);
                }
                return;
            }

            var progress = Progress;
            var vivid = Mathf.InverseLerp(VividFrom, 1f, progress);
            var pulse = vivid * vivid * (0.5f + 0.5f * Mathf.Sin(now * (0.12f + 0.25f * vivid)));
            FairySigilRenderer.Draw(center, sigil, FairyCount, progress, Mathf.Lerp(0.85f, 1.5f, vivid) + 0.3f * pulse, 0.35f * pulse, faintAlpha: Mathf.Lerp(FairySigilRenderer.DefaultFaintAlpha, 0.3f, vivid));
            FairySigilRenderer.DrawFairies(center, sigil, FairyCount, progress);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (var option in base.GetFloatMenuOptions(selPawn))
            {
                yield return option;
            }

            var report = CanResonate(selPawn);
            if (!report.Accepted)
            {
                if (!report.Reason.NullOrEmpty())
                {
                    yield return new FloatMenuOption($"{LocalizeString_Etc.VV_FloatMenu_ResonateFairyFlower.Translate()} ({report.Reason})", null);
                }
                yield break;
            }

            var label = LocalizeString_Etc.VV_FloatMenu_ResonateFairyFlower.Translate();
            if (!selPawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption($"{label} ({"NoPath".Translate()})", null);
                yield break;
            }

            if (!selPawn.CanReserve(this))
            {
                var reserver = Map.reservationManager.FirstRespectedReserver(this, selPawn) ?? Map.physicalInteractionReservationManager.FirstReserverOf(this);
                var reason = reserver != null ? "ReservedBy".Translate(reserver.LabelShort, reserver) : "Reserved".Translate();
                yield return new FloatMenuOption($"{label} ({reason})", null);
                yield break;
            }

            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(label, () => selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(VVJobDefOf.VV_ResonateFairyFlower, this), JobTag.Misc)),
                selPawn,
                this);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (DebugSettings.godMode && !Ending)
            {
                var nearest = FindNearestResonator();
                yield return EndingVfxUtility.DevAction("DEV: Resonate nearest vivi", () => Notify_Resonated(nearest, true), nearest != null ? null : "No player vivi can resonate.");
                yield return EndingVfxUtility.DevAction("DEV: Burst now", Burst);
            }
        }

        private Pawn FindNearestResonator()
        {
            if (!Spawned) { return null; }

            return Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)
                .Where(p => CanResonate(p).Accepted)
                .OrderBy(p => p.Position.DistanceToSquared(Position))
                .FirstOrDefault();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref skill, "skill");
            Scribe_Values.Look(ref _expireTick, "expireTick", -1);
            Scribe_Values.Look(ref _endTick, "endTick", -1);
            Scribe_Values.Look(ref _resonated, "resonated", false);
            Scribe_Collections.Look(ref _failedPawns, "failedPawns", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _failedPawns ??= new List<Pawn>();
                _failedPawns.RemoveAll(p => p == null);
            }
        }
    }
}
