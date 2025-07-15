using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class EverflowerAttunementAbilityInfo
    {
        public AbilityDef abilityDef;
        public int level;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "abilityDef", xmlRoot.Name);
            level = int.Parse(xmlRoot.InnerText);
        }
    }

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
        public SimpleCurve everflowerAttuneRateCurve;
        public SimpleCurve everflowerAttuneLevelCurve;

        public List<GraphicData> graphicsByLevel;
        public List<EffecterDef> effectsOnLevelAcquire;

        public List<EverflowerAttunementAbilityInfo> abilities;

        public CompProperties_Everflower()
        {
            compClass = typeof(CompEverflower);
        }
    }

    [StaticConstructorOnStartup]
    public class CompEverflower : ThingComp, IThingHolder
    {
        public static Material AttunementLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(0.5f, 1f, 0.5f));

        public CompProperties_Everflower Props => (CompProperties_Everflower)props;

        public ArcanePlant_Everflower Everflower => (ArcanePlant_Everflower)parent;

        public float Attunement => _attunementInfo.attunement;
        public int AttunementLevel => _attunementInfo.attunementLevel;

        private EverflowerAttunementInfo _attunementInfo;

        public float AttunementPct
        {
            get
            {
                var nextLevelExp = Props.everflowerAttuneLevelCurve.EvaluateInverted(_attunementInfo.attunementLevel + 1);
                var curLevelExp = Props.everflowerAttuneLevelCurve.EvaluateInverted(_attunementInfo.attunementLevel);

                if (nextLevelExp == curLevelExp) { return 0; }
                return (_attunementInfo.attunement - curLevelExp) / (nextLevelExp - curLevelExp);
            }
        }

        public float AttunementHediffSeverity => _attunementInfo.attunementLevel + AttunementPct;

        public IEnumerable<Pawn> LinkedPawns => _linked;
        private List<Pawn> _linked = new List<Pawn>();

        public IEnumerable<Pawn> InnerFairyPawns => _innerContainer.Where(v => v is Pawn).Cast<Pawn>();
        private ThingOwner _innerContainer;

        public CompEverflower()
        {
            _attunementInfo = new EverflowerAttunementInfo();
            _innerContainer = new ThingOwner<Pawn>(this);
        }

        public override void PostExposeData()
        {
            Scribe_Deep.Look(ref _innerContainer, "innerContainer", this);
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

            if (mode != DestroyMode.WillReplace)
            {
                if (_innerContainer.Count > 0)
                {
                    _innerContainer.ClearAndDestroyContents(mode);
                }
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
                drawPos + graphic.drawOffset.RotatedBy(parent.Rotation), 
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
            if (AttunementLevel >= 1)
            {

            }

            if (DebugSettings.godMode)
            {
                if (_attunementInfo.attunementLevel >= 1)
                {
                    yield return new Command_Action()
                    {
                        defaultLabel = "DEV: +1000 Attunement from mana",
                        action = () =>
                        {
                            TransferMana(1000);
                        }
                    };
                }

                if (_innerContainer.Count > 0)
                {
                    yield return new Command_Action()
                    {
                        defaultLabel = "DEV: Eject directly pawn",
                        action = () =>
                        {
                            foreach (var thing in _innerContainer)
                            {
                                EjectFairy((Pawn)thing);
                            }
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

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return _innerContainer;
        }

        public void Link(Pawn pawn)
        {
            Everflower.UnreserveLink();
            _linked.Add(pawn);

            if (_attunementInfo.attunementLevel == 0)
            {
                ChangeAttunementLevel(1);
            }

            pawn.GetCompVivi()?.Notify_LinkEverflower(Everflower);

            Messages.Message(LocalizeString_Message.VV_Message_LinkEverflowerComplete.Translate(pawn.Named("PAWN")), MessageTypeDefOf.PositiveEvent);
        }

        public void Unlink(Pawn pawn)
        {
            if (!_linked.Contains(pawn)) { return; }

            _linked.Remove(pawn);
        }

        public void TransferMana(float mana)
        {
            var nextLevelExp = Props.everflowerAttuneLevelCurve.EvaluateInverted(_attunementInfo.attunementLevel + 1);
            var curLevelExp = Props.everflowerAttuneLevelCurve.EvaluateInverted(_attunementInfo.attunementLevel);
            if (nextLevelExp == curLevelExp) { return; }

            _attunementInfo.attunement += Props.everflowerAttuneRateCurve.Evaluate(mana);

            var newLevel = (int)Props.everflowerAttuneLevelCurve.Evaluate(_attunementInfo.attunement);
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

        private void ChangeAttunementLevel(int level)
        {
            if (_attunementInfo.attunementLevel == level) { return; }
            _attunementInfo.attunementLevel = level;

            if (level > 1)
            {
                Find.LetterStack.ReceiveLetter(
                    LocalizeString_Letter.VV_Letter_EverflowerAttumentLevelUpLabel.Translate(level.Named("LEVEL")),
                    LocalizeString_Letter.VV_Letter_EverflowerAttumentLevelUp.Translate(level.Named("LEVEL")),
                    LetterDefOf.PositiveEvent);
            }

            foreach (var linked in _linked)
            {
                var hediff = linked.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerLink);
                if (hediff != null)
                {
                    hediff.TryGetComp<HediffComp_GiveEverflowerAbility>()?.CheckAndGiveAbility();
                }
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

        private void EjectFairy(Pawn pawn)
        {
            if (_innerContainer.Contains(pawn))
            {
                _innerContainer.TryDrop(pawn, ThingPlaceMode.Radius, out _);
            }
        }
    }
}
