using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 아포크리파 1페이즈 사출 포격의 조준 하나. 표적 칸과 발사 틱만 저장하고, 경고 연출(표적 연꽃 표식·시전 연꽃·꽃잎과 반짝임)은 CompApocrypha가 매 틱 유지한다.
    public class ApocryphaShellAim : IExposable
    {
        public IntVec3 cell = IntVec3.Invalid;
        public int fireTick = -1;

        // 저장하지 않는 연출
        private Mote _targetMote;
        private Mote _chargeMote;
        private Effecter _effecter;

        public ApocryphaShellAim()
        {
        }

        public ApocryphaShellAim(IntVec3 cell, int fireTick)
        {
            this.cell = cell;
            this.fireTick = fireTick;
        }

        // 기절하면 CompApocrypha가 조준을 모두 취소하므로 연출은 기절 중에 돌지 않는다.
        // aimTicks: 조준 시간. 표식 셰이더는 모트 나이로 피어나는 꽃·충전광을 그리므로, 모트를 (다시) 만들 때 나이를 조준 시작 틱에 맞춘다.
        public void TickVisuals(Pawn boss, Map map, Vector3 bodyCenter, int aimTicks)
        {
            var direction = cell.ToVector3Shifted() - boss.DrawPos;
            direction.y = 0f;
            direction.Normalize();

            var aimStartTick = Mathf.Min(fireTick - aimTicks, Find.TickManager.TicksGame);
            if (_targetMote == null || _targetMote.Destroyed)
            {
                _targetMote = MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, ApocryphaThingDefOf.VV_Mote_ApocryphaAimTarget, 1f, makeOffscreen: true);
                _targetMote?.ForceSpawnTick(aimStartTick);
            }

            if (_targetMote != null)
            {
                _targetMote.exactRotation = direction.AngleFlat();
                _targetMote.Maintain();
            }

            var chargePos = bodyCenter + direction * 1.4f;
            if (_chargeMote == null || _chargeMote.Destroyed)
            {
                _chargeMote = MoteMaker.MakeStaticMote(chargePos, map, ApocryphaThingDefOf.VV_Mote_ApocryphaAimCharge, 1f, makeOffscreen: true);
                _chargeMote?.ForceSpawnTick(aimStartTick);
            }

            if (_chargeMote != null)
            {
                _chargeMote.exactPosition = chargePos;
                _chargeMote.exactRotation = direction.AngleFlat();
                _chargeMote.Maintain();
            }

            var source = new TargetInfo(boss);
            var target = new TargetInfo(cell, map);
            if (_effecter == null)
            {
                _effecter = ApocryphaEffecterDefOf.VV_Effecter_ApocryphaAim.Spawn();
                _effecter.Trigger(source, target);
            }

            _effecter.EffectTick(source, target);
        }

        // 모트는 유지가 끊기면 스스로 사라지므로 이펙터만 정리한다.
        public void CleanupVisuals()
        {
            _effecter?.Cleanup();
            _effecter = null;
            _targetMote = null;
            _chargeMote = null;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref fireTick, "fireTick", -1);
        }
    }
}
