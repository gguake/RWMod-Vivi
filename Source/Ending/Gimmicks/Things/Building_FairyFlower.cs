using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class Building_FairyFlower : Building_ApocryphaFlower
    {
        public const int LifespanTicks = 1250;

        public const int FairyCount = 4;
        public const int BurstTicks = 50;
        private const int RelayedSigilFadeTicks = 40;
        private const float VividFrom = 0.85f;
        private const int ResonanceLightCount = 28;

        private static readonly Color ResonanceLightColor = new Color(0.95f, 0.98f, 1f, 1f);

        private int _expireTick = -1;
        private int _endTick = -1;
        private bool _relayed;
        private int _relayedTick = -1;
        private bool _chainCollapsing;

        [Unsaved]
        internal bool SuppressAutoChain;

        public FairySigil Sigil => FairySigil.Daffodil;

        public bool Ending => _endTick >= 0;

        public bool Relayed => _relayed;

        public bool ChainCollapsing => _chainCollapsing;

        public int TicksUntilExpire => _expireTick < 0 ? LifespanTicks : Mathf.Max(0, _expireTick - Find.TickManager.TicksGame);

        public float Progress => _relayed ? 0f : 1f - TicksUntilExpire / (float)LifespanTicks;

        public override float LinkAlpha => Ending ? 0f : base.LinkAlpha;

        public AcceptanceReport CanResonate(Pawn pawn)
        {
            if (Ending || _relayed || _chainCollapsing || pawn == null || pawn.Dead || pawn.Downed || pawn.Faction != Faction.OfPlayer) { return false; }

            var bossComp = Spawned ? ApocryphaGimmicks.FindBoss(Map)?.TryGetComp<CompApocrypha>() : null;
            if (bossComp != null && bossComp.IsAsphodelActive && !bossComp.FairyChains.IsFlowerAwaitingInteraction(this))
            {
                return false;
            }

            if (!pawn.IsVivi()) { return LocalizeString_Etc.VV_FailReason_FairyFlowerNotVivi.Translate().Resolve(); }
            if (pawn.IsRoyalVivi()) { return LocalizeString_Etc.VV_FailReason_FairyFlowerRoyalVivi.Translate().Resolve(); }
            if (pawn.DevelopmentalStage.Baby()) { return LocalizeString_Etc.VV_FailReason_FairyFlowerNotAdult.Translate().Resolve(); }
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) { return LocalizeString_Etc.VV_FailReason_CannotMove.Translate().Resolve(); }
            if (pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_AsphodelBond)) { return LocalizeString_Etc.VV_FailReason_FairyFlowerAlreadyBound.Translate().Resolve(); }

            return true;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (_expireTick < 0)
            {
                _expireTick = Find.TickManager.TicksGame + LifespanTicks;
            }

            if (!respawningAfterLoad && !SuppressAutoChain)
            {
                var bossComp = ApocryphaGimmicks.FindBoss(map)?.TryGetComp<CompApocrypha>();
                if (bossComp != null && bossComp.IsAsphodelActive && bossComp.IsQuestBossFight)
                {
                    bossComp.FairyChains.EnsureTracked(this);
                }
            }
            SuppressAutoChain = false;
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
            else if (!_relayed && !_chainCollapsing && now >= _expireTick)
            {
                Burst();
            }
        }

        public void Notify_Interacted(Pawn pawn)
        {
            if (!Spawned || !CanResonate(pawn).Accepted) { return; }

            var bossComp = ApocryphaGimmicks.FindBoss(Map)?.TryGetComp<CompApocrypha>();
            if (bossComp != null && bossComp.IsAsphodelActive)
            {
                bossComp.FairyChains.Notify_FlowerInteracted(this, pawn);
                return;
            }

            MarkRelayed(pawn);
            ApocryphaFairyChains.RemoveBondHediffSilently(pawn);
            pawn.health.AddHediff(VVHediffDefOf.VV_AsphodelBond);
            Messages.Message(LocalizeString_Message.VV_Message_FairyFlowerResonated.Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.PositiveEvent);
        }

        internal void MarkRelayed(Pawn pawn)
        {
            if (_relayed) { return; }

            _relayed = true;
            _relayedTick = Find.TickManager.TicksGame;

            if (!Spawned || pawn == null) { return; }

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
        }

        internal void MarkChainCollapsing()
        {
            _chainCollapsing = true;
        }

        internal void BeginChainVanish()
        {
            _chainCollapsing = true;
            if (Destroyed) { return; }

            if (Spawned)
            {
                ApocryphaGimmicks.PlayEffect(Extension?.witherEffecter, Position, Map);
            }

            Destroy(DestroyMode.Vanish);
        }

        public void Burst()
        {
            if (!Spawned || Ending) { return; }

            var map = Map;
            var boss = ApocryphaGimmicks.FindBoss(map);
            var bossComp = boss?.TryGetComp<CompApocrypha>();

            _endTick = Find.TickManager.TicksGame + BurstTicks;
            _chainCollapsing = true;
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

            if (bossComp?.IsAsphodelActive == true)
            {
                var ward = (FairySigilWard)ThingMaker.MakeThing(VVThingDefOf.VV_FairySigilWard);
                GenSpawn.Spawn(ward, Position, map);
                bossComp.FairyChains.Notify_FlowerBurst(this);
            }
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (Ending || _chainCollapsing || IsBossAttack(dinfo))
            {
                absorbed = true;
                return;
            }

            base.PreApplyDamage(ref dinfo, out absorbed);
        }

        public override void Kill(DamageInfo? dinfo = null, Hediff exactCulprit = null)
        {
            if (Spawned && !Ending && !_chainCollapsing)
            {
                _chainCollapsing = true;
                var bossComp = ApocryphaGimmicks.FindBoss(Map)?.TryGetComp<CompApocrypha>();
                if (bossComp != null && bossComp.IsAsphodelActive)
                {
                    bossComp.FairyChains.Notify_FlowerDestroyedByAttack(this);
                }
            }

            base.Kill(dinfo, exactCulprit);
        }

        private static bool IsBossAttack(DamageInfo dinfo)
        {
            if (dinfo.Instigator is Pawn pawn && pawn.TryGetComp<CompApocrypha>() != null) { return true; }
            if (dinfo.Instigator is ApocryphaNeedle) { return true; }
            if (dinfo.Instigator is ViviFairy fairy && fairy.Owner?.TryGetComp<CompApocrypha>() != null) { return true; }
            if (dinfo.Weapon == VVThingDefOf.VV_ApocryphaNeedle) { return true; }

            return false;
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
                var t = 1f - Mathf.Clamp01((_endTick - now) / (float)BurstTicks);
                FairySigilRenderer.Draw(center, sigil, FairyCount, 1f, 2.2f * (1f - t) * (1f - t), 1f - 0.4f * t);
                return;
            }

            if (_relayed)
            {
                if (_relayedTick >= 0 && now - _relayedTick < RelayedSigilFadeTicks)
                {
                    var t = Mathf.Clamp01((now - _relayedTick) / (float)RelayedSigilFadeTicks);
                    var frozenProgress = 1f - TicksUntilExpire / (float)LifespanTicks;
                    FairySigilRenderer.Draw(center, sigil, FairyCount, frozenProgress, 1.3f * (1f - t), 0.4f * (1f - t));
                }
                return;
            }

            if (_chainCollapsing) { return; }

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
                // 문자열 보간은 TaggedString의 원문(RawText)을 그대로 넣어 이름 색 태그가 풀리지 않은 채 출력된다.
                var reason = (reserver != null ? "ReservedBy".Translate(reserver.LabelShort, reserver) : "Reserved".Translate()).Resolve().StripTags();
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

            if (DebugSettings.godMode && !Ending && !_relayed && !_chainCollapsing)
            {
                var nearest = FindNearestResonator();
                yield return EndingVfxUtility.DevAction("DEV: Resonate nearest vivi", () => Notify_Interacted(nearest), nearest != null ? null : "No player vivi can resonate.");
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
            Scribe_Values.Look(ref _expireTick, "expireTick", -1);
            Scribe_Values.Look(ref _endTick, "endTick", -1);
            Scribe_Values.Look(ref _relayed, "relayed", false);
            Scribe_Values.Look(ref _relayedTick, "relayedTick", -1);
            Scribe_Values.Look(ref _chainCollapsing, "chainCollapsing", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && HitPoints <= 0)
            {
                HitPoints = MaxHitPoints;
            }
        }
    }
}
