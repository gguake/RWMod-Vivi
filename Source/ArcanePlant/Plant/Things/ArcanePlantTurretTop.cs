using UnityEngine;
using Verse;

namespace VVRace
{
    public class ArcanePlantTurretTop
    {
        private const int IdleTurnDuration = 40;
        private const int IdleTurnIntervalMin = 200;
        private const int IdleTurnIntervalMax = 400;

        private ArcanePlant_Turret parent;

        private float curRotationInt = Rand.Range(0f, 360f);
        private int ticksUntilIdleTurn;
        private int idleTurnTicksLeft;
        private bool idleTurnClockwise = Rand.Bool;

        public float CurRotation
        {
            get
            {
                return curRotationInt;
            }
            set
            {
                curRotationInt = value;
                if (curRotationInt > 360f)
                {
                    curRotationInt -= 360f;
                }
                if (curRotationInt < 0f)
                {
                    curRotationInt += 360f;
                }
            }
        }

        public void SetRotationFromOrientation()
        {
            CurRotation = parent.Rotation.AsAngle;
        }

        public ArcanePlantTurretTop(ArcanePlant_Turret parent)
        {
            this.parent = parent;
        }

        public void ForceFaceTarget(LocalTargetInfo targ)
        {
            if (parent.Gun == null) { return; }

            if (targ.IsValid)
            {
                CurRotation = (targ.Cell.ToVector3Shifted() - parent.DrawPos).AngleFlat();
            }
        }

        public void TurretTopTick()
        {
            if (parent.Gun == null) { return; }

            var currentTarget = parent.CurrentTarget;
            if (currentTarget.IsValid)
            {
                CurRotation = (currentTarget.Cell.ToVector3Shifted() - parent.DrawPos).AngleFlat();
                ticksUntilIdleTurn = Rand.RangeInclusive(IdleTurnIntervalMin, IdleTurnIntervalMax);
            }

            else if (ticksUntilIdleTurn > 0)
            {
                ticksUntilIdleTurn--;
                if (ticksUntilIdleTurn == 0)
                {
                    if (Rand.Value < 0.5f)
                    {
                        idleTurnClockwise = true;
                    }
                    else
                    {
                        idleTurnClockwise = false;
                    }

                    idleTurnTicksLeft = IdleTurnDuration;
                }
            }
            else
            {
                if (idleTurnClockwise)
                {
                    CurRotation += parent.ArcanePlantModExtension.idleTurnAnglePerTick;
                }
                else
                {
                    CurRotation -= parent.ArcanePlantModExtension.idleTurnAnglePerTick;
                }

                idleTurnTicksLeft--;
                if (idleTurnTicksLeft <= 0)
                {
                    ticksUntilIdleTurn = Rand.RangeInclusive(IdleTurnIntervalMin, IdleTurnIntervalMax);
                }
            }
        }

        public void DrawTurret(Vector3 recoilDrawOffset, float recoilAngleOffset)
        {
            var gun = parent.Gun;
            if (gun == null) { return; }

            var angle = (parent.AttackVerb?.AimAngleOverride ?? CurRotation) + recoilAngleOffset;
            var offset = new Vector3(parent.def.building.turretTopOffset.x, 0f, parent.def.building.turretTopOffset.y).RotatedBy(angle);
            float turretTopDrawSize = gun.DrawSize.x;

            offset += recoilDrawOffset;

            var matrix = default(Matrix4x4);
            if (angle < 180f)
            {
                matrix.SetTRS(parent.DrawPos + Altitudes.AltIncVect * 2f + offset, (angle - 90f).ToQuat(), new Vector3(turretTopDrawSize, 1f, turretTopDrawSize));
                Graphics.DrawMesh(MeshPool.plane10, matrix, gun.Graphic.MatSingleFor(gun), 0);
            }
            else
            {
                matrix.SetTRS(parent.DrawPos + Altitudes.AltIncVect * 2f + offset, (angle + 90f).ToQuat(), new Vector3(turretTopDrawSize, 1f, turretTopDrawSize));
                Graphics.DrawMesh(MeshPool.GridPlaneFlip(new Vector2(1f, 1f)), matrix, gun.Graphic.MatSingleFor(gun), 0);
            }
        }
    }
}
