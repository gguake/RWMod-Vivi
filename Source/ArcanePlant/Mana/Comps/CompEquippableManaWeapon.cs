using RimWorld;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace VVRace
{
    public struct ManaWeaponStatModifier
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
            if (!_offsetCurveDict.TryGetValue(stat, out var curve))
            {
                var data = statOffsetCurves.FirstOrDefault(v => v.stat == stat);
                if (data.stat == stat)
                {
                    curve = data.curve;
                }

                _offsetCurveDict.Add(stat, curve);
            }

            return curve != null ? curve.Evaluate(t) : 0f;
        }

        public float GetStatFactor(StatDef stat, float t)
        {
            if (!_factorCurveDict.TryGetValue(stat, out var curve))
            {
                var data = statFactorCurves.FirstOrDefault(v => v.stat == stat);
                if (data.stat == stat)
                {
                    curve = data.curve;
                }

                _factorCurveDict.Add(stat, curve);
            }

            return curve != null ? curve.Evaluate(t) : 1f;
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
            yield return new ManaGizmo(ManaComp);
        }

        public override void Notify_UsedWeapon(Pawn pawn)
        {
            if (parent.def.IsMeleeWeapon)
            {
                var cost = parent.GetStatValue(VVStatDefOf.VV_MeleeWeapon_ManaCost);
                if (cost > 0 && ManaComp.Stored > cost)
                {
                    ManaComp.Stored -= cost;
                }
            }
        }
    }
}
