using System.Text;
using Spelljammer.Localization;

namespace Spelljammer.Tools.Localization;

internal static class MessageParser
{
    public static LocalizationStatus Compile(
        string source,
        IReadOnlyList<LocalizationArgumentSchema> arguments,
        Func<string, string>? literalTransform,
        out CompiledMessageProgram? program,
        out string error)
    {
        try
        {
            Parser parser = new(source, arguments, literalTransform);
            IReadOnlyList<MessageNode> nodes = parser.Parse();
            if (parser.UsedArguments.Count != arguments.Count)
            {
                string unused = string.Join(", ", arguments
                    .Where(argument => !parser.UsedArguments.Contains(argument.Name))
                    .Select(argument => argument.Name));
                error = $"Declared arguments are not used: {unused}.";
                program = null;
                return LocalizationStatus.InvalidArgument;
            }

            LocalizationStatus status = MessageProgramCodec.Encode(nodes, arguments, out program, out error);
            return status == LocalizationStatus.DataCorrupt ? LocalizationStatus.InvalidArgument : status;
        }
        catch (MessageParseException exception)
        {
            program = null;
            error = exception.Message;
            return LocalizationStatus.InvalidArgument;
        }
    }

    private sealed class Parser
    {
        private readonly string source;
        private readonly IReadOnlyList<LocalizationArgumentSchema> arguments;
        private readonly Dictionary<string, int> argumentIndexes;
        private readonly Func<string, string>? literalTransform;
        private int position;

        public Parser(
            string source,
            IReadOnlyList<LocalizationArgumentSchema> arguments,
            Func<string, string>? literalTransform)
        {
            this.source = source;
            this.arguments = arguments;
            this.literalTransform = literalTransform;
            argumentIndexes = arguments.Select((argument, index) => (argument.Name, index))
                .ToDictionary(item => item.Name, item => item.index, StringComparer.Ordinal);
        }

        public HashSet<string> UsedArguments { get; } = new(StringComparer.Ordinal);

        public IReadOnlyList<MessageNode> Parse()
        {
            IReadOnlyList<MessageNode> nodes = ParseBlock(0, null, topLevel: true);
            if (position != source.Length)
            {
                Fail("Unexpected trailing message data");
            }

            return nodes;
        }

        private IReadOnlyList<MessageNode> ParseBlock(int depth, int? pluralArgument, bool topLevel)
        {
            if (depth > LocalizationLimits.MaximumMessageNesting)
            {
                Fail("Selection nesting exceeds its limit");
            }

            List<MessageNode> nodes = [];
            StringBuilder literal = new();
            while (position < source.Length)
            {
                char current = source[position];
                if (current == '}')
                {
                    if (topLevel)
                    {
                        Fail("Unmatched closing brace");
                    }

                    FlushLiteral(nodes, literal);
                    ++position;
                    return nodes;
                }

                if (current == '{')
                {
                    FlushLiteral(nodes, literal);
                    nodes.Add(ParsePlaceholder(depth, pluralArgument));
                    continue;
                }

                if (current == '#')
                {
                    if (pluralArgument is not int pluralIndex)
                    {
                        Fail("'#' is valid only inside plural and selectordinal branches");
                        return nodes;
                    }

                    FlushLiteral(nodes, literal);
                    nodes.Add(new MessagePoundNode(pluralIndex));
                    ++position;
                    continue;
                }

                if (current == '\'')
                {
                    ParseQuotedLiteral(literal);
                    continue;
                }

                literal.Append(current);
                ++position;
            }

            if (!topLevel)
            {
                Fail("Unterminated selection branch");
            }

            FlushLiteral(nodes, literal);
            return nodes;
        }

