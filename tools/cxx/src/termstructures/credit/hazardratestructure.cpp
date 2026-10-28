// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/credit/hazardratestructure.hpp and defaultdensitystructure.hpp: their default survival
// probabilities, a 48-point Gauss-Chebyshev integral of the hazard rate or the default density, which no QuantLib curve
// keeps, through harness subclasses on deterministic forms:
//
//     HazardRateStructure::survival (reference date, a, b, times): S(t) for the hazard rate a + b t (Actual/365 Fixed)
//     DefaultDensityStructure::survival (reference date, c, times): S(t) for the density c e^(-c t), floored at 0

#include "convert.hpp"

#include <ql/termstructures/credit/defaultdensitystructure.hpp>
#include <ql/termstructures/credit/hazardratestructure.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <cmath>

using namespace QuantLib;

namespace harness {

    namespace {

        class LinearHazard : public HazardRateStructure {
          public:
            LinearHazard(const Date& d, Real a, Real b)
            : HazardRateStructure(d, Calendar(), Actual365Fixed()), a_(a), b_(b) {}
            Date maxDate() const override { return Date::maxDate(); }

          protected:
            Real hazardRateImpl(Time t) const override { return a_ + b_ * t; }

          private:
            Real a_, b_;
        };

        class ExpDensity : public DefaultDensityStructure {
          public:
            ExpDensity(const Date& d, Real c) : DefaultDensityStructure(d, Calendar(), Actual365Fixed()), c_(c) {}
            Date maxDate() const override { return Date::maxDate(); }

          protected:
            Real defaultDensityImpl(Time t) const override { return c_ * std::exp(-c_ * t); }

          private:
            Real c_;
        };

        json::value survivals(const DefaultProbabilityTermStructure& curve, const std::vector<Real>& times) {
            json::array out;
            for (Time t : times)
                out.emplace_back(curve.survivalProbability(t));
            return out;
        }

        const Register unit({
            {"HazardRateStructure::survival",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 DateSpec d = date(a[0]);
                 Real x = real(a[1]), y = real(a[2]);
                 std::vector<Real> times = reals(a[3]);
                 return [=] { return survivals(LinearHazard(makeDate(d), x, y), times); };
             }},
            {"DefaultDensityStructure::survival",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 DateSpec d = date(a[0]);
                 Real c = real(a[1]);
                 std::vector<Real> times = reals(a[2]);
                 return [=] { return survivals(ExpDensity(makeDate(d), c), times); };
             }},
        });

    }

}
