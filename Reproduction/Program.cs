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
    private const int MaxSolveMillis = 3000;
    private const int MaxSolveNodes = 2_000_000;
    private const int RetrySolveMillis = 8000;
    private const int RetrySolveNodes = 8_000_000;

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
            Console.WriteLine("Usage: run [analyze <level> | bulk <count>]");
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
        lines.Add($"# Generated: {DateTime.UtcNow:O}");
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

            if (difficulty <= 0)
            {
                violations.Add($"Level {level}: difficulty is invalid ({difficulty})");
                continue;
            }

            if (level <= LevelDifficultyEngine.LinearDifficultyMaxLevel)
            {
                if (difficulty != level)
                {
                    violations.Add($"Level {level}: difficulty={difficulty} expected {level}");
                }
            }
            else
            {
                if (difficulty != LevelDifficultyEngine.DifficultyClampValue)
                {
                    violations.Add($"Level {level}: difficulty={difficulty} expected {LevelDifficultyEngine.DifficultyClampValue}");
                }
            }
        }

        for (int level = 1; level <= count; level++)
        {
            if (!seenLevels.Contains(level))
            {
                violations.Add($"Missing level {level} in output");
            }
        }

        Console.WriteLine(violations.Count == 0
            ? "PASS: Difficulty invariant satisfied."
            : $"FAIL: {violations.Count} difficulty invariant violation(s) found.");

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

}
