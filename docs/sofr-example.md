<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Pricing a USD SOFR swap to match the market

QuantLib's `MakeOIS` builds an overnight swap from an index and a tenor, taking a default for every option it isn't
given. For USD SOFR most of its defaults are already the market's. This example sets the market's conventions in XML,
adds them one at a time, and shows what each changes in the price.

The quotes are illustrative, made up for the example. The conventions are the market's, as the CFTC describes USD SOFR
OIS for its trade execution requirement (Release 8745-23, 2023).

## The market's convention

| | The market | QuantLib's `MakeOIS` defaults |
|---|---|---|
| Start | spot, two New York business days after the trade | two days on SOFR's fixing calendar |
| Both legs | annual, Actual/360 | annual, Actual/360 |
| Business days | New York's | SOFR's fixing calendar, the US government securities market's |
| Payment | two business days after each period ends | at the period's end |
| SOFR fixings | on the US government securities calendar | the same |

So the defaults already match the market's frequency, day count and spot lag. Two things differ: the payment lag, and
the calendar. New York's business days are the Federal Reserve's, and the government securities calendar closes on
days the Federal Reserve doesn't, Good Friday among them.

The conventions file holds the market's convention, `USD-SOFR`, and the two steps toward it from the defaults:

[!code-xml[](examples/SofrMarket/market.xml)]

## The curve

The curve is bootstrapped from nine quotes, 15 October 2026, each a SOFR swap built by the market's convention:

[!code-csharp[](examples/SofrMarket/SofrExample.cs#curve)]

## Each convention's price

Three five-year swaps on $10 million: one at the curve's par rate, one off-market at 4.25%, and one at par starting on
Good Friday, 26 March 2027. Each is priced four times, from `MakeOIS`'s defaults to the market's convention:

[!code-csharp[](examples/SofrMarket/SofrExample.cs#steps)]

[!INCLUDE [each trade's price at each step](_generated/sofr-output.md)]

- **The payment lag** moves every trade. Paying two days late discounts each period's net payment over two more days,
  so its effect grows with the gap between the fixed rate and the floating leg: small at par, larger off-market.
- **The calendars** move only the Good Friday trade. Its start, 26 March 2027, and its end, 26 March 2032, are both
  Good Fridays: on SOFR's fixing calendar both are holidays, and the schedule rolls them to the Monday; on New York's
  days both stand.
- **The market's convention** adds nothing beyond the two: its frequency, day count and spot lag are the defaults'.
  The Good Friday trade takes `USD-SOFR-Dated`, the same convention without the spot lag, since a swap given its own
  start date can't take settlement days too.

On the Good Friday trade, the first period accrues from a day SOFR isn't published. The fixing before it, Thursday's,
is projected over its own value period, to the next fixing day, and accrues from the period's start, as QuantLib's
overnight coupon does.

Conventions checked against market prices across many more trades are Masseef's verified catalogue.
