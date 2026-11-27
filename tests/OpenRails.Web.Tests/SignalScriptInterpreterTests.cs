using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class SignalScriptInterpreterTests
{
    private readonly SignalScriptInterpreter interpreter = new();

    [Fact]
    public void ExecutesNativeAssignmentsArithmeticAndConditionalBranches()
    {
        var script = Script("""
            EXTERN FLOAT STATE;
            EXTERN FLOAT DRAW_STATE;
            FLOAT local;
            local = 2 + 3 * 4;
            IF local == 14 {
                STATE = 6;
                DRAW_STATE = 3;
            } ELSE {
                STATE = 0;
            }
            """);

        var result = interpreter.Execute(script,
            new SignalScriptExecutionContext(0, 0, false, 0, true));

        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal(6, result.State);
        Assert.Equal(3, result.DrawState);
    }

    [Fact]
    public void SupportsParenthesizedArithmeticOnComparisonLeftSide()
    {
        var script = Script("""
            EXTERN FLOAT STATE;
            IF (STATE + 1) == 3 {
                STATE = 6;
            } ELSE {
                STATE = 0;
            }
            """);

        var result = interpreter.Execute(script,
            new SignalScriptExecutionContext(2, 0, false, 0, true));

        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal(6, result.State);
    }

    [Fact]
    public void ReadsNativeBlockStateAndRouteSetFunctions()
    {
        var script = Script("""
            EXTERN FLOAT STATE;
            IF BLOCK_STATE() == 0 AND ROUTE_SET() == 1 {
                STATE = 7;
            } ELSE {
                STATE = 0;
            }
            """);

        var result = interpreter.Execute(script,
            new SignalScriptExecutionContext(0, 0, false, 0, true));

        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal(7, result.State);
    }

    [Fact]
    public void StopsAtReturnAndDoesNotExecuteFollowingAssignments()
    {
        var script = Script("""
            EXTERN FLOAT STATE;
            STATE = 3;
            RETURN;
            STATE = 7;
            """);

        var result = interpreter.Execute(script,
            new SignalScriptExecutionContext(0, 0, false, 0, true));

        Assert.True(result.Success, result.Diagnostic);
        Assert.True(result.Returned);
        Assert.Equal(3, result.State);
    }

    [Fact]
    public void UnsupportedNativeFunctionFailsClosed()
    {
        var script = Script("""
            EXTERN FLOAT STATE;
            STATE = NEXT_SIG_LR();
            """);

        var result = interpreter.Execute(script,
            new SignalScriptExecutionContext(7, 2, false, 0, true));

        Assert.False(result.Success);
        Assert.Equal(7, result.State);
        Assert.Contains("NEXT_SIG_LR", result.Diagnostic);
    }

    [Fact]
    public void EnforcesInstructionBudget()
    {
        var script = Script("""
            EXTERN FLOAT STATE;
            STATE = 2;
            STATE = 3;
            STATE = 4;
            """);

        var result = interpreter.Execute(script,
            new SignalScriptExecutionContext(0, 0, false, 0, true), instructionBudget: 1);

        Assert.False(result.Success);
        Assert.Contains("instruction budget", result.Diagnostic);
    }

    [Fact]
    public void RejectsWritesToReadOnlyExternalFloats()
    {
        var script = Script("""
            EXTERN FLOAT ENABLED;
            ENABLED = 1;
            """);

        var result = interpreter.Execute(script,
            new SignalScriptExecutionContext(0, 0, false, 0, true));

        Assert.False(result.Success);
        Assert.Contains("read-only", result.Diagnostic);
    }

    [Fact]
    public void ArithmeticOverflowFailsClosedAndPreservesInitialSignalState()
    {
        var script = Script("""
            EXTERN FLOAT STATE;
            STATE = 2147483647 + 1;
            """);

        var result = interpreter.Execute(script,
            new SignalScriptExecutionContext(6, 2, false, 0, true));

        Assert.False(result.Success);
        Assert.Equal(6, result.State);
        Assert.Contains("overflow", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnaryNegationOverflowFailsClosed()
    {
        var script = Script("""
            EXTERN FLOAT STATE;
            STATE = -(-2147483647 - 1);
            """);

        var result = interpreter.Execute(script,
            new SignalScriptExecutionContext(3, 1, false, 0, true));

        Assert.False(result.Success);
        Assert.Equal(3, result.State);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("%")]
    public void DivisionByZeroFailsClosed(string operation)
    {
        var script = Script($"""
            EXTERN FLOAT STATE;
            STATE = 12 {operation} 0;
            """);

        var result = interpreter.Execute(script,
            new SignalScriptExecutionContext(7, 1, false, 0, true));

        Assert.False(result.Success);
        Assert.Equal(7, result.State);
        Assert.Contains("zero", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static SignalScriptDefinition Script(string source)
    {
        var lines = source.Split('\n').Select(line => line.Trim()).ToArray();
        var locals = lines.Where(line => line.StartsWith("FLOAT ", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[6..].Trim().TrimEnd(';')).ToArray();
        var externals = lines.Where(line => line.StartsWith("EXTERN FLOAT ", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[13..].Trim().TrimEnd(';')).ToArray();
        return new SignalScriptDefinition("test", lines, locals, externals, lines.Length, Array.Empty<string>());
    }
}
