using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using HarmonyLib;
using ExitGames.Client.Photon;
using Photon.Realtime;

namespace SignalMenu.SignalSafety
{
    public static class NetworkDebug
    {
        private static int _eventCount = 0;
        private static float _resetTime = 0f;
        private const float RESET_INTERVAL = 1f;
        private const int PHOTON_LIMIT = 500;
        private const int WARNING_THRESHOLD = 400;

        private static Dictionary<string, int> _eventSources = new Dictionary<string, int>();

        public static int EventsThisSecond => _eventCount;
        public static int EventLimit => PHOTON_LIMIT;

        public static void Reset()
        {
            if (Time.time > _resetTime)
            {
                if (SafetyConfig.VerboseNetworkLogging && _eventCount > 50)
                {
                    Plugin.Instance?.Log($"[NetworkDebug] Events last second: {_eventCount}/{PHOTON_LIMIT}");
                    
                    foreach (var kvp in _eventSources)
                    {
                        if (kvp.Value > 5)
                            Plugin.Instance?.Log($"[NetworkDebug]   {kvp.Key}: {kvp.Value} events");
                    }
                }
                
                _eventCount = 0;
                _eventSources.Clear();
                _resetTime = Time.time + RESET_INTERVAL;
                SafetyConfig.NetworkEventCount = 0;
                SafetyConfig.NetworkEventResetTime = _resetTime;
            }
        }

        public static bool OnEventSend(byte eventCode)
        {
            Reset();
            _eventCount++;
            SafetyConfig.NetworkEventCount = _eventCount;

            if (SafetyConfig.VerboseNetworkLogging)
            {
                string source = GetCallerInfo();
                if (!_eventSources.ContainsKey(source))
                    _eventSources[source] = 0;
                _eventSources[source]++;

                if (_eventCount == WARNING_THRESHOLD)
                {
                    Plugin.Instance?.Log($"[NetworkDebug] WARNING: Approaching Photon limit! {_eventCount}/{PHOTON_LIMIT} events");
                    Plugin.Instance?.Log($"[NetworkDebug] Top source: {source}");
                }

                if (_eventCount >= PHOTON_LIMIT - 10)
                {
                    Plugin.Instance?.Log($"[NetworkDebug] CRITICAL: {_eventCount}/{PHOTON_LIMIT} - Blocking event from {source}");
                    return false;
                }
            }

            if (_eventCount >= PHOTON_LIMIT - 5)
            {
                Plugin.Instance?.Log($"[NetworkDebug] BLOCKED event - at {_eventCount}/{PHOTON_LIMIT} limit");
                return false;
            }

            return true;
        }

        private static string GetCallerInfo()
        {
            try
            {
                var st = new StackTrace(true);
                for (int i = 3; i < st.FrameCount && i < 15; i++)
                {
                    var frame = st.GetFrame(i);
                    var method = frame?.GetMethod();
                    if (method == null) continue;

                    string typeName = method.DeclaringType?.Name ?? "Unknown";
                    string methodName = method.Name;

                    if (typeName.Contains("Photon") || typeName.Contains("LoadBalancing") || 
                        typeName.Contains("Harmony") || typeName.Contains("NetworkDebug"))
                        continue;

                    if (typeName.Contains("Overpowered") || typeName.Contains("Fun") || 
                        typeName.Contains("Movement") || typeName.Contains("Visuals") ||
                        typeName.Contains("Safety") || typeName.Contains("Settings") ||
                        typeName.Contains("Advantages") || typeName.Contains("Experimental"))
                    {
                        return $"{typeName}.{methodName}";
                    }
                }
                return "Unknown";
            }
            catch
            {
                return "Error";
            }
        }

        public static string GetStatus()
        {
            Reset();
            return $"{_eventCount}/{PHOTON_LIMIT}";
        }
    }

    [HarmonyPatch(typeof(LoadBalancingPeer), nameof(LoadBalancingPeer.OpRaiseEvent))]
    public class PatchOpRaiseEvent
    {
        [HarmonyPrefix]
        public static bool Prefix(byte eventCode)
        {
            return NetworkDebug.OnEventSend(eventCode);
        }
    }
}
