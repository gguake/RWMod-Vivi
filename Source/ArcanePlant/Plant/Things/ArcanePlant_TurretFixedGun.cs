using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ArcanePlant_TurretFixedGun : ArcanePlant_Turret
    {
        public override Thing Gun => _gun;
        private Thing _gun;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _gun, "gun");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_gun == null)
                {
                    Log.Error("Turret had null gun after loading. Recreating.");
                    MakeGun();
                }
                else
                {
                    UpdateGunVerbs();
                }
            }
        }

        public override void PostMake()
        {
            base.PostMake();
            MakeGun();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (AttackVerb is Verb_LaunchProjectile verbLaunchProjectile)
            {
                EquipmentUtility.Recoil(Gun.def, verbLaunchProjectile, out var drawOffset, out var angleOffset, _turretTop.CurRotation);
                _turretTop.DrawTurret(ArcanePlantTurretExtension.turretTopBaseOffset + drawOffset, ArcanePlantTurretExtension.turretTopBaseAngle + angleOffset);
            }
            else
            {
                _turretTop.DrawTurret(ArcanePlantTurretExtension.turretTopBaseOffset, ArcanePlantTurretExtension.turretTopBaseAngle);
            }

            base.DrawAt(drawLoc, flip);
        }

        public void MakeGun()
        {
            _gun = ThingMaker.MakeThing(def.building.turretGunDef);
            UpdateGunVerbs();
        }
    }
}
