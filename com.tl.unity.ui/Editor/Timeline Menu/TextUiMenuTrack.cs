using System.ComponentModel;
using Tl.Generated;
using Tl.Unity;

namespace TlUi.Editor
{
    [DisplayName("UI/Text")]
    public sealed class TextUiMenuTrack : TlMenuTrack
    {
        public override TlClipData CreateTemplate() => new Tl_Ui_TextClip_TlData();
    }
}
