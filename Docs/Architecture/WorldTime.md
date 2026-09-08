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
  ordered month definitions.

Calendar month names are localization keys. Simulation and persistence never
store localized month text. Definitions are parsed strictly, bounded,
canonicalized, fingerprinted, and exposed through `IWorldContentCatalog`.

## Date queries

`WorldTimeQueries.GetDateTime` is a pure projection over a clock and matching
calendar definition. The first implemented calendar has twelve authored
30-day months, a seven-day week, and starting year 327. These are base content
values rather than simulation constants. Leap years and irregular era rules
are not implemented.

## Persistence

Campaign save schema 12 stores elapsed campaign seconds, the fractional
remainder, `CalendarId`, and `TimeScaleId`. Loading resolves the referenced
definitions from the content-locked snapshot. Campaign validation rejects
missing, stale, mismatched, or structurally invalid clock definitions.

The prototype accepts only the current save schema; schema 11 saves require an
explicit migration before they can be loaded.

## Planned extensions

Voyage-relative days, accelerated voyage scales, local planetary light cycles,
and shipboard watch schedules are not part of this first implementation. They
should derive from absolute campaign time instead of maintaining parallel
mutable clocks.
