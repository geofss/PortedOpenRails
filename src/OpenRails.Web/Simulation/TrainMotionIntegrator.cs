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
    /// Portable constant-acceleration integration for a one-dimensional train motion state.
    /// Uses metres, seconds, and metres per second. This is a browser-side foundation,
    /// not a replacement for the full upstream Train force and resistance model.
    /// </summary>
    public static class TrainMotionIntegrator
    {
        /// <summary>
        /// Advances position and velocity by one time step, enforcing a symmetric speed limit.
        /// Acceleration is assumed constant during the step.
        /// </summary>
        public static void Step(
            ref double positionMeters,
            ref double speedMetersPerSecond,
            double accelerationMetersPerSecondSquared,
            double elapsedSeconds,
            double maximumSpeedMetersPerSecond)
        {
            if (!IsFinite(positionMeters))
                throw new ArgumentOutOfRangeException(nameof(positionMeters));
            if (!IsFinite(speedMetersPerSecond))
                throw new ArgumentOutOfRangeException(nameof(speedMetersPerSecond));
            if (!IsFinite(accelerationMetersPerSecondSquared))
                throw new ArgumentOutOfRangeException(nameof(accelerationMetersPerSecondSquared));
            if (!IsFinite(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (!IsFinite(maximumSpeedMetersPerSecond) || maximumSpeedMetersPerSecond < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumSpeedMetersPerSecond));

            var limit = maximumSpeedMetersPerSecond;
            var initialSpeed = Math.Clamp(speedMetersPerSecond, -limit, limit);

            if (elapsedSeconds == 0 || limit == 0)
            {
                speedMetersPerSecond = limit == 0 ? 0 : initialSpeed;
                return;
            }

            var unconstrainedFinalSpeed = initialSpeed + accelerationMetersPerSecondSquared * elapsedSeconds;
            var finalSpeed = Math.Clamp(unconstrainedFinalSpeed, -limit, limit);

            // If the speed limit is reached during this step, integrate acceleration only
            // until that instant, then continue at the limited speed for the remaining time.
            var timeToFinalSpeed = elapsedSeconds;
            if (accelerationMetersPerSecondSquared != 0 &&
                finalSpeed != unconstrainedFinalSpeed)
            {
                timeToFinalSpeed = Math.Clamp(
                    (finalSpeed - initialSpeed) / accelerationMetersPerSecondSquared,
                    0,
                    elapsedSeconds);
            }

            var acceleratedDistance =
                initialSpeed * timeToFinalSpeed +
                0.5 * accelerationMetersPerSecondSquared * timeToFinalSpeed * timeToFinalSpeed;
            var limitedDistance = finalSpeed * (elapsedSeconds - timeToFinalSpeed);

            positionMeters += acceleratedDistance + limitedDistance;
            speedMetersPerSecond = finalSpeed;
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
