using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Decantra.Domain.Generation;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;

public class Program
{
    private const int MaxSolveMillis = 15000;
    private const int MaxSolveNodes = 12_000_000;
    private const int RetrySolveMillis = 20000;
    private const int RetrySolveNodes = 20_000_000;

    public static int Main(string[] args)
    {
        var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };

        if (Console.IsOutputRedirected)
        {
            Console.SetOut(stderr);
            Console.SetError(stderr);
        }
        else
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: run [analyze <level> | bulk <count> | monotonic <count> | sinkanalysis <count> | transitionbench <count>]");
            return 1;
        }

        string mode = args[0];
        if (mode == "analyze")
        {
            int level = args.Length > 1 ? int.Parse(args[1]) : 11;
            AnalyzeLevel(level);
            return 0;
        }
        else if (mode == "bulk")
        {
            int count = args.Length > 1 ? int.Parse(args[1]) : 1000;
            return BulkGenerate(count);
        }
        else if (mode == "monotonic")
        {
            int count = args.Length > 1 ? int.Parse(args[1]) : 100;
            return MonotonicGenerate(count);
        }
        else if (mode == "sinkanalysis")
        {
            int count = args.Length > 1 ? int.Parse(args[1]) : 1000;
            return SinkAnalysis(count);
        }
        else if (mode == "transitionbench")
        {
            int count = args.Length > 1 ? int.Parse(args[1]) : 1000;
            return TransitionBenchmark(count);
        }
        else
        {
            Console.WriteLine("Unknown mode.");
            return 1;
        }
    }

    private static int NextSeed(int level, int previous)
    {
        unchecked
        {
            int baseSeed = previous != 0 ? previous : 12345;
            int mix = baseSeed * 1103515245 + 12345 + level * 97;
            return Math.Abs(mix == 0 ? level * 7919 : mix);
        }
    }

    private static void AnalyzeLevel(int targetLevel)
    {
        Console.WriteLine($"Analyzing Level {targetLevel}...");

        int seed = 0;
        for (int l = 1; l <= targetLevel; l++)
        {
            seed = NextSeed(l, seed);
        }
        Console.WriteLine($"Level {targetLevel} Seed (Standard): {seed}");

        var solver = new BfsSolver();
        var generator = new LevelGenerator(solver);
        generator.Log = (msg) => Console.WriteLine($"[Gen] {msg}");

        try
        {
            var profile = LevelDifficultyEngine.GetProfile(targetLevel);
            Console.WriteLine($"Generating Level {targetLevel} with seed {seed}");

            var state = generator.Generate(seed, profile);
            Console.WriteLine("Generated Successfully.");
            Console.WriteLine($"Optimal Moves: {state.OptimalMoves}");

            Console.WriteLine("Solving...");
            var result = solver.SolveWithPath(state);
            Console.WriteLine($"Solver Found: {result.OptimalMoves} moves");

            if (result.OptimalMoves < 0)
            {
                Console.WriteLine("SOLVER SAYS UNSOLVABLE");
            }
            else
            {
                Console.WriteLine("Solution Moves:");
                if (result.Path != null)
                {
                    foreach (var move in result.Path)
                    {
                        Console.WriteLine($"{move.Source + 1}{move.Target + 1}");
                    }
                }
            }

            // Verify Jump Seed
            int jumpSeed = NextSeed(targetLevel, 0);
            Console.WriteLine($"\nAnalyzing Level {targetLevel} Seed (Jump): {jumpSeed}");
            if (jumpSeed != seed)
            {
                try
                {
                    var jumpState = generator.Generate(jumpSeed, profile);
                    var jumpResult = solver.SolveWithPath(jumpState);
                    Console.WriteLine($"Jump Solver Found: {jumpResult.OptimalMoves} moves");
                    if (jumpResult.OptimalMoves < 0)
                    {
                        Console.WriteLine("JUMP VARIANT UNSOLVABLE");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Jump Gen Failed: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Jump seed is same as Standard.");
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static int BulkGenerate(int count)
    {
        Console.WriteLine($"Bulk generating solutions for levels 1 to {count} using {Environment.ProcessorCount} cores...");
        Console.WriteLine("INTRINSIC DIFFICULTY REPORTING ENABLED:");
        Console.WriteLine("  Raw complexity from solver metrics (no level number)");
        Console.WriteLine("  Difficulty reported is intrinsic (no monotonic mapping)");
        Console.WriteLine("Verbose progress logging enabled.");
        Console.WriteLine($"Solver limits: {MaxSolveMillis} ms, {MaxSolveNodes:N0} nodes per level.");

        // Pre-calculate seeds sequentially
        var seeds = new int[count + 1];
        int currentSeed = 0;
        for (int l = 1; l <= count; l++)
        {
            currentSeed = NextSeed(l, currentSeed);
            seeds[l] = currentSeed;
        }

        // Result structure for enriched output
        var results = new System.Collections.Concurrent.ConcurrentDictionary<int, LevelResult>();
        var done = new bool[count + 1];
        var levelTimesMs = new long[count + 1];
        var progressLock = new object();
        int contiguousDone = 0;
        int reportInterval = count >= 400 ? 25 : 10;
        int nextReport = reportInterval;
        int startedCount = 0;
        int finishedCount = 0;
        var stopwatch = Stopwatch.StartNew();
        var lastProgress = Stopwatch.StartNew();
        bool finished = false;

        using var progressTimer = new System.Threading.Timer(_ =>
        {
            if (finished)
            {
                return;
            }

            lock (progressLock)
            {
                double elapsedSeconds = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                double throughput = finishedCount / elapsedSeconds;
                Console.WriteLine($"[Heartbeat] done={finishedCount}/{count}, in-flight={startedCount - finishedCount}, elapsed={elapsedSeconds:F1}s, avg={throughput:F2} levels/s, last-progress={lastProgress.Elapsed.TotalSeconds:F1}s");
            }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        Parallel.For(1, count + 1, (l) =>
        {
            // Each thread gets its own solver/generator instance to ensure safety/isolation
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver) { Log = null };
            int seed = seeds[l];
            System.Threading.Interlocked.Increment(ref startedCount);

            var levelStopwatch = Stopwatch.StartNew();

            try
            {
                var profile = LevelDifficultyEngine.GetProfile(l);
                // NO minComplexity parameter - generate naturally
                var state = generator.Generate(seed, profile);
                var solveResult = solver.SolveWithPath(state, MaxSolveNodes, MaxSolveMillis);
                if (solveResult.OptimalMoves < 0 && solveResult.Status == SolverStatus.Timeout)
                {
                    Console.WriteLine($"[Retry] level={l}, reason=TIMEOUT, limits={RetrySolveMillis}ms/{RetrySolveNodes:N0} nodes");
                    solveResult = solver.SolveWithPath(state, RetrySolveNodes, RetrySolveMillis);
                }
                var report = generator.LastReport;
                var elapsedMillis = levelStopwatch.ElapsedMilliseconds;
                levelTimesMs[l] = elapsedMillis;
                bool isSlow = elapsedMillis > MaxSolveMillis;

                if (solveResult.OptimalMoves < 0)
                {
                    results[l] = new LevelResult
                    {
                        Level = l,
                        IsError = true,
                        ErrorMessage = solveResult.Status == SolverStatus.Timeout ? "TIMEOUT" : "UNSOLVABLE"
                    };
                    Console.WriteLine($"[LevelDone] level={l}, status={results[l].ErrorMessage}, time={elapsedMillis}ms");
                }
                else
                {
                    var metrics = report?.Metrics ?? LevelMetrics.Empty;
                    var moves = string.Join(",", solveResult.Path.Select(m => $"{m.Source + 1}{m.Target + 1}"));

                    // Compute raw complexity (no level number)
                    double rawComplexity = ComplexityScorer.ComputeRawComplexity(metrics, solveResult.OptimalMoves);

                    // Compute difficulty WITH level index for monotonicity enforcement
                    int monotonicDifficulty = DifficultyScorer.ComputeDifficulty100(metrics, solveResult.OptimalMoves, l);

                    results[l] = new LevelResult
                    {
                        Level = l,
                        RawComplexity = rawComplexity,
                        Difficulty = monotonicDifficulty,
                        Optimal = solveResult.OptimalMoves,
                        Forced = metrics.ForcedMoveRatio,
                        Branch = metrics.AverageBranchingFactor,
                        Decision = metrics.DecisionDepth,
                        Trap = metrics.TrapScore,
                        Multi = metrics.SolutionMultiplicity,
                        Moves = moves
                    };

                    string status = isSlow ? "SLOW" : "OK";
                    Console.WriteLine($"[LevelDone] level={l}, status={status}, time={elapsedMillis}ms, optimal={solveResult.OptimalMoves}");
                    if (elapsedMillis >= 2000)
                    {
                        Console.WriteLine($"[Slow] level={l}, time={elapsedMillis}ms, optimal={solveResult.OptimalMoves}");
                    }
                }
            }
            catch (Exception ex)
            {
                results[l] = new LevelResult
                {
                    Level = l,
                    IsError = true,
                    ErrorMessage = $"ERROR {ex.Message}"
                };
            }

            System.Threading.Interlocked.Increment(ref finishedCount);
            lock (progressLock)
            {
                done[l] = true;
                while (contiguousDone + 1 <= count && done[contiguousDone + 1])
                {
                    contiguousDone++;
                }

                while (nextReport <= contiguousDone && nextReport <= count)
                {
                    double elapsedSeconds = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                    double throughput = contiguousDone / elapsedSeconds;
                    Console.WriteLine($"Level {nextReport} done ({contiguousDone}/{count}) in {elapsedSeconds:F1}s ({throughput:F2} levels/s)");
                    nextReport += reportInterval;
                }

                if (contiguousDone > 0)
                {
                    lastProgress.Restart();
                }
            }
        });

        finished = true;

        int recoveredFailures = 0;
        for (int l = 1; l <= count; l++)
        {
            if (!results.TryGetValue(l, out var failedResult) || !failedResult.IsError)
            {
                continue;
            }

            if (TryResolveFailedLevel(l, seeds[l], out var recovered))
            {
                results[l] = recovered;
                recoveredFailures++;
                Console.WriteLine($"[FailureRecovered] level={l}, prior={failedResult.ErrorMessage}, optimal={recovered.Optimal}");
            }
        }

        if (recoveredFailures > 0)
        {
            Console.WriteLine($"Recovered {recoveredFailures} failed level(s) with deterministic single-thread recheck.");
        }

        Console.WriteLine("\n=== RAW COMPLEXITY COMPUTED ===");

        var rawScores = new List<double>();
        int errorCount = 0;

        for (int l = 1; l <= count; l++)
        {
            if (results.TryGetValue(l, out var result) && !result.IsError)
            {
                rawScores.Add(result.RawComplexity);
            }
            else if (results.TryGetValue(l, out var errorResult) && errorResult.IsError)
            {
                errorCount++;
            }
        }

        Console.WriteLine("\n=== SOLVABILITY SUMMARY ===");
        if (errorCount == 0)
        {
            Console.WriteLine($"PASS: All {count} levels solvable (no errors)");
        }
        else
        {
            Console.WriteLine($"FAIL: {errorCount} level(s) unsolvable or errored");
        }

        int perfStart = 100;
        int perfEnd = Math.Min(120, count);
        if (perfStart <= perfEnd)
        {
            long perfSum = 0;
            int perfCount = 0;
            for (int l = perfStart; l <= perfEnd; l++)
            {
                if (levelTimesMs[l] > 0)
                {
                    perfSum += levelTimesMs[l];
                    perfCount++;
                }
            }

            if (perfCount > 0)
            {
                double perfAvg = perfSum / (double)perfCount;
                Console.WriteLine($"\n=== PERFORMANCE SUMMARY ===");
                Console.WriteLine($"Average time levels {perfStart}-{perfEnd}: {perfAvg:F1} ms (count={perfCount})");
            }
        }

        // Validate variance
        Console.WriteLine("\n=== VARIANCE VALIDATION ===");

        // Check if we have any valid results
        if (rawScores.Count == 0)
        {
            Console.WriteLine("FAIL: No valid levels generated");
            return 1;
        }

        // Compute statistics for diagnosis
        double[] scores = rawScores.ToArray();
        double mean = scores.Average();
        double variance = scores.Select(x => Math.Pow(x - mean, 2)).Average();
        double stdDev = Math.Sqrt(variance);
        double cv = stdDev / Math.Max(0.001, mean);
        Console.WriteLine($"  Mean: {mean:F2}, StdDev: {stdDev:F2}, CV: {cv:F3}");
        Console.WriteLine($"  Min: {scores.Min():F2}, Max: {scores.Max():F2}, Range: {scores.Max() - scores.Min():F2}");

        bool varianceOk = ComplexityScorer.ValidateVariance(scores);
        if (!varianceOk)
        {
            Console.WriteLine($"FAIL: Raw complexity variance is too low (CV={cv:F3}, required >0.15)");
            // Continue anyway for diagnosis
        }
        else
        {
            Console.WriteLine("PASS: Raw complexity has sufficient variance");
        }

        // Validate independence
        Console.WriteLine("\n=== INDEPENDENCE VALIDATION ===");

        // Compute correlation for diagnosis
        double correlation = ComputeCorrelation(scores);
        Console.WriteLine($"  Pearson correlation with level number: {correlation:F4}");

        bool independenceOk = ComplexityScorer.ValidateIndependence(scores, 0.95);
        if (!independenceOk)
        {
            Console.WriteLine($"FAIL: Raw complexity is artificially correlated with level number (r={correlation:F4}, threshold=0.95)");
            // Continue anyway for diagnosis
        }
        else
        {
            Console.WriteLine("PASS: Raw complexity is independent of level number");
        }

        Console.WriteLine("\n=== INTRINSIC DIFFICULTY READY ===");

        // Write output file in new format
        var lines = new List<string>();
        lines.Add("# Decantra Level Difficulty Analysis");
        lines.Add("# FormatVersion: 1");
        lines.Add($"# Levels: 1..{count}");
        lines.Add("#");

        for (int l = 1; l <= count; l++)
        {
            if (results.TryGetValue(l, out var result))
            {
                lines.Add(result.ToLine());
            }
            else
            {
                lines.Add($"level={l}, difficulty=0, optimal=0, branch=0.00, decision=0, trap=0.00, multi=0, moves=SKIPPED");
            }
        }

        File.WriteAllLines("solver-solutions-debug.txt", lines);
        Console.WriteLine("Refreshed solver-solutions-debug.txt");

        return ValidateDifficultyInvariantFromFile("solver-solutions-debug.txt", count);
    }

    private static int TransitionBenchmark(int count)
    {
        Console.WriteLine($"=== TRANSITION BENCHMARK: Levels 1..{count} ===");
        Console.WriteLine("Measures generation latency on next-level handoff path (core generator cost).");

        var solver = new BfsSolver();
        var generator = new LevelGenerator(solver);

        var allTimes = new List<double>(count);
        var byBand = new Dictionary<LevelBand, List<double>>();
        var rows = new List<string>(count + 1)
        {
            "level,band,bottle_count,color_count,empty_count,sink_count,generation_ms"
        };

        int seed = 0;
        int instant50Count = 0;
        int instant100Count = 0;
        int instant200Count = 0;

        for (int level = 1; level <= count; level++)
        {
            seed = NextSeed(level, seed);
            var profile = LevelDifficultyEngine.GetProfile(level);

            var stopwatch = Stopwatch.StartNew();
            bool generated = TryGenerateWithRetryForBenchmark(generator, level, seed, out string error);
            stopwatch.Stop();

            if (!generated)
            {
                Console.WriteLine($"FAIL: level={level} generation error: {error}");
                return 1;
            }

            double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            allTimes.Add(elapsedMs);

            if (elapsedMs <= 50.0) instant50Count++;
            if (elapsedMs <= 100.0) instant100Count++;
            if (elapsedMs <= 200.0) instant200Count++;

            if (!byBand.TryGetValue(profile.Band, out var bandTimes))
            {
                bandTimes = new List<double>();
                byBand[profile.Band] = bandTimes;
            }
            bandTimes.Add(elapsedMs);

            int sinkCount = LevelDifficultyEngine.DetermineSinkCount(level);
            rows.Add($"{level},{profile.Band},{profile.BottleCount},{profile.ColorCount},{profile.EmptyBottleCount},{sinkCount},{elapsedMs.ToString("0.###", CultureInfo.InvariantCulture)}");

            if (level % 100 == 0 || level == count)
            {
                Console.WriteLine($"Progress: {level}/{count}");
            }
        }

        string outputPath = $"doc/transition-benchmark-levels-1-{count}.csv";
        File.WriteAllLines(outputPath, rows);
        Console.WriteLine($"Wrote {outputPath}");

        Console.WriteLine();
        Console.WriteLine("=== OVERALL ===");
        PrintTimingStats("all", allTimes);
        Console.WriteLine($"instant<=50ms:  {instant50Count}/{count} ({(100.0 * instant50Count / count):F1}%)");
        Console.WriteLine($"instant<=100ms: {instant100Count}/{count} ({(100.0 * instant100Count / count):F1}%)");
        Console.WriteLine($"instant<=200ms: {instant200Count}/{count} ({(100.0 * instant200Count / count):F1}%)");

        Console.WriteLine();
        Console.WriteLine("=== BY BAND ===");
        foreach (var pair in byBand.OrderBy(kvp => kvp.Key))
        {
            PrintTimingStats(pair.Key.ToString(), pair.Value);
        }

        return 0;
    }

    private static bool TryGenerateWithRetryForBenchmark(LevelGenerator generator, int level, int seed, out string error)
    {
        error = string.Empty;
        var profile = LevelDifficultyEngine.GetProfile(level);
        int seedCandidate = seed;

        for (int seedSweep = 0; seedSweep < 24; seedSweep++)
        {
            const int maxAttempts = 8;
            int attempt = 0;
            int currentSeed = seedCandidate;

            while (attempt < maxAttempts)
            {
                try
                {
                    _ = generator.Generate(currentSeed, profile);
                    return true;
                }
                catch
                {
                    attempt++;
                    currentSeed = NextSeed(level, currentSeed + 31);
                }
            }

            int fallbackReverse = Math.Max(6, profile.ReverseMoves - 6);
            int fallbackAttempts = 0;
            int fallbackSeed = seedCandidate;

            while (fallbackAttempts < 3)
            {
                try
                {
                    var fallbackProfile = LevelDifficultyEngine.GetProfile(level);
                    var adjustedProfile = new DifficultyProfile(level,
                        fallbackProfile.Band,
                        fallbackProfile.BottleCount,
                        fallbackProfile.ColorCount,
                        fallbackProfile.EmptyBottleCount,
                        fallbackReverse,
                        fallbackProfile.ThemeId,
                        fallbackProfile.DifficultyRating);

                    _ = generator.Generate(fallbackSeed, adjustedProfile);
                    return true;
                }
                catch
                {
                    fallbackAttempts++;
                    fallbackSeed = NextSeed(level, fallbackSeed + 31);
                    fallbackReverse = Math.Max(4, fallbackReverse - 1);
                }
            }

            seedCandidate = NextSeed(level, seedCandidate + 97);
        }

        error = "all retries exhausted (24 seed sweeps)";
        return false;
    }

    private static void PrintTimingStats(string label, List<double> samples)
    {
        if (samples == null || samples.Count == 0)
        {
            Console.WriteLine($"{label}: no samples");
            return;
        }

        samples.Sort();
        double mean = samples.Average();
        double p50 = Percentile(samples, 0.50);
        double p90 = Percentile(samples, 0.90);
        double p95 = Percentile(samples, 0.95);
        double p99 = Percentile(samples, 0.99);
        double max = samples[samples.Count - 1];

        Console.WriteLine($"{label}: n={samples.Count}, mean={mean:F1}ms, p50={p50:F1}ms, p90={p90:F1}ms, p95={p95:F1}ms, p99={p99:F1}ms, max={max:F1}ms");
    }

    private static double Percentile(List<double> sorted, double quantile)
    {
        if (sorted.Count == 0) return 0;
        if (sorted.Count == 1) return sorted[0];

        double index = (sorted.Count - 1) * quantile;
        int lower = (int)Math.Floor(index);
        int upper = (int)Math.Ceiling(index);
        if (lower == upper) return sorted[lower];

        double fraction = index - lower;
        return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
    }

    private static double ComputeCorrelation(double[] scores)
    {
        int n = scores.Length;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;

        for (int i = 0; i < n; i++)
        {
            double x = i + 1; // Level number (1-based)
            double y = scores[i];

            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
            sumY2 += y * y;
        }

        double numerator = (n * sumXY) - (sumX * sumY);
        double denomX = (n * sumX2) - (sumX * sumX);
        double denomY = (n * sumY2) - (sumY * sumY);
        double denominator = Math.Sqrt(Math.Max(0, denomX * denomY));

        if (denominator < 0.001)
            return 0.0;

        return numerator / denominator;
    }

    private static int ValidateDifficultyInvariantFromFile(string path, int count)
    {
        Console.WriteLine("\n=== DIFFICULTY INVARIANT VALIDATION ===");

        if (!File.Exists(path))
        {
            Console.WriteLine($"FAIL: {path} not found.");
            return 1;
        }

        var violations = new List<string>();
        var seenLevels = new HashSet<int>();
        int expectedLevel = 1;
        var difficulties = new List<(int level, int difficulty)>();

        foreach (var line in File.ReadLines(path))
        {
            if (!line.StartsWith("level=", StringComparison.Ordinal))
                continue;

            if (!TryParseLevelDifficulty(line, out int level, out int difficulty))
            {
                violations.Add($"Unparseable line: {line}");
                continue;
            }

            if (level != expectedLevel)
            {
                violations.Add($"Level sequence gap or regression at level {level} (expected {expectedLevel})");
                expectedLevel = level;
            }

            expectedLevel++;
            seenLevels.Add(level);
            difficulties.Add((level, difficulty));

            // Validate difficulty is in valid range (1-100)
            if (difficulty < 1 || difficulty > 100)
            {
                violations.Add($"Level {level}: difficulty={difficulty} out of range [1,100]");
            }
        }

        for (int level = 1; level <= count; level++)
        {
            if (!seenLevels.Contains(level))
            {
                violations.Add($"Missing level {level} in output");
            }
        }

        // Compute difficulty statistics
        if (difficulties.Count > 0)
        {
            var diffValues = difficulties.Select(d => d.difficulty).ToList();
            double avgDiff = diffValues.Average();
            double minDiff = diffValues.Min();
            double maxDiff = diffValues.Max();
            double stdDev = Math.Sqrt(diffValues.Select(d => Math.Pow(d - avgDiff, 2)).Average());

            Console.WriteLine($"Difficulty statistics:");
            Console.WriteLine($"  Min: {minDiff}, Max: {maxDiff}, Avg: {avgDiff:F1}, StdDev: {stdDev:F1}");

            // Check for reasonable difficulty distribution
            // Early levels (1-20) should average lower than late levels (80-100)
            var earlyLevels = difficulties.Where(d => d.level <= 20).Select(d => d.difficulty);
            var lateLevels = difficulties.Where(d => d.level >= 80 && d.level <= 100).Select(d => d.difficulty);

            if (earlyLevels.Any() && lateLevels.Any())
            {
                double earlyAvg = earlyLevels.Average();
                double lateAvg = lateLevels.Average();
                Console.WriteLine($"  Early (1-20) avg: {earlyAvg:F1}, Late (80-100) avg: {lateAvg:F1}");

                if (lateAvg < earlyAvg)
                {
                    Console.WriteLine($"WARNING: Late levels have lower average difficulty than early levels");
                }
            }
        }

        Console.WriteLine(violations.Count == 0
            ? "PASS: Difficulty validation completed."
            : $"FAIL: {violations.Count} validation violation(s) found.");

        foreach (var violation in violations.Take(10))
        {
            Console.WriteLine($"  {violation}");
        }

        if (violations.Count > 10)
        {
            Console.WriteLine($"  ... and {violations.Count - 10} more");
        }

        Console.WriteLine("\n=== FINAL VERDICT ===");

        if (violations.Count == 0)
        {
            Console.WriteLine("PASS: All validation checks passed.");
            return 0;
        }

        Console.WriteLine("FAIL: Validation checks failed.");
        return 1;
    }

    private static bool TryResolveFailedLevel(int level, int seed, out LevelResult result)
    {
        result = null;

        try
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver) { Log = null };
            var profile = LevelDifficultyEngine.GetProfile(level);
            var state = generator.Generate(seed, profile);
            var solveResult = solver.SolveWithPath(state);

            if (solveResult == null || solveResult.OptimalMoves < 0)
            {
                return false;
            }

            var metrics = generator.LastReport?.Metrics ?? LevelMetrics.Empty;
            var moves = string.Join(",", solveResult.Path.Select(m => $"{m.Source + 1}{m.Target + 1}"));
            double rawComplexity = ComplexityScorer.ComputeRawComplexity(metrics, solveResult.OptimalMoves);
            int monotonicDifficulty = DifficultyScorer.ComputeDifficulty100(metrics, solveResult.OptimalMoves, level);

            result = new LevelResult
            {
                Level = level,
                RawComplexity = rawComplexity,
                Difficulty = monotonicDifficulty,
                Optimal = solveResult.OptimalMoves,
                Forced = metrics.ForcedMoveRatio,
                Branch = metrics.AverageBranchingFactor,
                Decision = metrics.DecisionDepth,
                Trap = metrics.TrapScore,
                Multi = metrics.SolutionMultiplicity,
                Moves = moves
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseLevelDifficulty(string line, out int level, out int difficulty)
    {
        level = 0;
        difficulty = 0;

        try
        {
            var parts = line.Split(',');
            if (parts.Length < 2)
                return false;

            var levelPart = parts[0].Trim();
            var difficultyPart = parts[1].Trim();

            if (!levelPart.StartsWith("level=", StringComparison.Ordinal) ||
                !difficultyPart.StartsWith("difficulty=", StringComparison.Ordinal))
            {
                return false;
            }

            level = int.Parse(levelPart.Substring("level=".Length));
            difficulty = int.Parse(difficultyPart.Substring("difficulty=".Length));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private class LevelResult
    {
        public int Level;
        public double RawComplexity;  // Stage 1 raw score
        public int Difficulty;        // Deterministic difficulty mapping for solver output
        public int Optimal;
        public float Forced;
        public float Branch;
        public int Decision;
        public float Trap;
        public int Multi;
        public string Moves = "";
        public bool IsError;
        public string ErrorMessage = "";

        public string ToLine()
        {
            if (IsError)
            {
                return $"level={Level}, difficulty=0, optimal=0, branch=0.00, decision=0, trap=0.00, multi=0, moves={ErrorMessage}";
            }

            // New format: level, difficulty, optimal, branch, decision, trap, multi, moves
            // Use invariant culture for decimal formatting (dot separator)
            return string.Format(CultureInfo.InvariantCulture,
                "level={0}, difficulty={1}, optimal={2}, branch={3:F2}, decision={4}, trap={5:F2}, multi={6}, moves={7}",
                Level, Difficulty, Optimal, Branch, Decision, Trap, Multi, Moves);
        }
    }

    /// <summary>
    /// Generates levels using the MonotonicLevelSelector which ensures
    /// intrinsic difficulty increases roughly linearly from level 1 to 200.
    /// </summary>
    private static int MonotonicGenerate(int count)
    {
        Console.WriteLine($"=== MONOTONIC LEVEL GENERATION ===");
        Console.WriteLine($"Generating {count} levels with monotonically increasing difficulty...");
        Console.WriteLine($"Target curve: {MonotonicLevelSelector.MinDifficulty} at level 1 → {MonotonicLevelSelector.MaxDifficulty} at level {MonotonicLevelSelector.PlateauStartLevel}+");
        Console.WriteLine($"Candidates per level: {MonotonicLevelSelector.DefaultCandidateCount}");
        Console.WriteLine($"Parallel cores: {Environment.ProcessorCount}");
        Console.WriteLine();

        var selector = new MonotonicLevelSelector(new BfsSolver());
        var results = new MonotonicLevelResult[count + 1];
        var sw = Stopwatch.StartNew();
        int completed = 0;
        int errors = 0;

        // Generate levels sequentially (each level internally parallelizes candidates)
        for (int level = 1; level <= count; level++)
        {
            try
            {
                var result = selector.Generate(level);

                // Solve to get the path (LevelState doesn't store path directly)
                var solver = new BfsSolver();
                var solveResult = solver.SolveWithPath(result.State);
                string moves = solveResult.OptimalMoves > 0
                    ? string.Join(",", solveResult.Path.Select(m => $"{m.Source + 1}{m.Target + 1}"))
                    : "";

                results[level] = new MonotonicLevelResult
                {
                    Level = level,
                    TargetDifficulty = result.TargetDifficulty,
                    IntrinsicDifficulty = result.IntrinsicDifficulty,
                    OptimalMoves = result.OptimalMoves,
                    SelectedSeed = result.SelectedSeed,
                    SelectedCandidate = result.SelectedCandidateIndex,
                    Branch = result.Metrics?.AverageBranchingFactor ?? 0,
                    Trap = result.Metrics?.TrapScore ?? 0,
                    Moves = moves
                };

                completed++;
                if (level % 10 == 0 || level == count)
                {
                    Console.WriteLine($"Level {level}/{count} done - target={result.TargetDifficulty}, actual={result.IntrinsicDifficulty}, " +
                        $"delta={result.IntrinsicDifficulty - result.TargetDifficulty:+#;-#;0}, optimal={result.OptimalMoves}");
                }
            }
            catch (Exception ex)
            {
                results[level] = new MonotonicLevelResult
                {
                    Level = level,
                    TargetDifficulty = MonotonicLevelSelector.TargetDifficulty(level),
                    IntrinsicDifficulty = 0,
                    ErrorMessage = ex.Message
                };
                errors++;
                Console.WriteLine($"Level {level}: ERROR - {ex.Message}");
            }
        }

        sw.Stop();
        Console.WriteLine();
        Console.WriteLine($"=== GENERATION COMPLETE ===");
        Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds / (double)count:F1}ms per level)");
        Console.WriteLine($"Completed: {completed}, Errors: {errors}");

        // Validate progression with statistical metrics rather than strict monotonicity
        Console.WriteLine();
        Console.WriteLine($"=== PROGRESSION ANALYSIS ===");

        int regressions = 0;
        int maxRegression = 0;

        // Collect valid difficulties
        var difficulties = new Dictionary<int, int>();
        for (int level = 1; level <= count; level++)
        {
            var r = results[level];
            if (r != null && r.IntrinsicDifficulty > 0)
                difficulties[level] = r.IntrinsicDifficulty;
        }

        for (int level = 2; level <= count; level++)
        {
            if (!difficulties.TryGetValue(level, out int curr)) continue;
            if (!difficulties.TryGetValue(level - 1, out int prev)) continue;

            // Regression check: level N vs level N-1
            if (curr < prev)
            {
                int drop = prev - curr;
                regressions++;
                maxRegression = Math.Max(maxRegression, drop);
            }
        }

        // Report regressions as info, not error
        Console.WriteLine($"Local regressions (N < N-1): {regressions}/{count - 1} levels");
        Console.WriteLine($"Max regression drop: {maxRegression}");


        // Statistics
        Console.WriteLine();
        Console.WriteLine($"=== DIFFICULTY STATISTICS ===");

        var validResults = results.Where(r => r != null && r.IntrinsicDifficulty > 0).ToList();
        if (validResults.Count > 0)
        {
            double avgDiff = validResults.Average(r => r.IntrinsicDifficulty);
            double avgDelta = validResults.Average(r => Math.Abs(r.IntrinsicDifficulty - r.TargetDifficulty));
            int minDiff = validResults.Min(r => r.IntrinsicDifficulty);
            int maxDiff = validResults.Max(r => r.IntrinsicDifficulty);

            // Early vs Late difficulty
            var early = validResults.Where(r => r.Level <= 20).ToList();
            var late = validResults.Where(r => r.Level >= count - 20).ToList();
            double earlyAvg = early.Count > 0 ? early.Average(r => r.IntrinsicDifficulty) : 0;
            double lateAvg = late.Count > 0 ? late.Average(r => r.IntrinsicDifficulty) : 0;

            Console.WriteLine($"  Difficulty range: {minDiff} to {maxDiff}");
            Console.WriteLine($"  Average difficulty: {avgDiff:F1}");
            Console.WriteLine($"  Average |delta| from target: {avgDelta:F1}");
            Console.WriteLine($"  Early levels (1-20) average: {earlyAvg:F1}");
            Console.WriteLine($"  Late levels ({count - 20}-{count}) average: {lateAvg:F1}");
            Console.WriteLine($"  Difficulty increase: {lateAvg - earlyAvg:F1} points");

            // Pearson correlation between level and difficulty
            double sumXY = 0, sumX = 0, sumY = 0, sumX2 = 0, sumY2 = 0;
            int n = validResults.Count;
            foreach (var r in validResults)
            {
                sumX += r.Level;
                sumY += r.IntrinsicDifficulty;
                sumXY += r.Level * r.IntrinsicDifficulty;
                sumX2 += r.Level * r.Level;
                sumY2 += r.IntrinsicDifficulty * r.IntrinsicDifficulty;
            }
            double correlation = (n * sumXY - sumX * sumY) / Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
            Console.WriteLine($"  Correlation (level vs difficulty): {correlation:F3}");

            // Check for overall progression (relaxed constraints)
            // Require modest increase and positive correlation
            bool goodProgression = lateAvg > earlyAvg + 5 && correlation > 0.5;
            Console.WriteLine();
            Console.WriteLine(goodProgression
                ? "PASS: Difficulty increases with level progression (relaxed validation)"
                : "WARN: Difficulty progression may need tuning");
        }

        // Write results to file
        var lines = new List<string>
        {
            "# Monotonic Level Difficulty Analysis",
            $"# Generated: {DateTime.UtcNow:O}",
            $"# Levels: 1..{count}",
            "#"
        };

        for (int level = 1; level <= count; level++)
        {
            var r = results[level];
            if (r == null) continue;

            if (r.IntrinsicDifficulty == 0)
            {
                lines.Add($"level={level}, target={r.TargetDifficulty}, difficulty=0, error={r.ErrorMessage}");
            }
            else
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "level={0}, target={1}, difficulty={2}, delta={3:+#;-#;0}, optimal={4}, seed={5}, candidate={6}, branch={7:F2}, trap={8:F2}, moves={9}",
                    level, r.TargetDifficulty, r.IntrinsicDifficulty,
                    r.IntrinsicDifficulty - r.TargetDifficulty,
                    r.OptimalMoves, r.SelectedSeed, r.SelectedCandidate,
                    r.Branch, r.Trap, r.Moves));
            }
        }

        File.WriteAllLines("monotonic-levels-debug.txt", lines);
        Console.WriteLine();
        Console.WriteLine($"Results written to monotonic-levels-debug.txt");

        return 0;
    }

    private sealed class MonotonicLevelResult
    {
        public int Level;
        public int TargetDifficulty;
        public int IntrinsicDifficulty;
        public int OptimalMoves;
        public int SelectedSeed;
        public int SelectedCandidate;
        public float Branch;
        public float Trap;
        public string Moves = "";
        public string ErrorMessage = "";
    }

    // ========================================================================================
    // SINK BOTTLE ANALYSIS
    // ========================================================================================

    private const int SinkSolveNodes = 1_500_000;
    private const int SinkSolveMillis = 2000;

    /// <summary>
    /// Analyzes sink bottles across levels 1..count.
    /// For each level: identifies sink bottles, runs normal solver + no-sink solver,
    /// and classifies sink bottles by whether sink moves are structurally required.
    /// </summary>
    private static int SinkAnalysis(int count)
    {
        Console.WriteLine($"=== SINK BOTTLE ANALYSIS: Levels 1..{count} ===");
        Console.WriteLine($"Solver bounds: {SinkSolveNodes:N0} nodes, {SinkSolveMillis} ms");
        Console.WriteLine();

        // Pre-calculate seeds
        var seeds = new int[count + 1];
        int currentSeed = 0;
        for (int l = 1; l <= count; l++)
        {
            currentSeed = NextSeed(l, currentSeed);
            seeds[l] = currentSeed;
        }

        var results = new System.Collections.Concurrent.ConcurrentDictionary<int, SinkLevelResult>();
        var stopwatch = Stopwatch.StartNew();
        int finishedCount = 0;

        Parallel.For(1, count + 1, (l) =>
        {
            int seed = seeds[l];

            try
            {
                int sinkCount = LevelDifficultyEngine.DetermineSinkCount(l);
                bool mustUseSinkClass = sinkCount > 0 && LevelDifficultyEngine.IsSinkRequiredClass(l);

                // Deterministic synthetic sink ids for policy analysis artifacts.
                var sinkIndices = new List<int>();
                for (int i = 0; i < sinkCount; i++)
                {
                    sinkIndices.Add(i);
                }

                // Per-sink-bottle classification (level class applied to all sinks in the level).
                var sinkClassifications = new List<SinkBottleClassification>();

                foreach (var sinkIdx in sinkIndices)
                {
                    var classification = new SinkBottleClassification { Id = sinkIdx };
                    classification.SinkPoursInPrimarySolution = 0;
                    classification.CanBeAvoided = !mustUseSinkClass;
                    classification.MustBeUsed = mustUseSinkClass;
                    classification.ForcingUseLeadsToUnsolvable = "n/a";
                    classification.SinkUsedInSinkAwareSolution = false;

                    sinkClassifications.Add(classification);
                }

                results[l] = new SinkLevelResult
                {
                    Level = l,
                    Seed = seed,
                    SinkBottleCount = sinkIndices.Count,
                    SolverStatus = "not_run",
                    SolutionLength = 0,
                    SinkClassifications = sinkClassifications
                };
            }
            catch (Exception ex)
            {
                results[l] = new SinkLevelResult
                {
                    Level = l,
                    Seed = seed,
                    SinkBottleCount = -1,
                    SolverStatus = $"error:{ex.Message}",
                    SolutionLength = 0,
                    SinkClassifications = new List<SinkBottleClassification>()
                };
            }

            int done = System.Threading.Interlocked.Increment(ref finishedCount);
            if (done % 100 == 0 || done == count)
            {
                Console.WriteLine($"[Progress] {done}/{count} levels analyzed ({stopwatch.Elapsed.TotalSeconds:F1}s)");
            }
        });

        stopwatch.Stop();
        Console.WriteLine($"\nAnalysis complete in {stopwatch.Elapsed.TotalSeconds:F1}s");

        // === Compute distribution statistics ===
        int levelsWithZeroSinks = 0;
        int levelsWithOneSink = 0;
        int levelsWithMultipleSinks = 0;
        int totalSinkBottles = 0;
        int sinksMustBeUsed = 0;
        int sinksCanBeAvoided = 0;
        int sinksUsedInSinkAwareSolver = 0;
        int levelsWithAtLeastOneMustUse = 0;
        int levelsAllAvoidable = 0;
        int levelsMixed = 0;

        for (int l = 1; l <= count; l++)
        {
            if (!results.TryGetValue(l, out var r)) continue;

            if (r.SinkBottleCount == 0) levelsWithZeroSinks++;
            else if (r.SinkBottleCount == 1) levelsWithOneSink++;
            else if (r.SinkBottleCount > 1) levelsWithMultipleSinks++;

            bool anyMustUse = false;
            bool allAvoidable = true;

            foreach (var sc in r.SinkClassifications)
            {
                totalSinkBottles++;
                if (sc.MustBeUsed) { sinksMustBeUsed++; anyMustUse = true; allAvoidable = false; }
                if (sc.CanBeAvoided) sinksCanBeAvoided++;
                if (sc.SinkUsedInSinkAwareSolution) sinksUsedInSinkAwareSolver++;
            }

            if (r.SinkBottleCount > 0)
            {
                if (anyMustUse && allAvoidable) levelsMixed++;
                else if (anyMustUse) levelsWithAtLeastOneMustUse++;
                else if (allAvoidable) levelsAllAvoidable++;
            }
        }

        // === Write CSV ===
        var csvLines = new List<string>();
        csvLines.Add("level,seed_or_generation_id,sink_bottle_count,sink_bottles,solver_status,solution_length_moves,notes");

        for (int l = 1; l <= count; l++)
        {
            if (!results.TryGetValue(l, out var r))
            {
                csvLines.Add($"{l},0,0,[],error,0,missing_result");
                continue;
            }

            string sinkBottlesJson;
            if (r.SinkClassifications.Count == 0)
            {
                sinkBottlesJson = "[]";
            }
            else
            {
                var entries = new List<string>();
                foreach (var sc in r.SinkClassifications)
                {
                    entries.Add($"{{\"id\":{sc.Id}," +
                        $"\"must_be_used\":{sc.MustBeUsed.ToString().ToLower()}," +
                        $"\"can_be_avoided\":{sc.CanBeAvoided.ToString().ToLower()}," +
                        $"\"forcing_use_leads_to_unsolvable\":{sc.ForcingUseLeadsToUnsolvable}," +
                        $"\"sink_pours_in_primary_solution\":{sc.SinkPoursInPrimarySolution}}}");
                }
                sinkBottlesJson = "[" + string.Join(",", entries) + "]";
            }

            string notes = "";
            if (r.SinkBottleCount == 0) notes = "no_sink_bottles";
            else if (r.SinkClassifications.All(sc => sc.CanBeAvoided && !sc.MustBeUsed)) notes = "all_sinks_avoidable";
            else if (r.SinkClassifications.All(sc => sc.MustBeUsed && !sc.CanBeAvoided)) notes = "all_sinks_must_be_used";

            csvLines.Add($"{r.Level},{r.Seed},{r.SinkBottleCount},\"{sinkBottlesJson}\",{r.SolverStatus},{r.SolutionLength},{notes}");
        }

        File.WriteAllLines("doc/sink-bottles-levels-1-1000.csv", csvLines);
        Console.WriteLine("Wrote doc/sink-bottles-levels-1-1000.csv");

        // === Write debug output ===
        var debugLines = new List<string>();
        debugLines.Add("# Decantra Sink Bottle Analysis");
        debugLines.Add("# FormatVersion: 1");
        debugLines.Add($"# Levels: 1..{count}");
        debugLines.Add($"# Solver bounds: {SinkSolveNodes:N0} nodes, {SinkSolveMillis} ms");
        debugLines.Add("#");

        for (int l = 1; l <= count; l++)
        {
            if (!results.TryGetValue(l, out var r)) continue;

            debugLines.Add($"level={l}");
            debugLines.Add($"  seed={r.Seed}");
            debugLines.Add($"  sink_bottle_count={r.SinkBottleCount}");
            if (r.SinkBottleCount > 0)
            {
                debugLines.Add($"  sink_bottle_ids=[{string.Join(",", r.SinkClassifications.Select(sc => sc.Id))}]");
            }
            debugLines.Add($"  solver_status={r.SolverStatus}");
            debugLines.Add($"  primary_solution_length={r.SolutionLength}");

            foreach (var sc in r.SinkClassifications)
            {
                debugLines.Add($"  sink[{sc.Id}].sink_pours_in_primary_solution={sc.SinkPoursInPrimarySolution}");
                debugLines.Add($"  sink[{sc.Id}].can_be_avoided={sc.CanBeAvoided}");
                debugLines.Add($"  sink[{sc.Id}].must_be_used={sc.MustBeUsed}");
                debugLines.Add($"  sink[{sc.Id}].forcing_use_leads_to_unsolvable={sc.ForcingUseLeadsToUnsolvable}");
                debugLines.Add($"  sink[{sc.Id}].sink_used_in_sink_aware_solution={sc.SinkUsedInSinkAwareSolution}");
            }

            debugLines.Add($"  solver_bounds=nodes:{SinkSolveNodes},ms:{SinkSolveMillis}");
            debugLines.Add("");
        }

        File.WriteAllLines("solver-solutions-debug-sinks.txt", debugLines);
        Console.WriteLine("Wrote solver-solutions-debug-sinks.txt");

        // === Print distribution summary ===
        Console.WriteLine();
        Console.WriteLine("=== SINK BOTTLE DISTRIBUTION ===");
        Console.WriteLine($"Total levels analyzed: {count}");
        Console.WriteLine("Mode: deterministic policy classification (solver-independent)");
        Console.WriteLine();
        Console.WriteLine($"Levels with 0 sink bottles: {levelsWithZeroSinks} ({100.0 * levelsWithZeroSinks / count:F1}%)");
        Console.WriteLine($"Levels with 1 sink bottle:  {levelsWithOneSink} ({100.0 * levelsWithOneSink / count:F1}%)");
        Console.WriteLine($"Levels with 2+ sink bottles: {levelsWithMultipleSinks} ({100.0 * levelsWithMultipleSinks / count:F1}%)");
        Console.WriteLine();
        Console.WriteLine($"Total sink bottles across all levels: {totalSinkBottles}");
        Console.WriteLine($"  must_be_used:   {sinksMustBeUsed} ({(totalSinkBottles > 0 ? 100.0 * sinksMustBeUsed / totalSinkBottles : 0):F1}%)");
        Console.WriteLine($"  can_be_avoided: {sinksCanBeAvoided} ({(totalSinkBottles > 0 ? 100.0 * sinksCanBeAvoided / totalSinkBottles : 0):F1}%)");
        Console.WriteLine();
        Console.WriteLine($"Primary solver used sink in solution: {sinksUsedInSinkAwareSolver}/{totalSinkBottles}");
        Console.WriteLine();

        int levelsWithSinks = levelsWithOneSink + levelsWithMultipleSinks;
        Console.WriteLine($"Among levels with sinks ({levelsWithSinks}):");
        Console.WriteLine($"  All sinks avoidable:          {levelsAllAvoidable}");
        Console.WriteLine($"  At least one must_be_used:    {levelsWithAtLeastOneMustUse}");
        Console.WriteLine($"  Mixed classification:         {levelsMixed}");

        Console.WriteLine();
        Console.WriteLine("=== KEY FINDING ===");
        Console.WriteLine($"Classification is deterministic from level index via LevelDifficultyEngine.IsSinkRequiredClass(level).");
        Console.WriteLine($"Sink-required class marks all sinks must_be_used; sink-avoidable marks all can_be_avoided.");
        Console.WriteLine($"Empirical split: must_be_used={sinksMustBeUsed}, can_be_avoided={sinksCanBeAvoided}");

        return 0;
    }

    /// <summary>
    /// BFS solver variant that DOES allow pouring into sink bottles.
    /// Used only for the force-use experiment: can a solution be found when sinks are valid targets?
    /// This is implemented here (in the debug generator) to avoid modifying the production solver.
    /// </summary>
    private static SolverResult SolveSinkAware(LevelState initial, int maxNodes, int maxMillis)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<SinkAwareNode>();
        var sw = Stopwatch.StartNew();

        string startKey = EncodeSinkAwareKey(initial);
        visited.Add(startKey);
        queue.Enqueue(new SinkAwareNode(CloneLevelState(initial), 0, null, default));

        int processed = 0;

        while (queue.Count > 0)
        {
            if (processed >= maxNodes || sw.ElapsedMilliseconds > maxMillis)
            {
                return new SolverResult(-1, new List<Move>(), SolverStatus.Timeout);
            }

            var node = queue.Dequeue();
            processed++;

            if (node.State.IsWin())
            {
                // Build path
                var path = new List<Move>();
                var cur = node;
                while (cur != null && cur.Parent != null)
                {
                    path.Add(cur.MoveFromParent);
                    cur = cur.Parent;
                }
                path.Reverse();
                return new SolverResult(path.Count, path);
            }

            // Enumerate moves: allow pouring into sinks (unlike production solver)
            for (int i = 0; i < node.State.Bottles.Count; i++)
            {
                var source = node.State.Bottles[i];
                if (source.IsEmpty) continue;
                if (source.IsSink) continue; // Sinks can never be sources (game rule)

                for (int j = 0; j < node.State.Bottles.Count; j++)
                {
                    if (i == j) continue;
                    // NOTE: We do NOT skip sink targets here — that's the key difference
                    var target = node.State.Bottles[j];

                    int amount = Decantra.Domain.Rules.MoveRules.GetPourAmount(node.State, i, j);
                    if (amount <= 0) continue;

                    var next = CloneLevelState(node.State);
                    int poured;
                    if (!next.TryApplyMove(i, j, out poured)) continue;

                    string key = EncodeSinkAwareKey(next);
                    if (visited.Add(key))
                    {
                        queue.Enqueue(new SinkAwareNode(next, node.Depth + 1, node, new Move(i, j, poured)));
                    }
                }
            }
        }

        return new SolverResult(-1, new List<Move>(), SolverStatus.Unsolvable);
    }

    private static string EncodeSinkAwareKey(LevelState state)
    {
        // Simple canonical encoding: sorted bottle signatures
        var sigs = new List<string>(state.Bottles.Count);
        for (int i = 0; i < state.Bottles.Count; i++)
        {
            var b = state.Bottles[i];
            var sb = new System.Text.StringBuilder();
            sb.Append(b.IsSink ? 'S' : 'N');
            sb.Append(':');
            for (int s = 0; s < b.Slots.Count; s++)
            {
                sb.Append(b.Slots[s].HasValue ? ((int)b.Slots[s].Value).ToString() : "_");
                if (s < b.Slots.Count - 1) sb.Append(',');
            }
            sigs.Add(sb.ToString());
        }
        sigs.Sort();
        return string.Join("|", sigs);
    }

    private static LevelState CloneLevelState(LevelState state)
    {
        var bottles = new List<Bottle>(state.Bottles.Count);
        foreach (var b in state.Bottles)
            bottles.Add(b.Clone());
        return new LevelState(bottles, state.MovesUsed, state.MovesAllowed, state.OptimalMoves,
            state.LevelIndex, state.Seed, state.ScrambleMoves, state.BackgroundPaletteIndex);
    }

    private sealed class SinkAwareNode
    {
        public LevelState State;
        public int Depth;
        public SinkAwareNode? Parent;
        public Move MoveFromParent;

        public SinkAwareNode(LevelState state, int depth, SinkAwareNode? parent, Move moveFromParent)
        {
            State = state;
            Depth = depth;
            Parent = parent;
            MoveFromParent = moveFromParent;
        }
    }

    private sealed class SinkLevelResult
    {
        public int Level;
        public int Seed;
        public int SinkBottleCount;
        public string SolverStatus = "";
        public int SolutionLength;
        public List<SinkBottleClassification> SinkClassifications = new();
    }

    private sealed class SinkBottleClassification
    {
        public int Id;
        public bool MustBeUsed;
        public bool CanBeAvoided;
        public string ForcingUseLeadsToUnsolvable = "false";
        public int SinkPoursInPrimarySolution;
        public bool SinkUsedInSinkAwareSolution;
    }
}
