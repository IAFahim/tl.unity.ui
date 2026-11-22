using System.ComponentModel;
using Tl.Generated;
using Tl.Unity;

namespace TlUi.Editor
{
    [DisplayName("UI/Attach")]
    public sealed class AttachUiMenuTrack : TlMenuTrack
    {
        public override TlClipData CreateTemplate() => new Tl_Ui_AttachClip_TlData();
    }
}
