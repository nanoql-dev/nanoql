// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The curve, model and lattice specs the short-rate model methods share (ql/models, ql/methods/lattices,
// ql/discretizedasset), with tests/NanoQL.Tests/Vectors/ModelInputs.cs's names.
//
// A curve is {"type": "FlatForward", "referenceDate", "forward", "dayCounter", "compounding", "frequency"}, a
// FlatForward with a fixed reference date, or {"type": "DiscountCurve", "dates", "discounts", "dayCounter"}, QuantLib's
// DiscountCurve (InterpolatedDiscountCurve<LogLinear>) on its default calendar, with no jumps. A model is {"type":
// "Vasicek", "r0", "a", "b", "sigma", "lambda", "params"} or {"type": "HullWhite", "curve", "a", "sigma", "params"},
// "params" null, or a list of reals the call sets with setParams after construction (unchecked, as QuantLib's is). A
// lattice is {"model", "timeGrid", "spread"}: the model's tree(grid), a TimeGrid spec (timegrid.hpp), cast to
// OneFactorModel::ShortRateTree, then, for a spread that isn't null, setSpread(spread) on it.

#ifndef NANOQL_CXX_MODELS_MODELSPEC_HPP
#define NANOQL_CXX_MODELS_MODELSPEC_HPP

#include "convert.hpp"
#include "timegrid.hpp"

#include <ql/math/array.hpp>
#include <ql/models/shortrate/onefactormodel.hpp>
#include <ql/models/shortrate/onefactormodels/vasicek.hpp>
#include <ql/shared_ptr.hpp>
#include <ql/termstructures/yieldtermstructure.hpp>

#include <optional>
#include <string>
#include <vector>

namespace harness::models {

    struct CurveSpec {
        std::string type;  // FlatForward or DiscountCurve
        DateSpec referenceDate{true, 0};
        QuantLib::Real forward = 0.0;
        DayCounterSpec dayCounter;
        QuantLib::Compounding compounding = QuantLib::Continuous;
        QuantLib::Frequency frequency = QuantLib::Annual;
        std::vector<DateSpec> dates;
        std::vector<QuantLib::Real> discounts;
    };
    CurveSpec curve(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::YieldTermStructure> makeCurve(const CurveSpec& s);

    struct ModelSpec {
        std::string type;  // Vasicek or HullWhite
        QuantLib::Real r0 = 0.0, a = 0.0, b = 0.0, sigma = 0.0, lambda = 0.0;
        CurveSpec curve;
        std::optional<std::vector<QuantLib::Real>> params;
    };
    ModelSpec model(const json::value& v);
    // Hull-White derives from Vasicek, whose inspectors both share.
    QuantLib::ext::shared_ptr<QuantLib::Vasicek> makeModel(const ModelSpec& s);

    struct LatticeSpec {
        ModelSpec model;
        TimeGridSpec grid;
        std::optional<QuantLib::Real> spread;
    };
    LatticeSpec lattice(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::OneFactorModel::ShortRateTree> makeLattice(const LatticeSpec& s);

    // A non-negative index, as the harness passes it.
    QuantLib::Size index(const json::value& v);

    QuantLib::Array makeArray(const std::vector<QuantLib::Real>& x);
    json::value arrayResult(const QuantLib::Array& a);

}

#endif
