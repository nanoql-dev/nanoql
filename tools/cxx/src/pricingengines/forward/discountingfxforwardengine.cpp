// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/forward/discountingfxforwardengine.hpp: the engine's additional results, which the wheel doesn't
// expose. "DiscountingFxForwardEngine::additionalResults" takes (sourceNominal, targetNominal, maturityDate,
// paySourceCurrency, settlementDays, onTarget, sourceReferenceDate, sourceRate, sourceDayCounter, targetReferenceDate,
// targetRate, targetDayCounter, spotFx) and prices a USD/SGD FxForward built from its two nominals, settling on TARGET
// or on no calendar (Calendar(), QuantLib's empty one), on flat forwards, and returns {"spotFx",
// "sourceCurrencyDiscountFactor", "targetCurrencyDiscountFactor", "sourceCurrencySettlementDiscountFactor",
// "targetCurrencySettlementDiscountFactor", "sourceCurrencyPV", "targetCurrencyPV"}; the engine reads no currency. The
// evaluation date is the case's Settings.

#include "convert.hpp"

#include <ql/currencies/america.hpp>
#include <ql/currencies/asia.hpp>
#include <ql/instruments/fxforward.hpp>
#include <ql/pricingengines/forward/discountingfxforwardengine.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/target.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            // (sourceNominal, targetNominal, maturityDate, paySourceCurrency, settlementDays, onTarget,
            //  sourceReferenceDate, sourceRate, sourceDayCounter, targetReferenceDate, targetRate, targetDayCounter,
            //  spotFx)
            {"DiscountingFxForwardEngine::additionalResults",
             [](const Args& a) -> Call {
                 arity(a, 13);
                 Real sourceNominal = real(a[0]), targetNominal = real(a[1]);
                 DateSpec maturity = date(a[2]);
                 bool pay = boolean(a[3]);
                 Integer days = integer(a[4]);
                 bool onTarget = boolean(a[5]);
                 DateSpec sourceReference = date(a[6]);
                 Real sourceRate = real(a[7]);
                 DayCounterSpec sourceDc = dayCounter(a[8]);
                 DateSpec targetReference = date(a[9]);
                 Real targetRate = real(a[10]);
                 DayCounterSpec targetDc = dayCounter(a[11]);
                 Real spot = real(a[12]);
                 return [=] {
                     FxForward forward(sourceNominal, USDCurrency(), targetNominal, SGDCurrency(), makeDate(maturity),
                                       pay, static_cast<Natural>(days), onTarget ? Calendar(TARGET()) : Calendar());
                     forward.setPricingEngine(ext::make_shared<DiscountingFxForwardEngine>(
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(
                             makeDate(sourceReference), sourceRate, makeDayCounter(sourceDc))),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(
                             makeDate(targetReference), targetRate, makeDayCounter(targetDc))),
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot))));
                     forward.NPV();
                     const auto& results = forward.additionalResults();
                     json::object out;
                     for (const char* key :
                          {"spotFx", "sourceCurrencyDiscountFactor", "targetCurrencyDiscountFactor",
                           "sourceCurrencySettlementDiscountFactor", "targetCurrencySettlementDiscountFactor",
                           "sourceCurrencyPV", "targetCurrencyPV"})
                         out[key] = ext::any_cast<Real>(results.at(key));
                     return json::value(out);
                 };
             }},
        });

    }

}
