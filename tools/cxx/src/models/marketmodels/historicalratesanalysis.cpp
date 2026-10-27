// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/historicalratesanalysis.hpp and historicalforwardratesanalysis.hpp: the historical analyses,
// which the wheel doesn't expose, on the fixing histories the case's Settings call stores under each index's name.
//
// "historicalRatesAnalysis" takes (startDate, endDate, step, indexes): the step [length, units], the indexes Ibor
// specs (indexes/indexspec.hpp); the result {"skippedDates", "skippedDatesErrorMessage", "statistics"}.
//
// "historicalForwardRatesAnalysis" takes (curve, startDate, endDate, step, fwdIndex, initialGap, horizon,
// iborIndexes, yieldCurveDayCounter, yieldCurveAccuracy): the curve "Discount/LogLinear" or "ZeroYield/Linear", the
// periods [length, units], the indexes Ibor specs, no swap index; the result {"skippedDates",
// "skippedDatesErrorMessage", "failedDates", "failedDatesErrorMessage", "fixingPeriods", "statistics"}.
//
// The statistics are {"size", "samples", "weightSum", "mean", "covariance"}, the mean {"throws": true} on no sample
// and the covariance on fewer than two.

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/errors.hpp>
#include <ql/math/interpolations/linearinterpolation.hpp>
#include <ql/math/interpolations/loginterpolation.hpp>
#include <ql/models/marketmodels/historicalforwardratesanalysis.hpp>
#include <ql/models/marketmodels/historicalratesanalysis.hpp>
#include <ql/termstructures/yield/bootstraptraits.hpp>

#include <cstdint>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        struct PeriodSpec {
            Integer length = 0;
            TimeUnit units = Days;
        };

        PeriodSpec period(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 2)
                throw InvalidParams("expected a period as [length, units], got " + json::serialize(v));
            return {integer(v.get_array()[0]), enumeration<TimeUnit>(v.get_array()[1])};
        }

        Period makePeriod(const PeriodSpec& p) { return Period(p.length, p.units); }

        std::vector<indexes::IndexSpec> iborIndexes(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of indexes");
            std::vector<indexes::IndexSpec> out;
            for (const json::value& x : v.get_array()) {
                indexes::IndexSpec s = indexes::iborIndex(x);
                if (s.type != "IborIndex")
                    throw InvalidParams("the analyses read IborIndex specs");
                out.push_back(s);
            }
            return out;
        }

        json::value dates(const std::vector<Date>& d) {
            json::array out;
            for (const Date& x : d)
                out.emplace_back(harness::result(x));
            return out;
        }

        json::value texts(const std::vector<std::string>& t) {
            json::array out;
            for (const std::string& x : t)
                out.emplace_back(json::string(x));
            return out;
        }

        json::value statistics(const SequenceStatistics& s) {
            json::object out;
            out["size"] = static_cast<std::int64_t>(s.size());
            out["samples"] = static_cast<std::int64_t>(s.samples());
            out["weightSum"] = s.weightSum();
            json::object t;
            t["throws"] = true;
            try {
                out["mean"] = result(s.mean());
            } catch (Error&) {
                out["mean"] = t;
            }
            try {
                out["covariance"] = result(s.covariance());
            } catch (Error&) {
                out["covariance"] = t;
            }
            return out;
        }

        template <class Traits, class Interpolator>
        json::value forwardAnalysis(const DateSpec& start, const DateSpec& end, const PeriodSpec& step,
                                    const indexes::IndexSpec& fwd, const PeriodSpec& gap, const PeriodSpec& horizon,
                                    const std::vector<indexes::IndexSpec>& ibors, const DayCounterSpec& dc,
                                    Real accuracy) {
            std::vector<ext::shared_ptr<IborIndex>> iborIdx;
            for (const auto& s : ibors)
                iborIdx.push_back(indexes::makeIborIndex(s));
            SequenceStatistics stats;
            std::vector<Date> skipped, failed;
            std::vector<std::string> skippedMessages, failedMessages;
            std::vector<Period> fixingPeriods;
            historicalForwardRatesAnalysis<Traits, Interpolator>(
                stats, skipped, skippedMessages, failed, failedMessages, fixingPeriods, makeDate(start), makeDate(end),
                makePeriod(step), indexes::makeIborIndex(fwd), makePeriod(gap), makePeriod(horizon), iborIdx,
                std::vector<ext::shared_ptr<SwapIndex>>(), makeDayCounter(dc), accuracy);
            json::object out;
            out["skippedDates"] = dates(skipped);
            out["skippedDatesErrorMessage"] = texts(skippedMessages);
            out["failedDates"] = dates(failed);
            out["failedDatesErrorMessage"] = texts(failedMessages);
            json::array periods;
            for (const Period& p : fixingPeriods) {
                json::array x;
                x.emplace_back(static_cast<std::int64_t>(p.length()));
                x.emplace_back(static_cast<std::int64_t>(p.units()));
                periods.emplace_back(x);
            }
            out["fixingPeriods"] = periods;
            out["statistics"] = statistics(stats);
            return json::value(out);
        }

        const Register unit({
            {"historicalRatesAnalysis",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 DateSpec start = date(a[0]), end = date(a[1]);
                 PeriodSpec step = period(a[2]);
                 std::vector<indexes::IndexSpec> specs = iborIndexes(a[3]);
                 if (specs.empty() || step.length <= 0)
                     throw InvalidParams("an index at least and a positive step");
                 return [=] {
                     std::vector<ext::shared_ptr<InterestRateIndex>> idx;
                     for (const auto& s : specs)
                         idx.push_back(indexes::makeIborIndex(s));
                     SequenceStatistics stats;
                     std::vector<Date> skipped;
                     std::vector<std::string> messages;
                     historicalRatesAnalysis(stats, skipped, messages, makeDate(start), makeDate(end),
                                             makePeriod(step), idx);
                     json::object out;
                     out["skippedDates"] = dates(skipped);
                     out["skippedDatesErrorMessage"] = texts(messages);
                     out["statistics"] = statistics(stats);
                     return json::value(out);
                 };
             }},
            {"historicalForwardRatesAnalysis",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 std::string curve = text(a[0]);
                 if (curve != "Discount/LogLinear" && curve != "ZeroYield/Linear")
                     throw InvalidParams("unknown curve " + curve);
                 DateSpec start = date(a[1]), end = date(a[2]);
                 PeriodSpec step = period(a[3]);
                 indexes::IndexSpec fwd = indexes::iborIndex(a[4]);
                 PeriodSpec gap = period(a[5]), horizon = period(a[6]);
                 std::vector<indexes::IndexSpec> ibors = iborIndexes(a[7]);
                 DayCounterSpec dc = dayCounter(a[8]);
                 Real accuracy = real(a[9]);
                 if (step.length <= 0 || fwd.type != "IborIndex")
                     throw InvalidParams("a positive step and an IborIndex to forward");
                 return [=] {
                     return curve == "Discount/LogLinear"
                                ? forwardAnalysis<Discount, LogLinear>(start, end, step, fwd, gap, horizon, ibors, dc,
                                                                       accuracy)
                                : forwardAnalysis<ZeroYield, Linear>(start, end, step, fwd, gap, horizon, ibors, dc,
                                                                     accuracy);
                 };
             }},
        });

    }

}
