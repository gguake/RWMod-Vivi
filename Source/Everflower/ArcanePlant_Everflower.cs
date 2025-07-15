using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ArcanePlant_Everflower : ArcanePlant, IGatherableTarget
    {
        private static readonly Texture2D ReserveLinkCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_LinkConnect");
        private static readonly Texture2D CancelCommandTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        public CompEverflower EverflowerComp
        {
            get
            {
                if (_everflowerComp == null)
                {
                    _everflowerComp = GetComp<CompEverflower>();
                }
                return _everflowerComp;
            }
        }
        private CompEverflower _everflowerComp;

        public int AttunementLevel => EverflowerComp.AttunementLevel;

        public Pawn ReservedPawn => _linkReservedPawn;
        private Pawn _linkReservedPawn;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref _linkReservedPawn, "linkReserved");
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            UnreserveLink();
            base.Destroy(mode);
        }

        public override AcceptanceReport DeconstructibleBy(Faction faction) => false;

        private List<FloatMenuOption> _tmpFloatMenuOptions = new List<FloatMenuOption>();
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Spawned && (Faction?.IsPlayer ?? false))
            {
                _tmpFloatMenuOptions.Clear();
                foreach (var pawn in Map.mapPawns.AllPawnsSpawned.Where(p => p.IsRoyalVivi() && (p.IsColonist || p.IsSlaveOfColony)).OrderBy(p => p.IsColonist ? 0 : 1))
                {
                    var report = pawn.CanLinkEverflower();
                    if (report.Accepted)
                    {
                        _tmpFloatMenuOptions.Add(new FloatMenuOption(pawn.Label, () =>
                        {
                            _linkReservedPawn = pawn;

                        }, pawn, Color.white));
                    }
                    else
                    {
                        if (!report.Reason.NullOrEmpty())
                        {
                            _tmpFloatMenuOptions.Add(new FloatMenuOption($"{pawn.Label}: {report.Reason}", null, pawn, Color.white));
                            continue;
                        }
                    }
                }

                if (Map.mapPawns.AllPawnsSpawned.Any(p => p.IsRoyalVivi()))
                {
                    if (ReservedPawn == null)
                    {
                        var cmd = new Command_Action()
                        {
                            icon = ReserveLinkCommandTex,
                            defaultLabel = LocalizeString_Command.VV_Command_ReserveLinkEverflower.Translate(),
                            defaultDesc = LocalizeString_Command.VV_Command_ReserveLinkEverflowerDesc.Translate(),
                            action = () =>
                            {
                                if (!_tmpFloatMenuOptions.Any())
                                {
                                    _tmpFloatMenuOptions.Add(new FloatMenuOption(LocalizeString_Gizmo.VV_Gizmo_OptionLabel_NoRoyalVivi.Translate(), null));
                                }

                                Find.WindowStack.Add(new FloatMenu(_tmpFloatMenuOptions));
                            }
                        };
                        yield return cmd;
                    }
                    else
                    {
                        var cmd = new Command_Action()
                        {
                            icon = CancelCommandTex,
                            defaultLabel = LocalizeString_Command.VV_Command_CancelReserveLinkEverflower.Translate(),
                            defaultDesc = LocalizeString_Command.VV_Command_CancelReserveLinkEverflowerDesc.Translate(),
                            action = () =>
                            {
                                UnreserveLink();
                            }
                        };
                        yield return cmd;
                    }
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

        public void UnreserveLink()
        {
            _linkReservedPawn = null;
        }
    }
}
