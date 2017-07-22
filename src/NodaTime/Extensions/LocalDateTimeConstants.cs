using System;

namespace NodaTime.Extensions
{
    /// <summary>
    /// </summary>
    public static class LocalDateTimeConstants
    {
        /// <summary>
        /// Get the Mininum value a <see cref="LocalDateTime"/> can be.
        /// </summary>
        public static readonly LocalDateTime MinValue = LocalDateTime.FromDateTime(DateTime.MinValue);

        /// <summary>
        /// Get the Maximum value a <see cref="LocalDateTime"/> can be.
        /// </summary>
        public static readonly LocalDateTime MaxValue = LocalDateTime.FromDateTime(DateTime.MaxValue);
    }
}
