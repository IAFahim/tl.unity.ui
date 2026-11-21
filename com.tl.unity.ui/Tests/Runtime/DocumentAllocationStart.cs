using System;
using TlUi;
using UnityEngine;

[DefaultExecutionOrder(TlUiOrder.Data - 1)]
public sealed class DocumentAllocationStart : MonoBehaviour
{
    public long AllocatedBeforeUiCode;

    void Update() => AllocatedBeforeUiCode = GC.GetAllocatedBytesForCurrentThread();
}
