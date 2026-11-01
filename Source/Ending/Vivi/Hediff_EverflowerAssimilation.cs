using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 〈영원꽃 완전 동화〉: 〈대조율 의식〉의 공명자(로열 비비)가 의식을 시작하는 순간(영원꽃 곁에 도착해 의식이 진행되기 시작할 때) 걸린다.
    // 제단 위 영원꽃 앞(남쪽 칸)의 공중에 떠올라 고정되고(JobDriver_EverflowerAssimilation), 영원꽃과 금빛 입자로 이어져 공명한다.
    // 동면처럼 욕구가 줄지 않고 정신 붕괴도 없으며 지시를 받지 않지만(EverflowerAssimilationPatch), 피해는 그대로 받는다(정착민이 치료할 수 있다).
    // 쓰러지거나 죽으면 동화가 끊어진다: 의식 중이면 의식이 취소되고, 엔딩(공성~보스전) 중이면 엔딩이 실패한다(ViviEndingQuest.Notify_AssimilationBroken).
    // 아포크리파를 토벌해 대관식만 남거나, 엔딩이 시작되지 않고 의식이 끝나거나, 엔딩이 다른 이유로 끝나면 천천히 내려와 풀린다.
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

        private static readonly HashSet<Pawn> _assimilated = new HashSet<Pawn>();

        public ArcanePlant_Everflower everflower;

        private IntVec3 _holdCell = IntVec3.Invalid;
        private Vector3 _glideFrom;
        private int _startTick = -1;
        private int _releaseTick = -1;
        private Vector3 _releaseFrom;
        private bool _broken;

        // 렌더링·패치가 자주 묻는다(빠른 조회용 등록부). 게임을 새로 만들거나 불러올 때 비운다(ViviEndingQuest 생성자).
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
                if (pawn.Spawned && pawn.Map == everflower.Map && Of(pawn)?.everflower == everflower)
                {
                    return true;
                }
            }

            return false;
        }

        // 공명자를 동화시킨다(이미 동화돼 있으면 그대로 돌려준다).
        public static Hediff_EverflowerAssimilation Begin(Pawn pawn, ArcanePlant_Everflower everflower)
        {
            var existing = Of(pawn);
            if (existing != null) { return existing; }

            var hediff = (Hediff_EverflowerAssimilation)HediffMaker.MakeHediff(EverflowerAssimilationDefOf.VV_EverflowerFullAssimilation, pawn);
            hediff.everflower = everflower;
            pawn.health.AddHediff(hediff);
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

        // 고정 작업(JobDriver_EverflowerAssimilation.ForcedBodyOffset)이 쓰는 몸 그리기 오프셋: 의식 자리에서 영원꽃 앞 공중으로 떠올라 천천히 흔들리고,
        // 풀릴 때는 그 자리에서 땅으로 내려온다.
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

        public override bool ShouldRemove => _releaseTick >= 0 && Find.TickManager.TicksGame - _releaseTick >= DescendTicks;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);

            _assimilated.Add(pawn);
            _startTick = Find.TickManager.TicksGame;

            if (!pawn.Spawned) { return; }

            // 영원꽃 바로 앞(남쪽) 칸으로 옮겨 그 위 공중에 떠오른다. 의식 자리(원래 칸)에서 미끄러지듯 옮겨 가도록 오프셋을 잡는다.
            // 그려지는 자리(트위너)는 화면을 그릴 때만 따라가므로 칸 중심을 기준으로 삼고 트위너를 새 칸에 맞춘다.
            var oldPos = pawn.Position.ToVector3Shifted();
            var cell = everflower != null && everflower.Spawned ? everflower.Position + IntVec3.South : pawn.Position;
            if (cell != pawn.Position && cell.InBounds(pawn.Map) && cell.Standable(pawn.Map))
            {
                pawn.Position = cell;
                pawn.Notify_Teleported(endCurrentJob: false);
            }

            pawn.Drawer?.tweener?.ResetTweenedPosToRoot();
            _holdCell = pawn.Position;
            _glideFrom = (oldPos - _holdCell.ToVector3Shifted()).WithY(0f);

            if (pawn.drafter != null && pawn.Drafted)
            {
                pawn.drafter.Drafted = false;
            }

            // 생각 트리가 곧바로 고정 작업을 고르게 한다.
            pawn.jobs?.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);

            EverflowerAssimilationDefOf.VV_Effecter_AssimilationBegin?.Spawn(pawn, pawn.Map).Cleanup();
            Messages.Message(LocalizeString_EndingRituals.VV_Message_AssimilationBegan.Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent);
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

        public override void Tick()
        {
            base.Tick();

            if (pawn == null || pawn.Dead || _broken) { return; }

            if (pawn.Downed && !IsReleasing)
            {
                Break(died: false);
                return;
            }

            if (!IsReleasing && pawn.IsHashIntervalTick(CheckInterval) && !ShouldKeep())
            {
                Release();
            }

            if (pawn.Spawned)
            {
                ThrowParticles(Find.TickManager.TicksGame);
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            if (!_broken && !IsReleasing)
            {
                Break(died: true);
            }
        }

        // 공중에 그대로 있어야 하는지: 영원꽃이 같은 맵에 있고, 창구가 동화를 이어 가라고 할 때(대조율 의식 중, 공성~보스전).
        private bool ShouldKeep()
        {
            if (everflower == null || everflower.Destroyed || !everflower.Spawned || everflower.Map != pawn.MapHeld) { return false; }

            return ViviEndingQuest.Current?.ShouldKeepAssimilation(pawn, everflower) == true;
        }

        // 천천히 내려와 풀린다(ShouldRemove).
        public void Release()
        {
            if (IsReleasing || _broken) { return; }

            _releaseFrom = BodyOffset;
            _releaseTick = Find.TickManager.TicksGame;
        }

        // 쓰러지거나 죽어 동화가 끊어진다: 입자가 흩어지고 곧바로 풀리며, 의식·엔딩에 알린다.
        private void Break(bool died)
        {
            _broken = true;

            if (pawn.MapHeld != null && pawn.PositionHeld.IsValid)
            {
                EverflowerAssimilationDefOf.VV_Effecter_AssimilationBroken?.Spawn(pawn.PositionHeld, pawn.MapHeld).Cleanup();
            }

            ViviEndingQuest.Current?.Notify_AssimilationBroken(pawn, died);

            if (pawn.health.hediffSet.hediffs.Contains(this))
            {
                pawn.health.RemoveHediff(this);
            }
        }

        // 꽃봉오리 ↔ 몸통 사이로 금빛 입자가 양쪽으로 흐르고, 이따금 몸 둘레에 공명 고리가 번진다(떠 있는 정도만큼).
        private void ThrowParticles(int now)
        {
            var map = pawn.Map;
            if (everflower == null || !everflower.Spawned || everflower.Map != map) { return; }

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
                MoteMaker.MakeAttachedOverlay(pawn, EverflowerAssimilationDefOf.VV_Mote_EverflowerCircle, new Vector3(0f, 0f, BodyCenterHeight), CircleScale);
            }
        }

        // 몸통 중심(공중에 떠 있는 몸이 그려지는 자리 기준)
        public Vector3 BodyPoint => pawn.DrawPos + new Vector3(0f, 0f, BodyCenterHeight);

        // 영원꽃 꽃봉오리(의식 이펙트가 터지는 자리)
        public Vector3 BudPoint => everflower.DrawPos + everflower.EverflowerComp.RitualEffecterOffset;

        // [CompVivi.PostDraw] 꽃봉오리와 몸통을 잇는 빛줄기. 천천히 맥동한다.
        public void DrawLink()
        {
            if (!pawn.Spawned || everflower == null || !everflower.Spawned || everflower.Map != pawn.Map) { return; }

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
