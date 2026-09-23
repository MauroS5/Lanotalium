## Notes

- **Ctrl + ← / →** moves the selected notes to the previous or next angleline; **Ctrl + ↑ / ↓** moves them to the next or previous beatline. With nothing selected, these still walk the timeline as before.
- **Shift + ← / →** turns the selection 5 degrees, **Shift + ↑ / ↓** turns it 1 degree, ignoring the anglelines.
- **Alt + ← / →** turns the selection over in time, so the last note becomes the first; **Alt + ↑ / ↓** mirrors it across its own middle degree.
- **Flip (Horiz.)** and **Flip (Vert.)** buttons in the Creator do the same two mirrors as the Alt shortcuts.
- **Select Even** and **Select Odd** thin a selection down to its even or odd members, counting in playing order.
- A group always travels as one block, keeping its shape and its spacing, with the note nearest the judgement line deciding where it lands.
- The paste preview is now see-through, so it cannot be mistaken for notes already placed. Arrow keys flip it before you drop it.
- Pasted and dragged notes now land exactly on the angleline or beatline, with the camera rotating or not.
- Note degrees stay inside 0-360 instead of drifting to values like -2430 after several rotation motions.
- **1, 2, 3 and 4 resize the selected notes** while Click To Create is off; 4 is the default size. The whole selection is one undo.
- **Repeat paste**: the preview stays up after a copy is dropped, so the same pattern can be laid down again and again. Right click puts it away.
- **Shift and the wheel over the Tuner** walk the chart beatline by beatline, up for forward and down for back, without reaching for the arrow keys.
- **Ctrl and a drag** fan a selected run out: the first and the last note stay where they are, the note you pull follows the pointer, and the rest land on the straight lines joining the three. Pulling an end swings the whole run; pulling one from the middle bends it into a >. Only the degrees move.
- **Create Group Ease**, under Create Catch Rail, bends a selected run along an ease curve from its first note to its last. The field takes the same 0 to 12 a motion takes, and 0 straightens the run again.

## Motions and the timeline

- **Right-drag a motion** to move it along the timeline. It sticks to the nearest beatline, hops over its neighbours when dragged far enough, and Ctrl moves it freely.
- **Right-drag either end** of a motion to stretch or shorten it. Only its timing changes: ease, origin and destination stay as they were.
- With several motions selected, moving and stretching apply to all of them, keeping their spacing and their durations.
- **Shift + click** selects every motion between the one clicked before and this one.
- **S** splits the motion under the pointer in two, at the pointer. The halves are identical but for their timing.
- Motions shorter than 0.05 s now show a small diamond, so they can be clicked at all.
- **Invert Values**, a new button in the Motion inspector and **Ctrl + R**, turns a motion's values around: a drop of 20 becomes a rise of 20.
- **Ctrl + C and Ctrl + V copy motions** the way they copy notes: a see-through preview, left click to drop it, right click to cancel.
- With several motions selected, the inspector edits them **all at once**: ease and both control points are written to every one of them.
- **Transparency motion**, a fourth kind of motion alongside Horizontal, Vertical and Rotation, on its own row under Rotation. It fades the tuner itself (background, border, judge line, arrow and core) and never the notes. **Create Motion (Transparency)** in the Creator adds one at the playhead; the inspector shows Timing, Duration, Ease and Transparency, from 0 (gone) to 100 (the opaque ring as it has always been). Each motion takes the ring from wherever it was to its own number, so a new one starts at the ring's current value and changes nothing until edited. The bars are a soft coral, as light as the other three rows, and turn pink when selected, and splitting with S, copy and paste, right-drag moving and stretching, box selection, Favourite Groups and the Copier all work on them as on the other three. **Invert Values** turns a transparency around its middle, 30 becoming 70.
- **The timeline is dragged along by the fourth row too.** The drag reached down to the foot of the three rows there used to be, so the new one was a dead strip that swallowed it. It is measured against the foot of whatever rows exist now, and the row keeps the hover hint its sisters have.

## Waveform

- **One waveform instead of a left and a right one**, always in place along the bottom of the TimeLine. The separator that had to be dragged about is gone.
- It is drawn from an **envelope** worked out once while the song loads, a couple of megabytes instead of eighty: it works in the 32-bit build and costs nothing per frame.
- **The wheel over it zooms it**, from the whole song down to about a second, without touching the TimeLine's own scale.
- **Click or drag on it to move the playhead** there. A pale line shows where the playhead is.
- Ten colours to choose from in Preferences, cyan by default, and that is also where it is switched off.
- The playhead is a hairline with a small pin at its foot, in place of the wide bar it used to draw.
- **Sync Both**, beside Waveform in the title bar, ties the strip's zoom to the TimeLine's: both then show the same stretch of song, starting at the playhead, and the wheel over either moves both.
- Dragging on a zoomed strip now stays in the part being looked at instead of jumping to the beginning of the song.
- The strip is shorter, leaving an empty row above it for the transparency motions to come.

## Moving the tuner

- Hold **Ctrl** and drag the core, the small circle in the middle of the ring, to move the whole tuner around the viewport. Ctrl and a double click bring it back.
- Holding **Alt** as well snaps the middle of the ring onto the grid, landing on a crossing when it is near one.
- With exactly one horizontal motion selected, where you drop the tuner becomes that motion's destination, so the target coordinates no longer have to be typed.

## Grid

- New **Grid** tool between Angleline and Copier: **V.** and **H.** set how many lines cross the background, spread evenly and symmetrically.
- A tick beside each field switches that direction off without losing what was typed, and a slider sets the grid's opacity.

## Anglelines and beatlines

- Angleline patterns can be **kept as favourites**: the heart button saves what is in the field, and each saved pattern gets a row with show, rename and delete. They survive a restart.
- Beatlines are now coloured by where they fall in the beat: blue on the beat, green on the half, orange on the quarters, pink for anything finer.
- Anglelines at 0, 90, 180 and 270 are gold, and those at 45, 135, 225 and 315 are bronze.
- Both, and the grid, have their own **opacity sliders**.

