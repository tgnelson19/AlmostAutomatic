using System.Collections.Generic;

namespace FpsRange.Gameplay;

/// <summary>
/// Tracks scoring hits with timestamps and computes rolling-window sums for the
/// last 1s / 10s / 60s, plus the highest each rolling sum has ever reached.
/// </summary>
public class ScoreTracker
{
    private struct Hit
    {
        public double Time;
        public int Points;
    }

    private readonly List<Hit> _hits = new();

    public int Last1s { get; private set; }
    public int Last10s { get; private set; }
    public int Last60s { get; private set; }

    public int Best1s { get; private set; }
    public int Best10s { get; private set; }
    public int Best60s { get; private set; }

    public void RegisterHit(double currentTime, int points)
    {
        _hits.Add(new Hit { Time = currentTime, Points = points });
    }

    public void Update(double currentTime)
    {
        // Drop anything older than the largest window (60s) - no window needs it anymore.
        _hits.RemoveAll(h => currentTime - h.Time > 60.0);

        int sum1 = 0, sum10 = 0, sum60 = 0;
        foreach (var h in _hits)
        {
            double age = currentTime - h.Time;
            if (age <= 60.0) sum60 += h.Points;
            if (age <= 10.0) sum10 += h.Points;
            if (age <= 1.0) sum1 += h.Points;
        }

        Last1s = sum1;
        Last10s = sum10;
        Last60s = sum60;

        if (sum1 > Best1s) Best1s = sum1;
        if (sum10 > Best10s) Best10s = sum10;
        if (sum60 > Best60s) Best60s = sum60;
    }
}
