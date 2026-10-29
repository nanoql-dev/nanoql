// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/basismodels/swaptioncfs.hpp, tenoroptionletvts.hpp and tenorswaptionvts.hpp (and their .cpp files):
// the tenor basis models, which the wheel doesn't bind, on test-suite/basismodels.cpp's markets.
//
// A curve is {"dates", "rates"}: QuantLib's InterpolatedZeroCurve<Cubic> on Actual/365 (Fixed) and NullCalendar, as the
// test's getYTS builds it. An index is {"tenor": "3M" or "6M", "curve"}: Euribor3M or Euribor6M on the curve.
//
// "SwaptionCashFlows::flows" takes (discount, projection, swap, exercise, contTenorSpread): the curves, a swap
// {"start", "end", "nominal", "fixedRate"} (QuantLib's case's vanilla payer swap: annual Thirty/360 (Bond Basis) fixed
// against Euribor 6M, Modified Following on TARGET, backward), the exercise date and the flag; it returns
// {"floatTimes", "floatWeights", "fixedTimes", "fixedWeights", "annuityWeights", "exerciseTimes"}.
//
// "TenorOptionletVTS::volatility" takes (base, baseIndex, targIndex, correlation, tenors, strikes): the base optionlet
// structure {"curve", "dates", "strikes", "volatilities"} (QuantLib's case's normal StrippedOptionlet on Euribor 3M with
// two settlement days, TARGET, Following and Actual/365 (Fixed), adapted), the indexes, the correlation {"rhoInf",
// "beta"} (constant, a two-node linear interpolation on [0, 50] each), and the option tenors and strikes; it returns
// {"volatility": one array per tenor, one value per strike, read with extrapolation; "sectionVolatility": the same
// through the smile section at the tenor's option date, built with extrapolation; "atmLevel", "minStrike", "maxStrike":
// that smile section's}.
//
// "TenorSwaptionVTS::volatility" takes (base, discount, baseIndex, targIndex, optionTenors, swapTenors, strikes): the
// base swaption structure {"optionTenors", "swapTenors", "volatilities"} (QuantLib's case's normal
// SwaptionVolatilityMatrix on TARGET, Following, Actual/365 (Fixed), flat-extrapolated), the curve, the indexes and the
// tenors ([length, units] each) and strikes; both fixed legs annual Thirty/360 (Bond Basis), as the case's. It returns
// {"volatility": per option tenor, per swap tenor, per strike, with extrapolation; "atmLevel": per option tenor, per
// swap tenor}.
//
// QuantLib's refusals at a read are {"throws": true}.

#include "convert.hpp"

