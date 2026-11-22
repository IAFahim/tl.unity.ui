using System.ComponentModel;
using Tl.Generated;
using Tl.Unity;

namespace TlUi.Editor
{
    [DisplayName("UI/Hide")]
    public sealed class HideUiMenuTrack : TlMenuTrack
    {
        public override TlClipData CreateTemplate() => new Tl_Ui_HideClip_TlData();
    }
}
