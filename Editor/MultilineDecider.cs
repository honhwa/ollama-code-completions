namespace OllamaCodeCompletions
{
    internal enum MultilineDecision { Single, Multi }
    internal enum DecisionSource { ForcedSingle, ForcedMulti, AutoSingle, AutoMulti }

    internal static class MultilineDecider
    {
        public static (MultilineDecision Decision, DecisionSource Source) Decide(
            MultilineMode mode,
            string lineBeforeCursor,
            string contentTypeName)
        {
            if (mode == MultilineMode.Always)
                return (MultilineDecision.Multi, DecisionSource.ForcedMulti);
            if (mode == MultilineMode.Never)
                return (MultilineDecision.Single, DecisionSource.ForcedSingle);

            // Auto from here on.
            string ct = contentTypeName ?? "";
            string trimmed = (lineBeforeCursor ?? "").TrimEnd();
            string trimmedStart = (lineBeforeCursor ?? "").TrimStart();

            // Rule 4: line comment → single-line (only LINE comments, not block openers).
            if (IsLineComment(trimmedStart, ct))
                return (MultilineDecision.Single, DecisionSource.AutoSingle);

            // Rule 4b: block comment opener (/* or /**) → multi-line.
            // Block comments legitimately span lines, so we allow multi-line here.
            if (trimmedStart.StartsWith("/*"))
                return (MultilineDecision.Multi, DecisionSource.AutoMulti);

            // Rule 5: inside a JSON string value (odd count of unescaped double-quotes).
            if (IsJsonFamily(ct) && IsInsideJsonString(lineBeforeCursor ?? ""))
                return (MultilineDecision.Single, DecisionSource.AutoSingle);

            // Rule 7: block opener → multi-line.
            if (EndsWithBlockOpener(trimmed, ct))
                return (MultilineDecision.Multi, DecisionSource.AutoMulti);

            // Rule 8: empty or whitespace-only line → multi-line.
            if (string.IsNullOrWhiteSpace(lineBeforeCursor))
                return (MultilineDecision.Multi, DecisionSource.AutoMulti);

            // Rule 9: default.
            return (MultilineDecision.Single, DecisionSource.AutoSingle);
        }

        private static bool IsLineComment(string trimmedStart, string ct)
        {
            // C-family: //
            if (IsCFamily(ct) && trimmedStart.StartsWith("//"))
                return true;
            // Python / shell: #
            if (IsPython(ct) && trimmedStart.StartsWith("#"))
                return true;
            // SQL / Lua / Haskell: --
            if (IsSqlOrLuaFamily(ct) && trimmedStart.StartsWith("--"))
                return true;
            // Lisps: ;
            if (IsLisp(ct) && trimmedStart.StartsWith(";"))
                return true;
            // LaTeX / Matlab: %
            if (IsLatexOrMatlab(ct) && trimmedStart.StartsWith("%"))
                return true;
            return false;
        }

        private static bool EndsWithBlockOpener(string trimmed, string ct)
        {
            if (trimmed.EndsWith("{"))
                return true;
            if (trimmed.EndsWith("=>"))
                return true;
            // `:` only for Python
            if (trimmed.EndsWith(":") && IsPython(ct))
                return true;
            // whole-word `do`
            if (EndsWithWord(trimmed, "do"))
                return true;
            // whole-word `then`
            if (EndsWithWord(trimmed, "then"))
                return true;
            return false;
        }

        // Returns true if `line` ends with the exact word `word` at a word boundary.
        private static bool EndsWithWord(string line, string word)
        {
            if (!line.EndsWith(word))
                return false;
            int beforeWord = line.Length - word.Length;
            if (beforeWord == 0)
                return true;
            return char.IsWhiteSpace(line[beforeWord - 1]);
        }

        private static bool IsJsonFamily(string ct)
            => ct == "json" || ct == "jsonc" || ct.StartsWith("json");

        private static bool IsInsideJsonString(string line)
        {
            // Odd count of unescaped double-quotes → inside a string.
            int count = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '\\') { i++; continue; }
                if (line[i] == '"') count++;
            }
            return (count % 2) == 1;
        }

        private static bool IsCFamily(string ct)
        {
            return ct == "csharp" || ct == "cs"
                || ct == "cpp" || ct == "c" || ct == "c/c++"
                || ct == "java"
                || ct == "javascript" || ct == "js"
                || ct == "typescript" || ct == "ts"
                || ct == "go" || ct == "golang"
                || ct == "rust" || ct == "rs"
                || ct == "kotlin" || ct == "swift"
                || ct.StartsWith("csharp") || ct.StartsWith("cpp")
                || ct.StartsWith("typescript") || ct.StartsWith("javascript");
        }

        private static bool IsPython(string ct)
            => ct.StartsWith("python") || ct == "py";

        private static bool IsSqlOrLuaFamily(string ct)
            => ct == "sql" || ct == "lua" || ct == "haskell" || ct == "hs";

        private static bool IsLisp(string ct)
            => ct == "lisp" || ct == "scheme" || ct == "clojure"
            || ct == "clj" || ct == "racket";

        private static bool IsLatexOrMatlab(string ct)
            => ct == "latex" || ct == "tex" || ct == "matlab" || ct == "m";
    }
}
