using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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

                Assert(CommandScore.Score("Hello", "Hello") > CommandScore.Score("Hello", "hello"));

                // should match exact strings exactly
                Assert(CommandScore.Score("hello", "hello").Equals(1), "should match exact strings exactly");

                // should prefer case-sensitive matches
                Assert(CommandScore.Score("Hello", "Hello") > CommandScore.Score("Hello", "hello"), "should prefer case-sensitive matches");

                // should mark down prefixes
                Assert(CommandScore.Score("hello", "hello") > CommandScore.Score("hello", "he"), "should mark down prefixes");

                // should score all prefixes the same
                Assert(Math.Abs(CommandScore.Score("help", "he") - CommandScore.Score("hello", "he")) < TOLERANCE, "should score all prefixes the same");


                // should mark down word jumps
                Assert(CommandScore.Score("hello world", "hello") > CommandScore.Score("hello world", "hewo"), "should mark down word jumps");


                // should score similar word jumps the same
                Assert(Math.Abs(CommandScore.Score("hello world", "hewo") - CommandScore.Score("hey world", "hewo")) < TOLERANCE, "should score similar word jumps the same");


                // should penalize long word jumps
                Assert(CommandScore.Score("hello world", "hewo") > CommandScore.Score("hello kind world", "hewo"), "should penalize long word jumps");


                // should match missing characters
                Assert(CommandScore.Score("hello", "hl") > 0, "should match missing characters");


                // should penalize more for more missing characters
                Assert(CommandScore.Score("hello", "hllo") > CommandScore.Score("hello", "hlo"), "should penalize more for more missing characters");


                // should penalize more for missing characters than case
                Assert(CommandScore.Score("go to Inbox", "in") > CommandScore.Score("go to Unversity/Societies/CUE/info@cue.org.uk", "in"), "should penalize more for missing characters than case");


                // should match transpotisions
                Assert(CommandScore.Score("hello", "hle") > 0, "should match transpotisions");


                // should not match with a trailing letter
                Assert(Math.Abs(CommandScore.Score("ss", "sss") - 0.1) < TOLERANCE, "should not match with a trailing letter");


                // should match long jumps
                Assert(CommandScore.Score("go to @QuickFix", "fix") > 0, "should match long jumps");
                Assert(CommandScore.Score("go to Quick Fix", "fix") > CommandScore.Score("go to @QuickFix", "fix"), "should match long jumps");


                // should work well with the presence of an m-dash
                Assert(CommandScore.Score("no go — Windows", "windows") > 0, "should work well with the presence of an m-dash");


                // should be robust to duplicated letters
                Assert(Math.Abs(CommandScore.Score("talent", "tall") - 0.099) < TOLERANCE, "should be robust to duplicated letters");


                // should not allow letter insertion
                Assert(CommandScore.Score("talent", "tadlent") == 0, "should not allow letter insertion");


                // should match - with " " characters
                Assert(Math.Abs(CommandScore.Score("Auto-Advance", "Auto Advance") - 0.9999) < TOLERANCE, "should match - with \" \" characters");


                // should score long strings quickly
                Assert(Math.Abs(CommandScore.Score("go to this is a really long label that is really longthis is a really long label that is really longthis is a really long label that is really longthis is a really long label that is really long", "this is a") - 0.891) < TOLERANCE, "should score long strings quickly");
            }
        }
    }
}