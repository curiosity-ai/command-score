using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommandScore;

namespace CommandScore.Test
{
    class Program
    {
        private static bool anyFailed = false;
        private static void Assert(bool condition, string message = null)
        {
#if DEBUG
            Debug.Assert(condition, message);
#endif
            anyFailed |= !condition;
        }

        static void Main(string[] args)
        {
            int N = 1000000;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++)
            {
                if (i % 1000 == 0)
                {
                    Console.WriteLine($"At {i:n0} of {N:n0} \t {30*1000 / sw.Elapsed.TotalSeconds:n0} calls/s");
                    sw.Restart();
                }

                const float TOLERANCE = 0.00001f;

                Assert(Scorer.For("Hello", "Hello") > Scorer.For("Hello", "hello"));

                // should match exact strings exactly
                Assert(Scorer.For("hello", "hello").Equals(1), "should match exact strings exactly");

                // should prefer case-sensitive matches
                Assert(Scorer.For("Hello", "Hello") > Scorer.For("Hello", "hello"), "should prefer case-sensitive matches");

                // should mark down prefixes
                Assert(Scorer.For("hello", "hello") > Scorer.For("hello", "he"), "should mark down prefixes");

                // should score all prefixes the same
                Assert(Math.Abs(Scorer.For("help", "he") - Scorer.For("hello", "he")) < TOLERANCE, "should score all prefixes the same");


                // should mark down word jumps
                Assert(Scorer.For("hello world", "hello") > Scorer.For("hello world", "hewo"), "should mark down word jumps");


                // should score similar word jumps the same
                Assert(Math.Abs(Scorer.For("hello world", "hewo") - Scorer.For("hey world", "hewo")) < TOLERANCE, "should score similar word jumps the same");


                // should penalize long word jumps
                Assert(Scorer.For("hello world", "hewo") > Scorer.For("hello kind world", "hewo"), "should penalize long word jumps");


                // should match missing characters
                Assert(Scorer.For("hello", "hl") > 0, "should match missing characters");


                // should penalize more for more missing characters
                Assert(Scorer.For("hello", "hllo") > Scorer.For("hello", "hlo"), "should penalize more for more missing characters");


                // should penalize more for missing characters than case
                Assert(Scorer.For("go to Inbox", "in") > Scorer.For("go to Unversity/Societies/CUE/info@cue.org.uk", "in"), "should penalize more for missing characters than case");


                // should match transpotisions
                Assert(Scorer.For("hello", "hle") > 0, "should match transpotisions");


                // should not match with a trailing letter
                Assert(Math.Abs(Scorer.For("ss", "sss") - 0.1) < TOLERANCE, "should not match with a trailing letter");


                // should match long jumps
                Assert(Scorer.For("go to @QuickFix", "fix") > 0, "should match long jumps");
                Assert(Scorer.For("go to Quick Fix", "fix") > Scorer.For("go to @QuickFix", "fix"), "should match long jumps");


                // should work well with the presence of an m-dash
                Assert(Scorer.For("no go — Windows", "windows") > 0, "should work well with the presence of an m-dash");


                // should be robust to duplicated letters
                Assert(Math.Abs(Scorer.For("talent", "tall") - 0.099) < TOLERANCE, "should be robust to duplicated letters");


                // should not allow letter insertion
                Assert(Scorer.For("talent", "tadlent") == 0, "should not allow letter insertion");


                // should match - with " " characters
                Assert(Math.Abs(Scorer.For("Auto-Advance", "Auto Advance") - 0.9999) < TOLERANCE, "should match - with \" \" characters");


                // should score long strings quickly
                Assert(Math.Abs(Scorer.For("go to this is a really long label that is really longthis is a really long label that is really longthis is a really long label that is really longthis is a really long label that is really long", "this is a") - 0.891) < TOLERANCE, "should score long strings quickly");
            }
        }
    }
}