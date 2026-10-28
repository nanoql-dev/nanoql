// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/cdo.hpp, QuantLib's older CDO, which the wheel doesn't expose, on
// experimental/credit/portfoliospec's copula and termstructures/credit/creditspec's curves, schedule and discount:
//
//     CDO::calculate (copula, attachment, detachment, nominals, curves, protectionSeller, schedule, premiumRate,
//         dayCounter, recoveryRate, upfrontPremiumRate, discount, nBuckets, integrationStep ([length, units])):
//         [NPV, premiumValue, protectionValue, fairPremium, error, nominal, isExpired]; not lgd(), which QuantLib sums
//         onto an uninitialized value (the S-xcr plan's XR8 (xxvii))

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include "experimental/credit/portfoliospec.hpp"
#include "termstructures/credit/creditspec.hpp"

#include <ql/experimental/credit/cdo.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"CDO::calculate",
             [](const Args& a) -> Call {
                 arity(a, 14);
                 json::value copula = a[0], curves = a[4], step = a[13];
                 Real attachment = real(a[1]), detachment = real(a[2]);
                 std::vector<Real> nominals = reals(a[3]);
                 bool seller = boolean(a[5]);
                 credit::ScheduleSpec schedule = credit::schedule(a[6]);
                 Real premiumRate = real(a[7]);
                 credit::DayCounterSpec dc = credit::dayCounter(a[8]);
                 Real recoveryRate = real(a[9]), upfront = real(a[10]);
                 credit::DiscountSpec discount = credit::discount(a[11]);
                 auto nBuckets = static_cast<Size>(integer(a[12]));
                 if (!curves.is_array())
                     throw InvalidParams("expected an array of curves");
                 std::vector<credit::CurveSpec> curveSpecs;
                 for (const json::value& c : curves.get_array())
                     curveSpecs.push_back(credit::curve(c));
                 return [=] {
                     std::vector<Handle<DefaultProbabilityTermStructure>> basket;
                     for (const credit::CurveSpec& c : curveSpecs)
                         basket.emplace_back(credit::makeCurve(c));
                     CDO cdo(attachment, detachment, nominals, basket,
                             Handle<OneFactorCopula>(credit::portfolio::makeOneFactorCopula(copula)), seller,
                             credit::makeSchedule(schedule), premiumRate, credit::makeDayCounter(dc), recoveryRate,
                             upfront, credit::makeDiscount(discount), nBuckets, credit::portfolio::period(step));
                     json::array out;
                     out.emplace_back(cdo.NPV());
                     out.emplace_back(cdo.premiumValue());
                     out.emplace_back(cdo.protectionValue());
                     out.emplace_back(cdo.fairPremium());
                     out.emplace_back(static_cast<std::int64_t>(cdo.error()));
                     out.emplace_back(cdo.nominal());
                     out.emplace_back(cdo.isExpired());
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
