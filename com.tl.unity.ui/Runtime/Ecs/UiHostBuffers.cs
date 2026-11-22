using Tl;
using Unity.Collections;
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
        public int Target;
    }

    [InternalBufferCapacity(0)]
    public struct UiTextInbox : IBufferElementData
    {
        public int Code;
        public FixedString128Bytes Text;
    }

    public struct UiInputState : IComponentData
    {
        public bool PointerOverUi;
        public bool Typing;
    }
}
