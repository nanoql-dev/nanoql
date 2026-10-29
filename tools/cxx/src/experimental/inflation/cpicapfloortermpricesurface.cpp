// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/inflation/cpicapfloortermpricesurface.hpp, .cpp and cpicapfloorengines.hpp, .cpp: the CPI cap and
// floor price surface and its interpolating engine, which the wheel doesn't bind.
// "CPICapFloorTermPriceSurface::values" takes (surface, tenors, strikes, dates): a surface spec (below), the tenors and
// quoted strikes the prices are read at, and the dates the at-the-money rate is read at; it builds the surface inside
// the call, on the case's evaluation date and fixings, and returns {"strikes", "optionDates" (cpiOptionDateFromTenor
// of each maturity), "minDate", "maxDate", "baseDate", "capPrice", "floorPrice", "price" (one array per tenor, one
// price per strike), "atmRate" (one per date)}. "InterpolatingCPICapFloorEngine::npv" takes (surface, capFloors): the
// surface and the instruments, each {"type" (QuantLib's Option::Type integer), "nominal", "startDate", "baseCPI",
// "maturity", "fixCalendar", "fixConvention", "payCalendar", "payConvention", "strike", "observationLag",
// "observationInterpolation"}, on the surface's index; it returns each instrument's NPV.
//
// A surface is {"interpolator" ("Bilinear" or "Bicubic"), "nominal", "startRate", "observationLag" ([length, units]),
// "calendar", "convention", "dayCounter", "index" (cashflows/inflationspec.hpp's zero-inflation index), "interpolation"
// (QuantLib's CPI::InterpolationType integer), "nominalCurve" (indexes/indexspec.hpp's curve), "capStrikes",
// "floorStrikes", "maturities" ([length, units] each), "capPrices" and "floorPrices" (a row per strike, a column per
// maturity)}. QuantLib's refusals, at construction or at a read, are the generator's {"throws": true}.

