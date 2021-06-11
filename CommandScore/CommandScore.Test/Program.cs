﻿using System;
using System.Diagnostics;

namespace CommandScore.Test
{
    class Program
    {
        static void Main(string[] args)
        {

            Debug.Assert(CommandScore.Score("Hello", "Hello") > CommandScore.Score("Hello", "hello"));

            // should match exact strings exactly
            Debug.Assert(CommandScore.Score("hello", "hello").Equals(1));

            // should prefer case-sensitive matches
            Debug.Assert(CommandScore.Score("Hello", "Hello") > CommandScore.Score("Hello", "hello"));

            // should mark down prefixes
            Debug.Assert(CommandScore.Score("hello", "hello") > CommandScore.Score("hello", "he"));

            // should score all prefixes the same
            Debug.Assert(CommandScore.Score("help", "he") == CommandScore.Score("hello", "he"));


            // should mark down word jumps
            Debug.Assert(CommandScore.Score("hello world", "hello") > CommandScore.Score("hello world", "hewo"));


            // should score similar word jumps the same
            Debug.Assert(CommandScore.Score("hello world", "hewo") == CommandScore.Score("hey world", "hewo"));


            // should penalize long word jumps
            Debug.Assert(CommandScore.Score("hello world", "hewo") > CommandScore.Score("hello kind world", "hewo"));


            // should match missing characters
            Debug.Assert(CommandScore.Score("hello", "hl") > 0);


            // should penalize more for more missing characters
            Debug.Assert(CommandScore.Score("hello", "hllo") > CommandScore.Score("hello", "hlo"));


            // should penalize more for missing characters than case
            Debug.Assert(CommandScore.Score("go to Inbox", "in") > CommandScore.Score("go to Unversity/Societies/CUE/info@cue.org.uk", "in"));


            // should match transpotisions
            Debug.Assert(CommandScore.Score("hello", "hle") > 0);


            // should not match with a trailing letter
            Debug.Assert(CommandScore.Score("ss", "sss") == 0.1);


            // should match long jumps
            Debug.Assert(CommandScore.Score("go to @QuickFix", "fix") > 0);
            Debug.Assert(CommandScore.Score("go to Quick Fix", "fix") > CommandScore.Score("go to @QuickFix", "fix"));


            // should work well with the presence of an m-dash
            Debug.Assert(CommandScore.Score("no go — Windows", "windows") > 0);


            // should be robust to duplicated letters
            Debug.Assert(CommandScore.Score("talent", "tall") == 0.099);


            // should not allow letter insertion
            Debug.Assert(CommandScore.Score("talent", "tadlent") == 0);


            // should match - with " " characters
            Debug.Assert(CommandScore.Score("Auto-Advance", "Auto Advance") == 0.9999);


            // should score long strings quickly
            Debug.Assert(CommandScore.Score("go to this is a really long label that is really longthis is a really long label that is really longthis is a really long label that is really longthis is a really long label that is really long", "this is a") == 0.891);

        }
    }
}