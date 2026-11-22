using Tl;
using Tl.Ui;
using Tl.Unity;
using Unity.Entities;

namespace TlUi
{
    public static class UiSignals
    {
        public static void Raise(this DynamicBuffer<TimelineEvent<UiSignal>> signals, byte code)
        {
            signals.Add(new TimelineEvent<UiSignal> { Record = new EventRecord<UiSignal>(default, 0, signals.Length, new UiSignal { Code = code }) });
        }
    }
}
