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

        // 날씨 변경 의식에서 선택한 날씨. 의식 시작 시 설정되고 의식 완료(Apply) 시 소비된다.
        // null인 경우 (구버전 세이브 / 선택 없이 시작된 경우) 랜덤하게 결정된다.
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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _ritualCooldownTick, "ritualCooldownTick");
            Scribe_Values.Look(ref _lastRitualTick, "lastRitualTick");
            Scribe_Values.Look(ref _reserveTeleportCell, "reserveTeleportCell");
            Scribe_Values.Look(ref _selectedRitualWeather, "selectedRitualWeather");
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            // 재설치·불러오기 시 제단 안치 상태를 다시 계산한다.
            // (불러오기에서는 제단이 영원꽃보다 늦게 스폰되므로 제단 쪽 SpawnSetup에서 한 번 더 갱신된다)
            EverflowerComp.RefreshEnshrinement(silent: respawningAfterLoad);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            var map = Map;
            base.DeSpawn(mode);

            // 5/6단계 영원꽃이 맵에서 빠지면(포장·파괴) 제단 마나 상태를 다시 계산한다.
            map?.GetManaComponent()?.RefreshEverflowerAltarState();
        }

        // 엔딩이 묶인 영원꽃의 파괴는 앵커 퀘스트 태그로 바닐라가 엔딩 퀘스트에 알린다("<태그>.Destroyed").
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Unreserve();
            base.Destroy(mode);
        }

        public override AcceptanceReport DeconstructibleBy(Faction faction) => EverflowerComp.AttunementLevel == 0;

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
                // 영원꽃 제단 발판을 지정하면 거리와 관계없이 제단 중앙으로 맞춘다.
                var cell = ResolveTeleportCell(target.Cell, out var altar);
                if (altar == null && cell.DistanceToSquared(Position) > Mathf.Pow(EverflowerComp.Props.teleportRange, 2f)) { return false; }
                if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, cell, def)) { return false; }

                return true;
            }

            // 제단에 안치된 영원꽃은 다시 옮길 수 없다(엔딩 이후에도 제단에서 내리지 않는다).
            if (Spawned && (Faction?.IsPlayer ?? false) && EverflowerComp.AttunementLevel >= 1 && !EverflowerComp.IsEnshrined)
            {
                if (!_reserveTeleportCell.HasValue)
                {
                    string desc = LocalizeString_Command.VV_Command_ReserveTeleportEverflowerDesc.Translate();
                    if (Building_DreamumAltar.FindOnMap(Map) != null)
                    {
                        desc += "\n\n" + LocalizeString_EndingAltar.VV_Command_ReserveTeleportEverflowerAltarDesc.Translate(Building_DreamumAltar.RequiredAttunementLevel.Named("LEVEL")).Resolve();
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

                                    // 검증기를 통과하지 못한 클릭은 action을 호출하지 않으므로, 제단의 단계 조건을 충족하지 못한 경우에는 여기서 메시지를 표시하고 거부한다.
                                    if (altar != null && EverflowerComp.AttunementLevel < Building_DreamumAltar.RequiredAttunementLevel)
                                    {
                                        Messages.Message(
                                            LocalizeString_EndingAltar.VV_Message_EverflowerAltarRequiresLevel.Translate(Building_DreamumAltar.RequiredAttunementLevel.Named("LEVEL")),
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

            // 제단에 안치된 영원꽃은 화염 피해까지 모두 제단이 대신 받는다(제단은 공격으로 부서지지 않는다).
            // 일반 화분처럼 화염만 꽃에 들어가면 소이탄 한 번에 엔딩이 실패할 수 있다.
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
                // 엔딩이 묶인 영원꽃이 제단을 잃으면 경고한다. 엔딩은 제단 밖 그래픽으로 계속된다.
                if (wasEnshrined && (ViviEndingQuest.Current?.IsAnchor(this) ?? false))
                {
                    Messages.Message(LocalizeString_EndingAltar.VV_Message_AnchorEverflowerLeftAltar.Translate(), this, MessageTypeDefOf.NegativeEvent);
                }

                return;
            }

            base.Notify_ArcanePlantPotDespawned();
        }

        public void Notify_TeleportJobCompleted()
        {
            if (_reserveTeleportCell.HasValue)
            {
                if (!Teleport(_reserveTeleportCell.Value))
                {
                    TeleportRandomly();
                }

                _reserveTeleportCell = null;
            }
        }

        public bool TeleportRandomly()
        {
            // 무작위 순간이동으로는 영원꽃 제단에 안치되지 않게 제단 발판은 제외한다.
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

            // 순간이동은 디스폰 없이 Position만 바꾸므로, 바닐라의 Position 대입으로 갱신되지 않는 관련 정보를 직접 갱신한다.
            // (edifice 칸, 마법 식물 칸 사전, 체감 이동 비용, 빛 위치)
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

            // 떠난 자리에는 떠나기 전 모습(제단 위/밖)에 맞춘 이펙트를 남긴다.
            EverflowerComp.SpawnLevelEffect(previousPosition, map, wasEnshrined);

            // 도착한 자리가 제단 중앙이면 안치 이펙트·메시지가 대신 재생된다.
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

            return true;
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

        // 순간이동 지정 칸 보정: 영원꽃 제단 발판의 아무 칸이나 고르면 제단 중앙으로 맞춘다.
        private IntVec3 ResolveTeleportCell(IntVec3 cell, out Building_DreamumAltar altar)
        {
            altar = null;
            if (!cell.IsValid || Map == null || !cell.InBounds(Map)) { return cell; }

            altar = _mapComponent?.GetArcanePlantPot(cell) as Building_DreamumAltar;
            return altar != null ? altar.Position : cell;
        }

        private bool IsAltarCell(IntVec3 cell)
        {
            return _mapComponent?.GetArcanePlantPot(cell) is Building_DreamumAltar;
        }

        // 화분 위 식물이 떠난 칸의 edifice를 화분으로 되돌린다(EdificeGrid.DeRegister는 칸을 비워 버린다).
        // 다칸 화분은 통째로 등록되므로 다른 칸에 심긴 식물을 다시 등록해 덮어쓰지 않게 한다.
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
