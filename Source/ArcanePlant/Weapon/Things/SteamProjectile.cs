using RimWorld;
using System.Collections.Generic;
using System.Linq;
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
        public float heatPerCell;

        public float range;
        public float propagationSpeed;
        public List<IntVec3> affectCells;

        private IntVec3 _initalPosition;
        private float _progress;
        private int _index;

        private HashSet<Thing> _damagedTargets = new HashSet<Thing>();

        public override int UpdateRateTicks => 15;

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

        private Dictionary<Room, float> _roomHeatDict = new Dictionary<Room, float>(4);
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

                        if (thing is Fire)
                        {
                            j--;
                            thing.Destroy();
                            continue;
                        }

                        var fireAttachment = thing.GetAttachment(ThingDefOf.Fire);
                        if (fireAttachment != null)
                        {
                            fireAttachment.Destroy();
                        }

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

                    var room = cell.GetRoom(Map);
                    if (room != null)
                    {
                        if (!_roomHeatDict.ContainsKey(room))
                        {
                            _roomHeatDict.Add(room, 0f);
                        }

                        _roomHeatDict[room] += heatPerCell;
                    }
                }
                else
                {
                    _index = i;
                    break;
                }
            }

            if (_roomHeatDict.Any())
            {
                foreach (var kv in _roomHeatDict)
                {
                    kv.Key.PushHeat(kv.Value);
                }
                _roomHeatDict.Clear();
            }

            if (_progress >= range) { Destroy(); }
        }
    }
}
