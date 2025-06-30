using UnityEngine;
using Verse;

namespace VVRace
{
    public class ArcanePlantTurretExtension : DefModExtension
    {
        public float idleTurnAnglePerTick = 0.1f;
        public Vector3 turretTopBaseOffset = Vector3.zero;
        public float turretTopBaseAngle = 0f;
        public bool turretTopBaseFlippable = false;
    }
}
