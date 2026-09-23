# CLAUDE.md

Notes for anyone, human or otherwise, picking this branch up. It covers what
the project is, how to run it, the rules that break things when ignored, and
what was added here and why.

## The project

Lanotalium is a visual chart editor for **Lanota**, built in **Unity
2018.3.6f1** — that exact version, whatever the upstream README says about
2017.4.2f2. C#, uGUI, Windows only: it uses `System.Windows.Forms`,
`System.Drawing` and a native `libzipw.dll`.

This branch, `offline-spanish-editing`, adds an offline mode, a Spanish
translation and a large amount of editing comfort. `CHANGELOG.md` lists the
user-facing side of it in both languages.

## Running and compiling

- The standalone Unity editor does not list projects: open it with **Open**
  and pick the project root.
- Play from **LimFirstRun** (scene index 0). The flow is LimFirstRun →
  LimLaunch → LimTuner; starting at LimTuner skips initialisation.
- Headless compile:
  `Unity.exe -quit -batchmode -nographics -projectPath <proj> -logFile <log>`,
  plus `-buildWindows64Player <out>\Lanotalium.exe` for a build.
- **Unity must not be open**, or it aborts with "another Unity instance is
  running". Searching the log only for `error CS` hides that.
- The process detaches and returns early: wait for it to exit and for the log
  to have content before reading it.
- **A batch run empties `Library/LastSceneManagerSetup.txt`**, so the next
  time the editor is opened it shows a blank Untitled scene (a sky, and Play
  does nothing), which looks exactly like the project failing to open. Save
  that file before a batch run and put it back after; if it is lost, opening
  `Assets/_Scenes/LimFirstRun.unity` by double-click fixes it.
- About 52 warnings are expected, nearly all from the obsolete `WWW` API. The
  `.csproj` and `.sln` files are regenerated constantly; churn there is noise.
- **With the editor open**, compile with Unity's own Roslyn
  (`Editor/Data/Tools/Roslyn/csc.exe`): the references and defines from the
  generated `Schwarzer.Lanotalium.csproj`, the sources gathered from disk
  (everything under `Assets/Scripts` without a nearer `.asmdef`; the `.csproj`
  may predate new files). Check it catches a planted error before trusting a
  clean run. Pure logic (`ChartData`, `LimTimeGroups`) can then be run outside
  Unity against the resulting dll with `mscorlib`, `UnityEngine.CoreModule`
  and `Newtonsoft.Json` referenced. In Git Bash set `MSYS_NO_PATHCONV=1`, or
  `/options` are read as paths.
- **Play mode runs in batch mode**, audio included, so a harness can open
  a real project and play it: enter play mode from `-executeMethod` without
  `-quit`, survive the domain reload with `[InitializeOnLoad]` and
  `SessionState`, drive it from `EditorApplication.update`, and call
  `EditorApplication.Exit` at the end. Rendering the tuner camera into a
  RenderTexture gives screenshots. `Tools/UiTweakCheck` is one; loading a
  project rewrites its `.lap`, backs its chart up and rewrites
  `%APPDATA%/Lanotalium/Preferences.json`, so it works on copies of the
  project. **Never delete or "restore" `%APPDATA%/Lanotalium` from a
  sandboxed shell**: a delete there reaches the real folder while a copy
  back only lands in the sandbox's virtual layer, which still shows the
  files. That wiped the user's settings on every run (language prompt,
  favourites gone, layout back to default) while every check said
  "restored". Only read the settings; compare them afterwards from Python,
  which sees the real disk.
- **With the editor closed**, an `-executeMethod` harness in a temporary
  `Assets/Editor` checks code against the real engine. Write its report
  outside the project: **Unity deletes `Temp/` on exit**. Remove the harness
  and its `.meta` afterwards, and put `LastSceneManagerSetup.txt` back.

## Where things live

Code is split by `.asmdef`: `Schwarzer.Lanotalium` (everything under
`Assets/Scripts` that has no nearer asmdef), `Schwarzer.Chart`,
`Schwarzer.WebApi`, `Schwarzer.Dialogs`, `Schwarzer.StringParser`,
`Schwarzer.Unity`, and `Lanotalium.Offline`. Cross-assembly use needs a
declared reference, and cycles are refused. `Lanotalium.Offline` deliberately
depends on nothing so anything can reference it.

The pieces worth knowing:

| Area | Where |
| --- | --- |
| Editing, selection, undo | `LimSystem/LimEditor/Operation/LimOperationManager*.cs` (one partial class) |
| The ring and its camera | `LimSystem/LimTuner/` |
| Timeline and motions | `LimSystem/LimEditor/Windows/TimeLine/` |
| Creator tools | `LimSystem/LimEditor/Windows/Creator/` |
| Inspector components | `LimSystem/LimEditor/Windows/Inspector/Components/` |
| Shared helpers | `LimSystem/LimHelpers/` |
| Text | `Assets/StreamingAssets/Language/LimLangPkg_*.txt` |
| Hit effects, ornaments, skins | `LimSystem/LimTuner/Effects/`, art in `StreamingAssets/UiTweak` and `StreamingAssets/TunerSkin` |

## Rules that break things when ignored

- **Offline filter.** `LimOfflineMode.Enabled` guards about 45 network calls.
  It must only block `http`/`https`: `LimProjectManager` loads audio and
  backgrounds through `file:///`, and blocking those stops projects opening.
- **Language packs.** English and Spanish only. The lookup throws on a missing
  key, so every new key goes in **both** files, in the right section
  (`#TextDict`, `#NotificationDict`, `#HintDict`, `#TutorialDict`). Game jargon
  stays in English on purpose: Tap, Hold, Flick In/Out, Catch, Rail, Joint,
  Beatline, Angleline, Scroll Speed, Degree, Ease, Motion, Timing, Offset.
- **Text lives in four dictionaries**: `TextDict`, `NotificationDict`,
  `HintDict`, `TutorialDict`, and asking the wrong one throws just as a
  missing key does. Where a key sits is not always where its meaning suggests:
  `Copier_Msg_Success` is a message but lives in `TextDict`. Check before
  using an existing key.
