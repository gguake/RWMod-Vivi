using LudeonTK;
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Verse;
using Waylith.Jobs;

namespace VVRace
{
    public class ManaMapComponent : MapComponent, ICellBoolGiver, IDisposable
    {
        private VVRaceModSettings _modSettingCache = LoadedModManager.GetMod<VVRaceMod>().GetSettings<VVRaceModSettings>();

        public const float ViviFlowerChance = 0.04f;
        public const float EnvironmentManaMax = 1000f;

        public const int DiffuseInterval = 1129;
        public const int RefreshManaInterval = 250;

        public const float DefaultManaVeinPerDay = 10000f;

        private NativeArray<float> _manaReserveGrid;
        private NativeArray<float> _manaGrid;
        private NativeArray<float> _tmpGrid;
        private readonly HashSet<int> _dirtyManaCells = new HashSet<int>();

        private bool _checkFlowerCells;

        private bool _diffusionJobStart;
        private int _diffusionJobHandle;

        private CellBoolDrawer _cellBoolDrawer;
        private bool _drawManaOverlay;
        public bool manaOverlaySetting;

        // 6단계 마나 바닥 전용 오버레이: 맵 전체가 같은 색이므로 칸 오버레이(250틱마다 재생성)와 나눠 그리고,
        // 바닥이 바뀔 때와 그린 칸의 안개가 실제로 바뀔 때만 다시 만든다.
        // 바닐라는 문을 지날 때마다 이미 걷힌 칸에도 안개 변경 이벤트를 보내므로, 마지막으로 그릴 때 칠한 칸을 기억해 두고 비교한다.
        private CellBoolDrawer _manaFloorDrawer;
        private Color _manaFloorColor;
        private bool[] _manaFloorDrawnCells;

        // 마나 영맥(오디세이 랜드마크): 게임 내내 환경 마나를 방출하는 지점
        private List<IntVec3> _manaVeins = new List<IntVec3>();
        public IReadOnlyList<IntVec3> ManaVeins => _manaVeins;

        // 영원꽃 제단(엔딩): 안치된 5/6단계 영원꽃은 제단 발판 전체에서 추가 마나를 방출하고,
        // 6단계 영원꽃이 있는 맵은 모든 칸의 환경 마나가 바닥값 아래로 내려가지 않는다(조회 시 적용하는 바닥값이므로 흡수해도 줄지 않음).
        // 이 상태는 저장하지 않는다: 불러오기에서는 1x1 영원꽃이 5x5 제단보다 먼저 스폰되므로, FinalizeInit과 250틱마다
        // 맵의 제단·영원꽃 상태를 바탕으로 다시 계산한다(레벨이나 안치 상태가 바뀌면 CompEverflower도 즉시 갱신을 요청한다).
        public const float EverflowerAltarManaPerDayLevel5 = 6000f;
        public const float EverflowerAltarManaPerDayLevel6 = 12000f;
        public const float EverflowerLevel6ManaFloor = 300f;
        public const int EverflowerAltarEmitRadius = 2;

        private float _altarManaPerDay;
        private IntVec3 _altarCenter = IntVec3.Invalid;
        private float _manaFloor;

        public float ManaFloor => _manaFloor;

        public Color Color => new Color(1f, 1f, 1f);

        public float this[IntVec3 cell]
        {
            get
            {
                var idx = map.cellIndices.CellToIndex(cell);
                return Mathf.Max(_manaFloor, Mathf.Clamp(_manaReserveGrid[idx] + _manaGrid[idx], 0f, EnvironmentManaMax));
            }
        }

