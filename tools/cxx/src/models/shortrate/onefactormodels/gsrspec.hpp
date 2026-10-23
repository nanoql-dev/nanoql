// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The GSR model spec the Gaussian1d methods share (ql/models/shortrate/onefactormodels/gsr.hpp), with the vectors'
// input names (tools/vectors/gen/pricingengines/swaption/gaussian1dswaptionengine.py's specs, as
// tests/NanoQL.Tests/Vectors/Gaussian1dInputs.cs documents them): {"curve", "volstepdates", "volatilities",
// "reversions", "T"}, its curve a flat forward spec of indexes/indexspec.hpp, its step dates serial numbers, built with
// Gsr's constructor on quotes, as the wheel's.

#ifndef NANOQL_CXX_MODELS_SHORTRATE_ONEFACTORMODELS_GSRSPEC_HPP
#define NANOQL_CXX_MODELS_SHORTRATE_ONEFACTORMODELS_GSRSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/models/shortrate/onefactormodels/gsr.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/yield/discountcurve.hpp>

#include <vector>

namespace harness::gaussian1d {

    struct GsrSpec {
        indexes::CurveSpec curve;
        std::vector<DateSpec> volstepdates;
        std::vector<QuantLib::Real> volatilities;
        std::vector<QuantLib::Real> reversions;
        QuantLib::Real T = 60.0;
    };

    inline GsrSpec gsr(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a GSR spec, got " + json::serialize(v));
        const json::object& o = v.as_object();
        GsrSpec s;
        s.curve = indexes::curve(o.at("curve"));
        for (const auto& d : o.at("volstepdates").as_array())
            s.volstepdates.push_back(date(d));
        s.volatilities = reals(o.at("volatilities"));
        s.reversions = reals(o.at("reversions"));
        s.T = real(o.at("T"));
        return s;
    }

    // A discount curve given to an engine: null, QuantLib's empty handle; a flat forward spec of indexes/indexspec.hpp;
    // or {"dates", "discounts", "dayCounter"}, QuantLib's DiscountCurve on those nodes, a market restated on another
    // clock (tests/NanoQL.Tests/Divergences/Gaussian1dMismatchedTimesTests.cs).
    struct DiscountSpec {
        bool nodes = false;
        indexes::CurveSpec flat;
        std::vector<DateSpec> dates;
        std::vector<QuantLib::Real> discounts;
        DayCounterSpec dayCounter;
    };

    inline DiscountSpec discount(const json::value& v) {
        DiscountSpec s;
        if (v.is_object() && v.as_object().contains("dates")) {
            const json::object& o = v.as_object();
            s.nodes = true;
            for (const auto& d : o.at("dates").as_array())
                s.dates.push_back(date(d));
            s.discounts = reals(o.at("discounts"));
            s.dayCounter = harness::dayCounter(o.at("dayCounter"));
        } else {
            s.flat = indexes::curve(v);
        }
        return s;
    }

    inline QuantLib::Handle<QuantLib::YieldTermStructure> makeDiscount(const DiscountSpec& s) {
        if (!s.nodes)
            return indexes::makeCurve(s.flat);
        std::vector<QuantLib::Date> dates;
        for (const auto& d : s.dates)
            dates.push_back(makeDate(d));
        return QuantLib::Handle<QuantLib::YieldTermStructure>(
            QuantLib::ext::make_shared<QuantLib::DiscountCurve>(dates, s.discounts, makeDayCounter(s.dayCounter)));
    }

    inline QuantLib::ext::shared_ptr<QuantLib::Gsr> makeGsr(const GsrSpec& s) {
        auto quotes = [](const std::vector<QuantLib::Real>& x) {
            std::vector<QuantLib::Handle<QuantLib::Quote>> out;
            for (QuantLib::Real v : x)
                out.emplace_back(QuantLib::ext::make_shared<QuantLib::SimpleQuote>(v));
            return out;
        };
        std::vector<QuantLib::Date> dates;
        for (const auto& d : s.volstepdates)
            dates.push_back(makeDate(d));
        return QuantLib::ext::make_shared<QuantLib::Gsr>(indexes::makeCurve(s.curve), dates, quotes(s.volatilities),
                                                         quotes(s.reversions), s.T);
    }

}

#endif
