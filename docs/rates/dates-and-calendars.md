<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Dates and calendars

`Date` is a day, `Period` a length of time in days, weeks, months or years, and a `Calendar` knows its market's
business days. Each is an immutable value, as in QuantLib: `new Date(23, Month.December, 2026)`,
`new Period(6, TimeUnit.Months)`.

## Rolling dates

A calendar advances a date by business days or by a period, and adjusts a date that falls on a holiday by a
business-day convention:

[!code-csharp[](../examples/Rates/DatesAndCalendars.cs#roll)]

[!INCLUDE [the rolled dates](../_generated/dates-roll-output.md)]

Spot from 23 December is the 28th: TARGET closes on the 25th and 26th. The 31st of October 2026 is a Saturday, and
modified following rolls it back to the Friday rather than into November.

The calendars are QuantLib's, each a sealed class: `TARGET`, `UnitedStates` and `UnitedKingdom` with their markets,
and the rest by country. `JointCalendar` combines several, and `BespokeCalendar` is one of your own. Adding a holiday
returns a new calendar, and leaves the original as it was. With the `NanoQL.Extensions` package a calendar's holidays
can be changed as data ([conventions.md](../conventions.md)).

## Schedules and day counts

`MakeSchedule` builds a coupon schedule as QuantLib's does, and each day counter gives a period's accrual fraction:

[!code-csharp[](../examples/Rates/DatesAndCalendars.cs#schedule)]

[!INCLUDE [the schedule and its fractions](../_generated/dates-schedule-output.md)]

Actual/Actual (ISDA) counts each day over its own year's length, so the periods in 2028, a leap year, differ from
Actual/365 (Fixed).
