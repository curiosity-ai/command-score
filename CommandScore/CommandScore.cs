using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CommandScore
{
    public static class CommandScore
    {
        // The scores are arranged so that a continuous match of characters will
        // result in a total score of 1.
        //
        // The best case, this character is a match, and either this is the start
        // of the string, or the previous character was also a match.
        private const double SCORE_CONTINUE_MATCH = 1.0f;

        // A new match at the start of a word scores better than a new match
        // elsewhere as it's more likely that the user will type the starts
        // of fragments.
        // NOTE: We score word jumps between spaces slightly higher than slashes, brackets
        // hyphens, etc.
        private const double SCORE_SPACE_WORD_JUMP     = 0.9f;
        private const double SCORE_NON_SPACE_WORD_JUMP = 0.8f;

        // Any other match isn't ideal, but we include it for completeness.
        private const double SCORE_CHARACTER_JUMP = 0.3f;

        // If the user transposed two letters, it should be signficantly penalized.
        //
        // i.e. "ouch" is more likely than "curtain" when "uc" is typed.
        private const double SCORE_TRANSPOSITION = 0.1f;

        // The goodness of a match should decay slightly with each missing
        // character.
        //
        // i.e. "bad" is more likely than "bard" when "bd" is typed.
        //
        // This will not change the order of suggestions based on SCORE_* until
        // 100 characters are inserted between matches.
        private const double PENALTY_SKIPPED = 0.999f;

        // The goodness of an exact-case match should be higher than a
        // case-insensitive match by a small amount.
        //
        // i.e. "HTML" is more likely than "haml" when "HM" is typed.
        //
        // This will not change the order of suggestions based on SCORE_* until
        // 1000 characters are inserted between matches.
        private const double PENALTY_CASE_MISMATCH = 0.9999f;

        // Match higher for letters closer to the beginning of the word
        private const double PENALTY_DISTANCE_FROM_START = 0.9f;

        // If the word has more characters than the user typed, it should
        // be penalised slightly.
        //
        // i.e. "html" is more likely than "html5" if I type "html".
        //
        // However, it may well be the case that there's a sensible secondary
        // ordering (like alphabetical) that it makes sense to rely on when
        // there are many prefix matches, so we don't make the penalty increase
        // with the number of tokens.
        private const double PENALTY_NOT_COMPLETE = 0.99f;

        private static readonly Regex IS_GAP_REGEXP      = new Regex("/[\\\\/_+.#\"@\\[\\(\\{&]/");
        private static readonly Regex COUNT_GAPS_REGEXP  = new Regex("/[\\\\/_+.#\"@\\[\\(\\{&]/g");
        private static readonly Regex IS_SPACE_REGEXP    = new Regex("/[\\s-]/");
        private static readonly Regex COUNT_SPACE_REGEXP = new Regex("/[\\s-]/g");

        private static double CommandScoreInner(string item, string abbreviation, string lowerString, string lowerAbbreviation, int stringIndex, int abbreviationIndex, Dictionary<(int, int), double> memoizedResults)
        {

            if (abbreviationIndex == abbreviation.Length)
            {
                if (stringIndex == item.Length)
                {
                    return SCORE_CONTINUE_MATCH;

                }
                return PENALTY_NOT_COMPLETE;
            }

            var memoizeKey = (stringIndex, abbreviationIndex);
            if (memoizedResults.ContainsKey(memoizeKey))
            {
                return memoizedResults[memoizeKey];
            }

            var abbreviationChar = lowerAbbreviation[abbreviationIndex];
            var index = lowerString.IndexOf(abbreviationChar, stringIndex);
            double highScore = 0f;

            double score;
            double transposedScore;
            Match wordBreaks;
            Match spaceBreaks;

            while (index >= 0)
            {

                score = CommandScoreInner(item, abbreviation, lowerString, lowerAbbreviation, index + 1, abbreviationIndex + 1, memoizedResults);
                if (score > highScore)
                {
                    if (index == stringIndex)
                    {
                        score *= SCORE_CONTINUE_MATCH;
                    }
                    else if (IS_GAP_REGEXP.IsMatch(item[index - 1].ToString()))
                    {
                        score *= SCORE_NON_SPACE_WORD_JUMP;
                        wordBreaks = COUNT_GAPS_REGEXP.Match(item.Substring(stringIndex, index - 1));
                        if (wordBreaks.Success && stringIndex > 0)
                        {
                            score *= Math.Pow(PENALTY_SKIPPED, wordBreaks.Length);
                        }
                    }
                    else if (IS_SPACE_REGEXP.IsMatch(item[index - 1].ToString()))
                    {
                        score *= SCORE_SPACE_WORD_JUMP;
                        spaceBreaks = COUNT_SPACE_REGEXP.Match(item.Substring(stringIndex, index - 1));
                        if (spaceBreaks.Success && stringIndex > 0)
                        {
                            score *= Math.Pow(PENALTY_SKIPPED, spaceBreaks.Length);
                        }
                    }
                    else
                    {
                        score *= SCORE_CHARACTER_JUMP;
                        if (stringIndex > 0)
                        {
                            score *= Math.Pow(PENALTY_SKIPPED, index - stringIndex);
                        }
                    }

                    if (item[index] != abbreviation[abbreviationIndex])
                    {
                        score *= PENALTY_CASE_MISMATCH;
                    }

                }

                if (score < SCORE_TRANSPOSITION
                 && (lowerString.CharAt(index - 1) == lowerAbbreviation.CharAt(abbreviationIndex + 1)
                  || lowerAbbreviation[abbreviationIndex + 1] == lowerAbbreviation[abbreviationIndex] // allow duplicate letters. Ref #7428
                  && lowerString.CharAt(index - 1) != lowerAbbreviation.CharAt(abbreviationIndex)))
                {

                    transposedScore = CommandScoreInner(item, abbreviation, lowerString, lowerAbbreviation, index + 1, abbreviationIndex + 2, memoizedResults);

                    if (transposedScore * SCORE_TRANSPOSITION > score)
                    {
                        score = transposedScore * SCORE_TRANSPOSITION;
                    }
                }

                if (score > highScore)
                {
                    highScore = score;
                }

                index = lowerString.IndexOf(abbreviationChar, index + 1);
            }

            memoizedResults[memoizeKey] = highScore;
            return highScore;
        }

        private static string CharAt(this string term, int index)
        {
            if (term.Length >= index || index < 0) return "";
            return term[index].ToString();
        }

        private static string FormatInput(string item)
        {
            // convert all valid space characters to space so they match each other
            return COUNT_SPACE_REGEXP.Replace(item.ToLower(), " ");
        }

        public static double Score(string item, string abbreviation)
        {
            /* NOTE:
             * in the original, we used to do the lower-casing on each recursive call, but this meant that toLowerCase()
             * was the dominating cost in the algorithm, passing both is a little ugly, but considerably faster.
             */
            return CommandScoreInner(item, abbreviation, FormatInput(item), FormatInput(abbreviation), 0, 0, new Dictionary<(int, int), double>());
        }
    }
}