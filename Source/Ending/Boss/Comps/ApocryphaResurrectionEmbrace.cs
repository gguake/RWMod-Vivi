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
        private const float QuoteHeight = 2.4f;
        private const float QuoteSecs = 5f;
        private const float ReturnLightSpeed = 6f;

        private static readonly Color ReturnLightColor = new Color(1f, 0.95f, 0.8f, 1f);

        private readonly CompApocrypha _comp;

        private int _castStartTick = -1;
        private int _readyTick;
        private int _flashTick = -1;
        private IntVec3 _center = IntVec3.Invalid;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public bool IsCasting => _castStartTick >= 0;
        public int ReadyTick => _readyTick;
        public IntVec3 Center => _center;
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
        }

        public void SetReadyTick(int tick)
        {
            _readyTick = tick;
        }

        public void Tick(int now, bool stunned)
        {
            if (IsCasting)
            {
                TickCast(now, stunned);
                return;
            }

            if (stunned || now < _readyTick || !Pawn.IsHashIntervalTick(Mathf.Max(1, Props.swarmAttackCooldownTicks.min))) { return; }
            if (_comp.ShieldBroken || !_comp.Swarm.Fairies.Any(v => v != null && v.Spawned)) { return; }
            if (_comp.Channel.IsChanneling || _comp.Channel.PendingSteal) { return; }
            if (!FindCorpses(Pawn.Position).Any()) { return; }

            StartCast(now);
        }

        public void StartCast(int now)
        {
            _castStartTick = now;
            _flashTick = -1;
            _center = Pawn.Position;

            _comp.NeedleAttack.CancelAims();
            _comp.BlastBreach.Cancel();
            _comp.InterruptCurrentJob();
            _comp.SpawnEffecter(VVEffecterDefOf.VV_Effecter_FairyFlowerResonated, ApocryphaGimmicks.GetBossBodyOffset(Pawn));

            MoteMaker.ThrowText(_comp.BodyCenter + new Vector3(0f, 0f, QuoteHeight), Pawn.Map, LocalizeString_Etc.VV_Speech_AsphodelEmbrace.Translate(), QuoteSecs);
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

            FairySigil.Everflower.GetLanes(LaneCount).GetHead(index % LaneCount, Progress, out var local, out _, out _);
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
                FairySigilRenderer.Draw(center, FairySigil.Everflower, LaneCount, progress, Mathf.Lerp(0.95f, 1.6f, vivid) + 0.3f * pulse, 0.35f * pulse,
                    radius: Props.embraceRadius, faintAlpha: Mathf.Lerp(0.16f, 0.32f, vivid));
                return;
            }

            if (_flashTick >= 0 && now - _flashTick < FlashTicks)
            {
                var t = (now - _flashTick) / (float)FlashTicks;
                FairySigilRenderer.Draw(center, FairySigil.Everflower, LaneCount, 1f, 2.2f * (1f - t) * (1f - t), 1f - 0.4f * t, radius: Props.embraceRadius);
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
            }
        }

        private void Complete(int now)
        {
            var map = Pawn.Map;
            var center = _center;
            _castStartTick = -1;
            _flashTick = now;
            _readyTick = now + Props.embraceCooldownTicks;

            ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyFlowerBurst, center, map, scale: Props.embraceRadius / FairySigil.WorldRadius);
            ApocryphaUtility.ShakeCamera(map, 1f);

            var revived = new List<Pawn>();
            foreach (var corpse in FindCorpses(center).ToList())
            {
                var pawn = corpse.InnerPawn;
                Find.Selector.Deselect(corpse);
                if (!ResurrectionUtility.TryResurrect(pawn)) { continue; }

                ApocryphaUtility.HealAllInjuriesAndRestoreParts(pawn);
                revived.Add(pawn);
            }

            if (revived.Count == 0) { return; }

            var body = _comp.BodyCenter.Yto0();
            foreach (var pawn in revived)
            {
                if (pawn.Spawned)
                {
                    ApocryphaGimmicks.ThrowFairyLight(map, pawn.DrawPos, body + Gen.RandomHorizontalVector(0.6f), ReturnLightSpeed, 0.8f, ReturnLightColor);
                }
            }

            ApocryphaUtility.BindToAsphodel(revived, map, center);
            _comp.RecoverBond(Props.embraceBondRecoverPerVivi * revived.Count);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ApocryphaEmbraceLabel.Translate(),
                LocalizeString_Letter.VV_Letter_ApocryphaEmbrace.Translate(revived.Select(v => v.LabelShortCap).ToLineList("  - ").Named("PAWNS")),
                LetterDefOf.NegativeEvent,
                new LookTargets(Pawn));
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

        private bool IsEligible(Corpse corpse, IntVec3 center, Map map)
        {
            if (!corpse.Spawned || corpse.Map != map || !corpse.Position.InHorDistOf(center, Props.embraceRadius)) { return false; }

            var pawn = corpse.InnerPawn;
            if (pawn == null || pawn.Discarded || pawn.Faction != Faction.OfPlayer || !pawn.IsVivi() || pawn.IsRoyalVivi()) { return false; }

            return corpse.GetRotStage() != RotStage.Dessicated;
        }
    }
}
