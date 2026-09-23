"""Songs with a real tempo change: the second part is played faster or slower (pitch moves too; the beat is what counts)."""
import numpy as np, os
H = os.path.dirname(os.path.abspath(__file__))
D = H + '/data'; S = H + '/synth'
os.makedirs(S, exist_ok=True)
SR = 22050
cases = [  # name, source, bpm, split second, speed factor
    ('MadokaTo210', 'MadokaMagicaConnect', 175, 60, 1.2),
    ('GravityTo120', 'Gravity', 150, 50, 0.8),
    ('AzureTo200', 'Azure', 150, 70, 4 / 3),
    ('LavenderTo136', 'LavenderLeaf', 170, 45, 0.8),
    ('VindicationTo150', 'Vindication', 174, 40, 150 / 174),
]
truth = []
for name, src, bpm, split, f in cases:
    x = np.fromfile(f'{D}/{src}.f32', dtype=np.float32)
    a = x[:split * SR]
    b = x[split * SR: split * SR + 60 * SR]
    n = int(len(b) / f)
    b2 = np.interp(np.arange(n) * f, np.arange(len(b)), b).astype(np.float32)
    np.concatenate([a, b2]).tofile(f'{S}/{name}.f32')
    truth.append(f'{name}|{bpm}|{split}|{bpm * f:.2f}')
open(f'{S}/truth.txt', 'w').write('\n'.join(truth))
print('\n'.join(truth))
