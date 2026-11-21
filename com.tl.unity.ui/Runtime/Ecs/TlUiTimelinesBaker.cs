using Tl;
using Tl.Unity;
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
            AddBuffer<UiCommandInbox>(host);
        }
    }
}
