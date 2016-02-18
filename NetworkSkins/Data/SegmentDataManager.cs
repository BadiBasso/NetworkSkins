﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColossalFramework.IO;
using ICities;
using NetworkSkins.Detour;
using NetworkSkins.Net;
using UnityEngine;

namespace NetworkSkins.Data
{
    public class SegmentDataManager : SerializableDataExtensionBase
    {
        private const string DataId = "NetworkSkins_SEGMENTS";
        private const uint DataVersion = 0;

        public static SegmentDataManager Instance;

        // stores all segment data instances that are currently in use (selected in options or applied to network segments)
        private readonly List<SegmentData> _usedSegmentData = new List<SegmentData>();

        // stores which options were selected by the user, per prefab
        private readonly Dictionary<NetInfo, SegmentData> _selectedSegmentOptions = new Dictionary<NetInfo, SegmentData>();

        // stores which options were selected by the user, per prefab
        private readonly Dictionary<NetInfo, SegmentData> _assetSegmentOptions = new Dictionary<NetInfo, SegmentData>();

        // stores which options should be used (user or asset mode)
        private bool _assetMode = false;

        // stores which data is applied to a network segment
        // this is an array field for high lookup performance
        public SegmentData[] SegmentToSegmentDataMap;

        public void SetAssetMode(bool value) // TODO make property
        {
            _assetMode = value;

            foreach (var entry in _assetSegmentOptions)
            {
                entry.Value.UsedCount--;
                DeleteIfNotInUse(entry.Value);
            }
            _assetSegmentOptions.Clear();
        }

        public bool AssetMode => _assetMode;

        public override void OnCreated(ISerializableData serializableData)
        {
            base.OnCreated(serializableData);
            Instance = this;

            RenderManagerDetour.EventUpdateDataPost += OnUpdateData;

            NetManagerDetour.EventSegmentCreate += OnSegmentCreate;
            NetManagerDetour.EventSegmentRelease += OnSegmentRelease;
            NetManagerDetour.EventSegmentTransferData += OnSegmentTransferData;
        }

