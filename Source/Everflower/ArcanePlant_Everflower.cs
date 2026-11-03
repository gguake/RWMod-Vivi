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
        public static readonly Material TeleportLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(1f, 1f, 1f));

        private static readonly Texture2D TeleportCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_MoveEverflower");
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

        public bool HasRitualCooldown => _ritualCooldownTick > GenTicks.TicksGame;
        public int CurRitualCooldownTicks => _ritualCooldownTick;

        private int _ritualCooldownTick;
        private int _lastRitualTick;

        public IntVec3? ReservedTeleportCell => _reserveTeleportCell;
        private IntVec3? _reserveTeleportCell;

        // 구버전 세이브 등에서 선택값이 null이면 날씨를 무작위로 결정한다.
        public EverflowerWeatherChoice? SelectedRitualWeather
        {
            get => _selectedRitualWeather;
            set => _selectedRitualWeather = value;
        }
        private EverflowerWeatherChoice? _selectedRitualWeather;

        public EverflowerWeatherChoice? ConsumeSelectedRitualWeather()
        {
            var selected = _selectedRitualWeather;
            _selectedRitualWeather = null;
            return selected;
        }

        public IntVec3? SelectedFlowerLandCell
        {
            get => _selectedFlowerLandCell;
            set => _selectedFlowerLandCell = value;
        }
        private IntVec3? _selectedFlowerLandCell;

        public IntVec3? ConsumeSelectedFlowerLandCell()
        {
            var selected = _selectedFlowerLandCell;
            _selectedFlowerLandCell = null;
            return selected;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _ritualCooldownTick, "ritualCooldownTick");
            Scribe_Values.Look(ref _lastRitualTick, "lastRitualTick");
            Scribe_Values.Look(ref _reserveTeleportCell, "reserveTeleportCell");
            Scribe_Values.Look(ref _selectedRitualWeather, "selectedRitualWeather");
            Scribe_Values.Look(ref _selectedFlowerLandCell, "selectedFlowerLandCell");
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            // 불러올 때 제단이 영원꽃보다 늦게 스폰되므로 제단의 SpawnSetup에서도 안치 캐시를 갱신한다.
            EverflowerComp.RefreshEnshrinement(silent: respawningAfterLoad);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Unreserve();
            base.Destroy(mode);
        }

        public override AcceptanceReport DeconstructibleBy(Faction faction) => EverflowerComp.AttunementLevel == 0;

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            var resonator = EverflowerComp.EnshrinedAltar?.HeldResonator;
            var assimilation = Hediff_EverflowerAssimilation.Of(resonator);
            if (assimilation != null && assimilation.everflower == this)
            {
                resonator.Drawer.renderer.DynamicDrawPhaseAt(phase, assimilation.PawnDrawPos, Rot4.South, neverAimWeapon: true);
                if (phase == DrawPhase.Draw)
                {
                    assimilation.DrawLink();
                }
            }

            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
        }

        public override ushort PathWalkCostFor(Pawn p)
        {
            if (EverflowerComp.AttunementLevel >= 3) { return 36; }

            return 0;
        }

        public override IEnumerable<string> GetUniqueFunctionDescriptions()
        {
            foreach (var description in base.GetUniqueFunctionDescriptions())
            {
                yield return description;
            }

            yield return LocalizeString_PlantFunction.VV_PlantFunction_Everflower.Translate();
            yield return LocalizeString_PlantFunction.VV_PlantFunction_EverflowerTeleport.Translate(
                EverflowerComp.Props.teleportRange.ToString("0.#"));
        }

        private List<FloatMenuOption> _tmpFloatMenuOptions = new List<FloatMenuOption>();
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                if (gizmo is Designator_Install || gizmo is Designator_Uninstall) { continue; }

                yield return gizmo;
            }

            bool ValidateCell(LocalTargetInfo target)
            {
                var cell = ResolveTeleportCell(target.Cell, out var altar);
                if (altar == null && cell.DistanceToSquared(Position) > Mathf.Pow(EverflowerComp.Props.teleportRange, 2f)) { return false; }
                if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, cell, def)) { return false; }

                return true;
            }

            if (Spawned && (Faction?.IsPlayer ?? false) && EverflowerComp.AttunementLevel >= 1 && !EverflowerComp.IsEnshrined)
            {
                if (!_reserveTeleportCell.HasValue)
                {
                    string desc = LocalizeString_Command.VV_Command_ReserveTeleportEverflowerDesc.Translate();
                    if (Building_EverflowerAltar.FindOnMap(Map) != null)
                    {
                        desc += "\n\n" + LocalizeString_Command.VV_Command_ReserveTeleportEverflowerAltarDesc.Translate(Building_EverflowerAltar.RequiredAttunementLevel.Named("LEVEL")).Resolve();
                    }

                    yield return new Command_Action()
                    {
                        defaultLabel = LocalizeString_Command.VV_Command_ReserveTeleportEverflower.Translate(),
                        defaultDesc = desc,
                        icon = TeleportCommandTex,
                        action = () =>
                        {
                            Find.Targeter.BeginTargeting(
                                TargetingParameters.ForCell(),
                                action: (target) =>
                                {
                                    var cell = ResolveTeleportCell(target.Cell, out var altar);

                                    // 검증에 실패한 클릭은 action을 호출하지 않으므로 단계 조건 검증과 거부 메시지는 여기서 처리한다.
                                    if (altar != null && EverflowerComp.AttunementLevel < Building_EverflowerAltar.RequiredAttunementLevel)
                                    {
                                        Messages.Message(
                                            LocalizeString_Message.VV_Message_EverflowerAltarRequiresLevel.Translate(Building_EverflowerAltar.RequiredAttunementLevel.Named("LEVEL")),
                                            this,
                                            MessageTypeDefOf.RejectInput,
                                            historical: false);
                                        return;
                                    }

                                    _reserveTeleportCell = cell;
                                },
                                highlightAction: (target) =>
                                {
                                    if (ValidateCell(target))
                                    {
                                        GenDraw.DrawTargetHighlight(ResolveTeleportCell(target.Cell, out _));
                                    }
                                },
                                targetValidator: ValidateCell,
                                mouseAttachment: null,
                                onUpdateAction: (target) =>
                                {
                                    GenDraw.DrawRadiusRing(Position, EverflowerComp.Props.teleportRange);
                                });
                        }
                    };
                }
                else
                {
                    yield return new Command_Action()
                    {
                        defaultLabel = LocalizeString_Command.VV_Command_CancelReservationEverflower.Translate(),
                        defaultDesc = LocalizeString_Command.VV_Command_CancelReservationEverflowerDesc.Translate(),
                        icon = CancelCommandTex,
                        action = () =>
                        {
                            _reserveTeleportCell = default;
                        }
                    };
                }
            }

            if (Spawned && (Faction?.IsPlayer ?? false) && EverflowerComp.AttunementLevel >= 1)
            {
                if (DebugSettings.godMode)
                {
                    if (HasRitualCooldown)
                    {
                        yield return new Command_Action()
                        {
                            defaultLabel = $"DEV: Reset ritual cooldown",
                            action = () =>
                            {
                                _ritualCooldownTick = GenTicks.TicksGame + 1;
                                _lastRitualTick = GenTicks.TicksGame - 1;
                            }
                        };
                    }
                }
            }
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (_reserveTeleportCell.HasValue)
            {
                var a = this.TrueCenter();
                var b = _reserveTeleportCell.Value.ToVector3Shifted();
                GenDraw.DrawLineBetween(a, b, TeleportLineMat);
            }
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (dinfo.Def == DamageDefOf.SurgicalCut)
            {
                absorbed = true;
                return;
            }

            // 화염 피해도 제단이 대신 받게 한다. 일반 화분처럼 꽃에 직접 적용하면 소이탄 하나로 엔딩이 실패할 수 있다.
            var altar = EverflowerComp.EnshrinedAltar;
            if (altar != null)
            {
                altar.TakeDamage(dinfo);
                absorbed = true;
                return;
            }

            base.PreApplyDamage(ref dinfo, out absorbed);
        }

        public override void Notify_ArcanePlantPotDespawned()
        {
            var wasEnshrined = EverflowerComp.IsEnshrined;
            if (TeleportRandomly())
            {
                if (wasEnshrined && (ViviEndingQuest.Current?.IsAnchor(this) ?? false))
                {
                    Messages.Message(LocalizeString_Message.VV_Message_AnchorEverflowerLeftAltar.Translate(), this, MessageTypeDefOf.NegativeEvent);
                }

                return;
            }

            base.Notify_ArcanePlantPotDespawned();
        }

        public void Notify_TeleportJobCompleted()
        {
            if (_reserveTeleportCell.HasValue)
            {
                var cell = ResolveTeleportCell(_reserveTeleportCell.Value, out var altar);
                _reserveTeleportCell = null;

                // 예약 후 제단이 건설됐을 수 있으므로 이동 직전에 단계 조건을 다시 검사한다.
                // 미달이면 무작위 이동 없이 예약만 취소해 낮은 단계의 영원꽃이 제단에 갇히지 않게 한다.
                if (altar != null && EverflowerComp.AttunementLevel < Building_EverflowerAltar.RequiredAttunementLevel)
                {
                    Messages.Message(
                        LocalizeString_Message.VV_Message_EverflowerAltarRequiresLevel.Translate(Building_EverflowerAltar.RequiredAttunementLevel.Named("LEVEL")),
                        this,
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                    return;
                }

                if (!Teleport(cell))
                {
                    TeleportRandomly();
                }
            }
        }

        public bool TeleportRandomly()
        {
            var cells = Map.AllCells.Where(c => c != Position && !IsAltarCell(c) && ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, c, def)).ToList();
            if (cells.Count > 0)
            {
                if (Teleport(cells.RandomElement()))
                {
                    Messages.Message(LocalizeString_Message.VV_Message_EverflowerRandomTeleported.Translate(), MessageTypeDefOf.NeutralEvent);
                    return true;
                }
            }

            return false;
        }

        public bool Teleport(IntVec3 cell)
        {
            if (!cell.IsValid || !cell.InBounds(Map) || !ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, cell, def))
            {
                return false;
            }

            var map = Map;
            var previousPosition = Position;
            var wasEnshrined = EverflowerComp.IsEnshrined;
            var mapComponent = _mapComponent;

            // 순간이동은 디스폰 없이 Position만 바꾸므로 자동 갱신되지 않는 정보를 직접 갱신한다.
            map.edificeGrid.DeRegister(this);
            Position = cell;
            map.edificeGrid.Register(this);
            RestorePotEdifice(map, mapComponent, previousPosition);

            mapComponent?.Notify_ArcanePlantMoved(this, previousPosition);

            map.pathFinder.MapData.Notify_CellDelta(previousPosition);
            map.pathing.RecalculatePerceivedPathCostAt(previousPosition);
            map.pathing.RecalculatePerceivedPathCostUnderThing(this);

            var glower = GetComp<CompGlower>();
            if (glower != null && glower.Glows)
            {
                glower.ForceRegister(map);
            }

            EverflowerComp.SpawnLevelEffect(previousPosition, map, wasEnshrined);

            EverflowerComp.RefreshEnshrinement();
            if (!EverflowerComp.IsEnshrined)
            {
                EverflowerComp.SpawnLevelEffect(Position, map, false);
            }

            return true;
        }

        public bool CanGatherByPawn(Pawn pawn, RecipeDef_Gathering recipe)
        {
            if (!pawn.IsVivi() || pawn.ageTracker.AgeBiologicalYears < 13) { return false; }

            if (pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_EverflowerImpact))
            {
                return false;
            }

            // 의식 밖의 폰이 영원꽃을 예약하면 의식이 취소되므로 의식 중에는 채집을 막는다.
            if (IsTargetOfAnyRitual()) { return false; }

            return true;
        }

        // IsRitualTarget은 참가자가 도착한 뒤에만 참이므로 집결 중인 의식도 감지하도록 로드를 직접 확인한다.
        private bool IsTargetOfAnyRitual()
        {
            if (!Spawned) { return false; }

            var lords = Map.lordManager.lords;
            for (int i = 0; i < lords.Count; ++i)
            {
                if (lords[i].LordJob is LordJob_Ritual ritual && ritual.selectedTarget.Thing == this)
                {
                    return true;
                }
            }

            return false;
        }

        public void Notify_Gathered(Pawn pawn, RecipeDef_Gathering recipe)
        {
            if (pawn.IsVivi())
            {
                var ticks = Mathf.Clamp(
                    Mathf.CeilToInt(Rand.Range(60000 * 5, 60000 * 10) * pawn.ageTracker.BiologicalTicksPerTick), 
                    0, 
                    (int)(pawn.ageTracker.AgeBiologicalTicks - 13.01f * 60000 * 60));

                if (ticks > 0)
                {
                    pawn.ageTracker.AgeBiologicalTicks -= ticks;

                    Map.GetManaComponent().ChangeEnvironmentMana(Position, ticks / 60000f * 50);
                    pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerImpact);
                }
            }
        }

        public void Notify_RitualComplete(float quality)
        {
            var cooldown = Mathf.CeilToInt(EverflowerComp.Props.ritualCooldownCurve.Evaluate(quality));

            if (cooldown > 0)
            {
                _lastRitualTick = GenTicks.TicksGame;
                _ritualCooldownTick = _lastRitualTick + cooldown;
            }

            Unreserve();
        }

        public void Unreserve()
        {
            _reserveTeleportCell = default;
        }

        private IntVec3 ResolveTeleportCell(IntVec3 cell, out Building_EverflowerAltar altar)
        {
            altar = null;
            if (!cell.IsValid || Map == null || !cell.InBounds(Map)) { return cell; }

            altar = _mapComponent?.GetArcanePlantPot(cell) as Building_EverflowerAltar;
            return altar != null ? altar.Position : cell;
        }

        private bool IsAltarCell(IntVec3 cell)
        {
            return _mapComponent?.GetArcanePlantPot(cell) is Building_EverflowerAltar;
        }

        // EdificeGrid.DeRegister는 칸을 비우므로 식물이 떠난 자리에는 화분을 다시 등록한다.
        // 다칸 화분이 다른 칸의 식물을 덮어쓰지 않도록 해당 식물도 다시 등록한다.
        private static void RestorePotEdifice(Map map, ArcanePlantMapComponent mapComponent, IntVec3 cell)
        {
            var pot = mapComponent?.GetArcanePlantPot(cell);
            if (pot == null) { return; }

            map.edificeGrid.Register(pot);
            foreach (var potCell in pot.OccupiedRect())
            {
                if (potCell == cell) { continue; }

                var plant = potCell.GetFirstThing<ArcanePlant>(map);
                if (plant != null && plant.Spawned && plant.def.IsEdifice())
                {
                    map.edificeGrid.Register(plant);
                }
            }
        }
    }
}
