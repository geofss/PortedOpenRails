// COPYRIGHT 2011 by the Open Rails project.
// Browser-compatible adaptation of ORTS.Common.Integrator from the pinned
// Open Rails source tree. Copyright and GPL-3.0-or-later terms are retained.
// Original source: Source/Orts.Common/Integrator.cs
// Upstream revision: dfb5246ca9da46e352cfcd2a365d284ac55c1a1f
//
// This file is part of Open Rails.
// Open Rails is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your
// option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See <https://www.gnu.org/licenses/>.

namespace OpenRails.Web.Simulation.Upstream;

/// <summary>
/// Browser-safe numerical integration core adapted from ORTS.Common.Integrator.
/// It preserves the native solver choices used by rolling-stock subsystems while
/// replacing BinaryReader/BinaryWriter persistence with an explicit snapshot.
/// </summary>
public enum NativeIntegratorMethod
{
    EulerBackward = 0,
    EulerBackModified = 1,
    EulerForward = 2,
    RungeKutta2 = 3,
    RungeKutta4 = 4,
    NewtonRaphson = 5,
    AdamsMoulton = 6
}

public sealed class NativeNumericalIntegrator
{
    private readonly float[] previousDerivatives = new float[4];
    private float previousDerivative;
    private int adaptiveSubsteps = 1;
    private int waitBeforeReducing = 0;

    public NativeIntegratorMethod Method { get; set; } = NativeIntegratorMethod.EulerBackward;
    public float InitialCondition { get; }
    public float Value { get; private set; }
    public float MinStep { get; set; } = 0.001f;
    public int MaxSubsteps { get; set; } = 300;
    public float Error { get; set; } = 0.001f;
    public float Minimum { get; set; } = -1000f;
    public float Maximum { get; set; } = 1000f;
    public bool IsLimited { get; set; }
    public int NumOfSubstepsPerSecond => adaptiveSubsteps;
    public bool IsStepDividing { get; private set; }

    public NativeNumericalIntegrator(float initialCondition = 0,
        NativeIntegratorMethod method = NativeIntegratorMethod.EulerBackward)
    {
        if (!float.IsFinite(initialCondition))
            throw new ArgumentOutOfRangeException(nameof(initialCondition));
        InitialCondition = initialCondition;
        Value = initialCondition;
        Method = method;
    }

