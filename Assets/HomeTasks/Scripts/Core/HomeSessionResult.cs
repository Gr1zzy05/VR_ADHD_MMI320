using System;
using System.Collections.Generic;

namespace HomeTasks
{
    // A value snapshot survives the scene change without retaining any scene objects.
    public sealed class HomeSessionResult
    {
        public float Elapsed { get; }
        public int Washed { get; }
        public int Organized { get; }
        public int DishCount { get; }
        public int Disposed { get; }
        public int TrashCount { get; }
        public int CorrectAnswers { get; }
        public int QuestionCount { get; }
        public int RequiredScore { get; }
        public IReadOnlyList<DistractionRecord> Distractions { get; }
        public bool QuizPassed => CorrectAnswers >= RequiredScore;

        public HomeSessionResult(HomeSession session)
        {
            Elapsed = session.Elapsed;
            Washed = session.Washed;
            Organized = session.Organized;
            DishCount = session.dishes.Count;
            Disposed = session.Disposed;
            TrashCount = session.rubbish.Count;
            CorrectAnswers = session.meeting.CorrectAnswers;
            QuestionCount = session.meeting.content.questions.Length;
            RequiredScore = session.meeting.RequiredScore;
            Distractions = Array.AsReadOnly(session.SnapshotDistractions());
        }
    }
}