- **Note moves: time before degree.** `SetTapNoteDegree(..., isAbsolute: true)`
  subtracts the camera rotation sampled at the note's own timing. Setting the
  degree first misplaces notes whenever the camera rotates.
- **Numbers.** `LimNumberCulture` forces `InvariantCulture` before any scene
  loads, so `.` is the decimal separator everywhere. Parse user input through
  `LimNumber.TryParseFloat`, which also accepts `,`. Never go back to a bare
  `float.Parse`/`TryParse`: the machine runs Spanish Windows, where 1.5 read as
  15 in every field and in the Arcaea importer.
- **Preferences arrive late.** `LimSystem.RestorePreferences()` runs in
  `LimSystem.Start` and **replaces the whole `Preferences` object**. Anything a
  tool reads or writes in its own `Start` is thrown away. UI built from
  preferences must notice the real list arriving and rebuild; compare counts in
  `Update`/`LateUpdate`. Saving happens in `OnDestroy`/`OnApplicationQuit`.
- **The tuner camera only draws layers 8-13** (`cullingMask` 16128). A new
  GameObject defaults to layer 0 and is simply never rendered there. Layer 8 is
  "Tuner".
- **Sorting layers**, low to high: Default, Background, Tuner, Beatline,
  Angleline, Note, Core, ClickToCreate, EditorUI, Effect, Tutorial. The ring's
  own sprites sit on Tuner; anything meant to pass behind them goes on
  Background, and anything that has to stay on top of the notes goes on
  ClickToCreate.
- **Depth beats sorting layers for opaque materials.** The anglelines use an
  opaque Standard material: something opaque and nearer the camera draws over
  the ring whatever its sorting layer says. Use a transparent, non
  depth-writing material for anything hung in front of the ring plane.
- **Polar positions have two names.** The camera position is
  `X = -Rou·cos(Theta)`, `Z = Rou·sin(Theta)`, and charts do carry **negative**
  radii: radius 9 at one angle is the same place as radius −9 half a turn
  round. Reading a point back with the plain formula always answers positive,
  which lands the result on the opposite side of a motion holding a negative
  radius. Convert with `PlaneToPolarLike`, which keeps the motion's own sign
  and nearest turn.
- **Scenes store text as `\uXXXX` escapes.** Search for escapes, not
  characters.
- **Creator tool panels fold by resizing their ViewRect**, not by
  deactivating, so their scripts keep running while collapsed and their static
  flags stay reliable.
- **A clone carries its EventTriggers.** Several controls report being
  pressed or typed into through an `EventTrigger` rather than through their
  own event, so a copy keeps telling the original's manager what is happening
  to it. Strip them as well as the `LimMouseOverHint`s.
- **`GetComponentInParent` finds nothing on an inactive object** in this
  Unity. The Analyzer looked up its menu buttons that way while their
  drop-down was hidden, found none, and silently left Chart Convert's old
  importer wired to both entries; it compiled clean and only a click showed
  it. Walk `transform.parent` with `GetComponent` instead, and check runtime
  rewiring with a harness that opens the scene and invokes the buttons.
- **The Preferences window starts inactive**, so its `Start` does not run
  until it is first opened. Anything of its own that has to exist earlier,
  such as rows the theme has to know about, is built from `SetTexts`, which
  the language manager calls while the editor is still starting up.

## How UI is added here

Nothing added on this branch touches `LimTuner.unity`. New controls are built
at runtime, usually by cloning something already on screen so the result
matches the panel it lands in:

- Whole tools (Favourite Groups, Grid) are copies of the **Angleline tool**,
  stripped of its component and contents and refilled. Strip with
  `DestroyImmediate` so the copy's `Start` never runs.
- A clone keeps the original's event wiring and its hover hints. Replace the
  event (`Btn.onClick = new Button.ButtonClickedEvent()`; `RemoveAllListeners`
  leaves scene-serialised calls in place) and remove inherited
  `LimMouseOverHint` components, or the copy will keep reporting to the tool it
  came from.
- Hand a new tool **its own** content rect. Asking a cloned template for its
  parent lands every row inside the tool the template belongs to, where it is
  clipped out of sight.
- Row helpers: `LimUiBuilder` (labels, sliders), `LimIcons` (icons, with a
  text fallback when a file is missing), `LimLineColor` (tinting the ring's
  guide lines, one shared material per colour).
- Inserting a row means **pushing down** whatever sits below it and growing the
  panel's height, never drawing on top.
- **Order matters when a row is both a template and a host.** The Creator's
  Create Catch Rail row is copied to make Create Group Ease, which is copied to
  make Segment Hold Note, which is copied to make Set Rail Ease. Anything added
  onto the Catch Rail row before those run is copied onto all four, and the
  copies are dead: listeners added at runtime are not carried by Instantiate,
  so the extras look like controls and do nothing. Build onto a template row
  **after** everything that clones it.
- **The Creator's rows are laid out last** by `CompactCreatorRows`. It puts
  them 30 apart and keeps a 5 px gap only after Create Scroll Speed and
  before the tools, which is the user's choice. A new row can push its
  neighbours down by any amount: what counts is its order, and it has to be
  in before that pass runs.
- **Every runtime clone of scene UI calls `LimThemeManager.Adopt(Source,
  Clone)` right after `Instantiate`, and every colour code sets on a themed
  control goes through `LimThemeManager.Paint(Graphic, Original)`.** The
  theme repaints from recorded originals and deliberately does not record
  controls that appear once a theme is painted. A clone made after that
  carries the painted colour; the next change of theme then recorded it as
  its own and painted it again, so cloned Creator rows came out darker than
  their neighbours. A colour set directly (the Inspector's tabs set
  `UnpressedColor`/`PressedColor`) is the default theme's whatever theme is
  on, which is why the tabs went pale after being pressed.
- `LimMouseOverHint` keeps every hover balloon on screen: it opens to the
  left of the pointer at the right edge and below it at the top. A hint needs
  a graphic that catches the pointer, so put it on the button or field, not
  on a label (`LimUiBuilder` labels do not catch it).
- A `Button` added with `AddComponent` at runtime has `transition` ColorTint
  and no `targetGraphic`. Anything that colours such a button itself should
  name the target and set the transition to None, rather than leave whether
  Unity repaints it resting on an unset field.

