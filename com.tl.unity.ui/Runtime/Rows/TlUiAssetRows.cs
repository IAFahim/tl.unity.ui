using System.Collections;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;

namespace TlUi
{
    [CreateAssetMenu(menuName = "Tl UI/Asset Rows", fileName = "Asset Rows")]
    public sealed class TlUiAssetRows : ScriptableObject, ITlUiRows
    {
        [Tooltip("The rows, in order. Each row element's data source is its asset, so bind the asset's [CreateProperty] fields in the row template.")]
        public List<ScriptableObject> Items = new List<ScriptableObject>();

        IList ITlUiRows.Items => Items;

        public PropertyPath PathToRow => default;

        public long CountVersion => Items.Count;
    }
}
