using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 〈요정 간섭〉: 요정꽃과 결속한 비비. 6시간 안에 1페이즈 아포크리파에게 2.9칸 이내로 다가가면
    // 결속망에 마나 역류를 일으켜 결속을 크게 해방하고(보스 쪽 ReleaseBond), 멸망꽃 저주를 모두 풀며,
    // 비비 자신은 잠시 사라졌다가 제단 앞으로 돌아온다(ViviEndingQuest.HoldPawnUntilReturn).
    public class Hediff_FairyInterference : HediffWithComps
    {
        public const float ReleaseRadius = 2.9f;
        public static readonly IntRange ReturnDelayTicks = new IntRange(1500, 2500);

        // 보스 탐색은 퀘스트 기준이면 바로 끝나지만, 맵을 훑어야 할 때는 자주 하지 않는다.
        private const int BossSearchIntervalTicks = 30;

        [Unsaved]
        private Effecter _auraEffecter;

        [Unsaved]
        private Pawn _boss;

        public override void Tick()
        {
            base.Tick();

            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                EndAura();
                return;
            }

            MaintainAura();

            if (pawn.Downed) { return; }

            var boss = GetBoss();
            if (boss == null || !ApocryphaGimmicks.IsBoundBoss(boss, out var comp)) { return; }
            if (!ApocryphaGimmicks.IsBossFightActive) { return; }

            if (pawn.Position.InHorDistOf(boss.Position, ReleaseRadius))
            {
                TriggerManaBackflow(boss, comp);
            }
        }

        // 결속 해방: 마나 역류 연출 → 보스 결속 해방 → 멸망꽃 저주 해제 → 비비는 잠시 사라졌다가 제단 앞으로 귀환
        private void TriggerManaBackflow(Pawn boss, CompApocrypha comp)
        {
            var vivi = pawn;
            var map = vivi.Map;

            // 결속 해방이 2페이즈 전환을 일으키면 간섭 헤디프 일괄 정리가 다시 불리므로, 자기 자신부터 먼저 제거한다.
            EndAura();
            vivi.health.RemoveHediff(this);

            ApocryphaGimmicks.PlayManaBackflow(vivi, boss);
            comp.ReleaseBond(vivi);

            DoomBlightUtility.ClearDoomBlights(map);
            Messages.Message(
                LocalizeString_EndingGimmicks.VV_Message_FairyBondReleased.Translate(vivi.Named("PAWN")),
                new LookTargets(boss),
                MessageTypeDefOf.PositiveEvent);

            // 퀘스트가 스킵 연출과 함께 디스폰해 잠시 보관한 뒤 제단 앞으로 돌려보낸다.
            // (마지막 자유 정착민이면 게임 오버를 피하려고 보류 없이 바로 제단 앞으로 순간이동시킨다)
            var quest = ViviEndingQuest.Current;
            if (quest != null && vivi.Spawned && !vivi.Dead)
            {
                quest.HoldPawnUntilReturn(vivi, ReturnDelayTicks.RandomInRange);
            }
        }

        private Pawn GetBoss()
        {
            if (_boss != null && _boss.Spawned && _boss.Map == pawn.Map) { return _boss; }

            var questBoss = ViviEndingQuest.Current?.Boss;
            if (questBoss != null && questBoss.Spawned && questBoss.Map == pawn.Map)
            {
                _boss = questBoss;
            }
            else if (pawn.IsHashIntervalTick(BossSearchIntervalTicks))
            {
                _boss = ApocryphaGimmicks.FindBoss(pawn.Map);
            }
            else
            {
                _boss = null;
            }

            return _boss;
        }

        private void MaintainAura()
        {
            var auraDef = ApocryphaGimmickDefOf.VV_Effecter_FairyInterferenceAura;
            if (auraDef == null) { return; }

            if (_auraEffecter == null)
            {
                _auraEffecter = auraDef.SpawnAttached(pawn, pawn.Map);
            }

            _auraEffecter.EffectTick(new TargetInfo(pawn), TargetInfo.Invalid);
        }

        private void EndAura()
        {
            if (_auraEffecter == null) { return; }

            _auraEffecter.Cleanup();
            _auraEffecter = null;
        }

        public override void PostRemoved()
        {
            EndAura();
            base.PostRemoved();
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            EndAura();

            // 죽은 폰의 헤디프는 틱이 처리되지 않으므로 여기서 정리한다.
            // Pawn.Kill이 헤디프 목록을 정방향 순회하며 이 메서드를 부르므로, 즉시 제거하면 다음 헤디프가 건너뛰어진다.
            var deadPawn = pawn;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (deadPawn?.health != null && deadPawn.health.hediffSet.hediffs.Contains(this))
                {
                    deadPawn.health.RemoveHediff(this);
                }
            });
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (!DebugSettings.godMode || pawn == null || !pawn.Spawned) { yield break; }

            var boss = ApocryphaGimmicks.FindBoss(pawn.Map);
            var bossBound = ApocryphaGimmicks.IsBoundBoss(boss);

            var commandRelease = new Command_Action()
            {
                defaultLabel = "DEV: Release bond now",
                action = () =>
                {
                    var target = ApocryphaGimmicks.FindBoss(pawn.Map);
                    if (ApocryphaGimmicks.IsBoundBoss(target, out var targetComp) && pawn.health.hediffSet.hediffs.Contains(this))
                    {
                        TriggerManaBackflow(target, targetComp);
                    }
                },
            };
            if (!bossBound)
            {
                commandRelease.Disable("No bound Apocrypha on this map.");
            }
            yield return commandRelease;

            var commandTeleport = new Command_Action()
            {
                defaultLabel = "DEV: Teleport next to Apocrypha",
                action = () =>
                {
                    var target = ApocryphaGimmicks.FindBoss(pawn.Map);
                    if (target == null) { return; }

                    var map = target.Map;
                    if (CellFinder.TryFindRandomCellNear(target.Position, map, 2, c => c != target.Position && c.Standable(map) && c.InHorDistOf(target.Position, 2f), out var cell))
                    {
                        pawn.Position = cell;
                        pawn.Notify_Teleported();
                    }
                },
            };
            if (boss == null)
            {
                commandTeleport.Disable("No Apocrypha on this map.");
            }
            yield return commandTeleport;
        }
    }
}
