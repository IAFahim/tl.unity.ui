import os
from xml.sax.saxutils import escape

OUT = os.path.join(os.path.dirname(__file__), "..", "images")
FONT = "Inter, 'Segoe UI', Roboto, Arial, sans-serif"
BG = "#191919"
PANEL = "#282828"
HEADER = "#3c3c3c"
FIELD = "#1f1f1f"
LINE = "#4a4a4a"
TEXT = "#d2d2d2"
DIM = "#8f8f8f"
SELECT = "#2c5d87"
ACCENT = "#ff9f1a"
CLIP = "#3f6ea8"
GREEN = "#4f8f5a"


class Drawing:
    def __init__(self, width, height, title):
        self.width, self.height, self.parts = width, height, []
        self.rect(0, 0, width, height, BG)
        self.text(20, 32, title, 20, TEXT, weight="600")

    def rect(self, x, y, w, h, fill, stroke=None, radius=4, width=1.5, dash=None):
        extra = f' stroke="{stroke}" stroke-width="{width}"' if stroke else ""
        extra += f' stroke-dasharray="{dash}"' if dash else ""
        self.parts.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{radius}" fill="{fill}"{extra}/>')

    def text(self, x, y, words, size=14, color=TEXT, weight="400", anchor="start"):
        self.parts.append(f'<text x="{x}" y="{y}" font-family="{FONT}" font-size="{size}" font-weight="{weight}" fill="{color}" text-anchor="{anchor}">{escape(words)}</text>')

    def window(self, x, y, w, h, name):
        self.rect(x, y, w, h, PANEL, LINE)
        self.rect(x, y, w, 26, HEADER, radius=4)
        self.text(x + 10, y + 18, name, 13, TEXT, "600")

    def row(self, x, y, w, words, selected=False, indent=0, color=TEXT):
        if selected:
            self.rect(x, y - 15, w, 22, SELECT, radius=2)
        self.text(x + 8 + indent, y, words, 13, color)

    def field(self, x, y, w, label, value, label_width=110, dropdown=False, highlight=False):
        self.text(x, y, label, 13, DIM)
        self.rect(x + label_width, y - 15, w - label_width, 22, FIELD, ACCENT if highlight else LINE, 3, 2 if highlight else 1)
        self.text(x + label_width + 8, y, value, 13, TEXT)
        if dropdown:
            self.text(x + w - 16, y, "▾", 13, DIM)

    def button(self, x, y, w, h, words, fill=HEADER):
        self.rect(x, y, w, h, fill, LINE, 3)
        self.text(x + w / 2, y + h / 2 + 5, words, 13, TEXT, anchor="middle")

    def callout(self, number, x, y, note=None, note_x=None, note_y=None, note_width=260):
        self.parts.append(f'<circle cx="{x}" cy="{y}" r="15" fill="{ACCENT}" stroke="#1a1a1a" stroke-width="2"/>')
        self.text(x, y + 5, str(number), 15, "#1a1a1a", "700", "middle")
        if note:
            nx = note_x if note_x is not None else x + 24
            ny = note_y if note_y is not None else y - 12
            lines = note.split("\n")
            self.rect(nx, ny, note_width, 10 + 19 * len(lines), "#2b2416", ACCENT, 6, 1.5)
            for index, line in enumerate(lines):
                self.text(nx + 10, ny + 22 + 19 * index, line, 13, "#ffe2b8")

    def arrow(self, x1, y1, x2, y2):
        self.parts.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{ACCENT}" stroke-width="2.5" marker-end="url(#head)"/>')

    def save(self, name):
        head = f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.width}" height="{self.height}" viewBox="0 0 {self.width} {self.height}">'
        marker = f'<defs><marker id="head" markerWidth="10" markerHeight="10" refX="8" refY="5" orient="auto"><path d="M0,0 L10,5 L0,10 z" fill="{ACCENT}"/></marker></defs>'
        footer = f'<text x="{self.width - 16}" y="{self.height - 12}" font-family="{FONT}" font-size="11" fill="#6b6b6b" text-anchor="end">Diagram of the Unity Editor, not a screenshot</text>'
        with open(os.path.join(OUT, name), "w") as svg:
            svg.write(head + marker + "".join(self.parts) + footer + "</svg>\n")


