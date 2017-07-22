using System;

namespace NodaTime.Extensions
{
    /// <summary>
    /// </summary>
    public static class NodaTimeExtensions
    {
        #region Instance

        #endregion

        #region ZoneDateTime

        /// <summary>
        /// Returns the <see cref="ZonedDateTime"/> representing the same point in time as this instant, in the UTC time
        /// zone and ISO-8601 calendar. This is a shortcut for calling <see cref="InZone" /> with an
        /// argument of <see cref="DateTimeZone.Utc"/>.
        /// </summary>
        /// <returns>A <see cref="ZonedDateTime"/> for the same instant, in the UTC time zone
        /// and the ISO-8601 calendar</returns>
        public static ZonedDateTime InUtc(this ZonedDateTime source)
        {
            return source.ToInstant().InUtc();
        }

        /// <summary>
        /// If needed creates a new <see cref="ZonedDateTime"/> representing the same instant in time, in the
        /// same calendar but in the time zone specified by <paramref name="targetZone"/>.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="targetZone">The target time zone to convert to.</param>
        /// <returns>A new value in the target time zone.</returns>
        public static ZonedDateTime InZone(this ZonedDateTime source, DateTimeZone targetZone)
        {
            return Equals(source.Zone, targetZone) ? source : source.WithZone(targetZone);
        }

        /// <summary>
        /// Returns the <see cref="ZonedDateTime"/> as a short date time format.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>a string</returns>
        public static string ToShortDateTimeString(this ZonedDateTime source)
        {
            return source.ToDateTimeOffset().ToString("g");
        }

        /// <summary>
        /// Replace the date portion of a <see cref="ZonedDateTime"/> object
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="date">The new date</param>
        /// <returns></returns>
        public static ZonedDateTime SetDate(this ZonedDateTime source, LocalDate date)
        {
            return source.Set(date.Year, date.Month, date.Day);
        }

        /// <summary>
        /// Replace the time portion of a <see cref="ZonedDateTime"/> object
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="time">The new time</param>
        /// <returns></returns>
        public static ZonedDateTime SetTime(this ZonedDateTime source, LocalTime time)
        {
            return source.Set(hour: time.Hour, minute: time.Minute, second: time.Second, millisecond: time.Millisecond);
        }

        /// <summary>
        /// Replace the year portion of a <see cref="ZonedDateTime"/> object
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="year">The new year</param>
        /// <returns></returns>
        public static ZonedDateTime SetYear(this ZonedDateTime source, int year)
        {
            return source.Set(year: year);
        }

        /// <summary>
        /// Replace the month portion of a <see cref="ZonedDateTime"/> object
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="month">The new month</param>
        /// <returns></returns>
        public static ZonedDateTime SetMonth(this ZonedDateTime source, int month)
        {
            return source.Set(month: month);
        }

        /// <summary>
        /// Replace the day portion of a <see cref="ZonedDateTime"/> object
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="day">The new day</param>
        /// <returns></returns>
        public static ZonedDateTime SetDay(this ZonedDateTime source, int day)
        {
            return source.Set(day: day);
        }

        /// <summary>
        /// Replace the hour portion of a <see cref="ZonedDateTime"/> object
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="hour">The new hour</param>
        /// <returns></returns>
        public static ZonedDateTime SetHour(this ZonedDateTime source, int hour)
        {
            return source.Set(hour: hour);
        }

        /// <summary>
        /// Replace the minute portion of a <see cref="ZonedDateTime"/> object
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="minute">The new minute</param>
        /// <returns></returns>
        public static ZonedDateTime SetMinute(this ZonedDateTime source, int minute)
        {
            return source.Set(minute: minute);
        }

        /// <summary>
        /// Replace the second portion of a <see cref="ZonedDateTime"/> object
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="second">The new second</param>
        /// <returns></returns>
        public static ZonedDateTime SetSecond(this ZonedDateTime source, int second)
        {
            return source.Set(second: second);
        }

        /// <summary>
        /// Replace the millisecond portion of a <see cref="ZonedDateTime"/> object
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="millisecond">The new millisecond</param>
        /// <returns></returns>
        public static ZonedDateTime SetMillisecond(this ZonedDateTime source, int millisecond)
        {
            return source.Set(millisecond: millisecond);
        }

        /// <summary>
        /// Replace one or more parts of a <see cref="ZonedDateTime"/> object
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="year">The new year</param>
        /// <param name="month">The new month</param>
        /// <param name="day">The new day</param>
        /// <param name="hour">The new hour</param>
        /// <param name="minute">The new minute</param>
        /// <param name="second">The new second</param>
        /// <param name="millisecond">The new millisecond</param>
        /// <returns></returns>
        public static ZonedDateTime Set(this ZonedDateTime source, int year = -1, int month = -1, int day = -1, int hour = -1, int minute = -1, int second = -1, int millisecond = -1)
        {
            var localDateTime = source.LocalDateTime.Set(year, month, day, hour, minute, second, millisecond);
            try
            {
                var zoneDateTime = localDateTime.InZoneStrictly(source.Zone);
                return new ZonedDateTime(localDateTime, source.Zone, source.Offset);
            }
            catch (Exception e)
            {
                var m = e.Message;
                throw;
            }
        }

        #endregion

