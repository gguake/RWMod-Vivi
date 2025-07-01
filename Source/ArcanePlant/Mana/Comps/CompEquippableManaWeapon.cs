using RimWorld;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace VVRace
{
    public class ManaWeaponStatModifier
    {
        public StatDef stat;
        public SimpleCurve curve;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "stat", xmlRoot.Name);
            curve = DirectXmlToObject.ObjectFromXml<SimpleCurve>(xmlRoot, true);
        }
    }

    public class CompProperties_EquippableManaWeapon : CompProperties
    {
        public List<ManaWeaponStatModifier> statOffsetCurves;
        public List<ManaWeaponStatModifier> statFactorCurves;

        private Dictionary<StatDef, SimpleCurve> _offsetCurveDict;
        private Dictionary<StatDef, SimpleCurve> _factorCurveDict;

        public float GetStatOffset(StatDef stat, float t)
        {
            if (_offsetCurveDict == null)
            {
                _offsetCurveDict = new Dictionary<StatDef, SimpleCurve>();
                if (statOffsetCurves != null)
                {
                    foreach (var mod in statOffsetCurves)
                    {
                        _offsetCurveDict.Add(mod.stat, mod.curve);
                    }
                }
            }

            return _offsetCurveDict.TryGetValue(stat, out var curve) ? curve.Evaluate(t) : 0f;
        }

        public float GetStatFactor(StatDef stat, float t)
        {
            if (_factorCurveDict == null)
            {
                _factorCurveDict = new Dictionary<StatDef, SimpleCurve>();
                if (statFactorCurves != null)
                {
                    foreach (var mod in statFactorCurves)
                    {
                        _factorCurveDict.Add(mod.stat, mod.curve);
                    }
                }
            }

            return _factorCurveDict.TryGetValue(stat, out var curve) ? curve.Evaluate(t) : 1f;
        }

        public CompProperties_EquippableManaWeapon()
        {
            compClass = typeof(CompEquippableManaWeapon);
        }
    }

    public class CompEquippableManaWeapon : CompEquippable
    {
        public CompProperties_EquippableManaWeapon Props => (CompProperties_EquippableManaWeapon)props;

        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null) { _manaComp = parent.GetComp<CompMana>(); }
                return _manaComp;
            }
        }
        private CompMana _manaComp;

        public override float GetStatOffset(StatDef stat)
        {
            var manaComp = ManaComp;
            if (manaComp == null) { return 0f; }

            return Props.GetStatOffset(stat, ManaComp.StoredPct);
        }

        public override float GetStatFactor(StatDef stat)
        {
            var manaComp = ManaComp;
            if (manaComp == null) { return 1f; }

            return Props.GetStatFactor(stat, ManaComp.StoredPct);
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            if (Holder?.Faction?.IsPlayer ?? false)
            {
                yield return new ManaGizmo(ManaComp);
            }
        }

        public override void Notify_UsedWeapon(Pawn pawn)
        {
            if (parent.def.IsMeleeWeapon)
            {
                var cost = parent.GetStatValue(VVStatDefOf.VV_MeleeWeapon_ManaCost);
                if (cost > 0 && ManaComp.Stored >= cost)
                {
                    ManaComp.Stored -= cost;
                }
            }
        }
    }
}