        public ManaMapComponent(Map map) : base(map)
        {
            _manaReserveGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);
            _manaGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);
            _tmpGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);

            _cellBoolDrawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3634, _modSettingCache.manaGridOpacity);
            _manaFloorDrawer = CreateManaFloorDrawer();

            map.events.RoofChanged += Notify_RoofChanged;
            map.events.CellFogChanged += Notify_CellFogChanged;
            map.events.MapFogged += Notify_MapFogged;
        }

        public override void ExposeData()
        {
            if (_diffusionJobStart) { CompleteDiffuseManaJob(); }
            if (_dirtyManaCells.Count > 0) { ApplyDirtyReservedMana(); }

            IOUtility.ScribeNativeFloatArray(ref _manaReserveGrid, "encodedManaReserveGrid");
            IOUtility.ScribeNativeFloatArray(ref _manaGrid, "encodedManaGrid");

            Scribe_Values.Look(ref manaOverlaySetting, "manaOverlaySetting");

            Scribe_Collections.Look(ref _manaVeins, "manaVeins", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && _manaVeins == null)
            {
                _manaVeins = new List<IntVec3>();
            }

        }

        public override void FinalizeInit()
        {
            _tmpGrid.Clear();
            _cellBoolDrawer.SetDirty();
            _manaFloorDrawer.SetDirty();

            LoadedModManager.GetMod<VVRaceMod>().OnWriteSettings += Notify_ModSettingChanged;

            ApplyAllReservedMana();
            RefreshEverflowerAltarState();
        }

        public override void MapComponentDraw()
        {
            if (!Find.ScreenshotModeHandler.Active)
            {
                _drawManaOverlay = _drawManaOverlay || manaOverlaySetting;
                if (_drawManaOverlay)
                {
                    if (_manaFloor > 0f)
                    {
                        _manaFloorDrawer.MarkForDraw();
                    }

                    _cellBoolDrawer.MarkForDraw();
                    _drawManaOverlay = false;
                }
            }

            _manaFloorDrawer.CellBoolDrawerUpdate();
            _cellBoolDrawer.CellBoolDrawerUpdate();
        }

        public void MapComponentPreTick()
        {
            var ticksGame = GenTicks.TicksGame;
            var a = ticksGame % DiffuseInterval;
            var b = ticksGame % RefreshManaInterval;

            if ((a == 0 && b != 0) ||
                (a == 1 && b == 1))
            {
                ScheduleDiffuseManaJob();
            }
        }

        private List<(int idx, float mana)> _tmpFlowerCellCandidates = new List<(int idx, float mana)>();
        public override void MapComponentTick()
        {
            if (_diffusionJobStart)
            {
                CompleteDiffuseManaJob();

                if (_checkFlowerCells)
                {
                    _tmpFlowerCellCandidates.Clear();

                    var sum = 0f;
                    for (int i = 0; i < _manaGrid.Length; ++i)
                    {
                        var v = _manaGrid[i];
                        sum += v;
                        if (v >= 100f) { _tmpFlowerCellCandidates.Add((i, v)); }
                    }

                    // 마나 밀도가 높은 셀부터 스폰. 500 이상은 동순위 취급
                    _tmpFlowerCellCandidates.Sort((x, y) => Mathf.Min(y.mana, 500f).CompareTo(Mathf.Min(x.mana, 500f)));

                    var thingDef = ArcanePlantUtility.ViviFlowerDefs.RandomElement();
                    int flowerCount = (int)Mathf.Clamp(sum / 50000f, 1, 20) + Rand.Range(1, 5);
                    foreach (var (cellIndex, _) in _tmpFlowerCellCandidates)
                    {
                        if (flowerCount <= 0) { break; }

                        var c = map.cellIndices.IndexToCell(cellIndex);
                        if (ArcanePlantUtility.TrySpawnViviFlower(map, c, out _, flowerDef: thingDef))
                        {
                            flowerCount--;
                            thingDef = ArcanePlantUtility.ViviFlowerDefs.RandomElement();
                        }
                    }

                    _checkFlowerCells = false;
                }
            }

            if (GenTicks.TicksGame % RefreshManaInterval == 0)
            {
                EmitManaVeins(RefreshManaInterval);
                RefreshEverflowerAltarState();
                EmitEverflowerAltarMana(RefreshManaInterval);

                var gameComponentMana = Current.Game.GetComponent<GameComponent_Mana>();
                if (gameComponentMana != null)
                {
                    foreach (var comp in gameComponentMana.AllManaComps)
                    {
                        var root = comp.parent.SpawnedParentOrMe;
                        if (root != null && root.Map == map)
                        {
                            comp.RefreshMana(this, root.Position, RefreshManaInterval);
                        }
                    }

                    _cellBoolDrawer.SetDirty();
                }
            }

            if (_dirtyManaCells.Count > 0)
            {
                ApplyDirtyReservedMana();
            }
        }

        public void MarkForDrawOverlay()
        {
            if (VVResearchProjectDefOf.VV_ArcaneBotany.IsFinished)
            {
                _drawManaOverlay = true;
            }
        }

        public void ChangeEnvironmentMana(IntVec3 cell, float flux)
        {
            var idx = map.cellIndices.CellToIndex(cell);
            if (_diffusionJobStart)
            {
                _manaReserveGrid[idx] += flux;
            }
            else
            {
                var currentMana = _manaGrid[idx] + _manaReserveGrid[idx];
                var updatedMana = Mathf.Clamp(currentMana + flux, 0f, EnvironmentManaMax);
                _manaReserveGrid[idx] = updatedMana - _manaGrid[idx];
            }
            _dirtyManaCells.Add(idx);
        }

        public void AddManaVein(IntVec3 cell)
        {
            if (!_manaVeins.Contains(cell))
            {
                _manaVeins.Add(cell);
            }
        }

        private void EmitManaVeins(int ticks)
        {
            if (_manaVeins.Count == 0) { return; }

            var manaPerDay = VVTileMutatorDefOf.VV_ManaVein?.GetModExtension<ManaVeinExtension>()?.manaPerDay ?? DefaultManaVeinPerDay;

            // 칸당 상한(EnvironmentManaMax)에 막혀 버려지지 않도록 중심 3x3에 나눠 넣는다.
            var manaPerCell = manaPerDay / 60000f * ticks / 9f;
            foreach (var vein in _manaVeins)
            {
                foreach (var cell in CellRect.CenteredOn(vein, 1))
                {
                    if (cell.InBounds(map))
                    {
                        ChangeEnvironmentMana(cell, manaPerCell);
                    }
                }
            }
        }

        // 맵의 영원꽃을 훑어 제단 상태를 다시 계산한다. 영원꽃은 맵에 여러 송이 있을 수 있으므로 꽃이 직접 값을 넣으면
        // 서로 덮어쓰게 된다. 그래서 CompEverflower는 레벨 변경·안치 변경·스폰/디스폰 때 이 함수만 부른다.
        public void RefreshEverflowerAltarState()
        {
            var center = IntVec3.Invalid;
            var altarLevel = 0;
            var mapLevel = 0;

            var everflowers = map.listerThings.ThingsOfDef(VVThingDefOf.VV_Everflower);
            for (int i = 0; i < everflowers.Count; ++i)
            {
                if (!(everflowers[i] is ArcanePlant_Everflower everflower) || !everflower.Spawned || everflower.Map != map) { continue; }

                var level = everflower.AttunementLevel;
                mapLevel = Mathf.Max(mapLevel, level);

                if (level > altarLevel && everflower.EverflowerComp.IsEnshrined)
                {
                    center = everflower.Position;
                    altarLevel = level;
                }
            }

            SetEverflowerAltarState(center, altarLevel, mapLevel);
        }

        private void SetEverflowerAltarState(IntVec3 center, int altarLevel, int mapLevel)
        {
            var manaPerDay = 0f;
            if (center.IsValid && center.InBounds(map))
            {
                if (altarLevel >= 6) { manaPerDay = EverflowerAltarManaPerDayLevel6; }
                else if (altarLevel >= 5) { manaPerDay = EverflowerAltarManaPerDayLevel5; }
            }

            _altarManaPerDay = manaPerDay;
            _altarCenter = manaPerDay > 0f ? center : IntVec3.Invalid;

            var manaFloor = mapLevel >= 6 ? EverflowerLevel6ManaFloor : 0f;
            if (_manaFloor != manaFloor)
            {
                _manaFloor = manaFloor;
                _manaFloorColor = GetManaOverlayColor(manaFloor);
                _cellBoolDrawer.SetDirty();
                _manaFloorDrawer.SetDirty();
            }
        }

        private void EmitEverflowerAltarMana(int ticks)
        {
            if (_altarManaPerDay <= 0f || !_altarCenter.IsValid) { return; }

            // 한 점에 넣으면 칸당 상한에 막혀 버려지므로 제단 5x5 발판에 고르게 나눠 넣는다.
            var rect = CellRect.CenteredOn(_altarCenter, EverflowerAltarEmitRadius).ClipInsideMap(map);
            if (rect.Area <= 0) { return; }

            var manaPerCell = _altarManaPerDay / 60000f * ticks / rect.Area;
            foreach (var cell in rect)
            {
                ChangeEnvironmentMana(cell, manaPerCell);
            }
        }

        public void Dispose()
        {
            LoadedModManager.GetMod<VVRaceMod>().OnWriteSettings -= Notify_ModSettingChanged;

            if (!WaylithJobRuntime.IsCompleted(_diffusionJobHandle)) { WaylithJobRuntime.Complete(_diffusionJobHandle); }

            _tmpGrid.Dispose();
            _manaGrid.Dispose();
            _manaReserveGrid.Dispose();
        }

        private void Notify_ModSettingChanged()
        {
            _cellBoolDrawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3634, _modSettingCache.manaGridOpacity);
            _manaFloorDrawer = CreateManaFloorDrawer();
        }

        // 칸 오버레이보다 먼저(renderQueue 3633) 그려 마나가 바닥값을 넘는 칸의 오버레이가 그 위에 겹쳐 보이게 한다.
        private CellBoolDrawer CreateManaFloorDrawer()
        {
            return new CellBoolDrawer(GetManaFloorCellBool, () => Color, GetManaFloorCellColor, map.Size.x, map.Size.z, 3633, _modSettingCache.manaGridOpacity);
        }

        private void Notify_RoofChanged(IntVec3 c)
        {
            foreach (var thing in c.GetThingList(map))
            {
                var comp = thing.TryGetComp<CompManaGlower>();
                if (comp != null)
                {
                    comp.UpdateLit(map);
                }
            }
        }

        private void ScheduleDiffuseManaJob()
        {
            if (_diffusionJobStart) { return; }

            _checkFlowerCells = Rand.Chance(ViviFlowerChance);

            var job = new ManaDiffusionJob()
            {
                w = map.Size.x,
                h = map.Size.z,
                manaGrid = _manaGrid,
                outputGrid = _tmpGrid,
            };

            _diffusionJobHandle = WaylithJobRuntime.ScheduleFor(job, map.Size.z, 1);
            _diffusionJobStart = true;
        }

        private void CompleteDiffuseManaJob()
        {
            WaylithJobRuntime.Complete(_diffusionJobHandle);

            var previousManaGrid = _manaGrid;
            _manaGrid = _tmpGrid;
            _tmpGrid = previousManaGrid;

            ApplyDirtyReservedMana();
            _diffusionJobStart = false;
        }

        private void ApplyDirtyReservedMana()
        {
            foreach (var idx in _dirtyManaCells)
            {
                _manaGrid[idx] = Mathf.Clamp(_manaGrid[idx] + _manaReserveGrid[idx], 0f, EnvironmentManaMax);
                _manaReserveGrid[idx] = 0f;
            }

            _dirtyManaCells.Clear();
        }

        private void ApplyAllReservedMana()
        {
            for (int i = 0; i < _manaReserveGrid.Length; ++i)
            {
                var reservedMana = _manaReserveGrid[i];
                if (reservedMana == 0f) { continue; }

                _manaGrid[i] = Mathf.Clamp(_manaGrid[i] + reservedMana, 0f, EnvironmentManaMax);
                _manaReserveGrid[i] = 0f;
            }

            _dirtyManaCells.Clear();
        }

        // 칸 오버레이는 6단계 마나 바닥을 넘는 칸만 그린다. 바닥(맵 전체 균일)은 _manaFloorDrawer가 따로 그린다.
        // 바닥까지 여기서 칠하면 오버레이가 켜져 있는 동안 마나 갱신(250틱)마다 맵 전체 칸의 메시를 다시 만든다.
        public bool GetCellBool(int index)
        {
            return _manaGrid[index] > Mathf.Max(5f, _manaFloor);
        }

        public Color GetCellExtraColor(int index)
        {
            if (map.fogGrid.IsFogged(index)) { return new Color(0f, 0f, 0f, 0f); }

            return GetManaOverlayColor(Mathf.Max(_manaFloor, _manaGrid[index] + _manaReserveGrid[index]));
        }

        // 메시를 다시 만들 때 칸마다 한 번씩 불린다. 칠한 칸을 기억해 두었다가 안개 변경이 실제로 그림을 바꾸는지 비교한다.
        // (바닥 오버레이는 바닥값이 있을 때만 그리므로 엔딩 전에는 배열을 만들지 않는다)
        private bool GetManaFloorCellBool(int index)
        {
            var draw = _manaFloor > 0f && !map.fogGrid.IsFogged(index);
            if (_manaFloorDrawnCells == null)
            {
                _manaFloorDrawnCells = new bool[map.cellIndices.NumGridCells];
            }

            _manaFloorDrawnCells[index] = draw;
            return draw;
        }

        // 바닥값이 바뀔 때 한 번만 계산해 둔 색을 쓴다(맵 전체 칸이 같은 색).
        private Color GetManaFloorCellColor(int index)
        {
            return _manaFloorColor;
        }

        // 바닐라는 이미 걷힌 칸에도 안개 변경 이벤트를 보내므로(문을 지날 때 등), 마지막으로 그린 모습과 달라질 때만 바닥 오버레이를 다시 만든다.
        private void Notify_CellFogChanged(IntVec3 c, bool fogged)
        {
            if (_manaFloor <= 0f || _manaFloorDrawnCells == null || !c.InBounds(map)) { return; }

            if (IsManaFloorDrawnDifferently(c, fogged))
            {
                _manaFloorDrawer.SetDirty();
                return;
            }

            // 칸을 꽉 채우는 여러 칸짜리 건물은 한 칸의 이벤트로 모든 칸의 안개가 함께 걷힌다.
            if (!fogged)
            {
                var things = c.GetThingList(map);
                for (int i = 0; i < things.Count; ++i)
                {
                    if (things[i].def.Fillage != FillCategory.Full) { continue; }

                    foreach (var cell in things[i].OccupiedRect())
                    {
                        if (cell.InBounds(map) && IsManaFloorDrawnDifferently(cell, false))
                        {
                            _manaFloorDrawer.SetDirty();
                            return;
                        }
                    }
                }
            }
        }

        // 칠한 칸(안개 없음)이 안개로 덮였거나, 칠하지 않은 칸(안개)이 걷혔으면 그림이 바뀐다.
        private bool IsManaFloorDrawnDifferently(IntVec3 cell, bool fogged)
        {
            return _manaFloorDrawnCells[map.cellIndices.CellToIndex(cell)] == fogged;
        }

        private void Notify_MapFogged()
        {
            _manaFloorDrawer.SetDirty();
        }

        private static Color GetManaOverlayColor(float value)
        {
            Color color;
            if (value <= 100)
            {
                float normalized = value / 100.0f;
                color = Color.Lerp(new Color(0, 1, 1, 0.1f), new Color(0, 1, 1, 1), normalized);
            }
            else if (value <= 500)
            {
                float normalized = (value - 100) / 400.0f;
                color = Color.Lerp(new Color(0, 1, 1, 1), new Color(0, 0, 1, 1), normalized);
            }
            else
            {
                float normalized = (value - 500) / 500.0f;
                color = Color.Lerp(new Color(0, 0, 1, 1), new Color(0, 1, 0, 1), normalized);
            }

            color.r = Mathf.Round(color.r / 0.05f) * 0.05f;
            color.g = Mathf.Round(color.g / 0.05f) * 0.05f;
            color.b = Mathf.Round(color.b / 0.05f) * 0.05f;
            color.a = Mathf.Round(color.a / 0.05f) * 0.05f;
            return color;
        }
    }
}
