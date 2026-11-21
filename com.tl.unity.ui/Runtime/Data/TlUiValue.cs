using Unity.Properties;
using UnityEngine.UIElements;

namespace TlUi
{
    public interface ITlUiDataSource
    {
        object Source { get; }

        PropertyPath PathToData { get; }
    }

    public sealed class TlUiValue<T> : IDataSourceViewHashProvider where T : unmanaged
    {
        public T Value;
        public long Version;

        public long GetViewHashCode() => Version;
    }

    public sealed class TlUiValuePropertyBag<T> : ContainerPropertyBag<TlUiValue<T>> where T : unmanaged
    {
        sealed class ValueProperty : Property<TlUiValue<T>, T>
        {
            public override string Name => nameof(TlUiValue<T>.Value);

            public override bool IsReadOnly => true;

            public override T GetValue(ref TlUiValue<T> container) => container.Value;

            public override void SetValue(ref TlUiValue<T> container, T value)
            {
            }
        }

        public TlUiValuePropertyBag()
        {
            AddProperty(new ValueProperty());
        }
    }
}
