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

        private Pawn _linkReserved;
        private Pawn _linked;

        public bool CanReserveRoyalViviLink => _linked == null && _linkReserved == null;

        public Pawn LinkedPawn => _linked;

        public Pawn ReservedPawn => _linkReserved;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref _linkReserved, "linkReserved");
            Scribe_References.Look(ref _linked, "linked");
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                if (LinkedPawn != null && !LinkedPawn.Destroyed)
                {
                    LinkedPawn.GetCompVivi()?.Notify_LinkedEverflowerSpawned();
                }
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (LinkedPawn != null && !LinkedPawn.Destroyed)
            {
                LinkedPawn.GetCompVivi()?.Notify_LinkedEverflowerDespawned();
            }

            UnreserveLink();
            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (LinkedPawn != null && !LinkedPawn.Destroyed)
            {
                LinkedPawn.GetCompVivi().Notify_LinkedEverflowerDestroyed();
            }

            UnreserveLink();
            base.Destroy(mode);
        }

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
                            _linkReserved = pawn;

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

                if (LinkedPawn == null)
                {
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

        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (LinkedPawn != null && LinkedPawn.Spawned && LinkedPawn.Map == Map)
            {
                LinkedPawn.TakeDamage(dinfo);
                absorbed = true;
                return;
            }

            base.PreApplyDamage(ref dinfo, out absorbed);
        }

        public override void PreTraded(TradeAction action, Pawn playerNegotiator, ITrader trader)
        {
            if (LinkedPawn != null)
            {
                LinkedPawn.GetCompVivi().Notify_LinkedEverflowerDestroyed();
            }
            else
            {
                UnreserveLink();
            }

            base.PreTraded(action, playerNegotiator, trader);
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

        public void Link(Pawn pawn)
        {
            _linkReserved = null;
            _linked = pawn;

            pawn.GetCompVivi()?.Notify_LinkEverflower(this);

            Messages.Message(LocalizeString_Message.VV_Message_LinkEverflowerComplete.Translate(pawn.Named("PAWN")), MessageTypeDefOf.PositiveEvent);
        }

        public void UnreserveLink()
        {
            _linkReserved = null;
        }
    }
}
