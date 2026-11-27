using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Executes the deterministic core of the native MSTS SIGSCR language: declared
/// local/external integers, assignments, arithmetic, IF/ELSEIF/ELSE, comparisons,
/// logical operators and RETURN. Unsupported native signal-topology functions fail
/// closed so the caller can retain a restrictive indication.
/// </summary>
public sealed class SignalScriptInterpreter
{
    public const int DefaultInstructionBudget = 4096;

    public SignalScriptExecutionResult Execute(
        SignalScriptDefinition script,
        SignalScriptExecutionContext context,
        int instructionBudget = DefaultInstructionBudget)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);
        if (instructionBudget < 1 || instructionBudget > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(instructionBudget));
        if (script.Diagnostics.Count > 0)
            return SignalScriptExecutionResult.Failed(context.InitialState, context.InitialDrawState,
                "Script has structural diagnostics and was not executed.");

        try
        {
            var parser = new Parser(script.SourceLines);
            var statements = parser.Parse();
            var runtime = new Runtime(script, context, instructionBudget);
            runtime.Execute(statements);
            return new SignalScriptExecutionResult(true, runtime.State, runtime.DrawState,
                runtime.Returned, null, runtime.InstructionsExecuted, runtime.DrawStateWasAssigned);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or
            OverflowException or DivideByZeroException or ArgumentException)
        {
            return SignalScriptExecutionResult.Failed(context.InitialState, context.InitialDrawState,
                exception.Message);
        }
    }

    private sealed class Runtime
    {
        private readonly SignalScriptDefinition script;
        private readonly SignalScriptExecutionContext context;
        private readonly int budget;
        private readonly Dictionary<string, int> locals;
        private readonly Dictionary<string, int> external;
        public int State { get; private set; }
        public int DrawState { get; private set; }
        public bool Returned { get; private set; }
        public bool DrawStateWasAssigned { get; private set; }
        public int InstructionsExecuted { get; private set; }

        public Runtime(SignalScriptDefinition script, SignalScriptExecutionContext context, int budget)
        {
            this.script = script;
            this.context = context;
            this.budget = budget;
            State = context.InitialState;
            DrawState = context.InitialDrawState;
            locals = script.LocalFloats.ToDictionary(name => name, _ => 0, StringComparer.OrdinalIgnoreCase);
            external = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["STATE"] = State,
                ["DRAW_STATE"] = DrawState,
                ["ENABLED"] = context.Enabled ? 1 : 0,
                ["BLOCK_STATE"] = context.BlockState,
                ["APPROACH_CONTROL_REQ_POSITION"] = context.ApproachControlRequiredPosition ?? -1,
                ["APPROACH_CONTROL_REQ_SPEED"] = context.ApproachControlRequiredSpeed ?? -1
            };
            foreach (var name in script.ExternalFloats)
                if (!external.ContainsKey(name))
                    throw new NotSupportedException($"Unsupported native external float '{name}'.");
        }

        public void Execute(IReadOnlyList<Node> statements)
        {
            foreach (var statement in statements)
            {
                if (Returned) break;
                Tick();
                switch (statement)
                {
                    case Assignment assignment:
                        Assign(assignment.Name, assignment.Value.Evaluate(this));
                        break;
                    case Conditional conditional:
                        var selected = conditional.Branches.FirstOrDefault(branch =>
                            branch.Condition is null || branch.Condition.Evaluate(this));
                        if (selected is not null)
                            Execute(selected.Statements);
                        break;
                    case Return:
                        Returned = true;
                        break;
                }
            }
        }

        public int Read(string name)
        {
            if (locals.TryGetValue(name, out var local)) return local;
            if (external.TryGetValue(name, out var value)) return value;
            throw new InvalidDataException($"SIGSCR references undeclared variable '{name}'.");
        }

        public int Call(string name, IReadOnlyList<int> arguments)
        {
            Tick();
            if (name.Equals("BLOCK_STATE", StringComparison.OrdinalIgnoreCase) && arguments.Count == 0)
                return context.BlockState;
            if (name.Equals("ROUTE_SET", StringComparison.OrdinalIgnoreCase) && arguments.Count == 0)
                return context.RouteSet ? 1 : 0;
            if (name.Equals("DEF_DRAW_STATE", StringComparison.OrdinalIgnoreCase) && arguments.Count == 1 &&
                context.DrawStateResolver is not null)
                return context.DrawStateResolver(arguments[0]) ?? 0;
            if (context.FunctionResolver?.Invoke(name, arguments) is { } resolved)
                return resolved;
            throw new NotSupportedException($"Native SIGSCR function '{name}' is not available in the browser signal context.");
        }

        private void Assign(string name, int value)
        {
            if (locals.ContainsKey(name))
            {
                locals[name] = value;
                return;
            }
            if (name.Equals("STATE", StringComparison.OrdinalIgnoreCase))
            {
                State = value;
                external[name] = value;
                return;
            }
            if (name.Equals("DRAW_STATE", StringComparison.OrdinalIgnoreCase))
            {
                DrawState = value;
                DrawStateWasAssigned = true;
                external[name] = value;
                return;
            }
            if (external.ContainsKey(name))
                throw new InvalidDataException($"SIGSCR cannot assign to read-only external float '{name}'.");
            throw new InvalidDataException($"SIGSCR assigns to undeclared variable '{name}'.");
        }

        private void Tick()
        {
            InstructionsExecuted++;
            if (InstructionsExecuted > budget)
                throw new InvalidDataException($"SIGSCR instruction budget of {budget} was exceeded.");
        }
    }

    private abstract record Node;
    private sealed record Assignment(string Name, Expression Value) : Node;
    private sealed record Return : Node;
    private sealed record Conditional(IReadOnlyList<Branch> Branches) : Node;
    private sealed record Branch(Condition? Condition, IReadOnlyList<Node> Statements);

    private abstract record Expression
    {
        public abstract int Evaluate(Runtime runtime);
    }
    private sealed record Constant(int Value) : Expression
    {
        public override int Evaluate(Runtime runtime) => Value;
    }
    private sealed record Variable(string Name) : Expression
    {
        public override int Evaluate(Runtime runtime) => runtime.Read(Name);
    }
    private sealed record FunctionCall(string Name, IReadOnlyList<Expression> Arguments) : Expression
    {
        public override int Evaluate(Runtime runtime) => runtime.Call(Name, Arguments.Select(argument => argument.Evaluate(runtime)).ToArray());
    }
    private sealed record Unary(string Operator, Expression Operand) : Expression
    {
        public override int Evaluate(Runtime runtime)
        {
            var value = Operand.Evaluate(runtime);
            return Operator switch
            {
                "-" => checked(-value),
                "+" => value,
                "!" => value == 0 ? 1 : 0,
                _ => throw new InvalidDataException($"Unsupported unary operator '{Operator}'.")
            };
        }
    }
    private sealed record Binary(Expression Left, string Operator, Expression Right) : Expression
    {
        public override int Evaluate(Runtime runtime)
        {
            var left = Left.Evaluate(runtime);
            if (Operator.Equals("&&", StringComparison.OrdinalIgnoreCase) || Operator.Equals("AND", StringComparison.OrdinalIgnoreCase))
                return left != 0 && Right.Evaluate(runtime) != 0 ? 1 : 0;
            if (Operator.Equals("||", StringComparison.OrdinalIgnoreCase) || Operator.Equals("OR", StringComparison.OrdinalIgnoreCase))
                return left != 0 || Right.Evaluate(runtime) != 0 ? 1 : 0;
            var right = Right.Evaluate(runtime);
            return Operator switch
            {
                "+" => checked(left + right),
                "-" => checked(left - right),
                "*" => checked(left * right),
                "/" => right == 0 ? 0 : left / right,
                "%" => right == 0 ? 0 : left % right,
                _ => throw new InvalidDataException($"Unsupported arithmetic operator '{Operator}'.")
            };
        }
    }
    private sealed record Comparison(Expression Left, string Operator, Expression Right) : Condition
    {
        public override bool Evaluate(Runtime runtime)
        {
            var left = Left.Evaluate(runtime);
            var right = Right.Evaluate(runtime);
            return Operator switch
            {
                ">" => left > right,
                ">=" => left >= right,
                "<" => left < right,
                "<=" => left <= right,
                "==" or "::" => left == right,
                "!=" => left != right,
                _ => throw new InvalidDataException($"Unsupported comparison '{Operator}'.")
            };
        }
    }
    private abstract record Condition
    {
        public abstract bool Evaluate(Runtime runtime);
    }
    private sealed record Truthy(Expression Value) : Condition
    {
        public override bool Evaluate(Runtime runtime) => Value.Evaluate(runtime) != 0;
    }
    private sealed record Not(Condition Value) : Condition
    {
        public override bool Evaluate(Runtime runtime) => !Value.Evaluate(runtime);
    }
    private sealed record Logical(Condition Left, string Operator, Condition Right) : Condition
    {
        public override bool Evaluate(Runtime runtime) =>
            Operator.Equals("&&", StringComparison.OrdinalIgnoreCase) || Operator.Equals("AND", StringComparison.OrdinalIgnoreCase)
                ? Left.Evaluate(runtime) && Right.Evaluate(runtime)
                : Left.Evaluate(runtime) || Right.Evaluate(runtime);
    }

    private sealed class Parser
    {
        private readonly List<Token> tokens;
        private int position;
        public Parser(IEnumerable<string> lines)
        {
            tokens = Lex(string.Join("\n", lines));
        }

        public IReadOnlyList<Node> Parse()
        {
            var statements = ParseBlock(false);
            if (Current.Kind != TokenKind.End)
                throw Error($"Unexpected token '{Current.Text}'.");
            return statements;
        }

        private List<Node> ParseBlock(bool nested)
        {
            var result = new List<Node>();
            while (Current.Kind != TokenKind.End && !(nested && Is("}")))
            {
                if (Match(";")) continue;
                if (Match("FLOAT")) { ExpectIdentifier(); Expect(";"); continue; }
                if (Match("EXTERN")) { Expect("FLOAT"); ExpectIdentifier(); Expect(";"); continue; }
                if (Match("RETURN")) { Match(";"); result.Add(new Return()); continue; }
                if (Match("IF")) { result.Add(ParseIf()); continue; }
                if (Current.Kind == TokenKind.Identifier)
                {
                    var name = Advance().Text;
                    Expect("=");
                    var expression = ParseExpression();
                    Expect(";");
                    result.Add(new Assignment(name, expression));
                    continue;
                }
                throw Error($"Unsupported SIGSCR statement beginning with '{Current.Text}'.");
            }
            if (nested) Expect("}");
            return result;
        }

        private Conditional ParseIf()
        {
            var branches = new List<Branch>();
            var condition = ParseCondition();
            Expect("{");
            branches.Add(new Branch(condition, ParseBlock(true)));
            while (Is("ELSEIF") || (Is("ELSE") && Peek(1).Text.Equals("IF", StringComparison.OrdinalIgnoreCase)))
            {
                if (Match("ELSE")) Expect("IF");
                else Expect("ELSEIF");
                var nextCondition = ParseCondition();
                Expect("{");
                branches.Add(new Branch(nextCondition, ParseBlock(true)));
            }
            if (Match("ELSE"))
            {
                Expect("{");
                branches.Add(new Branch(null, ParseBlock(true)));
            }
            return new Conditional(branches);
        }

        private Condition ParseCondition() => ParseConditionOr();

        private Condition ParseConditionOr()
        {
            var value = ParseConditionAnd();
            while (Is("||") || Is("OR"))
            {
                var op = Advance().Text;
                value = new Logical(value, op, ParseConditionAnd());
            }
            return value;
        }

        private Condition ParseConditionAnd()
        {
            var value = ParseConditionAtom();
            while (Is("&&") || Is("AND"))
            {
                var op = Advance().Text;
                value = new Logical(value, op, ParseConditionAtom());
            }
            return value;
        }

        private Condition ParseConditionAtom()
        {
            if (Match("!")) return new Not(ParseConditionAtom());
            if (Match("("))
            {
                var inner = ParseCondition();
                Expect(")");
                if (inner is Truthy truthy &&
                    Current.Text is ">" or ">=" or "<" or "<=" or "==" or "!=" or "::")
                {
                    var op = Advance().Text;
                    return new Comparison(truthy.Value, op, ParseExpression());
                }
                return inner;
            }
            var left = ParseExpression();
            if (Current.Text is ">" or ">=" or "<" or "<=" or "==" or "!=" or "::")
            {
                var op = Advance().Text;
                return new Comparison(left, op, ParseExpression());
            }
            return new Truthy(left);
        }

        private Expression ParseExpression() => ParseAdditive();
        private Expression ParseAdditive()
        {
            var value = ParseMultiplicative();
            while (Is("+") || Is("-"))
            {
                var op = Advance().Text;
                value = new Binary(value, op, ParseMultiplicative());
            }
            return value;
        }
        private Expression ParseMultiplicative()
        {
            var value = ParseUnary();
            while (Is("*") || Is("/") || Is("%"))
            {
                var op = Advance().Text;
                value = new Binary(value, op, ParseUnary());
            }
            return value;
        }
        private Expression ParseUnary()
        {
            if (Is("-") || Is("+") || Is("!"))
            {
                var op = Advance().Text;
                return new Unary(op, ParseUnary());
            }
            return ParsePrimary();
        }
        private Expression ParsePrimary()
        {
            if (Match("("))
            {
                var value = ParseExpression();
                Expect(")");
                return value;
            }
            if (Current.Kind == TokenKind.Number)
                return new Constant(int.Parse(Advance().Text, System.Globalization.CultureInfo.InvariantCulture));
            if (Current.Kind != TokenKind.Identifier)
                throw Error($"Expected value, found '{Current.Text}'.");
            var name = Advance().Text;
            if (!Match("(")) return new Variable(name);
            var arguments = new List<Expression>();
            if (!Match(")"))
            {
                do { arguments.Add(ParseExpression()); } while (Match(","));
                Expect(")");
            }
            return new FunctionCall(name, arguments);
        }

        private void ExpectIdentifier()
        {
            if (Current.Kind != TokenKind.Identifier) throw Error("Expected a variable declaration.");
            Advance();
        }
        private void Expect(string text)
        {
            if (!Match(text)) throw Error($"Expected '{text}', found '{Current.Text}'.");
        }
        private bool Match(string text)
        {
            if (!Is(text)) return false;
            Advance();
            return true;
        }
        private bool Is(string text) => Current.Text.Equals(text, StringComparison.OrdinalIgnoreCase);
        private Token Advance()
        {
            var token = Current;
            if (position < tokens.Count - 1) position++;
            return token;
        }
        private Token Current => tokens[position];
        private Token Peek(int offset) => tokens[Math.Min(position + offset, tokens.Count - 1)];
        private InvalidDataException Error(string message) => new($"SIGSCR line {Current.Line}: {message}");

        private static List<Token> Lex(string source)
        {
            var result = new List<Token>();
            var line = 1;
            for (var index = 0; index < source.Length;)
            {
                var character = source[index];
                if (character == '\n') { line++; index++; continue; }
                if (char.IsWhiteSpace(character)) { index++; continue; }
                if (character == '/' && index + 1 < source.Length && source[index + 1] == '/')
                {
                    while (index < source.Length && source[index] != '\n') index++;
                    continue;
                }
                if (character == '/' && index + 1 < source.Length && source[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < source.Length && !(source[index] == '*' && source[index + 1] == '/'))
                    {
                        if (source[index] == '\n') line++;
                        index++;
                    }
                    if (index + 1 >= source.Length) throw new InvalidDataException($"SIGSCR line {line}: unclosed block comment.");
                    index += 2;
                    continue;
                }
                if (char.IsLetter(character) || character == '_')
                {
                    var start = index++;
                    while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_')) index++;
                    result.Add(new Token(TokenKind.Identifier, source[start..index], line));
                    continue;
                }
                if (char.IsDigit(character))
                {
                    var start = index++;
                    while (index < source.Length && char.IsDigit(source[index])) index++;
                    result.Add(new Token(TokenKind.Number, source[start..index], line));
                    continue;
                }
                if (index + 1 < source.Length && source.Substring(index, 2) is ">=" or "<=" or "==" or "!=" or "&&" or "||" or "::")
                {
                    result.Add(new Token(TokenKind.Symbol, source.Substring(index, 2), line));
                    index += 2;
                    continue;
                }
                if ("{}();,+-*/%!=<>".Contains(character))
                {
                    result.Add(new Token(TokenKind.Symbol, character.ToString(), line));
                    index++;
                    continue;
                }
                throw new InvalidDataException($"SIGSCR line {line}: unsupported character '{character}'.");
            }
            result.Add(new Token(TokenKind.End, "<end>", line));
            return result;
        }
    }

    private enum TokenKind { Identifier, Number, Symbol, End }
    private sealed record Token(TokenKind Kind, string Text, int Line);
}

public sealed record SignalScriptExecutionContext(
    int InitialState,
    int InitialDrawState,
    bool Enabled,
    int BlockState,
    bool RouteSet,
    int? ApproachControlRequiredPosition = null,
    int? ApproachControlRequiredSpeed = null,
    Func<int, int?>? DrawStateResolver = null,
    Func<string, IReadOnlyList<int>, int?>? FunctionResolver = null);

public sealed record SignalScriptExecutionResult(
    bool Success,
    int State,
    int DrawState,
    bool Returned,
    string? Diagnostic,
    int InstructionsExecuted,
    bool DrawStateWasAssigned = false)
{
    public static SignalScriptExecutionResult Failed(int state, int drawState, string diagnostic) =>
        new(false, state, drawState, false, diagnostic, 0, false);
}
