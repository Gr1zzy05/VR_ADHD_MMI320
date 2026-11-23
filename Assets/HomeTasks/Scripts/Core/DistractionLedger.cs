using System;

namespace HomeTasks
{
    public enum DistractionKind { FidgetCube, PopIt, Squishy, Television, Plushie, Phone, Conversation, Party, Construction }

    public readonly struct DistractionRecord
    {
        public DistractionKind Kind { get; }
        public int Uses { get; }
        public float Seconds { get; }
        public string Name => Kind switch
        {
            DistractionKind.FidgetCube => GameText.Get("Fidget cube"),
            DistractionKind.PopIt => GameText.Get("Rainbow pop-it"),
            DistractionKind.Squishy => GameText.Get("Butter squishy"),
            DistractionKind.Television => GameText.Get("Television"),
            DistractionKind.Plushie => GameText.Get("Plushie"),
            DistractionKind.Phone => GameText.Get("Phone"),
            DistractionKind.Conversation => GameText.Get("Neighbour chatter"),
            DistractionKind.Party => GameText.Get("Neighbour party"),
            DistractionKind.Construction => GameText.Get("Construction site"),
            _ => Kind.ToString()
        };

        public DistractionRecord(DistractionKind kind, int uses, float seconds)
        {
            Kind = kind;
            Uses = uses;
            Seconds = seconds;
        }
    }

    internal sealed class DistractionLedger
    {
        static readonly DistractionKind[] Kinds = (DistractionKind[])Enum.GetValues(typeof(DistractionKind));
        readonly int[] uses = new int[Kinds.Length];
        readonly float[] seconds = new float[Kinds.Length];

        public void Record(DistractionKind kind, bool began, float duration)
        {
            int index = (int)kind;
            if (began) uses[index]++;
            seconds[index] += duration;
        }

        public DistractionRecord[] Snapshot()
        {
            var snapshot = new DistractionRecord[Kinds.Length];
            for (var i = 0; i < snapshot.Length; i++)
                snapshot[i] = new DistractionRecord(Kinds[i], uses[i], seconds[i]);
            return snapshot;
        }
    }
}
