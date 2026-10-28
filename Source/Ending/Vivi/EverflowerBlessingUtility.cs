using RimWorld;
using Verse;

namespace VVRace
{
    // 엔딩 이후 비비 쪽 영구 보상.
    // - 5단계 이상(공성 이후) 앵커 영원꽃이 있는 맵: 성인 일반 비비의 영원꽃 잔향 상시 유지
    // - 6단계(엔딩 완료) 앵커 영원꽃이 있는 맵: 플레이어 비비에게 〈영원꽃의 축복〉(유전자 불안정 면역, 성체 노화 감속)
    // CompVivi의 주기 틱에서 RefreshFor를 호출해 유지하며, 맵을 떠나면 축복은 하루 뒤 사라진다.
    public static class EverflowerBlessingUtility
    {
        public const int RefreshIntervalTicks = 2000;

        private const int ReverberationAttunementLevel = 5;
        private const int BlessingAttunementLevel = 6;

        public static bool IsBlessed(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) { return false; }

            if (pawn.health.hediffSet.HasHediff(EndingRitualDefOf.VV_EverflowerBlessing)) { return true; }

            // 아이 비비는 성장이 멈추지 않도록 노화 감속 헤디프를 받지 않지만, 축복받은 땅에 있는 동안에는 유전자 불안정에 면역이다.
            return pawn.Faction != null && pawn.Faction.IsPlayer && pawn.IsVivi() && IsBlessedMap(pawn.MapHeld);
        }

        // 엔딩을 완료한 6단계 앵커 영원꽃이 있는 맵인지.
        public static bool IsBlessedMap(Map map)
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null || !quest.IsCompleted) { return false; }

            var anchor = GetSpawnedAnchor(quest, map);
            return anchor != null && anchor.AttunementLevel >= BlessingAttunementLevel;
        }

        public static void RefreshFor(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.health == null || !pawn.IsVivi()) { return; }

            var quest = ViviEndingQuest.Current;
            if (quest == null) { return; }

            var anchor = GetSpawnedAnchor(quest, pawn.Map);
            if (anchor == null) { return; }

            var compEverflower = anchor.EverflowerComp;
            var level = compEverflower.AttunementLevel;
            if (level < ReverberationAttunementLevel) { return; }

            if (!pawn.IsRoyalVivi() && pawn.ageTracker.Adult)
            {
                MaintainReverberation(pawn, compEverflower.AttunementHediffSeverity);
            }

            if (quest.IsCompleted && level >= BlessingAttunementLevel && pawn.Faction != null && pawn.Faction.IsPlayer)
            {
                if (pawn.ageTracker.Adult)
                {
                    GiveOrRefreshBlessing(pawn);
                }

                RemoveGeneticUnstability(pawn);
                UpgradeLegacyFairyBond(pawn, anchor);
            }
        }

        private static ArcanePlant_Everflower GetSpawnedAnchor(ViviEndingQuest quest, Map map)
        {
            var anchor = quest.AnchorEverflower;
            if (map == null || anchor == null || !anchor.Spawned || anchor.Map != map) { return null; }

            return anchor;
        }

        // 잔향의 노화 배율(0.5)은 고정이고 수치(=지속 시간)만 영원꽃 조율 수치 이상으로 끌어올린다.
        private static void MaintainReverberation(Pawn pawn, float severity)
        {
            if (severity <= 0f) { return; }

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerReverberation)
                ?? pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerReverberation);

            if (hediff != null && hediff.Severity < severity)
            {
                hediff.Severity = severity;
            }
        }

        private static void GiveOrRefreshBlessing(Pawn pawn)
        {
            var blessing = pawn.health.hediffSet.GetFirstHediffOfDef(EndingRitualDefOf.VV_EverflowerBlessing);
            if (blessing == null)
            {
                pawn.health.AddHediff(EndingRitualDefOf.VV_EverflowerBlessing);
                return;
            }

            blessing.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
        }

        private static void RemoveGeneticUnstability(Pawn pawn)
        {
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(EndingRitualDefOf.VV_GeneticUnstability);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        // 구 세이브에서 요정 비비 결속 헤디프를 불러오면 예전 클래스의 인스턴스로 생성되어 6단계 페널티 제거가 적용되지 않으므로 새로 부여한다.
        private static void UpgradeLegacyFairyBond(Pawn pawn, ArcanePlant_Everflower anchor)
        {
            if (pawn.GetCompVivi()?.LinkedEverflower != anchor) { return; }

            var bond = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_ViviFairyFollow);
            if (bond != null && bond.GetType() != bond.def.hediffClass)
            {
                pawn.GetComp<CompViviHolder>()?.RefreshBondHediff();
            }
        }
    }
}