def editor_map():
    d = Drawing(1200, 720, "Where things are in the Unity Editor")
    d.rect(10, 48, 1180, 34, HEADER)
    d.button(560, 53, 36, 24, "", "#2f2f2f")
    d.parts.append('<path d="M572,58 L572,72 L585,65 z" fill="#d2d2d2"/>')
    d.button(600, 53, 36, 24, "❚❚", "#2f2f2f")
    d.window(10, 92, 260, 360, "Hierarchy")
    for i, (words, indent) in enumerate([("Main Camera", 0), ("Screen", 0), ("☑ Screen Timelines", 0), ("Screen Timelines", 16)]):
        d.row(14, 130 + 24 * i, 252, words, selected=i == 3, indent=indent)
    d.window(280, 92, 620, 360, "Game")
    d.rect(300, 130, 580, 300, "#10131a", radius=2)
    d.rect(510, 245, 160, 70, "#2858c8", radius=12)
    d.text(590, 290, "Hello", 30, "#ffffff", "600", "middle")
    d.window(910, 92, 280, 360, "Inspector")
    d.text(922, 140, "Fade hello", 15, TEXT, "600")
    d.field(922, 180, 256, "Target", "hello", 70, True)
    d.field(922, 210, 256, "Property", "Opacity", 70, True)
    d.field(922, 240, 256, "From", "0", 70)
    d.field(922, 270, 256, "To", "1", 70)
    d.window(10, 462, 380, 248, "Project")
    for i, words in enumerate(["Assets", "  Tl UI", "    Screen.uxml", "    Screen Timeline.playable", "    Screen Panel Settings"]):
        d.row(14, 500 + 24 * i, 372, words)
    d.window(400, 462, 790, 248, "Timeline")
    d.button(410, 494, 30, 22, "+")
    d.row(410, 540, 150, "Animate")
    d.row(410, 580, 150, "Animate")
    d.rect(570, 525, 220, 26, CLIP, radius=3)
    d.text(580, 543, "Fade hello", 13, "#ffffff")
    d.rect(570, 565, 220, 26, CLIP, radius=3)
    d.text(580, 583, "Move hello Y", 13, "#ffffff")
    d.parts.append(f'<line x1="700" y1="500" x2="700" y2="700" stroke="#ffffff" stroke-width="2"/>')
    d.callout(1, 246, 105, "Hierarchy: the objects in your scene.\nScreen shows the UI; Screen Timelines\nholds its timelines.", 40, 330, 250)
    d.callout(2, 876, 105, "Game view: what the player sees.\nIt previews while you drag the\nTimeline playhead, no Play needed.", 310, 330, 270)
    d.callout(3, 1166, 105, "Inspector: settings of whatever\nyou clicked. Click a clip to see\nwhat it changes.", 912, 300, 270)
    d.callout(4, 366, 475, "Project: your files.", 196, 600, 170)
    d.callout(5, 1160, 486, "Timeline: clips over time. Each row\nis a track, each block a clip.", 870, 640, 300)
    d.callout(6, 536, 65, "Play runs the game.", 650, 50, 170)
    d.save("editor-map.svg")


