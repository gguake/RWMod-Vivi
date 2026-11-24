using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public enum FairyChainState : byte
    {
        WaitingOnFlower = 0,
        TetheredToPawn = 1,
        CollapsingFail = 2,
        CollapsingDestroyed = 3,
        CollapsingDispel = 4,
    }

    public class FairyFlowerChain : IExposable
    {
        public FairyChainState state = FairyChainState.WaitingOnFlower;
        public List<Building_FairyFlower> flowers = new List<Building_FairyFlower>();
        public Pawn tetheredPawn;
        public List<Pawn> boundVivis = new List<Pawn>();
        public List<ViviFairy> orbitFairies = new List<ViviFairy>();
        public Building_EverflowerAltar altarTarget;

        public IntVec3 lastTetherCell = IntVec3.Invalid;
        public Vector3 lastTetherPos;
        public bool pendingBondExpired;
        public bool isAltarLinked;

        public int collapseFlowerCount;
        public List<Vector3> collapsePoints = new List<Vector3>();
        public int collapseSegmentIndex;
        public int collapseSegmentStartTick;
        public int collapseSegmentTicks;

        public bool IsActive => state == FairyChainState.WaitingOnFlower || state == FairyChainState.TetheredToPawn;

        public Building_FairyFlower TipFlower => flowers.Count > 0 ? flowers[flowers.Count - 1] : null;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                flowers.RemoveAll(v => v == null || v.Destroyed);
                boundVivis.RemoveAll(v => v == null || v.Destroyed);
                orbitFairies.RemoveAll(v => v == null || v.Destroyed);
            }

            Scribe_Values.Look(ref state, "state", FairyChainState.WaitingOnFlower);
            Scribe_Collections.Look(ref flowers, "flowers", LookMode.Reference);
            Scribe_References.Look(ref tetheredPawn, "tetheredPawn", saveDestroyedThings: true);
            Scribe_Collections.Look(ref boundVivis, "boundVivis", LookMode.Reference);
            Scribe_Collections.Look(ref orbitFairies, "orbitFairies", LookMode.Reference);
            Scribe_References.Look(ref altarTarget, "altarTarget");

            Scribe_Values.Look(ref lastTetherCell, "lastTetherCell", IntVec3.Invalid);
            Scribe_Values.Look(ref lastTetherPos, "lastTetherPos");
            Scribe_Values.Look(ref pendingBondExpired, "pendingBondExpired", false);
            Scribe_Values.Look(ref isAltarLinked, "isAltarLinked", false);

            Scribe_Values.Look(ref collapseFlowerCount, "collapseFlowerCount", 0);
            Scribe_Collections.Look(ref collapsePoints, "collapsePoints", LookMode.Value);
            Scribe_Values.Look(ref collapseSegmentIndex, "collapseSegmentIndex", 0);
            Scribe_Values.Look(ref collapseSegmentStartTick, "collapseSegmentStartTick", 0);
            Scribe_Values.Look(ref collapseSegmentTicks, "collapseSegmentTicks", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                flowers ??= new List<Building_FairyFlower>();
                flowers.RemoveAll(v => v == null);
                boundVivis ??= new List<Pawn>();
                boundVivis.RemoveAll(v => v == null);
                orbitFairies ??= new List<ViviFairy>();
                orbitFairies.RemoveAll(v => v == null);
                collapsePoints ??= new List<Vector3>();
            }
        }
    }

    [StaticConstructorOnStartup]
    public class ApocryphaFairyChains : IExposable
    {
        private const int OrbitFairyLifespanTicks = int.MaxValue / 2;

        private const float FailOrbCellsPerTick = 0.24f;
        private const int MinFailSegmentTicks = 18;
        private const int MaxFailSegmentTicks = 65;

        private const int DestroyedStepTicks = 12;

        private const float DispelOrbCellsPerTick = 0.35f;
        private const int MinDispelSegmentTicks = 16;
        private const int MaxDispelSegmentTicks = 65;

        private const float ChainLinkWidth = 0.62f;
        private const float TetherLinkWidth = 0.72f;
        private const float GoldenLinkWidth = 0.88f;
        private const float GoldenPetalWidth = 1.05f;
        private const float DispelWakeLength = 7.0f;
        private const float DispelWakeOuterWidth = 1.65f;
        private const float DispelWakeCoreWidth = 0.95f;
        private const float FailWakeLength = 3.5f;

        private static readonly Color WhiteBeamColor = new Color(0.95f, 0.98f, 1f, 0.90f);
        private static readonly Color TetherBeamColor = new Color(0.97f, 0.99f, 1f, 0.98f);
        private static readonly Color GoldenBeamColor = new Color(1f, 0.85f, 0.28f, 1f);
        private static readonly Color GoldenPetalColor = new Color(1f, 0.88f, 0.38f, 0.85f);
        private static readonly Color FailOrbColor = new Color(1f, 0.95f, 0.78f, 1f);
        private static readonly Color DispelOrbColor = new Color(1f, 0.86f, 0.30f, 1f);
        private static readonly Color DispelWakeOuterColor = new Color(1f, 0.90f, 0.42f, 0.98f);
        private static readonly Color DispelWakeCoreColor = new Color(1f, 0.98f, 0.84f, 1f);
        private static readonly Color DestroyedLinkColor = new Color(0.8f, 0.82f, 0.88f, 0.45f);

        private static readonly Material OrbGlowMat = MaterialPool.MatFrom("VFX/VV_ScentAura", ShaderDatabase.MoteGlow, Color.white);
        private static readonly Material OrbSwirlMat = MaterialPool.MatFrom("VFX/VV_ScentAura2", ShaderDatabase.MoteGlow, Color.white);
        private static readonly Material OrbCoreMat = MaterialPool.MatFrom("Things/Mote/SparkFlash", ShaderDatabase.MoteGlow, Color.white);
        private static readonly Material OrbTwinkleMat = MaterialPool.MatFrom("VFX/VV_ScentTwinkle", ShaderDatabase.MoteGlow, Color.white);
        private static readonly MaterialPropertyBlock OrbPropertyBlock = new MaterialPropertyBlock();

        private readonly CompApocrypha _comp;
        private List<FairyFlowerChain> _chains = new List<FairyFlowerChain>();
        [Unsaved]
        private bool _checkedLegacyFlowers;

        private Pawn Boss => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public IReadOnlyList<FairyFlowerChain> Chains => _chains;

        public int ActiveChainCount
        {
            get
            {
                var count = 0;
                for (int i = 0; i < _chains.Count; i++)
                {
                    if (_chains[i] != null && _chains[i].IsActive) { count++; }
                }
                return count;
            }
        }

        public ApocryphaFairyChains(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _chains, "chains", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _chains ??= new List<FairyFlowerChain>();
                _chains.RemoveAll(c => c == null);
            }
        }

        #region 조회/등록
        public FairyFlowerChain FindChain(Building_FairyFlower flower)
        {
            if (flower == null) { return null; }

            for (int i = 0; i < _chains.Count; i++)
            {
                if (_chains[i].flowers.Contains(flower))
                {
                    return _chains[i];
                }
            }

            return null;
        }

        public FairyFlowerChain FindChainByPawn(Pawn pawn)
        {
            if (pawn == null) { return null; }

            for (int i = 0; i < _chains.Count; i++)
            {
                if (_chains[i].state == FairyChainState.TetheredToPawn && _chains[i].tetheredPawn == pawn)
                {
                    return _chains[i];
                }
            }

            return null;
        }

        public FairyFlowerChain CreateChain(Building_FairyFlower flower)
        {
            if (flower == null) { return null; }

            var existing = FindChain(flower);
            if (existing != null) { return existing; }

            var chain = new FairyFlowerChain
            {
                state = FairyChainState.WaitingOnFlower,
            };
            chain.flowers.Add(flower);
            _chains.Add(chain);
            return chain;
        }

        public void EnsureTracked(Building_FairyFlower flower)
        {
            if (flower == null || !_comp.IsAsphodelActive) { return; }
            if (FindChain(flower) != null) { return; }

            CreateChain(flower);
        }

        public bool IsFlowerAwaitingInteraction(Building_FairyFlower flower)
        {
            if (flower == null || flower.Ending || flower.ChainCollapsing || flower.Relayed) { return false; }

            var chain = FindChain(flower);
            if (chain == null) { return true; }

            return chain.state == FairyChainState.WaitingOnFlower && chain.TipFlower == flower;
        }

        public void MarkBondExpired(Pawn pawn)
        {
            var chain = FindChainByPawn(pawn);
            if (chain != null)
            {
                chain.pendingBondExpired = true;
            }
        }
        #endregion

        #region 이벤트 처리
        public void Notify_FlowerInteracted(Building_FairyFlower flower, Pawn pawn)
        {
            if (flower == null || !flower.Spawned || pawn == null || pawn.Dead || !pawn.Spawned) { return; }
            if (!_comp.IsAsphodelActive) { return; }

            var chain = FindChain(flower) ?? CreateChain(flower);
            if (chain.state != FairyChainState.WaitingOnFlower || chain.TipFlower != flower) { return; }

            flower.MarkRelayed(pawn);

            RemoveBondHediffSilently(pawn);
            var bond = pawn.health.AddHediff(VVHediffDefOf.VV_AsphodelBond) as Hediff_AsphodelBond;
            bond?.Configure(Props.asphodelBondDurationTicks);

            chain.state = FairyChainState.TetheredToPawn;
            chain.tetheredPawn = pawn;
            chain.lastTetherCell = pawn.Position;
            chain.lastTetherPos = pawn.DrawPos.Yto0();
            chain.pendingBondExpired = false;
            chain.isAltarLinked = IsNearAltar(Boss.Map, pawn.Position, out var altar);
            chain.altarTarget = altar;

            SpawnOrbitFairies(chain, pawn);

            Messages.Message(LocalizeString_Message.VV_Message_FairyFlowerResonated.Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.PositiveEvent);
        }

        public void Notify_FlowerBurst(Building_FairyFlower flower)
        {
            var chain = FindChain(flower);
            if (chain == null || !chain.IsActive) { return; }

            StartFailCollapse(chain, skipTipVanish: true);
        }

        public void Notify_FlowerDestroyedByAttack(Building_FairyFlower flower)
        {
            var chain = FindChain(flower);
            if (chain == null) { return; }

            if (chain.state == FairyChainState.CollapsingFail
                || chain.state == FairyChainState.CollapsingDestroyed
                || chain.state == FairyChainState.CollapsingDispel)
            {
                return;
            }

            if (chain.tetheredPawn != null)
            {
                RemoveBondHediffSilently(chain.tetheredPawn);
                chain.tetheredPawn = null;
            }
            DisperseOrbitFairies(chain);

            chain.flowers.Remove(flower);
            _comp.AddResolveStack();

            StartDestroyedCollapse(chain);
        }
        #endregion

        #region 틱
        public void Tick(int now)
        {
            var map = Boss.Map;
            if (map == null) { return; }

            if (!_checkedLegacyFlowers)
            {
                _checkedLegacyFlowers = true;
                AdoptUntrackedFlowers(map);
            }

            for (int i = _chains.Count - 1; i >= 0; i--)
            {
                var chain = _chains[i];
                switch (chain.state)
                {
                    case FairyChainState.WaitingOnFlower:
                        TickWaitingOnFlower(chain, i);
                        break;

                    case FairyChainState.TetheredToPawn:
                        TickTetheredToPawn(chain, map);
                        break;

                    case FairyChainState.CollapsingFail:
                        TickCollapsingFail(chain, i, now, map);
                        break;

                    case FairyChainState.CollapsingDestroyed:
                        TickCollapsingDestroyed(chain, i, now);
                        break;

                    case FairyChainState.CollapsingDispel:
                        TickCollapsingDispel(chain, i, now, map);
                        break;
                }
            }
        }

        private void TickWaitingOnFlower(FairyFlowerChain chain, int index)
        {
            var tip = chain.TipFlower;
            if (tip == null || tip.Destroyed || !tip.Spawned)
            {
                chain.flowers.RemoveAll(v => v == null || v.Destroyed || !v.Spawned);
                if (chain.flowers.Count == 0)
                {
                    _chains.RemoveAt(index);
                }
                else
                {
                    StartFailCollapse(chain, skipTipVanish: false);
                }
            }
        }

        private void TickTetheredToPawn(FairyFlowerChain chain, Map map)
        {
            var pawn = chain.tetheredPawn;
            if (pawn == null)
            {
                DisperseOrbitFairies(chain);
                StartFailCollapse(chain, skipTipVanish: false);
                return;
            }

            if (pawn.Dead)
            {
                CompleteTether(chain, map, fromDeath: true);
                return;
            }

            if (pawn.Destroyed)
            {
                chain.tetheredPawn = null;
                DisperseOrbitFairies(chain);
                StartFailCollapse(chain, skipTipVanish: false);
                return;
            }

            if (!pawn.Spawned || pawn.Map != map)
            {
                RemoveBondHediffSilently(pawn);
                chain.tetheredPawn = null;
                DisperseOrbitFairies(chain);
                StartFailCollapse(chain, skipTipVanish: false);
                return;
            }

            chain.lastTetherCell = pawn.Position;
            chain.lastTetherPos = pawn.DrawPos.Yto0();
            chain.isAltarLinked = IsNearAltar(map, pawn.Position, out var altar);
            chain.altarTarget = altar;

            var bond = pawn.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_AsphodelBond) as Hediff_AsphodelBond;
            if (chain.pendingBondExpired || (bond != null && bond.TicksLeft <= 1))
            {
                CompleteTether(chain, map, fromDeath: false);
                return;
            }

            if (bond == null)
            {
                chain.tetheredPawn = null;
                DisperseOrbitFairies(chain);
                StartFailCollapse(chain, skipTipVanish: false);
            }
        }

        private void CompleteTether(FairyFlowerChain chain, Map map, bool fromDeath)
        {
            var pawn = chain.tetheredPawn;
            chain.tetheredPawn = null;
            chain.pendingBondExpired = false;
            DisperseOrbitFairies(chain);

            IntVec3 triggerCell;
            Vector3 triggerPos;
            if (fromDeath)
            {
                var corpse = pawn.Corpse;
                if (corpse != null && !corpse.Destroyed && corpse.Spawned && corpse.Map == map)
                {
                    triggerCell = corpse.Position;
                    triggerPos = corpse.DrawPos.Yto0();
                    Find.Selector.Deselect(corpse);
                }
                else
                {
                    triggerCell = chain.lastTetherCell.IsValid ? chain.lastTetherCell : Boss.Position;
                    triggerPos = chain.lastTetherPos;
                }
            }
            else
            {
                triggerCell = pawn.Position;
                triggerPos = pawn.DrawPos.Yto0();
            }

            var nearAltar = IsNearAltar(map, triggerCell, out var altar);
            chain.altarTarget = altar;
            chain.isAltarLinked = nearAltar;

            RemoveBondHediffSilently(pawn);

            if (fromDeath && pawn.Dead)
            {
                if (ResurrectionUtility.TryResurrect(pawn) && !pawn.Dead)
                {
                    ApocryphaUtility.HealAllInjuriesAndRestoreParts(pawn);
                }
            }

            if (!pawn.Dead && !pawn.Destroyed)
            {
                if (!chain.boundVivis.Contains(pawn))
                {
                    chain.boundVivis.Add(pawn);
                }

                ApocryphaUtility.BindToAsphodel(new List<Pawn> { pawn }, map, triggerCell);
            }

            if (nearAltar)
            {
                StartDispelCollapse(chain, map, triggerPos, altar);
                return;
            }

            if (!ApocryphaGimmicks.TryFindNearestRelayFlowerCell(triggerCell, map, out var relayCell))
            {
                StartFailCollapse(chain, skipTipVanish: false);
                return;
            }

            var nextFlower = (Building_FairyFlower)ThingMaker.MakeThing(VVThingDefOf.VV_FairyFlower);
            nextFlower.SuppressAutoChain = true;
            ApocryphaGimmicks.SpawnRelayFlower(nextFlower, relayCell, map);
            if (!nextFlower.Spawned)
            {
                StartFailCollapse(chain, skipTipVanish: false);
                return;
            }

            chain.flowers.Add(nextFlower);
            chain.state = FairyChainState.WaitingOnFlower;
        }
        #endregion

        #region 체인 붕괴/파훼
        private void StartFailCollapse(FairyFlowerChain chain, bool skipTipVanish)
        {
            chain.flowers.RemoveAll(v => v == null || v.Destroyed);
            chain.collapseFlowerCount = Mathf.Max(1, chain.flowers.Count);
            chain.collapsePoints.Clear();

            for (int i = 0; i < chain.flowers.Count; i++)
            {
                chain.flowers[i].MarkChainCollapsing();
                chain.collapsePoints.Add(chain.flowers[i].LinkAnchor.Yto0());
            }

            chain.state = FairyChainState.CollapsingFail;
            if (chain.collapsePoints.Count == 0)
            {
                _comp.RecoverBondMaxFraction(Props.fairyFlowerExpireBondRecover * chain.collapseFlowerCount, showStream: false);
                _chains.Remove(chain);
                return;
            }

            var lastIdx = chain.flowers.Count - 1;
            if (!skipTipVanish)
            {
                chain.flowers[lastIdx].BeginChainVanish();
            }

            chain.collapseSegmentIndex = lastIdx;
            BeginFailSegment(chain, GenTicks.TicksGame);
        }

        private void BeginFailSegment(FairyFlowerChain chain, int now)
        {
            var idx = chain.collapseSegmentIndex;
            var from = chain.collapsePoints[idx];
            var to = idx > 0 ? chain.collapsePoints[idx - 1] : _comp.BodyCenter.Yto0();
            var dist = (to - from).MagnitudeHorizontal();

            chain.collapseSegmentStartTick = now;
            chain.collapseSegmentTicks = Mathf.Clamp(Mathf.RoundToInt(dist / FailOrbCellsPerTick), MinFailSegmentTicks, MaxFailSegmentTicks);
        }

        private void TickCollapsingFail(FairyFlowerChain chain, int listIndex, int now, Map map)
        {
            if (chain.collapseSegmentIndex < 0 || chain.collapseSegmentIndex >= chain.collapsePoints.Count)
            {
                _chains.RemoveAt(listIndex);
                return;
            }

            var ticks = Mathf.Max(1, chain.collapseSegmentTicks);
            var prevT = Mathf.Clamp01((now - 1 - chain.collapseSegmentStartTick) / (float)ticks);
            var t = Mathf.Clamp01((now - chain.collapseSegmentStartTick) / (float)ticks);
            var from = chain.collapsePoints[chain.collapseSegmentIndex];
            var to = chain.collapseSegmentIndex > 0 ? chain.collapsePoints[chain.collapseSegmentIndex - 1] : _comp.BodyCenter.Yto0();
            var prevOrbPos = Vector3.Lerp(from, to, prevT);
            var orbPos = Vector3.Lerp(from, to, t);

            ThrowOrbTrail(map, prevOrbPos, orbPos, (to - from).Yto0(), FailOrbColor, 0.95f, intense: false);

            if (now < chain.collapseSegmentStartTick + chain.collapseSegmentTicks) { return; }

            ThrowOrbWaypointBurst(map, to, FailOrbColor, 1.25f, grandRing: false);

            chain.collapseSegmentIndex--;
            if (chain.collapseSegmentIndex >= 0)
            {
                if (chain.collapseSegmentIndex < chain.flowers.Count)
                {
                    var flower = chain.flowers[chain.collapseSegmentIndex];
                    if (flower != null && !flower.Destroyed)
                    {
                        flower.BeginChainVanish();
                    }
                }

                BeginFailSegment(chain, now);
                return;
            }

            _comp.RecoverBondMaxFraction(Props.fairyFlowerExpireBondRecover * chain.collapseFlowerCount, showStream: false);
            if (Boss.Spawned)
            {
                ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyFlowerVanish, Boss.Position, map, ApocryphaGimmicks.GetBossBodyOffset(Boss));
            }
            _chains.RemoveAt(listIndex);
        }

        private void StartDestroyedCollapse(FairyFlowerChain chain)
        {
            chain.flowers.RemoveAll(v => v == null || v.Destroyed);
            chain.collapsePoints.Clear();

            for (int i = 0; i < chain.flowers.Count; i++)
            {
                chain.flowers[i].MarkChainCollapsing();
                chain.collapsePoints.Add(chain.flowers[i].LinkAnchor.Yto0());
            }

            if (chain.flowers.Count == 0)
            {
                _chains.Remove(chain);
                return;
            }

            chain.state = FairyChainState.CollapsingDestroyed;
            var lastIdx = chain.flowers.Count - 1;
            chain.flowers[lastIdx].BeginChainVanish();
            chain.collapseSegmentIndex = lastIdx - 1;
            chain.collapseSegmentStartTick = GenTicks.TicksGame;
            chain.collapseSegmentTicks = DestroyedStepTicks;

            if (chain.collapseSegmentIndex < 0)
            {
                _chains.Remove(chain);
            }
        }

        private void TickCollapsingDestroyed(FairyFlowerChain chain, int listIndex, int now)
        {
            if (chain.collapseSegmentIndex < 0)
            {
                _chains.RemoveAt(listIndex);
                return;
            }

            if (now < chain.collapseSegmentStartTick + chain.collapseSegmentTicks) { return; }

            if (chain.collapseSegmentIndex < chain.flowers.Count)
            {
                var flower = chain.flowers[chain.collapseSegmentIndex];
                if (flower != null && !flower.Destroyed)
                {
                    flower.BeginChainVanish();
                }
            }

            chain.collapseSegmentIndex--;
            chain.collapseSegmentStartTick = now;

            if (chain.collapseSegmentIndex < 0)
            {
                _chains.RemoveAt(listIndex);
            }
        }

        private void StartDispelCollapse(FairyFlowerChain chain, Map map, Vector3 triggerPos, Building_EverflowerAltar altar)
        {
            chain.flowers.RemoveAll(v => v == null || v.Destroyed);
            chain.collapseFlowerCount = Mathf.Max(1, chain.flowers.Count);
            chain.collapsePoints.Clear();

            for (int i = 0; i < chain.flowers.Count; i++)
            {
                chain.flowers[i].MarkChainCollapsing();
                chain.collapsePoints.Add(chain.flowers[i].LinkAnchor.Yto0());
            }

            chain.collapsePoints.Add(triggerPos.Yto0());
            chain.collapsePoints.Add(GetAltarAnchorPos(map, altar));

            chain.state = FairyChainState.CollapsingDispel;
            chain.collapseSegmentIndex = 0;
            BeginDispelSegment(chain, map, GenTicks.TicksGame);
            ThrowOrbWaypointBurst(map, _comp.BodyCenter.Yto0(), DispelOrbColor, 1.85f, grandRing: true);
        }

        private void BeginDispelSegment(FairyFlowerChain chain, Map map, int now)
        {
            var idx = chain.collapseSegmentIndex;
            var from = idx == 0 ? _comp.BodyCenter.Yto0() : chain.collapsePoints[idx - 1];
            var to = idx == chain.collapsePoints.Count - 1 ? GetAltarAnchorPos(map, chain.altarTarget) : chain.collapsePoints[idx];
            var dist = (to - from).MagnitudeHorizontal();

            chain.collapseSegmentStartTick = now;
            chain.collapseSegmentTicks = Mathf.Clamp(Mathf.RoundToInt(dist / DispelOrbCellsPerTick), MinDispelSegmentTicks, MaxDispelSegmentTicks);
        }

        private void TickCollapsingDispel(FairyFlowerChain chain, int listIndex, int now, Map map)
        {
            if (chain.collapseSegmentIndex < 0 || chain.collapseSegmentIndex >= chain.collapsePoints.Count)
            {
                _chains.RemoveAt(listIndex);
                return;
            }

            var idx = chain.collapseSegmentIndex;
            var ticks = Mathf.Max(1, chain.collapseSegmentTicks);
            var prevT = Mathf.Clamp01((now - 1 - chain.collapseSegmentStartTick) / (float)ticks);
            var t = Mathf.Clamp01((now - chain.collapseSegmentStartTick) / (float)ticks);
            var from = idx == 0 ? _comp.BodyCenter.Yto0() : chain.collapsePoints[idx - 1];
            var to = idx == chain.collapsePoints.Count - 1 ? GetAltarAnchorPos(map, chain.altarTarget) : chain.collapsePoints[idx];
            var prevOrbPos = Vector3.Lerp(from, to, prevT);
            var orbPos = Vector3.Lerp(from, to, t);

            ThrowOrbTrail(map, prevOrbPos, orbPos, (to - from).Yto0(), DispelOrbColor, 1.35f, intense: true);

            if (now < chain.collapseSegmentStartTick + chain.collapseSegmentTicks) { return; }

            ThrowOrbWaypointBurst(map, to, DispelOrbColor, 2.0f, grandRing: false);
            if (idx < chain.flowers.Count && chain.flowers[idx] != null && chain.flowers[idx].Spawned)
            {
                ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyFlowerResonated, chain.flowers[idx].Position, map);
            }

            chain.collapseSegmentIndex++;
            if (chain.collapseSegmentIndex < chain.collapsePoints.Count)
            {
                BeginDispelSegment(chain, map, now);
                return;
            }

            FinishDispelCollapse(chain, listIndex, map);
        }

        private void FinishDispelCollapse(FairyFlowerChain chain, int listIndex, Map map)
        {
            var altar = ResolveAltar(map, chain.altarTarget);
            var released = ReleaseChainSalvationVivis(chain, map, altar, out var effectCell);

            var altarPos = GetAltarAnchorPos(map, altar);
            ThrowOrbWaypointBurst(map, altarPos, DispelOrbColor, 2.25f, grandRing: true);
            ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_ApocryphaReleaseRefresh, effectCell, map);
            ApocryphaUtility.ShakeCamera(map, 0.9f);

            _comp.DrainBondMaxFraction(Props.fairyFlowerDispelBondDrain * chain.collapseFlowerCount);
            var angle = (altarPos - _comp.BodyCenter.Yto0()).AngleFlat();
            _comp.BondShatter.Trigger(angle);

            var lookTarget = released.Count > 0 ? new LookTargets(released) : new LookTargets(new TargetInfo(effectCell, map));
            Messages.Message(LocalizeString_Message.VV_Message_FairyChainDispelled.Translate(), lookTarget, MessageTypeDefOf.PositiveEvent);

            _chains.RemoveAt(listIndex);
        }

        public void SettlePendingDispelsBeforeCleanup()
        {
            var map = Boss.Map;
            if (map == null || _chains.Count == 0) { return; }

            for (int i = _chains.Count - 1; i >= 0; i--)
            {
                var chain = _chains[i];
                if (chain.state != FairyChainState.CollapsingDispel) { continue; }

                var altar = ResolveAltar(map, chain.altarTarget);
                ReleaseChainSalvationVivis(chain, map, altar, out _);
            }
        }

        private List<Pawn> ReleaseChainSalvationVivis(FairyFlowerChain chain, Map map, Building_EverflowerAltar altar, out IntVec3 effectCell)
        {
            for (int i = 0; i < chain.flowers.Count; i++)
            {
                if (chain.flowers[i] != null && !chain.flowers[i].Destroyed)
                {
                    chain.flowers[i].BeginChainVanish();
                }
            }

            var returnCell = ResolveAltarReturnCell(map, altar);
            effectCell = altar != null && altar.Spawned ? altar.Position : returnCell;

            var mana = GameComponent_Mana.Of(Current.Game);
            var released = new List<Pawn>();
            if (mana == null) { return released; }

            for (int i = 0; i < chain.boundVivis.Count; i++)
            {
                var vivi = chain.boundVivis[i];
                if (vivi == null || vivi.Destroyed || vivi.Dead || !mana.AsphodelBoundVivis.Contains(vivi)) { continue; }
                if (!TryPlaceSavedVivi(vivi, returnCell, map)) { continue; }

                mana.UnbindFromAsphodel(vivi);
                RestoreSavedVivi(vivi, map);
                released.Add(vivi);
            }

            chain.boundVivis.RemoveAll(v => released.Contains(v));
            return released;
        }

        private bool TryPlaceSavedVivi(Pawn vivi, IntVec3 returnCell, Map map)
        {
            if (GenPlace.TryPlaceThing(vivi, returnCell, map, ThingPlaceMode.Near)) { return true; }

            var near = CellFinder.StandableCellNear(returnCell, map, 20f);
            if (near.IsValid && GenPlace.TryPlaceThing(vivi, near, map, ThingPlaceMode.Near)) { return true; }

            var bossNear = Boss.Spawned ? CellFinder.StandableCellNear(Boss.Position, map, 25f) : IntVec3.Invalid;
            if (bossNear.IsValid && GenPlace.TryPlaceThing(vivi, bossNear, map, ThingPlaceMode.Near)) { return true; }

            return false;
        }

        private IntVec3 ResolveAltarReturnCell(Map map, Building_EverflowerAltar altar)
        {
            if (altar != null && altar.Spawned && altar.Map == map)
            {
                var front = altar.FrontCell;
                if (front.IsValid && front.InBounds(map)) { return front; }
            }

            var anchor = ViviEndingQuest.Current?.AnchorEverflower;
            if (anchor != null && anchor.Spawned && anchor.Map == map)
            {
                var cell = EverflowerPawnHolder.FindReturnCell(anchor, map, 4f);
                if (cell.IsValid && cell.InBounds(map)) { return cell; }
            }

            return Boss.Position.ClampInsideMap(map);
        }

        private static void RestoreSavedVivi(Pawn vivi, Map map)
        {
            var injuries = new List<Hediff_Injury>();
            vivi.health?.hediffSet?.GetHediffs(ref injuries);
            for (int i = 0; i < injuries.Count; i++)
            {
                vivi.health.RemoveHediff(injuries[i]);
            }

            var bloodLoss = vivi.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null)
            {
                vivi.health.RemoveHediff(bloodLoss);
            }

            if (injuries.Count > 0 || bloodLoss != null)
            {
                vivi.health.CheckForStateChange(null, null);
                if (vivi.Spawned)
                {
                    FleckMaker.ThrowMetaIcon(vivi.Position, vivi.Map, FleckDefOf.HealingCross);
                }
            }

            if (vivi.needs?.food != null)
            {
                vivi.needs.food.CurLevelPercentage = 1f;
            }
            if (vivi.needs?.rest != null)
            {
                vivi.needs.rest.CurLevelPercentage = 1f;
            }

            vivi.needs?.mood?.thoughts?.memories?.TryGainMemory(VVThoughtDefOf.VV_AsphodelSalvation);

            VVEffecterDefOf.VV_Effecter_FairyReturn.SpawnMaintained(vivi, map);
            VVEffecterDefOf.VV_Effecter_ApocryphaReleaseRefresh.SpawnMaintained(vivi, map);
        }
        #endregion

        #region 정리
        public void Clear()
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                var chain = _chains[i];
                if (chain.tetheredPawn != null)
                {
                    RemoveBondHediffSilently(chain.tetheredPawn);
                    chain.tetheredPawn = null;
                }

                DisperseOrbitFairies(chain);

                for (int j = 0; j < chain.flowers.Count; j++)
                {
                    chain.flowers[j]?.MarkChainCollapsing();
                }
            }

            _chains.Clear();
        }
        #endregion

        #region 그리기
        public void Draw()
        {
            var map = Boss.Map;
            if (map == null || _chains.Count == 0) { return; }

            var now = GenTicks.TicksGame;
            var bossPos = _comp.BodyCenter;

            for (int i = 0; i < _chains.Count; i++)
            {
                var chain = _chains[i];
                switch (chain.state)
                {
                    case FairyChainState.WaitingOnFlower:
                    case FairyChainState.TetheredToPawn:
                        DrawActiveChain(chain, bossPos, map);
                        break;

                    case FairyChainState.CollapsingFail:
                        DrawCollapsingFail(chain, bossPos, now);
                        break;

                    case FairyChainState.CollapsingDestroyed:
                        DrawCollapsingDestroyed(chain, bossPos);
                        break;

                    case FairyChainState.CollapsingDispel:
                        DrawCollapsingDispel(chain, bossPos, map, now);
                        break;
                }
            }
        }

        private void DrawActiveChain(FairyFlowerChain chain, Vector3 bossPos, Map map)
        {
            var golden = chain.state == FairyChainState.TetheredToPawn && chain.isAltarLinked;
            var beamColor = golden ? GoldenBeamColor : WhiteBeamColor;
            var beamWidth = golden ? GoldenLinkWidth : ChainLinkWidth;

            var prev = bossPos;
            for (int i = 0; i < chain.flowers.Count; i++)
            {
                var flower = chain.flowers[i];
                if (flower == null || !flower.Spawned || flower.Ending) { continue; }

                var alpha = flower.LinkAlpha;
                var anchor = flower.LinkAnchor;
                if (golden)
                {
                    DrawGoldenSegment(prev, anchor, beamWidth, GoldenPetalWidth, ScaleAlpha(beamColor, alpha), ScaleAlpha(GoldenPetalColor, alpha), flower.thingIDNumber);
                }
                else
                {
                    SoulLinkRenderer.Draw(prev, anchor, beamWidth, ScaleAlpha(beamColor, alpha), desaturate: 1f);
                }
                prev = anchor;
            }

            if (chain.state == FairyChainState.TetheredToPawn && chain.tetheredPawn != null && chain.tetheredPawn.Spawned)
            {
                var pawnPos = chain.tetheredPawn.DrawPos;
                if (chain.tetheredPawn.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_AsphodelBond) is Hediff_AsphodelBond bond)
                {
                    AsphodelBondChainRenderer.Draw(chain.tetheredPawn, bond.Progress);
                }

                if (golden)
                {
                    DrawGoldenSegment(prev, pawnPos, GoldenLinkWidth, GoldenPetalWidth, GoldenBeamColor, GoldenPetalColor, chain.tetheredPawn.thingIDNumber);
                    var altarPos = GetAltarAnchorPos(map, chain.altarTarget);
                    DrawGoldenSegment(pawnPos, altarPos, GoldenLinkWidth, GoldenPetalWidth, GoldenBeamColor, GoldenPetalColor, chain.tetheredPawn.thingIDNumber + 31);
                }
                else
                {
                    SoulLinkRenderer.Draw(prev, pawnPos, TetherLinkWidth, TetherBeamColor, desaturate: 1f);
                }
            }
        }

        private void DrawCollapsingFail(FairyFlowerChain chain, Vector3 bossPos, int now)
        {
            var idx = chain.collapseSegmentIndex;
            if (idx < 0 || idx >= chain.collapsePoints.Count) { return; }

            var t = Mathf.Clamp01((now - chain.collapseSegmentStartTick) / (float)Mathf.Max(1, chain.collapseSegmentTicks));
            var from = chain.collapsePoints[idx];
            var to = idx > 0 ? chain.collapsePoints[idx - 1] : bossPos.Yto0();
            var orbPos = Vector3.Lerp(from, to, t);

            var prev = bossPos;
            for (int i = 0; i < idx; i++)
            {
                var pt = chain.collapsePoints[i];
                SoulLinkRenderer.Draw(prev, pt, ChainLinkWidth, WhiteBeamColor, desaturate: 1f);
                prev = pt;
            }

            SoulLinkRenderer.Draw(prev, orbPos, TetherLinkWidth, FailOrbColor, desaturate: 0.7f);

            var back = from - orbPos;
            var backLen = back.MagnitudeHorizontal();
            if (backLen >= 0.05f)
            {
                var wakeEnd = orbPos + (back / backLen) * Mathf.Min(FailWakeLength, backLen);
                SoulLinkRenderer.Draw(wakeEnd, orbPos, TetherLinkWidth * 1.35f, ScaleAlpha(FailOrbColor, 0.85f), desaturate: 0.85f);
            }

            DrawLightOrb(orbPos, (to - from).Yto0(), FailOrbColor, 1.45f, idx, intense: false);
        }

        private void DrawCollapsingDestroyed(FairyFlowerChain chain, Vector3 bossPos)
        {
            var prev = bossPos;
            for (int i = 0; i <= chain.collapseSegmentIndex && i < chain.collapsePoints.Count; i++)
            {
                var pt = chain.collapsePoints[i];
                SoulLinkRenderer.Draw(prev, pt, ChainLinkWidth * 0.85f, DestroyedLinkColor, desaturate: 1f);
                prev = pt;
            }
        }

        private void DrawCollapsingDispel(FairyFlowerChain chain, Vector3 bossPos, Map map, int now)
        {
            var idx = chain.collapseSegmentIndex;
            if (idx < 0 || idx >= chain.collapsePoints.Count) { return; }

            var t = Mathf.Clamp01((now - chain.collapseSegmentStartTick) / (float)Mathf.Max(1, chain.collapseSegmentTicks));
            var from = idx == 0 ? bossPos.Yto0() : chain.collapsePoints[idx - 1];
            var to = idx == chain.collapsePoints.Count - 1 ? GetAltarAnchorPos(map, chain.altarTarget) : chain.collapsePoints[idx];
            var orbPos = Vector3.Lerp(from, to, t);

            // 빛무리 앞쪽의 남은 황금 체인만 유지해 에너지가 휩쓸려 이동하는 대비를 만든다.
            if ((to - orbPos).MagnitudeHorizontal() >= 0.05f)
            {
                DrawGoldenSegment(orbPos, to, GoldenLinkWidth, GoldenPetalWidth, GoldenBeamColor, GoldenPetalColor, idx * 53);
            }

            var prev = to;
            for (int i = idx + 1; i < chain.collapsePoints.Count; i++)
            {
                var pt = i == chain.collapsePoints.Count - 1 ? GetAltarAnchorPos(map, chain.altarTarget) : chain.collapsePoints[i];
                DrawGoldenSegment(prev, pt, GoldenLinkWidth, GoldenPetalWidth, GoldenBeamColor, GoldenPetalColor, i * 53);
                prev = pt;
            }

            DrawDispelWake(chain, bossPos, idx, orbPos);
            DrawLightOrb(orbPos, (to - from).Yto0(), DispelOrbColor, 2.15f, idx + 17, intense: true);
        }

        private static void DrawDispelWake(FairyFlowerChain chain, Vector3 bossPos, int idx, Vector3 orbPos)
        {
            var remaining = DispelWakeLength;
            var head = orbPos;
            for (int seg = idx; seg >= 0 && remaining > 0.05f; seg--)
            {
                var segStart = seg == 0 ? bossPos.Yto0() : chain.collapsePoints[seg - 1];
                var back = head - segStart;
                var segLen = back.MagnitudeHorizontal();
                if (segLen >= 0.05f)
                {
                    var take = Mathf.Min(remaining, segLen);
                    var tail = head - (back / segLen) * take;
                    var fade = Mathf.Clamp01(remaining / DispelWakeLength);
                    var widthScale = 0.65f + 0.35f * fade;

                    SoulLinkRenderer.Draw(tail, head, DispelWakeOuterWidth * widthScale, ScaleAlpha(DispelWakeOuterColor, fade), desaturate: 0.7f);
                    SoulLinkRenderer.Draw(tail, head, DispelWakeCoreWidth * widthScale, ScaleAlpha(DispelWakeCoreColor, fade), desaturate: 0.9f);
                    PetalLinkRenderer.Draw(tail, head, GoldenPetalWidth * 1.45f, ScaleAlpha(GoldenPetalColor, fade), seg * 53 + 7);

                    remaining -= take;
                }

                head = segStart;
            }
        }

        private static void DrawGoldenSegment(Vector3 a, Vector3 b, float beamWidth, float petalWidth, Color beamColor, Color petalColor, int seed)
        {
            SoulLinkRenderer.Draw(a, b, beamWidth, beamColor, desaturate: 1f);
            PetalLinkRenderer.Draw(a, b, petalWidth, petalColor, seed);
        }

        private static void DrawLightOrb(Vector3 pos, Vector3 moveDir, Color color, float scale, int seed, bool intense)
        {
            var age = LinkRenderUtility.WrappedAgeSecs;
            var pulse = 0.90f + 0.12f * Mathf.Sin(age * 11f + seed * 1.7f);
            var baseY = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;
            var drawPos = new Vector3(pos.x, baseY, pos.z);

            var glowScale = (intense ? 3.2f : 2.3f) * scale * pulse;
            var swirlScale = (intense ? 1.85f : 1.35f) * scale * pulse;
            var coreScale = (intense ? 1.55f : 1.10f) * scale * pulse;
            var twinkleScale = (intense ? 2.10f : 1.40f) * scale * (1.06f - 0.10f * pulse);

            OrbPropertyBlock.Clear();
            OrbPropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(color.r, color.g, color.b, 0.95f));
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(glowScale, 1f, glowScale)), OrbGlowMat, 0, null, 0, OrbPropertyBlock);

            var swirlRot = Quaternion.AngleAxis((age * 140f + seed * 47f) % 360f, Vector3.up);
            OrbPropertyBlock.Clear();
            OrbPropertyBlock.SetColor(ShaderPropertyIDs.Color, Color.Lerp(color, Color.white, 0.35f));
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawPos + new Vector3(0f, 0.01f, 0f), swirlRot, new Vector3(swirlScale, 1f, swirlScale)), OrbSwirlMat, 0, null, 0, OrbPropertyBlock);

            OrbPropertyBlock.Clear();
            OrbPropertyBlock.SetColor(ShaderPropertyIDs.Color, Color.Lerp(color, Color.white, 0.55f));
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawPos + new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(coreScale, 1f, coreScale)), OrbCoreMat, 0, null, 0, OrbPropertyBlock);

            OrbPropertyBlock.Clear();
            OrbPropertyBlock.SetColor(ShaderPropertyIDs.Color, Color.white);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawPos + new Vector3(0f, 0.03f, 0f), Quaternion.identity, new Vector3(twinkleScale, 1f, twinkleScale)), OrbTwinkleMat, 0, null, 0, OrbPropertyBlock);

            // 중심 주위를 감도는 작은 빛 입자들(빛무리)
            var dir = moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : Vector3.forward;
            var wispCount = intense ? 4 : 2;
            var wispBaseRadius = (intense ? 0.65f : 0.45f) * scale;
            var wispSpeed = intense ? 9.5f : 6.5f;
            var wispColor = Color.Lerp(color, Color.white, 0.65f);
            for (int w = 0; w < wispCount; w++)
            {
                var angle = age * wispSpeed + w * (Mathf.PI * 2f / wispCount) + seed;
                var radius = wispBaseRadius * (0.75f + 0.25f * Mathf.Sin(age * 13f + w * 2.3f));
                var offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius) - dir * (0.25f * scale);
                var wispPos = new Vector3(pos.x + offset.x, baseY + 0.035f, pos.z + offset.z);
                var wispScale = (intense ? 0.48f : 0.34f) * scale;

                OrbPropertyBlock.Clear();
                OrbPropertyBlock.SetColor(ShaderPropertyIDs.Color, wispColor);
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(wispPos, Quaternion.identity, new Vector3(wispScale, 1f, wispScale)), OrbCoreMat, 0, null, 0, OrbPropertyBlock);
            }
        }

        private static Color ScaleAlpha(Color color, float factor)
        {
            color.a *= Mathf.Clamp01(factor);
            return color;
        }
        #endregion

        #region 내부 도우미
        private void SpawnOrbitFairies(FairyFlowerChain chain, Pawn target)
        {
            DisperseOrbitFairies(chain);

            var map = target.Map;
            if (map == null || !Boss.Spawned) { return; }

            var count = Mathf.Max(1, Props.bondGuardFairyCount);
            var center = target.DrawPos.Yto0();
            for (int slot = 0; slot < count; slot++)
            {
                var angle = slot * (Mathf.PI * 2f / count);
                var offset = new Vector3(Mathf.Cos(angle) * FairyToil_AsphodelBondOrbit.StartRadiusX, 0f, Mathf.Sin(angle) * FairyToil_AsphodelBondOrbit.StartRadiusZ);
                var cell = (center + offset).ToIntVec3().ClampInsideMap(map);

                var fairy = (ViviFairy)ThingMaker.MakeThing(VVThingDefOf.VV_ViviFairy);
                fairy.Initialize(Boss, OrbitFairyLifespanTicks);
                GenSpawn.Spawn(fairy, cell, map);
                fairy.StartJob(new FairyJob_AsphodelBondOrbit(Boss, target, slot, count));
                chain.orbitFairies.Add(fairy);
            }
        }

        private static void DisperseOrbitFairies(FairyFlowerChain chain)
        {
            for (int i = 0; i < chain.orbitFairies.Count; i++)
            {
                var fairy = chain.orbitFairies[i];
                if (fairy != null && !fairy.Destroyed)
                {
                    fairy.BeginDematerialize(applyAssimilation: false);
                }
            }

            chain.orbitFairies.Clear();
        }

        private void AdoptUntrackedFlowers(Map map)
        {
            if (!_comp.IsAsphodelActive || !_comp.IsQuestBossFight) { return; }

            var flowers = map.listerThings.ThingsOfDef(VVThingDefOf.VV_FairyFlower);
            for (int i = 0; i < flowers.Count; i++)
            {
                if (flowers[i] is Building_FairyFlower flower && flower.Spawned && !flower.Ending && !flower.ChainCollapsing && FindChain(flower) == null)
                {
                    CreateChain(flower);
                }
            }
        }

        private static Building_EverflowerAltar ResolveAltar(Map map, Building_EverflowerAltar preferred = null)
        {
            if (preferred != null && preferred.Spawned && preferred.Map == map) { return preferred; }

            var anchor = ViviEndingQuest.Current?.AnchorEverflower;
            if (anchor != null && anchor.Spawned && anchor.Map == map)
            {
                var enshrinedAltar = anchor.Position.GetFirstThing<Building_EverflowerAltar>(map);
                if (enshrinedAltar != null && enshrinedAltar.Spawned) { return enshrinedAltar; }
            }

            return Building_EverflowerAltar.FindOnMap(map);
        }

        private bool IsNearAltar(Map map, IntVec3 cell, out Building_EverflowerAltar altar)
        {
            altar = ResolveAltar(map);
            if (altar != null && altar.Spawned)
            {
                return cell.InHorDistOf(altar.Position, Props.fairyChainAltarRadius);
            }

            return false;
        }

        private static Vector3 GetAltarAnchorPos(Map map, Building_EverflowerAltar altar)
        {
            altar = ResolveAltar(map, altar);
            if (altar != null && altar.Spawned && altar.Map == map)
            {
                var enshrined = altar.EnshrinedEverflower;
                if (enshrined != null && enshrined.Spawned)
                {
                    return ApocryphaGimmicks.EverflowerBudPos(enshrined);
                }

                return altar.TrueCenter().Yto0() + new Vector3(0f, 0f, 0.4f);
            }

            return Vector3.zero;
        }

        private static void ThrowOrbTrail(Map map, Vector3 fromPos, Vector3 toPos, Vector3 moveDir, Color color, float scale, bool intense)
        {
            if (map == null) { return; }

            var stepDist = (toPos - fromPos).MagnitudeHorizontal();
            var samples = intense
                ? Mathf.Clamp(Mathf.CeilToInt(stepDist / 0.45f), 2, 5)
                : Mathf.Clamp(Mathf.CeilToInt(stepDist / 0.65f), 1, 3);
            var baseAngle = moveDir.sqrMagnitude > 0.0001f ? moveDir.AngleFlat() : Rand.Range(0f, 360f);

            for (int i = 0; i < samples; i++)
            {
                var frac = samples > 1 ? (i + 1f) / samples : 1f;
                var pos = Vector3.Lerp(fromPos, toPos, frac) + Gen.RandomHorizontalVector(intense ? 0.45f : 0.28f);
                if (!pos.ShouldSpawnMotesAt(map)) { continue; }

                var comet = FleckMaker.GetDataStatic(pos, map, VVFleckDefOf.VV_Fleck_ApocryphaFairyLight, scale * Rand.Range(0.85f, 1.25f));
                comet.velocityAngle = baseAngle + Rand.Range(-22f, 22f);
                comet.velocitySpeed = intense ? Rand.Range(2.2f, 4.6f) : Rand.Range(1.0f, 2.2f);
                comet.solidTimeOverride = intense ? Rand.Range(0.22f, 0.42f) : Rand.Range(0.18f, 0.32f);
                comet.instanceColor = Color.Lerp(color, Color.white, Rand.Range(0.15f, 0.45f));
                map.flecks.CreateFleck(comet);
            }

            if (!toPos.ShouldSpawnMotesAt(map)) { return; }

            var emberCount = intense ? 2 : 1;
            for (int i = 0; i < emberCount; i++)
            {
                var ember = FleckMaker.GetDataStatic(toPos + Gen.RandomHorizontalVector(intense ? 0.55f : 0.3f), map, VVFleckDefOf.VV_Fleck_ApocryphaEmber, intense ? Rand.Range(0.65f, 1.15f) : Rand.Range(0.4f, 0.7f));
                ember.velocityAngle = Rand.Range(0f, 360f);
                ember.velocitySpeed = intense ? Rand.Range(0.8f, 2.6f) : Rand.Range(0.4f, 1.3f);
                ember.instanceColor = Color.Lerp(color, Color.white, Rand.Range(0.2f, 0.55f));
                map.flecks.CreateFleck(ember);
            }
        }

        private static void ThrowOrbWaypointBurst(Map map, Vector3 pos, Color color, float scale, bool grandRing)
        {
            if (map == null || !pos.InBounds(map)) { return; }

            if (grandRing && VVFleckDefOf.VV_Fleck_GoldenWaveRing != null)
            {
                var wave = FleckMaker.GetDataStatic(pos.WithY(AltitudeLayer.MoteOverhead.AltitudeFor()), map, VVFleckDefOf.VV_Fleck_GoldenWaveRing, scale * 0.55f);
                wave.rotation = Rand.Range(0f, 360f);
                map.flecks.CreateFleck(wave);
            }

            if (!pos.ShouldSpawnMotesAt(map)) { return; }

            if (VVFleckDefOf.VV_Fleck_GimmickPhaseRing != null)
            {
                var ring = FleckMaker.GetDataStatic(pos, map, VVFleckDefOf.VV_Fleck_GimmickPhaseRing, scale);
                ring.instanceColor = Color.Lerp(color, Color.white, 0.25f);
                map.flecks.CreateFleck(ring);
            }

            var emberCount = grandRing ? 12 : 7;
            for (int i = 0; i < emberCount; i++)
            {
                var ember = FleckMaker.GetDataStatic(pos + Gen.RandomHorizontalVector(0.35f), map, VVFleckDefOf.VV_Fleck_ApocryphaEmber, Rand.Range(0.6f, 1.15f));
                ember.velocityAngle = Rand.Range(0f, 360f);
                ember.velocitySpeed = Rand.Range(2.0f, 5.5f);
                ember.instanceColor = Color.Lerp(color, Color.white, Rand.Range(0.15f, 0.5f));
                map.flecks.CreateFleck(ember);
            }
        }

        internal static void RemoveBondHediffSilently(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) { return; }

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_AsphodelBond);
            if (hediff == null) { return; }

            if (hediff is Hediff_AsphodelBond bond)
            {
                bond.SuppressRemovalNotification();
            }
            pawn.health.RemoveHediff(hediff);
        }
        #endregion
    }
}
