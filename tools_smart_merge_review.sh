#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <branch-a> <branch-b>" >&2
  exit 1
fi

A="$1"
B="$2"

if ! git rev-parse --verify "$A" >/dev/null 2>&1; then
  echo "Unknown ref: $A" >&2
  exit 1
fi

if ! git rev-parse --verify "$B" >/dev/null 2>&1; then
  echo "Unknown ref: $B" >&2
  exit 1
fi

BASE="$(git merge-base "$A" "$B")"

echo "=== Smart merge review ==="
echo "Branch A: $A"
echo "Branch B: $B"
echo "Merge base: $BASE"
echo

echo "--- Commits only in $A ---"
git log --oneline "$BASE..$A" || true
echo

echo "--- Commits only in $B ---"
git log --oneline "$BASE..$B" || true
echo

echo "--- Files changed in both branches (highest conflict risk) ---"
comm -12 \
  <(git diff --name-only "$BASE..$A" | sort) \
  <(git diff --name-only "$BASE..$B" | sort) || true
echo

echo "--- Files changed only in $A ---"
comm -23 \
  <(git diff --name-only "$BASE..$A" | sort) \
  <(git diff --name-only "$BASE..$B" | sort) || true
echo

echo "--- Files changed only in $B ---"
comm -13 \
  <(git diff --name-only "$BASE..$A" | sort) \
  <(git diff --name-only "$BASE..$B" | sort) || true
echo

echo "Next step:"
echo "  git checkout $A"
echo "  git merge --no-ff $B"
echo "Then resolve files listed in 'changed in both branches' first."
