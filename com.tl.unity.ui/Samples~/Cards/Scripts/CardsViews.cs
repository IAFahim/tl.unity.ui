using TlUi;
using UnityEngine;

namespace CardsSample
{
    [AddComponentMenu("Tl UI Samples/Hand Rows")]
    public sealed class HandRows : TlUiBufferRows<CardInHand>
    {
    }

    [AddComponentMenu("Tl UI Samples/Table Rows")]
    public sealed class TableRows : TlUiBufferRows<CardOnTable>
    {
    }

    [AddComponentMenu("Tl UI Samples/Card Score View")]
    public sealed class CardScoreView : TlUiData<CardScore>
    {
    }
}
