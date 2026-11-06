using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 보관된 폰은 틱을 처리하지 않으므로 ViviEndingQuest.Tick에서 TickHeld를 호출해 동화 상태와 입자 연출을 갱신한다.
    public class Hediff_EverflowerAssimilation : HediffWithComps
    {
        public const int RiseTicks = 120;
        public const int DescendTicks = 150;
        private const float FloatHeight = 1.1f;
        private const float BobHeight = 0.07f;
        private const float BobSpeed = 0.045f;

        private const int CheckInterval = 30;
        private const int ParticleInterval = 4;
        private const int CircleInterval = 72;
        private const float CircleScale = 0.45f;

        private const float BodyCenterHeight = 0.3f;
        private const float LinkWidth = 0.55f;
        private const float ParticleLifeSecs = 1.1f;
        private static readonly Color LinkColor = new Color(1f, 0.9f, 0.55f, 0.55f);

        // 폰 층 바로 위에 그린다. 동화 중에는 영원꽃을 폰 층 바로 아래로 내린다.
        private const float DrawAltitudeOffset = 0.05f;

        private static readonly HashSet<Pawn> _assimilated = new HashSet<Pawn>();

        public ArcanePlant_Everflower everflower;

        private IntVec3 _holdCell = IntVec3.Invalid;
        private Vector3 _glideFrom;
        private int _startTick = -1;
        private int _releaseTick = -1;
        private Vector3 _releaseFrom;
        private bool _broken;

        // 매 프레임 조회하는 등록부. 새 게임과 불러오기 시 ViviEndingQuest 생성자에서 비운다.
        public static bool IsAssimilated(Pawn pawn)
        {
            return pawn != null && _assimilated.Count > 0 && _assimilated.Contains(pawn);
        }

        public static void ResetRegistry()
        {
            _assimilated.Clear();
        }

        public static Hediff_EverflowerAssimilation Of(Pawn pawn)
        {
            return IsAssimilated(pawn) ? pawn.health?.hediffSet?.GetFirstHediff<Hediff_EverflowerAssimilation>() : null;
        }

        public static bool IsFloatingBefore(ArcanePlant_Everflower everflower)
        {
            if (_assimilated.Count == 0 || everflower == null) { return false; }

            foreach (var pawn in _assimilated)
            {
                if (Of(pawn)?.everflower == everflower)
                {
                    return true;
                }
            }

            return false;
        }

        public static Hediff_EverflowerAssimilation Begin(Pawn pawn, ArcanePlant_Everflower everflower, Building_EverflowerAltar altar)
        {
            var existing = Of(pawn);
            if (existing != null) { return existing; }
            if (pawn == null || everflower == null || altar == null || !pawn.Spawned) { return null; }

            var map = pawn.Map;
            var hediff = (Hediff_EverflowerAssimilation)HediffMaker.MakeHediff(VVHediffDefOf.VV_EverflowerFullAssimilation, pawn);
            hediff.everflower = everflower;
            hediff._holdCell = everflower.Position + IntVec3.South;
            hediff._glideFrom = (pawn.Position.ToVector3Shifted() - hediff._holdCell.ToVector3Shifted()).WithY(0f);
            pawn.health.AddHediff(hediff);

            if (!altar.HoldResonator(pawn))
            {
                pawn.health.RemoveHediff(hediff);
                return null;
            }

            VVEffecterDefOf.VV_Effecter_AssimilationBegin?.Spawn(hediff._holdCell, map).Cleanup();
            Messages.Message(LocalizeString_Message.VV_Message_AssimilationBegan.Translate(pawn.Named("PAWN")), new LookTargets(everflower), MessageTypeDefOf.NeutralEvent);
            return hediff;
        }

        public bool IsReleasing => _releaseTick >= 0;

        // 부유 진행도: 0은 지면, 1은 공중. 연결선과 입자 강도에 사용한다.
        public float FloatFactor
        {
            get
            {
                var now = Find.TickManager.TicksGame;
                if (_releaseTick >= 0)
                {
                    return 1f - Mathf.Clamp01((now - _releaseTick) / (float)DescendTicks);
                }

                return _startTick < 0 ? 1f : Mathf.Clamp01((now - _startTick) / (float)RiseTicks);
            }
        }

        public Vector3 BodyOffset
        {
            get
            {
                var now = Find.TickManager.TicksGame;
                if (_releaseTick >= 0)
                {
                    var down = Mathf.Clamp01((now - _releaseTick) / (float)DescendTicks);
                    return Vector3.Lerp(_releaseFrom, Vector3.zero, EaseInOut(down));
                }

                var rise = _startTick < 0 ? 1f : Mathf.Clamp01((now - _startTick) / (float)RiseTicks);
                var floatOffset = new Vector3(0f, 0f, FloatHeight + BobHeight * Mathf.Sin(now * BobSpeed));
                return Vector3.Lerp(_glideFrom, floatOffset, EaseInOut(rise));
            }
        }

        public Vector3 PawnDrawPos
        {
            get
            {
                var pos = _holdCell.ToVector3Shifted() + BodyOffset;
                pos.y = AltitudeLayer.Pawn.AltitudeFor() + DrawAltitudeOffset;
                return pos;
            }
        }

        public Vector3 BodyPoint => PawnDrawPos + new Vector3(0f, 0f, BodyCenterHeight);

        public Vector3 BudPoint => everflower.DrawPos + everflower.EverflowerComp.RitualEffecterOffset;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);

            _assimilated.Add(pawn);
            _startTick = Find.TickManager.TicksGame;
        }

        public override void PostRemoved()
        {
            base.PostRemoved();

            _assimilated.Remove(pawn);
            ViviEndingQuest.Current?.Notify_AssimilationEnded(pawn);

            if (!_broken && pawn.Spawned && !pawn.Dead)
            {
                Messages.Message(LocalizeString_Message.VV_Message_AssimilationReleased.Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            if (_broken) { return; }

            _broken = true;
            ViviEndingQuest.Current?.Notify_AssimilationBroken(pawn);
            if (pawn.health.hediffSet.hediffs.Contains(this))
            {
                pawn.health.RemoveHediff(this);
            }
        }

        public void TickHeld(int now)
        {
            if (pawn == null || pawn.Dead || _broken) { return; }

            if (!(pawn.ParentHolder is Building_EverflowerAltar altar))
            {
                pawn.health.RemoveHediff(this);
                return;
            }

            if (IsReleasing)
            {
                if (now - _releaseTick >= DescendTicks)
                {
                    altar.EjectResonator(pawn, _holdCell);
                    pawn.health.RemoveHediff(this);
                    return;
                }
            }
            else if (now % CheckInterval == 0 && !ShouldKeep())
            {
                Release();
            }

            ThrowParticles(now);
        }

        private bool ShouldKeep()
        {
            if (everflower == null || everflower.Destroyed || !everflower.Spawned || everflower.Map != pawn.MapHeld) { return false; }

            return ViviEndingQuest.Current?.ShouldKeepAssimilation(pawn, everflower) == true;
        }

        public void Release()
        {
            if (IsReleasing || _broken) { return; }

            _releaseFrom = BodyOffset;
            _releaseTick = Find.TickManager.TicksGame;
        }

        private void ThrowParticles(int now)
        {
            if (everflower == null || !everflower.Spawned) { return; }

            var map = everflower.Map;
            var strength = FloatFactor;
            if (strength <= 0.05f) { return; }

            var body = BodyPoint;
            if (!body.ShouldSpawnMotesAt(map)) { return; }

            if ((now + pawn.thingIDNumber) % ParticleInterval == 0)
            {
                var bud = BudPoint;
                var toBody = Rand.Bool;
                var from = toBody ? bud : body;
                var to = toBody ? body : bud;
                var delta = (to - from).Yto0();
                var side = new Vector3(delta.z, 0f, -delta.x).normalized;
                var start = from + side * Rand.Range(-0.35f, 0.35f);
                var travel = (to - start).Yto0();

                var data = FleckMaker.GetDataStatic(start, map, VVFleckDefOf.VV_Fleck_AssimilationMote, Rand.Range(0.3f, 0.55f) * strength);
                data.velocityAngle = travel.AngleFlat();
                data.velocitySpeed = travel.magnitude / ParticleLifeSecs;
                data.rotation = Rand.Range(0f, 360f);
                map.flecks.CreateFleck(data);
            }

            if ((now + pawn.thingIDNumber) % CircleInterval == 0 && !IsReleasing)
            {
                MoteMaker.MakeStaticMote(body, map, VVThingDefOf.VV_Mote_EverflowerCircle, CircleScale);
            }
        }

        public void DrawLink()
        {
            if (everflower == null || !everflower.Spawned) { return; }

            var strength = FloatFactor;
            if (strength <= 0.01f) { return; }

            var color = LinkColor;
            color.a *= strength * (0.75f + 0.25f * Mathf.Sin(Find.TickManager.TicksGame * 0.06f));

            var a = BodyPoint;
            var b = BudPoint;
            a.y = b.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();
            SoulLinkRenderer.Draw(a, b, LinkWidth, color);
        }

        private static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref everflower, "everflower");
            Scribe_Values.Look(ref _holdCell, "holdCell", IntVec3.Invalid);
            Scribe_Values.Look(ref _glideFrom, "glideFrom");
            Scribe_Values.Look(ref _startTick, "startTick", -1);
            Scribe_Values.Look(ref _releaseTick, "releaseTick", -1);
            Scribe_Values.Look(ref _releaseFrom, "releaseFrom");

            if (Scribe.mode == LoadSaveMode.PostLoadInit && pawn != null)
            {
                _assimilated.Add(pawn);
            }
        }
    }
}
