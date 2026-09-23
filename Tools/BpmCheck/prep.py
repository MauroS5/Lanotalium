"""Decode every level's song to raw mono float32 at 22050 Hz and collect its chart's bpm list."""
import os, json, glob, sys
import numpy as np, soundfile as sf

# The folder of levels, one subfolder per level holding its song and chart:
# python prep.py <folder>, or the LANOTA_LEVELS environment variable.
ROOT = (sys.argv[1] if len(sys.argv) > 1 else '') or os.environ.get('LANOTA_LEVELS', '')
if not ROOT or not os.path.isdir(ROOT): sys.exit('usage: python prep.py <folder of levels>')
OUT = os.path.dirname(os.path.abspath(__file__)) + '/data'
os.makedirs(OUT, exist_ok=True)
truth = []
for d in sorted(os.listdir(ROOT)):
    p = os.path.join(ROOT, d)
    if not os.path.isdir(p): continue
    songs = [f for f in os.listdir(p) if f.lower().endswith(('.ogg', '.mp3', '.wav'))]
    charts = [f for f in os.listdir(p) if f.lower().endswith('.txt') and 'backup' not in f.lower()]
    if not songs or not charts: continue
    bpms = None
    for c in charts:
        try:
            j = json.load(open(os.path.join(p, c), encoding='utf-8-sig'))
            if j.get('bpm'):
                bpms = [(e['Timing'], e['Bpm']) for e in j['bpm']]
                break
        except Exception:
            pass
    if bpms is None: continue
    song = sorted(songs, key=lambda f: (not f.lower().endswith('.ogg'), f))[0]
    name = ''.join(ch for ch in d if ch.isalnum())[:30]
    raw = os.path.join(OUT, name + '.f32')
    if not os.path.exists(raw):
        try:
            x, sr = sf.read(os.path.join(p, song), dtype='float32', always_2d=True)
        except Exception as e:
            print('skip', d, e); continue
        x = x.mean(axis=1)
        # To 22050 by linear interpolation; good enough for onset work.
        n = int(len(x) * 22050 / sr)
        x = np.interp(np.arange(n) * (sr / 22050), np.arange(len(x)), x).astype(np.float32)
        x.tofile(raw)
    truth.append({'name': name, 'dir': d, 'song': song, 'bpm': bpms})
    print(name, bpms[:4], len(bpms))
json.dump(truth, open(os.path.join(OUT, 'truth.json'), 'w'), indent=1)
