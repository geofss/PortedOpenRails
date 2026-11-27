using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads the original MSTS/Open Rails locomotive force and power limits, including
/// ORTS two-dimensional throttle/speed traction tables.
/// </summary>
public sealed class LocomotiveTractionMetadataReader
{
    public LocomotiveTractionMetadata Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var normalized = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(normalized, useTree: false);
            var maxForce = 0f;
            var maxContinuousForce = 0f;
            var maxPower = 0f;
            var maxSpeed = 1_000f;
            var unloadingSpeed = 0f;
            var runUpTimeToMaxForce = -1f;
            var tractionRampUpRate = 0f;
            var tractionRampDownRate = 0f;
            var tractionRampDownToZeroRate = 0f;
            InterpolatorDiesel2D? tractionCurves = null;
            var engineFound = false;

            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("engine", () =>
                {
                    engineFound = true;
                    reader.MustMatch("(");
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("maxforce", () =>
                            maxForce = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("maxcontinuousforce", () =>
                            maxContinuousForce = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("maxpower", () =>
                            maxPower = reader.ReadFloatBlock(STFReader.UNITS.Power, null)),
                        new STFReader.TokenProcessor("maxvelocity", () =>
                            maxSpeed = reader.ReadFloatBlock(STFReader.UNITS.Speed, null)),
                        new STFReader.TokenProcessor("ortsunloadingspeed", () =>
                            unloadingSpeed = reader.ReadFloatBlock(STFReader.UNITS.Speed, null)),
                        new STFReader.TokenProcessor("runuptimetomaxforce", () =>
                            runUpTimeToMaxForce = reader.ReadFloatBlock(STFReader.UNITS.Time, null)),
                        new STFReader.TokenProcessor("ortstractiveforcerampuprate", () =>
                            tractionRampUpRate = reader.ReadFloatBlock(STFReader.UNITS.ForceRate, null)),
                        new STFReader.TokenProcessor("ortstractiveforcerampdownrate", () =>
                            tractionRampDownRate = reader.ReadFloatBlock(STFReader.UNITS.ForceRate, null)),
                        new STFReader.TokenProcessor("ortstractiveforcerampdowntozerorate", () =>
                            tractionRampDownToZeroRate = reader.ReadFloatBlock(STFReader.UNITS.ForceRate, null)),
                        new STFReader.TokenProcessor("ortsmaxtractiveforcecurves", () =>
                            tractionCurves = new InterpolatorDiesel2D(reader, false)),
                        new STFReader.TokenProcessor("ortstractioncharacteristics", () =>
                            tractionCurves = new InterpolatorDiesel2D(reader, true))
                    });
                })
            });

            if (!engineFound)
                throw new InvalidDataException($"Locomotive file '{normalized}' contains no Engine block.");

            tractionCurves?.HasNegativeValue();
            if (maxForce > 0 && runUpTimeToMaxForce > 0 &&
                tractionRampUpRate <= 0 && tractionRampDownRate <= 0 && tractionRampDownToZeroRate <= 0)
            {
                tractionRampUpRate = maxForce / runUpTimeToMaxForce;
                tractionRampDownRate = 2 * tractionRampUpRate;
                tractionRampDownToZeroRate = 10 * tractionRampUpRate;
            }
            return new LocomotiveTractionMetadata(
                Path.GetFileNameWithoutExtension(normalized),
                maxForce,
                maxContinuousForce,
                maxPower,
                maxSpeed,
                unloadingSpeed,
                tractionCurves,
                normalized,
                tractionRampUpRate,
                tractionRampDownRate,
                tractionRampDownToZeroRate);
        }
    }
}

public sealed record LocomotiveTractionMetadata(
    string Name,
    float MaxForceNewtons,
    float MaxContinuousForceNewtons,
    float MaxPowerWatts,
    float MaxSpeedMetersPerSecond,
    float UnloadingSpeedMetersPerSecond,
    InterpolatorDiesel2D? TractionForceCurves,
    string SourcePath,
    float TractionForceRampUpNewtonsPerSecond = 0,
    float TractionForceRampDownNewtonsPerSecond = 0,
    float TractionForceRampDownToZeroNewtonsPerSecond = 0);
