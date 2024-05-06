using RimWorld;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public class LordJob_DefendViviBase : LordJob
    {
        private Faction _faction;
        private IntVec3 _baseCenter;
        private bool _attackWhenPlayerBecameEnemy;

        public override bool AddFleeToil => false;

        public LordJob_DefendViviBase()
        {
        }

        public LordJob_DefendViviBase(Faction faction, IntVec3 baseCenter, bool attackWhenPlayerBecameEnemy = false)
        {
            this._faction = faction;
            this._baseCenter = baseCenter;
            this._attackWhenPlayerBecameEnemy = attackWhenPlayerBecameEnemy;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref _faction, "faction");
            Scribe_Values.Look(ref _baseCenter, "baseCenter");
            Scribe_Values.Look(ref _attackWhenPlayerBecameEnemy, "attackWhenPlayerBecameEnemy", defaultValue: false);
        }

        public override StateGraph CreateGraph()
        {
            var stateGraph = new StateGraph();
            var lordToil_DefendBase = new LordToil_DefendViviBase(_baseCenter);
            stateGraph.StartingToil = lordToil_DefendBase;

            var lordToil_DefendBase2 = new LordToil_DefendViviBase(_baseCenter);
            stateGraph.AddToil(lordToil_DefendBase2);

            var lordToil_AssaultColony = new LordToil_AssaultColony(attackDownedIfStarving: true)
            {
                useAvoidGrid = true
            };
            stateGraph.AddToil(lordToil_AssaultColony);

            var transition_becameHostile = new Transition(lordToil_DefendBase, lordToil_DefendBase2);
            transition_becameHostile.AddSource(lordToil_AssaultColony);
            transition_becameHostile.AddTrigger(new Trigger_BecameNonHostileToPlayer());
            stateGraph.AddTransition(transition_becameHostile);

            var transition2 = new Transition(lordToil_DefendBase2, _attackWhenPlayerBecameEnemy ? ((LordToil)lordToil_AssaultColony) : ((LordToil)lordToil_DefendBase));
            if (_attackWhenPlayerBecameEnemy)
            {
                transition2.AddSource(lordToil_DefendBase);
            }
            transition2.AddTrigger(new Trigger_BecamePlayerEnemy());
            stateGraph.AddTransition(transition2);

            var transition3 = new Transition(lordToil_DefendBase, lordToil_AssaultColony);
            transition3.AddTrigger(new Trigger_FractionPawnsLost(0.35f));
            transition3.AddTrigger(new Trigger_TicksPassed(251999));
            transition3.AddTrigger(new Trigger_UrgentlyHungry());
            transition3.AddTrigger(new Trigger_OnClamor(ClamorDefOf.Ability));
            transition3.AddPostAction(new TransitionAction_WakeAll());

            var taggedString = "MessageDefendersAttacking".Translate(_faction.def.pawnsPlural, _faction.Name, Faction.OfPlayer.def.pawnsPlural).CapitalizeFirst();
            transition3.AddPreAction(new TransitionAction_Message(taggedString, MessageTypeDefOf.ThreatBig));
            stateGraph.AddTransition(transition3);

            return stateGraph;
        }
    }
}
