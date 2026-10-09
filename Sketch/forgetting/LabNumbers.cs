using System.Collections.Generic;

namespace Sketch.Forgetting;

/// <summary>Everything a run measured, as written to <c>numbers.json</c>.</summary>
internal sealed record LabNumbers(
    RunFacts Run,
    Engine Engine,
    SessionNumbers Session,
    PreviewNumbers Preview,
    ProofNumbers Proof,
    PastNumbers Past,
    CommitNumbers Commit,
    IReadOnlyDictionary<string, JournalState> Journal,
    RehydrationNumbers Rehydration);

internal sealed record RunFacts(string DateUtc, int Seed, int Survivors, int Hesitations, int Scale, int Runs, double TotalSeconds);

internal sealed record SessionNumbers(int Acts, int Draws, int Erases, long Head, long MaxPairDistanceEntries, int StrokesOnCanvas, string PictureSha256, double Seconds);

internal sealed record PreviewNumbers(int WouldElide, long Pairs, bool PrimaryUntouched, double Seconds);

internal sealed record ProofNumbers(bool PairsAreSafe, double SafeSeconds, bool LoneEraseIsSafe, IReadOnlyList<string> LoneEraseChangedObservations, int ResurrectedStroke, double UnsafeSeconds);

internal sealed record PastNumbers(
    long MidSessionEntry,
    int StrokesAtMid,
    IReadOnlyList<int> HesitationsAtMid,
    bool ForgettingIsSafeForThePast,
    IReadOnlyList<string> ChangedObservationsAtMid,
    int StrokesAtMidOnceForgotten,
    int StrokesAtMidAfterDistill,
    bool AfterDistillMatchesPrediction,
    int RecordsUpToMidBeforeDistill,
    int RecordsUpToMidAfterDistill);

internal sealed record CommitNumbers(
    long PairsElided,
    double ElideSeconds,
    double DistillSeconds,
    bool PictureByteEqualAfterReopen,
    bool StrokeCountEqualAfterReopen,
    long HeadBeforeCommit,
    long HeadAfterReopen,
    long NextEntryId,
    bool EntryIdsUniqueAndAscending,
    bool NextEntryIdIsNew);

internal sealed record JournalState(int Records, int Scripts, int Defines, int Invocations, int ElisionMarks, long SegmentBytes, long TotalBytes, int Files);

internal sealed record RehydrationNumbers(int WarmUpStarts, IReadOnlyDictionary<string, TimingNumbers> States);

internal sealed record TimingNumbers(double MedianMs, double MinMs, double MaxMs, IReadOnlyList<double> SamplesMs);