        /// <summary>
        /// Like OnLevelLoaded, but executed earlier.
        /// </summary>
        /// <param name="mode"></param>
        public void OnUpdateData(SimulationManager.UpdateMode mode)
        {
            if (mode != SimulationManager.UpdateMode.LoadGame && mode != SimulationManager.UpdateMode.LoadMap) return;

            var data = serializableDataManager.LoadData(DataId);

            if (data != null)
            {
                try
                {
                    using (var stream = new MemoryStream(data))
                    {
                        SegmentToSegmentDataMap = DataSerializer.DeserializeArray<SegmentData>(stream,
                            DataSerializer.Mode.Memory);
                    }

                    Debug.LogFormat("Network Skins: Data loaded (Data length: {0})", data.Length);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (SegmentToSegmentDataMap == null)
            {
                SegmentToSegmentDataMap = new SegmentData[NetManager.instance.m_segments.m_size];
                Debug.Log("Network Skins: No data found!");
            }

            _usedSegmentData.AddRange(SegmentToSegmentDataMap.Distinct().Where(segmentData => segmentData != null));

            foreach (var segmentData in _usedSegmentData)
            {
                segmentData.UsedCount = SegmentToSegmentDataMap.Count(segmentData.Equals);
                segmentData.FindPrefabs(); // Find the prefabs for the loaded names
            }

            CleanupData();
        }

        public void OnLevelLoaded()
        {
            if (SegmentToSegmentDataMap == null)
            {
                SegmentToSegmentDataMap = new SegmentData[NetManager.instance.m_segments.m_size];
            }
        }

        public void OnLevelUnloaded()
        {
            _usedSegmentData.Clear();
            _selectedSegmentOptions.Clear();
            _assetSegmentOptions.Clear();
            SegmentToSegmentDataMap = null;
        }

        public override void OnReleased()
        {
            base.OnReleased();

            RenderManagerDetour.EventUpdateDataPost -= OnUpdateData;

            NetManagerDetour.EventSegmentCreate -= OnSegmentCreate;
            NetManagerDetour.EventSegmentCreate -= OnSegmentRelease;
            NetManagerDetour.EventSegmentTransferData -= OnSegmentTransferData;

            Instance = null;
        }

        public override void OnSaveData()
        {
            base.OnSaveData();

            var saveRequired = CleanupData();

            // check if data must be saved
            if (saveRequired)
            {
                byte[] data;

                using (var stream = new MemoryStream())
                {
                    DataSerializer.SerializeArray(stream, DataSerializer.Mode.Memory, DataVersion, SegmentToSegmentDataMap);
                    data = stream.ToArray();
                }

                serializableDataManager.SaveData(DataId, data);

                Debug.LogFormat("Network Skins: Data Saved (Data length: {0})", data.Length);
            }
            else
            {
                serializableDataManager.EraseData(DataId);

                Debug.Log("Network Skins: Data Cleared!");
            }
        }

        public SegmentData GetActiveSegmentData(NetInfo prefab)
        {
            var options = _assetMode ? _assetSegmentOptions : _selectedSegmentOptions;

            SegmentData segmentData;
            options.TryGetValue(prefab, out segmentData);
            return segmentData;
        }

        public void SetActiveSegmentData(NetInfo prefab, SegmentData segmentData)
        {
            var options = _assetMode ? _assetSegmentOptions : _selectedSegmentOptions;

            // Delete existing data if it is not used anymore
            var activeSegmentData = GetActiveSegmentData(prefab);
            if (activeSegmentData != null)
            {
                options.Remove(prefab);
                activeSegmentData.UsedCount--;
                DeleteIfNotInUse(activeSegmentData);
            }

            // No new data? Stop here
            if (segmentData == null || segmentData.Features == SegmentData.FeatureFlags.None) return;

            // Check if there is an equal data object
            var equalSegmentData = _usedSegmentData.FirstOrDefault(segmentData.Equals);
            if (equalSegmentData != null)
            {
                // yes? use that, discard the one we created
                options[prefab] = equalSegmentData;
                equalSegmentData.UsedCount++;
            }
            else
            {
                // no? Use the one we got
                _usedSegmentData.Add(segmentData);
                options[prefab] = segmentData;
                segmentData.UsedCount++;
            }

        }

        public void OnSegmentCreate(ushort segment)
        {
            if (SegmentToSegmentDataMap == null) return;

            var prefab = NetManager.instance.m_segments.m_buffer[segment].Info;
            var segmentData = GetActiveSegmentData(prefab);
            SegmentToSegmentDataMap[segment] = segmentData;

            if (segmentData != null) segmentData.UsedCount++;

            //Debug.LogFormat("Segment {0} created!", segment);
        }

        public void OnSegmentRelease(ushort segment)
        {
            if (SegmentToSegmentDataMap == null) return;

            var segmentData = SegmentToSegmentDataMap[segment];
            if (segmentData != null)
            {
                segmentData.UsedCount--;
                DeleteIfNotInUse(segmentData);
            }

            SegmentToSegmentDataMap[segment] = null;

            //Debug.LogFormat("Segment {0} released!", segment);
        }

        public void OnSegmentTransferData(ushort oldSegment, ushort newSegment)
        {
            if (SegmentToSegmentDataMap == null) return;

            var segmentData = SegmentToSegmentDataMap[oldSegment];
            if (segmentData != null) segmentData.UsedCount++;

            SegmentToSegmentDataMap[newSegment] = SegmentToSegmentDataMap[oldSegment];

            //Debug.LogFormat("Transfer data from {0} to {1}!", oldSegment, newSegment);
        }

        /// <summary>
        /// Validates the data. Removes data which is no longer used (should never happen).
        /// </summary>
        /// <returns>If there is data applied to any segment</returns>
        private bool CleanupData()
        {
            var result = false;

            if(_usedSegmentData != null)
            foreach (var segmentData in _usedSegmentData.ToArray())
            {
                var segmentMapUsedCount = SegmentToSegmentDataMap.Count(segmentData.Equals);
                var segmentOptionsUsedCount = _selectedSegmentOptions.Values.Count(segmentData.Equals);
                var assetOptionsUsedCount = _assetSegmentOptions.Values.Count(segmentData.Equals);
                var calculatedUsedCount = segmentMapUsedCount + segmentOptionsUsedCount + assetOptionsUsedCount;

                if (segmentMapUsedCount > 0) result = true;

                if (segmentData.UsedCount != calculatedUsedCount)
                {
                    Debug.LogErrorFormat("Network Skins: Incorrect usedCount detected, should be {0} ({1})", calculatedUsedCount, segmentData);

                    segmentData.UsedCount = calculatedUsedCount;
                    DeleteIfNotInUse(segmentData);
                }
            }

            // check if data is applied to segments which no longer exist
            if(SegmentToSegmentDataMap != null)
            for (var i = 0; i <SegmentToSegmentDataMap.Length; i++)
            {
                var segmentData = SegmentToSegmentDataMap[i];

                if (segmentData != null && NetManager.instance.m_segments.m_buffer[i].m_flags == NetSegment.Flags.None)
                {
                    Debug.LogErrorFormat("Network Skins: Data was applied to released segment {0}", segmentData);

                    SegmentToSegmentDataMap[i] = null;
                    segmentData.UsedCount--;
                    DeleteIfNotInUse(segmentData);
                }
            }

            return result;
        }

        private void DeleteIfNotInUse(SegmentData segmentData)
        {
            if (segmentData.UsedCount <= 0)
            {
                _usedSegmentData.Remove(segmentData);
            }
        }
    }
}
