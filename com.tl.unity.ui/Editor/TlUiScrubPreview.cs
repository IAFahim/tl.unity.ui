using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Tl.Ui;
using Tl.Unity;
using Tl.Unity.Editor;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

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
        }

        static readonly Dictionary<PlayableDirector, Recording> s_Recordings = new Dictionary<PlayableDirector, Recording>();
        static readonly List<TlUiPanel> s_Previewing = new List<TlUiPanel>();
        static Action<PlayableDirector> s_EvaluateBeforeUi;
        static List<Type> s_Systems;

        static TlUiScrubPreview()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;
            RuntimeHelpers.RunClassConstructor(typeof(TlPreview).TypeHandle);
            s_EvaluateBeforeUi = TlPreviewHook.Evaluate;
            TlPreviewHook.Evaluate = Evaluate;
            EditorApplication.update += StopWhenPreviewEnds;
            EditorApplication.playModeStateChanged += StopOnPlayModeChange;
            AssemblyReloadEvents.beforeAssemblyReload += Release;
        }

        public static int PreviewingPanels => s_Previewing.Count;

        static void Release()
        {
            StopAll();
            s_Recordings.Clear();
            TlPreviewHook.Evaluate = s_EvaluateBeforeUi;
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

        static void Evaluate(PlayableDirector director)
        {
            s_EvaluateBeforeUi?.Invoke(director);
            if (director == null || EditorApplication.isPlayingOrWillChangePlaymode || !AnimationMode.InAnimationMode() || director.GetComponent<TlUiTimelines>() == null)
                return;
            var recording = RecordingOf(director);
            if (recording == null || recording.Switches.Count == 0)
                return;
            var tick = Mathf.Clamp(TlPreview.TickOf(director), 0, recording.Switches.Count - 1);
            if (tick == recording.ShownTick)
                return;
            recording.ShownTick = tick;
            foreach (var panel in UnityEngine.Object.FindObjectsByType<TlUiPanel>(FindObjectsInactive.Exclude))
            {
                if (!panel.isActiveAndEnabled)
                    continue;
                panel.ShowPreview(recording.TargetNames, recording.Switches[tick], recording.Channels[tick]);
                if (!s_Previewing.Contains(panel))
                    s_Previewing.Add(panel);
            }
        }

        static Recording RecordingOf(PlayableDirector director)
        {
            var version = VersionOf(director);
            if (s_Recordings.TryGetValue(director, out var recording) && recording.Version == version)
                return recording;
            try
            {
                recording = Record(director, version);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Tl UI could not preview " + director.name + ": " + (error.InnerException ?? error).Message, director);
                recording = new Recording { Version = version };
            }
            s_Recordings[director] = recording;
            return recording;
        }

        static long VersionOf(PlayableDirector director)
        {
            var also = director.GetComponent<TlAlsoPlays>();
            long version = EditorUtility.GetDirtyCount(director) + EditorUtility.GetDirtyCount(director.GetComponent<TlUiTimelines>()) * 7 + EditorUtility.GetDirtyCount(TlProjectSettings.instance) * 13;
            if (also != null)
                version += EditorUtility.GetDirtyCount(also) * 17;
            foreach (var timeline in TlInspectorModel.Plays(director, also))
            {
                version = version * 31 + EditorUtility.GetDirtyCount(timeline) + timeline.GetHashCode();
                foreach (var track in timeline.GetOutputTracks())
                {
                    if (!(track is TlTrack))
                        continue;
                    version = version * 31 + EditorUtility.GetDirtyCount(track) + (track.mutedInHierarchy ? 1 : 0);
                    foreach (var clip in track.GetClips())
                        version = version * 31 + EditorUtility.GetDirtyCount(clip.asset) + Mathf.RoundToInt((float)(clip.start * 1000.0)) * 7 + Mathf.RoundToInt((float)(clip.duration * 1000.0));
                }
            }
            return version;
        }

        static Recording Record(PlayableDirector director, long version)
        {
            var recording = new Recording { Version = version };
            foreach (var name in director.GetComponent<TlUiTimelines>().TargetNames)
                recording.TargetNames.Add(name ?? "");
            var playable = director.playableAsset as TimelineAsset;
            if (playable == null)
                return recording;
            var ticksPerSecond = TlProjectSettings.instance.TicksPerSecond;
            using var blobs = new BlobAssetStore(128);
            using var baked = new World("Tl UI Preview Bake", WorldFlags.Editor);
            Bake(baked, director.gameObject, blobs);
            using var world = new World("Tl UI Preview", WorldFlags.Editor);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, Systems());
            using (var all = baked.EntityManager.UniversalQuery.ToEntityArray(Allocator.Temp))
                world.EntityManager.CopyEntitiesFrom(baked.EntityManager, all);
            var entities = world.EntityManager;
            using var hosts = entities.CreateEntityQuery(ComponentType.ReadOnly<UiTimelineSlot>(), ComponentType.ReadOnly<TlTimelineSet>(), ComponentType.ReadOnly<Tl.Unity.Playback>());
            if (hosts.IsEmpty)
                return recording;
            var host = hosts.GetSingletonEntity();
            var initialization = world.GetExistingSystemManaged<InitializationSystemGroup>();
            var tl = world.GetExistingSystemManaged<global::Tl.TlSimulationGroup>();
            var tickSeconds = 1f / ticksPerSecond;
            var lastStep = (int)TlTimelinePacker.Ticks(playable.duration, ticksPerSecond) + 64;
            ushort playableId = 0;
            var started = false;
            for (var step = 0; step < lastStep; step++)
            {
                world.SetTime(new TimeData(step * (double)tickSeconds, tickSeconds));
                initialization.Update();
                tl?.Update();
                entities.CompleteAllTrackedJobs();
                if (playableId == 0)
                {
                    var set = entities.GetBuffer<TlTimelineSet>(host, true);
                    playableId = set.Length > 0 ? set[0].Id : (ushort)0;
                }
                var playbacks = entities.GetBuffer<Tl.Unity.Playback>(host, true);
                var followed = -1;
                for (var index = 0; index < playbacks.Length && playableId != 0; index++)
                    if (playbacks[index].Timeline == playableId)
                    {
                        followed = index;
                        break;
                    }
                if (followed < 0)
                {
                    if (!started)
                        continue;
                    Keep(recording, entities, host);
                    break;
                }
                started = true;
                var playback = playbacks[followed];
                if ((playback.Flags & global::Tl.PlaybackFlags.StartPending) != 0)
                    continue;
                while (recording.Switches.Count <= playback.Tick)
                    Keep(recording, entities, host);
            }
            return recording;
        }

        static void Keep(Recording recording, EntityManager entities, Entity host)
        {
            recording.Switches.Add(entities.HasComponent<UiSwitches>(host) ? entities.GetComponentData<UiSwitches>(host) : default);
            recording.Channels.Add(entities.HasComponent<UiChannels>(host) ? entities.GetComponentData<UiChannels>(host) : default);
        }

        static List<Type> Systems()
        {
            if (s_Systems != null)
                return s_Systems;
            var always = new HashSet<Type> { typeof(TlLibrarySystem), typeof(TlTimelineRegisterSystem), typeof(TlPlayOnAwakeSystem), typeof(global::Tl.TlSimulationGroup), typeof(FixedStepSimulationSystemGroup), typeof(SimulationSystemGroup), typeof(InitializationSystemGroup) };
            var systems = new List<Type>();
            var indices = DefaultWorldInitialization.GetAllSystemTypeIndices(WorldSystemFilterFlags.Default);
            for (var index = 0; index < indices.Length; index++)
            {
                var type = TypeManager.GetSystemType(indices[index]);
                if (type != null && (always.Contains(type) || UnderTl(type)))
                    systems.Add(type);
            }
            foreach (var type in always)
                if (!systems.Contains(type))
                    systems.Add(type);
            s_Systems = systems;
            return systems;
        }

        static bool UnderTl(Type type)
        {
            for (var depth = 0; depth < 16 && type != null; depth++)
            {
                var groups = type.GetCustomAttributes(typeof(UpdateInGroupAttribute), true);
                if (groups.Length == 0)
                    return false;
                type = ((UpdateInGroupAttribute)groups[0]).GroupType;
                if (type == typeof(global::Tl.TlSimulationGroup))
                    return true;
            }
            return false;
        }

        static void Bake(World world, GameObject root, BlobAssetStore blobs)
        {
            var hybrid = typeof(Baker<>).Assembly;
            var bakingUtility = hybrid.GetType("Unity.Entities.BakingUtility", true);
            var flags = Enum.Parse(bakingUtility.GetNestedType("BakingFlags", BindingFlags.Public | BindingFlags.NonPublic), "AssignName");
            var settings = Activator.CreateInstance(hybrid.GetType("Unity.Entities.BakingSettings", true), flags, blobs);
            bakingUtility.GetMethod("BakeGameObjects", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { world, new[] { root }, settings });
        }
    }
}
