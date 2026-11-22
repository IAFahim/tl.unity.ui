using Tl;
using Tl.Unity;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Playables;

namespace TlUi
{
    public sealed class TlUiTimelinesBaker : Baker<TlUiTimelines>
    {
        public override void Bake(TlUiTimelines authoring)
        {
            var host = GetEntity(TransformUsageFlags.None);
            var slots = AddBuffer<UiTimelineSlot>(host);
            var director = GetComponent<PlayableDirector>();
            slots.Add(new UiTimelineSlot { Timeline = TlBaking.GetTimeline(this, director != null ? director.playableAsset : null) });
            var alsoPlays = GetComponent<TlAlsoPlays>();
            if (alsoPlays != null)
                foreach (var timeline in alsoPlays.Timelines)
                    slots.Add(new UiTimelineSlot { Timeline = TlBaking.GetTimeline(this, timeline) });
            var targetNames = AddBuffer<UiTargetName>(host);
            for (var id = 0; id < authoring.TargetNames.Count && id < TlUiTimelines.MostTargetNames; id++)
            {
                var name = default(FixedString64Bytes);
                name.CopyFromTruncated(authoring.TargetNames[id] ?? "");
                targetNames.Add(new UiTargetName { Name = name });
            }
            AddBuffer<UiCommandInbox>(host);
            AddBuffer<UiTextInbox>(host);
            AddComponent<UiInputState>(host);
        }
    }
}