Icons come from the Bigmug set on svgrepo, CC0. Fetch them from
`https://www.svgrepo.com/show/<id>/<slug>.svg` — `/download/` answers 429 to
anything but a browser session. Rasterise with svglib + reportlab + rlPyCairo
to a 256px PNG that is white with the drawing in its alpha channel, drop it in
`Assets/Resources/Icons`, and write the `.meta` with `textureType: 8`.

## What was added, and why

**Note editing.** Ctrl with the arrows walks the selection between anglelines
and beatlines; Shift turns it by a fixed 5 or 1 degrees; Alt mirrors it. A
group always moves rigidly, anchored on the note nearest the judgement line,
because a selection that re-snaps each note individually loses the shape that
made it worth selecting. Everything is one undo entry per gesture.

Snapping works in the **note's own degrees**, not in on-screen degrees. The
anglelines are drawn rotated by the camera, and converting through that
rotation left the camera's angle inside the stored value: notes landed a few
degrees off whenever a rotation motion was running. Degrees are also kept
inside 0-360, since the accumulated camera rotation used to leak into them and
write things like -2430.

**Motions on the timeline.** Right-drag moves them, right-drag on an end
stretches them, and the whole selection follows. The right button was chosen
because the left one already selects motions and scrubs the timeline. Only
timing is ever written, so ease, origin and destination survive a resize.

**A row added below the three is not a row until the hit tests say so.** The
left-drag that scrubs the timeline was bounded by a literal 120, the foot of
the three rows that existed, while the pointer readout beside it used
`LimWaveformManager.StripTop`, 150. The transparency row sits between the two,
so it showed a timing and then ate the drag. Both now measure against
`MotionRowsDepth`. Anything else that asks whether the cursor is on the rows
belongs on that constant too, and a clone of a row should keep its
`LimMouseOverHint`: the only hint on these rows is `Timeline_Scroll`, which is
about the timeline, not about the row it was copied from.

**The transparency motion (type 14) is Rotation's twin, file for file.** An
earlier attempt at it left Unity unable to compile, so it was rebuilt by
following every place Rotation is handled and giving each one a transparency
counterpart, compiling after each block. To audit it, count per file the
lines naming `LanotaCameraRot`/`case 13`/`ComponentMotionMode.Rotation`
against `LanotaCameraTrs`/`case 14`/`ComponentMotionMode.Transparency`; the
gaps that remain are deliberate: Rotation's operations live in
`LimOperationManager.cs` while Transparency's are in
`LimOperationManagerTransparency.cs`, the Gizmo is spatial and does not apply,
and `OperationAdd` in Favourites is dead code. `Schwarzer/Chart/Lanota` only
imports BMS and Arcaea charts and needs nothing.

- It is a **destination**, like a type 11 horizontal: each motion takes the
  ring from wherever it was to its own `ctp`, 0 to 100, starting from 100.
  `LimCameraManager.CalculateTransparency` walks them the way the other three
  are walked. A new one is created at the ring's current value, so creating
  it changes nothing.
- **The tuner is five sprites under the Tuner object**: Background, Border,
  JudgeLine, Arrow, Core. Notes are not its children and are never touched.
  They are found by name and their scene colours kept, so the fade always
  starts from the original. Nothing else writes their colour: the skins only
  swap sprites (Ritmo and Física two, a TunerSkin folder all five), and the
  theme does not touch SpriteRenderers.
- **Background opacity is `TunerBackgroundAlpha`, an absolute 0 to 100,
  default 60** (the scene has 0.603, not the 0.7 it is often remembered as).
  The `TunerBackgroundOpacity` an earlier attempt saved meant a *scale* of
  0.603 and defaulted to 100; it was renamed rather than reused so that a
  stale 100 in someone's preferences file is ignored instead of read as a
  solid background. Newtonsoft skips unknown fields on load.
- `ComponentMotionMode.Transparency` was inserted **before** `Multiple`,
  shifting its number. Safe only because the scene stores the inspector's
  `Mode` as 0 (Idle) and nothing casts the enum to int. Check both before
  adding to any enum a serialized field uses.
- The Creator's motion buttons are **30** apart, not the 35 the later rows
  use, so Create Motion (Transparency) steps by `CreatorButtonStep`.
- `InstantiateSingleTransparency` builds the fourth row itself if a chart
  arrives before the TimeLine's `Start` has run.

**Moving the tuner.** Ctrl and a drag on the core move an offset added on top
of whatever the chart's motions say; the chart itself is untouched. With
exactly one horizontal motion selected, releasing writes where the ring was
left as that motion's destination. Grabbing the core first parks the playhead
just past the end of that motion, so what is on screen **is** the destination
being placed — and a fraction past it, because a motion created from the
Creator lasts ten microseconds and landing a hair short shows the motion not
yet started.

**Grid.** A pane hung off the camera at a fixed distance. Measuring it from
the live camera height made the lines stretch and thin as a vertical motion
moved the camera. Because the pane is nearer than the ring plane, grid
positions are scaled out to that plane before snapping.

**Favourites.** Angleline patterns and groups of notes or motions live in the
preferences file, and export/import writes them as one JSON file so a set can
be passed to someone else. Import **adds**; it never replaces, because losing a
list to a misclick is worse than a few duplicates.

**Waveform.** Rewritten around an envelope: the quietest and loudest sample in
every millisecond, worked out once while the song loads by reading the clip in
64k blocks. The old code kept every sample of both channels in two `List<float>`,
some eighty megabytes for four minutes, which is what the "32-bit memory limit"
warning was about, and then redrew it every frame by taking one sample in every
N, which draws decimated noise rather than a waveform. The envelope is a couple
of megabytes whatever the sample rate, and the picture is only rebuilt when the
view actually moves.

The strip is one waveform rather than one per channel, and it no longer follows
the playhead: it is a map of the song with its own zoom, so the wheel over it
does not touch the TimeLine's scale, and a click or a drag moves the playhead.
While the pointer is down the view is held still, or centring on the playhead
would drag the song out from under the cursor. The frozen value has to be read
**before** the flag is raised: `ViewStart` answers with it once the flag is on,
so freezing first captured a zero and every drag walked the beginning of the
song. Sync Both replaces both the span and the start with the TimeLine's own,
so the two windows line up; the strip's own zoom is left untouched while it is
on, which is what gives it back unchanged afterwards. The right channel's LineRenderer
was kept and reused for the playhead line, which is why the second row is only
hidden and not removed.