#include "cashflows/inflationspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/experimental/inflation/cpicapfloorengines.hpp>
#include <ql/experimental/inflation/cpicapfloortermpricesurface.hpp>
#include <ql/instruments/cpicapfloor.hpp>
#include <ql/math/interpolations/bicubicsplineinterpolation.hpp>
#include <ql/math/interpolations/bilinearinterpolation.hpp>
#include <ql/math/matrix.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct PeriodSpec {
            Integer length = 0;
            TimeUnit units = Days;
        };

        PeriodSpec periodSpec(const json::value& v) {
            if (!v.is_array() || v.as_array().size() != 2)
                throw InvalidParams("a period is [length, units]");
            return {integer(v.as_array()[0]), enumeration<TimeUnit>(v.as_array()[1])};
        }

        Period makePeriod(const PeriodSpec& p) { return {p.length, p.units}; }

        std::vector<std::vector<Real>> rows(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("a matrix is an array of rows");
            std::vector<std::vector<Real>> out;
            for (const auto& r : v.as_array())
                out.push_back(reals(r));
            return out;
        }

        Matrix makeMatrix(const std::vector<std::vector<Real>>& r) {
            Matrix m(r.size(), r.empty() ? 0 : r[0].size());
            for (Size i = 0; i < r.size(); ++i) {
                if (r[i].size() != m.columns())
                    throw InvalidParams("a matrix's rows have one length");
                for (Size j = 0; j < r[i].size(); ++j)
                    m[i][j] = r[i][j];
            }
            return m;
        }

        struct SurfaceSpec {
            std::string interpolator;
            Real nominal = 0.0;
            Real startRate = 0.0;
            PeriodSpec observationLag;
            indexes::CalendarSpec calendar;
            BusinessDayConvention convention = Following;
            DayCounterSpec dayCounter;
            cashflows::InflationIndexSpec index;
            CPI::InterpolationType interpolation = CPI::Flat;
            indexes::CurveSpec nominalCurve;
            std::vector<Real> capStrikes, floorStrikes;
            std::vector<PeriodSpec> maturities;
            std::vector<std::vector<Real>> capPrices, floorPrices;
        };

        SurfaceSpec surface(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("a surface is an object");
            const json::object& o = v.as_object();
            SurfaceSpec s;
            s.interpolator = text(o.at("interpolator"));
            if (s.interpolator != "Bilinear" && s.interpolator != "Bicubic")
                throw InvalidParams("unknown interpolator " + s.interpolator);
            s.nominal = real(o.at("nominal"));
            s.startRate = real(o.at("startRate"));
            s.observationLag = periodSpec(o.at("observationLag"));
            s.calendar = indexes::calendar(o.at("calendar"));
            s.convention = enumeration<BusinessDayConvention>(o.at("convention"));
            s.dayCounter = dayCounter(o.at("dayCounter"));
            s.index = cashflows::inflationIndex(o.at("index"));
            s.interpolation = enumeration<CPI::InterpolationType>(o.at("interpolation"));
            s.nominalCurve = indexes::curve(o.at("nominalCurve"));
            s.capStrikes = reals(o.at("capStrikes"));
            s.floorStrikes = reals(o.at("floorStrikes"));
            for (const auto& m : o.at("maturities").as_array())
                s.maturities.push_back(periodSpec(m));
            s.capPrices = rows(o.at("capPrices"));
            s.floorPrices = rows(o.at("floorPrices"));
            return s;
        }

        ext::shared_ptr<CPICapFloorTermPriceSurface> makeSurface(const SurfaceSpec& s) {
            std::vector<Period> maturities;
            for (const auto& m : s.maturities)
                maturities.push_back(makePeriod(m));
            auto zii = cashflows::makeZeroIndex(s.index);
            Calendar cal = indexes::makeCalendar(s.calendar);
            DayCounter dc = makeDayCounter(s.dayCounter);
            Handle<YieldTermStructure> yts = indexes::makeCurve(s.nominalCurve);
            Matrix cPrice = makeMatrix(s.capPrices), fPrice = makeMatrix(s.floorPrices);
            if (s.interpolator == "Bilinear")
                return ext::make_shared<InterpolatedCPICapFloorTermPriceSurface<Bilinear>>(
                    s.nominal, s.startRate, makePeriod(s.observationLag), cal, s.convention, dc, zii, s.interpolation,
                    yts, s.capStrikes, s.floorStrikes, maturities, cPrice, fPrice);
            return ext::make_shared<InterpolatedCPICapFloorTermPriceSurface<Bicubic>>(
                s.nominal, s.startRate, makePeriod(s.observationLag), cal, s.convention, dc, zii, s.interpolation, yts,
                s.capStrikes, s.floorStrikes, maturities, cPrice, fPrice);
        }

        template <class F>
        json::value attempt(F f) {
            try {
                return f();
            } catch (const QuantLib::Error&) {
                return json::object{{"throws", true}};
            }
        }

        struct CapFloorSpec {
            Option::Type type = Option::Call;
            Real nominal = 0.0, baseCPI = 0.0, strike = 0.0;
            DateSpec startDate{true, 0}, maturity{true, 0};
            indexes::CalendarSpec fixCalendar, payCalendar;
            BusinessDayConvention fixConvention = Unadjusted, payConvention = Following;
            PeriodSpec observationLag;
            CPI::InterpolationType observationInterpolation = CPI::Flat;
        };

        CapFloorSpec capFloor(const json::value& v) {
            const json::object& o = v.as_object();
            CapFloorSpec c;
            c.type = enumeration<Option::Type>(o.at("type"));
            c.nominal = real(o.at("nominal"));
            c.startDate = date(o.at("startDate"));
            c.baseCPI = real(o.at("baseCPI"));
            c.maturity = date(o.at("maturity"));
            c.fixCalendar = indexes::calendar(o.at("fixCalendar"));
            c.fixConvention = enumeration<BusinessDayConvention>(o.at("fixConvention"));
            c.payCalendar = indexes::calendar(o.at("payCalendar"));
            c.payConvention = enumeration<BusinessDayConvention>(o.at("payConvention"));
            c.strike = real(o.at("strike"));
            c.observationLag = periodSpec(o.at("observationLag"));
            c.observationInterpolation = enumeration<CPI::InterpolationType>(o.at("observationInterpolation"));
            return c;
        }

        const Register unit({
            // (surface, tenors, strikes, dates)
            {"CPICapFloorTermPriceSurface::values",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 SurfaceSpec s = surface(a[0]);
                 std::vector<PeriodSpec> tenors;
                 for (const auto& t : a[1].as_array())
                     tenors.push_back(periodSpec(t));
                 std::vector<Real> strikes = reals(a[2]);
                 std::vector<DateSpec> dates;
                 for (const auto& d : a[3].as_array())
                     dates.push_back(date(d));
                 return [=] {
                     auto surf = makeSurface(s);
                     json::array optionDates, caps, floors, prices, atm;
                     for (const auto& m : s.maturities)
                         optionDates.emplace_back(result(surf->cpiOptionDateFromTenor(makePeriod(m))));
                     for (const auto& t : tenors) {
                         json::array c, f, p;
                         for (Real k : strikes) {
                             c.emplace_back(attempt([&] { return json::value(surf->capPrice(makePeriod(t), k)); }));
                             f.emplace_back(attempt([&] { return json::value(surf->floorPrice(makePeriod(t), k)); }));
                             p.emplace_back(attempt([&] { return json::value(surf->price(makePeriod(t), k)); }));
                         }
                         caps.emplace_back(c);
                         floors.emplace_back(f);
                         prices.emplace_back(p);
                     }
                     for (const auto& d : dates)
                         atm.emplace_back(attempt([&] { return json::value(surf->atmRate(makeDate(d))); }));
                     json::array ks;
                     for (Real k : surf->strikes())
                         ks.emplace_back(k);
                     json::object out;
                     out["strikes"] = ks;
                     out["optionDates"] = optionDates;
                     out["minDate"] = result(surf->minDate());
                     out["maxDate"] = result(surf->maxDate());
                     out["baseDate"] = result(surf->baseDate());
                     out["capPrice"] = caps;
                     out["floorPrice"] = floors;
                     out["price"] = prices;
                     out["atmRate"] = atm;
                     return json::value(out);
                 };
             }},
            // (surface, capFloors)
            {"InterpolatingCPICapFloorEngine::npv",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 SurfaceSpec s = surface(a[0]);
                 std::vector<CapFloorSpec> instruments;
                 for (const auto& c : a[1].as_array())
                     instruments.push_back(capFloor(c));
                 return [=] {
                     auto surf = makeSurface(s);
                     auto zii = cashflows::makeZeroIndex(s.index);
                     auto engine =
                         ext::make_shared<InterpolatingCPICapFloorEngine>(Handle<CPICapFloorTermPriceSurface>(surf));
                     json::array npvs;
                     for (const auto& c : instruments) {
                         npvs.emplace_back(attempt([&] {
                             CPICapFloor cf(c.type, c.nominal, makeDate(c.startDate), c.baseCPI, makeDate(c.maturity),
                                            indexes::makeCalendar(c.fixCalendar), c.fixConvention,
                                            indexes::makeCalendar(c.payCalendar), c.payConvention, c.strike, zii,
                                            makePeriod(c.observationLag), c.observationInterpolation);
                             cf.setPricingEngine(engine);
                             return json::value(cf.NPV());
                         }));
                     }
                     return json::value(npvs);
                 };
             }},
        });

    }

}
