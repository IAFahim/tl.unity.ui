using System.ComponentModel;
using Tl.Generated;
using Tl.Unity;

namespace TlUi.Editor
{
    [DisplayName("UI/Disable")]
    public sealed class DisableUiMenuTrack : TlMenuTrack
    {
        public override TlClipData CreateTemplate() => new Tl_Ui_DisableClip_TlData();
    }
}
