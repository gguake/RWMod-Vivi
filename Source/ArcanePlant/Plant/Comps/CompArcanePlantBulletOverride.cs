using System.Collections.Generic;
using System.Xml;
using Verse;

namespace VVRace
{
    public class BulletReplaceData
    {
        public ThingDef targetTurretDef;
        public BulletReplacer replacer;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "targetTurretDef", xmlRoot.Name);
            replacer = DirectXmlToObject.ObjectFromXml<BulletReplacer>(xmlRoot, true);
        }
    }

    public class BulletModifierData
    {
        public ThingDef targetTurretDef;
        public BulletModifier modifier;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "targetTurretDef", xmlRoot.Name);
            modifier = DirectXmlToObject.ObjectFromXml<BulletModifier>(xmlRoot, true);
        }
    }

    public class BulletReplacer
    {
        public ThingDef bulletDef;
        public float chance;
    }

    public class BulletModifier : IExposable
    {
        public int additionalDamage;
        public float additionalArmorPenetration;

        public void ExposeData()
        {
            Scribe_Values.Look(ref additionalDamage, "addditionalDamage");
            Scribe_Values.Look(ref additionalArmorPenetration, "additionalArmorPenetration");
        }
    }

    public class CompProperties_ArcanePlantBulletOverride : CompProperties
    {
        public List<BulletReplaceData> replacers;
        public List<BulletModifierData> modifiers;

        public CompProperties_ArcanePlantBulletOverride()
        {
            compClass = typeof(CompArcanePlantBulletOverride);
        }
    }

    public class CompArcanePlantBulletOverride : ThingComp
    {
        public CompProperties_ArcanePlantBulletOverride Props => (CompProperties_ArcanePlantBulletOverride)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            foreach (var pos in GenAdjFast.AdjacentCellsCardinal(parent.Position))
            {
                var turret = pos.GetFirstThing<ArcanePlant_Turret>(parent.Map);
                if (turret != null)
                {
                    ApplyBulletOverrides(turret);
                }
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            RemoveAdjacentBulletOverrides(map);
        }

        public override void Notify_DefsHotReloaded()
        {
            RemoveAdjacentBulletOverrides(parent.Map);

            foreach (var pos in GenAdjFast.AdjacentCellsCardinal(parent.Position))
            {
                var turret = pos.GetFirstThing<ArcanePlant_Turret>(parent.Map);
                if (turret != null)
                {
                    ApplyBulletOverrides(turret);
                }
            }
        }

        public void ApplyBulletOverrides(ArcanePlant_Turret turret)
        {
            if (Props.replacers != null)
            {
                foreach (var data in Props.replacers)
                {
                    if (turret.def == data.targetTurretDef)
                    {
                        turret.AddBulletReplaceData(parent, data.replacer);
                        break;
                    }
                }
            }

            if (Props.modifiers != null)
            {
                foreach (var data in Props.modifiers)
                {
                    if (turret.def == data.targetTurretDef)
                    {
                        turret.AddBulletModifierData(parent, data.modifier);
                        break;
                    }
                }
            }
        }

        private void RemoveAdjacentBulletOverrides(Map map)
        {
            foreach (var pos in GenAdjFast.AdjacentCellsCardinal(parent.Position))
            {
                var turret = pos.GetFirstThing<ArcanePlant_Turret>(map);
                if (turret != null)
                {
                    turret.RemoveBulletOverrideData(parent);
                }
            }
        }
    }
}
