using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaGrid : MapComponent, ICellBoolGiver, IDisposable
    {
        public const int RefreshManaInterval = 300;
        public const int DiffuseInterval = 1997;

        private const string ManaGridExposeName = "encodedManaGrid";

        [Unsaved]
        private NativeArray<float> _manaGrid;

        [Unsaved]
        private JobHandle _jobHandle;

        [Unsaved]
        private NativeArray<float> _tmpArray;

        [Unsaved]
        private CellBoolDrawer _cellBoolDrawer;

        [Unsaved]
        private HashSet<CompMana> _manaComps = new HashSet<CompMana>();

        public Color Color => new Color(1f, 1f, 1f);

        public float this[IntVec3 cell] => _manaGrid[map.cellIndices.CellToIndex(cell)];

        public ManaGrid(Map map) : base(map)
        {
            _manaGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);

            _cellBoolDrawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3634, 0.4f);

            map.events.BuildingSpawned += Notify_BuildingSpawned;
            map.events.BuildingDespawned += Notify_BuildingDespawned;
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                string encodedManaGrid = null;
                Scribe_Values.Look(ref encodedManaGrid, ManaGridExposeName);
                if (encodedManaGrid != null && encodedManaGrid.Length != 0)
                {
                    var bytes = IOUtility.DeserializeGZipBase64String(encodedManaGrid);
                    if (bytes.Length == map.cellIndices.NumGridCells * sizeof(float))
                    {
                        for (int i = 0; i < map.cellIndices.NumGridCells; ++i)
                        {
                            _manaGrid[i] = BitConverter.ToSingle(bytes, i * sizeof(float));
                        }
                    }
                    else
                    {
                        Log.Warning($"Failed to load mana grid since map size not matchded : {bytes.Length} != {map.cellIndices.NumGridCells * 4}");
                    }
                }
            }
            else if (Scribe.mode == LoadSaveMode.Saving)
            {
                var byteLength = map.cellIndices.NumGridCells * sizeof(float);
                var manaGridBytes = new byte[byteLength];
                unsafe
                {
                    var ptr = (IntPtr)_manaGrid.GetUnsafePtr();
                    Marshal.Copy(ptr, manaGridBytes, 0, byteLength);
                }

                var encodedManaGrid = IOUtility.SerializeGZipBase64String(manaGridBytes);
                Scribe_Values.Look(ref encodedManaGrid, ManaGridExposeName);
            }
        }

        public override void FinalizeInit()
        {
            _cellBoolDrawer.SetDirty();
        }

        public override void MapComponentUpdate()
        {
        }

        public override void MapComponentDraw()
        {
            // TODO: DEBUG
            if (!Find.ScreenshotModeHandler.Active)
            {
                _cellBoolDrawer.MarkForDraw();
            }

            _cellBoolDrawer.CellBoolDrawerUpdate();
        }

        public void MapComponentPreTick()
        {
            ScheduleDiffuseMana();
        }

        public override void MapComponentTick()
        {
            if (GenTicks.TicksGame % RefreshManaInterval == 0)
            {
                Refresh();
            }

            if (!_jobHandle.IsCompleted)
            {
                _jobHandle.Complete();
                _tmpArray.CopyTo(_manaGrid);
                _tmpArray.Dispose();

                _jobHandle = default;
                _tmpArray = default;
            }
        }

        public void AddMana(IntVec3 cell, float flux)
        {
            var idx = map.cellIndices.CellToIndex(cell);
            _manaGrid[idx] = Mathf.Clamp(_manaGrid[idx] + flux, 0f, 10000f);
        }

        public void Dispose()
        {
            _manaGrid.Dispose();
        }

        private void Refresh()
        {
            foreach (var comp in _manaComps)
            {
                comp.RefreshMana(this, RefreshManaInterval);
            }

            _cellBoolDrawer.SetDirty();
        }

        private void ScheduleDiffuseMana()
        {
            _tmpArray = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Temp);
            var job = new ManaDiffusionJob()
            {
                mapSize = map.Size.ToIntVec2,
                manaGrid = _manaGrid,
                outputGrid = _tmpArray,
            };

            _jobHandle = job.Schedule(map.cellIndices.NumGridCells, Mathf.Max(map.Size.x, map.Size.z));
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
            var value = _manaGrid[index];
            if (value < 500)
            {
                float normalized = value / 500.0f;
                color = Color.Lerp(new Color(0, 1, 1, 0f), new Color(0, 1, 1, 1), normalized);
            }
            else
            {
                float normalized = (value - 500) / 500.0f;
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
