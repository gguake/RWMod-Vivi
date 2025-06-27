using LudeonTK;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class EnvironmentManaGrid : MapComponent, ICellBoolGiver, IDisposable
    {
        public const float EnvironmentManaMax = 1000f;

        public const int RefreshManaInterval = 250;
        public const int DiffuseInterval = 1129;

        private NativeArray<float> _manaReserveGrid;
        private NativeArray<float> _manaGrid;
        private NativeArray<float> _tmpGrid;

        private bool _shouldUpdate;
        private bool _updateJobStart;
        private bool _diffusionJobStart;
        private JobHandle _updateJobHandle;
        private JobHandle _diffusionJobHandle;

        private CellBoolDrawer _cellBoolDrawer;
        private bool _drawManaOverlay;

        private HashSet<CompMana> _manaComps = new HashSet<CompMana>();

        public Color Color => new Color(1f, 1f, 1f);

        public float this[IntVec3 cell]
        {
            get
            {
                var idx = map.cellIndices.CellToIndex(cell);
                return _manaReserveGrid[idx] + _manaGrid[idx];
            }
        }

        public EnvironmentManaGrid(Map map) : base(map)
        {
            _manaReserveGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);
            _manaGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);
            _tmpGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);

            _cellBoolDrawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3634, 0.7f);

            map.events.BuildingSpawned += Notify_BuildingSpawned;
            map.events.BuildingDespawned += Notify_BuildingDespawned;
        }

        public override void ExposeData()
        {
            if (_updateJobStart) { CompleteUpdateManaJob(); }
            if (_diffusionJobStart) { CompleteDiffuseManaJob(); }

            IOUtility.ScribeNativeFloatArray(ref _manaReserveGrid, "encodedManaReserveGrid");
            IOUtility.ScribeNativeFloatArray(ref _manaGrid, "encodedManaGrid");

            Scribe_Values.Look(ref _shouldUpdate, "shouldUpdateMana");
        }

        public override void FinalizeInit()
        {
            _tmpGrid.Clear();

            _cellBoolDrawer.SetDirty();
        }

        public override void MapComponentDraw()
        {
            if (!Find.ScreenshotModeHandler.Active)
            {
                if (_drawManaOverlay)
                {
                    _cellBoolDrawer.MarkForDraw();
                    _drawManaOverlay = false;
                }
            }

            _cellBoolDrawer.CellBoolDrawerUpdate();
        }

        public void MapComponentPreTick()
        {
            if (_updateJobStart)
            {
                CompleteUpdateManaJob();
            }

            var ticksGame = GenTicks.TicksGame;
            var a = ticksGame % DiffuseInterval;
            var b = ticksGame % RefreshManaInterval;

            if ((a == 0 && b != 0) ||
                (a == 1 && b == 1))
            {
                ScheduleDiffuseManaJob();
            }
        }

        public override void MapComponentTick()
        {
            if (_diffusionJobStart)
            {
                CompleteDiffuseManaJob();
            }

            if (GenTicks.TicksGame % RefreshManaInterval == 0)
            {
                Refresh();
            }

            if (_shouldUpdate)
            {
                _shouldUpdate = false;

                ScheduleUpdateManaJob();
            }
        }

        public void MarkForDrawOverlay()
        {
            _drawManaOverlay = true;
        }

        public void ChangeEnvironmentMana(IntVec3 cell, float flux, bool direct = false)
        {
            var idx = map.cellIndices.CellToIndex(cell);
            if (direct)
            {
                _manaGrid[idx] = Mathf.Clamp(_manaGrid[idx] + flux, 0f, EnvironmentManaMax);
            }
            else
            {
                _manaReserveGrid[idx] = flux;
                _shouldUpdate = true;
            }
        }

        public void Dispose()
        {
            _tmpGrid.Dispose();
            _manaGrid.Dispose();
            _manaReserveGrid.Dispose();
        }

        private void Refresh()
        {
            foreach (var comp in _manaComps)
            {
                comp.RefreshMana(this, RefreshManaInterval);
            }

            _cellBoolDrawer.SetDirty();
        }

        private void ScheduleUpdateManaJob()
        {
            if (_updateJobStart) { return; }

            var job = new ManaUpdateJob()
            {
                manaReserveGrid = _manaReserveGrid,
                manaGrid = _manaGrid,
                outputGrid = _tmpGrid,
            };

            _updateJobHandle = job.ScheduleBatch(map.cellIndices.NumGridCells, map.Size.x);
            _updateJobStart = true;
        }

        private void ScheduleDiffuseManaJob()
        {
            if (_diffusionJobStart) { return; }

            var job = new ManaDiffusionJob()
            {
                width = map.Size.x,
                height = map.Size.z,
                manaGrid = _manaGrid,
                outputGrid = _tmpGrid,
            };

            _diffusionJobHandle = job.Schedule(map.cellIndices.NumGridCells, map.Size.x);
            _diffusionJobStart = true;
        }

        private void CompleteUpdateManaJob()
        {
            _updateJobHandle.Complete();

            _tmpGrid.CopyTo(_manaGrid);
            _manaReserveGrid.Clear();

            _updateJobStart = false;
        }

        private void CompleteDiffuseManaJob()
        {
            _diffusionJobHandle.Complete();

            _tmpGrid.CopyTo(_manaGrid);

            _diffusionJobStart = false;
        }

        private void Notify_BuildingSpawned(Building building)
        {
            var comp = building.TryGetComp<CompMana>();
            if (comp != null)
            {
                _manaComps.Add(comp);

                _cellBoolDrawer.SetDirty();
            }
        }

        private void Notify_BuildingDespawned(Building building)
        {
            var comp = building.TryGetComp<CompMana>();
            if (comp != null)
            {
                _manaComps.Remove(comp);

                _cellBoolDrawer.SetDirty();
            }
        }

        public bool GetCellBool(int index)
        {
            return _manaGrid[index] > 5f;
        }

        public Color GetCellExtraColor(int index)
        {
            Color color;
            var value = _manaGrid[index] + _manaReserveGrid[index];
            if (value < 250)
            {
                float normalized = value / 250.0f;
                color = Color.Lerp(new Color(0, 1, 1, 0.1f), new Color(0, 1, 1, 1), normalized);
            }
            else
            {
                float normalized = (value - 250) / 500.0f;
                color = Color.Lerp(new Color(0, 1, 1, 1), new Color(0, 0, 1, 1), normalized);
            }

            color.r = Mathf.Round(color.r / 0.05f) * 0.05f;
            color.g = Mathf.Round(color.g / 0.05f) * 0.05f;
            color.b = Mathf.Round(color.b / 0.05f) * 0.05f;
            color.a = Mathf.Round(color.a / 0.05f) * 0.05f;
            return color;
        }
    }
}