#include <ql/experimental/basismodels/swaptioncfs.hpp>
#include <ql/experimental/basismodels/tenoroptionletvts.hpp>
#include <ql/experimental/basismodels/tenorswaptionvts.hpp>
#include <ql/exercise.hpp>
#include <ql/indexes/ibor/euribor.hpp>
#include <ql/instruments/vanillaswap.hpp>
#include <ql/math/interpolations/cubicinterpolation.hpp>
#include <ql/math/interpolations/linearinterpolation.hpp>
#include <ql/pricingengines/swap/discountingswapengine.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/optionlet/strippedoptionlet.hpp>
#include <ql/termstructures/volatility/optionlet/strippedoptionletadapter.hpp>
#include <ql/termstructures/volatility/swaption/swaptionvolmatrix.hpp>
#include <ql/termstructures/yield/zerocurve.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/calendars/target.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>
#include <ql/time/daycounters/thirty360.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Curve {
            std::vector<DateSpec> dates;
            std::vector<Real> rates;
        };

        std::vector<DateSpec> dateList(const json::value& v) {
            std::vector<DateSpec> out;
            for (const auto& d : v.as_array())
                out.push_back(date(d));
            return out;
        }

        Curve curve(const json::value& v) {
            const json::object& o = v.as_object();
            return {dateList(o.at("dates")), reals(o.at("rates"))};
        }

        Handle<YieldTermStructure> makeCurve(const Curve& c) {
            std::vector<Date> d;
            for (const auto& x : c.dates)
                d.push_back(makeDate(x));
            return Handle<YieldTermStructure>(
                ext::make_shared<InterpolatedZeroCurve<Cubic>>(d, c.rates, Actual365Fixed(), NullCalendar()));
        }

        struct Index {
            std::string tenor;
            Curve curve;
        };

        Index index(const json::value& v) {
            const json::object& o = v.as_object();
            Index i{text(o.at("tenor")), curve(o.at("curve"))};
            if (i.tenor != "3M" && i.tenor != "6M")
                throw InvalidParams("an index's tenor is 3M or 6M");
            return i;
        }

        ext::shared_ptr<IborIndex> makeIndex(const Index& i) {
            if (i.tenor == "3M")
                return ext::make_shared<Euribor3M>(makeCurve(i.curve));
            return ext::make_shared<Euribor6M>(makeCurve(i.curve));
        }

        std::vector<std::vector<Real>> rows(const json::value& v) {
            std::vector<std::vector<Real>> out;
            for (const auto& r : v.as_array())
                out.push_back(reals(r));
            return out;
        }

        std::vector<Period> periods(const json::value& v) {
            std::vector<Period> out;
            for (const auto& p : v.as_array())
                out.emplace_back(integer(p.as_array().at(0)), enumeration<TimeUnit>(p.as_array().at(1)));
            return out;
        }

        json::array array(const std::vector<Real>& x) {
            json::array out;
            for (Real r : x)
                out.emplace_back(r);
            return out;
        }

        template <class F>
        json::value attempt(F f) {
            try {
                return f();
            } catch (const QuantLib::Error&) {
                return json::object{{"throws", true}};
            }
        }

        const Register unit({
            // (discount, projection, swap, exercise, contTenorSpread)
            {"SwaptionCashFlows::flows",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Curve disc = curve(a[0]);
                 Curve proj = curve(a[1]);
                 const json::object& s = a[2].as_object();
                 DateSpec start = date(s.at("start")), end = date(s.at("end"));
                 Real nominal = real(s.at("nominal")), fixedRate = real(s.at("fixedRate"));
                 DateSpec exercise = date(a[3]);
                 bool cont = boolean(a[4]);
                 return [=] {
                     Handle<YieldTermStructure> discYTS = makeCurve(disc);
                     auto euribor6m = ext::make_shared<Euribor6M>(makeCurve(proj));
                     Schedule fixedSchedule(makeDate(start), makeDate(end), Period(1, Years), TARGET(),
                                            ModifiedFollowing, ModifiedFollowing, DateGeneration::Backward, false);
                     Schedule floatSchedule(makeDate(start), makeDate(end), Period(6, Months), TARGET(),
                                            ModifiedFollowing, ModifiedFollowing, DateGeneration::Backward, false);
                     auto swap = ext::make_shared<VanillaSwap>(Swap::Payer, nominal, fixedSchedule, fixedRate,
                                                               Thirty360(Thirty360::BondBasis), floatSchedule,
                                                               euribor6m, 0.0, euribor6m->dayCounter());
                     auto swaption = ext::make_shared<Swaption>(
                         swap, ext::make_shared<EuropeanExercise>(makeDate(exercise)), Settlement::Physical);
                     SwaptionCashFlows cfs(swaption, discYTS, cont);
                     json::object out;
                     out["floatTimes"] = array(cfs.floatTimes());
                     out["floatWeights"] = array(cfs.floatWeights());
                     out["fixedTimes"] = array(cfs.fixedTimes());
                     out["fixedWeights"] = array(cfs.fixedWeights());
                     out["annuityWeights"] = array(cfs.annuityWeights());
                     out["exerciseTimes"] = array(cfs.exerciseTimes());
                     return json::value(out);
                 };
             }},
            // (base, baseIndex, targIndex, correlation, tenors, strikes)
            {"TenorOptionletVTS::volatility",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 const json::object& b = a[0].as_object();
                 Curve baseCurve = curve(b.at("curve"));
                 std::vector<DateSpec> dates = dateList(b.at("dates"));
                 std::vector<Real> baseStrikes = reals(b.at("strikes"));
                 std::vector<std::vector<Real>> vols = rows(b.at("volatilities"));
                 Index bi = index(a[1]), ti = index(a[2]);
                 Real rhoInf = real(a[3].as_object().at("rhoInf")), beta = real(a[3].as_object().at("beta"));
                 std::vector<Period> tenors = periods(a[4]);
                 std::vector<Real> strikes = reals(a[5]);
                 return [=] {
                     auto index3m = ext::make_shared<Euribor3M>(makeCurve(baseCurve));
                     std::vector<Date> d;
                     for (const auto& x : dates)
                         d.push_back(makeDate(x));
                     std::vector<std::vector<Handle<Quote>>> quotes;
                     for (const auto& row : vols) {
                         std::vector<Handle<Quote>> q;
                         for (Real x : row)
                             q.emplace_back(ext::make_shared<SimpleQuote>(x));
                         quotes.push_back(q);
                     }
                     auto stripped = ext::make_shared<StrippedOptionlet>(2, TARGET(), Following, index3m, d,
                                                                         baseStrikes, quotes, Actual365Fixed(), Normal,
                                                                         0.0);
                     Handle<OptionletVolatilityStructure> base(ext::make_shared<StrippedOptionletAdapter>(stripped));
                     std::vector<Real> t = {0.0, 50.0}, r = {rhoInf, rhoInf}, bt = {beta, beta};
                     auto rho = ext::make_shared<LinearInterpolation>(t.begin(), t.end(), r.begin());
                     auto be = ext::make_shared<LinearInterpolation>(t.begin(), t.end(), bt.begin());
                     auto corr = ext::make_shared<TenorOptionletVTS::TwoParameterCorrelation>(rho, be);
                     TenorOptionletVTS vts(base, makeIndex(bi), makeIndex(ti), corr);
                     json::array volatility, section, atm, minK, maxK;
                     for (const auto& p : tenors) {
                         json::array row, sectionRow;
                         for (Real k : strikes) {
                             row.emplace_back(attempt([&] { return json::value(vts.volatility(p, k, true)); }));
                             sectionRow.emplace_back(attempt([&] {
                                 return json::value(vts.smileSection(vts.optionDateFromTenor(p), true)->volatility(k));
                             }));
                         }
                         volatility.emplace_back(row);
                         section.emplace_back(sectionRow);
                         atm.emplace_back(attempt([&] {
                             return json::value(vts.smileSection(vts.optionDateFromTenor(p), true)->atmLevel());
                         }));
                         minK.emplace_back(attempt([&] {
                             return json::value(vts.smileSection(vts.optionDateFromTenor(p), true)->minStrike());
                         }));
                         maxK.emplace_back(attempt([&] {
                             return json::value(vts.smileSection(vts.optionDateFromTenor(p), true)->maxStrike());
                         }));
                     }
                     json::object out;
                     out["volatility"] = volatility;
                     out["sectionVolatility"] = section;
                     out["atmLevel"] = atm;
                     out["minStrike"] = minK;
                     out["maxStrike"] = maxK;
                     return json::value(out);
                 };
             }},
            // (base, discount, baseIndex, targIndex, optionTenors, swapTenors, strikes)
            {"TenorSwaptionVTS::volatility",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 const json::object& b = a[0].as_object();
                 std::vector<Period> baseOptions = periods(b.at("optionTenors"));
                 std::vector<Period> baseSwaps = periods(b.at("swapTenors"));
                 std::vector<std::vector<Real>> vols = rows(b.at("volatilities"));
                 Curve disc = curve(a[1]);
                 Index bi = index(a[2]), ti = index(a[3]);
                 std::vector<Period> optionTenors = periods(a[4]);
                 std::vector<Period> swapTenors = periods(a[5]);
                 std::vector<Real> strikes = reals(a[6]);
                 return [=] {
                     std::vector<std::vector<Handle<Quote>>> quotes;
                     for (const auto& row : vols) {
                         std::vector<Handle<Quote>> q;
                         for (Real x : row)
                             q.emplace_back(ext::make_shared<SimpleQuote>(x));
                         quotes.push_back(q);
                     }
                     Handle<SwaptionVolatilityStructure> base(ext::make_shared<SwaptionVolatilityMatrix>(
                         TARGET(), Following, baseOptions, baseSwaps, quotes, Actual365Fixed(), true, Normal));
                     TenorSwaptionVTS vts(base, makeCurve(disc), makeIndex(bi), makeIndex(ti), Period(1, Years),
                                          Period(1, Years), Thirty360(Thirty360::BondBasis),
                                          Thirty360(Thirty360::BondBasis));
                     json::array volatility, atm;
                     for (const auto& o : optionTenors) {
                         json::array perSwap, atmRow;
                         for (const auto& s : swapTenors) {
                             json::array row;
                             for (Real k : strikes)
                                 row.emplace_back(attempt([&] { return json::value(vts.volatility(o, s, k, true)); }));
                             perSwap.emplace_back(row);
                             atmRow.emplace_back(attempt([&] {
                                 return json::value(vts.smileSection(o, s, true)->atmLevel());
                             }));
                         }
                         volatility.emplace_back(perSwap);
                         atm.emplace_back(atmRow);
                     }
                     json::object out;
                     out["volatility"] = volatility;
                     out["atmLevel"] = atm;
                     return json::value(out);
                 };
             }},
        });

    }

}
