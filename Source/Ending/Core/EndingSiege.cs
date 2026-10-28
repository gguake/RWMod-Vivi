using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    // 【1막】 〈여명의 맥동〉: 개화 응축 0→99%(8~10일), 비비 연방 지원군.
    // 습격은 직접 일으키지 않는다. 바닐라 우주선 반응로 가동처럼 앵커 맵에 습격 비콘 태그(Map_RaidBeacon)를 붙여
    // 스토리텔러가 큰 위협(ThreatBig)으로 습격을 보내게 한다(EndingCorePatch.Map_IncidentTargetTags_Postfix). 아포크리파를 토벌하면 해제된다.
    public partial class ViviEndingQuest
    {
        private const int SiegeMaxDurationTicks = 600000;   // 의식 품질 0 → 10일
        private const int SiegeMinDurationTicks = 480000;   // 의식 최대 품질 → 8일
        private const float RitualMaxQuality = 10f;
        private static readonly IntRange ReinforceIntervalTicks = new IntRange(90000, 150000);
        private static readonly IntRange ReinforceCountRange = new IntRange(3, 6);

        private void StartSiege(ArcanePlant_Everflower everflower, float quality)
        {
            var now = Find.TickManager.TicksGame;

            _anchor = everflower;
            if (everflower.Spawned)
            {
                _lastAnchorMap = everflower.Map;
            }

            _stage = ViviEndingStage.Siege;
            _bloomProgress = 0f;
            _boss = null;
            _bossDeferred = false;
            _bossDeferredMessageSent = false;
            _bossMissingSinceTick = -1;
            _calmEndTick = -1;
            _siegeStartTick = now;
            _siegeDurationTicks = Mathf.RoundToInt(Mathf.Lerp(SiegeMaxDurationTicks, SiegeMinDurationTicks, Mathf.Clamp01(quality / RitualMaxQuality)));
            _nextReinforceTick = now + ReinforceIntervalTicks.RandomInRange;

            var days = (_siegeDurationTicks / (float)GenDate.TicksPerDay).ToString("0.#");
            var text = LocalizeString_EndingCore.VV_Letter_EndingSiege.Translate(days.Named("DAYS")).Resolve();
            if (FindFederation(requireNonHostile: true) != null)
            {
                text += "\n\n" + LocalizeString_EndingCore.VV_Letter_EndingSiegeFederation.Translate().Resolve();
            }

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingCore.VV_Letter_EndingSiegeLabel.Translate(),
                text,
                LetterDefOf.ThreatBig,
                new LookTargets(everflower));
        }

        // 250틱마다: 응축 진행(앵커가 스폰되어 있을 때만), 99% 도달 시 평화, 지원군 타이머.
        private void TickSiege(int now)
        {
            if (!CheckAnchorAlive()) { return; }

            if (_siegeDurationTicks <= 0)
            {
                _siegeDurationTicks = SiegeMaxDurationTicks;
            }

            if (_anchor.Spawned)
            {
                _bloomProgress = Mathf.Min(BloomLockedProgress, _bloomProgress + BloomLockedProgress * StageTickInterval / _siegeDurationTicks);
                if (_bloomProgress >= BloomLockedProgress)
                {
                    StartCalm();
                    return;
                }
            }

            var map = AnchorMap;
            if (map == null) { return; }

            if (now >= _nextReinforceTick)
            {
                _nextReinforceTick = now + ReinforceIntervalTicks.RandomInRange;
                TrySendFederationReinforcements(map);
            }
        }

        // 패망하지 않은 비적대 비비 연방이 있으면 경비원·소방수·저격수 3~6명을 스카이위드 상자로 제단 근처에 보내 한동안 함께 싸우게 한다.
        private void TrySendFederationReinforcements(Map map)
        {
            var federation = FindFederation(requireNonHostile: true);
            if (federation == null) { return; }

            var kinds = new List<PawnKindDef>(3);
            if (EndingCoreDefOf.VV_ViviGuard != null)
            {
                kinds.Add(EndingCoreDefOf.VV_ViviGuard);
            }
            if (EndingCoreDefOf.VV_ViviFirefighter != null)
            {
                kinds.Add(EndingCoreDefOf.VV_ViviFirefighter);
            }
            if (EndingCoreDefOf.VV_ViviSniper != null)
            {
                kinds.Add(EndingCoreDefOf.VV_ViviSniper);
            }
            if (kinds.Count == 0) { return; }

            var altar = AnchorAltar;
            var center = altar != null && altar.Spawned && altar.Map == map ? altar.Position : _anchor.Position;

            var count = ReinforceCountRange.RandomInRange;
            var pawns = new List<Pawn>(count);
            try
            {
                for (int i = 0; i < count; ++i)
                {
                    var request = new PawnGenerationRequest(kinds.RandomElement(), federation, PawnGenerationContext.NonPlayer, map.Tile, mustBeCapableOfViolence: true);
                    pawns.Add(PawnGenerator.GeneratePawn(request));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] failed to generate federation reinforcements: {ex}");
            }

            if (pawns.Count == 0) { return; }

            DropPodUtility.DropThingsNear(center, map, pawns.Cast<Thing>(), canRoofPunch: false, forbid: false, allowFogged: false, faction: federation);
            LordMaker.MakeNewLord(federation, new LordJob_AssistColony(federation, center), map, pawns);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingCore.VV_Letter_EndingFederationAidLabel.Translate(),
                LocalizeString_EndingCore.VV_Letter_EndingFederationAid.Translate(pawns.Count.Named("COUNT")),
                LetterDefOf.PositiveEvent,
                new LookTargets(pawns),
                federation);
        }
    }
}
