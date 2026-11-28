using System.Numerics;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Applies native bogie pivot yaw by sampling the connected route tangent at
/// each BOGIE/BOGIE1/BOGIE2 matrix's longitudinal offset from the vehicle center.
/// </summary>
public static class RollingStockBogiePoseBuilder
{
    public static IReadOnlyDictionary<int, Matrix4x4> Build(
        TrackShapeMesh mesh,
        IReadOnlyList<TrackWorldPosition> centerline,
        IReadOnlyList<double> cumulativeDistances,
        double routeDistanceMeters,
        double vehicleHeadingRadians,
        bool flipped = false)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(centerline);
        ArgumentNullException.ThrowIfNull(cumulativeDistances);
        if (!double.IsFinite(routeDistanceMeters) || routeDistanceMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(routeDistanceMeters));
        if (!double.IsFinite(vehicleHeadingRadians))
            throw new ArgumentOutOfRangeException(nameof(vehicleHeadingRadians));
        if (centerline.Count < 2 || cumulativeDistances.Count != centerline.Count)
            return new Dictionary<int, Matrix4x4>();

        var overrides = new Dictionary<int, Matrix4x4>();
        for (var index = 0; index < mesh.Matrices.Count; index++)
        {
            var matrix = mesh.Matrices[index];
            var name = matrix.Name?.Trim().ToUpperInvariant();
            if (name is null || !name.StartsWith("BOGIE", StringComparison.Ordinal) || name.Length > 6)
                continue;

            var localLongitudinalOffset = -matrix.DZ;
            if (flipped) localLongitudinalOffset = -localLongitudinalOffset;
            if (Math.Abs(localLongitudinalOffset) < 0.01)
                continue;

            var bogieDistance = Math.Clamp(routeDistanceMeters + localLongitudinalOffset,
                cumulativeDistances[0], cumulativeDistances[^1]);
            var bogieHeading = HeadingAt(centerline, cumulativeDistances, bogieDistance);
            var delta = NormalizeAngle(bogieHeading - vehicleHeadingRadians);
            if (Math.Abs(delta) < 0.0001)
                continue;

            var rotation = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, (float)delta);
            overrides[index] = rotation * ToOpenRailsMatrix(matrix);
        }
        return overrides;
    }

    private static double HeadingAt(
        IReadOnlyList<TrackWorldPosition> points,
        IReadOnlyList<double> cumulative,
        double distance)
    {
        var index = 0;
        while (index < cumulative.Count - 2 && cumulative[index + 1] <= distance)
            index++;
        var a = points[index];
        var b = points[index + 1];
        return Math.Atan2(b.X - a.X, b.Z - a.Z);
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI) angle -= Math.PI * 2;
        while (angle < -Math.PI) angle += Math.PI * 2;
        return angle;
    }

    private static Matrix4x4 ToOpenRailsMatrix(TrackShapeMatrix matrix) => new(
        matrix.AX, matrix.AY, -matrix.AZ, 0,
        matrix.BX, matrix.BY, -matrix.BZ, 0,
        -matrix.CX, -matrix.CY, matrix.CZ, 0,
        matrix.DX, matrix.DY, -matrix.DZ, 1);
}