## Favourite groups

- New **Favourite Groups** tool between Click To Create and Angleline, holding patterns of notes and patterns of motions.
- **Ctrl + F**, or the heart button, saves the current selection as a pattern: notes or motions, never both, up to 100 notes.
- Each pattern has **Copy**, **Rename** and **Delete (double click)**. Copying notes raises the paste preview; copying motions lays them at the playhead.
- **Export** and **Import** write and read a `.json` file holding the groups and the anglelines together, so a set of patterns can be passed to someone else.

## Click To Create

- **C** switches it on and off. The shortcut used to be F2 and did nothing at all.
- Switching it on also switches on **Attach To Beatline and Angleline**. Turning either off by hand lasts until the next time C is pressed.
- **1 to 5 pick the kind of note** while it is on: Click, Flick In, Flick Out, Catch and Rail.
- The Type list is numbered **1 to 5** rather than 0, 2, 3, 4, 5, both here and in the inspector, so it reads in order and matches those keys.
- **Shift + 1 to 4 pick how big the next note will be**: sizes 0, 1, 2 and 3, in the order the Size list reads.
- **Keep the button down after placing a rail** and it draws itself: the head stays where it was clicked and the end follows the pointer until you let go, out for length and round for lean, so a rail can be drawn diagonally in one go. A plain click still leaves the one second it always did.

## Rail notes

- **A small yellow dot marks the end of every rail**, so there is somewhere to take hold of, and it is plainly a handle rather than a note left there by accident.
- **Ctrl and a drag on that dot** move where the rail finishes: how long it is and which way it leans, in one gesture, sticking to the beatlines and anglelines like everything else. Shift ignores the snapping, and can be pressed or let go at any point of the drag.
- **J puts a joint wherever the pointer is** on a rail, without changing its shape. Hold Ctrl and it lands on a beatline or on an angleline the rail crosses.
- **Joints are notes now**: click one to pick it up, Ctrl+click for several, then drag them or move them with the Ctrl, Shift and arrow shortcuts. The rest of the rail stays where it was.
- **Shift + S cuts the rail under the pointer in two.** The second half starts at the cut, at the degree the rail had reached, with the same size and the same kind, and carries the joints that were past the cut. Ctrl + S still saves the project, as it always has.
- **On a selected rail a thin red line follows the pointer** across the rail, showing where the cut would land. Ctrl snaps it to the beatlines and anglelines the rail runs across.
- **Segment Rail Note**, under Create Group Ease, cuts one selected rail into as many rails as are typed in, from 2 to 64. They travel exactly the path the original travelled: the degrees are read off its curve, so an ease is kept rather than sliced evenly into straight pieces.
- **Convert To Single Hold Note**, under Convert To Hold Note, joins a run of separate hold notes back into one: the first keeps its head and the rest become its joints, keeping their places and their eases.
- **Set Rail Ease**, under Segment Hold Note, writes one ease to every joint of every selected rail at once. The inspector still edits one joint at a time; this is for a run of rails that all want the same curve.
- **Rail Note Visual Guide** in Preferences switches off both the dot and the cut line.

## Time Groups

