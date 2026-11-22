using System.Collections.Generic;
using Tl.Ui;
using Tl.Unity.Editor;
using Unity.Entities;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace TlUi.Editor
{
    [InitializeOnLoad]
    [NoAutoStaticsCleanup]
    public static class TlUiScrubPreview
    {
        sealed class Recording
        {
            public long Version;
            public int ShownTick = -1;
            public readonly List<string> TargetNames = new List<string>();
            public readonly List<UiSwitches> Switches = new List<UiSwitches>();
            public readonly List<UiChannels> Channels = new List<UiChannels>();

            public void Keep(EntityManager entities, Entity director, uint tick)
            {
                var switches = entities.HasComponent<UiSwitches>(director) ? entities.GetComponentData<UiSwitches>(director) : default;
                var channels = entities.HasComponent<UiChannels>(director) ? entities.GetComponentData<UiChannels>(director) : default;
                while (Switches.Count <= tick)
                {
                    Switches.Add(switches);
                    Channels.Add(channels);
                }
            }
        }

        static readonly Dictionary<PlayableDirector, Recording> s_Recordings = new Dictionary<PlayableDirector, Recording>();
        static readonly List<TlUiPanel> s_Previewing = new List<TlUiPanel>();

        static TlUiScrubPreview()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;
            TlPreview.Evaluated += Show;
            EditorApplication.update += StopWhenPreviewEnds;
            EditorApplication.playModeStateChanged += StopOnPlayModeChange;
            AssemblyReloadEvents.beforeAssemblyReload += Release;
        }

        public static int PreviewingPanels => s_Previewing.Count;

        static void Release()
        {
            StopAll();
            s_Recordings.Clear();
            TlPreview.Evaluated -= Show;
            EditorApplication.update -= StopWhenPreviewEnds;
            EditorApplication.playModeStateChanged -= StopOnPlayModeChange;
            AssemblyReloadEvents.beforeAssemblyReload -= Release;
        }

        static void StopOnPlayModeChange(PlayModeStateChange change)
        {
            StopAll();
            s_Recordings.Clear();
        }

        static void StopWhenPreviewEnds()
        {
            if (s_Previewing.Count == 0 && s_Recordings.Count == 0)
                return;
            if (!AnimationMode.InAnimationMode() || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                StopAll();
                s_Recordings.Clear();
            }
        }

        public static void StopAll()
        {
            foreach (var panel in s_Previewing)
                if (panel != null)
                    panel.StopPreview();
            s_Previewing.Clear();
            foreach (var recording in s_Recordings.Values)
                recording.ShownTick = -1;
        }

        static void Show(PlayableDirector director)
        {
            var timelinesObject = director.GetComponent<TlUiTimelines>();
            if (timelinesObject == null)
                return;
            var recording = RecordingOf(director, timelinesObject);
            if (recording.Switches.Count == 0)
                return;
            var tick = Mathf.Clamp(TlPreview.TickOf(director), 0, recording.Switches.Count - 1);
            if (tick == recording.ShownTick)
                return;
            recording.ShownTick = tick;
            foreach (var panel in Object.FindObjectsByType<TlUiPanel>(FindObjectsInactive.Exclude))
            {
                if (!panel.isActiveAndEnabled)
                    continue;
                panel.ShowPreview(recording.TargetNames, recording.Switches[tick], recording.Channels[tick]);
                if (!s_Previewing.Contains(panel))
                    s_Previewing.Add(panel);
            }
        }

        static Recording RecordingOf(PlayableDirector director, TlUiTimelines timelinesObject)
        {
            var version = TlPreview.Version(director) * 31 + EditorUtility.GetDirtyCount(timelinesObject);
            if (s_Recordings.TryGetValue(director, out var recording) && recording.Version == version)
                return recording;
            recording = new Recording { Version = version };
            foreach (var name in timelinesObject.TargetNames)
                recording.TargetNames.Add(name ?? "");
            TlPreview.Record(director, recording.Keep);
            s_Recordings[director] = recording;
            return recording;
        }
    }
}