def create_screen():
    d = Drawing(1200, 560, "Step 1: make a screen (one click)")
    d.window(20, 60, 330, 470, "Hierarchy")
    d.row(24, 100, 322, "Main Camera")
    d.row(24, 124, 322, "Directional Light")
    d.rect(150, 150, 220, 210, "#2f2f2f", LINE, 4)
    for i, words in enumerate(["Copy", "Paste", "Create Empty", "UI Toolkit  ▸", "Tl UI  ▸", "Camera"]):
        d.row(154, 175 + 30 * i, 212, words, selected=words.startswith("Tl UI"))
    d.rect(372, 285, 150, 34, "#2f2f2f", LINE, 4)
    d.row(376, 308, 142, "Screen", selected=True)
    d.callout(1, 60, 160, "Save your scene, then right-click\nempty space in the Hierarchy.", 30, 380, 250)
    d.callout(2, 388, 268, "Pick Tl UI ▸ Screen.", 400, 220, 170)
    d.arrow(530, 302, 640, 302)
    d.window(650, 60, 260, 200, "Hierarchy, after")
    d.row(654, 100, 252, "Main Camera")
    d.row(654, 124, 252, "Directional Light")
    d.row(654, 148, 252, "Screen", selected=True)
    d.row(654, 172, 252, "☑ Screen Timelines")
    d.row(654, 196, 252, "Screen Timelines", indent=16)
    d.window(920, 60, 260, 200, "Project, after")
    for i, words in enumerate(["Tl UI", "  Screen.uxml", "  Screen Timeline.playable", "  Screen Timelines (scene)", "  Screen Panel Settings"]):
        d.row(924, 100 + 24 * i, 252, words)
    d.window(650, 280, 530, 250, "Game, press Play")
    d.rect(670, 316, 490, 196, "#10131a", radius=2)
    d.rect(845, 384, 140, 60, "#2858c8", radius=12)
    d.text(915, 424, "Hello", 26, "#ffffff", "600", "middle")
    d.callout(3, 1150, 300, "Press Play: Hello fades in and\nslides up. That motion is two\nclips on the Screen Timeline.", 860, 456, 290)
    d.save("step-1-create-screen.svg")


def ui_builder():
    d = Drawing(1200, 640, "Step 2: design the screen in UI Builder (UI Toolkit)")
    d.text(20, 64, "In the Project window, double-click Screen.uxml. UI Builder opens:", 14, DIM)
    d.window(20, 80, 250, 260, "Library")
    for i, words in enumerate(["Containers", "  VisualElement", "  ScrollView", "Controls", "  Label", "  Button", "  Slider", "  ProgressBar"]):
        d.row(24, 124 + 25 * i, 242, words, selected=words.strip() == "Label", color=TEXT if words.startswith("  ") else DIM)
    d.window(20, 350, 250, 270, "Hierarchy")
    d.row(24, 394, 242, "Screen.uxml", color=DIM)
    d.row(24, 418, 242, "#screen", indent=12)
    d.row(24, 442, 242, "#hello", indent=24)
    d.row(24, 466, 242, "#title", indent=24, selected=True)
    d.window(280, 80, 600, 540, "Viewport")
    d.rect(300, 120, 560, 480, "#10131a", radius=2)
    d.rect(500, 260, 160, 70, "#2858c8", radius=12)
    d.text(580, 305, "Hello", 30, "#ffffff", "600", "middle")
    d.rect(470, 180, 220, 46, "#1a1f2b", ACCENT, 4, 2, "6 4")
    d.text(580, 210, "Level 1", 24, "#ffffff", "600", "middle")
    d.window(890, 80, 290, 540, "Inspector")
    d.text(902, 120, "Label", 14, TEXT, "600")
    d.field(902, 156, 266, "Name", "title", 80, highlight=True)
    d.field(902, 186, 266, "Text", "Level 1", 80)
    d.text(902, 230, "Text", 13, TEXT, "600")
    d.field(902, 260, 266, "Size", "40", 80)
    d.field(902, 290, 266, "Color", "■ white", 80)
    d.text(902, 334, "Background", 13, TEXT, "600")
    d.field(902, 364, 266, "Color", "■ none", 80)
    d.text(902, 408, "Position", 13, TEXT, "600")
    d.field(902, 438, 266, "Margin", "0 0 24 0", 80)
    d.callout(1, 240, 224, "Drag a Label from the Library\nonto the screen (or the Hierarchy).", 120, 590, 280)
    d.arrow(250, 226, 460, 200)
    d.callout(2, 1160, 156, "Type a Name. That name is what\nclips pick as their Target.", 880, 470, 290)
    d.callout(3, 1160, 290, "Style it here: text, colour,\nsize, spacing. Or in your .uss.", 880, 540, 290)
    d.callout(4, 860, 104, "Ctrl+S saves Screen.uxml.\nThe name now shows in every\nclip's Target list.", 560, 520, 260)
    d.save("step-2-ui-builder.svg")


