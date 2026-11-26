// COPYRIGHT 2009-2013 by the Open Rails project.
// This file is part of Open Rails and is licensed under GPL-3.0-or-later.
// Browser-port adaptation reads the original little-endian terrain altitude/flag files.

using System.Buffers.Binary;
using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

public sealed class TerrainAltitudeReader
{
    private const int MaximumSamplesPerSide = 4096;

    public TerrainAltitudeData Read(IVirtualContentStore store, string path, int sampleCount)
    {
        ArgumentNullException.ThrowIfNull(store);
        ValidateSampleCount(sampleCount);
        var normalized = VirtualPath.Normalize(path);
        var byteCount = checked(sampleCount * sampleCount * sizeof(ushort));
        var bytes = new byte[byteCount];

        using var stream = store.OpenRead(normalized);
        stream.ReadExactly(bytes);

        var samples = new ushort[sampleCount * sampleCount];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2, 2));

        return new TerrainAltitudeData(sampleCount, samples);
    }

    internal static void ValidateSampleCount(int sampleCount)
    {
        if (sampleCount <= 0 || sampleCount > MaximumSamplesPerSide)
            throw new InvalidDataException($"Terrain sample count {sampleCount} is outside the supported range.");
    }
}

public sealed class TerrainFlagsReader
{
    public TerrainFlagsData Read(IVirtualContentStore store, string path, int sampleCount)
    {
        ArgumentNullException.ThrowIfNull(store);
        TerrainAltitudeReader.ValidateSampleCount(sampleCount);
        var normalized = VirtualPath.Normalize(path);
        var bytes = new byte[checked(sampleCount * sampleCount)];

        using var stream = store.OpenRead(normalized);
        stream.ReadExactly(bytes);
        return new TerrainFlagsData(sampleCount, bytes);
    }
}

public sealed class TerrainAltitudeData
{
    private readonly ushort[] samples;
    public int SampleCount { get; }

    public TerrainAltitudeData(int sampleCount, ushort[] samples)
    {
        TerrainAltitudeReader.ValidateSampleCount(sampleCount);
        if (samples.Length != checked(sampleCount * sampleCount))
            throw new ArgumentException("Altitude sample count does not match the terrain grid.", nameof(samples));

        SampleCount = sampleCount;
        this.samples = (ushort[])samples.Clone();
    }

    public ushort GetRawSample(int x, int z) => samples[GetIndex(x, z)];

    public float GetElevationMeters(int x, int z, float floorMeters, float scaleMetersPerUnit) =>
        floorMeters + GetRawSample(x, z) * scaleMetersPerUnit;

    private int GetIndex(int x, int z)
    {
        if ((uint)x >= (uint)SampleCount || (uint)z >= (uint)SampleCount)
            throw new ArgumentOutOfRangeException(nameof(x));
        return checked(z * SampleCount + x);
    }
}

public sealed class TerrainFlagsData
{
    private readonly byte[] flags;
    public int SampleCount { get; }

    public TerrainFlagsData(int sampleCount, byte[] flags)
    {
        TerrainAltitudeReader.ValidateSampleCount(sampleCount);
        if (flags.Length != checked(sampleCount * sampleCount))
            throw new ArgumentException("Terrain flag count does not match the terrain grid.", nameof(flags));

        SampleCount = sampleCount;
        this.flags = (byte[])flags.Clone();
    }

    public bool IsVertexHidden(int x, int z)
    {
        if ((uint)x >= (uint)SampleCount || (uint)z >= (uint)SampleCount)
            throw new ArgumentOutOfRangeException(nameof(x));
        return (flags[checked(z * SampleCount + x)] & 0x04) != 0;
    }
}

public sealed class TerrainTileContentLoader
{
    public TerrainTileContent Load(IVirtualContentStore store, TerrainTileReference reference)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(reference);

        var terrain = new TerrainTileReader().Read(store, reference.Path);
        var altitude = new TerrainAltitudeReader().Read(store, reference.AltitudePath, terrain.Samples.SampleCount);
        var flags = reference.FlagsPath is null
            ? null
            : new TerrainFlagsReader().Read(store, reference.FlagsPath, terrain.Samples.SampleCount);

        return new TerrainTileContent(reference, terrain, altitude, flags);
    }
}

public sealed record TerrainTileContent(
    TerrainTileReference Reference,
    TerrainTileData Terrain,
    TerrainAltitudeData Altitude,
    TerrainFlagsData? Flags);
