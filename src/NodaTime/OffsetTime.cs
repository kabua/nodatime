﻿// Copyright 2017 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using JetBrains.Annotations;
using NodaTime.Annotations;
using NodaTime.Text;
using NodaTime.Utility;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using static NodaTime.NodaConstants;

namespace NodaTime
{
    /// <summary>
    /// A combination of a <see cref="LocalTime"/> and an <see cref="Offset"/>, to represent
    /// a time-of-day at a specific offset from UTC but without any date information.
    /// </summary>
    /// <threadsafety>This type is an immutable value type. See the thread safety section of the user guide for more information.</threadsafety>
    public readonly struct OffsetTime : IEquatable<OffsetTime>, IXmlSerializable, IFormattable
    {
        private const int NanosecondsBits = 47;
        private const long NanosecondsMask = (1L << NanosecondsBits) - 1;

        // Bottom NanosecondsBits bits are the nanosecond-of-day; top 17 bits are the offset (in seconds). This has a slight
        // execution-time cost (masking for each component) but the logical benefit of saving 4 bytes per
        // value actually ends up being 8 bytes per value on a 64-bit CLR due to alignment.
        private readonly long nanosecondsAndOffset;

        // Constructor only used in specialist cases where we know the offset will be 0.
        internal OffsetTime([Trusted] long nanosecondOfDayZeroOffset)
        {
            Preconditions.DebugCheckArgument((nanosecondOfDayZeroOffset & ~NanosecondsMask) == 0, nameof(nanosecondsAndOffset),
                "Constructor with zero offset called with non-zero offset");
            nanosecondsAndOffset = nanosecondOfDayZeroOffset;
        }

        internal OffsetTime([Trusted] long nanosecondOfDay, [Trusted] int offsetSeconds) =>
            nanosecondsAndOffset = nanosecondOfDay | (((long)offsetSeconds) << NanosecondsBits);

        /// <summary>
        /// Constructs an instance of the specified time and offset.
        /// </summary>
        /// <param name="time">The time part of the value.</param>
        /// <param name="offset">The offset part of the value.</param>
        public OffsetTime(LocalTime time, Offset offset) : this(time.NanosecondOfDay, offset.Seconds)
        {
        }

        /// <summary>
        /// Gets the time-of-day represented by this value.
        /// </summary>
        /// <value>The time-of-day represented by this value.</value>
        public LocalTime TimeOfDay => new LocalTime(NanosecondOfDay);

        /// <summary>
        /// Gets the offset from UTC of this value.
        /// <value>The offset from UTC of this value.</value>
        /// </summary>
        public Offset Offset => new Offset((int)(nanosecondsAndOffset >> NanosecondsBits));

        /// <summary>
        /// Returns the number of seconds in the offset, without going via an Offset.
        /// </summary>
        internal int OffsetSeconds => (int)(nanosecondsAndOffset >> NanosecondsBits);

        /// <summary>
        /// Returns the number of nanoseconds in the offset, without going via an Offset.
        /// </summary>
        internal long OffsetNanoseconds => unchecked(nanosecondsAndOffset >> NanosecondsBits) * NanosecondsPerSecond;

        /// <summary>
        /// Gets the hour of day of this offset time, in the range 0 to 23 inclusive.
        /// </summary>
        /// <value>The hour of day of this offset time, in the range 0 to 23 inclusive.</value>
        public int Hour =>
            // Effectively nanoseconds / NanosecondsPerHour, but apparently rather more efficient.
            (int)((NanosecondOfDay >> 13) / 439453125);

        /// <summary>
        /// Gets the hour of the half-day of this offset time, in the range 1 to 12 inclusive.
        /// </summary>
        /// <value>The hour of the half-day of this offset time, in the range 1 to 12 inclusive.</value>
        public int ClockHourOfHalfDay
        {
            get
            {
                unchecked
                {
                    int hourOfHalfDay = HourOfHalfDay;
                    return hourOfHalfDay == 0 ? 12 : hourOfHalfDay;
                }
            }
        }

        // TODO(feature): Consider exposing this.
        /// <summary>
        /// Gets the hour of the half-day of this offset time, in the range 0 to 11 inclusive.
        /// </summary>
        /// <value>The hour of the half-day of this offset time, in the range 0 to 11 inclusive.</value>
        internal int HourOfHalfDay => unchecked(Hour % 12);

        /// <summary>
        /// Gets the minute of this offset time, in the range 0 to 59 inclusive.
        /// </summary>
        /// <value>The minute of this offset time, in the range 0 to 59 inclusive.</value>
        public int Minute
        {
            get
            {
                unchecked
                {
                    // Effectively NanosecondOfDay / NanosecondsPerMinute, but apparently rather more efficient.
                    int minuteOfDay = (int)((NanosecondOfDay >> 11) / 29296875);
                    return minuteOfDay % MinutesPerHour;
                }
            }
        }

        /// <summary>
        /// Gets the second of this offset time within the minute, in the range 0 to 59 inclusive.
        /// </summary>
        /// <value>The second of this offset time within the minute, in the range 0 to 59 inclusive.</value>
        public int Second
        {
            get
            {
                unchecked
                {
                    int secondOfDay = (int)(NanosecondOfDay / (int)NanosecondsPerSecond);
                    return secondOfDay % SecondsPerMinute;
                }
            }
        }

        /// <summary>
        /// Gets the millisecond of this offset time within the second, in the range 0 to 999 inclusive.
        /// </summary>
        /// <value>The millisecond of this offset time within the second, in the range 0 to 999 inclusive.</value>
        public int Millisecond
        {
            get
            {
                unchecked
                {
                    long milliSecondOfDay = (NanosecondOfDay / (int)NanosecondsPerMillisecond);
                    return (int) (milliSecondOfDay % MillisecondsPerSecond);
                }
            }
        }

        /// <summary>
        /// Gets the tick of this offset time within the second, in the range 0 to 9,999,999 inclusive.
        /// </summary>
        /// <value>The tick of this offset time within the second, in the range 0 to 9,999,999 inclusive.</value>
        public int TickOfSecond => unchecked((int)(TickOfDay % (int)TicksPerSecond));

        /// <summary>
        /// Gets the tick of this offset time within the day, in the range 0 to 863,999,999,999 inclusive.
        /// </summary>
        /// <remarks>
        /// If the value does not fall on a tick boundary, it will be truncated towards zero.
        /// </remarks>
        /// <value>The tick of this offset time within the day, in the range 0 to 863,999,999,999 inclusive.</value>
        public long TickOfDay => NanosecondOfDay / NanosecondsPerTick;

        /// <summary>
        /// Gets the nanosecond of this offset time within the second, in the range 0 to 999,999,999 inclusive.
        /// </summary>
        /// <value>The nanosecond of this offset time within the second, in the range 0 to 999,999,999 inclusive.</value>
        public int NanosecondOfSecond => unchecked((int) (NanosecondOfDay % NanosecondsPerSecond));

        /// <summary>
        /// Gets the nanosecond of this offset time within the day, in the range 0 to 86,399,999,999,999 inclusive.
        /// </summary>
        /// <value>The nanosecond of this offset time within the day, in the range 0 to 86,399,999,999,999 inclusive.</value>
        public long NanosecondOfDay => nanosecondsAndOffset & NanosecondsMask;

        /// <summary>
        /// Creates a new <see cref="OffsetTime"/> for the same time-of-day, but with the specified UTC offset.
        /// </summary>
        /// <param name="offset">The new UTC offset.</param>
        /// <returns>A new <c>OffsetTime</c> for the same date, but with the specified UTC offset.</returns>
        [Pure]
        public OffsetTime WithOffset(Offset offset) => new OffsetTime(TimeOfDay, offset); // TODO: Consider using bitmasking for nanos instead.

        /// <summary>
        /// Returns this offset time-of-day, with the given date adjuster applied to it, maintaining the existing offset.
        /// </summary>
        /// <remarks>
        /// If the adjuster attempts to construct an invalid time-of-day, any exception thrown by
        /// that construction attempt will be propagated through this method.
        /// </remarks>
        /// <param name="adjuster">The adjuster to apply.</param>
        /// <returns>The adjusted offset date.</returns>
        [Pure]
        public OffsetTime With([NotNull] Func<LocalTime, LocalTime> adjuster) =>
            new OffsetTime(TimeOfDay.With(adjuster), Offset);

        /// <summary>
        /// Combines this <see cref="OffsetTime"/> with the given <see cref="LocalDate"/>
        /// into an <see cref="OffsetDateTime"/>.
        /// </summary>
        /// <param name="date">The date to combine with this time-of-day.</param>
        /// <returns>The <see cref="OffsetDateTime"/> representation of this time-of-day on the given date.</returns>
        [Pure]
        public OffsetDateTime On(LocalDate date) => new OffsetDateTime(date.At(TimeOfDay), Offset);

        /// <summary>
        /// Returns a hash code for this offset time.
        /// </summary>
        /// <returns>A hash code for this offset time.</returns>
        public override int GetHashCode() => HashCodeHelper.Hash(TimeOfDay, Offset);

        /// <summary>
        /// Compares two <see cref="OffsetTime"/> values for equality. This requires
        /// that the time-of-day values be the same and the offsets.
        /// </summary>
        /// <param name="obj">The object to compare this offset time with.</param>
        /// <returns>True if the given value is another offset time equal to this one; false otherwise.</returns>
        public override bool Equals(object obj) => obj is OffsetTime other && Equals(other);

        /// <summary>
        /// Compares two <see cref="OffsetTime"/> values for equality. This requires
        /// that the date values be the same and the offsets.
        /// </summary>
        /// <param name="other">The value to compare this offset time with.</param>
        /// <returns>True if the given value is another offset time equal to this one; false otherwise.</returns>
        public bool Equals(OffsetTime other) => TimeOfDay == other.TimeOfDay && Offset == other.Offset;

        /// <summary>
        /// Implements the operator == (equality).
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns><c>true</c> if values are equal to each other, otherwise <c>false</c>.</returns>
        public static bool operator ==(OffsetTime left, OffsetTime right) => left.Equals(right);

        /// <summary>
        /// Implements the operator != (inequality).
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns><c>true</c> if values are not equal to each other, otherwise <c>false</c>.</returns>
        public static bool operator !=(OffsetTime left, OffsetTime right) => !(left == right);

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// The value of the current instance in the default format pattern ("G"), using the current thread's
        /// culture to obtain a format provider.
        /// </returns>
        public override string ToString() => OffsetTimePattern.Patterns.BclSupport.Format(this, null, CultureInfo.CurrentCulture);

        /// <summary>
        /// Formats the value of the current instance using the specified pattern.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String" /> containing the value of the current instance in the specified format.
        /// </returns>
        /// <param name="patternText">The <see cref="T:System.String" /> specifying the pattern to use,
        /// or null to use the default format pattern ("G").
        /// </param>
        /// <param name="formatProvider">The <see cref="T:System.IFormatProvider" /> to use when formatting the value,
        /// or null to use the current thread's culture to obtain a format provider.
        /// </param>
        /// <filterpriority>2</filterpriority>
        public string ToString(string patternText, IFormatProvider formatProvider) =>
            OffsetTimePattern.Patterns.BclSupport.Format(this, patternText, formatProvider);


        ///<summary>
        /// Deconstruct this value into its components.
        /// </summary>
        /// <param name="localTime">
        /// The <see cref="LocalTime"/> component.
        /// </param>
        /// <param name="offset">
        /// The <see cref="Offset"/> component.
        /// </param>
        [Pure]
        public void Deconstruct(out LocalTime localTime, out Offset offset)
        {
            localTime = TimeOfDay;
            offset = Offset;
        }
        #region XML serialization
        /// <inheritdoc />
        XmlSchema IXmlSerializable.GetSchema() => null;

        /// <inheritdoc />
        void IXmlSerializable.ReadXml([NotNull] XmlReader reader)
        {
            Preconditions.CheckNotNull(reader, nameof(reader));
            string text = reader.ReadElementContentAsString();
            Unsafe.AsRef(this) = OffsetTimePattern.ExtendedIso.Parse(text).Value;
        }

        /// <inheritdoc />
        void IXmlSerializable.WriteXml([NotNull] XmlWriter writer)
        {
            Preconditions.CheckNotNull(writer, nameof(writer));
            writer.WriteString(OffsetTimePattern.ExtendedIso.Format(this));
        }
        #endregion
    }
}
