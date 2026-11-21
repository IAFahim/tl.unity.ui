using System.Collections;
using Unity.Properties;

namespace TlUi
{
    public interface ITlUiRows
    {
        IList Items { get; }

        PropertyPath PathToRow { get; }

        long CountVersion { get; }
    }

    public sealed class UiRowHandle
    {
        public UiRowHandle(int list)
        {
            List = list;
        }

        public readonly int List;
        public int Index = -1;
    }
}
