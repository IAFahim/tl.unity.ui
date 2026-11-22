using System.ComponentModel;
using Tl.Generated;
using Tl.Unity;

namespace TlUi.Editor
{
    [DisplayName("UI/Remove Class")]
    public sealed class RemoveClassUiMenuTrack : TlMenuTrack
    {
        public override TlClipData CreateTemplate() => new Tl_Ui_RemoveClassClip_TlData();
    }
}
