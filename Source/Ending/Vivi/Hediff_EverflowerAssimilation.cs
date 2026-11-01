using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 〈영원꽃 완전 동화〉: 〈대조율 의식〉의 공명자(로열 비비)가 의식을 시작하는 순간(영원꽃 곁에 도착해 의식이 진행되기 시작할 때) 걸린다.
    // 공명자는 제단 안(Building_DreamumAltar의 보관함)으로 들어가 동면관처럼 정지된다: 욕구·나이가 멈추고, 맵에 없으므로 공격받지도 지시받지도 않는다.
    // 맵에는 영원꽃이 그 모습을 꽃 앞 공중에 떠 있는 것처럼 그리고(ArcanePlant_Everflower.DynamicDrawPhaseAt), 꽃봉오리와 금빛 빛줄기·입자로 잇는다.
    // 보관된 폰은 틱이 돌지 않으므로 판단·입자는 창구(ViviEndingQuest.Tick → TickHeld)가 맡는다.
    // 아포크리파를 토벌해 대관식만 남거나, 엔딩이 시작되지 않고 의식이 끝나거나, 엔딩이 다른 이유로 끝나면 천천히 내려와 제단 앞으로 나온다.
    public class Hediff_EverflowerAssimilation : HediffWithComps
    {
        // 떠오르기(의식 자리에서 영원꽃 앞 공중으로 미끄러지듯)·내려오기에 걸리는 시간과 떠 있는 높이(칸)·흔들림
        public const int RiseTicks = 120;
        public const int DescendTicks = 150;
        private const float FloatHeight = 1.1f;
        private const float BobHeight = 0.07f;
        private const float BobSpeed = 0.045f;

        private const int CheckInterval = 30;
        private const int ParticleInterval = 4;
        private const int CircleInterval = 72;
        private const float CircleScale = 0.45f;

        // 영원꽃 ↔ 비비 연결: 몸통 중심(그려지는 자리에서 조금 위)과 꽃봉오리를 잇는 빛줄기와, 그 위를 양쪽으로 흐르는 입자
        private const float BodyCenterHeight = 0.3f;
        private const float LinkWidth = 0.55f;
        private const float ParticleLifeSecs = 1.1f;
        private static readonly Color LinkColor = new Color(1f, 0.9f, 0.55f, 0.55f);

        // 그리는 높이: 폰 층 바로 위(영원꽃은 동화 중 폰 층 바로 아래로 내려 그린다)
        private const float DrawAltitudeOffset = 0.05f;

        private static readonly HashSet<Pawn> _assimilated = new HashSet<Pawn>();

        public ArcanePlant_Everflower everflower;

        private IntVec3 _holdCell = IntVec3.Invalid;
        private Vector3 _glideFrom;
        private int _startTick = -1;
        private int _releaseTick = -1;
        private Vector3 _releaseFrom;
        private bool _broken;

        // 렌더링이 매 프레임 묻는다(빠른 조회용 등록부). 게임을 새로 만들거나 불러올 때 비운다(ViviEndingQuest 생성자).
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

        // [CompEverflower.DrawAt] 이 영원꽃 앞 공중에 동화 중인 공명자가 떠 있는지(꽃을 공명자 뒤로 그려야 하는지). 매 프레임 불리므로 등록부가 비면 바로 끝낸다.
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

        // 공명자를 동화시켜 제단 안에 보관한다(이미 동화돼 있으면 그대로 돌려준다). 영원꽃 앞(남쪽) 칸 위 공중에 그려지며,
        // 지금 서 있는 칸에서 미끄러지듯 떠오른다.
        public static Hediff_EverflowerAssimilation Begin(Pawn pawn, ArcanePlant_Everflower everflower, Building_DreamumAltar altar)
        {
            var existing = Of(pawn);
            if (existing != null) { return existing; }
            if (pawn == null || everflower == null || altar == null || !pawn.Spawned) { return null; }

            var map = pawn.Map;
            var hediff = (Hediff_EverflowerAssimilation)HediffMaker.MakeHediff(EverflowerAssimilationDefOf.VV_EverflowerFullAssimilation, pawn);
            hediff.everflower = everflower;
            hediff._holdCell = everflower.Position + IntVec3.South;
            hediff._glideFrom = (pawn.Position.ToVector3Shifted() - hediff._holdCell.ToVector3Shifted()).WithY(0f);
            pawn.health.AddHediff(hediff);

            if (!altar.HoldResonator(pawn))
            {
                pawn.health.RemoveHediff(hediff);
                return null;
            }

            EverflowerAssimilationDefOf.VV_Effecter_AssimilationBegin?.Spawn(hediff._holdCell, map).Cleanup();
            Messages.Message(LocalizeString_EndingRituals.VV_Message_AssimilationBegan.Translate(pawn.Named("PAWN")), new LookTargets(everflower), MessageTypeDefOf.NeutralEvent);
            return hediff;
        }

        public bool IsReleasing => _releaseTick >= 0;

        // 떠 있는 정도(0: 땅, 1: 공중). 연결선·입자의 세기에 쓴다.
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

        // 영원꽃 앞 칸 중심에서의 몸 오프셋: 의식 자리에서 공중으로 떠올라 천천히 흔들리고, 풀릴 때는 그 자리에서 땅으로 내려온다.
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
                return Vector3.Lerp(_glideFrom, FloatOffset(now), EaseInOut(rise));
            }
        }

        // [ArcanePlant_Everflower] 보관된 공명자를 그리는 자리(폰 층 바로 위).
        public Vector3 PawnDrawPos
        {
            get
            {
                var pos = _holdCell.ToVector3Shifted() + BodyOffset;
                pos.y = AltitudeLayer.Pawn.AltitudeFor() + DrawAltitudeOffset;
                return pos;
            }
        }

        // 몸통 중심(공중에 떠 있는 몸이 그려지는 자리 기준)
        public Vector3 BodyPoint => PawnDrawPos + new Vector3(0f, 0f, BodyCenterHeight);

        // 영원꽃 꽃봉오리(의식 이펙트가 터지는 자리)
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
                Messages.Message(LocalizeString_EndingRituals.VV_Message_AssimilationReleased.Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        // 보관 중에는 피해를 받지 않지만, 개발자 도구 등으로 죽으면 동화가 끊어진 것으로 처리한다.
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            if (_broken) { return; }

            _broken = true;
            ViviEndingQuest.Current?.Notify_AssimilationBroken(pawn, died: true);
            if (pawn.health.hediffSet.hediffs.Contains(this))
            {
                pawn.health.RemoveHediff(this);
            }
        }

        // [ViviEndingQuest.Tick] 보관된 공명자의 판단과 연출. 제단 밖에 있으면(제단이 사라져 밖으로 나옴) 바로 풀린다.
        public void TickHeld(int now)
        {
            if (pawn == null || pawn.Dead || _broken) { return; }

            if (!(pawn.ParentHolder is Building_DreamumAltar altar))
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

        // 공중에 그대로 있어야 하는지: 영원꽃이 제단에 남아 있고, 창구가 동화를 이어 가라고 할 때(대조율 의식 중, 공성~보스전).
        private bool ShouldKeep()
        {
            if (everflower == null || everflower.Destroyed || !everflower.Spawned || everflower.Map != pawn.MapHeld) { return false; }

            return ViviEndingQuest.Current?.ShouldKeepAssimilation(pawn, everflower) == true;
        }

        // 천천히 내려와 제단 앞으로 나온다(TickHeld).
        public void Release()
        {
            if (IsReleasing || _broken) { return; }

            _releaseFrom = BodyOffset;
            _releaseTick = Find.TickManager.TicksGame;
        }

        // 꽃봉오리 ↔ 몸통 사이로 금빛 입자가 양쪽으로 흐르고, 이따금 몸 둘레에 공명 고리가 번진다(떠 있는 정도만큼).
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

                var data = FleckMaker.GetDataStatic(start, map, EverflowerAssimilationDefOf.VV_Fleck_AssimilationMote, Rand.Range(0.3f, 0.55f) * strength);
                data.velocityAngle = travel.AngleFlat();
                data.velocitySpeed = travel.magnitude / ParticleLifeSecs;
                data.rotation = Rand.Range(0f, 360f);
                map.flecks.CreateFleck(data);
            }

            if ((now + pawn.thingIDNumber) % CircleInterval == 0 && !IsReleasing)
            {
                MoteMaker.MakeStaticMote(body, map, EverflowerAssimilationDefOf.VV_Mote_EverflowerCircle, CircleScale);
            }
        }

        // [ArcanePlant_Everflower] 꽃봉오리와 몸통을 잇는 빛줄기. 천천히 맥동한다.
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
            ApocryphaGimmicks.DrawSoulLink(a, b, LinkWidth, color);
        }

        private static Vector3 FloatOffset(int now)
        {
            return new Vector3(0f, 0f, FloatHeight + BobHeight * Mathf.Sin(now * BobSpeed));
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