**Theme.** Only the flat neutral greys the windows are made of are repainted,
about 150 Images out of 900: they are the large surfaces, and nothing else can
be remapped without knowing what sits on top of it. Text and the white widget
parts are left alone in the dark theme, where white on darker grey only reads
better; the light theme also darkens white text and white icons, which would
otherwise disappear into pale chrome. Every repaint starts from a recorded
original, and controls first seen while a theme is on are **not** recorded,
because a copy of an already painted control would be recorded as if its tint
were its own colour and would then be tinted again. That left late clones
unrecorded until the theme was next switched through Default, which recorded
their painted colour as their own; `LimThemeManager.Adopt` and `Paint` close
the gap, and "How UI is added here" says when to use them.

**Fanning a run.** Ctrl with a drag reshapes the selection instead of moving
it. **Both** ends of the run are pinned, the first note and the last, the
dragged note follows the pointer, and every other note is placed by its own
timing on the line joining the dragged note to the end on its side of it.
Dragging an end therefore swings the run as one line and dragging from the
middle bends it into a >. Nothing is extrapolated past an end: a first attempt
anchored only the far end and carried the line onward, which made a middle note
behave exactly as if the last one had been dragged. Only degrees are written;
letting the timings move as well would shift the very lines the shape is
measured against. **Create Group Ease** is the
same idea with a curve instead of a line, sharing the camera's own ease table
so that an ease number means one thing everywhere. Its sweep is added up note
by note, because degrees are stored inside 0 to 360 and a run travelling three
quarters of the way round reads as a quarter the other way if only its ends are
compared.

**Number keys.** 1 to 5 choose what Click To Create will place; Shift and 1 to
4 choose how big it will be, sizes 0 to 3 in the order the Size list reads;
with the tool off, 1 to 4 resize the selection, 4 being the default size 0. The
Type list is numbered from 1 so that the keys and the list agree, while the type
written into the chart is still Lanota's own 0, 2, 3, 4, 5 through
`ConvertValueToType`.

**Rail notes.** A rail is a head (`Time`, `Degree`), a `Duration`, and a list of
joints each carrying `dTime` and `dDegree`, the step from the joint before it.
The **last joint in the list is the end of the rail, not a bend**, which is why
it is the one joint with no object drawn for it and why a rail with a single
bend has two joints; its absolute time has to stay equal to `Time + Duration`,
and `Jcount` has to stay equal to `Joints.Count` or `SetHoldNoteJCount` throws.
`LimOperationManagerRail.cs` holds the reading of a rail and the three gestures
— Ctrl-drag on the end, J for a joint, Shift + S for a cut —
`LimOperationManagerRailSegment.cs` the Creator's Segment Rail Note, and
`LimOperationManagerRailGuide.cs` the two marks drawn for them. Segmenting
reads the degrees **off the original's curve** at each boundary rather than
sharing the turn out evenly, which is what keeps an ease, and reads a few
points inside each piece too so a piece is only drawn straight when the
original was straight along it.

Undo for a rail is written as a **whole shape**, `Duration` and every joint
together (`RailShape`), because a cut or a bend changes both at once and putting
one back without the other leaves a rail that ends in two places. Degrees along
a rail are **never** wrapped into 0-360: a joint's step is a running total and a
rail is allowed to wind round the ring, so `LimMathUtil.NormalizeDegree` belongs
to notes only. A rail is about six degrees wide wherever it is on the ring — it
is drawn a hundredth of its distance from the middle across — so one angle
serves as the whole hit test, near and far alike.

The joint prefab ships **without a collider**, because the game never had to
pick one up; `LimHoldNoteManager` adds one at instantiation, on the root, where
the instance id selection compares against lives. A cut is pulled at least a
millisecond clear of the joints either side of it, or the Ctrl snapping would
happily ask for a step of no time at all.

The cut is **Shift + S**, not S and not Ctrl + S. Ctrl + S saves the project
and S on the timeline splits a motion; Ctrl is also what makes the cut line
stick to the beatlines, so binding the cut to Ctrl + S would have taken the
save shortcut away exactly when it was most wanted.

**The tuner camera is below the ring**, at y = −20 looking up (+y), which is
why `TunerScreenToWorld` passes `-camera.position.y` as the distance. So
−y is **towards** the camera: lifting something in +y to put it in front of the
rail buries it behind instead. `RailLiftDirection` reads the sign off the
camera rather than assuming it.

**Undo never had a limit**, but `Undo` ran the entry before moving the
position, so an entry that threw left the stack parked on it and every later
Ctrl + Z re-ran the same failure — which looked exactly like a cap on the
number of steps. `RunOperation` now catches, logs and moves on.

**Time Groups.** Modelled on ArcCreate's timing groups, which is GPL-3.0:
behaviour was taken, code was not. Every note carries a `Group`; 0 is the
base group, the chart's own, and is never stored as a group. A group has its
own scroll speed list, opacity keys, rotation keys, a fade by distance and a
tint. It lives in `LimTimeGroups` (static: the tuner, Creator, clipboard and
inspector all need it), `LimOperationManagerTimeGroups.cs` and the Inspector
tab in `LimInspectorTimeGroups.cs`.

*Movement.*

- A note's position is `100 - distance`, where distance is the scroll list
  integrated from now to the note's time, and it shows while that sits
  between 20 and 100. A group is just **another scroll list**, so negative and
  stopped speeds need no special case: the same integral runs backwards. The
  show and hide rules were left untouched; only the list changes. A group
  stopped from the start leaves its notes on the judge line, hidden; notes
  that "freeze" mid-path come from stopping a group that was moving.
- `LimTimeGroups.Load` runs in `LimTunerManager.Initialize` **before** the
  note managers, which ask it which list each note moves by.
- **`LimScanTime` culls notes by the chart's own scroll speed.** A group note
  run through it is hidden at the wrong moments, so notes that
  `UsesOwnScroll` skip it and are placed every frame. Base notes still go
  through it unchanged.