def timeline():
    d = Drawing(1200, 640, "Step 3: animate it in the Timeline window")
    d.window(20, 60, 300, 210, "Hierarchy")
    d.row(24, 100, 292, "Screen")
    d.row(24, 124, 292, "☑ Screen Timelines")
    d.row(24, 148, 292, "Screen Timelines", indent=16, selected=True)
    d.callout(1, 296, 124, "Tick the box next to Screen Timelines,\nexpand it, click the object inside.\nWindow ▸ Sequencing ▸ Timeline shows it.", 340, 70, 330)
    d.window(20, 290, 1160, 330, "Timeline")
    d.button(30, 324, 30, 22, "+")
    d.rect(30, 350, 180, 150, "#2f2f2f", LINE, 4)
    for i, words in enumerate(["Track Group", "Tl ▸", "UI  ▸"]):
        d.row(34, 374 + 28 * i, 172, words, selected=words.startswith("UI"))
    d.rect(212, 410, 150, 190, "#2f2f2f", LINE, 4)
    for i, words in enumerate(["Show", "Hide", "Enable", "Add Class", "Animate", "Text", "Signal"]):
        d.row(216, 432 + 25 * i, 142, words, selected=words == "Animate")
    d.callout(2, 76, 335, "Click +, open UI, pick a block.\nEach block is a new track.", 110, 292, 260)
    d.parts.append(f'<line x1="380" y1="350" x2="1170" y2="350" stroke="{LINE}"/>')
    for i, second in enumerate(range(0, 5)):
        x = 400 + i * 190
        d.text(x, 342, f"{second}s", 12, DIM)
    d.row(380, 390, 140, "Animate")
    d.row(380, 440, 140, "Hide")
    d.rect(520, 374, 380, 28, CLIP, radius=3)
    d.text(530, 393, "Fade hello", 13, "#ffffff")
    d.rect(700, 424, 210, 28, CLIP, radius=3)
    d.text(710, 443, "Hide title", 13, "#ffffff")
    d.rect(520, 480, 170, 70, "#2f2f2f", LINE, 4)
    d.row(524, 504, 162, "Add Tl Clip", selected=True)
    d.row(524, 530, 162, "Mute track")
    d.callout(3, 505, 470, "Right-click a track ▸ Add Tl Clip.", 705, 488, 250)
    d.callout(4, 900, 388, "Drag the clip to move it in time;\ndrag its right edge to make it\nlonger or shorter.", 925, 400, 245)
    d.parts.append('<line x1="640" y1="352" x2="640" y2="610" stroke="#ffffff" stroke-width="2"/>')
    d.parts.append('<path d="M632,352 L648,352 L640,364 z" fill="#ffffff"/>')
    d.callout(5, 640, 588, "Drag the playhead: the Game view\nshows the screen at that moment.", 660, 566, 270)
    d.save("step-3-timeline.svg")


def inspector():
    d = Drawing(1200, 600, "Step 4: tell the clip what to change (Inspector)")
    d.window(20, 60, 520, 520, "Inspector")
    d.text(32, 98, "Fade hello", 15, TEXT, "600")
    d.text(32, 132, "Clip Timing", 13, TEXT, "600")
    d.field(44, 160, 480, "Start", "0 s", 120)
    d.field(44, 190, 480, "Duration", "0.6 s", 120)
    d.text(32, 232, "▾ Tl Clip", 13, TEXT, "600")
    d.field(44, 264, 480, "Target", "hello", 120, True, True)
    d.field(44, 296, 480, "Property", "Opacity", 120, True, True)
    d.rect(44, 310, 480, 54, "#232323", LINE, 3)
    d.text(54, 330, "0 is invisible, 1 is as designed. The clip goes", 12, DIM)
    d.text(54, 350, "From → To, shaped by Ease. Hold keeps To after.", 12, DIM)
    d.field(44, 392, 480, "From", "0", 120, highlight=True)
    d.field(44, 424, 480, "To", "1", 120, highlight=True)
    d.text(44, 456, "Ease", 13, DIM)
    d.rect(164, 441, 360, 40, FIELD, LINE, 3)
    d.parts.append('<path d="M170,476 C230,445 300,446 518,446" stroke="#4fd1a5" stroke-width="2" fill="none"/>')
    d.text(44, 512, "Hold", 13, DIM)
    d.rect(164, 498, 20, 20, SELECT, LINE, 3)
    d.text(168, 513, "✓", 13, "#ffffff")
    d.callout(1, 560, 264, "Target: which element. The list shows\nevery Name from UI Builder. The clip\nrenames itself: Fade hello, Hide title.", 590, 230, 340)
    d.callout(2, 560, 296, "Property: Opacity, Move X, Move Y,\nScale, Rotate, Width %, Height %,\nReveal (typing) or Value (bars).", 590, 300, 340)
    d.callout(3, 560, 408, "From and To: the numbers at the\nclip's start and end. Equal numbers\nmean nothing moves (it warns you).", 590, 376, 340)
    d.callout(4, 560, 461, "Ease: the shape of the change.", 590, 446, 340)
    d.callout(5, 560, 508, "Hold: keep To after the clip ends.", 590, 496, 340)
    d.text(960, 120, "Examples", 15, TEXT, "600")
    examples = [("Fade in", "Opacity 0 → 1"), ("Slide in from left", "Move X -300 → 0"), ("Pop", "Scale 0.6 → 1"), ("Spin", "Rotate 0 → 360"), ("Type out text", "Reveal 0 → 1"), ("Fill a bar", "Value 0 → 1")]
    for i, (name, value) in enumerate(examples):
        d.text(960, 150 + 34 * i, name, 13, TEXT)
        d.text(1180, 150 + 34 * i, value, 13, "#9fd3ff", anchor="end")
    d.save("step-4-inspector.svg")