    public float Integrate(float timeSpan, Func<float, float> derivative)
    {
        ArgumentNullException.ThrowIfNull(derivative);
        if (!float.IsFinite(timeSpan))
            throw new ArgumentOutOfRangeException(nameof(timeSpan));
        if (timeSpan <= 0)
            return Value;
        if (!float.IsFinite(MinStep) || MinStep <= 0 ||
            MaxSubsteps < 1 || !float.IsFinite(Error) || Error < 0 ||
            !float.IsFinite(Minimum) || !float.IsFinite(Maximum) ||
            Maximum <= Minimum)
            throw new InvalidOperationException("Integrator configuration is invalid.");

        if (MathF.Abs(previousDerivative) > Error)
        {
            adaptiveSubsteps = Math.Min(adaptiveSubsteps + 1, MaxSubsteps);
            waitBeforeReducing = 100;
        }
        else if (--waitBeforeReducing <= 0)
        {
            adaptiveSubsteps = Math.Max(adaptiveSubsteps - 1, 1);
            waitBeforeReducing = 10;
        }

        var steps = Math.Clamp(Math.Max(adaptiveSubsteps,
            (int)MathF.Ceiling(timeSpan / Math.Max(MinStep, timeSpan / MaxSubsteps))),
            1, MaxSubsteps);
        var dt = timeSpan / steps;
        IsStepDividing = steps > 1;
        var derivativeNow = 0f;

        for (var i = 0; i < steps; i++)
        {
            var y = Value;
            switch (Method)
            {
                case NativeIntegratorMethod.EulerBackward:
                    derivativeNow = derivative(y);
                    Value = y + dt * derivativeNow;
                    break;
                case NativeIntegratorMethod.EulerBackModified:
                    derivativeNow = derivative(y);
                    Value = y + dt * 0.5f * (previousDerivatives[0] + derivativeNow);
                    previousDerivatives[0] = derivativeNow;
                    break;
                case NativeIntegratorMethod.EulerForward:
                    derivativeNow = derivative(y);
                    Value = y + dt * derivative(y + dt * derivativeNow);
                    break;
                case NativeIntegratorMethod.RungeKutta2:
                    var midpointSlope = derivative(y + 0.5f * dt * derivative(y));
                    derivativeNow = midpointSlope;
                    Value = y + dt * midpointSlope;
                    break;
                case NativeIntegratorMethod.RungeKutta4:
                    var k1 = derivative(y);
                    var k2 = derivative(y + 0.5f * dt * k1);
                    var k3 = derivative(y + 0.5f * dt * k2);
                    var k4 = derivative(y + dt * k3);
                    derivativeNow = k4;
                    Value = y + dt * (k1 + 2 * k2 + 2 * k3 + k4) / 6f;
                    break;
                case NativeIntegratorMethod.AdamsMoulton:
                    var predicted = y + dt / 24f *
                        (55f * previousDerivatives[0] - 59f * previousDerivatives[1] +
                         37f * previousDerivatives[2] - 9f * previousDerivatives[3]);
                    Value = y + dt / 24f *
                        (9f * derivative(predicted) + 19f * previousDerivatives[0] -
                         5f * previousDerivatives[1] + previousDerivatives[2]);
                    derivativeNow = derivative(Value);
                    Array.Copy(previousDerivatives, 0, previousDerivatives, 1, 3);
                    previousDerivatives[0] = derivativeNow;
                    break;
                case NativeIntegratorMethod.NewtonRaphson:
                    throw new NotSupportedException("The upstream Newton-Raphson mode has no implemented solver.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(Method));
            }

            if (!float.IsFinite(Value) || !float.IsFinite(derivativeNow))
                throw new ArithmeticException("Numerical integration produced a non-finite value.");
            if (IsLimited)
                Value = Math.Clamp(Value, Minimum, Maximum);
        }

        previousDerivative = derivativeNow;
        return Value;
    }

    public void Reset()
    {
        Value = InitialCondition;
        Array.Clear(previousDerivatives);
        previousDerivative = 0;
        adaptiveSubsteps = 1;
        waitBeforeReducing = 0;
        IsStepDividing = false;
    }

    public NativeIntegratorSnapshot CaptureState() =>
        new(Value, previousDerivative, adaptiveSubsteps, waitBeforeReducing,
            (float[])previousDerivatives.Clone());

    public void RestoreState(NativeIntegratorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!float.IsFinite(snapshot.Value) || !float.IsFinite(snapshot.PreviousDerivative) ||
            snapshot.AdaptiveSubsteps < 1 || snapshot.AdaptiveSubsteps > MaxSubsteps ||
            snapshot.WaitBeforeReducing < 0 || snapshot.PreviousDerivatives is not { Length: 4 } ||
            snapshot.PreviousDerivatives.Any(x => !float.IsFinite(x)))
            throw new ArgumentException("Integrator snapshot is invalid.", nameof(snapshot));
        Value = snapshot.Value;
        previousDerivative = snapshot.PreviousDerivative;
        adaptiveSubsteps = snapshot.AdaptiveSubsteps;
        waitBeforeReducing = snapshot.WaitBeforeReducing;
        Array.Copy(snapshot.PreviousDerivatives, previousDerivatives, 4);
    }
}

public sealed record NativeIntegratorSnapshot(
    float Value,
    float PreviousDerivative,
    int AdaptiveSubsteps,
    int WaitBeforeReducing,
    float[] PreviousDerivatives);