- The hold manager's four `CalculateMovePercent` calls all read `NoteScroll`,
  set per note by `UseScrollOf` at the top of the loops that place notes and
  cleared after; `IsBackwarding(Note)` replaces the global flag for groups.

*Effects.*

- Evaluated once a frame (`EvaluateFrame`), with the camera's own ease table,
  and applied where the managers already write colour and rotation. Opacity
  keys are destinations from 100, like the transparency motion; rotation keys
  add up, like a rotation motion. The fade by distance is in per cent of the
  path: 20 to 100 in the tuner's own percent.
- The hold manager's `ViewRotation` adds the group's turn wherever it used
  the camera's, and subtracts it again before writing a joint's `aDegree`,
  which stays in chart degrees for the rail tools.
- **A highlighted (Combination) note is two sprites**, the note and a `Light`
  behind it, and `Note.Sprite` is simply the first one `GetComponentInChildren`
  finds, which on a highlighted rail is the Light. Anything that fades or
  tints a note has to reach every sprite of it (`FadeExtras`), or the glow
  stays and gives away where the note is.
- A faded or tinted rail body is drawn with a copy of its material on
  `Sprites/Default`, coloured through the LineRenderer's start and end
  colours, because neither body shader takes a colour: the pressed material is
  `Unlit/Transparent` (its texture only) and the unpressed one `Unlit/Color`
  (its flat blue `_Color`, no texture at all). The copy takes the texture only
  if the source shader has `_MainTex` and the colour only if it has `_Color`,
  which reproduces both exactly. Plain rails keep the shared material, and a
  rail that leaves its group gets it back.

*Editing.*

- **The pointer is always read against the chart's speed**
  (`LimTunerCoordinate`), the rail gestures included, since a stopped group
  has no position that tells one moment from another. The "Group effects"
  switch (the `Preview` flag in code) turned off draws every note as the base
  group moves, unturned, with hidden notes as faint ghosts (`GhostAlpha`):
  that is the supported way to place and drag notes of a group that moves
  differently. The first version labelled it "Preview: Off"; the user turned
  it off expecting to see the effect and then reported that group speeds did
  nothing. Label a switch by what it does.
- New notes take `LimTimeGroups.ActiveGroup` (create, Click To Create, paste,
  Favourite Groups, Copier); notes derived from another keep its `Group`
  (cut, segment, merge, convert, change type). `DeepCopy`, `ToHoldNote` and
  `ToTapNote` carry it, and so must any new code that builds a note field by
  field from another one.
- Scroll speed operations find the list an entry belongs to (`ScrollListOf`)
  instead of assuming the chart's, which is what lets the Scroll Speed tab's
  row prefab edit any group's list.
- Deleting a group deletes its notes with it (the user's call: everything
  related to the group goes). One undo restores the group and the same note
  objects, which `AddTapNote`/`AddHoldNote` re-instantiate.

*Saving.*

- `tg` on each event, left out when 0, and a top-level `timegroups` list,
  left out when empty, so a chart without groups is byte-for-byte what it
  was. A `tg` naming a group the file lacks is read as base.
- `fadein`/`fadeout` are nullable in the file, so a group saved before they
  existed reads as off rather than as 0, which would fade everything out.

*The tab.*

- The tab and panel are clones of the Default tab and the Scroll Speed
  component; buttons are the scroll speed row's delete button with its icon
  replaced by a label. The panel rebuilds on `LimTimeGroups.Changed`, which
  renaming and editing a key deliberately do not raise, or the field would be
  destroyed under the cursor while typing; keys are re-sorted in the data and
  the rows keep their order until the next rebuild.
- Each row shows how many notes the group holds, refreshed every frame while
  the panel is open, so moving notes into a group shows at once that it
  worked. That count exists because the first version gave no sign either way.
