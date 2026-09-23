import json, sys, math, os
HERE = os.path.dirname(os.path.abspath(__file__))
truth = {t['name']: t for t in json.load(open(HERE + '/data/truth.json'))}
res = sys.argv[1] if len(sys.argv) > 1 else 'result.txt'
exact = octave = 0; phases = []; changes = 0; n = 0
for line in open(os.path.join(HERE, res)):
    name, bpm, first, secs, ms = line.strip().split('|')
    bpm = float(bpm); first = float(first)
    t = truth[name]['bpm']
    real = [(tm, b) for tm, b in t if 20 <= b <= 1000]
    dur = {}
    for i, (tm, b) in enumerate(real):
        end = real[i + 1][0] if i + 1 < len(real) else 400
        dur[b] = dur.get(b, 0) + end - max(tm, 0)
    main = max(dur, key=dur.get)
    refs = [tm for i, (tm, b) in enumerate(t) if b == main and i >= 1 and tm >= -0.5]
    ref = refs[0] if refs else 0.0
    ratio = bpm / main
    kind = 'EXACT' if abs(bpm - main) < 0.02 else ('octave x%.2g' % ratio if min(abs(ratio - r) for r in (0.5, 2, 1.5, 2 / 3, 4, 0.25)) < 0.01 else ('near' if abs(ratio - 1) < 0.01 else 'WRONG'))
    P = 60 / main
    Q = 60 / bpm
    step = min(P, Q)
    e = (first - ref) % step
    if e > step / 2: e -= step
    n += 1
    if kind == 'EXACT': exact += 1
    elif kind.startswith('octave'): octave += 1
    if kind in ('EXACT',) or kind.startswith('octave'): phases.append(e * 1000)
    nsec = len(secs.split(';'))
    if nsec > 1: changes += 1
    print('%-22s truth %7.3f det %7.3f %-12s phase %+7.1f ms  first %7.3f (ref %7.3f)  sections %d' % (name, main, bpm, kind, e * 1000, first, ref, nsec))
ph = sorted(phases)
print('exact %d/%d, octave %d, change-popups %d' % (exact, n, octave, changes))
if ph:
    print('phase ms median %+.1f, |e|<10: %d, |e|<20: %d of %d' % (ph[len(ph) // 2], sum(abs(x) < 10 for x in ph), sum(abs(x) < 20 for x in ph), len(ph)))
