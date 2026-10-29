// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/inflation/yoyinflationoptionletvolatilitystructure2.hpp, piecewiseyoyoptionletvolatility.hpp and
// yoycapfloortermpricesurface.hpp: what the wheel can't reach of the year-on-year volatility structures.
//
// "InterpolatedYoYOptionletVolatilityCurve::values" takes (curve, dates, strike): a curve {"settlementDays",
// "calendar", "convention", "dayCounter", "lag" ([length, units]), "frequency", "indexIsInterpolated", "dates",
// "volatilities", "minStrike", "maxStrike"}, linear in time, built on the case's evaluation date; it returns
// {"referenceDate", "baseDate", "baseLevel", "maxDate", "times", "dates", "data", "nodes" ([[date, value], ...]),
// "volatility" (each date at the strike, with extrapolation), "totalVariance" (likewise)}.
//
// "YoYCapFloorTermPriceSurface::dateRates" takes (market, surface): the euro market and the surface of
// tools/vectors/gen/experimental/inflation/yoycapfloortermpricesurface.py, bicubic and cubic as the wheel's; it returns
// {"atmYoYSwapDateRates": [dates, rates]}, the pair the wheel's binding can't return.
//
// "PiecewiseYoYOptionletVolatilityCurve::strip" takes (market, surface, slope, dates): the same, stripped by the
// k-interpolated surface with QuantLib's InterpolatedYoYOptionletStripper<Linear> on the unit-displaced Black engine;
// it returns {"referenceDate", "baseDate", "baseLevel", "maxDate", "times", "dates", "data", "volatility" (each date
// at the curve's strike, through its own date route, with extrapolation)}, one entry per stripped curve, read from the
// stripper's curves through a subclass, as QuantLib's master's test reads them (#2941). Their nodes() are left out:
// 1.44's are empty for a bootstrapped curve.

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/experimental/inflation/interpolatedyoyoptionletstripper.hpp>
#include <ql/experimental/inflation/kinterpolatedyoyoptionletvolatilitysurface.hpp>
#include <ql/experimental/inflation/yoycapfloortermpricesurface.hpp>
#include <ql/experimental/inflation/yoyinflationoptionletvolatilitystructure2.hpp>
#include <ql/indexes/inflation/euhicp.hpp>
#include <ql/math/interpolations/bicubicsplineinterpolation.hpp>
#include <ql/math/interpolations/cubicinterpolation.hpp>
#include <ql/math/interpolations/linearinterpolation.hpp>
#include <ql/pricingengines/inflation/inflationcapfloorengines.hpp>
#include <ql/termstructures/inflation/interpolatedyoyinflationcurve.hpp>
#include <ql/termstructures/yield/zerocurve.hpp>
#include <ql/time/calendars/target.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct CurveSpec {
            Natural settlementDays = 0;
            indexes::CalendarSpec calendar;
            BusinessDayConvention convention = Following;
            DayCounterSpec dayCounter;
            Integer lagLength = 0;
            TimeUnit lagUnits = Months;
            Frequency frequency = Monthly;
            bool indexIsInterpolated = false;
            std::vector<DateSpec> dates;
            std::vector<Real> volatilities;
            Real minStrike = 0.0, maxStrike = 0.0;
        };

        std::vector<DateSpec> dates(const json::value& v) {
            std::vector<DateSpec> out;
            for (const auto& d : v.as_array())
                out.push_back(date(d));
            return out;
        }

        CurveSpec curve(const json::value& v) {
            const json::object& o = v.as_object();
            CurveSpec s;
            Integer days = integer(o.at("settlementDays"));
            if (days < 0)
                throw InvalidParams("settlementDays is a Natural");
            s.settlementDays = static_cast<Natural>(days);
            s.calendar = indexes::calendar(o.at("calendar"));
            s.convention = enumeration<BusinessDayConvention>(o.at("convention"));
            s.dayCounter = dayCounter(o.at("dayCounter"));
            const json::array& lag = o.at("lag").as_array();
            s.lagLength = integer(lag.at(0));
            s.lagUnits = enumeration<TimeUnit>(lag.at(1));
            s.frequency = enumeration<Frequency>(o.at("frequency"));
            s.indexIsInterpolated = boolean(o.at("indexIsInterpolated"));
            s.dates = dates(o.at("dates"));
            s.volatilities = reals(o.at("volatilities"));
            s.minStrike = real(o.at("minStrike"));
            s.maxStrike = real(o.at("maxStrike"));
            return s;
        }

        // The euro market and surface specs of tools/vectors/gen/experimental/inflation/yoycapfloortermpricesurface.py.
        struct Market {
            std::vector<DateSpec> nominalDates;
            std::vector<Real> nominalRates;
            DateSpec yoyReference{true, 0};
            std::vector<DateSpec> yoyDates;
            std::vector<Real> yoyRates;
        };

        Market market(const json::value& v) {
            const json::object& o = v.as_object();
            Market m;
            const json::object& n = o.at("nominal").as_object();
            m.nominalDates = dates(n.at("dates"));
            m.nominalRates = reals(n.at("rates"));
            const json::object& y = o.at("yoy").as_object();
            m.yoyReference = date(y.at("referenceDate"));
            m.yoyDates = dates(y.at("dates"));
            m.yoyRates = reals(y.at("rates"));
            return m;
        }

        struct Surface {
            Natural fixingDays = 0;
            Integer lagLength = 0;
            TimeUnit lagUnits = Months;
            CPI::InterpolationType interpolation = CPI::Flat;
            std::vector<Real> capStrikes, floorStrikes;
            std::vector<std::pair<Integer, TimeUnit>> maturities;
            std::vector<std::vector<Real>> capPrices, floorPrices;
        };

        Surface surface(const json::value& v) {
            const json::object& o = v.as_object();
            Surface s;
            s.fixingDays = static_cast<Natural>(integer(o.at("fixingDays")));
            const json::array& lag = o.at("lag").as_array();
            s.lagLength = integer(lag.at(0));
            s.lagUnits = enumeration<TimeUnit>(lag.at(1));
            s.interpolation = enumeration<CPI::InterpolationType>(o.at("interpolation"));
            s.capStrikes = reals(o.at("capStrikes"));
            s.floorStrikes = reals(o.at("floorStrikes"));
            for (const auto& m : o.at("maturities").as_array())
                s.maturities.emplace_back(integer(m.as_array().at(0)), enumeration<TimeUnit>(m.as_array().at(1)));
            for (const auto& r : o.at("capPrices").as_array())
                s.capPrices.push_back(reals(r));
            for (const auto& r : o.at("floorPrices").as_array())
                s.floorPrices.push_back(reals(r));
            return s;
        }

        Matrix matrix(const std::vector<std::vector<Real>>& r) {
            Matrix m(r.size(), r.at(0).size());
            for (Size i = 0; i < r.size(); ++i)
                for (Size j = 0; j < r[i].size(); ++j)
                    m[i][j] = r[i][j];
            return m;
        }

        std::vector<Date> makeDates(const std::vector<DateSpec>& d) {
            std::vector<Date> out;
            for (const auto& x : d)
                out.push_back(makeDate(x));
            return out;
        }

        struct Built {
            Handle<YieldTermStructure> nominal;
            ext::shared_ptr<YoYInflationIndex> index;
            ext::shared_ptr<YoYCapFloorTermPriceSurface> surface;
        };

        Built build(const Market& m, const Surface& s) {
            Built b;
            b.nominal = Handle<YieldTermStructure>(ext::make_shared<InterpolatedZeroCurve<Cubic>>(
                makeDates(m.nominalDates), m.nominalRates, Actual365Fixed()));
            auto yoy = ext::make_shared<InterpolatedYoYInflationCurve<Linear>>(
                makeDate(m.yoyReference), makeDates(m.yoyDates), m.yoyRates, Monthly, Actual365Fixed());
            b.index = ext::make_shared<YoYInflationIndex>(ext::make_shared<EUHICP>(),
                                                          Handle<YoYInflationTermStructure>(yoy));
            std::vector<Period> maturities;
            for (const auto& p : s.maturities)
                maturities.emplace_back(p.first, p.second);
            b.surface = ext::make_shared<InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic>>(
                s.fixingDays, Period(s.lagLength, s.lagUnits), b.index, s.interpolation, b.nominal, Actual365Fixed(),
                TARGET(), ModifiedFollowing, s.capStrikes, s.floorStrikes, maturities, matrix(s.capPrices),
                matrix(s.floorPrices));
            return b;
        }

        // The stripper's curves, as QuantLib's master's testYoYPriceSurfaceToVol reads them.
        class InspectableStripper : public InterpolatedYoYOptionletStripper<Linear> {
          public:
            const std::vector<ext::shared_ptr<YoYOptionletVolatilitySurface>>& curves() const { return volCurves_; }
        };

        template <class C>
        json::object inspect(const C& c) {
            json::array times, ds, data;
            for (Real t : c.times())
                times.emplace_back(t);
            for (const Date& d : c.dates())
                ds.emplace_back(result(d));
            for (Real x : c.data())
                data.emplace_back(x);
            json::object out;
            out["referenceDate"] = result(c.referenceDate());
            out["baseDate"] = result(c.baseDate());
            out["baseLevel"] = c.baseLevel();
            out["maxDate"] = result(c.maxDate());
            out["times"] = times;
            out["dates"] = ds;
            out["data"] = data;
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
            // (curve, dates, strike)
            {"InterpolatedYoYOptionletVolatilityCurve::values",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 CurveSpec s = curve(a[0]);
                 std::vector<DateSpec> at = dates(a[1]);
                 Real strike = real(a[2]);
                 return [=] {
                     InterpolatedYoYOptionletVolatilityCurve<Linear> c(
                         s.settlementDays, indexes::makeCalendar(s.calendar), s.convention,
                         makeDayCounter(s.dayCounter), Period(s.lagLength, s.lagUnits), s.frequency,
                         s.indexIsInterpolated, makeDates(s.dates), s.volatilities, s.minStrike, s.maxStrike);
                     json::object out = inspect(c);
                     json::array nodes, vols, variances;
                     for (const auto& n : c.nodes())
                         nodes.emplace_back(json::array{result(n.first), n.second});
                     for (const auto& d : at) {
                         vols.emplace_back(attempt([&] {
                             return json::value(c.volatility(makeDate(d), strike, Period(-1, Days), true));
                         }));
                         variances.emplace_back(attempt([&] {
                             return json::value(c.totalVariance(makeDate(d), strike, Period(-1, Days), true));
                         }));
                     }
                     out["nodes"] = nodes;
                     out["volatility"] = vols;
                     out["totalVariance"] = variances;
                     return json::value(out);
                 };
             }},
            // (market, surface)
            {"YoYCapFloorTermPriceSurface::dateRates",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Market m = market(a[0]);
                 Surface s = surface(a[1]);
                 return [=] {
                     Built b = build(m, s);
                     auto rates = b.surface->atmYoYSwapDateRates();
                     json::array ds, rs;
                     for (const Date& d : rates.first)
                         ds.emplace_back(result(d));
                     for (Real r : rates.second)
                         rs.emplace_back(r);
                     json::object out;
                     out["atmYoYSwapDateRates"] = json::array{ds, rs};
                     return json::value(out);
                 };
             }},
            // (market, surface, slope, dates)
            {"PiecewiseYoYOptionletVolatilityCurve::strip",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Market m = market(a[0]);
                 Surface s = surface(a[1]);
                 Real slope = real(a[2]);
                 std::vector<DateSpec> at = dates(a[3]);
                 return [=] {
                     Built b = build(m, s);
                     RelinkableHandle<YoYOptionletVolatilitySurface> hvs;
                     auto pricer = ext::make_shared<YoYInflationUnitDisplacedBlackCapFloorEngine>(b.index, hvs,
                                                                                                   b.nominal);
                     auto stripper = ext::make_shared<InspectableStripper>();
                     KInterpolatedYoYOptionletVolatilitySurface<Linear> k(
                         0, TARGET(), ModifiedFollowing, Actual365Fixed(), b.surface->observationLag(), b.surface,
                         pricer, stripper, slope);
                     // one array per field, an entry per stripped curve
                     json::object out;
                     for (const char* key : {"referenceDate", "baseDate", "baseLevel", "maxDate", "times", "dates",
                                             "data", "volatility"})
                         out[key] = json::array();
                     std::vector<Real> strikes = b.surface->strikes();
                     for (Size i = 0; i < stripper->curves().size(); ++i) {
                         auto pw = ext::dynamic_pointer_cast<PiecewiseYoYOptionletVolatilityCurve<Linear>>(
                             stripper->curves()[i]);
                         json::object one = inspect(*pw);
                         for (const auto& kv : one)
                             out[kv.key()].as_array().emplace_back(kv.value());
                         json::array vols;
                         for (const auto& d : at)
                             vols.emplace_back(attempt([&] {
                                 return json::value(pw->volatility(makeDate(d), strikes[i], Period(-1, Days), true));
                             }));
                         out["volatility"].as_array().emplace_back(vols);
                     }
                     return json::value(out);
                 };
             }},
        });

    }

}