- Rows are laid out in 500 wide units scaled to the window, **down only**.
  The user's Inspector was once saved 2431 wide, its right edge far off the
  screen: every button came out three times wider and out of sight, and the
  tab looked broken. `RestoreEditorLayout` now also cuts a window saved past
  the screen's right edge back to the room it has, and Reset Layout asks
  first (`LimTopMenuResetLayout.cs`, the shared message box's OK/Cancel):
  one stray click had thrown the user's arrangement away.
- **The language flag** (`LimTopMenuLanguage.cs`) sits left of the scene's
  Plugin cell, found by name (`TopMenu/Plugin`; its `PluginPanel` field is
  unassigned and its words are set after Start). Flags are in
  `Resources/Flags`, keyed by the language pack's name in
  `LanguageFlagFiles`; a pack with no flag shows its name. Choosing one does
  what the Preferences drop-down does (`SetLanguage` and the preference).
  Its texts take the Plugin button's own Text as reference: the group's
  first Text is inside the hidden drop-down and too big for a 30 row, which
  made the names vanish.
- A group's colour reaches the notes and the highlight glow separately
  (`ColorNotes`, `ColorHighlight`; `colornotes`/`colorhighlight` in the file,
  written only when off). Which sprite is the glow is asked **by name**
  ("Light", `LimTimeGroups.IsHighlight`, cached because `name` allocates),
  never by position: a highlighted rail's `Note.Sprite` is its Light. The
  ticks are copies of the Basic component's Combination toggle, its label
  dropped and ours put beside it.

**Auto Highlight** treats notes less than **8 ms** apart as simultaneous,
chaining runs, whatever their group. It compared times rounded to four
decimals, and a chord placed a hair apart could straddle a rounding boundary.
The 8 ms comes from the user's 184 charts: gaps between neighbouring notes
pile up below 5 ms (over two thousand, all left unhighlighted by the old rule),
nearly vanish from 8 to 13 ms, and only become rhythm again from about 16 ms.
It now rebuilds only the notes whose highlight changes, as one undo; it used
to re-instantiate every note in the chart and push an undo step for each.

**Analyzer.** Takes Chart Convert's place in the top menu. The scene's
button and drop-down are reused at runtime (`LimTopMenuAnalyzer.cs`): the
two entries get fresh `onClick` events, so the old importer calls go with
them. `LimChartConverting` and `Schwarzer.Chart` stay, unreachable, because
the scene holds a `LimChartConverting` component and deleting its script
would leave a missing-script warning. The Chart Convert keys are gone from
the language packs; the `ChartConverting_*` ones stay for that dead code.

*Detection* is `LimBpmAnalysis`, plain C# with no Unity in it, so it runs on
a worker thread and can be checked outside the editor. The clip is read and
mixed down on the main thread (only it may touch a clip), a 30 ms slice per
frame. The pipeline, and why each step is there:

- A spectral-flux onset envelope (1024-point FFT, hop 128, about 5.8 ms at
  22 kHz), plus a second one from the bass bins alone.
- The tempo comes from autocorrelation with a prior centred on 155 bpm, then
  gets pinned down by folding the envelope onto one beat. The fold is summed
  over 20 s chunks rather than taken over the whole song, because a song
  whose beat jumps once (DeathMoon, BigDaddy) pulled a whole-song fold to
  159.96 instead of 160. The result is rounded to a whole or half bpm when
  that scores within 3 % of the best.
- **The phase takes its position from the full-band envelope and its choice
  from the bass.** The full band places an attack to a few ms, but the
  offbeat often folds up nearly as high as the beat. The bass knows which
  one is the kick, but its onsets arrive 40-50 ms late and loosely. Weighting
  the two together fixed some songs and broke others (Xepher went half a
  beat off), so the bass only picks among the full band's tall peaks.
- The first line goes on the first grid beat once the level rises above
  0.5 % of the song's RMS, less 15 % of a beat of allowance, because the
  onset is seen before the level has risen. Charters put the first line where
  the audio starts, soft intro or not; several songs carry 5 s of silence.
- Tempo changes come from 10 s windows. They are counted only when another
  tempo beats the song's own by 1.25x and lasts 15 s. Half and double count
  as the same pulse, and the octave nearest the song's tempo is kept. A
  boundary survives only if each side's grid fits its own side at least 2x
  better than the other side's grid carried over. The weakest boundary is
  merged first, and the merge keeps whichever grid fits the whole better.
  Real changes scored 2.9-18; triplet passages and ambient intros stayed
  under 1.6. **Phase jumps at the same tempo are not detected**: in real
  songs an offbeat-heavy passage looks exactly like a half-beat jump, and
  trying produced a popup on over half the songs.

It was measured on 32 levels, each a folder holding its song and chart, with
decodable audio. `Tools/BpmCheck/check.sh` repeats the whole
measurement: it decodes the songs with Python's `soundfile` to 22 kHz mono
floats, compiles `LimBpmAnalysis.cs` on its own with Unity's Roslyn, and
scores the result against each chart's bpm list.

- **Tempo:** the same bpm as the chart in 25. Double in 5, all charts that
  count a fast song at half. Two odd songs miss (Overthinker,
  Principio Catastropha).
- **Phase:** 22 of the 30 within 10 ms of the charter's line.
- **False popups:** one, on one of those two odd songs.
- **Real changes:** five songs with their second half resampled 0.8x-1.33x
  were all found, the switch within 0.3 s.

Run it again after touching any threshold.

*Applying* goes through `LimOperationManager.ReplaceBpmList`. That is one
undo step holding copies of the whole list before and after, because the
existing bpm operations never recorded undo. The list is refilled in place:
it is the chart's own object and other managers hold it. Entry 0 is the base
entry at -3, and its lines start at 0 whatever its time, so the base takes
the first section's bpm.

*Manual* is a copy of the Preferences window, emptied, with buttons and
fields copied from the Media Player's. It is added to `LimWindowArranger`,
or it would keep the Preferences window's place in the order. Taps are timed
on pointer down, since release times vary, and on a stopwatch from the moment
the song starts, since `AudioSource.time` moves in buffer-sized steps. The
bpm is the least-squares line through every tap, and a 2 s rest starts a new
count. The circle is a sprite drawn at runtime, because uGUI's round sprites
are editor-only resources. The question about tempo changes is a copy of the
message box's Canvas with wider buttons, told apart by their persistent
method names (`OK`, `Cancel`). The shared box is left alone.

**UiTweak.** Flowaria's community plugins for Lanotalium 2.5 (a dll of four,
plus AUTO_FlickArrow), rewritten into the editor with Flowaria's permission,
each part behind a switch in its own top menu (`LimTopMenuUiTweak.cs`):
`LimNoteEffects` (hit effects and combo counter), `LimFlickArrows`,
`LimJudgeLineOrnaments`, `LimPlaySceneWave`, `LimTunerSkins` ("Custom Tunes")
and `LimLanotaHeader` with its score, set up by `LimTunerManagerVisuals.cs`.
`LimChartClock` holds the two readings they share: the combo at a moment and
the beat phase. Its art is its own three asset bundles,
built with 2018.3.0, in `StreamingAssets/UiTweak`: the particle bundle holds a
compiled shader with no source, so they could not become project assets.
They carry no scripts, their layers are the tuner's 8-13 and their sorting
layer ids match this project's, so they render as they did under the plugin.
The dll was read with Mono.Cecil (Unity ships it under `il2cpp/build`); the
plugin's layout figures, colours and effect choices are kept as it had them.

- **The bundles cannot be open together.** Unity refuses the second as
  "another AssetBundle with the same files is already loaded"; the plugin
  never met it because it closed each one straight after reading it.
  `LimUiTweakAssets` does the same: open, read everything, `Unload(false)`.
- Effects are children of the Tuner object, as in the plugin, so they turn
  with rotation motions and take its 2.25 scale. A tap's effect is placed by
  the note managers' own formula (radius 10, `Degree` + camera rotation +
  group rotation) rather than read off the note, which may be culled. A
  hold's follow its head, which the hold manager keeps on the judge line.
