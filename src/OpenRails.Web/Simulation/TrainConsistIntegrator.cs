// COPYRIGHT 2026 by the Open Rails project contributors.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails. If not, see <http://www.gnu.org/licenses/>.

using System;

namespace ORTS.Common
{
    /// <summary>
    /// Longitudinal force inputs for one vehicle in a consist.
    /// Motive and gravity forces are signed; resistance values are magnitudes.
    /// </summary>
    public readonly struct TrainCarForceState
    {
        public double MotiveForceNewtons { get; }
        public double GravityForceNewtons { get; }
        public double FrictionForceNewtons { get; }
        public double BrakeForceNewtons { get; }
        public double CurveForceNewtons { get; }
        public double WindForceNewtons { get; }
        public double TunnelForceNewtons { get; }

        public TrainCarForceState(
            double motiveForceNewtons,
            double gravityForceNewtons,
            double frictionForceNewtons,
            double brakeForceNewtons,
            double curveForceNewtons,
            double windForceNewtons,
            double tunnelForceNewtons)
        {
            MotiveForceNewtons = motiveForceNewtons;
            GravityForceNewtons = gravityForceNewtons;
            FrictionForceNewtons = frictionForceNewtons;
            BrakeForceNewtons = brakeForceNewtons;
            CurveForceNewtons = curveForceNewtons;
            WindForceNewtons = windForceNewtons;
            TunnelForceNewtons = tunnelForceNewtons;
        }
    }

    /// <summary>
    /// Advances a locomotive and its coaches as one temporarily rigid consist.
    /// All vehicles share longitudinal speed and retain their relative spacing.
    /// This is an initial browser-port milestone; slack, draft gear, and
    /// independent coupler forces are not modelled here.
    /// </summary>
    public static class TrainConsistIntegrator
    {
        /// <summary>
        /// Advances all vehicle positions by the same distance using the sum
        /// of each vehicle's longitudinal forces and mass.
        /// The first position is treated as the lead vehicle reference position.
        /// </summary>
        /// <returns>The summed net longitudinal force in newtons.</returns>
        public static double Advance(
            double[] vehiclePositionsMeters,
            double[] vehicleMassesKilograms,
            TrainCarForceState[] vehicleForces,
            ref double consistSpeedMetersPerSecond,
            double elapsedSeconds,
            double maximumSpeedMetersPerSecond)
        {
            if (vehiclePositionsMeters == null)
                throw new ArgumentNullException(nameof(vehiclePositionsMeters));
            if (vehicleMassesKilograms == null)
                throw new ArgumentNullException(nameof(vehicleMassesKilograms));
            if (vehicleForces == null)
                throw new ArgumentNullException(nameof(vehicleForces));

            var count = vehiclePositionsMeters.Length;
            if (count == 0)
                throw new ArgumentException("A consist must contain at least one vehicle.", nameof(vehiclePositionsMeters));
            if (vehicleMassesKilograms.Length != count)
                throw new ArgumentException("Every vehicle must have a mass.", nameof(vehicleMassesKilograms));
            if (vehicleForces.Length != count)
                throw new ArgumentException("Every vehicle must have a force state.", nameof(vehicleForces));

            double totalMass = 0;
            double totalMotiveForce = 0;
            double totalGravityForce = 0;
            double totalFrictionForce = 0;
            double totalBrakeForce = 0;
            double totalCurveForce = 0;
            double totalWindForce = 0;
            double totalTunnelForce = 0;

            for (var i = 0; i < count; i++)
            {
                var mass = vehicleMassesKilograms[i];
                if (!IsFinite(mass) || mass <= 0)
                    throw new ArgumentOutOfRangeException(nameof(vehicleMassesKilograms), "Every vehicle mass must be finite and greater than zero.");
                if (!IsFinite(vehiclePositionsMeters[i]))
                    throw new ArgumentOutOfRangeException(nameof(vehiclePositionsMeters), "Vehicle positions must be finite.");

                totalMass += mass;
                var force = vehicleForces[i];
                totalMotiveForce += force.MotiveForceNewtons;
                totalGravityForce += force.GravityForceNewtons;
                totalFrictionForce += force.FrictionForceNewtons;
                totalBrakeForce += force.BrakeForceNewtons;
                totalCurveForce += force.CurveForceNewtons;
                totalWindForce += force.WindForceNewtons;
                totalTunnelForce += force.TunnelForceNewtons;
            }

            if (!IsFinite(totalMass) || !IsFinite(totalMotiveForce) ||
                !IsFinite(totalGravityForce) || !IsFinite(totalFrictionForce) ||
                !IsFinite(totalBrakeForce) || !IsFinite(totalCurveForce) ||
                !IsFinite(totalWindForce) || !IsFinite(totalTunnelForce))
                throw new ArgumentOutOfRangeException(nameof(vehicleForces), "Summed consist values must be finite.");

            var leadPosition = vehiclePositionsMeters[0];
            var netForce = TrainPhysicsStep.Advance(
                ref leadPosition,
                ref consistSpeedMetersPerSecond,
                totalMotiveForce,
                totalGravityForce,
                totalMass,
                totalFrictionForce,
                totalBrakeForce,
                totalCurveForce,
                totalWindForce,
                totalTunnelForce,
                elapsedSeconds,
                maximumSpeedMetersPerSecond);

            var distanceTravelled = leadPosition - vehiclePositionsMeters[0];
            for (var i = 0; i < count; i++)
                vehiclePositionsMeters[i] += distanceTravelled;

            return netForce;
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
