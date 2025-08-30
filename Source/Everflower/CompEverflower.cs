using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
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

    public class CompProperties_Everflower : CompProperties
    {
        public SimpleCurve everflowerResonateRateCurve;
        public SimpleCurve everflowerResonateLevelCurve;
        public SimpleCurve everflowerResonateRitualCurve;

        public SimpleCurve ritualCooldownCurve;

        public List<GraphicData> graphicsByLevel;
        public List<EffecterDef> effectsOnLevelAcquire;

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

        public CompProperties_Everflower Props => (CompProperties_Everflower)props;

        public ArcanePlant_Everflower Everflower => (ArcanePlant_Everflower)parent;

        public float Attunement => _attunementInfo.attunement;
        public int AttunementLevel => _attunementInfo.attunementLevel;

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

        public float AttunementHediffSeverity => _attunementInfo.attunementLevel + AttunementPct;

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

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            foreach (var pawn in LinkedPawns)
            {
                pawn.GetCompVivi().Notify_LinkedEverflowerDestroyed();
            }
        }

        public override bool DontDrawParent()
        {
            return true;
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var graphic = Props.graphicsByLevel[_attunementInfo.attunementLevel];

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
            }
        }

        public override string CompInspectStringExtra()
        {
            if (AttunementLevel > 0)
            {
                var sb = new StringBuilder();
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_EverflowerAttunementState.Translate(
                    _attunementInfo.attunementLevel.Named("LEVEL"),
                    AttunementPct.ToStringPercent().Named("EXP")));

                return sb.ToString();
            }

            return string.Empty;
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
            if (!_linked.Contains(pawn)) { return; }
            _linked.Remove(pawn);
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

        private void ChangeAttunementLevel(int level)
        {
            if (_attunementInfo.attunementLevel == level) { return; }
            _attunementInfo.attunementLevel = level;

            if (level > 1)
            {
                var sb = new StringBuilder();
                sb.Append(LocalizeString_Letter.VV_Letter_EverflowerAttumentLevelUp.Translate(level.Named("LEVEL")));
                sb.Append("\n\n");

                foreach (var ritualDef in DefDatabase<PreceptDef>.AllDefsListForReading)
                {
                    var obligation = ritualDef.ritualPatternBase?.ritualObligationTargetFilter;
                    if (obligation != null && obligation is RitualObligationTargetFilterDef_EverflowerRitual everflowerRitualObligation && everflowerRitualObligation.requiredAttunementLevel == level)
                    {
                        sb.AppendInNewLine($"- {ritualDef.LabelCap}");
                    }
                }

                Find.LetterStack.ReceiveLetter(
                    LocalizeString_Letter.VV_Letter_EverflowerAttumentLevelUpLabel.Translate(level.Named("LEVEL")),
                    sb.ToString(),
                    LetterDefOf.PositiveEvent);
            }

            if (parent.Spawned)
            {
                if (level - 1 < Props.effectsOnLevelAcquire.Count)
                {
                    var effect = Props.effectsOnLevelAcquire[level - 1];
                    effect?.SpawnAttached(parent, parent.Map);
                }
            }
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
                        seedling.Growth = Random.Range(0.05f, 0.6f);
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