- **Notes fire only when playback crosses them**, in (last frame, now].
  The plugin fired every note behind the playhead, so any jump set off a
  burst. A jump is a step larger than the frame explains (3x the frame at the
  playback speed, plus 0.1 s for the audio clock's buffer steps). Pausing
  freezes effects through simulation speed and animator speed, not
  `Pause()`, which a system told to stop emitting would undo on resuming.
- **Combo** is taps passed plus, per hold, its head, a tick every 30/bpm
  seconds (the bpm where it starts) and its end: the plugin's count. The
  eight places of the counter are picked by the hit's direction in the
  Tuner's own frame, which matches the plugin's degree arithmetic and also
  covers time group rotation. A new counter copy is sent to its animation's
  last frame, or it flashes "Harmony!!" in all eight places.
- The judge glow's animator is stopped and its time set every frame from the
  chart's beat (the first bpm counts from 0, like the beatlines). The plugin
  ran it at bpm/60 from whenever it was created, so the pulse had a random
  phase. The ornaments fade through their materials, since their animations
  write the sprites' own colour.
- **Skins are drawn at the size of the part they replace**, pixels per unit
  scaled by width against the scene's sprite. The plugin used 100 for all,
  which made the 1024 px cores of the "HD" skins five times too big. File
  names are matched in any case ("Decimal" has background.png). A skin is
  drawn over Ritmo or Física; choosing either in the Skin panel clears it,
  which is why `UseRitmoSkin`/`UseFisicaSkin` (wired in the scene) now clear
  the preference and painting moved to `PaintBuiltInSkin`. Opening the panel
  re-applies the loaded sprites without reading the files again.
- **The header's colours are written every frame while it is on**, because
  the theme records its grey as chrome and the light theme darkens white
  icons. Turning it off hands each colour back through
  `LimThemeManager.Paint` with what `OriginalOf` gave when it was put on.
  Nothing is reverted in `OnDestroy`: during a scene change `Paint` found no
  theme manager and created one, which Unity then reported as an object left
  behind by the closing scene. Difficulty and level are `Difficulty` (0
  hidden, then Whisper, Acoustic, Ultra, Master; default Master) and `Level`
  on the `.lap`'s `LanotaliumProject`. The level field covers only the
  number's end of the badge, so the rest of the badge stays clickable.
- **The menu** is a copy of Analyzer's button and drop-down placed right
  after it. Its entries are copies of one of Chart Convert's rows with a tick
  column added; the drop-down closes on pointer exit like the others, which
  is why the skin list is its child (moving onto a child is not an exit).
  The switches were first rows in Preferences and moved here at the user's
  request.
- Flowaria later sent the plugin's Unity project (prefabs, shaders with
  source, animations). From it `Resources/UiTweak` holds the flick arrow
  pictures and the 1024 px core with their own metas, and the two rail
  pictures turned a quarter round; the rest still comes from the bundles.
  Its timeline marker was left out: the timeline's own diamond does that job.
- **HD rails** swap the hold note manager's two shared rail materials
  (`HoldTouch`, `HoldUntouch`) for copies of the pressed one with the new
  pictures, and swap them back when off; rails already drawn are repainted.
  Flowaria's "Affine UV fix" shader divides by a second UV set that a
  LineRenderer never has, so it is not used; the pictures ran along their
  height for it and were rotated to run along the width, as the editor's own.
- **HD core** is part of `LimTunerSkins`: it fills the Core whenever the
  skin in use brings none, sized like any skin part. AUTO_CorePatch also
  tinted it to 0.7; left out, since the transparency motion owns the ring's
  colours. The refresh key includes the switch, and the sprite is made again
  on every rebuild, because the old one is destroyed with the rest.
- **Perfect Purified**: `PP.anim` in the project is empty, so the animation
  is made in `LimPerfectPurified` from the sprites and the all-combo sound.
  It fires when continuous playback crosses the latest tap or hold end, runs
  on real time and is taken off if the playhead goes back before that.
- **Ready** (off by default) needs no silence in the song: `LimReadyIntro`
  listens on the media player's static `OnPlay` (remade in the player's
  Start, hence `LimOnPlayHook`), and a start at time 0 is paused again inside
  the same call, before audio is out, while the four-second animation plays;
  then it plays for real. The project's own autoplay on opening happens
  before the tuner is initialized and is not caught. The animation is
  Flowaria's ReadyScale and ReadyAlpha keys.
