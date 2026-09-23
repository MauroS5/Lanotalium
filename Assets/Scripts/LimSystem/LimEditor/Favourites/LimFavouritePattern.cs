using System.Collections.Generic;

namespace Lanotalium.Editor
{
    /// <summary>
    /// A saved pattern: either a handful of notes or a handful of motions,
    /// never both, kept so it can be dropped into another part of the chart
    /// or another chart altogether.
    ///
    /// Everything is stored relative to the earliest element, so a pattern
    /// has no place of its own until it is pasted somewhere. These live in
    /// the preferences file, which is plain JSON, so the classes are kept
    /// flat and free of anything Unity has to be running to make sense of.
    /// </summary>
    public class FavouritePattern
    {
        public string Name = string.Empty;
        public List<FavouriteNote> Notes = new List<FavouriteNote>();
        public List<FavouriteMotion> Motions = new List<FavouriteMotion>();

        public bool HoldsMotions { get { return Motions != null && Motions.Count > 0; } }
        public int Count { get { return HoldsMotions ? Motions.Count : (Notes != null ? Notes.Count : 0); } }
    }

    /// <summary>
    /// Everything a chart maker keeps, in one file they can hand to someone
    /// else: the angleline patterns and the saved groups together.
    /// </summary>
    public class FavouriteExport
    {
        public List<string> Anglelines = new List<string>();
        public List<FavouritePattern> Patterns = new List<FavouritePattern>();
    }

    public class FavouriteNote
    {
        public int Type;
        /// <summary>Seconds after the earliest element of the pattern.</summary>
        public float Time;
        public float Duration;
        public float Degree;
        public int Size;
        public bool Critical;
        public bool Combination;
        public float Bpm;
        /// <summary>Only a hold note has these; the rail it draws.</summary>
        public List<FavouriteJoint> Joints = new List<FavouriteJoint>();
    }

    public class FavouriteJoint
    {
        public int Cfmi;
        public float dDegree;
        public float dTime;
    }

    public class FavouriteMotion
    {
        public int Type;
        /// <summary>Seconds after the earliest element of the pattern.</summary>
        public float Time;
        public float Duration;
        public float ctp, ctp1, ctp2;
        public int cfmi;
        public bool cflg;
    }
}
