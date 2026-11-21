using System;
using TlUi;
using UnityEngine;

[DefaultExecutionOrder(TlUiOrder.Data - 1)]
public sealed class InventoryAllocationWindowStart : MonoBehaviour
{
    public long AllocatedBeforeUiCode;

    void Update() => AllocatedBeforeUiCode = GC.GetAllocatedBytesForCurrentThread();
}
