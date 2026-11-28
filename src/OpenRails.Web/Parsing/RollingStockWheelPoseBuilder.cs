using System.Numerics;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Applies the native Open Rails rolling-stock wheel convention: WHEELS matrix
/// nodes below 3 m are rotated about their local X axis by travelled distance /
/// the declared wheel radius. Animated non-wheel parts remain shape-clip driven.
/// </summary>
public static class RollingStockWheelPoseBuilder
{
    private const float DefaultWheelRadiusMeters = 0.4572f;

    public static IReadOnlyDictionary<int, Matrix4x4> Build(
        TrackShapeMesh mesh,
        RollingStockMetadata? metadata,
        bool isEngine,
        double routeDistanceMeters,
        IReadOnlyDictionary<int, Matrix4x4>? baseOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!double.IsFinite(routeDistanceMeters) || routeDistanceMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(routeDistanceMeters));

        var radius = isEngine
            ? metadata?.DriverWheelRadiusM ?? metadata?.WheelRadiusM ?? DefaultWheelRadiusMeters
            : metadata?.WheelRadiusM ?? DefaultWheelRadiusMeters;
        if (!float.IsFinite(radius) || radius <= 0)
            radius = DefaultWheelRadiusMeters;

        var hierarchy = mesh.Lods.FirstOrDefault()?.Hierarchy;
        if (hierarchy is null || hierarchy.Count == 0)
            return new Dictionary<int, Matrix4x4>();

        var rotation = Matrix4x4.CreateRotationX((float)(-routeDistanceMeters / radius));
        var overrides = baseOverrides is null
            ? new Dictionary<int, Matrix4x4>()
            : new Dictionary<int, Matrix4x4>(baseOverrides);
        var count = Math.Min(mesh.Matrices.Count, hierarchy.Count);
        for (var index = 0; index < count; index++)
        {
            var matrix = mesh.Matrices[index];
            var name = matrix.Name?.Trim().ToUpperInvariant();
            if (name is null || !name.StartsWith("WHEELS", StringComparison.Ordinal) ||
                name.Length is < 7 or > 9)
                continue;
            if (GetMatrixProduct(mesh.Matrices, hierarchy, index).M42 >= 3f)
                continue;
            overrides[index] = rotation * ToOpenRailsMatrix(matrix);
        }
        return overrides;
    }

    private static Matrix4x4 GetMatrixProduct(
        IReadOnlyList<TrackShapeMatrix> matrices,
        IReadOnlyList<int> hierarchy,
        int index)
    {
        var product = Matrix4x4.Identity;
        var visited = new HashSet<int>();
        var node = index;
        while (node >= 0)
        {
            if ((uint)node >= (uint)matrices.Count || (uint)node >= (uint)hierarchy.Count ||
                !visited.Add(node))
                return Matrix4x4.Identity;
            product *= ToOpenRailsMatrix(matrices[node]);
            node = hierarchy[node];
        }
        return product;
    }

    private static Matrix4x4 ToOpenRailsMatrix(TrackShapeMatrix matrix) => new(
        matrix.AX, matrix.AY, -matrix.AZ, 0,
        matrix.BX, matrix.BY, -matrix.BZ, 0,
        -matrix.CX, -matrix.CY, matrix.CZ, 0,
        matrix.DX, matrix.DY, -matrix.DZ, 1);
}
