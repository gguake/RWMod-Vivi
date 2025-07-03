using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ViviHatcher : CompProperties
    {
        public SimpleCurve hatcherDaystoHatch;
        public SimpleCurve geneCountCurve;

        public float adaptTemperatureBonus;

        public CompProperties_ViviHatcher()
        {
            compClass = typeof(CompViviHatcher);
        }
    }

    public class CompViviHatcher : ThingComp
    {
        public CompProperties_ViviHatcher Props => (CompProperties_ViviHatcher)props;

        public CompTemperatureRuinable FreezerComp => parent.GetComp<CompTemperatureRuinable>();

        public bool TemperatureDamaged
        {
            get
            {
                if (FreezerComp != null)
                {
                    return FreezerComp.Ruined;
                }
                return false;
            }
        }

        public bool CanHatch
        {
            get
            {
                var parentHolder = parent.ParentHolder;
                if (parent.Spawned || parentHolder == null || !(parentHolder is ViviEggHatchery hatchery)) { return false; }

                return true;
            }
        }

        public float hatchDays = 0f;
        public float hatchProgress = 0f;
        public Pawn hatcheeParent;
        public List<GeneDef> parentXenogenes;
        public int randomSeed = Rand.Int;

        public override void PostPostMake()
        {
            hatchDays = Props.hatcherDaystoHatch.Evaluate(Rand.Range(0, 10000));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref hatchDays, "hatchDays", 0f);
            Scribe_Values.Look(ref hatchProgress, "hatchProgress", 0f);
            Scribe_References.Look(ref hatcheeParent, "hatcheeParent");
            Scribe_Collections.Look(ref parentXenogenes, "xenogenes", LookMode.Def);
            Scribe_Values.Look(ref randomSeed, "randomSeed", 0);
        }

        public override string CompInspectStringExtra()
        {
            var sb = new StringBuilder();
            if (FreezerComp != null)
            {
                sb.Append(LocalizeString_Inspector.VV_Inspector_ProperTemperature.Translate(FreezerComp.Props.minSafeTemperature.ToStringTemperature(), FreezerComp.Props.maxSafeTemperature.ToStringTemperature()));
            }

            if (!TemperatureDamaged)
            {
                sb.AppendLine();
                sb.Append("EggProgress".Translate())
                    .Append(": ")
                    .Append(hatchProgress.ToStringPercent("F1"));
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        public override void CompTickRare()
        {
            TickInterval(GenTicks.TickRareInterval);
        }

        public override void CompTickInterval(int delta)
        {
            TickInterval(delta);
        }

        private void TickInterval(int delta)
        {
            if (TemperatureDamaged)
            {
                parent.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, Mathf.Max(1f, parent.MaxHitPoints * delta / 10000f)));
            }
            else
            {
                if (!CanHatch) { return; }

                var t = Mathf.InverseLerp(FreezerComp.Props.minSafeTemperature, FreezerComp.Props.maxSafeTemperature, parent.AmbientTemperature);
                var bonusMultiplier = Mathf.Lerp(1f, 1f + Props.adaptTemperatureBonus, Mathf.Clamp01(1f - (t - 0.5f) * (t - 0.5f) * 4f));

                if (parent.PositionHeld.GetRoof(parent.MapHeld) == null)
                {
                    bonusMultiplier /= 2f;
                }

                var room = parent.PositionHeld.GetRoom(parent.MapHeld);
                if (room == null || room.OutdoorsForWork)
                {
                    bonusMultiplier /= 2f;
                }

                hatchProgress += 1f / (hatchDays * 60000f) * delta * bonusMultiplier;

                if (hatchProgress > 1f)
                {
                    Hatch();
                }
            }
        }

        public override bool AllowStackWith(Thing other)
        {
            return false;
        }

        public override void PostSplitOff(Thing piece)
        {
            var pieceComp = piece.TryGetComp<CompViviHatcher>();
            if (pieceComp != null)
            {
                pieceComp.hatchDays = hatchDays;
                pieceComp.hatchProgress = hatchProgress;
                pieceComp.hatcheeParent = hatcheeParent;
                pieceComp.parentXenogenes = parentXenogenes;
                pieceComp.randomSeed = randomSeed;
            }
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            base.PreAbsorbStack(otherStack, count);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (DebugSettings.godMode)
            {
                Command_Action commandDebugHatching = new Command_Action();
                commandDebugHatching.defaultLabel = "DEV: Hatching +10%";
                commandDebugHatching.action = () =>
                {
                    hatchProgress += 0.1f;
                };

                yield return commandDebugHatching;
            }
        }

        private void Hatch()
        {
            try
            {
                var hatchery = parent.ParentHolder as ViviEggHatchery;
                if (hatchery == null) { return; }

                var faction = hatchery.Faction;

                var pawnKindDef = (faction?.IsPlayer ?? false) ? VVPawnKindDefOf.VV_PlayerVivi : null;
                if (pawnKindDef != null)
                {
                    try
                    {
                        Rand.PushState(randomSeed);

                        var randomGeneCount = Mathf.FloorToInt(Props.geneCountCurve.Evaluate(Rand.Range(0, 10000)));
                        var xenogenes = ViviUtility.SelectRandomGeneForVivi(randomGeneCount, parentXenogenes);

                        var request = new PawnGenerationRequest(
                            pawnKindDef,
                            faction: faction,
                            allowDowned: true,
                            developmentalStages: DevelopmentalStage.Newborn,
                            forcedXenotype: VVXenotypeDefOf.VV_Vivi,
                            forcedXenogenes: xenogenes);

                        if (hatcheeParent != null && hatcheeParent.Name is NameTriple nameTriple)
                        {
                            request.SetFixedLastName(nameTriple.Last);
                        }

                        var pawn = PawnGenerator.GeneratePawn(request);
                        if (GenSpawn.Spawn(pawn, hatchery.Position, hatchery.Map) != null)
                        {
                            if (pawn != null)
                            {
                                if (hatcheeParent != null)
                                {
                                    if (pawn.IsColonist)
                                    {
                                        Find.LetterStack.ReceiveLetter(
                                            LocalizeString_Letter.VV_Letter_ViviEggHatchedLabel.Translate(),
                                            LocalizeString_Letter.VV_Letter_ViviEggHatched.Translate(hatcheeParent.Named("PARENT")),
                                            LetterDefOf.PositiveEvent,
                                            pawn);
                                    }
                                }
                                else
                                {
                                    if (pawn.IsColonist)
                                    {
                                        Find.LetterStack.ReceiveLetter(
                                            LocalizeString_Letter.VV_Letter_ViviEggHatchedLabel.Translate(),
                                            LocalizeString_Letter.VV_Letter_ViviEggHatchedNoParent.Translate(hatcheeParent.Named("PARENT")),
                                            LetterDefOf.PositiveEvent,
                                            pawn);
                                    }
                                }

                                if (hatcheeParent != null)
                                {
                                    if (pawn.playerSettings != null && hatcheeParent.playerSettings != null && hatcheeParent.Faction == faction)
                                    {
                                        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = hatcheeParent.playerSettings.AreaRestrictionInPawnCurrentMap;
                                    }

                                    if (pawn.RaceProps.IsFlesh)
                                    {
                                        pawn.relations.AddDirectRelation(PawnRelationDefOf.Parent, hatcheeParent);
                                    }
                                }
                            }
                            if (parent.Spawned)
                            {
                                FilthMaker.TryMakeFilth(hatchery.Position, hatchery.Map, ThingDefOf.Filth_AmnioticFluid, count: 3);
                            }
                        }
                        else
                        {
                            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
                        }
                    }
                    finally
                    {
                        Rand.PopState();
                    }
                }
            }
            finally
            {
                parent.Destroy();
            }
        }
    }
}
