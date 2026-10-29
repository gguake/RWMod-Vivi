using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class EverflowerAttunementInfo : IExposable
    {
        public float attunement = 0f;
        public int attunementLevel = 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref attunement, "attunement");
            Scribe_Values.Look(ref attunementLevel, "attunementLevel");
        }
    }

    // 영원꽃 제단에 안치되었을 때의 그래픽. minLevel이 조율 단계 이하인 그래픽 중 minLevel이 가장 큰 것을 쓴다.
    public class EverflowerAltarGraphic
    {
        public int minLevel;
        public GraphicData graphicData;

        // 의식 완료 이펙트 생성 위치(셀 중심 기준, 꽃봉오리). ritualEffecterOffsetByLevel의 제단 버전.
        public Vector3 ritualEffecterOffset;
    }

    public class CompProperties_Everflower : CompProperties
    {
        public SimpleCurve everflowerResonateRateCurve;
        public SimpleCurve everflowerResonateLevelCurve;
        public SimpleCurve everflowerResonateRitualCurve;

        public SimpleCurve ritualCooldownCurve;

        public List<GraphicData> graphicsByLevel;
        public List<EffecterDef> effectsOnLevelAcquire;

        // 레벨별 의식 완료 이펙트 생성 위치(셀 중심 기준 오프셋). 각 단계 텍스처의 꽃봉오리 위치에 맞춰 세팅한다.
        // 비어 있으면 해당 레벨 그래픽의 drawOffset(= 텍스처 중심)을 사용한다.
        public List<Vector3> ritualEffecterOffsetByLevel;

        // 영원꽃 제단에 안치된 동안의 그래픽(줄기 밑동이 제단 중심에 오도록 맞춘 별도 텍스처).
        public List<EverflowerAltarGraphic> altarGraphics;

        public float teleportRange = 10.9f;

        public CompProperties_Everflower()
        {
            compClass = typeof(CompEverflower);
        }
    }

    [StaticConstructorOnStartup]
    public class CompEverflower : ThingComp
    {
        public static readonly Material AttunementLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(0.5f, 1f, 0.5f));

        // 조율 단계 상한(엔딩 6단계). 레벨 곡선은 4에서 멈추며 5/6단계는 SetAttunementLevel로만 오른다.
        public const int MaxAttunementLevel = 6;

        public CompProperties_Everflower Props => (CompProperties_Everflower)props;

        public ArcanePlant_Everflower Everflower => (ArcanePlant_Everflower)parent;

        public float Attunement => _attunementInfo.attunement;
        public int AttunementLevel => _attunementInfo.attunementLevel;

        // 지금 그려지는 그래픽. 제단에 안치되어 있으면 제단 그래픽, 아니면 레벨별 그래픽(목록을 넘는 레벨은 마지막 단계).
        public GraphicData CurrentGraphicData
        {
            get
            {
                var altarGraphic = CurrentAltarGraphic;
                if (altarGraphic?.graphicData != null)
                {
                    return altarGraphic.graphicData;
                }

                return GetLevelGraphic(_attunementInfo.attunementLevel);
            }
        }

        // 현재 그래픽을 그릴 때 셀 중심을 기준으로 적용하는 오프셋.
        // 이펙트를 thing 중심이 아니라 실제 꽃 그래픽 위치에서 재생할 때 사용한다. (DrawAt 참고)
        public Vector3 CurrentGraphicOffset => CurrentGraphicData.drawOffset;

        // 의식 완료 이펙트는 텍스처 중심(drawOffset)이 아니라 각 성장 단계 꽃봉오리 위치에서 터지도록 보정한다.
        public Vector3 RitualEffecterOffset
        {
            get
            {
                var altarGraphic = CurrentAltarGraphic;
                if (altarGraphic?.graphicData != null)
                {
                    return altarGraphic.ritualEffecterOffset;
                }

                var offsets = Props.ritualEffecterOffsetByLevel;
                if (offsets != null && _attunementInfo.attunementLevel < offsets.Count)
                {
                    return offsets[_attunementInfo.attunementLevel];
                }
                return CurrentGraphicOffset;
            }
        }

        // [계약] 제단 중앙 칸에 안치되어 있으면 그 제단, 아니면 null.
        // 캐시는 RefreshEnshrinement가 갱신하고, 여기서는 제단이 아직 같은 자리에 스폰되어 있는지만 확인한다.
        public Building_DreamumAltar EnshrinedAltar
        {
            get
            {
                var altar = _enshrinedAltar;
                if (altar == null || !parent.Spawned || !altar.Spawned || altar.Map != parent.Map || altar.Position != parent.Position)
                {
                    return null;
                }

                return altar;
            }
        }
        private Building_DreamumAltar _enshrinedAltar;

        public bool IsEnshrined => EnshrinedAltar != null;

        // 안치된 동안 쓰는 제단 그래픽(안치되지 않았거나 해당 단계의 제단 그래픽이 없으면 null).
        public EverflowerAltarGraphic CurrentAltarGraphic => IsEnshrined ? GetAltarGraphic(_attunementInfo.attunementLevel) : null;

        private EverflowerAttunementInfo _attunementInfo;

        public float AttunementPct
        {
            get
            {
                var nextLevelExp = Props.everflowerResonateLevelCurve.EvaluateInverted(_attunementInfo.attunementLevel + 1);
                var curLevelExp = Props.everflowerResonateLevelCurve.EvaluateInverted(_attunementInfo.attunementLevel);

                if (nextLevelExp == curLevelExp) { return 0; }
                return (_attunementInfo.attunement - curLevelExp) / (nextLevelExp - curLevelExp);
            }
        }

        public float AttunementHediffSeverity => _attunementInfo.attunementLevel + Mathf.Pow(AttunementPct, 2);

        public IEnumerable<Pawn> LinkedPawns => _linked;
        private List<Pawn> _linked = new List<Pawn>();

        public CompEverflower()
        {
            _attunementInfo = new EverflowerAttunementInfo();
        }

        public override void PostExposeData()
        {
            Scribe_Deep.Look(ref _attunementInfo, "attunementInfo");
            Scribe_Collections.Look(ref _linked, "linked", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _linked.RemoveAll(v => v == null);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);

            // 맵에서 빠지면(포장·파괴) 안치 캐시를 비워, 다시 제단에 놓일 때 새 안치로 판정되게 한다.
            _enshrinedAltar = null;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            // 엔딩 앵커의 파괴 통지는 ArcanePlant_Everflower.Destroy가 base.Destroy 전에(맵이 유효할 때) 보낸다.
            foreach (var pawn in LinkedPawns.ToList())
            {
                UnlinkAttunement(pawn);
            }
        }

        public override bool DontDrawParent()
        {
            return true;
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var graphic = CurrentGraphicData;

            var mesh = graphic.Graphic.MeshAt(parent.Rotation);
            var drawPos = parent.DrawPos;
            Graphics.DrawMesh(
                mesh, 
                drawPos + graphic.drawOffset, 
                Quaternion.identity, 
                graphic.Graphic.MatAt(parent.Rotation), 0);
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            if (_linked.Count > 0)
            {
                var amount = Mathf.Max(1f, dinfo.Amount / _linked.Count);
                dinfo.SetAmount(amount);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (DebugSettings.godMode)
            {
                if (_attunementInfo.attunementLevel >= 1)
                {
                    yield return new Command_Action()
                    {
                        defaultLabel = "DEV: +10000 Attunement",
                        action = () =>
                        {
                            GainAttunement(10000);
                        }
                    };
                }

                // 레벨 곡선은 4단계에서 멈추므로 자동 테스트·개발용으로 4~6단계를 직접 지정한다.
                for (int i = 4; i <= MaxAttunementLevel; ++i)
                {
                    var targetLevel = i;
                    yield return new Command_Action()
                    {
                        defaultLabel = $"DEV: Set attunement level {targetLevel}",
                        action = () =>
                        {
                            SetAttunementLevel(targetLevel);
                        }
                    };
                }
            }

            // 엔딩 퀘스트의 개화 응축 진행 막대와 DEV 단계 조작 기즈모
            var endingGizmos = ViviEndingQuest.Current?.GetEverflowerGizmos(Everflower);
            if (endingGizmos != null)
            {
                foreach (var gizmo in endingGizmos)
                {
                    yield return gizmo;
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            var sb = new StringBuilder();

            var level = _attunementInfo.attunementLevel;
            if (level > 0)
            {
                if (level > 4)
                {
                    // 5/6단계는 레벨 곡선 밖이라 경험치 비율이 의미 없으므로 단계만 표시한다.
                    sb.AppendInNewLine(LocalizeString_EndingAltar.VV_Inspector_EverflowerAttunementLevelOnly.Translate(level.Named("LEVEL")));
                }
                else
                {
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_EverflowerAttunementState.Translate(
                        _attunementInfo.attunementLevel.Named("LEVEL"),
                        AttunementPct.ToStringPercent().Named("EXP")));
                }
            }

            if (IsEnshrined)
            {
                sb.AppendInNewLine(LocalizeString_EndingAltar.VV_Inspector_EverflowerEnshrined.Translate());
            }

            var endingText = ViviEndingQuest.Current?.GetEverflowerInspectString(Everflower);
            if (!endingText.NullOrEmpty())
            {
                sb.AppendInNewLine(endingText);
            }

            return sb.ToString();
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            foreach (var pawn in LinkedPawns)
            {
                var a = parent.TrueCenter();
                var b = pawn.TrueCenter();
                GenDraw.DrawLineBetween(a, b, AttunementLineMat);
            }
        }

        public void LinkAttunement(Pawn pawn, float quality)
        {
            if (!_linked.Contains(pawn))
            {
                _linked.Add(pawn);

                if (_attunementInfo.attunementLevel == 0)
                {
                    ChangeAttunementLevel(1);
                }

                pawn.GetCompVivi()?.Notify_LinkEverflower(Everflower);

                if (pawn.TryGetComp<CompViviHolder>(out var compViviHolder))
                {
                    compViviHolder.Notify_EverflowerLinked();
                }

                Messages.Message(LocalizeString_Message.VV_Message_LinkEverflowerComplete.Translate(pawn.Named("PAWN")), MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message(LocalizeString_Message.VV_Message_AttunementEverflowerComplete.Translate(pawn.Named("PAWN")), MessageTypeDefOf.NeutralEvent);
            }

            GainAttunement(Props.everflowerResonateRitualCurve.Evaluate(quality));

            var count = 0;
            var v = Rand.Value;
            if ( v <= 0.5f)
            {
                count += GrowViviFlowerRandomly();
                if (Rand.Chance(0.4f))
                {
                    count += GrowArcanePlantRandomly();
                }

                if (count > 0)
                {
                    Messages.Message(LocalizeString_Message.VV_Message_PlantGrownAfterAttunement.Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
                }
            }
            else if (v < 0.7f)
            {
                foreach (var cell in GenRadial.RadialCellsAround(parent.Position, 4f, false))
                {
                    if (!cell.InBounds(parent.Map)) { continue; }

                    if (FilthMaker.CanMakeFilth(cell, parent.Map, VVThingDefOf.VV_FilthPollen))
                    {
                        if (FilthMaker.TryMakeFilth(cell, parent.Map, VVThingDefOf.VV_FilthPollen))
                        {
                            count++;
                        }
                    }
                }

                if (count > 0)
                {
                    Messages.Message(LocalizeString_Message.VV_Message_PollenWaveAfterAttunement.Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
                }
            }
        }

        public void UnlinkAttunement(Pawn pawn)
        {
            UnlinkAttunement(pawn, true);
        }

        public void UnlinkAttunement(Pawn pawn, bool showMessages)
        {
            if (pawn == null) { return; }

            var removed = _linked.Remove(pawn);
            var compVivi = pawn.GetComp<CompVivi>();
            var shouldNotifyPawn = compVivi != null && compVivi.LinkedEverflower == Everflower;

            if (!removed && !shouldNotifyPawn) { return; }

            if (shouldNotifyPawn)
            {
                compVivi.Notify_UnlinkEverflower(showMessages);

                if (pawn.TryGetComp<CompViviHolder>(out var compViviHolder))
                {
                    compViviHolder.Notify_EverflowerUnlinked();
                }
            }
        }

        public void GainAttunement(float attunement)
        {
            if (attunement == 0) { return; }

            var nextLevelExp = Props.everflowerResonateLevelCurve.EvaluateInverted(_attunementInfo.attunementLevel + 1);
            var curLevelExp = Props.everflowerResonateLevelCurve.EvaluateInverted(_attunementInfo.attunementLevel);
            if (nextLevelExp == curLevelExp) { return; }

            _attunementInfo.attunement += attunement;

            var newLevel = (int)Props.everflowerResonateLevelCurve.Evaluate(_attunementInfo.attunement);
            if (newLevel > _attunementInfo.attunementLevel)
            {
                ChangeAttunementLevel(newLevel);
            }

            var hediffSeverity = AttunementHediffSeverity;
            foreach (var pawn in LinkedPawns)
            {
                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerLink);
                if (hediff != null)
                {
                    hediff.Severity = hediffSeverity;
                }
            }
        }

        public void GainAttunementFromMana(float mana)
        {
            GainAttunement(Props.everflowerResonateRateCurve.Evaluate(mana));

        }

        // [계약] 엔딩 퀘스트용 강제 레벨 변경(5: 대조율 완료, 6: 아포크리파 토벌). 레벨 곡선은 4에서 멈추므로 5/6은 이 경로로만 오른다.
        public void SetAttunementLevel(int level)
        {
            ChangeAttunementLevel(Mathf.Clamp(level, 0, MaxAttunementLevel));
        }

        private void ChangeAttunementLevel(int level)
        {
            var previousLevel = _attunementInfo.attunementLevel;
            if (previousLevel == level) { return; }
            _attunementInfo.attunementLevel = level;

            // 레벨이 내려가는 경우(개발자 기즈모)에는 레벨업 편지와 성장 이펙트를 생략한다.
            var levelUp = level > previousLevel;
            if (levelUp && level > 1)
            {
                SendLevelUpLetter(level);
            }

            if (levelUp && parent.Spawned)
            {
                SpawnLevelEffect(parent.Position, parent.Map, IsEnshrined);
            }

            foreach (var pawn in LinkedPawns)
            {
                pawn.GetCompVivi()?.RefreshEverflowerHediffs();
            }

            // 4단계 도달(연방 서신 예약)과 5/6단계 진행은 엔딩 퀘스트가 판단한다.
            ViviEndingQuest.Current?.Notify_EverflowerLevelChanged(Everflower, level);
            RefreshAltarMana(parent.MapHeld);
        }

        private void SendLevelUpLetter(int level)
        {
            // 제단이 필요한 의식(대조율)은 4단계 도달 시점엔 아직 잠겨 있으므로 목록에서 빼고, 제단 해금 서신·안치 메시지로 따로 알린다.
            var rituals = new List<string>();
            foreach (var ritualDef in DefDatabase<PreceptDef>.AllDefsListForReading)
            {
                var obligation = ritualDef.ritualPatternBase?.ritualObligationTargetFilter;
                if (obligation != null && obligation is RitualObligationTargetFilterDef_EverflowerRitual everflowerRitualObligation
                    && everflowerRitualObligation.requiredAttunementLevel == level && !everflowerRitualObligation.requiresAltar)
                {
                    rituals.Add($"- {ritualDef.LabelCap}");
                }
            }

            var sb = new StringBuilder();

            // 5·6단계는 레벨 곡선 밖에서만 오르므로 전용 머리말을 앞에 붙이고, 새로 열리는 의식이 있을 때만 그 목록을 덧붙인다.
            string preamble = null;
            if (level == 5)
            {
                preamble = LocalizeString_EndingAltar.VV_Letter_EverflowerAttunementLevel5Preamble.Translate();
            }
            else if (level >= 6)
            {
                preamble = LocalizeString_EndingAltar.VV_Letter_EverflowerAttunementLevel6Preamble.Translate();
            }

            if (preamble != null)
            {
                sb.Append(preamble);
                if (rituals.Count > 0)
                {
                    sb.Append("\n\n");
                }
            }

            if (preamble == null || rituals.Count > 0)
            {
                sb.Append(LocalizeString_Letter.VV_Letter_EverflowerAttumentLevelUp.Translate(level.Named("LEVEL")));
                sb.Append("\n\n");

                foreach (var ritual in rituals)
                {
                    sb.AppendInNewLine(ritual);
                }
            }

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_EverflowerAttumentLevelUpLabel.Translate(level.Named("LEVEL")),
                sb.ToString(),
                LetterDefOf.PositiveEvent,
                parent);
        }

        // 제단 안치 상태를 다시 계산한다. 순간이동 직후, 영원꽃·제단 스폰(재설치·완공·불러오기) 시 호출된다.
        // 새로 안치되면 이펙트·메시지를 띄우고 엔딩 퀘스트에 알린다. silent가 true이면(불러오기) 상태만 갱신한다.
        internal void RefreshEnshrinement(bool silent = false)
        {
            var previousAltar = _enshrinedAltar;
            _enshrinedAltar = FindAltarAtCenter();

            var altar = EnshrinedAltar;
            if (altar != null)
            {
                var map = parent.Map;

                // 중앙 칸의 edifice는 영원꽃으로 맞춘다. (제단을 나중에 지었거나 불러오기에서 제단이 늦게 스폰되면 제단이 덮어쓴다)
                if (map.edificeGrid[parent.Position] != parent)
                {
                    map.edificeGrid.Register(Everflower);
                }

                // 안치된 영원꽃은 제단에서 내리지 않으므로 걸려 있던 순간이동 예약을 지운다.
                Everflower.Unreserve();

                if (altar != previousAltar && !silent)
                {
                    AltarDefOf.VV_Effecter_AltarEnshrine?.Spawn(parent.Position, map).Cleanup();
                    Messages.Message(LocalizeString_EndingAltar.VV_Message_EverflowerEnshrined.Translate(), parent, MessageTypeDefOf.PositiveEvent);
                }
            }

            RefreshAltarMana(parent.MapHeld);
        }

        // 성장(레벨) 이펙트를 꽃 그래픽 위치에 맞춰 재생한다.
        // 1~4단계 이펙트는 제단 밖 그래픽, 5/6단계 이펙트는 제단 위 그래픽 기준 오프셋으로 만들어져 있다.
        internal void SpawnLevelEffect(IntVec3 cell, Map map, bool onAltar)
        {
            var level = _attunementInfo.attunementLevel;
            var effects = Props.effectsOnLevelAcquire;
            if (map == null || effects == null || level < 1 || level - 1 >= effects.Count) { return; }

            var effecterDef = effects[level - 1];
            if (effecterDef == null) { return; }

            if (level >= 5)
            {
                // 제단 밖이면 제단 그래픽과 제단 밖 그래픽의 오프셋 차이(줄기 밑동 보정)만큼 옮겨서 재생한다.
                effecterDef.Spawn(cell, map, onAltar ? Vector3.zero : GetOffAltarEffectShift(level)).Cleanup();
            }
            else if (onAltar)
            {
                // 1~4단계 이펙트의 꽃 모양 발광은 제단 밖 그래픽을 기준으로 하므로, 제단 위에서는 위치가 어긋나 생략한다.
                return;
            }
            else if (parent.Spawned && cell == parent.Position)
            {
                effecterDef.SpawnAttached(parent, map);
            }
            else
            {
                effecterDef.Spawn(cell, map);
            }
        }

        private Building_DreamumAltar FindAltarAtCenter()
        {
            if (!parent.Spawned) { return null; }

            var altar = parent.Map.GetComponent<ArcanePlantMapComponent>()?.GetArcanePlantPot(parent.Position) as Building_DreamumAltar;
            if (altar == null || altar.Position != parent.Position) { return null; }

            return altar;
        }

        private GraphicData GetLevelGraphic(int level)
        {
            var graphics = Props.graphicsByLevel;
            return graphics[Mathf.Clamp(level, 0, graphics.Count - 1)];
        }

        private EverflowerAltarGraphic GetAltarGraphic(int level)
        {
            var altarGraphics = Props.altarGraphics;
            if (altarGraphics == null) { return null; }

            EverflowerAltarGraphic result = null;
            for (int i = 0; i < altarGraphics.Count; ++i)
            {
                var altarGraphic = altarGraphics[i];
                if (altarGraphic.minLevel <= level && (result == null || altarGraphic.minLevel > result.minLevel))
                {
                    result = altarGraphic;
                }
            }

            return result;
        }

        private Vector3 GetOffAltarEffectShift(int level)
        {
            var altarGraphic = GetAltarGraphic(level);
            if (altarGraphic?.graphicData == null) { return Vector3.zero; }

            var shift = GetLevelGraphic(level).drawOffset - altarGraphic.graphicData.drawOffset;
            shift.y = 0f;
            return shift;
        }

        // 5/6단계·안치 상태가 바뀌면 맵 마나(제단 발판 방출, 6단계 전역 바닥값)를 다시 계산하게 한다.
        private static void RefreshAltarMana(Map map)
        {
            map?.GetManaComponent()?.RefreshEverflowerAltarState();
        }

        private int GrowArcanePlantRandomly()
        {
            var spawnedCount = 0;
            var nearCells = GenRadial.RadialCellsAround(parent.Position, 5f, false)
                .Where(c => ArcanePlantUtility.CanPlaceArcanePlantToCell(parent.Map, c, VVThingDefOf.VV_ArcanePlantSeedling)).ToList();

            var count = Mathf.Min(nearCells.Count(), Rand.Range(2, 5));
            for (int i = 0; i < count; ++i)
            {
                var cell = nearCells.RandomElement();
                nearCells.Remove(cell);

                if (GenSpawn.TrySpawn(VVThingDefOf.VV_ArcanePlantSeedling, cell, parent.Map, out var thing, canWipeEdifices: false))
                {
                    thing.SetFaction(parent.Faction ?? Faction.OfPlayerSilentFail);

                    var seedling = thing as ArcanePlant_Seedling;
                    if (seedling != null)
                    {
                        seedling.Growth = Rand.Range(0.05f, 0.6f);
                    }

                    spawnedCount++;
                }
            }

            return spawnedCount;
        }

        private int GrowViviFlowerRandomly()
        {
            var spawnedCount = 0;
            var nearCells = GenRadial.RadialCellsAround(parent.Position, 9f, false)
                .Where(c => c.InBounds(parent.Map))
                .ToList();

            var count = Mathf.Min(nearCells.Count(), Rand.Range(6, 15));
            while (nearCells.Count > 0)
            {
                var cell = nearCells.RandomElement();
                nearCells.Remove(cell);

                if (ArcanePlantUtility.TrySpawnViviFlower(parent.Map, cell, out var flower))
                {
                    spawnedCount++;
                }

                if (spawnedCount >= count) { break; }
            }

            return spawnedCount;
        }
    }
}
