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

    public class EverflowerAltarGraphic
    {
        public int minLevel;
        public GraphicData graphicData;

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

        // 단계별 꽃봉오리 위치에 맞춘 의식 완료 이펙트 오프셋(셀 중심 기준).
        // 값이 없으면 해당 그래픽의 drawOffset, 즉 텍스처 중심을 사용한다.
        public List<Vector3> ritualEffecterOffsetByLevel;

        // 제단 안치용 별도 텍스처. 줄기 밑동이 제단 중심에 오도록 맞춘다.
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

        // 조율 상한은 6단계다. 레벨 곡선은 4에서 끝나므로 5·6단계는 SetAttunementLevel로만 올린다.
        public const int MaxAttunementLevel = 6;

        private const float BehindAssimilatedPawnAltitude = 0.1f;

        public CompProperties_Everflower Props => (CompProperties_Everflower)props;

        public ArcanePlant_Everflower Everflower => (ArcanePlant_Everflower)parent;

        public float Attunement => _attunementInfo.attunement;
        public int AttunementLevel => _attunementInfo.attunementLevel;

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

        public Vector3 CurrentGraphicOffset => CurrentGraphicData.drawOffset;

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

        public Building_EverflowerAltar EnshrinedAltar
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
        private Building_EverflowerAltar _enshrinedAltar;

        public bool IsEnshrined => EnshrinedAltar != null;

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

            // 재안치를 새 안치로 판정하도록 포장·파괴로 맵을 떠날 때 안치 캐시를 비운다.
            _enshrinedAltar = null;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            // 앵커 파괴는 바닐라가 퀘스트 태그의 <태그>.Destroyed 신호로 알리므로 별도 통지를 보내지 않는다.
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
            var drawPos = parent.DrawPos + graphic.drawOffset;

            if (Hediff_EverflowerAssimilation.IsFloatingBefore(Everflower))
            {
                drawPos.y = AltitudeLayer.Pawn.AltitudeFor() - BehindAssimilatedPawnAltitude;
            }

            Graphics.DrawMesh(
                mesh,
                drawPos,
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
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_EverflowerAttunementLevelOnly.Translate(level.Named("LEVEL")));
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
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_EverflowerEnshrined.Translate());
            }

            var skyShield = parent.Spawned ? ViviShieldDome.FindFor(parent.Map, VVThingDefOf.VV_SkyShield, parent) : null;
            if (skyShield != null)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_SkyShieldRemaining.Translate(skyShield.TicksLeft.ToStringTicksToPeriod()));
            }

            var endingText = ViviEndingQuest.Current?.GetEverflowerInspectString(Everflower);
            if (!endingText.NullOrEmpty())
            {
                sb.AppendInNewLine(endingText);
            }

            var rebirthText = Everflower.PawnHolder.GetInspectString();
            if (!rebirthText.NullOrEmpty())
            {
                sb.AppendInNewLine(rebirthText);
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

        public void SetAttunementLevel(int level)
        {
            ChangeAttunementLevel(Mathf.Clamp(level, 0, MaxAttunementLevel));
        }

        private void ChangeAttunementLevel(int level)
        {
            var previousLevel = _attunementInfo.attunementLevel;
            if (previousLevel == level) { return; }
            _attunementInfo.attunementLevel = level;

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
        }

        private void SendLevelUpLetter(int level)
        {
            var rituals = new List<string>();
            foreach (var ritualDef in DefDatabase<PreceptDef>.AllDefsListForReading)
            {
                var obligation = ritualDef.ritualPatternBase?.ritualObligationTargetFilter;
                if (obligation != null && obligation is RitualObligationTargetFilterDef_EverflowerRitual everflowerRitualObligation
                    && everflowerRitualObligation.requiredAttunementLevel == level && !everflowerRitualObligation.requiresAltar
                    && !everflowerRitualObligation.completesEndingQuest)
                {
                    rituals.Add($"- {ritualDef.LabelCap}");
                }
            }

            var sb = new StringBuilder();

            string preamble = null;
            if (level == 5)
            {
                preamble = LocalizeString_Letter.VV_Letter_EverflowerAttunementLevel5Preamble.Translate();
            }
            else if (level >= 6)
            {
                preamble = LocalizeString_Letter.VV_Letter_EverflowerAttunementLevel6Preamble.Translate();
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

        internal void RefreshEnshrinement(bool silent = false)
        {
            var previousAltar = _enshrinedAltar;
            _enshrinedAltar = FindAltarAtCenter();

            var altar = EnshrinedAltar;
            if (altar != null)
            {
                var map = parent.Map;

                // 제단의 늦은 스폰이나 후속 건설이 edifice를 덮어쓸 수 있으므로 중앙 칸을 영원꽃으로 복구한다.
                if (map.edificeGrid[parent.Position] != parent)
                {
                    map.edificeGrid.Register(Everflower);
                }

                Everflower.Unreserve();

                if (altar != previousAltar && !silent)
                {
                    VVEffecterDefOf.VV_Effecter_AltarEnshrine?.Spawn(parent.Position, map).Cleanup();
                    Messages.Message(LocalizeString_Message.VV_Message_EverflowerEnshrined.Translate(), parent, MessageTypeDefOf.PositiveEvent);
                }
            }
        }

        // 이펙트 오프셋은 1~4단계는 제단 밖 그래픽, 5·6단계는 제단 위 그래픽을 기준으로 한다.
        internal void SpawnLevelEffect(IntVec3 cell, Map map, bool onAltar)
        {
            var level = _attunementInfo.attunementLevel;
            var effects = Props.effectsOnLevelAcquire;
            if (map == null || effects == null || level < 1 || level - 1 >= effects.Count) { return; }

            var effecterDef = effects[level - 1];
            if (effecterDef == null) { return; }

            if (level >= 5)
            {
                effecterDef.Spawn(cell, map, onAltar ? Vector3.zero : GetOffAltarEffectShift(level)).Cleanup();
            }
            else if (onAltar)
            {
                // 1~4단계의 꽃 모양 발광은 제단 밖 그래픽 기준이라 제단 위에서는 위치가 맞지 않아 생략한다.
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

        private Building_EverflowerAltar FindAltarAtCenter()
        {
            if (!parent.Spawned) { return null; }

            var altar = parent.Map.GetComponent<ArcanePlantMapComponent>()?.GetArcanePlantPot(parent.Position) as Building_EverflowerAltar;
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
