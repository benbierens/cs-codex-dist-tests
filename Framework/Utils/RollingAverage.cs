﻿namespace Utils
{
    public static class RollingAverage
    {
        /// <param name="currentAverage">Value of average before new value is added.</param>
        /// <param name="newNumberOfValues">Number of values in average after new value is added.</param>
        /// <param name="newValue">New value to be added.</param>
        /// <returns>New average value.</returns>
        /// <exception cref="Exception">newNumberOfValues must be 1 or greater.</exception>
        public static float GetNewAverage(float currentAverage, int newNumberOfValues, float newValue)
        {
            if (newNumberOfValues < 1) throw new Exception("Should be at least 1 value.");

            float n = newNumberOfValues;
            var originalValue = currentAverage;
            var originalValueWeight = ((n - 1.0f) / n);
            var newValueWeight = (1.0f / n);
            return (originalValue * originalValueWeight) + (newValue * newValueWeight);
        }
    }
}
