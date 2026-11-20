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
    /// Result of splitting a requested locomotive brake effort between
    /// dynamic braking and friction braking. Forces are non-negative magnitudes.
    /// </summary>
    public readonly struct BrakeEffort
    {
        public double DynamicBrakeForceNewtons { get; }
        public double FrictionBrakeForceNewtons { get; }

        public BrakeEffort(double dynamicBrakeForceNewtons, double frictionBrakeForceNewtons)
        {
            DynamicBrakeForceNewtons = dynamicBrakeForceNewtons;
            FrictionBrakeForceNewtons = frictionBrakeForceNewtons;
        }
    }

    /// <summary>
    /// Portable effort limits for a locomotive. Uses the power relationship
    /// force = power / speed and separates dynamic from friction brake effort.
    /// This is a foundation, not the complete Open Rails locomotive subsystem.
    /// </summary>
    public static class LocomotiveEffortCalculator
    {
        /// <summary>
        /// Calculates requested tractive effort, limited by both maximum
        /// tractive force and available traction power.
        /// </summary>
        public static double CalculateTractiveForce(
            double throttleFraction,
            double maximumTractiveForceNewtons,
            double availableTractionPowerWatts,
            double speedMetersPerSecond)
        {
            ValidateFraction(throttleFraction, nameof(throttleFraction));
            ValidateNonNegative(maximumTractiveForceNewtons, nameof(maximumTractiveForceNewtons));
            ValidateNonNegative(availableTractionPowerWatts, nameof(availableTractionPowerWatts));
            ValidateFinite(speedMetersPerSecond, nameof(speedMetersPerSecond));

            var requestedForce = throttleFraction * maximumTractiveForceNewtons;
            var speed = Math.Abs(speedMetersPerSecond);

            // At standstill, the power limit does not constrain force because
            // mechanical power is zero at zero speed.
            if (speed == 0 || availableTractionPowerWatts == 0)
                return speed == 0 ? requestedForce : 0;

            return Math.Min(requestedForce, availableTractionPowerWatts / speed);
        }

        /// <summary>
        /// Splits requested brake effort between dynamic braking (prioritized
        /// up to its force and power limits) and friction braking for the remainder.
        /// </summary>
        public static BrakeEffort CalculateBrakeEffort(
            double brakeFraction,
            double maximumDynamicBrakeForceNewtons,
            double maximumDynamicBrakePowerWatts,
            double maximumFrictionBrakeForceNewtons,
            double speedMetersPerSecond)
        {
            ValidateFraction(brakeFraction, nameof(brakeFraction));
            ValidateNonNegative(maximumDynamicBrakeForceNewtons, nameof(maximumDynamicBrakeForceNewtons));
            ValidateNonNegative(maximumDynamicBrakePowerWatts, nameof(maximumDynamicBrakePowerWatts));
            ValidateNonNegative(maximumFrictionBrakeForceNewtons, nameof(maximumFrictionBrakeForceNewtons));
            ValidateFinite(speedMetersPerSecond, nameof(speedMetersPerSecond));

            var requestedTotal = brakeFraction *
                (maximumDynamicBrakeForceNewtons + maximumFrictionBrakeForceNewtons);
            var speed = Math.Abs(speedMetersPerSecond);

            var dynamicLimit = maximumDynamicBrakeForceNewtons;
            if (speed > 0)
                dynamicLimit = Math.Min(dynamicLimit, maximumDynamicBrakePowerWatts / speed);
            else if (maximumDynamicBrakePowerWatts == 0)
                dynamicLimit = 0;

            var dynamicForce = Math.Min(requestedTotal, dynamicLimit);
            var frictionForce = Math.Min(
                Math.Max(0, requestedTotal - dynamicForce),
                maximumFrictionBrakeForceNewtons);

            return new BrakeEffort(dynamicForce, frictionForce);
        }

        private static void ValidateFraction(double value, string name)
        {
            ValidateFinite(value, name);
            if (value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(name, "Control fractions must be between zero and one.");
        }

        private static void ValidateNonNegative(double value, string name)
        {
            ValidateFinite(value, name);
            if (value < 0)
                throw new ArgumentOutOfRangeException(name, "Effort and power limits cannot be negative.");
        }

        private static void ValidateFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "Values must be finite.");
        }
    }
}
