using System;
using System.Diagnostics;

namespace CommandScore.Test
{
    class Program
    {
        static void Main(string[] args)
        {

            const double TOLERANCE = 0.001;
            
            Debug.Assert(CommandScore.Score("Hello", "Hello") > CommandScore.Score("Hello", "hello"));

            // should match exact strings exactly
            Debug.Assert(CommandScore.Score("hello", "hello").Equals(1), "should match exact strings exactly");

            // should prefer case-sensitive matches
            Debug.Assert(CommandScore.Score("Hello", "Hello") > CommandScore.Score("Hello", "hello"), "should prefer case-sensitive matches");

            // should mark down prefixes
            Debug.Assert(CommandScore.Score("hello", "hello") > CommandScore.Score("hello", "he"), "should mark down prefixes");

            // should score all prefixes the same
            Debug.Assert(Math.Abs(CommandScore.Score("help", "he") - CommandScore.Score("hello", "he")) < TOLERANCE, "should score all prefixes the same");


            // should mark down word jumps
            Debug.Assert(CommandScore.Score("hello world", "hello") > CommandScore.Score("hello world", "hewo"), "should mark down word jumps");


            // should score similar word jumps the same
            Debug.Assert(Math.Abs(CommandScore.Score("hello world", "hewo") - CommandScore.Score("hey world", "hewo")) < TOLERANCE, "should score similar word jumps the same");


            // should penalize long word jumps
            Debug.Assert(CommandScore.Score("hello world", "hewo") > CommandScore.Score("hello kind world", "hewo"), "should penalize long word jumps");


            // should match missing characters
            Debug.Assert(CommandScore.Score("hello", "hl") > 0, "should match missing characters");


            // should penalize more for more missing characters
            Debug.Assert(CommandScore.Score("hello", "hllo") > CommandScore.Score("hello", "hlo"), "should penalize more for more missing characters");


            // should penalize more for missing characters than case
            Debug.Assert(CommandScore.Score("go to Inbox", "in") > CommandScore.Score("go to Unversity/Societies/CUE/info@cue.org.uk", "in"), "should penalize more for missing characters than case");


            // should match transpotisions
            Debug.Assert(CommandScore.Score("hello", "hle") > 0, "should match transpotisions");


            // should not match with a trailing letter
            Debug.Assert(Math.Abs(CommandScore.Score("ss", "sss") - 0.1) < TOLERANCE, "should not match with a trailing letter");


            // should match long jumps
            Debug.Assert(CommandScore.Score("go to @QuickFix", "fix") > 0,                                            "should match long jumps");
            Debug.Assert(CommandScore.Score("go to Quick Fix", "fix") > CommandScore.Score("go to @QuickFix", "fix"), "should match long jumps");


            // should work well with the presence of an m-dash
            Debug.Assert(CommandScore.Score("no go — Windows", "windows") > 0, "should work well with the presence of an m-dash");


            // should be robust to duplicated letters
            Debug.Assert(Math.Abs(CommandScore.Score("talent", "tall") - 0.099) < TOLERANCE, "should be robust to duplicated letters");


            // should not allow letter insertion
            Debug.Assert(CommandScore.Score("talent", "tadlent") == 0, "should not allow letter insertion");


            // should match - with " " characters
            Debug.Assert(Math.Abs(CommandScore.Score("Auto-Advance", "Auto Advance") - 0.9999) < TOLERANCE, "should match - with \" \" characters");


            // should score long strings quickly
            Debug.Assert(Math.Abs(CommandScore.Score("go to this is a really long label that is really longthis is a really long label that is really longthis is a really long label that is really longthis is a really long label that is really long", "this is a") - 0.891) < TOLERANCE, "should score long strings quickly");
        }
    }
}