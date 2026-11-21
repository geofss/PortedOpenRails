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
    /// Combines longitudinal train forces using the direction convention in
    /// Open Rails Train.ComputeCouplerForces. Resistance inputs are magnitudes
    /// in newtons; motive and gravity forces are signed.
    /// </summary>
    public static class TrainForceCalculator
    {
        /// <summary>
        /// Calculates the net longitudinal force for a moving car.
        /// Resistance opposes the current direction of travel. At exactly zero
        /// speed this method leaves the base force unchanged; standstill brake
        /// holding and breakaway behavior are handled by other simulator logic.
        /// </summary>
        public static double CalculateNetForce(
            double motiveForceNewtons,
            double gravityForceNewtons,
            double speedMetersPerSecond,
            double frictionForceNewtons,
            double brakeForceNewtons,
            double curveForceNewtons,
            double windForceNewtons,
            double tunnelForceNewtons)
        {
            ValidateFinite(motiveForceNewtons, nameof(motiveForceNewtons));
            ValidateFinite(gravityForceNewtons, nameof(gravityForceNewtons));
            ValidateFinite(speedMetersPerSecond, nameof(speedMetersPerSecond));
            ValidateResistance(frictionForceNewtons, nameof(frictionForceNewtons));
            ValidateResistance(brakeForceNewtons, nameof(brakeForceNewtons));
            ValidateResistance(curveForceNewtons, nameof(curveForceNewtons));
            ValidateResistance(windForceNewtons, nameof(windForceNewtons));
            ValidateResistance(tunnelForceNewtons, nameof(tunnelForceNewtons));

            var netForce = motiveForceNewtons + gravityForceNewtons;
            var totalResistance =
                frictionForceNewtons +
                brakeForceNewtons +
                curveForceNewtons +
                windForceNewtons +
                tunnelForceNewtons;

            if (speedMetersPerSecond > 0)
                netForce -= totalResistance;
            else if (speedMetersPerSecond < 0)
                netForce += totalResistance;

            return netForce;
        }

        private static void ValidateResistance(double value, string parameterName)
        {
            ValidateFinite(value, parameterName);
            if (value < 0)
                throw new ArgumentOutOfRangeException(parameterName, "Resistance magnitudes cannot be negative.");
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "Force and speed values must be finite.");
        }
    }
}