- **Time Groups**, a new tab in the Inspector beside BPM, Scroll Speed and Default. Opening it puts those three away, and opening any of them puts it away.
- Every note belongs to a group. **Base** is the chart's own and holds every note there was before; it moves by the chart's Scroll Speed exactly as always and cannot be removed.
- **Each group has a Scroll Speed list of its own**, negative speeds included, so some notes fall while others stop still or travel back towards the core. The list is edited in the same rows as the Scroll Speed tab, and **+ Speed** adds one at the playhead.
- **Edit** makes a group the active one. Whatever is placed next goes into it: Create Tap/Hold, Click To Create, paste, Favourite Groups and the Copier. Notes made out of another note keep that note's group: cutting, segmenting and joining rails, converting and changing type. With a group active, **Create Scroll Speed** in the Creator adds to that group.
- **Move selection here** puts the selected notes into the active group, in one undo.
- **Visible / Hidden** hides a group's notes while editing, the base group's too.
- Each row shows **how many notes** the group holds, so moving notes into a group shows at once that they went.
- **Opacity over time**: keys with Timing, Duration, Opacity (0 gone, 100 fully shown) and Ease, each taking the group's notes from where they were to its value.
- **Group rotation**: keys with Timing, Duration, Degrees and Ease that turn the group's notes round the core, adding up like a rotation motion.
- **Fade by distance**: notes appear gradually from the core until a point of their path, and/or start vanishing at a point and are gone by the judge line.
- **Group colour**: a tint from a row of swatches or any RRGGBB. Two ticks beside it say what it reaches: **Highlight**, the glow of highlighted notes, and **Notes**, the notes themselves (rails and joints too). Leave only Highlight ticked to colour just the glow.
- **Group effects: On/Off**. On, everything above is shown as the chart will look. Off, every note moves with the chart and is not turned, so notes sit exactly where you place or drag them (the pointer is always read against the chart's speed, since a stopped group has no position that tells one moment from another), and notes the chart hides are drawn as faint ghosts so they can still be picked. A selected note is never fully invisible either way.
- Deleting a group asks for a second click and removes everything that belongs to it, its notes included. One Ctrl + Z brings the group and all of its notes back. Creating groups can be undone too.
- Groups are saved inside the chart. A chart with no groups is written exactly as before, and notes of the base group write nothing new.
- A **highlighted note fades as a whole**: the glow behind it fades with it, instead of staying behind to show where an invisible note is.

## Analyzer

- **Chart Convert is gone from the top menu**; its place is taken by **Analyzer**. The BMS and Arcaea importers had stopped being useful.
- **Automatic** listens to the song and writes its BPM, with the first BPM line exactly on the first beat where the music starts. Whole or half tempos are kept round (150, not 149.98). Checked against 32 real Lanota charts: the same BPM in 25, double or half of it in 5 (the same pulse, counted the other way), and the first beat within 10 ms of the charter's in most of them.
- When the song **changes tempo**, Automatic asks whether to **apply every BPM** (one line per section, each starting on its own beat) or **keep the original BPM**. Parts that only feel different, such as triplet passages, do not count as a change.
- **Manual** opens a window with a large circle: the first press plays the song from where it was paused, and every press after that is a beat. The BPM of the taps is shown under it, and fills a BPM field and a Timing field that can be corrected by hand before pressing **Apply BPM**, which starts that BPM at that Timing.
- Everything the Analyzer writes is undone with a single Ctrl + Z.

## UiTweak

Flowaria's UiTweak, a set of community plugins for Lanotalium 2.5, is now part of the editor, with Flowaria's permission and its original art. Everything is switched from its own **UiTweak** menu, after Analyzer; each entry shows a tick while it is on.

- **Note Effects**: when a note reaches the judge line it flashes and sends out a shockwave; flicks throw sparks inwards or outwards; a catch gets the flash alone; a hold gets a shockwave, then a ripple and sparks on its head while it lasts, and a last shockwave when it ends. There is no player in the editor, so every note is a perfect hit.
- **Combo Counter**: the combo reached, with the game's "Harmony!!", beside each note as it is hit. Hold ticks count, as in the game.
- The effects fire only when playback crosses a note. Jumping the playhead or dragging it while paused no longer sets off every note in between. Pausing freezes the effects on screen, and they follow the playback speed.
- **Flick Arrows**: as in the game, Flick In and Flick Out notes carry a window reaching from the note's middle towards the core, through which chevrons keep sliding the way the finger has to go; what leaves by one edge comes back by the other. The window grows with the note's size.
- **Judge Ornaments**: a glow on the judge line that pulses on every beat of the chart, and a decorative ring turning slowly over it. Both fade with the transparency motion.
- **Background Wave and Particles**: the game's translucent blue wave along the bottom of the screen, rising and falling with the beat, and glowing specks drifting up from below. Both are drawn over the tuner; the wave is 16:9 wide and a narrower window cuts its ends rather than squeezing it.
- **Tuner Skin**: besides Ritmo and Física, any folder dropped into `StreamingAssets/TunerSkin` with the ring's pictures (Background, Border, JudgeLine, Arrow, Core); the README there explains it. Skins are not part of the repository. Every picture is drawn at the size of the part it replaces, so 1024 px "HD" cores fit too. Choosing Ritmo or Física, here or in the Tuner's Skin panel, goes back to the plain ring.
- **Lanota Header**: the Tuner's header is laid out as the game's, measured on a screenshot of it: the bar as tall as the game's, the large pause button (which plays and pauses), the chart's name beside it, "MASTER 16" in the middle section with a raised "+" for levels like 14+, and the lettering shaded as the game shades it, palest just above the middle. Click the badge to change the difficulty (Whisper, Acoustic, Ultra, Master, your own or hidden), and the number to type the level. **Your own difficulty**: right-click the badge, or use UiTweak > Custom Difficulty, to give it a word and a colour. The colour is a HEX code, with a swatch beside it, and can be picked on a colour picker in the same window. All of it is saved in the project. The difficulty and level are drawn crisp, shaded like the score, with the "+" tucked against the figures as in the game, and the bar's gold rim and dividers are as bright as in the game. Long names shrink to fit. Turning it off restores the usual header.
- **Progress Bar**: with the Lanota Header, a thin light along its top edge grows as the song plays, with the game's round flash at its tip.
- **Show Score**: with the Lanota Header, the designer's name gives way to the game's score, seven figures shaded from white to gold, counting up to 1,000,000 as the chart plays.
- **Perfect Purified at the End**: when playback passes the end of the chart's last note, the score having reached a million, the game's "Perfect Purified" comes up over the tuner with its stars and gears and the all-combo sound, and fades after a few seconds.
- **Ready at the Start** (off by default): playing from the very beginning shows the game's "Ready" for four seconds, the band opening across the screen and the word blinking, with the song held at its start until it ends. Nothing has to be added to the song file. Pressing play again skips it.
- **HD Rails**: sharper rail bodies, the held one brighter and the unheld one with the game's dark edges.
- **HD Core**: the tuner's core at 1024 px. A skin with its own core still shows its own.
- **Compact Highlight**: highlighted notes wear their glow close to the note, as in the game, instead of a wide halo.

## Preferences and the Media Player

- New **Theme** setting: Dark takes the editor's greys down to a near black, Light takes them up to a bright silver, Default leaves everything as it was.
- **Waveform Color** and **Rail Note Visual Guide** are set here as well. The Waveform switch itself lives in the TimeLine's title bar, right above the strip.
- **Tuner Background Opacity**, a slider from 0 to 100 for the tuner's background layer alone, the part of the ring that has always been a little see-through. 60 is how it has always been drawn. It is separate from the transparency motion, which fades the whole ring on top of it.
- New **Volume** bar in the Media Player, under Pitch, from nothing to 125 per cent. It moves the song only, not the hit sounds.

## Everything else

- **A flag for the language** at the top right, beside Plugin: it shows the language in use, and a click lists every language with its flag to switch to, so someone who cannot read the current one can still find it.
- **Reset Editor Layout asks first**, so a stray click no longer throws away how the windows were arranged. A window saved reaching past the edge of the screen is brought back inside it when the editor opens.
- The **Creator's buttons sit together** again. There are only two small gaps: one after Create Scroll Speed, between the buttons that create things and those that work on the selection, and one before the tools. Every button added over time had brought a gap of its own.
- **The decimal point works.** Typing 1.5 means one and a half; a comma still works too. The program used to follow the Windows setting, which read 1.5 as 15, and which also misread imported Arcaea charts.
- The chart is **saved to one side every five minutes**, into an `AutoSaves` folder beside it, keeping the newest 24 copies. Autosave used to stay off for the whole session unless a chart was already open when the editor started.
- **Create Catch Rail takes the short way round.** Reading the two degrees by subtraction sent a run from 350 to 10 the whole way round the back instead of the twenty degrees across zero. The new **Reverse** switch beside the Quantity box asks for the long way on purpose, and starts off.
- **Spins**, the box beside it, adds whole turns: three spins between a note at 0 and one at 5 winds the catch notes three times round the core on the way, evenly spaced. Hover it for what it does.
- **Shift and 1 to 5 change the kind of the selected notes** — Click, Flick In, Flick Out, Catch, Rail — the same five Click To Create places.
- **A degree typed into the inspector is kept as typed**, so -3600 stays -3600. The editor's own moves still wrap into 0-360.
- **A kept group of motions is placed like a kept group of notes**: it comes up as a see-through preview that follows the pointer, instead of landing at the playhead the moment the button is pressed.
- The two arrows that step from motion to motion now reach the fourth row.
- **Waveform** has left Preferences: the TimeLine's own title bar carries that switch, right above the strip.
- **Ctrl + V offers back whatever Ctrl + C took last.** It used to hand over the motions only while no note had ever been copied, and the note clipboard is never emptied, so copying a single note shut motion pasting off for the rest of the session — which is why it came back after saving and reopening the chart.
- **Right click puts everything down at once**: it cancels the preview and clears the selection in the same press, for notes and for motions.
- A rail that had somehow ended up painted a third colour stayed looking selected for good; its colour is now read the way the tap notes' already was.
- **Two clicks to remove a kept angleline pattern**, the way the Favourite Groups already asked.
- **Auto Highlight** treats notes less than 8 ms apart as falling together, whatever time group they are in. It compared times rounded to four decimals, which left chords placed a hair apart unhighlighted: across 184 real charts that was over two thousand notes. It now changes only the notes that need it, and the whole pass is one undo instead of one per note in the chart.
- The Inspector's tabs keep the dark or light theme after being pressed, and rows added to the Creator no longer come out darker than their neighbours after switching themes.
- **Undo and redo no longer give up.** There was never a limit on how many steps were kept, but a step that failed left the whole stack stuck on it, so Ctrl + Z appeared to stop working after a while. A step that fails is now reported and passed over, and the session can be walked back to its beginning.
- **Hover balloons stay on screen.** Near the right edge they open to the left of the pointer, and near the top they open below it, instead of running off the window.

---

# Registro de cambios

Cambios hechos en la rama `offline-spanish-editing`, escritos para quien usa
el editor y no para quien lee el código. Las correcciones internas que no se
notan al usarlo se quedan fuera.

## Notas

- **Ctrl + ← / →** lleva las notas seleccionadas a la angleline anterior o siguiente; **Ctrl + ↑ / ↓**, a la beatline siguiente o anterior. Sin selección, siguen moviendo la línea de tiempo como siempre.
- **Shift + ← / →** gira la selección 5 grados y **Shift + ↑ / ↓** la gira 1 grado, sin tener en cuenta las anglelines.
- **Alt + ← / →** le da la vuelta al grupo en el tiempo: la última nota pasa a ser la primera. **Alt + ↑ / ↓** lo refleja sobre su propio grado medio.
- Los botones **Invertir (Horiz.)** e **Invertir (Vert.)** del Creator hacen esos mismos dos giros.
- **Seleccionar pares** y **Seleccionar impares** reducen la selección a sus elementos pares o impares, contando en orden de reproducción.
- Un grupo se mueve siempre en bloque, conservando forma y separación, y manda la nota más cercana a la línea de juicio.
- La silueta del pegado ahora es translúcida, para no confundirla con las notas ya colocadas. Con las flechas la giras antes de soltarla.
- Al pegar o arrastrar, las notas caen justo en la angleline o la beatline, gire la cámara o no.
- Los grados de las notas se quedan entre 0 y 360 en vez de acabar en valores como -2430 tras varios motions de rotación.
- **1, 2, 3 y 4 cambian el tamaño de las notas seleccionadas** con Click To Create apagado; el 4 es el tamaño por defecto. Toda la selección es un solo deshacer.
- **Pegado repetido**: la silueta se queda después de soltar una copia, así que puedes repetir el mismo patrón las veces que quieras. Con el clic derecho se quita.
- **Shift y la rueda sobre el Tuner** recorren el chart beatline a beatline, arriba hacia delante y abajo hacia atrás, sin tener que ir a las flechas.
- **Ctrl y arrastrar** abren en abanico un grupo seleccionado: la primera y la última nota se quedan donde están, la que arrastras sigue al cursor y las demás caen en las rectas que unen a las tres. Si tiras de un extremo gira todo el grupo; si tiras de una de en medio, el grupo se dobla en un '>'. Solo se mueven los grados.
- **Crear ease de grupo**, debajo de Crear Catch Rail, curva el grupo seleccionado desde su primera nota hasta la última. El campo admite el mismo 0 a 12 que un motion, y el 0 lo vuelve a dejar recto.

## Motions y línea de tiempo

- **Arrastra un motion con el clic derecho** para moverlo. Se pega a la beatline más cercana, salta por encima de sus vecinos si lo llevas lo bastante lejos, y con Ctrl va libre.
- **Arrastra uno de sus bordes** para alargarlo o acortarlo. Solo cambia el tiempo: el ease, el origen y el destino se quedan igual.
- Con varios motions seleccionados, mover y estirar afectan a todos, conservando separación y duraciones.
- **Shift + clic** selecciona todos los motions que hay entre el anterior pulsado y este.
- **S** divide en dos el motion que tengas bajo el cursor, justo por ahí. Las dos mitades son idénticas salvo en el tiempo.
- Los motions de menos de 0,05 s muestran un rombo pequeño, para poder pulsarlos.
- **Invertir valores**, botón nuevo en el inspector de Motion y **Ctrl + R**, le da la vuelta a los valores: una bajada de 20 pasa a ser una subida de 20.
- **Ctrl + C y Ctrl + V copian motions** igual que copian notas: silueta translúcida, clic izquierdo para soltarla y clic derecho para cancelar.
- Con varios motions seleccionados, el inspector los edita **todos a la vez**: el ease y los dos puntos de control se escriben en todos ellos.
- **Motion de transparencia**, un cuarto tipo de motion junto a Horizontal, Vertical y Rotation, en su propia fila debajo de Rotation. Funde el afinador en sí (fondo, borde, línea de juicio, flecha y núcleo) y nunca las notas. **Crear Motion (Transparencia)** en el Creator añade uno en la posición de reproducción; el inspector muestra Timing, Duration, Ease y Transparencia, de 0 (invisible) a 100 (el afinador opaco de siempre). Cada motion lleva el afinador desde donde estuviera hasta su propio número, así que uno nuevo nace con el valor actual y no cambia nada hasta que se edita. Las barras son de un coral suave, tan claro como las otras tres filas, y se ponen rosas al seleccionarlas, y cortar con S, copiar y pegar, mover y estirar con clic derecho, la selección por recuadro, los Grupos favoritos y el Copier funcionan con ellas igual que con las otras tres. **Invert Values** gira una transparencia sobre su punto medio: 30 pasa a ser 70.
- **La timeline también se arrastra desde la cuarta fila.** El arrastre llegaba hasta el pie de las tres filas que había antes, así que la nueva era una franja muerta que se lo tragaba. Ahora se mide contra el pie de las filas que haya, y la fila conserva el globo de ayuda que tienen sus hermanas.

## Forma de onda

- **Una sola onda en vez de una izquierda y otra derecha**, siempre fija en la parte baja de la línea de tiempo. El separador que había que arrastrar ya no está.
- Se dibuja a partir de una **envolvente** calculada una sola vez al cargar la canción, un par de megas en vez de ochenta: funciona en la versión de 32 bits y no cuesta nada por fotograma.
- **La rueda encima de ella la amplía o la reduce**, desde la canción entera hasta un segundo aproximadamente, sin tocar la escala de la línea de tiempo.
- **Haz clic o arrastra sobre ella para llevar la cabecera** a ese punto. Una línea clara marca dónde está.
- Diez colores a elegir en Preferencias, cian por defecto, y ahí mismo se apaga.
- La cabecera es una línea finísima con una chincheta en el pie, en vez de la barra ancha que dibujaba antes.
- **Sincronizar**, al lado de Forma de onda en la barra de título, ata el zoom de la onda al de la línea de tiempo: las dos pasan a mostrar el mismo tramo, empezando en la cabecera, y la rueda sobre cualquiera de ellas mueve las dos.
- Arrastrar sobre la onda ampliada ya no te lleva al principio de la canción: te mueves por el tramo que estás mirando.
- La franja es más baja, dejando encima una fila libre para los motions de transparencia que vendrán.

## Mover el afinador

- Con **Ctrl** pulsado, arrastra el núcleo —el círculo del centro del aro— para mover el afinador por el visor. Ctrl y doble clic lo recentran.
- Manteniendo además **Alt**, el centro del aro se imanta a la malla y cae justo en un cruce cuando está cerca.
- Con **un solo motion horizontal seleccionado**, donde sueltes el afinador pasa a ser el destino de ese motion, y ya no hace falta escribir las coordenadas.

## Malla

- Apartado **Malla** nuevo entre Angleline y Copier: **V.** y **H.** indican cuántas líneas cruzan el fondo, repartidas por igual y simétricas respecto al centro.
- Una palometa junto a cada campo apaga esa dirección sin perder lo escrito, y un deslizador regula la opacidad de la malla.

## Anglelines y beatlines

- Los patrones de angleline se pueden **guardar como favoritos**: el botón del corazón guarda lo que haya en el campo, y cada patrón guardado tiene su fila con mostrar, renombrar y eliminar. Sobreviven al reinicio.
- Las beatlines se colorean según dónde caen en el compás: azul en el compás, verde en la mitad, naranja en los cuartos y rosa en lo más fino.
- Las anglelines de 0, 90, 180 y 270 salen doradas, y las de 45, 135, 225 y 315, en bronce.
- Ambas, y la malla, tienen su propio **deslizador de opacidad**.

## Grupos favoritos

- Apartado **Grupos favoritos** nuevo entre Click To Create y Angleline, con patrones de notas y patrones de motions.
- **Ctrl + F**, o el botón del corazón, guarda la selección como patrón: o notas o motions, nunca mezclados, hasta 100 notas.
- Cada patrón tiene **Copiar**, **Cambiar nombre** y **Eliminar (doble clic)**. Copiar notas levanta la silueta de pegado; copiar motions los coloca en la cabecera de reproducción.
- **Exportar** e **Importar** guardan y leen un archivo `.json` con los grupos y las anglelines juntos, para pasarle tus patrones a otra persona.

## Click To Create

- **C** lo activa y lo desactiva. El atajo era F2 y no hacía absolutamente nada.
- Al activarlo se activan también **Ajustar a Beatline y Angleline**. Si apagas alguno a mano, se queda apagado hasta la siguiente vez que pulses C.
- **Del 1 al 5 eligen el tipo de nota** mientras está activo: Click, Flick In, Flick Out, Catch y Rail.
- La lista de tipos va numerada **del 1 al 5** en vez de 0, 2, 3, 4, 5, tanto aquí como en el inspector, para que el orden tenga sentido y coincida con esas teclas.
- **Mayús + del 1 al 4 eligen el tamaño de la siguiente nota**: tamaños 0, 1, 2 y 3, en el orden en que se lee la lista de tamaños.
- **Si mantienes pulsado el botón al colocar un rail**, el rail se dibuja solo: el ancla se queda donde hiciste clic y el final sigue al cursor hasta que sueltas, hacia fuera para la longitud y alrededor para la inclinación, así que puedes trazar un rail en diagonal de una vez. Un clic normal sigue dejando el segundo de siempre.

## Notas rail

- **Un pequeño punto amarillo marca el final de cada rail**, para saber de dónde agarrarlo y para que se vea que es un tirador y no una nota puesta sin querer.
- **Ctrl y arrastrar ese punto** mueven el final del rail: lo que dura y hacia dónde se inclina, de una sola vez, imantándose a beatlines y anglelines como todo lo demás. Con Mayús va libre, y puedes pulsarlo o soltarlo en cualquier momento del arrastre.
- **J crea un joint justo donde esté el cursor** sobre el rail, sin cambiarle la forma. Con Ctrl cae sobre una beatline o sobre una angleline por la que pase el rail.
- **Los joints son notas**: haz clic en uno para seleccionarlo, Ctrl+clic para varios, y luego arrástralos o muévelos con los atajos de Ctrl, Mayús y las flechas. El resto del rail se queda donde estaba.
- **Mayús + S parte en dos el rail que haya bajo el cursor.** La segunda mitad empieza en el corte, en el grado al que había llegado el rail, con el mismo tamaño y el mismo tipo, y se lleva los joints que quedaban después del corte. Ctrl + S sigue guardando el proyecto, como siempre.
- **Sobre un rail seleccionado, una fina línea roja sigue al cursor** de lado a lado del carril y marca dónde caería el corte. Con Ctrl se pega a las beatlines y anglelines por las que pasa el rail.
- **Segmentar nota Rail**, debajo de Crear ease de grupo, parte un rail seleccionado en tantos rails como escribas, del 2 al 64. Recorren exactamente el mismo trazado que el original: los grados se leen de su curva, así que un ease se respeta en vez de repartirse en trozos rectos.
- **Convertir en una sola Hold**, debajo de Convert To Hold Note, vuelve a unir una tanda de notas hold sueltas en una sola: la primera se queda de cabeza y el resto pasan a ser sus joints, conservando su sitio y sus eases.
- **Aplicar ease a Rail**, debajo de Segmentar nota Hold, escribe un mismo ease en todos los joints de todas las rails seleccionadas de una vez. El inspector sigue editando joint a joint; esto es para una tanda de rails que quieren la misma curva.
- **Guías visuales de Rail**, en preferencias, apaga tanto el punto como la línea de corte.

## Time Groups

- **Time Groups**, una pestaña nueva en el Inspector junto a BPM, Scroll Speed y Por defecto. Al abrirla se recogen esas tres, y al abrir cualquiera de ellas se recoge esta.
- Cada nota pertenece a un grupo. **Base** es el del propio chart y contiene todas las notas que ya había; se mueve con el Scroll Speed del chart exactamente como siempre y no se puede borrar.
- **Cada grupo tiene su propia lista de Scroll Speed**, con velocidades negativas incluidas, así que unas notas caen mientras otras se quedan quietas o vuelven hacia el núcleo. La lista se edita en las mismas filas que la pestaña Scroll Speed, y **+ Velocidad** añade una en la posición de reproducción.
- **Editar** convierte un grupo en el activo. Todo lo que se coloque a continuación va a él: Crear Tap/Hold, Click To Create, pegar, Grupos favoritos y el Copier. Las notas que salen de otra conservan el grupo de esa nota: cortar, segmentar y unir rails, convertir y cambiar de tipo. Con un grupo activo, **Create Scroll Speed** del Creator añade la velocidad a ese grupo.
- **Mover selección aquí** mete las notas seleccionadas en el grupo activo, con un solo deshacer.
- **Visible / Oculto** esconde las notas de un grupo mientras editas, también las del grupo Base.
- Cada fila muestra **cuántas notas** tiene el grupo, así que al mover notas a un grupo se ve al instante que han entrado.
- **Opacidad en el tiempo**: claves con Timing, Duration, Opacidad (0 invisible, 100 del todo) y Ease, cada una lleva las notas del grupo desde donde estaban hasta su valor.
- **Rotación del grupo**: claves con Timing, Duration, Grados y Ease que giran las notas del grupo alrededor del núcleo, sumándose como un motion de rotación.
- **Fundido por distancia**: las notas aparecen poco a poco desde el núcleo hasta un punto de su recorrido, y/o empiezan a desaparecer en un punto y se han ido al llegar a la línea de juicio.
- **Color del grupo**: un tinte elegido entre muestras o con cualquier RRGGBB. Dos casillas a su lado dicen a qué llega: **Highlight**, el brillo de las notas resaltadas, y **Notas**, las propias notas (rails y joints incluidos). Deja marcada solo Highlight para colorear únicamente el brillo.
- **Efectos de grupo: Sí/No**. Con Sí, todo lo anterior se ve como quedará el chart. Con No, todas las notas se mueven con el chart y sin girar, así que quedan justo donde las colocas o arrastras (el puntero siempre se lee con la velocidad del chart, porque un grupo parado no tiene una posición que distinga un momento de otro), y las notas que el chart oculta se dibujan como fantasmas tenues para poder seleccionarlas. Una nota seleccionada nunca es del todo invisible.
- Borrar un grupo pide un segundo clic y elimina todo lo que le pertenece, sus notas incluidas. Un solo Ctrl + Z recupera el grupo con todas sus notas. Crear grupos también se puede deshacer.
- Los grupos se guardan dentro del chart. Un chart sin grupos se escribe exactamente igual que antes, y las notas del grupo Base no escriben nada nuevo.
- Una **nota con highlight se desvanece entera**: el brillo que lleva detrás se funde con ella, en vez de quedarse señalando dónde está una nota invisible.

## Analyzer

- **Chart Convert desaparece del menú superior** y su sitio lo ocupa **Analyzer**. Los importadores de BMS y Arcaea ya no servían.
- **Automatic** escucha la canción y escribe su BPM, con la primera línea de BPM justo en el primer beat, donde empieza la música. Los tempos enteros o de medio se dejan redondos (150, no 149,98). Probado con 32 charts reales de Lanota: el mismo BPM en 25, el doble o la mitad en 5 (el mismo pulso, contado de la otra forma), y el primer beat a menos de 10 ms del que puso el charter en la mayoría.
- Si la canción **cambia de tempo**, Automatic pregunta si **aplicar todos los BPM** (una línea por tramo, cada una en su propio beat) o **mantener el BPM original**. Los pasajes que solo suenan distinto, como los de tresillos, no cuentan como cambio.
- **Manual** abre una ventana con un círculo grande: la primera pulsación reproduce la canción desde donde estaba pausada y cada pulsación siguiente es un beat. El BPM de los toques aparece debajo y rellena un campo de BPM y otro de Timing, que se pueden corregir a mano antes de pulsar **Aplicar BPM**, que empieza ese BPM en ese Timing.
- Todo lo que escribe el Analyzer se deshace con un solo Ctrl + Z.

## UiTweak

El UiTweak de Flowaria, un conjunto de plugins de la comunidad para Lanotalium 2.5, forma parte ahora del editor, con permiso de Flowaria y sus gráficos originales. Todo se activa desde su propio menú **UiTweak**, después de Analyzer; cada opción lleva una marca mientras está activa.

- **Efectos de notas**: al llegar una nota a la línea de juicio hay un destello y una onda; los flicks lanzan chispas hacia dentro o hacia fuera; las catch solo llevan el destello; los holds, una onda al empezar, ondas y chispas en la cabeza mientras duran y una última onda al terminar. En el editor no hay jugador, así que todas las notas son un acierto perfecto.
- **Contador de combo**: el combo alcanzado, con el "Harmony!!" del juego, junto a cada nota que se golpea. Los ticks de los holds cuentan, como en el juego.
- Los efectos solo saltan cuando la reproducción pasa por una nota. Saltar en la línea de tiempo o arrastrarla en pausa ya no dispara todas las notas intermedias. Al pausar, los efectos se congelan en pantalla, y siguen la velocidad de reproducción.
- **Flechas en los Flick**: como en el juego, las notas Flick In y Flick Out llevan una ventana que va del centro de la nota hacia el núcleo, por la que pasan flechas sin parar hacia donde hay que mover el dedo; lo que sale por un borde vuelve a entrar por el otro. La ventana crece con el tamaño de la nota.
- **Adornos del Judge**: un brillo sobre la línea de juicio que late en cada beat del chart y un anillo decorativo que gira despacio encima. Los dos se desvanecen con la motion de transparencia.
- **Onda y partículas del fondo**: la onda azul translúcida del juego en la parte inferior de la pantalla, que sube y baja con el beat, y partículas brillantes que ascienden desde abajo. Las dos se dibujan por encima del afinador; la onda es de ancho 16:9 y en una ventana más estrecha se recortan sus extremos en vez de estrecharla.
- **Skin del afinador**: además de Ritmo y Física, cualquier carpeta que pongas en `StreamingAssets/TunerSkin` con las imágenes del anillo (Background, Border, JudgeLine, Arrow, Core); el README de esa carpeta lo explica. Las skins no forman parte del repositorio. Cada imagen se dibuja al tamaño de la pieza que sustituye, así que los núcleos "HD" de 1024 px también encajan. Elegir Ritmo o Física, aquí o en el panel Skin del Tuner, vuelve al anillo normal.
- **Cabecera de Lanota**: la cabecera del Tuner está maquetada como la del juego, medida sobre una captura suya: la barra tan alta como la del juego, el botón de pausa grande (que reproduce y pausa), el nombre del chart a su lado, "MASTER 16" en la sección central con el "+" elevado para niveles como 14+, y las letras con el degradado del juego, más claro justo por encima del centro. Pulsa la insignia para cambiar la dificultad (Whisper, Acoustic, Ultra, Master, la tuya u oculta) y el número para escribir el nivel. **Tu propia dificultad**: clic derecho en la insignia, o UiTweak > Dificultad personalizada, para darle una palabra y un color. El color es un código HEX, con una muestra a su lado, y se puede elegir en un selector de color en la misma ventana. Todo se guarda en el proyecto. La dificultad y el nivel se ven nítidos, con el mismo degradado que el Score y el "+" pegado a las cifras como en el juego, y el borde dorado y los separadores de la barra brillan como en el juego. Los nombres largos se encogen para caber. Al desactivarla vuelve la cabecera de siempre.
- **Barra de progreso**: con la Cabecera de Lanota, una línea luminosa en su borde superior crece según avanza la canción, con el destello redondo del juego en la punta.
- **Mostrar Score**: con la Cabecera de Lanota, el nombre del autor deja paso al Score del juego, siete cifras con degradado de blanco a dorado que suben hasta 1.000.000 mientras suena el chart.
- **Perfect Purified al final**: cuando la reproducción pasa por el final de la última nota del chart, con el Score ya en 1.000.000, aparece sobre el afinador el "Perfect Purified" del juego con sus estrellas, sus engranajes y el sonido de all combo, y se desvanece a los pocos segundos.
- **Ready al empezar** (desactivado por defecto): al reproducir desde el principio aparece el "Ready" del juego durante cuatro segundos, con la franja abriéndose y la palabra parpadeando, y la canción espera en su inicio hasta que termina. No hace falta añadir nada al archivo de la canción. Pulsar reproducir otra vez lo salta.
- **Rails HD**: cuerpos de rail más nítidos, el pulsado más brillante y el sin pulsar con los bordes oscuros del juego.
- **Núcleo HD**: el núcleo del afinador a 1024 px. Una skin que traiga su propio núcleo sigue mostrando el suyo.
- **Highlight compacto**: las notas resaltadas llevan el brillo ceñido a la nota, como en el juego, en vez de un halo ancho.

## Preferencias y reproductor

- Ajuste **Tema** nuevo: Oscuro baja los grises del editor hasta un gris casi negro, Claro los sube a un plateado brillante y Predeterminado lo deja todo como estaba.
- **Color de la onda** y **Guías visuales de Rail** también se ajustan aquí. El interruptor de la forma de onda está en la barra de título de la TimeLine, justo encima de la franja.
- **Opacidad del fondo del afinador**, un deslizador de 0 a 100 solo para la capa de fondo del afinador, la parte que siempre ha sido algo translúcida. 60 es como se ha dibujado siempre. Es independiente del motion de transparencia, que funde el afinador entero por encima.
- Barra de **Volumen** nueva en el reproductor, debajo de Tono, de cero al 125 por ciento. Solo mueve la canción, no los efectos de golpe.

## Lo demás

- **Una bandera para el idioma** arriba a la derecha, junto a Plugin: muestra el idioma en uso y, al pulsarla, despliega todos los idiomas con su bandera para cambiar, así quien no entienda el idioma actual puede encontrarlo igualmente.
- **Restablecer disposición del editor pregunta antes**, así un clic por error ya no deshace cómo estaban colocadas las ventanas. Una ventana guardada saliéndose del borde de la pantalla vuelve a caber al abrir el editor.
- Los **botones del Creator vuelven a estar juntos**. Solo quedan dos pequeños huecos: uno después de Create Scroll Speed, entre los botones que crean cosas y los que trabajan sobre la selección, y otro antes de las herramientas. Cada botón añadido con el tiempo había traído su propio hueco.
- **El punto decimal funciona.** Escribir 1.5 significa uno y medio, y la coma sigue valiendo. Antes el programa seguía la configuración de Windows, que leía 1.5 como 15 y que además estropeaba la importación de charts de Arcaea.
- El chart se **guarda aparte cada cinco minutos**, en una carpeta `AutoSaves` junto a él, conservando las 24 copias más recientes. Antes el autoguardado se quedaba apagado toda la sesión si al abrir el editor no había ningún chart abierto.
- **Create Catch Rail va por el camino corto.** Restar los dos grados mandaba una tanda del 350 al 10 toda la vuelta por detrás en vez de los veinte grados que hay cruzando el cero. El interruptor **Invertir**, al lado de la casilla de cantidad, pide el camino largo a propósito, y viene apagado.
- **Giros**, la casilla de al lado, añade vueltas enteras: tres giros entre una nota en el grado 0 y otra en el 5 hacen que las notas catch den tres vueltas al núcleo por el camino, repartidas por igual. Pasa el cursor por encima y te lo explica.
- **Mayús y del 1 al 5 cambian el tipo de las notas seleccionadas** — Click, Flick In, Flick Out, Catch, Rail — los cinco que coloca Click To Create.
- **Un grado escrito a mano en el inspector se queda como lo escribes**, así que -3600 sigue siendo -3600. Los movimientos del propio editor se siguen ajustando a 0-360.
- **Un grupo de motions guardado se coloca como uno de notas**: sale como previsualización translúcida que sigue al cursor, en vez de caer en el playhead nada más pulsar.
- Las dos flechas que saltan de motion en motion ya llegan a la cuarta fila.
- **Forma de onda** se ha ido de preferencias: ese interruptor está en la barra de título de la TimeLine, justo encima de la tira.
- **Ctrl + V devuelve lo último que cogió Ctrl + C.** Antes ofrecía los motions solo mientras no se hubiera copiado ninguna nota, y el portapapeles de notas no se vacía nunca, así que copiar una sola nota dejaba el pegado de motions apagado el resto de la sesión: por eso volvía a funcionar al guardar y reabrir el chart.
- **El clic derecho lo suelta todo de una vez**: cancela la previsualización y deselecciona, tanto en notas como en motions.
- Una rail que hubiera acabado pintada de un tercer color se quedaba para siempre con aspecto de seleccionada; ahora su color se lee como ya se leía el de las notas tap.
- **Dos clics para quitar un patrón de anglelines guardado**, igual que ya pedían los Grupos favoritos.
- **Auto Highlight** considera que caen a la vez las notas separadas menos de 8 ms, estén en el time group que estén. Antes comparaba tiempos redondeados a cuatro decimales, y dejaba sin resaltar acordes colocados con una mínima diferencia: en 184 charts reales, más de dos mil notas. Ahora solo cambia las notas que lo necesitan, y toda la pasada se deshace de una vez en lugar de nota a nota.
- Las pestañas del Inspector conservan el tema oscuro o claro después de pulsarlas, y las filas añadidas al Creator ya no salen más oscuras que las demás al cambiar de tema.
- **Deshacer y rehacer ya no se plantan.** Nunca hubo un límite de pasos guardados, pero un paso que fallaba dejaba toda la pila atascada en él, y por eso Ctrl + Z parecía dejar de funcionar al cabo de un rato. Ahora un paso que falla se anota y se pasa de largo, y se puede volver hasta el principio de la sesión.
- **Los globos de ayuda no se salen de la pantalla.** Junto al borde derecho se abren a la izquierda del puntero, y junto al borde superior se abren por debajo, en vez de cortarse.