def panel_tables():
    d = Drawing(1200, 600, "Step 5: buttons, lists and drag and drop (Tl UI Panel on the Screen object)")
    d.window(20, 60, 560, 520, "Inspector   Screen ▸ Tl UI Panel")
    d.text(32, 106, "▾ Commands", 13, TEXT, "600")
    d.field(44, 128, 520, "Target", "draw", 130, True, True)
    d.field(44, 158, 520, "On", "Click", 130, True)
    d.field(44, 188, 520, "Do", "Play Timeline", 130, True, True)
    d.field(44, 218, 520, "Timeline Slot", "1", 130)
    d.text(32, 262, "▾ Lists", 13, TEXT, "600")
    d.field(44, 292, 520, "Target", "hand", 130, True)
    d.field(44, 322, 520, "Rows", "Hand Rows", 130)
    d.field(44, 352, 520, "Row Template", "Card.uxml", 130)
    d.field(44, 382, 520, "Drag Code", "5", 130, highlight=True)
    d.field(44, 412, 520, "Drop Code", "4", 130, highlight=True)
    d.text(32, 456, "▾ Signals", 13, TEXT, "600")
    d.field(44, 486, 520, "Code", "1", 130)
    d.field(44, 516, 520, "Do", "Play Timeline (slot 1)", 130, True)
    d.callout(1, 600, 158, "Commands: pick the element by name;\nwhen it is clicked (or hovered, changed,\nor a hotkey), play a timeline or send\na code to the game.", note_width=330)
    d.callout(2, 600, 322, "Lists: rows from an asset or the\ngame, drawn with a row template.", note_width=330)
    d.callout(3, 600, 397, "Drag Code: a row dropped on another\nrow of this list (swap, reorder).\nDrop Code: a row from another list\ndropped here (hand → table).", note_width=330)
    d.callout(4, 600, 501, "Signals: what a Signal clip, or the\ngame, sets off: play a timeline,\na sound, any UnityEvent.", note_width=330)
    d.rect(990, 110, 190, 300, "#10131a", radius=4)
    d.rect(1010, 140, 70, 98, "#c9b0f0", "#ffd25a", 8, 2)
    d.text(1045, 196, "Golem", 12, "#1a1a1a", "600", "middle")
    d.rect(1020, 300, 60, 84, "#ffd877", "#ffffff", 8, 2)
    d.rect(1090, 300, 60, 84, "#9fd0ff", "#ffffff", 8, 2)
    d.text(1085, 430, "while dragging: tl-drag-ghost,", 11, DIM, anchor="middle")
    d.text(1085, 446, "tl-dragging, tl-drop-target (USS)", 11, DIM, anchor="middle")
    d.save("step-5-panel.svg")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    editor_map()
    create_screen()
    ui_builder()
    timeline()
    inspector()
    panel_tables()
