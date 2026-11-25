using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaResurrectionEmbrace : IExposable
    {
        private const int FlashTicks = 60;

        private readonly CompApocrypha _comp;

        private int _castStartTick = -1;
        private int _readyTick;
        private int _flashTick = -1;
        private IntVec3 _center = IntVec3.Invalid;
        private bool _pendingAfterShieldRestore;
        private Effecter _castEffecter;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public bool IsCasting => _castStartTick >= 0;
        public int ReadyTick => _readyTick;
        public IntVec3 Center => _center;
        public bool PendingAfterShieldRestore => _pendingAfterShieldRestore;
        public float Progress => IsCasting ? Mathf.Clamp01((GenTicks.TicksGame - _castStartTick) / (float)Mathf.Max(1, Props.embraceCastTicks)) : 0f;
        public int TicksLeft => IsCasting ? Mathf.Max(0, _castStartTick + Props.embraceCastTicks - GenTicks.TicksGame) : 0;

        private int LaneCount => Mathf.Max(1, Props.swarmFairyCount);

        public ApocryphaResurrectionEmbrace(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _castStartTick, "castStartTick", -1);
            Scribe_Values.Look(ref _readyTick, "readyTick", 0);
            Scribe_Values.Look(ref _flashTick, "flashTick", -1);
            Scribe_Values.Look(ref _center, "center", IntVec3.Invalid);
            Scribe_Values.Look(ref _pendingAfterShieldRestore, "pendingAfterShieldRestore", false);
        }

        public void SetReadyTick(int tick)
        {
            _readyTick = tick;
        }

        public void QueueCastAfterShieldRestore()
        {
            _pendingAfterShieldRestore = true;
        }

        public void Tick(int now, bool stunned)
        {
            if (IsCasting)
            {
                TickCast(now, stunned);
                return;
            }

            if (stunned || _comp.ShieldBroken || !_comp.Swarm.Fairies.Any(v => v != null && v.Spawned)) { return; }
            if (_comp.Channel.IsChanneling || _comp.Channel.PendingSteal) { return; }

            if (_pendingAfterShieldRestore)
            {
                _pendingAfterShieldRestore = false;
                StartCast(now);
                return;
            }

            if (now < _readyTick || !Pawn.IsHashIntervalTick(Mathf.Max(1, Props.swarmAttackCooldownTicks.min))) { return; }
            if (!HasTriggerTarget(Pawn.Position)) { return; }

            StartCast(now);
        }

        public void StartCast(int now)
        {
            _castStartTick = now;
            _flashTick = -1;
            _center = Pawn.Position;
            EndingVfxUtility.CleanupEffecter(ref _castEffecter);

            _comp.NeedleAttack.CancelAims();
            _comp.BlastBreach.Cancel();
            _comp.InterruptCurrentJob();
            _comp.SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaEmbraceStart, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
            MaintainCastEffecter();

            _comp.ThrowQuote(LocalizeString_Etc.VV_Speech_AsphodelEmbrace.Translate());
        }

        public void Interrupt()
        {
            if (!IsCasting) { return; }

            Cancel();
            _readyTick = GenTicks.TicksGame + Props.embraceInterruptRecoverTicks;
            if (Pawn.Spawned)
            {
                Messages.Message(LocalizeString_Message.VV_Message_ApocryphaEmbraceInterrupted.Translate(), Pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        public void Cancel()
        {
            _castStartTick = -1;
            _pendingAfterShieldRestore = false;
            EndingVfxUtility.CleanupEffecter(ref _castEffecter);
        }

        public bool TryGetFairyGoal(ViviFairy fairy, out Vector3 goal)
        {
            goal = Vector3.zero;
            if (!IsCasting || !_center.IsValid) { return false; }

            var index = -1;
            var fairies = _comp.Swarm.Fairies;
            for (int i = 0; i < fairies.Count; i++)
            {
                if (fairies[i] == fairy)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0) { return false; }

            FairySigil.Asphodel.GetLanes(LaneCount).GetHead(index % LaneCount, Progress, out var local, out _, out _);
            goal = FairySigil.ToWorld(_center.ToVector3Shifted().Yto0(), local, Props.embraceRadius);
            return true;
        }

        public void Draw()
        {
            if (!_center.IsValid) { return; }

            var center = _center.ToVector3Shifted();
            var now = GenTicks.TicksGame;
            if (IsCasting)
            {
                var progress = Progress;
                var vivid = Mathf.InverseLerp(0.85f, 1f, progress);
                var pulse = vivid * vivid * (0.5f + 0.5f * Mathf.Sin(now * (0.12f + 0.25f * vivid)));
                FairySigilRenderer.Draw(center, FairySigil.Asphodel, LaneCount, progress, Mathf.Lerp(0.95f, 1.6f, vivid) + 0.3f * pulse, 0.35f * pulse,
                    radius: Props.embraceRadius, faintAlpha: Mathf.Lerp(0.16f, 0.32f, vivid));
                return;
            }

            if (_flashTick >= 0 && now - _flashTick < FlashTicks)
            {
                var t = (now - _flashTick) / (float)FlashTicks;
                FairySigilRenderer.Draw(center, FairySigil.Asphodel, LaneCount, 1f, 2.2f * (1f - t) * (1f - t), 1f - 0.4f * t, radius: Props.embraceRadius);
            }
        }

        public void DebugStart()
        {
            if (!Pawn.Spawned || _comp.Phase != ApocryphaPhase.Asphodel || IsCasting) { return; }

            Pawn.stances.stunner.StopStun();
            StartCast(GenTicks.TicksGame);
        }

        private void TickCast(int now, bool stunned)
        {
            if (stunned || _comp.ShieldBroken)
            {
                Interrupt();
                return;
            }

            if (now - _castStartTick >= Props.embraceCastTicks)
            {
                Complete(now);
                return;
            }

            MaintainCastEffecter();
        }

        private void MaintainCastEffecter()
        {
            if (!_center.IsValid || Pawn.Map == null) { return; }

            var source = new TargetInfo(_center, Pawn.Map);
            EndingVfxUtility.MaintainEffecter(ref _castEffecter, VVEffecterDefOf.VV_Effecter_ApocryphaEmbraceCast, source, source);
        }

        private void Complete(int now)
        {
            var map = Pawn.Map;
            var center = _center;
            _castStartTick = -1;
            _flashTick = now;
            _readyTick = now + Props.embraceCooldownTicks;
            EndingVfxUtility.CleanupEffecter(ref _castEffecter);

            ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_ApocryphaEmbrace, center, map, scale: Props.embraceRadius / FairySigil.WorldRadius);
            ApocryphaUtility.ShakeCamera(map, 1f);

            var revived = new List<Pawn>();
            var origins = new List<Vector3>();
            foreach (var corpse in FindCorpses(center).ToList())
            {
                var pawn = corpse.InnerPawn;
                var origin = corpse.DrawPos;
                Find.Selector.Deselect(corpse);
                if (!ResurrectionUtility.TryResurrect(pawn)) { continue; }

                ApocryphaUtility.HealAllInjuriesAndRestoreParts(pawn);
                revived.Add(pawn);
                origins.Add(pawn.Spawned ? pawn.DrawPos : origin);
            }

            foreach (var pawn in FindLivingVivis(center, downedOnly: false).ToList())
            {
                if (revived.Contains(pawn)) { continue; }

                var origin = pawn.Spawned ? pawn.DrawPos : (pawn.SpawnedParentOrMe?.DrawPos ?? center.ToVector3Shifted());
                ApocryphaUtility.HealAllInjuriesAndRestoreParts(pawn);
                revived.Add(pawn);
                origins.Add(origin);
            }

            if (revived.Count == 0) { return; }

            for (int i = 0; i < origins.Count; i++)
            {
                _comp.Bond.SpawnViviReturnBurst(origins[i]);
            }

            ApocryphaUtility.BindToAsphodel(revived, map, center);
            _comp.RecoverBond(Props.embraceBondRecoverPerVivi * revived.Count);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ApocryphaEmbraceLabel.Translate(),
                LocalizeString_Letter.VV_Letter_ApocryphaEmbrace.Translate(revived.Select(v => v.LabelShortCap).ToLineList("  - ").Named("PAWNS")),
                LetterDefOf.NegativeEvent,
                new LookTargets(Pawn));
        }

        private bool HasTriggerTarget(IntVec3 center)
        {
            return FindCorpses(center).Any() || FindLivingVivis(center, downedOnly: true).Any();
        }

        private IEnumerable<Corpse> FindCorpses(IntVec3 center)
        {
            var map = Pawn.Map;
            var corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            for (int i = 0; i < corpses.Count; i++)
            {
                if (corpses[i] is Corpse corpse && IsEligible(corpse, center, map))
                {
                    yield return corpse;
                }
            }
        }

        private IEnumerable<Pawn> FindLivingVivis(IntVec3 center, bool downedOnly)
        {
            var map = Pawn.Map;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (!p.Spawned || p.Map != map || !p.Position.InHorDistOf(center, Props.embraceRadius)) { continue; }

                if (IsEligibleLivingVivi(p, downedOnly))
                {
                    yield return p;
                }

                if (p.carryTracker?.CarriedThing is Pawn carried && IsEligibleLivingVivi(carried, downedOnly))
                {
                    yield return carried;
                }
            }
        }

        private bool IsEligibleLivingVivi(Pawn pawn, bool downedOnly)
        {
            if (pawn == null || pawn.Dead || pawn == Pawn || pawn.TryGetComp<CompApocrypha>() != null) { return false; }
            if (downedOnly && !pawn.Downed) { return false; }
            if (pawn.Faction != Faction.OfPlayer) { return false; }

            return ApocryphaUtility.CanFairyficateVivi(pawn);
        }

        private bool IsEligible(Corpse corpse, IntVec3 center, Map map)
        {
            if (!corpse.Spawned || corpse.Map != map || !corpse.Position.InHorDistOf(center, Props.embraceRadius)) { return false; }

            var pawn = corpse.InnerPawn;
            if (pawn == null || pawn.Discarded || pawn.Faction != Faction.OfPlayer || !pawn.IsVivi() || pawn.DevelopmentalStage.Baby()) { return false; }
            if (pawn.health?.hediffSet?.HasHediff(VVHediffDefOf.VV_AsphodelBond) == true) { return false; }

            return corpse.GetRotStage() != RotStage.Dessicated;
        }
    }
}
