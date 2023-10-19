using UnityEngine;
using Verse;

namespace VVRace
{
    public class TurretPlantTop
    {
        private const float IdleTurnDegreesPerTick = 0.1f;
        private const int IdleTurnDuration = 40;
        private const int IdleTurnIntervalMin = 200;
        private const int IdleTurnIntervalMax = 400;

        private TurretPlant parentTurret;

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
            CurRotation = parentTurret.Rotation.AsAngle;
        }

        public TurretPlantTop(TurretPlant ParentTurret)
        {
            parentTurret = ParentTurret;
        }

        public void ForceFaceTarget(LocalTargetInfo targ)
        {
            if (targ.IsValid)
            {
                CurRotation = (targ.Cell.ToVector3Shifted() - parentTurret.DrawPos).AngleFlat();
            }
        }

        public void TurretTopTick()
        {
            var currentTarget = parentTurret.CurrentTarget;
            if (currentTarget.IsValid)
            {
                CurRotation = (currentTarget.Cell.ToVector3Shifted() - parentTurret.DrawPos).AngleFlat();
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
                    CurRotation += IdleTurnDegreesPerTick;
                }
                else
                {
                    CurRotation -= IdleTurnDegreesPerTick;
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
            var v = new Vector3(parentTurret.def.building.turretTopOffset.x, 0f, parentTurret.def.building.turretTopOffset.y).RotatedBy(CurRotation);
            float turretTopDrawSize = parentTurret.def.building.turretTopDrawSize;

            v = v.RotatedBy(recoilAngleOffset);
            v += recoilDrawOffset;

            float rot = parentTurret.CurrentEffectiveVerb?.AimAngleOverride ?? CurRotation;
            var matrix = default(Matrix4x4);

            matrix.SetTRS(parentTurret.DrawPos + Altitudes.AltIncVect * 2f + v, (-90f + rot).ToQuat(), new Vector3(turretTopDrawSize, 1f, turretTopDrawSize));
            Graphics.DrawMesh(MeshPool.plane10, matrix, parentTurret.def.building.turretTopMat, 0);
        }
    }
}