- **Flick arrows** are the game's: a fixed square window from the note's
  middle towards the core (the root's +y), the chevron texture scrolling
  through it and wrapping round, so a piece leaving one edge comes in at the
  other. AUTO_FlickArrow did exactly that, but its sprites had a tight mesh,
  which showed the scrolling picture only inside a chevron-shaped hole, in
  pieces; a first rewrite here then moved a whole arrow outside the note,
  which the user's screenshots of the game showed to be wrong. Now the
  sprite is re-made at runtime with a full rectangle over the same texture
  (wrap Repeat), on UI/Default (it applies `_MainTex_ST`; Sprites/Default
  does not), side 0.42 of the note's width. Four shared materials hold four
  phases. Added the frame after a note exists, so rebuilt notes get it back;
  `LimTimeGroups.ForgetExtras` makes a group's fade include it.
- **The wave** is the plugin's unused "ScreenSpectrum": `playscene_0` tinted
  (0.58, 0.67, 1, 0.39), Flowaria's figures turned into fractions of the
  canvas height, its one-second bob mapped onto one beat. It and the specks
  live on the tuner's FullScreenCanvas, drawn in full screen and in the
  window. **Screenshots taken by swapping the tuner camera's target for one
  Render misplace anything on that canvas**, which keeps the layout of the
  target it last laid out for: assign the target for a few frames first.
- **The header is laid out from a screenshot of the game**, in fractions of
  the bar (`Layout`, run whenever the bar's size changes): the top bar's
  dividing lines at 57.3 and 71.5 per cent of its width, the pause button
  1.37 bars tall centred at 5.3 per cent, the name from 11.1 per cent, the
  badge centred at 64.3, the right section 74.4 to 95.5; text sized from the
  bar's height, never scaled by a transform (that blurred the badge). The
  bar itself is made the game's height, 5.95 per cent of the screen's width
  (at most 14 per cent of the height), overriding the head manager's 7.7 per
  cent of the height every frame after it sets it. A first version kept the
  plugin's 30 unit tall name box, which best fit then shrank: the user saw
  the name small and thin. Lettering gets `LimTextGradient` with four stops
  sampled off the game's score figures (199,181,157 / 222,199,172 at 0.43 /
  177,156,136 at 0.71 / 138,117,106), over the whole line, and a faint
  `Outline` for body. **A letter is one quad**, which only blends its four
  corners' colours, so the gradient cuts every letter into bands at the
  stops; without that the middle highlight never showed. The badge lays the
  word and the level out itself (`ArrangeBadge`, from their preferred widths,
  0.35 bars apart, centred), the word in the plugin's face at 0.33 bars and
  shaded like the rest. A level ending in "+" draws the header face's own
  "+", shaded like the figures, sized so its ink is 0.58 of the figures'
  height and placed by ink on both sides (`Ink`, a `TextGenerator` run over
  each), not by font size or line box; the tuck and raise constants are
  tuned by eye, as the glyph's box is roomier than its arms. The badge's texts carry no
  `Outline`: at their size the pale rim only blurred them. They keep the
  shading of the score and the title (a paler set measured on the game's
  badge was tried; the user wanted the same as the rest). Thickening the
  strokes, by synthesised bold or by a second copy of the mesh, was tried
  and dropped: the user read it as bold and too bright. **Match sizes on
  a screenshot of the game scaled to the same bar height**: the score came
  out a quarter too tall and the glow far too tall (now 0.6 bars) until
  measured that way. The badge word is as tall as the figures and stands on
  their line, which the user asked for twice: its size is the figures'
  times `NameToFigures`, a ratio measured in pixels, because the capitals'
  glyph boxes carry room the ink does not and sizing by them left the word
  15 per cent short. The score's face
  is set narrower than the game's at the same height, so it is spaced out
  with `LimTextSpacing` (added before the gradient, which cuts characters
  into more quads). The stops' ends are pushed past the readings, because
  a letter's partly covered first and last rows come out duller than their
  stop. The glow sits a little left of and below the lettering, as in the
  game (`GlowLeft`, `GlowDown`); the lettering is its child, so it is moved
  back by the same `GlowShift`. The badge sits on whole
  pixels and is an even number of them wide. The bar is no longer tinted
  grey as the plugin had it: that halved the gold rim and dividers, which
  the game shows at the picture's own brightness. Arial's "+" and a drawn four-pointed
  star were both tried and read as too heavy and too bright next to the
  figures; the user asked for the face's own. The input field's own text
  only shows while typing. Difficulty 5 is the project's
  own (`DifficultyName`, `DifficultyColor` as RRGGBB, read with
  `LimTimeGroups.TryParseTint`), asked for with an EasyRequest form on a
  right click (`LimRightClick`) or from the menu; the form's labels are
  attributes, so there is a Spanish and an English form class. The form is
  dressed by `LimColourForm`: it starts the EasyRequest coroutine itself, so
  the rows exist when it returns, adds a swatch beside the colour field
  (`LimSitsLeftOf`, placed every frame: the new row has no size on its first
  frame), a `LimColourPicker` below the rows and an Apply button copied
  from Confirm, and removes all of them when the form closes, since the
  form is shared by every request. The colour is shown as `#RRGGBB`;
  `TryParseTint` and `UseCustomDifficulty` drop the `#`. The progress line
  wears a drawn round flash (`MakeSpark`) at its right end.
- **Compact highlight** scales each highlighted note's "Light" sprite by
  0.94 x 0.8, keeping the original to put back; the prefabs' 1.55 x 1.4 made
  a wide halo the game does not have. The designer's field is an
  InputField, which shows only the part of its text that fits and scrolls
  the rest, so best fit never shrinks it; `FitDesigner` measures the whole
  name with the text generator instead. The score is the share of the chart's
  combo reached, out of 1,000,000, with `LimTextGradient` (from Flowaria's
  EffectTest) shading its figures.

**Volume.** A copy of the Pitch row, set on the song's AudioSource alone. It
goes to 125 per cent because an AudioSource amplifies above 1; the two modes of
the Media Player set their own window height, so both had to grow by the height
of the row.

## Conventions worth matching

- Locals and parameters are PascalCase here. It is not the usual C# style, but
  it is this codebase's style.
- Comments explain **why**, in prose, and are reserved for the parts where the
  reason is not visible in the code. Match the density around you.
- New behaviour goes in a new partial file of the class that owns it, rather
  than growing the existing file.
- Every gesture is one undo entry, built with `Lanotalium.Editor.OperationSave`
  and pushed with `AddToOperationSaver`.
- Guard new shortcuts with a typing check, and against the other pointer
  gestures (`IsNoteGestureInProgress`, `IsPasting`, `IsPanningTuner`).

## Current state

Unity **is** installed, at `D:\Archivos de programa\Unity\Editor\Unity.exe`. An earlier
session looked under `C:\Program Files`, did not find it, and wrote here that it
was missing, which is why most of this branch was checked only structurally.
The editor's log at `%LOCALAPPDATA%\Unity\Editor\Editor.log` lists every
compile error in an assembly and is the quickest way to see the real ones,
with no need to run anything while the editor is open. The whole branch now
compiles clean under a headless run (three assemblies, exit code 0), so a
compile is a real check again rather than a hope; run one before handing work
back. The user testing each change in the editor still carries the rest:
uGUI layout, colours and anything that moves on screen cannot be checked
headless. Screenshots from the user drove the fixes for the BPM opacity row,
the grid's draw order, the motion destination, the Reverse button's row, the
Time Groups switch and the theme on cloned controls; ask for one whenever a
change is visual. The user works in Spanish and tests in the Dark theme.

Positions of runtime-built controls were worked out by reading the scene's
anchors rather than by looking at the result; a few needed a second pass. When
adding UI, expect to adjust after seeing it.

**LineRenderer widths are in world units, not canvas pixels.** This canvas is
about six pixels to the unit, so a width of 1 drew a six pixel line and the
playhead came out as a bar. `LimWaveformManager.InPixels` multiplies a width in
pixels by the strip's `lossyScale`, which is the conversion, and the pin at the
foot of the playhead is a width curve along that same line rather than a second
object: one line cannot be drawn out of step with itself.

A whole-UI restyle was attempted once before this branch and had to be
reverted: uGUI draws no text outside play mode, so it was done blind. If theming
comes up again, do one window at a time with real screenshots in between.
