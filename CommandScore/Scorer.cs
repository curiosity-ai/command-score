using Microsoft.Extensions.ObjectPool;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace CommandScore
{
    public static class Scorer
    {
        // The scores are arranged so that a continuous match of characters will
        // result in a total score of 1.
        //
        // The best case, this character is a match, and either this is the start
        // of the string, or the previous character was also a match.
        private const float SCORE_CONTINUE_MATCH = 1.0f;

        // A new match at the start of a word scores better than a new match
        // elsewhere as it's more likely that the user will type the starts
        // of fragments.
        // NOTE: We score word jumps between spaces slightly higher than slashes, brackets
        // hyphens, etc.
        private const float SCORE_SPACE_WORD_JUMP     = 0.9f;
        private const float SCORE_NON_SPACE_WORD_JUMP = 0.8f;

        // Any other match isn't ideal, but we include it for completeness.
        private const float SCORE_CHARACTER_JUMP = 0.3f;

        // If the user transposed two letters, it should be signficantly penalized.
        //
        // i.e. "ouch" is more likely than "curtain" when "uc" is typed.
        private const float SCORE_TRANSPOSITION = 0.1f;

        // The goodness of a match should decay slightly with each missing
        // character.
        //
        // i.e. "bad" is more likely than "bard" when "bd" is typed.
        //
        // This will not change the order of suggestions based on SCORE_* until
        // 100 characters are inserted between matches.
        private const float PENALTY_SKIPPED = 0.999f;

        // The goodness of an exact-case match should be higher than a
        // case-insensitive match by a small amount.
        //
        // i.e. "HTML" is more likely than "haml" when "HM" is typed.
        //
        // This will not change the order of suggestions based on SCORE_* until
        // 1000 characters are inserted between matches.
        private const float PENALTY_CASE_MISMATCH = 0.9999f;

        // Match higher for letters closer to the beginning of the word
        private const float PENALTY_DISTANCE_FROM_START = 0.9f;

        // If the word has more characters than the user typed, it should
        // be penalised slightly.
        //
        // i.e. "html" is more likely than "html5" if I type "html".
        //
        // However, it may well be the case that there's a sensible secondary
        // ordering (like alphabetical) that it makes sense to rely on when
        // there are many prefix matches, so we don't make the penalty increase
        // with the number of tokens.
        private const float PENALTY_NOT_COMPLETE = 0.99f;


        private static readonly HashSet<char> IsGap = new HashSet<char>(new[] { '\\', '/', '_', '+', '.', '#', '"', '@', '[', '(', '{', '&' });

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountSpaces(ReadOnlySpan<char> text)
        {
            int count = 0;
            foreach (var s in text)
            {
                if (char.IsWhiteSpace(s)) count++;
            }
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountGaps(ReadOnlySpan<char> text)
        {
            int count = 0;
            foreach (var s in text)
            {
                if (IsGap.Contains(s)) count++;
            }
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long MakeKey(int left, int right)
        {
            return (long)left << 32 | (long)(uint)right;
        }

        private static float CommandScoreInner(ReadOnlySpan<char> item, ReadOnlySpan<char> candidate, ReadOnlySpan<char> lowerItem, ReadOnlySpan<char> lowerCandidate, int itemIndex, int candidateIndex, Dictionary<long, float> memoizedResults)
        {
            if (candidateIndex == candidate.Length)
            {
                if (itemIndex == item.Length)
                {
                    return SCORE_CONTINUE_MATCH;

                }
                return PENALTY_NOT_COMPLETE;
            }

            var memoizeKey = MakeKey(itemIndex, candidateIndex);

            if (memoizedResults.ContainsKey(memoizeKey))
            {
                return memoizedResults[memoizeKey];
            }

            var candidateChar = lowerCandidate[candidateIndex];

            int startAt = itemIndex;
            int index = lowerItem.Slice(startAt).IndexOf(candidateChar);
            if (index >= 0) index += startAt;

            float highScore = 0f;

            float score;
            float transposedScore;

            while (index >= 0)
            {
                score = CommandScoreInner(item, candidate, lowerItem, lowerCandidate, index + 1, candidateIndex + 1, memoizedResults);

                if (score > highScore)
                {
                    if (index == itemIndex)
                    {
                        score *= SCORE_CONTINUE_MATCH;
                    }
                    else if (IsGap.Contains(item[index - 1]))
                    {
                        score *= SCORE_NON_SPACE_WORD_JUMP;
                        var countGaps = CountGaps(item.Slice(itemIndex, index - 1 - itemIndex));
                        if (countGaps > 0 && itemIndex > 0)
                        {
                            score *= MathF.Pow(PENALTY_SKIPPED, countGaps);
                        }
                    }
                    else if (char.IsWhiteSpace(item[index - 1]))
                    {
                        score *= SCORE_SPACE_WORD_JUMP;
                        var spaceBreaks = CountSpaces(item.Slice(itemIndex, index - 1 - itemIndex));
                        if (spaceBreaks>0 && itemIndex > 0)
                        {
                            score *= MathF.Pow(PENALTY_SKIPPED, spaceBreaks);
                        }
                    }
                    else
                    {
                        score *= SCORE_CHARACTER_JUMP;
                        if (itemIndex > 0)
                        {
                            score *= MathF.Pow(PENALTY_SKIPPED, index - itemIndex);
                        }
                    }

                    if (item[index] != candidate[candidateIndex])
                    {
                        score *= PENALTY_CASE_MISMATCH;
                    }
                }

                if (score < SCORE_TRANSPOSITION
                 && (CharMatch(lowerItem, index - 1, lowerCandidate, candidateIndex + 1)
                  || lowerCandidate[candidateIndex + 1] == lowerCandidate[candidateIndex] // allow duplicate letters. Ref #7428
                  && CharMismatch(lowerItem, index - 1, lowerCandidate, candidateIndex)))
                {

                    transposedScore = CommandScoreInner(item, candidate, lowerItem, lowerCandidate, index + 1, candidateIndex + 2, memoizedResults);

                    if (transposedScore * SCORE_TRANSPOSITION > score)
                    {
                        score = transposedScore * SCORE_TRANSPOSITION;
                    }
                }

                if (score > highScore)
                {
                    highScore = score;
                }

                startAt = index + 1;
                index = lowerItem.Slice(startAt).IndexOf(candidateChar);
                if (index >= 0) index += startAt;
            }

            memoizedResults[memoizeKey] = highScore;

            return highScore;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CharMatch(ReadOnlySpan<char> left, int indexLeft, ReadOnlySpan<char> right, int indexRight)
        {
            if (indexLeft < 0 || indexLeft > left.Length) return false;
            if (indexRight < 0 || indexRight > right.Length) return false;
            return left[indexLeft] == right[indexRight];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CharMismatch(ReadOnlySpan<char> left, int indexLeft, ReadOnlySpan<char> right, int indexRight)
        {
            if (indexLeft < 0 || indexLeft > left.Length) return false;
            if (indexRight < 0 || indexRight > right.Length) return false;
            return left[indexLeft] != right[indexRight];
        }

        private static ReadOnlySpan<char> FormatInput(ReadOnlySpan<char> item, Span<char> formatedItem)
        {
            // convert all valid space characters to space so they match each other
            var culture = CultureInfo.CurrentUICulture;
            
            int index = 0;

            foreach(var c in item)
            {
                if (char.IsWhiteSpace(c) || c == '-')
                {
                    formatedItem[index] = ' ';
                    index++;
                }
                else
                {
                    var newC = char.ToLower(c, culture);
                    formatedItem[index] = newC;
                    index++;
                }
            }
            return formatedItem.Slice(0, index);
        }

        
        private static ObjectPool<Dictionary<long, float>> _pool = ObjectPool.Create(new DefaultPooledObjectPolicy<Dictionary<long, float>>());
        public static float For(ReadOnlySpan<char> item, ReadOnlySpan<char> candidate)
        {
            char[] pooledItem            = (item.Length      > 512) ? ArrayPool<char>.Shared.Rent(item.Length)      : null;
            char[] pooledCandidate       = (candidate.Length > 512) ? ArrayPool<char>.Shared.Rent(candidate.Length) : null;
            
            Span<char> formatedItem      = pooledItem      is not null ? pooledItem.AsSpan(0, item.Length)   : stackalloc char[item.Length];
            Span<char> formatedCandidate = pooledCandidate is not null ? pooledCandidate.AsSpan(0, candidate.Length) : stackalloc char[candidate.Length];

            var dict = _pool.Get();
            var score = CommandScoreInner(item, candidate, FormatInput(item, formatedItem), FormatInput(candidate, formatedCandidate), 0, 0, dict);
            dict.Clear();
            _pool.Return(dict);

            if (pooledItem      is not null) ArrayPool<char>.Shared.Return(pooledItem);
            if (pooledCandidate is not null) ArrayPool<char>.Shared.Return(pooledCandidate);

            return score;
        }
    }
}