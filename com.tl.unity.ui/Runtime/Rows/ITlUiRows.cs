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
}
