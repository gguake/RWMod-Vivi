using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ArcanePlant_Everflower : ArcanePlant, IGatherableTarget
    {
        private static readonly Texture2D ReserveLinkCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_LinkConnect");
        private static readonly Texture2D CancelCommandTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        private Pawn _linkReserved;
        private Pawn _linkedRoyalVivi;

        public bool CanReserveRoyalViviLink => _linkedRoyalVivi == null && _linkReserved == null;

        public void UnreserveLink()
        {
            _linkReserved = null;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (_linkedRoyalVivi == null)
            {
                if (_linkReserved == null)
                {
                    var cmd = new Command_Action()
                    {
                        icon = ReserveLinkCommandTex,
                        defaultLabel = LocalizeString_Command.VV_Command_ReserveLinkEverflower,
                        defaultDesc = LocalizeString_Command.VV_Command_ReserveLinkEverflowerDesc,
                        action = () =>
                        {

                        }
                    };
                    yield return cmd;
                }
                else
                {
                    var cmd = new Command_Action()
                    {
                        icon = CancelCommandTex,
                        defaultLabel = LocalizeString_Command.VV_Command_CancelReserveLinkEverflower,
                        defaultDesc = LocalizeString_Command.VV_Command_CancelReserveLinkEverflowerDesc,
                        action = () =>
                        {
                            UnreserveLink();
                        }
                    };
                    yield return cmd;
                }
            }
        }

        public bool CanGatherByPawn(Pawn pawn, RecipeDef_Gathering recipe)
        {
            if (!pawn.IsVivi() || pawn.ageTracker.AgeBiologicalYears < 13) { return false; }

            if (pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_EverflowerImpact))
            {
                return false;
            }

            return true;
        }

        public void Notify_Gathered(Pawn pawn, RecipeDef_Gathering recipe)
        {
            if (pawn.IsVivi())
            {
                var ticks = Mathf.Clamp(
                    Mathf.CeilToInt(Rand.Range(60000 * 5, 60000 * 10) * pawn.ageTracker.BiologicalTicksPerTick), 
                    0, 
                    (int)(pawn.ageTracker.AgeBiologicalTicks - 13 * 60000 * 60)) - 1;

                if (ticks > 0)
                {
                    pawn.ageTracker.AgeBiologicalTicks -= ticks;

                    Map.GetManaComponent().ChangeEnvironmentMana(Position, ticks / 60000f * 50);
                    pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerImpact);
                }
            }
        }
    }
}
