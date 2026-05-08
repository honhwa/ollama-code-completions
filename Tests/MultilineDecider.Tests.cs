// Tests for MultilineDecider and CompletionPostProcessor.CapLines.
//
// To compile and run standalone (no test framework required):
//   set CSC="C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe"
//   %CSC% /define:TEST /langversion:9.0 /out:MultilineDeciderTests.exe Editor/MultilineDecider.cs Editor/CompletionPostProcessor.cs Tests/MultilineDecider.Tests.cs
//   MultilineDeciderTests.exe
//
// The VSIX build does not define TEST, so this file contributes no types.

#if TEST
using System;

namespace OllamaCodeCompletions
{
    internal static class MultilineDeciderTests
    {
        static int _pass, _fail;

        static int Main()
        {
            // ── MultilineDecider ──────────────────────────────────────────────────

            // Line comment → AutoSingle
            CheckDecide("// foo in csharp → AutoSingle",
                MultilineMode.Auto, "// foo", "csharp",
                MultilineDecision.Single, DecisionSource.AutoSingle);

            CheckDecide("# foo in python → AutoSingle",
                MultilineMode.Auto, "# foo", "python",
                MultilineDecision.Single, DecisionSource.AutoSingle);

            // `//` is not Python's comment marker (#), so rule 4 doesn't fire.
            // No block opener or blank line, so default (AutoSingle) applies.
            CheckDecide("// foo in python → AutoSingle (not a Python comment, hits default)",
                MultilineMode.Auto, "// foo", "python",
                MultilineDecision.Single, DecisionSource.AutoSingle);

            CheckDecide("/** doc here in csharp → AutoMulti (block comment, not line comment)",
                MultilineMode.Auto, "/** doc here", "csharp",
                MultilineDecision.Multi, DecisionSource.AutoMulti);

            CheckDecide("   // foo in csharp (leading spaces) → AutoSingle",
                MultilineMode.Auto, "   // foo", "csharp",
                MultilineDecision.Single, DecisionSource.AutoSingle);

            // Block openers → AutoMulti
            CheckDecide("function f() { in javascript → AutoMulti",
                MultilineMode.Auto, "function f() {", "javascript",
                MultilineDecision.Multi, DecisionSource.AutoMulti);

            CheckDecide("x => { in typescript → AutoMulti",
                MultilineMode.Auto, "x => {", "typescript",
                MultilineDecision.Multi, DecisionSource.AutoMulti);

            CheckDecide("def f(): in python → AutoMulti",
                MultilineMode.Auto, "def f():", "python",
                MultilineDecision.Multi, DecisionSource.AutoMulti);

            CheckDecide("def f(): in csharp → AutoSingle (: rule is Python-only)",
                MultilineMode.Auto, "def f():", "csharp",
                MultilineDecision.Single, DecisionSource.AutoSingle);

            CheckDecide("name: string in typescript → AutoSingle (default)",
                MultilineMode.Auto, "name: string", "typescript",
                MultilineDecision.Single, DecisionSource.AutoSingle);

            // Empty / whitespace → AutoMulti
            CheckDecide("empty string → AutoMulti",
                MultilineMode.Auto, "", "csharp",
                MultilineDecision.Multi, DecisionSource.AutoMulti);

            CheckDecide("whitespace-only → AutoMulti",
                MultilineMode.Auto, "   ", "csharp",
                MultilineDecision.Multi, DecisionSource.AutoMulti);

            // `do` word-boundary
            CheckDecide("do in ruby → AutoMulti",
                MultilineMode.Auto, "do", "ruby",
                MultilineDecision.Multi, DecisionSource.AutoMulti);

            CheckDecide("dosomething in ruby → AutoSingle (word boundary required)",
                MultilineMode.Auto, "dosomething", "ruby",
                MultilineDecision.Single, DecisionSource.AutoSingle);

            // Default case
            CheckDecide("const x = foo in javascript → AutoSingle",
                MultilineMode.Auto, "const x = foo", "javascript",
                MultilineDecision.Single, DecisionSource.AutoSingle);

            // Always / Never modes
            CheckDecide("Always + // foo in csharp → ForcedMulti",
                MultilineMode.Always, "// foo", "csharp",
                MultilineDecision.Multi, DecisionSource.ForcedMulti);

            CheckDecide("Never + function f() { in javascript → ForcedSingle",
                MultilineMode.Never, "function f() {", "javascript",
                MultilineDecision.Single, DecisionSource.ForcedSingle);

            // Arrow without brace
            CheckDecide("x => in typescript → AutoMulti",
                MultilineMode.Auto, "x =>", "typescript",
                MultilineDecision.Multi, DecisionSource.AutoMulti);

            // `then` keyword
            CheckDecide("if condition then → AutoMulti",
                MultilineMode.Auto, "if condition then", "lua",
                MultilineDecision.Multi, DecisionSource.AutoMulti);

            // ── CapLines ──────────────────────────────────────────────────────────

            CheckCap("unchanged when at limit", "a\nb\nc", 3, "a\nb\nc");
            CheckCap("truncated to 3", "a\nb\nc\nd\ne", 3, "a\nb\nc");
            CheckCap("truncated to 1", "a\nb\nc", 1, "a");
            CheckCap("single line with high cap", "a", 5, "a");
            CheckCap("empty string unchanged", "", 5, "");
            CheckCapNull("null input", null, 5);
            CheckCap("trailing newline trimmed naturally", "a\nb\n", 1, "a");
            CheckCap("CRLF normalised on split", "a\r\nb\r\nc\r\nd", 2, "a\nb");

            Console.WriteLine($"\n{_pass} passed, {_fail} failed.");
            return _fail == 0 ? 0 : 1;
        }

        static void CheckDecide(string name,
            MultilineMode mode, string lineBeforeCursor, string contentType,
            MultilineDecision expectedDecision, DecisionSource expectedSource)
        {
            var (decision, src) = MultilineDecider.Decide(mode, lineBeforeCursor, contentType);
            bool ok = decision == expectedDecision && src == expectedSource;
            if (ok)
            {
                Console.WriteLine($"  PASS  {name}");
                _pass++;
            }
            else
            {
                Console.WriteLine($"  FAIL  {name}");
                Console.WriteLine($"        expected: ({expectedDecision}, {expectedSource})");
                Console.WriteLine($"        actual:   ({decision}, {src})");
                _fail++;
            }
        }

        static void CheckCap(string name, string input, int maxLines, string expected)
        {
            string actual = CompletionPostProcessor.CapLines(input, maxLines);
            if (actual == expected)
            {
                Console.WriteLine($"  PASS  {name}");
                _pass++;
            }
            else
            {
                Console.WriteLine($"  FAIL  {name}");
                Console.WriteLine($"        expected: {Fmt(expected)}");
                Console.WriteLine($"        actual:   {Fmt(actual)}");
                _fail++;
            }
        }

        static void CheckCapNull(string name, string input, int maxLines)
        {
            string actual = CompletionPostProcessor.CapLines(input, maxLines);
            if (actual == null)
            {
                Console.WriteLine($"  PASS  {name}");
                _pass++;
            }
            else
            {
                Console.WriteLine($"  FAIL  {name}");
                Console.WriteLine($"        expected: <null>");
                Console.WriteLine($"        actual:   {Fmt(actual)}");
                _fail++;
            }
        }

        static string Fmt(string s) =>
            s == null ? "<null>" : $"\"{s.Replace("\r", "\\r").Replace("\n", "\\n")}\"";
    }
}
#endif