        private MessageNode ParsePlaceholder(int depth, int? outerPluralArgument)
        {
            ++position;
            SkipWhitespace();
            string name = ParseToken("argument name");
            if (!argumentIndexes.TryGetValue(name, out int argumentIndex))
            {
                Fail($"Unknown argument '{name}'");
            }

            UsedArguments.Add(name);
            SkipWhitespace();
            if (Consume('}'))
            {
                return new MessageArgumentNode(argumentIndex, MessageArgumentPresentation.Default);
            }

            Require(',');
            SkipWhitespace();
            string operation = ParseToken("argument operation");
            SkipWhitespace();
            if (operation is "number" or "percent")
            {
                Require('}');
                return new MessageArgumentNode(argumentIndex,
                    operation == "number" ? MessageArgumentPresentation.Number : MessageArgumentPresentation.Percent);
            }

            MessageSelectionKind selectionKind = operation switch
            {
                "select" => MessageSelectionKind.Select,
                "plural" => MessageSelectionKind.Cardinal,
                "selectordinal" => MessageSelectionKind.Ordinal,
                _ => throw Error($"Unsupported argument operation '{operation}'")
            };
            Require(',');
            List<MessageSelectionBranch> branches = [];
            HashSet<string> selectors = new(StringComparer.Ordinal);
            while (true)
            {
                SkipWhitespace();
                if (Consume('}'))
                {
                    break;
                }

                if (position >= source.Length)
                {
                    Fail("Unterminated selection");
                }

                if (branches.Count >= LocalizationLimits.MaximumBranchesPerSelection)
                {
                    Fail("Selection has too many branches");
                }

                string selector = source[position] == '=' ? ParseExactSelector() : ParseToken("branch selector");
                if (!selectors.Add(selector))
                {
                    Fail($"Duplicate selection branch '{selector}'");
                }

                SkipWhitespace();
                Require('{');
                IReadOnlyList<MessageNode> branchNodes = ParseBlock(
                    depth + 1,
                    selectionKind == MessageSelectionKind.Select ? outerPluralArgument : argumentIndex,
                    topLevel: false);
                branches.Add(new MessageSelectionBranch(selector, branchNodes));
            }

            if (!selectors.Contains("other"))
            {
                Fail("Every selection requires an 'other' branch");
            }

            return new MessageSelectionNode(argumentIndex, selectionKind, branches);
        }

        private void ParseQuotedLiteral(StringBuilder literal)
        {
            ++position;
            if (position < source.Length && source[position] == '\'')
            {
                literal.Append('\'');
                ++position;
                return;
            }

            while (position < source.Length)
            {
                char character = source[position++];
                if (character != '\'')
                {
                    literal.Append(character);
                    continue;
                }

                if (position < source.Length && source[position] == '\'')
                {
                    literal.Append('\'');
                    ++position;
                    continue;
                }

                return;
            }

            Fail("Unterminated apostrophe-quoted literal");
        }

        private string ParseExactSelector()
        {
            int start = position++;
            if (position < source.Length && source[position] == '-')
            {
                ++position;
            }

            int digitStart = position;
            while (position < source.Length && source[position] is >= '0' and <= '9')
            {
                ++position;
            }

            if (position == digitStart)
            {
                Fail("Exact plural selector requires an integer");
            }

            return source[start..position];
        }

        private string ParseToken(string description)
        {
            int start = position;
            while (position < source.Length &&
                (source[position] is >= 'a' and <= 'z' or >= '0' and <= '9' || source[position] == '-'))
            {
                ++position;
            }

            if (position == start || source[start] is < 'a' or > 'z')
            {
                Fail($"Expected canonical {description}");
            }

            return source[start..position];
        }

        private void FlushLiteral(List<MessageNode> nodes, StringBuilder literal)
        {
            if (literal.Length == 0)
            {
                return;
            }

            string value = literal.ToString();
            nodes.Add(new MessageLiteralNode(literalTransform?.Invoke(value) ?? value));
            literal.Clear();
        }

        private void SkipWhitespace()
        {
            while (position < source.Length && source[position] is ' ' or '\t' or '\r' or '\n')
            {
                ++position;
            }
        }

        private bool Consume(char expected)
        {
            if (position >= source.Length || source[position] != expected)
            {
                return false;
            }

            ++position;
            return true;
        }

        private void Require(char expected)
        {
            if (!Consume(expected))
            {
                Fail($"Expected '{expected}'");
            }
        }

        private MessageParseException Error(string message) =>
            new($"{message} at character {position}.");

        private void Fail(string message) => throw Error(message);
    }

    private sealed class MessageParseException : Exception
    {
        public MessageParseException(string message)
            : base(message)
        {
        }
    }
}
