using Tl;
using Unity.Entities;

namespace TlUi
{
    [InternalBufferCapacity(4)]
    public struct UiTimelineSlot : IBufferElementData
    {
        public TlTimeline Timeline;
    }

    [InternalBufferCapacity(4)]
    public struct UiCommandInbox : IBufferElementData
    {
        public int Code;
        public int Payload;
    }
}
