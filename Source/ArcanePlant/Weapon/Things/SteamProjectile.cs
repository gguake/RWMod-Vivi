using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class SteamProjectile : Thing
    {
        public Thing caster;
        public ThingDef weaponDef;
        public DamageDef damageDef;
        public float damageAmount;

        public float friendlyFireSafeDistance;

        public float range;
        public float propagationSpeed;
        public List<IntVec3> affectCells;

        private IntVec3 _initalPosition;
        private float _progress;
        private int _index;

        private HashSet<Thing> _damagedTargets = new HashSet<Thing>();

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_References.Look(ref caster, "caster");
            Scribe_Defs.Look(ref weaponDef, "weaponDef");
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Values.Look(ref damageAmount, "damageAmount");

            Scribe_Values.Look(ref friendlyFireSafeDistance, "friendlyFireSafeDistance");

            Scribe_Values.Look(ref range, "range");
            Scribe_Values.Look(ref propagationSpeed, "propagationSpeed");
            Scribe_Collections.Look(ref affectCells, "affectedCells", LookMode.Value);

            Scribe_Values.Look(ref _initalPosition, "initalPosition");
            Scribe_Values.Look(ref _progress, "progress");
            Scribe_Values.Look(ref _index, "index");

            Scribe_Collections.Look(ref _damagedTargets, "damagedTargets", LookMode.Reference);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            _initalPosition = Position;
        }

        protected override void Tick()
        {
            base.Tick();

            _progress += propagationSpeed / 100f;
            for (int i = _index; i < affectCells.Count; ++i)
            {
                var cell = affectCells[i];
                if (!cell.InBounds(Map)) { continue; }

                var distance = cell.DistanceToSquared(_initalPosition);
                if (distance < _progress * _progress)
                {
                    var thingList = cell.GetThingList(Map);
                    for (int j = 0; j < thingList.Count; ++j)
                    {
                        var thing = thingList[j];
                        if (_damagedTargets.Contains(thing)) { continue; }

                        if (thing is IAttackTarget && !(distance <= friendlyFireSafeDistance && !caster.HostileTo(thing)))
                        {
                            var log = new BattleLogEntry_RangedImpact(caster, thing, thing, weaponDef, null, null);
                            var angleFlat = (cell - _initalPosition).AngleFlat;

                            var dinfo = new DamageInfo(
                                damageDef,
                                damageAmount,
                                0,
                                angleFlat,
                                caster,
                                null,
                                weaponDef,
                                DamageInfo.SourceCategory.ThingOrUnknown,
                                thing);

                            thing.TakeDamage(dinfo).AssociateWithLog(log);
                            if (thing.Destroyed) { j--; }
                        }

                        _damagedTargets.Add(thing);
                    }
                }
                else
                {
                    _index = i;
                    break;
                }
            }

            if (_progress >= range) { Destroy(); }
        }
    }
}
