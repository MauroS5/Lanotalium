#!/bin/sh
# Measures LimBpmAnalysis against a folder of levels (one subfolder per level,
# holding its song and chart). Usage: check.sh <folder>. See CLAUDE.md, "Analyzer".
# Needs Python with numpy and soundfile. data/ and synth/ are rebuilt when missing.
LEVELS=""
[ -n "$1" ] && LEVELS="$(cd "$1" && pwd)"
cd "$(dirname "$0")"
[ -f data/truth.json ] || python prep.py "$LEVELS"
[ -f synth/truth.txt ] || python synth.py
MSYS_NO_PATHCONV=1 "/d/Archivos de programa/Unity/Editor/Data/Tools/Roslyn/csc.exe" @run.rsp || exit 1
./run.exe synth
./run.exe data > result.txt && python ev.py result.txt
