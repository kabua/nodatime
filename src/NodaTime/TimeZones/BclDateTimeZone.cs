// Copyright 2012 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using JetBrains.Annotations;
using NodaTime.Annotations;
using NodaTime.Extensions;
using NodaTime.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NodaTime.TimeZones
{
    /// <summary>
    /// Representation of a time zone converted from a <see cref="TimeZoneInfo"/> from the Base Class Library.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two instances of this class are deemed equal if and only if they refer to the exact same
    /// <see cref="TimeZoneInfo"/> object.
    /// </para>
    /// <para>
    /// This implementation does not always give the same results as <c>TimeZoneInfo</c>, in that it doesn't replicate
    /// the bugs in the BCL interpretation of the data. These bugs are described in
    /// <a href="http://codeblog.jonskeet.uk/2014/09/30/the-mysteries-of-bcl-time-zone-data/">a blog post</a>, but we're
    /// not expecting them to be fixed any time soon. Being bug-for-bug compatible would not only be tricky, but would be painful
    /// if the BCL were ever to be fixed. As far as we are aware, there are only discrepancies around new year where the zone
    /// changes from observing one rule to observing another.
    /// </para>
    /// </remarks>
    /// <threadsafety>This type is immutable reference type. See the thread safety section of the user guide for more information.</threadsafety>
    [Immutable]
    public sealed class BclDateTimeZone : DateTimeZone
    {
        /// <summary>
        /// This is used to cache the last result of a call to <see cref="ForSystemDefault"/>, but it doesn't
        /// matter if it's out of date - we'll just create another wrapper if necessary. It's not *that* expensive to make
        /// a few more wrappers than we need.
        /// </summary>
        private static BclDateTimeZone systemDefault;

        private readonly IZoneIntervalMap map;

        /// <summary>
        /// Gets the original <see cref="TimeZoneInfo"/> from which this was created.
        /// </summary>
        /// <value>The original <see cref="TimeZoneInfo"/> from which this was created.</value>
        [NotNull] public TimeZoneInfo OriginalZone { get; }

        /// <summary>
        /// Gets the display name associated with the time zone, as provided by the Base Class Library.
        /// </summary>
        /// <value>The display name associated with the time zone, as provided by the Base Class Library.</value>
        [NotNull] public string DisplayName => OriginalZone.DisplayName;

        private BclDateTimeZone(TimeZoneInfo bclZone, Offset minOffset, Offset maxOffset, IZoneIntervalMap map)
            : base(bclZone.Id, bclZone.SupportsDaylightSavingTime, minOffset, maxOffset)
        {
            this.OriginalZone = bclZone;
            this.map = map;
        }

        /// <inheritdoc />
        public override ZoneInterval GetZoneInterval(Instant instant)
        {
            return map.GetZoneInterval(instant);
        }

        /// <summary>
        /// Creates a new <see cref="BclDateTimeZone" /> from a <see cref="TimeZoneInfo"/> from the Base Class Library.
        /// </summary>
        /// <param name="bclZone">The original time zone to take information from.</param>
        /// <returns>A <see cref="BclDateTimeZone"/> wrapping the given <c>TimeZoneInfo</c>.</returns>
        [NotNull] public static BclDateTimeZone FromTimeZoneInfo([NotNull] TimeZoneInfo bclZone)
        {
            Preconditions.CheckNotNull(bclZone, nameof(bclZone));
            Offset standardOffset = bclZone.BaseUtcOffset.ToOffset();
            var rules = bclZone.GetAdjustmentRules();
            if (!bclZone.SupportsDaylightSavingTime || rules.Length == 0)
            {
                var fixedInterval = new ZoneInterval(bclZone.StandardName, Instant.BeforeMinValue, Instant.AfterMaxValue, standardOffset, Offset.Zero);
                return new BclDateTimeZone(bclZone, standardOffset, standardOffset, new SingleZoneIntervalMap(fixedInterval));
            }

            int windowsRules = rules.Count(IsWindowsRule);
            var ruleConverter = AreWindowsStyleRules(rules)
                ? rule => BclAdjustmentRule.FromWindowsAdjustmentRule(bclZone, rule)
                : (Converter<TimeZoneInfo.AdjustmentRule, BclAdjustmentRule>)(rule => BclAdjustmentRule.FromUnixAdjustmentRule(bclZone, rule));

            BclAdjustmentRule[] convertedRules = Array.ConvertAll(rules, ruleConverter);

            Offset minRuleOffset = convertedRules.Aggregate(Offset.MaxValue, (min, rule) => Offset.Min(min, rule.Savings + rule.StandardOffset));
            Offset maxRuleOffset = convertedRules.Aggregate(Offset.MinValue, (min, rule) => Offset.Max(min, rule.Savings + rule.StandardOffset));

            IZoneIntervalMap uncachedMap = BuildMap(convertedRules, standardOffset, bclZone.StandardName);
            IZoneIntervalMap cachedMap = CachingZoneIntervalMap.CacheMap(uncachedMap);
            return new BclDateTimeZone(bclZone, Offset.Min(standardOffset, minRuleOffset), Offset.Max(standardOffset, maxRuleOffset), cachedMap);
        }

        /// <summary>
        /// .NET Core on Unix adjustment rules can't currently be treated like regular Windows ones.
        /// Instead of dividing time into periods by year, the rules are read from TZIF files, so are like
        /// our PrecalculatedDateTimeZone. This is only visible for testing purposes.
        /// </summary>
        internal static bool AreWindowsStyleRules(TimeZoneInfo.AdjustmentRule[] rules)
        {
            int windowsRules = rules.Count(IsWindowsRule);
            return windowsRules == rules.Length;

            bool IsWindowsRule(TimeZoneInfo.AdjustmentRule rule) =>
                rule.DateStart.Month == 1 && rule.DateStart.Day == 1 && rule.DateStart.TimeOfDay.Ticks == 0 &&
                rule.DateEnd.Month == 12 && rule.DateEnd.Day == 31 && rule.DateEnd.TimeOfDay.Ticks == 0;
        }

        private static bool IsWindowsRule(TimeZoneInfo.AdjustmentRule rule) =>
            rule.DateStart.Month == 1 && rule.DateStart.Day == 1 && rule.DateStart.TimeOfDay.Ticks == 0 &&
            rule.DateEnd.Month == 12 && rule.DateEnd.Day == 31 && rule.DateEnd.TimeOfDay.Ticks == 0;

        private static IZoneIntervalMap BuildMap(BclAdjustmentRule[] rules, Offset standardOffset, [NotNull] string standardName)
        {
            Preconditions.CheckNotNull(standardName, nameof(standardName));

            // First work out a naive list of partial maps. These will give the right offset at every instant, but not necessarily
            // correct intervals - we may we need to stitch intervals together.
            List<PartialZoneIntervalMap> maps = new List<PartialZoneIntervalMap>();
            // Handle the start of time until the start of the first rule, if necessary.
            if (rules[0].Start.IsValid)
            {
                maps.Add(PartialZoneIntervalMap.ForZoneInterval(standardName, Instant.BeforeMinValue, rules[0].Start, standardOffset, Offset.Zero));
            }
            for (int i = 0; i < rules.Length - 1; i++)
            {
                var beforeRule = rules[i];
                var afterRule = rules[i + 1];
                maps.Add(beforeRule.PartialMap);
                // If there's a gap between this rule and the next one, fill it with a fixed interval.
                if (beforeRule.End < afterRule.Start)
                {
                    maps.Add(PartialZoneIntervalMap.ForZoneInterval(standardName, beforeRule.End, afterRule.Start, standardOffset, Offset.Zero));
                }
            }

            var lastRule = rules[rules.Length - 1];
            maps.Add(lastRule.PartialMap);

            // Handle the end of the last rule until the end of time, if necessary.
            if (lastRule.End.IsValid)
            {
                maps.Add(PartialZoneIntervalMap.ForZoneInterval(standardName, lastRule.End, Instant.AfterMaxValue, standardOffset, Offset.Zero));
            }
            return PartialZoneIntervalMap.ConvertToFullMap(maps);
        }

        /// <summary>
        /// Just a mapping of a TimeZoneInfo.AdjustmentRule into Noda Time types. Very little cleverness here.
        /// </summary>
        private sealed class BclAdjustmentRule
        {
            private static readonly DateTime MaxDate = DateTime.MaxValue.Date;

            /// <summary>
            /// Instant on which this rule starts.
            /// </summary>
            internal Instant Start { get; }

            /// <summary>
            /// Instant on which this rule ends.
            /// </summary>
            internal Instant End { get; }

            /// <summary>
            /// Daylight savings, when applicable within this rule.
            /// </summary>
            internal Offset Savings { get; }

            /// <summary>
            /// The standard offset for the duration of this rule.
            /// </summary>
            internal Offset StandardOffset { get; }

            internal PartialZoneIntervalMap PartialMap { get; }

            private BclAdjustmentRule(Instant start, Instant end, Offset standardOffset, Offset savings, PartialZoneIntervalMap partialMap)
            {
                Start = start;
                End = end;
                StandardOffset = standardOffset;
                Savings = savings;
                PartialMap = partialMap;
            }

            internal static BclAdjustmentRule FromUnixAdjustmentRule(TimeZoneInfo zone, TimeZoneInfo.AdjustmentRule rule)
            {
                // On .NET Core on Unix, each "adjustment rule" is effectively just a zone interval. The transitions are only used
                // to give the time of day values to combine with rule.DateStart and rule.DateEnd. It's all a bit odd.
                // The *last* adjustment rule internally can work like a normal Windows standard/daylight rule, but currently that's
                // not exposed properly.
                var bclLocalStart = rule.DateStart + rule.DaylightTransitionStart.TimeOfDay.TimeOfDay;
                var bclLocalEnd = rule.DateEnd + rule.DaylightTransitionEnd.TimeOfDay.TimeOfDay;
                var bclUtcStart = DateTime.SpecifyKind(bclLocalStart == DateTime.MinValue ? DateTime.MinValue : bclLocalStart - zone.BaseUtcOffset, DateTimeKind.Utc);
                var bclWallOffset = zone.GetUtcOffset(bclUtcStart);
                var bclSavings = rule.DaylightDelta;
                var bclUtcEnd = DateTime.SpecifyKind(rule.DateEnd == MaxDate ? DateTime.MaxValue : bclLocalEnd - (zone.BaseUtcOffset + bclSavings), DateTimeKind.Utc);
                var isDst = zone.IsDaylightSavingTime(bclUtcStart);

                // The BCL rule can't express "It's DST with a changed standard time" so we sometimes end
                // up with DST but no savings. Assume this means a savings of 1 hour. That's not a valid
                // assumption in all cases, but it's probably better than alternatives, given limited information.
                if (isDst && bclSavings == TimeSpan.Zero)
                {
                    bclSavings = TimeSpan.FromHours(1);
                }
                // Sometimes the rule says "This rule doesn't apply daylight savings" but still has a daylight
                // savings delta. Extremely bizarre: just override the savings to zero.
                if (!isDst && bclSavings != TimeSpan.Zero)
                {
                    bclSavings = TimeSpan.Zero;
                }

                // Handle changes crossing the international date line, which are represented as savings of +/-23
                // hours (but could conceivably be more).
                if (bclSavings.Hours < -14)
                {
                    bclSavings += TimeSpan.FromDays(1);
                }
                else if (bclSavings.Hours > 14)
                {
                    bclSavings -= TimeSpan.FromDays(1);
                }
                var bclStandard = bclWallOffset - bclSavings;

                // Now all the values are sensible - and in particular, now the daylight savings are in a range that can be represented by
                // Offset - we can converted everything to Noda Time types.
                var nodaStart = bclUtcStart == DateTime.MinValue ? Instant.BeforeMinValue : bclUtcStart.ToInstant();
                // The representation returned to us (not the internal representation) has an end point one second before the transition.
                var nodaEnd = bclUtcEnd == DateTime.MaxValue ? Instant.AfterMaxValue : bclUtcEnd.ToInstant() + Duration.FromSeconds(1);
                var nodaWallOffset = bclWallOffset.ToOffset();
                var nodaStandard = bclStandard.ToOffset();
                var nodaSavings = bclSavings.ToOffset();

                var partialMap = PartialZoneIntervalMap.ForZoneInterval(isDst ? zone.StandardName : zone.DaylightName, nodaStart, nodaEnd, nodaWallOffset, nodaSavings);
                return new BclAdjustmentRule(nodaStart, nodaEnd, nodaStandard, nodaSavings, partialMap);
            }

            internal static BclAdjustmentRule FromWindowsAdjustmentRule(TimeZoneInfo zone, TimeZoneInfo.AdjustmentRule rule)
            {
                // With .NET 4.6, adjustment rules can have their own standard offsets, allowing
                // a much more reasonable set of time zone data. Unfortunately, this isn't directly
                // exposed, but we can detect it by just finding the UTC offset for an arbitrary
                // time within the rule - the start, in this case - and then take account of the
                // possibility of that being in daylight saving time. Fortunately, we only need
                // to do this during the setup.
                var ruleStandardOffset = zone.GetUtcOffset(rule.DateStart);
                if (zone.IsDaylightSavingTime(rule.DateStart))
                {
                    ruleStandardOffset -= rule.DaylightDelta;
                }
                var standardOffset = ruleStandardOffset.ToOffset();

                // Although the rule may have its own standard offset, the start/end is still determined
                // using the zone's standard offset.
                var zoneStandardOffset = zone.BaseUtcOffset.ToOffset();

                // Note: this extends back from DateTime.MinValue to start of time, even though the BCL can represent
                // as far back as 1AD. This is in the *spirit* of a rule which goes back that far.
                var start = rule.DateStart == DateTime.MinValue ? Instant.BeforeMinValue : rule.DateStart.ToLocalDateTime().WithOffset(zoneStandardOffset).ToInstant();
                // The end instant (exclusive) is the end of the given date, so we need to add a day.
                var end = rule.DateEnd == MaxDate ? Instant.AfterMaxValue : rule.DateEnd.ToLocalDateTime().PlusDays(1).WithOffset(zoneStandardOffset).ToInstant();
                var savings = rule.DaylightDelta.ToOffset();

                PartialZoneIntervalMap partialMap;
                // Some rules have DST start/end of "January 1st", to indicate that they're just in standard time. This is important
                // for rules which have a standard offset which is different to the standard offset of the zone itself.
                if (IsStandardOffsetOnlyRule(rule))
                {
                    partialMap = PartialZoneIntervalMap.ForZoneInterval(zone.StandardName, start, end, standardOffset, Offset.Zero);
                }
                else
                {
                    var daylightRecurrence = new ZoneRecurrence(zone.DaylightName, savings, ConvertTransition(rule.DaylightTransitionStart), int.MinValue, int.MaxValue);
                    var standardRecurrence = new ZoneRecurrence(zone.StandardName, Offset.Zero, ConvertTransition(rule.DaylightTransitionEnd), int.MinValue, int.MaxValue);
                    IZoneIntervalMap recurringMap = new StandardDaylightAlternatingMap(standardOffset, standardRecurrence, daylightRecurrence);
                    // Fake 1 hour savings if the adjustment rule claims to be 0 savings. See DaylightFakingZoneIntervalMap documentation below for more details.
                    if (savings == Offset.Zero)
                    {
                        recurringMap = new DaylightFakingZoneIntervalMap(recurringMap, zone.DaylightName);
                    }
                    partialMap = new PartialZoneIntervalMap(start, end, recurringMap);
                }
                return new BclAdjustmentRule(start, end, standardOffset, savings, partialMap);
            }

            /// <summary>
            /// An implementation of IZoneIntervalMap that delegates to an original map, except for where the result of a
            /// ZoneInterval lookup has the given daylight name. In that case, a new ZoneInterval is built with the same
            /// wall offset (and start/end instants etc), but with a savings of 1 hour. This is only used to work around TimeZoneInfo
            /// adjustment rules with a daylight saving of 0 which are really trying to fake a more comprehensive solution.
            /// (This is currently only seen on Mono on Linux...)
            /// This addresses https://github.com/nodatime/nodatime/issues/746.
            /// If TimeZoneInfo had sufficient flexibility to use different names for different periods of time, we'd have
            /// another problem, as some "daylight names" don't always mean daylight - e.g. "BST" = British Summer Time and British Standard Time.
            /// In this case, the limited nature of TimeZoneInfo works in our favour.
            /// </summary>
            private sealed class DaylightFakingZoneIntervalMap : IZoneIntervalMap
            {
                private readonly IZoneIntervalMap originalMap;
                private readonly string daylightName;

                internal DaylightFakingZoneIntervalMap(IZoneIntervalMap originalMap, string daylightName)
                {
                    this.originalMap = originalMap;
                    this.daylightName = daylightName;
                }

                public ZoneInterval GetZoneInterval(Instant instant)
                {
                    var interval = originalMap.GetZoneInterval(instant);
                    return interval.Name == daylightName
                        ? new ZoneInterval(daylightName, interval.RawStart, interval.RawEnd, interval.WallOffset, Offset.FromHours(1))
                        : interval;
                }
            }

            /// <summary>
            /// The BCL represents "standard-only" rules using two fixed date January 1st transitions.
            /// Currently the time-of-day used for the DST end transition is at one millisecond past midnight... we'll
            /// be slightly more lenient, accepting anything up to 12:01...
            /// </summary>
            private static bool IsStandardOffsetOnlyRule(TimeZoneInfo.AdjustmentRule rule)
            {
                var daylight = rule.DaylightTransitionStart;
                var standard = rule.DaylightTransitionEnd;
                return daylight.IsFixedDateRule && daylight.Day == 1 && daylight.Month == 1 &&
                       daylight.TimeOfDay.TimeOfDay < TimeSpan.FromMinutes(1) &&
                       standard.IsFixedDateRule && standard.Day == 1 && standard.Month == 1 &&
                       standard.TimeOfDay.TimeOfDay < TimeSpan.FromMinutes(1);
            }

            // Converts a TimeZoneInfo "TransitionTime" to a "ZoneYearOffset" - the two correspond pretty closely.
            private static ZoneYearOffset ConvertTransition(TimeZoneInfo.TransitionTime transitionTime)
            {
                // Used for both fixed and non-fixed transitions.
                LocalTime timeOfDay = LocalDateTime.FromDateTime(transitionTime.TimeOfDay).TimeOfDay;

                // Easy case - fixed day of the month.
                if (transitionTime.IsFixedDateRule)
                {
                    return new ZoneYearOffset(TransitionMode.Wall, transitionTime.Month, transitionTime.Day, 0, false, timeOfDay);
                }

                // Floating: 1st Sunday in March etc.
                int dayOfWeek = (int)BclConversions.ToIsoDayOfWeek(transitionTime.DayOfWeek);
                int dayOfMonth;
                bool advance;
                // "Last"
                if (transitionTime.Week == 5)
                {
                    advance = false;
                    dayOfMonth = -1;
                }
                else
                {
                    advance = true;
                    // Week 1 corresponds to ">=1"
                    // Week 2 corresponds to ">=8" etc
                    dayOfMonth = (transitionTime.Week * 7) - 6;
                }
                return new ZoneYearOffset(TransitionMode.Wall, transitionTime.Month, dayOfMonth, dayOfWeek, advance, timeOfDay);
            }
        }
        
        /// <summary>
        /// Returns a time zone converted from the BCL representation of the system local time zone.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is approximately equivalent to calling <see cref="IDateTimeZoneProvider.GetSystemDefault"/> with
        /// an implementation that wraps <see cref="BclDateTimeZoneSource"/> (e.g.
        /// <see cref="DateTimeZoneProviders.Bcl"/>), with the exception that it will succeed even if the current local
        /// time zone was not one of the set of system time zones captured when the source was created (which, while
        /// highly unlikely, might occur either because the local time zone is not a system time zone, or because the
        /// system time zones have themselves changed).
        /// </para>
        /// <para>
        /// This method will retain a reference to the returned <c>BclDateTimeZone</c>, and will attempt to return it if
        /// called repeatedly (assuming that the local time zone has not changed) rather than creating a new instance,
        /// though this behaviour is not guaranteed.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">The system does not provide a time zone.</exception>
        /// <returns>A <see cref="BclDateTimeZone"/> wrapping the "local" (system) time zone as returned by
        /// <see cref="TimeZoneInfo.Local"/>.</returns>
        [NotNull] public static BclDateTimeZone ForSystemDefault()
        {
            TimeZoneInfo local = TimeZoneInfoInterceptor.Local;
            if (local is null)
            {
                throw new InvalidOperationException("No system default time zone is available");
            }
            BclDateTimeZone currentSystemDefault = systemDefault;

            // Cached copy is out of date - wrap a new one
            if (currentSystemDefault?.OriginalZone != local)
            {
                currentSystemDefault = FromTimeZoneInfo(local);
                systemDefault = currentSystemDefault;
            }
            // Always return our local variable; the variable may have changed again.
            return currentSystemDefault;
        }
    }
}
