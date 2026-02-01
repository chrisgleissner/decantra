using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Decantra.Domain.Generation;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: run [analyze <level> | bulk <count>]");
            return;
        }

        string mode = args[0];
        if (mode == "analyze")
        {
            int level = args.Length > 1 ? int.Parse(args[1]) : 11;
            AnalyzeLevel(level);
        }
        else if (mode == "bulk")
        {
            int count = args.Length > 1 ? int.Parse(args[1]) : 1000;
            BulkGenerate(count);
        }
        else
        {
            Console.WriteLine("Unknown mode.");
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


    private static void BulkGenerate(int count)
    {
        Console.WriteLine($"Bulk generating solutions for levels 1 to {count} using {Environment.ProcessorCount} cores...");

        // Pre-calculate seeds sequentially
        var seeds = new int[count + 1];
        int currentSeed = 0;
        for (int l = 1; l <= count; l++)
        {
            currentSeed = NextSeed(l, currentSeed);
            seeds[l] = currentSeed;
        }

        var results = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();

        Parallel.For(1, count + 1, (l) =>
        {
            // Each thread gets its own solver/generator instance to ensure safety/isolation
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver) { Log = null };
            int seed = seeds[l];

            try
            {
                var profile = LevelDifficultyEngine.GetProfile(l);
                var state = generator.Generate(seed, profile);
                var result = solver.SolveWithPath(state);

                if (result.OptimalMoves < 0)
                {
                    results[l] = $"Level {l}: UNSOLVABLE";
                    Console.WriteLine($"Level {l}: UNSOLVABLE");
                }
                else
                {
                    var moves = string.Join(",", result.Path.Select(m => $"{m.Source + 1}{m.Target + 1}"));
                    results[l] = $"Level {l}: {moves}";
                }
            }
            catch (Exception ex)
            {
                results[l] = $"Level {l}: ERROR {ex.Message}";
                Console.WriteLine($"Level {l}: ERROR {ex.Message}");
            }

            if (l % 100 == 0) Console.WriteLine($"Dispatched/Processed {l} levels...");
        });

        var lines = new List<string>();
        lines.Add("INTERNAL / DEBUG ONLY");
        for (int l = 1; l <= count; l++)
        {
            if (results.TryGetValue(l, out var line))
            {
                lines.Add(line);
            }
            else
            {
                lines.Add($"Level {l}: SKIPPED/MISSING");
            }
        }

        File.WriteAllLines("solver-solutions-debug.txt", lines);
        Console.WriteLine("Refreshed solver-solutions-debug.txt");
    }

}
