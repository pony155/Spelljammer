# World time

## Implemented boundary

Authoritative time has three separate contracts under
`Spelljammer.Simulation.World`:

- `WorldTimeDefinition` controls fixed simulation cadence and the maximum
  catch-up ticks accepted by one `World.Advance` call.
- `TimeScaleDefinition` maps simulation ticks to elapsed campaign seconds as
  an integer rational number.
- `CalendarDefinition` projects absolute campaign seconds into a display year,
  ordered month, day, weekday index, hour, minute, and second.

`CampaignClockState` persists only `ElapsedWorldSeconds`, a deterministic
`FractionRemainder`, and the selected calendar and time-scale IDs. It does not
persist duplicated year, month, day, or formatted text.

## Standard calendar and time

The setting's shared interstellar calendar is the **Elven Astral Calendar
(EAC)**. **Year 0 EAC** is the **First Voyage**, the first historically
significant elven interstellar voyage. The modern campaign era is approximately
**7421 EAC**.

The corresponding standard time is **Elven Astral Time (EAT)**. EAC identifies
the date and historical year; EAT identifies the time within that date. EAT
uses the 24-hour clock.

Their common unit structure is:

| Unit | Definition |
| --- | ---: |
| Minute | 60 seconds |
| Hour | 60 minutes |
| Day | 24 hours |
| Month | 30 days |
| Year | 12 months |
| Year | 360 days |

Elven, dwarven, and human institutions use EAC for cross-system navigation,
trade, military activity, diplomacy, and official records. Every starport,
warship, merchant vessel, navigation system, and formal record that uses EAC
uses EAT as its default time standard. Local, religious, dynastic, and
traditional calendars and local civil time remain valid presentation systems.
A conversion to one of those systems must be a pure projection from the same
absolute campaign time; it must not introduce a second mutable clock.

The `EAC` era label, `EAT` time-standard label, and localized timestamp
formatting belong to presentation and localization. Simulation state stores
stable calendar IDs and numeric time only. Scenario content selects the exact
opening timestamp; a campaign in the modern era should author a starting year
near 7421 rather than deriving it from the host computer's clock.
The base calendar explicitly starts at **7421 EAC, First Light 01,
00:00:00 EAT** with weekday index zero.

## Advancement

`World.AdvanceOneTick` advances both `World.Tick` and `CampaignClockState`.
`CampaignClockSystem` uses integer arithmetic:

```text
accumulated = previous remainder + world-seconds numerator * simulation ticks
elapsed     = accumulated / simulation-ticks denominator
remainder   = accumulated % simulation-ticks denominator
```

This makes batched and step-by-step advancement identical and avoids
floating-point drift. A paused `World` advances neither simulation ticks nor
campaign time.

Combat scheduling, casting, recovery, and reactions continue to use exact
simulation ticks. Long-running campaign systems should use absolute campaign
seconds.

## Authored content

The base definitions are:

- `Definitions/WorldTimes/standard.json` for engine cadence and catch-up;
- `Definitions/TimeScales/tactical.json` for normal campaign-time flow; and
- `Definitions/Calendars/voidfarer-standard.json` for calendar structure and
  ordered month definitions plus the opening year, month, day, weekday, hour,
  minute, and second.

Calendar names, EAC/EAT labels, date/time layouts, month names, and weekday
names are localization keys. `EacEatTimestampFormatter` in the application
presentation layer produces localized date, time, or combined timestamp text;
it does not alter authoritative state. Simulation and persistence never store
localized calendar text. Definitions are parsed strictly, bounded,
canonicalized, fingerprinted, and exposed through `IWorldContentCatalog`.

## Date queries

`WorldTimeQueries.GetDateTime` is a pure projection over a clock and matching
calendar definition. EAT uses 60-second minutes, 60-minute hours, and 24-hour
days. EAC has twelve authored 30-day months and a 360-day year. The existing
calendar model also authors a seven-day week for weekday display. All of these
values remain content rather than simulation constants. Leap years, irregular
era rules, and conversion between calendar definitions are not implemented.

The base `voidfarer-standard.json` authors the complete opening timestamp.
Changing an opening component or any calendar unit changes the compiled
semantic content fingerprint.

## Persistence

Campaign save schema 12 stores elapsed campaign seconds, the fractional
remainder, `CalendarId`, and `TimeScaleId`. Loading resolves the referenced
definitions from the content-locked snapshot. Campaign validation rejects
missing, stale, mismatched, or structurally invalid clock definitions.

The prototype accepts only the current save schema. No migration is shipped
for the pre-EAC base fingerprint or schema 11 saves. They are intentionally
treated as unsupported prototype data unless a caller separately retains the
matching old content snapshot and supplies an explicit migration path.

## Planned extensions

Voyage-relative days, accelerated voyage scales, EAC/EAT-to-local-time
conversion, local planetary light cycles, and shipboard watch schedules are
not part of this first implementation. They should derive from absolute
campaign time instead of maintaining parallel mutable clocks.
