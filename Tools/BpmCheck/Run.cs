using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading;

static class Run
{
    static void Main(string[] Args)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        string Dir = Args[0];
        for (int i = 1; i + 1 < Args.Length; i += 2)
        {
            double V = double.Parse(Args[i + 1], CultureInfo.InvariantCulture);
            var F = typeof(LimBpmAnalysis).GetField(Args[i]);
            F.SetValue(null, V);
        }
        foreach (string P in Directory.GetFiles(Dir, Environment.GetEnvironmentVariable("BPMONLY") ?? "*.f32").OrderBy(x => x))
        {
            byte[] B = File.ReadAllBytes(P);
            float[] X = new float[B.Length / 4];
            Buffer.BlockCopy(B, 0, X, 0, X.Length * 4);
            var W = System.Diagnostics.Stopwatch.StartNew();
            var R = LimBpmAnalysis.Analyze(X, 22050);
            string Secs = string.Join(";", R.Sections.Select(s => s.Time.ToString("F4") + "@" + s.Bpm.ToString("0.###")));
            Console.WriteLine(Path.GetFileNameWithoutExtension(P) + "|" + R.Bpm.ToString("0.###") + "|" + R.FirstBeat.ToString("F4") + "|" + Secs + "|" + W.ElapsedMilliseconds);
        }
    }
}