        #region LocalDateTime

        /// <summary>
        /// </summary>
        public static LocalDate FirstDayOfMonth(this LocalDate value)
        {
            return new LocalDate(value.Year, value.Month, 1);
        }

        /// <summary>
        /// </summary>
        public static int DaysInMonth(this LocalDate value)
        {
            return DateTime.DaysInMonth(value.Year, value.Month);
        }

        /// <summary>
        /// </summary>
        public static LocalDate LastDayOfMonth(this LocalDate value)
        {
            return new LocalDate(value.Year, value.Month, value.DaysInMonth());
        }

        /// <summary>
        /// </summary>
        public static LocalDateTime SetDate(this LocalDateTime source, LocalDate date)
        {
            return source.Set(date.Year, date.Month, date.Day);
        }

        /// <summary>
        /// </summary>
        public static LocalDateTime SetTime(this LocalDateTime source, LocalTime time)
        {
            return source.Set(hour: time.Hour, minute: time.Minute, second: time.Second, millisecond: time.Millisecond);
        }

        /// <summary>
        /// </summary>
        public static LocalDateTime SetYear(this LocalDateTime source, int year)
        {
            return source.Set(year: year);
        }

        /// <summary>
        /// </summary>
        public static LocalDateTime SetMonth(this LocalDateTime source, int month)
        {
            return source.Set(month: month);
        }

        /// <summary>
        /// </summary>
        public static LocalDateTime SetDay(this LocalDateTime source, int day)
        {
            return source.Set(day: day);
        }

        /// <summary>
        /// </summary>
        public static LocalDateTime SetHour(this LocalDateTime source, int hour)
        {
            return source.Set(hour: hour);
        }

        /// <summary>
        /// </summary>
        public static LocalDateTime SetMinute(this LocalDateTime source, int minute)
        {
            return source.Set(minute: minute);
        }

        /// <summary>
        /// </summary>
        public static LocalDateTime SetSecond(this LocalDateTime source, int second)
        {
            return source.Set(second: second);
        }

        /// <summary>
        /// </summary>
        public static LocalDateTime SetMillisecond(this LocalDateTime source, int millisecond)
        {
            return source.Set(millisecond: millisecond);
        }

        /// <summary>
        /// </summary>
        public static LocalDateTime Set(this LocalDateTime source, int year = -1, int month = -1, int day = -1, int hour = -1, int minute = -1, int second = -1, int millisecond = -1)
        {
            return new LocalDateTime(
                year != -1 ? year : source.Year,
                month != -1 ? month : source.Month,
                day != -1 ? day : source.Day,
                hour != -1 ? hour : source.Hour,
                minute != -1 ? minute : source.Minute,
                second != -1 ? second : source.Second,
                millisecond != -1 ? millisecond : source.Millisecond);
        }

        #endregion

        #region LocalDate


        #endregion

        #region LocalTime

        /// <summary>
        /// </summary>
        public static Period ToPeriod(this LocalTime source)
        {
            return Period.FromTicks(source.TickOfDay);
        }

        /// <summary>
        /// </summary>
        public static Duration ToDuration(this LocalTime source)
        {
            return Duration.FromTicks(source.TickOfDay);
        }

        /// <summary>
        /// </summary>
        public static LocalTime SetHour(this LocalTime source, int hour)
        {
            return source.Set(hour: hour);
        }

        /// <summary>
        /// </summary>
        public static LocalTime SetMinute(this LocalTime source, int minute)
        {
            return source.Set(minute: minute);
        }

        /// <summary>
        /// </summary>
        public static LocalTime SetSecond(this LocalTime source, int second)
        {
            return source.Set(second: second);
        }

        /// <summary>
        /// </summary>
        public static LocalTime SetMillisecond(this LocalTime source, int millisecond)
        {
            return source.Set(millisecond: millisecond);
        }

        /// <summary>
        /// </summary>
        public static LocalTime Set(this LocalTime source, int hour = -1, int minute = -1, int second = -1, int millisecond = -1)
        {
            return new LocalTime(
                hour != -1 ? hour : source.Hour,
                minute != -1 ? minute : source.Minute,
                second != -1 ? second : source.Second,
                millisecond != -1 ? millisecond : source.Millisecond);
        }

        #endregion

        #region Period


        #endregion

        #region Duration

        /// <summary>
        /// </summary>
        public static Period ToPeriod(this Duration source)
        {
            return Period.FromTicks(source.Ticks).Normalize();
        }

        #endregion

        #region DateTime

        /// <summary>
        /// </summary>
        public static ZonedDateTime ToZonedDateTime(this DateTime source, DateTimeZone zone)
        {
            return LocalDateTime.FromDateTime(source).InZoneStrictly(zone);
        }

        /// <summary>
        /// </summary>
        public static LocalDate ToLocalDate(this DateTime source)
        {
            return new LocalDate(source.Year, source.Month, source.Day);
        }

        #endregion

        #region TimeSpan

        /// <summary>
        /// </summary>
        public static Period ToPeriod(this TimeSpan source)
        {
            return Period.FromTicks(source.Ticks);
        }

        /// <summary>
        /// </summary>
        public static Duration ToDuration(this TimeSpan source)
        {
            return Duration.FromTimeSpan(source);
        }

        #endregion
    }
}
