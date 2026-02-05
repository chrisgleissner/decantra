#!/usr/bin/env python3
import sys
import re
from collections import Counter

LEVEL_LINE_RE = re.compile(
    r"""^
        \s*level\s*=\s*(\d+)
        .*?
        difficulty\s*=\s*(\d+)
    """,
    re.VERBOSE
)

def parse_levels(path: str):
    pairs = []
    with open(path, "r", encoding="utf-8") as f:
        for lineno, line in enumerate(f, start=1):
            if not line.lstrip().startswith("level"):
                continue
            m = LEVEL_LINE_RE.match(line)
            if not m:
                raise ValueError(
                    f"Line {lineno} starts with 'level' but does not match "
                    f"'level=<n> ... difficulty=<n>' format:\n{line.rstrip()}"
                )
            level = int(m.group(1))
            difficulty = int(m.group(2))
            pairs.append((level, difficulty))

    if not pairs:
        raise ValueError("No lines starting with 'level' were found.")
    return pairs

def main():
    if len(sys.argv) != 2:
        print(f"Usage: {sys.argv[0]} <path-to-file>", file=sys.stderr)
        sys.exit(1)

    path = sys.argv[1]
    pairs = parse_levels(path)

    # Deterministic ordering: (difficulty, level)
    pairs_sorted = sorted(pairs, key=lambda x: (x[1], x[0]))
    difficulties = [d for _, d in pairs_sorted]
    n = len(difficulties)

    if n < 3:
        raise ValueError(f"Need at least 3 entries to create 3 buckets (got {n}).")

    # Tertile cutoffs by rank (33% and 66%)
    t1_idx = n * 1 // 3 - 1
    t2_idx = n * 2 // 3 - 1
    t1 = difficulties[max(t1_idx, 0)]
    t2 = difficulties[max(t2_idx, 0)]

    print("=== Tertile cut-off values (rank-based) ===")
    print(f"T1 (33%): difficulty <= {t1}")
    print(f"T2 (66%): difficulty <= {t2}")
    print()

    # Range-based tiering (may be uneven due to ties)
    def bucket_by_range(d: int) -> int:
        if d <= t1:
            return 1
        if d <= t2:
            return 2
        return 3

    range_counts = Counter(bucket_by_range(d) for _, d in pairs)

    print("=== Range-based bucket sizes (may be unequal due to ties) ===")
    for b in (1, 2, 3):
        print(f"Bucket {b}: {range_counts.get(b, 0)}")
    print()

    # Rank-based exact buckets (as equal as possible)
    bucket_by_rank = {}
    for idx, (lvl, _) in enumerate(pairs_sorted):
        b = (idx * 3) // n + 1  # 1..3, partitions [0,n) into 3 contiguous bands
        bucket_by_rank[lvl] = b

    rank_counts = Counter(bucket_by_rank.values())

    print("=== Rank-based bucket sizes (always as equal as possible) ===")
    for b in (1, 2, 3):
        print(f"Bucket {b}: {rank_counts.get(b, 0)}")
    print()

    # Difficulty min/max per rank-based bucket
    print("=== Difficulty min/max per rank-based bucket ===")
    for b in (1, 2, 3):
        diffs = [diff for (lvl, diff) in pairs if bucket_by_rank.get(lvl) == b]
        print(f"Bucket {b}: {min(diffs)} .. {max(diffs)}")

if __name__ == "__main__":
    main()
