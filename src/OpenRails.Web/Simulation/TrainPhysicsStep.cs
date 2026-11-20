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
    /// Connects longitudinal force aggregation to one-dimensional train motion.
    /// This is a portable foundation, not the complete upstream Train simulation.
    /// </summary>
    public static class TrainPhysicsStep
    {
        /// <summary>
        /// Calculates net force, applies F = ma, and advances the motion state.
        /// If opposing acceleration would reverse a moving car during this step,
        /// the car is brought to rest at the stopping point instead.
        ///
        /// Static brake holding, breakaway forces, wheel slip, coupler dynamics,
        /// and force recalculation after stopping are outside this helper's scope.
        /// </summary>
        /// <returns>The net longitudinal force in newtons.</returns>
        public static double Advance(
            ref double positionMeters,
            ref double speedMetersPerSecond,
            double motiveForceNewtons,
            double gravityForceNewtons,
            double massKilograms,
            double frictionForceNewtons,
            double brakeForceNewtons,
            double curveForceNewtons,
            double windForceNewtons,
            double tunnelForceNewtons,
            double elapsedSeconds,
            double maximumSpeedMetersPerSecond)
        {
            if (double.IsNaN(massKilograms) || double.IsInfinity(massKilograms) || massKilograms <= 0)
                throw new ArgumentOutOfRangeException(nameof(massKilograms), "Mass must be finite and greater than zero.");

            var netForce = TrainForceCalculator.CalculateNetForce(
                motiveForceNewtons,
                gravityForceNewtons,
                speedMetersPerSecond,
                frictionForceNewtons,
                brakeForceNewtons,
                curveForceNewtons,
                windForceNewtons,
                tunnelForceNewtons);

            var acceleration = netForce / massKilograms;

            if (maximumSpeedMetersPerSecond < 0 ||
                double.IsNaN(maximumSpeedMetersPerSecond) ||
                double.IsInfinity(maximumSpeedMetersPerSecond))
                throw new ArgumentOutOfRangeException(nameof(maximumSpeedMetersPerSecond));

            var limitedSpeed = Math.Clamp(
                speedMetersPerSecond,
                -maximumSpeedMetersPerSecond,
                maximumSpeedMetersPerSecond);

            // A constant opposing force must bring the car to rest, not make it
            // reverse direction simply because the integration interval is longer.
            if (elapsedSeconds > 0 &&
                limitedSpeed != 0 &&
                acceleration != 0 &&
                Math.Sign(limitedSpeed) != Math.Sign(acceleration))
            {
                var stoppingTime = -limitedSpeed / acceleration;
                if (stoppingTime > 0 && stoppingTime <= elapsedSeconds)
                {
                    TrainMotionIntegrator.Step(
                        ref positionMeters,
                        ref speedMetersPerSecond,
                        acceleration,
                        stoppingTime,
                        maximumSpeedMetersPerSecond);
                    speedMetersPerSecond = 0;
                    return netForce;
                }
            }

            TrainMotionIntegrator.Step(
                ref positionMeters,
                ref speedMetersPerSecond,
                acceleration,
                elapsedSeconds,
                maximumSpeedMetersPerSecond);

            return netForce;
        }
    }
}
