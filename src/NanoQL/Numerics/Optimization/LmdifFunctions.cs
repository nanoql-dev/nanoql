// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: The original Fortran version is Copyright (C) 1999 University of Chicago.
// SPDX-FileCopyrightText: C translation Copyright (C) Steve Moshier
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause AND Minpack
// Upstream: ql/math/optimization/lmdif.hpp ql/math/optimization/lmdif.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// The original Fortran version is Copyright (C) 1999 University of Chicago.
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or
// without modification, are permitted provided that the
// following conditions are met:
//
// 1. Redistributions of source code must retain the above
// copyright notice, this list of conditions and the following
// disclaimer.
//
// 2. Redistributions in binary form must reproduce the above
// copyright notice, this list of conditions and the following
// disclaimer in the documentation and/or other materials
// provided with the distribution.
//
// 3. The end-user documentation included with the
// redistribution, if any, must include the following
// acknowledgment:
//
//    "This product includes software developed by the
//    University of Chicago, as Operator of Argonne National
//    Laboratory.
//
// Alternately, this acknowledgment may appear in the software
// itself, if and wherever such third-party acknowledgments
// normally appear.
//
// 4. WARRANTY DISCLAIMER. THE SOFTWARE IS SUPPLIED "AS IS"
// WITHOUT WARRANTY OF ANY KIND. THE COPYRIGHT HOLDER, THE
// UNITED STATES, THE UNITED STATES DEPARTMENT OF ENERGY, AND
// THEIR EMPLOYEES: (1) DISCLAIM ANY WARRANTIES, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO ANY IMPLIED WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE
// OR NON-INFRINGEMENT, (2) DO NOT ASSUME ANY LEGAL LIABILITY
// OR RESPONSIBILITY FOR THE ACCURACY, COMPLETENESS, OR
// USEFULNESS OF THE SOFTWARE, (3) DO NOT REPRESENT THAT USE OF
// THE SOFTWARE WOULD NOT INFRINGE PRIVATELY OWNED RIGHTS, (4)
// DO NOT WARRANT THAT THE SOFTWARE WILL FUNCTION
// UNINTERRUPTED, THAT IT IS ERROR-FREE OR THAT ANY ERRORS WILL
// BE CORRECTED.
//
// 5. LIMITATION OF LIABILITY. IN NO EVENT WILL THE COPYRIGHT
// HOLDER, THE UNITED STATES, THE UNITED STATES DEPARTMENT OF
// ENERGY, OR THEIR EMPLOYEES: BE LIABLE FOR ANY INDIRECT,
// INCIDENTAL, CONSEQUENTIAL, SPECIAL OR PUNITIVE DAMAGES OF
// ANY KIND OR NATURE, INCLUDING BUT NOT LIMITED TO LOSS OF
// PROFITS OR LOSS OF DATA, FOR ANY REASON WHATSOEVER, WHETHER
// SUCH LIABILITY IS ASSERTED ON THE BASIS OF CONTRACT, TORT
// (INCLUDING NEGLIGENCE OR STRICT LIABILITY), OR OTHERWISE,
// EVEN IF ANY OF SAID PARTIES HAS BEEN WARNED OF THE
// POSSIBILITY OF SUCH LOSS OR DAMAGES.
//
//
// C translation Copyright (C) Steve Moshier
//
// What you see here may be used freely but it comes with no support
// or guarantee.

namespace NanoQL.Numerics;

/// <summary>
/// MINPACK's Levenberg–Marquardt minimization (<c>lmdif</c>) and its QR helpers, from QuantLib's C translation, in
/// its operation order: the routines <see cref="LevenbergMarquardt"/> calls, QuantLib's <c>MINPACK</c> namespace.
/// </summary>
/// <remarks>
/// The C code's pointers into arrays are spans, an offset pointer a slice; its labels and jumps are kept.
/// </remarks>
internal static class LmdifFunctions
{
    // resolution of arithmetic
    private const double MACHEP = 1.2e-16;

    // smallest nonzero number
    private const double DWARF = 1.0e-38;

    /// <summary>
    /// The user-supplied function of <see cref="Lmdif"/>, QuantLib's <c>LmdifCostFunction</c>: it computes m values
    /// (or an m-by-n Jacobian, by columns) at the n variables x into its second array. It may stop the minimization by
    /// setting <c>iflag</c> negative.
    /// </summary>
    /// <param name="m">The number of functions.</param>
    /// <param name="n">The number of variables.</param>
    /// <param name="x">The variables.</param>
    /// <param name="fvec">The values, or the Jacobian, written.</param>
    /// <param name="iflag">The call's flag, which the function may set negative.</param>
    internal delegate void LmdifCostFunction(int m, int n, Span<double> x, Span<double> fvec, ref int iflag);

    /// <summary>
    /// Minimizes the sum of the squares of m nonlinear functions in n variables by a modification of the
    /// Levenberg–Marquardt algorithm. The Jacobian is a forward-difference approximation, or
    /// <paramref name="jacFcn"/>'s.
    /// </summary>
    /// <param name="m">The number of functions.</param>
    /// <param name="n">The number of variables, at most m.</param>
    /// <param name="x">The initial estimate on input; the final estimate on output.</param>
    /// <param name="fvec">The functions at the output x.</param>
    /// <param name="ftol">The relative error desired in the sum of squares.</param>
    /// <param name="xtol">The relative error desired in the approximate solution.</param>
    /// <param name="gtol">The orthogonality desired between fvec and the Jacobian's columns.</param>
    /// <param name="maxfev">The maximum number of calls to fcn, checked at the end of an iteration.</param>
    /// <param name="epsfcn">The functions' relative errors, for the forward-difference step.</param>
    /// <param name="diag">The variables' scale factors: set internally unless mode is 2.</param>
    /// <param name="mode">1 to scale internally, 2 to scale by diag.</param>
    /// <param name="factor">The initial step bound's factor.</param>
    /// <param name="nprint">If positive, fcn is called with iflag 0 every nprint iterations, for printing.</param>
    /// <param name="info">
    /// The outcome: 0 improper input parameters; 1 the actual and predicted relative reductions are at most ftol; 2
    /// the relative error between two iterates is at most xtol; 3 both; 4 the cosine between fvec and any column of
    /// the Jacobian is at most gtol; 5 the calls to fcn reached maxfev; 6 ftol is too small; 7 xtol is too small; 8
    /// gtol is too small; negative, the iflag the user set.
    /// </param>
    /// <param name="nfev">The number of calls to fcn.</param>
    /// <param name="fjac">The m-by-n output array, by columns.</param>
    /// <param name="ldfjac">The leading dimension of fjac, at least m.</param>
    /// <param name="ipvt">The permutation of the final Jacobian's QR factorization.</param>
    /// <param name="qtf">The first n elements of (q transpose)·fvec.</param>
    /// <param name="wa1">A work array of length n.</param>
    /// <param name="wa2">A work array of length n.</param>
    /// <param name="wa3">A work array of length n.</param>
    /// <param name="wa4">A work array of length m.</param>
    /// <param name="fcn">The functions.</param>
    /// <param name="jacFcn">The Jacobian, by columns, or <see langword="null"/> for forward differences.</param>
    internal static void Lmdif(
        int m,
        int n,
        Span<double> x,
        Span<double> fvec,
        double ftol,
        double xtol,
        double gtol,
        int maxfev,
        double epsfcn,
        Span<double> diag,
        int mode,
        double factor,
        int nprint,
        ref int info,
        ref int nfev,
        Span<double> fjac,
        int ldfjac,
        Span<int> ipvt,
        Span<double> qtf,
        Span<double> wa1,
        Span<double> wa2,
        Span<double> wa3,
        Span<double> wa4,
        LmdifCostFunction fcn,
        LmdifCostFunction? jacFcn)
    {
        /*
        *     **********
        *
        *     subroutine lmdif
        *
        *     the purpose of lmdif is to minimize the sum of the squares of
        *     m nonlinear functions in n variables by a modification of
        *     the levenberg-marquardt algorithm. the user must provide a
        *     subroutine which calculates the functions. the jacobian is
        *     then calculated by a forward-difference approximation.
        *
        *     subprograms called
        *
        *   user-supplied ...... fcn, jacFcn
        *
        *   minpack-supplied ... dpmpar,enorm,fdjac2,lmpar,qrfac
        *
        *   fortran-supplied ... dabs,dmax1,dmin1,dsqrt,mod
        *
        *     argonne national laboratory. minpack project. march 1980.
        *     burton s. garbow, kenneth e. hillstrom, jorge j. more
        *
        *     **********
        */
        int i, iflag, ij, jj, iter, j, l;
        double actred, delta = 0, dirder, fnorm, fnorm1, gnorm;
        double par, pnorm, prered, ratio;
        double sum, temp, temp1, temp2, temp3, xnorm = 0;
        const double one = 1.0;
        const double p1 = 0.1;
        const double p5 = 0.5;
        const double p25 = 0.25;
        const double p75 = 0.75;
        const double p0001 = 1.0e-4;
        const double zero = 0.0;

        info = 0;
        iflag = 0;
        nfev = 0;
        /*
        *     check the input parameters for errors.
        */
        if ((n <= 0) || (m < n) || (ldfjac < m) || (ftol < zero)
            || (xtol < zero) || (gtol < zero) || (maxfev <= 0)
            || (factor <= zero))
        {
            goto L300;
        }

        if (mode == 2)
        { /* scaling by diag[] */
            for (j = 0; j < n; j++)
            {
                if (diag[j] <= 0.0)
                {
                    goto L300;
                }
            }
        }

        /*
        *     evaluate the function at the starting point
        *     and calculate its norm.
        */
        iflag = 1;
        fcn(m, n, x, fvec, ref iflag);
        nfev = 1;
        if (iflag < 0)
        {
            goto L300;
        }

        fnorm = Enorm(m, fvec);
        /*
        *     initialize levenberg-marquardt parameter and iteration counter.
        */
        par = zero;
        iter = 1;
    /*
    *     beginning of the outer loop.
    */

    L30:

        /*
        *    calculate the jacobian matrix.
        */
        iflag = 2;
        if (jacFcn is null)
        {
            Fdjac2(m, n, x, fvec, fjac, ldfjac, ref iflag, epsfcn, wa4, fcn);
        }
        else
        {
            // use user supplied jacobian calculation
            jacFcn(m, n, x, fjac, ref iflag);
        }

        nfev += n;
        if (iflag < 0)
        {
            goto L300;
        }

        /*
        *    if requested, call fcn to enable printing of iterates.
        */
        if (nprint > 0)
        {
            iflag = 0;
            if (Mod(iter - 1, nprint) == 0)
            {
                fcn(m, n, x, fvec, ref iflag);
                if (iflag < 0)
                {
                    goto L300;
                }
            }
        }

        /*
        *    compute the qr factorization of the jacobian.
        */
        Qrfac(m, n, fjac, ldfjac, 1, ipvt, n, wa1, wa2, wa3);
        /*
        *    on the first iteration and if mode is 1, scale according
        *    to the norms of the columns of the initial jacobian.
        */
        if (iter == 1)
        {
            if (mode != 2)
            {
                for (j = 0; j < n; j++)
                {
                    diag[j] = wa2[j];
                    if (wa2[j] == zero)
                    {
                        diag[j] = one;
                    }
                }
            }

            /*
            *    on the first iteration, calculate the norm of the scaled x
            *    and initialize the step bound delta.
            */
            for (j = 0; j < n; j++)
            {
                wa3[j] = diag[j] * x[j];
            }

            xnorm = Enorm(n, wa3);
            delta = factor * xnorm;
            if (delta == zero)
            {
                delta = factor;
            }
        }

        /*
        *    form (q transpose)*fvec and store the first n components in
        *    qtf.
        */
        for (i = 0; i < m; i++)
        {
            wa4[i] = fvec[i];
        }

        jj = 0;
        for (j = 0; j < n; j++)
        {
            temp3 = fjac[jj];
            if (temp3 != zero)
            {
                sum = zero;
                ij = jj;
                for (i = j; i < m; i++)
                {
                    sum += fjac[ij] * wa4[i];
                    ij += 1;    /* fjac[i+m*j] */
                }

                temp = -sum / temp3;
                ij = jj;
                for (i = j; i < m; i++)
                {
                    wa4[i] += fjac[ij] * temp;
                    ij += 1;    /* fjac[i+m*j] */
                }
            }

            fjac[jj] = wa1[j];
            jj += m + 1;  /* fjac[j+m*j] */
            qtf[j] = wa4[j];
        }

        /*
        *    compute the norm of the scaled gradient.
        */
        gnorm = zero;
        if (fnorm != zero)
        {
            jj = 0;
            for (j = 0; j < n; j++)
            {
                l = ipvt[j];
                if (wa2[l] != zero)
                {
                    sum = zero;
                    ij = jj;
                    for (i = 0; i <= j; i++)
                    {
                        sum += fjac[ij] * (qtf[i] / fnorm);
                        ij += 1; /* fjac[i+m*j] */
                    }

                    gnorm = Dmax1(gnorm, Math.Abs(sum / wa2[l]));
                }

                jj += m;
            }
        }

        /*
        *    test for convergence of the gradient norm.
        */
        if (gnorm <= gtol)
        {
            info = 4;
        }

        if (info != 0)
        {
            goto L300;
        }

        /*
        *    rescale if necessary.
        */
        if (mode != 2)
        {
            for (j = 0; j < n; j++)
            {
                diag[j] = Dmax1(diag[j], wa2[j]);
            }
        }

    /*
    *    beginning of the inner loop.
    */
    L200:
        /*
        *       determine the levenberg-marquardt parameter.
        */
        Lmpar(n, fjac, ldfjac, ipvt, diag, qtf, delta, ref par, wa1, wa2, wa3, wa4);
        /*
        *       store the direction p and x + p. calculate the norm of p.
        */
        for (j = 0; j < n; j++)
        {
            wa1[j] = -wa1[j];
            wa2[j] = x[j] + wa1[j];
            wa3[j] = diag[j] * wa1[j];
        }

        pnorm = Enorm(n, wa3);
        /*
        *       on the first iteration, adjust the initial step bound.
        */
        if (iter == 1)
        {
            delta = Dmin1(delta, pnorm);
        }

        /*
        *       evaluate the function at x + p and calculate its norm.
        */
        iflag = 1;
        fcn(m, n, wa2, wa4, ref iflag);
        nfev += 1;
        if (iflag < 0)
        {
            goto L300;
        }

        fnorm1 = Enorm(m, wa4);
        /*
        *       compute the scaled actual reduction.
        */
        actred = -one;
        if ((p1 * fnorm1) < fnorm)
        {
            temp = fnorm1 / fnorm;
            actred = one - (temp * temp);
        }

        /*
        *       compute the scaled predicted reduction and
        *       the scaled directional derivative.
        */
        jj = 0;
        for (j = 0; j < n; j++)
        {
            wa3[j] = zero;
            l = ipvt[j];
            temp = wa1[l];
            ij = jj;
            for (i = 0; i <= j; i++)
            {
                wa3[i] += fjac[ij] * temp;
                ij += 1; /* fjac[i+m*j] */
            }

            jj += m;
        }

        temp1 = Enorm(n, wa3) / fnorm;
        temp2 = (Math.Sqrt(par) * pnorm) / fnorm;
        prered = (temp1 * temp1) + ((temp2 * temp2) / p5);
        dirder = -((temp1 * temp1) + (temp2 * temp2));
        /*
        *       compute the ratio of the actual to the predicted
        *       reduction.
        */
        ratio = zero;
        if (prered != zero)
        {
            ratio = actred / prered;
        }

        /*
        *       update the step bound.
        */
        if (ratio <= p25)
        {
            if (actred >= zero)
            {
                temp = p5;
            }
            else
            {
                temp = p5 * dirder / (dirder + (p5 * actred));
            }

            if (((p1 * fnorm1) >= fnorm)
                || (temp < p1))
            {
                temp = p1;
            }

            delta = temp * Dmin1(delta, pnorm / p1);
            par = par / temp;
        }
        else
        {
            if ((par == zero) || (ratio >= p75))
            {
                delta = pnorm / p5;
                par = p5 * par;
            }
        }

        /*
        *       test for successful iteration.
        */
        if (ratio >= p0001)
        {
            /*
            *       successful iteration. update x, fvec, and their norms.
            */
            for (j = 0; j < n; j++)
            {
                x[j] = wa2[j];
                wa2[j] = diag[j] * x[j];
            }

            for (i = 0; i < m; i++)
            {
                fvec[i] = wa4[i];
            }

            xnorm = Enorm(n, wa2);
            fnorm = fnorm1;
            iter += 1;
        }

        /*
        *       tests for convergence.
        */
        if ((Math.Abs(actred) <= ftol)
            && (prered <= ftol)
            && (p5 * ratio <= one))
        {
            info = 1;
        }

        if (delta <= xtol * xnorm)
        {
            info = 2;
        }

        if ((Math.Abs(actred) <= ftol)
            && (prered <= ftol)
            && (p5 * ratio <= one)
            && (info == 2))
        {
            info = 3;
        }

        if (info != 0)
        {
            goto L300;
        }

        /*
        *       tests for termination and stringent tolerances.
        */
        if (nfev >= maxfev)
        {
            info = 5;
        }

        if ((Math.Abs(actred) <= MACHEP)
            && (prered <= MACHEP)
            && (p5 * ratio <= one))
        {
            info = 6;
        }

        if (delta <= MACHEP * xnorm)
        {
            info = 7;
        }

        if (gnorm <= MACHEP)
        {
            info = 8;
        }

        if (info != 0)
        {
            goto L300;
        }

        /*
        *       end of the inner loop. repeat if iteration unsuccessful.
        */
        if (ratio < p0001)
        {
            goto L200;
        }

        /*
        *    end of the outer loop.
        */
        goto L30;

    L300:
        /*
        *     termination, either normal or user imposed.
        */
        if (iflag < 0)
        {
            info = iflag;
        }

        iflag = 0;
        if (nprint > 0)
        {
            fcn(m, n, x, fvec, ref iflag);
        }

        /*
              last card of subroutine lmdif.
        */
    }

    /// <summary>
    /// Completes the least-squares solution of a·x = b, d·x = 0 for a diagonal d, given the QR factorization with
    /// column pivoting of a: the full upper triangle of r, the permutation and the first n elements of
    /// (q transpose)·b.
    /// </summary>
    /// <param name="n">The order of r.</param>
    /// <param name="r">
    /// The n-by-n array whose full upper triangle is r on input; on output the upper triangle is unaltered and the
    /// strict lower triangle holds the strict upper triangle of s, transposed.
    /// </param>
    /// <param name="ldr">The leading dimension of r.</param>
    /// <param name="ipvt">The permutation: column j of p is column ipvt[j] of the identity.</param>
    /// <param name="diag">The diagonal elements of d.</param>
    /// <param name="qtb">The first n elements of (q transpose)·b.</param>
    /// <param name="x">The least-squares solution, written.</param>
    /// <param name="sdiag">The diagonal elements of s, written.</param>
    /// <param name="wa">A work array of length n.</param>
    internal static void Qrsolv(
        int n,
        Span<double> r,
        int ldr,
        ReadOnlySpan<int> ipvt,
        ReadOnlySpan<double> diag,
        ReadOnlySpan<double> qtb,
        Span<double> x,
        Span<double> sdiag,
        Span<double> wa)
    {
        /*
         *     **********
         *
         *     subroutine qrsolv
         *
         *     given an m by n matrix a, an n by n diagonal matrix d,
         *     and an m-vector b, the problem is to determine an x which
         *     solves the system
         *
         *       a*x = b ,     d*x = 0 ,
         *
         *     in the least squares sense.
         *
         *     subprograms called
         *
         *   fortran-supplied ... dabs,dsqrt
         *
         *     argonne national laboratory. minpack project. march 1980.
         *     burton s. garbow, kenneth e. hillstrom, jorge j. more
         *
         *     **********
         */
        int i, ij, ik, kk, j, jp1, k, kp1, l, nsing;
        double cos, cotan, qtbpj, sin, sum, tan, temp;
        const double zero = 0.0;
        const double p25 = 0.25;
        const double p5 = 0.5;

        /*
         *     copy r and (q transpose)*b to preserve input and initialize s.
         *     in particular, save the diagonal elements of r in x.
         */
        kk = 0;
        for (j = 0; j < n; j++)
        {
            ij = kk;
            ik = kk;
            for (i = j; i < n; i++)
            {
                r[ij] = r[ik];
                ij += 1;   /* [i+ldr*j] */
                ik += ldr; /* [j+ldr*i] */
            }

            x[j] = r[kk];
            wa[j] = qtb[j];
            kk += ldr + 1; /* j+ldr*j */
        }

        /*
        *     eliminate the diagonal matrix d using a givens rotation.
        */
        for (j = 0; j < n; j++)
        {
            /*
            *    prepare the row of d to be eliminated, locating the
            *    diagonal element using p from the qr factorization.
            */
            l = ipvt[j];
            if (diag[l] == zero)
            {
                goto L90;
            }

            for (k = j; k < n; k++)
            {
                sdiag[k] = zero;
            }

            sdiag[j] = diag[l];
            /*
            *    the transformations to eliminate the row of d
            *    modify only a single element of (q transpose)*b
            *    beyond the first n, which is initially zero.
            */
            qtbpj = zero;
            for (k = j; k < n; k++)
            {
                /*
                *       determine a givens rotation which eliminates the
                *       appropriate element in the current row of d.
                */
                if (sdiag[k] == zero)
                {
                    continue;
                }

                kk = k + (ldr * k);
                if (Math.Abs(r[kk]) < Math.Abs(sdiag[k]))
                {
                    cotan = r[kk] / sdiag[k];
                    sin = p5 / Math.Sqrt(p25 + (p25 * cotan * cotan));
                    cos = sin * cotan;
                }
                else
                {
                    tan = sdiag[k] / r[kk];
                    cos = p5 / Math.Sqrt(p25 + (p25 * tan * tan));
                    sin = cos * tan;
                }

                /*
                *       compute the modified diagonal element of r and
                *       the modified element of ((q transpose)*b,0).
                */
                r[kk] = (cos * r[kk]) + (sin * sdiag[k]);
                temp = (cos * wa[k]) + (sin * qtbpj);
                qtbpj = (-sin * wa[k]) + (cos * qtbpj);
                wa[k] = temp;
                /*
                *       accumulate the tranformation in the row of s.
                */
                kp1 = k + 1;
                if (n > kp1)
                {
                    ik = kk + 1;
                    for (i = kp1; i < n; i++)
                    {
                        temp = (cos * r[ik]) + (sin * sdiag[i]);
                        sdiag[i] = (-sin * r[ik]) + (cos * sdiag[i]);
                        r[ik] = temp;
                        ik += 1; /* [i+ldr*k] */
                    }
                }
            }

        L90:
            /*
            *    store the diagonal element of s and restore
            *    the corresponding diagonal element of r.
            */
            kk = j + (ldr * j);
            sdiag[j] = r[kk];
            r[kk] = x[j];
        }

        /*
        *     solve the triangular system for z. if the system is
        *     singular, then obtain a least squares solution.
        */
        nsing = n;
        for (j = 0; j < n; j++)
        {
            if ((sdiag[j] == zero) && (nsing == n))
            {
                nsing = j;
            }

            if (nsing < n)
            {
                wa[j] = zero;
            }
        }

        if (nsing < 1)
        {
            goto L150;
        }

        for (k = 0; k < nsing; k++)
        {
            j = nsing - k - 1;
            sum = zero;
            jp1 = j + 1;
            if (nsing > jp1)
            {
                ij = jp1 + (ldr * j);
                for (i = jp1; i < nsing; i++)
                {
                    sum += r[ij] * wa[i];
                    ij += 1; /* [i+ldr*j] */
                }
            }

            wa[j] = (wa[j] - sum) / sdiag[j];
        }

    L150:
        /*
        *     permute the components of z back to components of x.
        */
        for (j = 0; j < n; j++)
        {
            l = ipvt[j];
            x[l] = wa[j];
        }

        /*
        *     last card of subroutine qrsolv.
        */
    }

    /// <summary>
    /// The QR factorization of the m-by-n matrix a with column pivoting (optional), by Householder transformations:
    /// a·p = q·r, r upper trapezoidal with diagonal elements of nonincreasing magnitude.
    /// </summary>
    /// <param name="m">The number of rows of a.</param>
    /// <param name="n">The number of columns of a.</param>
    /// <param name="a">
    /// The m-by-n matrix, by columns; on output the strict upper trapezoidal part holds r's, and the lower trapezoidal
    /// part a factored form of q.
    /// </param>
    /// <param name="lda">The leading dimension of a, which the C translation doesn't read.</param>
    /// <param name="pivot">Nonzero to enforce column pivoting.</param>
    /// <param name="ipvt">The permutation, written when pivoting.</param>
    /// <param name="lipvt">The length of ipvt, which the C translation doesn't read.</param>
    /// <param name="rdiag">The diagonal elements of r, written.</param>
    /// <param name="acnorm">The norms of a's columns, written.</param>
    /// <param name="wa">A work array of length n.</param>
    internal static void Qrfac(
        int m,
        int n,
        Span<double> a,
        int lda,
        int pivot,
        Span<int> ipvt,
        int lipvt,
        Span<double> rdiag,
        Span<double> acnorm,
        Span<double> wa)
    {
        /*
        *     **********
        *
        *     subroutine qrfac
        *
        *     this subroutine uses householder transformations with column
        *     pivoting (optional) to compute a qr factorization of the
        *     m by n matrix a. that is, qrfac determines an orthogonal
        *     matrix q, a permutation matrix p, and an upper trapezoidal
        *     matrix r with diagonal elements of nonincreasing magnitude,
        *     such that a*p = q*r. the householder transformation for
        *     column k, k = 1,2,...,min(m,n), is of the form
        *
        *               t
        *       i - (1/u(k))*u*u
        *
        *     where u has zeros in the first k-1 positions. the form of
        *     this transformation and the method of pivoting first
        *     appeared in the corresponding linpack subroutine.
        *
        *     subprograms called
        *
        *   minpack-supplied ... dpmpar,enorm
        *
        *   fortran-supplied ... dmax1,dsqrt,min0
        *
        *     argonne national laboratory. minpack project. march 1980.
        *     burton s. garbow, kenneth e. hillstrom, jorge j. more
        *
        *     **********
        */
        _ = lda;
        _ = lipvt;
        int i, ij, jj, j, jp1, k, kmax, minmn;
        double ajnorm, sum, temp;
        const double zero = 0.0;
        const double one = 1.0;
        const double p05 = 0.05;

        /*
        *     compute the initial column norms and initialize several arrays.
        */
        ij = 0;
        for (j = 0; j < n; j++)
        {
            acnorm[j] = Enorm(m, a[ij..]);
            rdiag[j] = acnorm[j];
            wa[j] = rdiag[j];
            if (pivot != 0)
            {
                ipvt[j] = j;
            }

            ij += m; /* m*j */
        }

        /*
        *     reduce a to r with householder transformations.
        */
        minmn = Min0(m, n);
        for (j = 0; j < minmn; j++)
        {
            if (pivot == 0)
            {
                goto L40;
            }

            /*
            *    bring the column of largest norm into the pivot position.
            */
            kmax = j;
            for (k = j; k < n; k++)
            {
                if (rdiag[k] > rdiag[kmax])
                {
                    kmax = k;
                }
            }

            if (kmax == j)
            {
                goto L40;
            }

            ij = m * j;
            jj = m * kmax;
            for (i = 0; i < m; i++)
            {
                temp = a[ij]; /* [i+m*j] */
                a[ij] = a[jj]; /* [i+m*kmax] */
                a[jj] = temp;
                ij += 1;
                jj += 1;
            }

            rdiag[kmax] = rdiag[j];
            wa[kmax] = wa[j];
            k = ipvt[j];
            ipvt[j] = ipvt[kmax];
            ipvt[kmax] = k;

        L40:
            /*
            *    compute the householder transformation to reduce the
            *    j-th column of a to a multiple of the j-th unit vector.
            */
            jj = j + (m * j);
            ajnorm = Enorm(m - j, a[jj..]);
            if (ajnorm == zero)
            {
                goto L100;
            }

            if (a[jj] < zero)
            {
                ajnorm = -ajnorm;
            }

            ij = jj;
            for (i = j; i < m; i++)
            {
                a[ij] /= ajnorm;
                ij += 1; /* [i+m*j] */
            }

            a[jj] += one;
            /*
            *    apply the transformation to the remaining columns
            *    and update the norms.
            */
            jp1 = j + 1;
            if (jp1 < n)
            {
                for (k = jp1; k < n; k++)
                {
                    sum = zero;
                    ij = j + (m * k);
                    jj = j + (m * j);
                    for (i = j; i < m; i++)
                    {
                        sum += a[jj] * a[ij];
                        ij += 1; /* [i+m*k] */
                        jj += 1; /* [i+m*j] */
                    }

                    temp = sum / a[j + (m * j)];
                    ij = j + (m * k);
                    jj = j + (m * j);
                    for (i = j; i < m; i++)
                    {
                        a[ij] -= temp * a[jj];
                        ij += 1; /* [i+m*k] */
                        jj += 1; /* [i+m*j] */
                    }

                    // Preserve the generic MINPACK non-pivoting path even though QuantLib's
                    // current caller always enables pivoting.
                    if ((pivot != 0) && (rdiag[k] != zero))
                    {
                        temp = a[j + (m * k)] / rdiag[k];
                        temp = Dmax1(zero, one - (temp * temp));
                        rdiag[k] *= Math.Sqrt(temp);
                        temp = rdiag[k] / wa[k];
                        if ((p05 * temp * temp) <= MACHEP)
                        {
                            rdiag[k] = Enorm(m - j - 1, a[(jp1 + (m * k))..]);
                            wa[k] = rdiag[k];
                        }
                    }
                }
            }

        L100:
            rdiag[j] = -ajnorm;
        }

        /*
        *     last card of subroutine qrfac.
        */
    }

    // The euclidean norm of the n-vector x, accumulating the sum of squares in three different sums: the sums of
    // squares for the small and large components are scaled so that no overflows occur. Non-destructive underflows are
    // permitted. Underflows and overflows do not occur in the computation of the unscaled sum of squares for the
    // intermediate components. The definitions of small, intermediate and large components depend on two constants,
    // rdwarf and rgiant: rdwarf**2 must not underflow and rgiant**2 not overflow; the constants given here are suitable
    // for every known computer. (argonne national laboratory. minpack project. march 1980. burton s. garbow, kenneth e.
    // hillstrom, jorge j. more)
    private static double Enorm(int n, ReadOnlySpan<double> x)
    {
        int i;
        double agiant, floatn, s1, s2, s3, xabs, x1max, x3max;
        double ans, temp;
        const double rdwarf = 3.834e-20;
        const double rgiant = 1.304e19;
        const double zero = 0.0;
        const double one = 1.0;

        s1 = zero;
        s2 = zero;
        s3 = zero;
        x1max = zero;
        x3max = zero;
        floatn = n;
        agiant = rgiant / floatn;

        for (i = 0; i < n; i++)
        {
            xabs = Math.Abs(x[i]);
            if ((xabs > rdwarf) && (xabs < agiant))
            {
                /*
                *       sum for intermediate components.
                */
                s2 += xabs * xabs;
                continue;
            }

            if (xabs > rdwarf)
            {
                /*
                *          sum for large components.
                */
                if (xabs > x1max)
                {
                    temp = x1max / xabs;
                    s1 = one + (s1 * temp * temp);
                    x1max = xabs;
                }
                else
                {
                    temp = xabs / x1max;
                    s1 += temp * temp;
                }

                continue;
            }

            /*
            *          sum for small components.
            */
            if (xabs > x3max)
            {
                temp = x3max / xabs;
                s3 = one + (s3 * temp * temp);
                x3max = xabs;
            }
            else
            {
                if (xabs != zero)
                {
                    temp = xabs / x3max;
                    s3 += temp * temp;
                }
            }
        }

        /*
        *     calculation of norm.
        */
        if (s1 != zero)
        {
            temp = s1 + ((s2 / x1max) / x1max);
            ans = x1max * Math.Sqrt(temp);
            return ans;
        }

        if (s2 != zero)
        {
            if (s2 >= x3max)
            {
                temp = s2 * (one + ((x3max / s2) * (x3max * s3)));
            }
            else
            {
                temp = x3max * ((s2 / x3max) + (x3max * s3));
            }

            ans = Math.Sqrt(temp);
        }
        else
        {
            ans = x3max * Math.Sqrt(s3);
        }

        return ans;
        /*
        *     last card of function enorm.
        */
    }

    private static double Dmax1(double a, double b)
    {
        if (a >= b)
        {
            return a;
        }
        else
        {
            return b;
        }
    }

    private static double Dmin1(double a, double b)
    {
        if (a <= b)
        {
            return a;
        }
        else
        {
            return b;
        }
    }

    private static int Min0(int a, int b)
    {
        if (a <= b)
        {
            return a;
        }
        else
        {
            return b;
        }
    }

    private static int Mod(int k, int m) => k % m;

    // A forward-difference approximation to the m-by-n Jacobian of fcn at x, written into fjac by columns; fvec holds
    // the functions at x, and the step is sqrt(max(epsfcn, MACHEP)) times |x[j]|, or that root where x[j] is zero.
    // wa is a work array of length m. (argonne national laboratory. minpack project. march 1980. burton s. garbow,
    // kenneth e. hillstrom, jorge j. more)
    private static void Fdjac2(
        int m,
        int n,
        Span<double> x,
        ReadOnlySpan<double> fvec,
        Span<double> fjac,
        int ldfjac,
        ref int iflag,
        double epsfcn,
        Span<double> wa,
        LmdifCostFunction fcn)
    {
        _ = ldfjac;
        int i, j, ij;
        double eps, h, temp;
        const double zero = 0.0;

        temp = Dmax1(epsfcn, MACHEP);
        eps = Math.Sqrt(temp);
        ij = 0;
        for (j = 0; j < n; j++)
        {
            temp = x[j];
            h = eps * Math.Abs(temp);
            if (h == zero)
            {
                h = eps;
            }

            x[j] = temp + h;
            fcn(m, n, x, wa, ref iflag);
            if (iflag < 0)
            {
                return;
            }

            x[j] = temp;
            for (i = 0; i < m; i++)
            {
                fjac[ij] = (wa[i] - fvec[i]) / h;
                ij += 1; /* fjac[i+m*j] */
            }
        }

        /*
        *     last card of subroutine fdjac2.
        */
    }

    // Given the QR factorization with column pivoting of an m-by-n matrix a, an n-by-n nonsingular diagonal matrix d,
    // an m-vector b and a positive delta, determines par such that if x solves a·x = b, sqrt(par)·d·x = 0 in the least
    // squares sense and dxnorm is the euclidean norm of d·x, then either par is zero and (dxnorm − delta) ≤ 0.1·delta,
    // or par is positive and |dxnorm − delta| ≤ 0.1·delta; at most 10 iterations. r's full upper triangle is r on input
    // and unaltered on output, its strict lower triangle s's strict upper triangle, transposed. x is the solution for
    // the output par, sdiag s's diagonal; wa1 and wa2 are work arrays of length n. (argonne national laboratory.
    // minpack project. march 1980. burton s. garbow, kenneth e. hillstrom, jorge j. more)
    private static void Lmpar(
        int n,
        Span<double> r,
        int ldr,
        ReadOnlySpan<int> ipvt,
        ReadOnlySpan<double> diag,
        ReadOnlySpan<double> qtb,
        double delta,
        ref double par,
        Span<double> x,
        Span<double> sdiag,
        Span<double> wa1,
        Span<double> wa2)
    {
        int i, iter, ij, jj, j, jm1, jp1, k, l, nsing;
        double dxnorm, fp, gnorm, parc, parl, paru;
        double sum, temp;
        const double zero = 0.0;
        const double p1 = 0.1;
        const double p001 = 0.001;

        /*
         *     compute and store in x the gauss-newton direction. if the
         *     jacobian is rank-deficient, obtain a least squares solution.
         */
        nsing = n;
        jj = 0;
        for (j = 0; j < n; j++)
        {
            wa1[j] = qtb[j];
            if ((r[jj] == zero) && (nsing == n))
            {
                nsing = j;
            }

            if (nsing < n)
            {
                wa1[j] = zero;
            }

            jj += ldr + 1; /* [j+ldr*j] */
        }

        if (nsing >= 1)
        {
            for (k = 0; k < nsing; k++)
            {
                j = nsing - k - 1;
                wa1[j] = wa1[j] / r[j + (ldr * j)];
                temp = wa1[j];
                jm1 = j - 1;
                if (jm1 >= 0)
                {
                    ij = ldr * j;
                    for (i = 0; i <= jm1; i++)
                    {
                        wa1[i] -= r[ij] * temp;
                        ij += 1;
                    }
                }
            }
        }

        for (j = 0; j < n; j++)
        {
            l = ipvt[j];
            x[l] = wa1[j];
        }

        /*
        *     initialize the iteration counter.
        *     evaluate the function at the origin, and test
        *     for acceptance of the gauss-newton direction.
        */
        iter = 0;
        for (j = 0; j < n; j++)
        {
            wa2[j] = diag[j] * x[j];
        }

        dxnorm = Enorm(n, wa2);
        fp = dxnorm - delta;
        if (fp <= p1 * delta)
        {
            goto L220;
        }

        /*
        *     if the jacobian is not rank deficient, the newton
        *     step provides a lower bound, parl, for the zero of
        *     the function. otherwise set this bound to zero.
        */
        parl = zero;
        if (nsing >= n)
        {
            for (j = 0; j < n; j++)
            {
                l = ipvt[j];
                wa1[j] = diag[l] * (wa2[l] / dxnorm);
            }

            jj = 0;
            for (j = 0; j < n; j++)
            {
                sum = zero;
                jm1 = j - 1;
                if (jm1 >= 0)
                {
                    ij = jj;
                    for (i = 0; i <= jm1; i++)
                    {
                        sum += r[ij] * wa1[i];
                        ij += 1;
                    }
                }

                wa1[j] = (wa1[j] - sum) / r[j + (ldr * j)];
                jj += ldr; /* [i+ldr*j] */
            }

            temp = Enorm(n, wa1);
            parl = ((fp / delta) / temp) / temp;
        }

        /*
        *     calculate an upper bound, paru, for the zero of the function.
        */
        jj = 0;
        for (j = 0; j < n; j++)
        {
            sum = zero;
            ij = jj;
            for (i = 0; i <= j; i++)
            {
                sum += r[ij] * qtb[i];
                ij += 1;
            }

            l = ipvt[j];
            wa1[j] = sum / diag[l];
            jj += ldr; /* [i+ldr*j] */
        }

        gnorm = Enorm(n, wa1);
        paru = gnorm / delta;
        if (paru == zero)
        {
            paru = DWARF / Dmin1(delta, p1);
        }

        /*
        *     if the input par lies outside of the interval (parl,paru),
        *     set par to the closer endpoint.
        */
        par = Dmax1(par, parl);
        par = Dmin1(par, paru);
        if (par == zero)
        {
            par = gnorm / dxnorm;
        }

    /*
    *     beginning of an iteration.
    */
    L150:
        iter += 1;
        /*
        *    evaluate the function at the current value of par.
        */
        if (par == zero)
        {
            par = Dmax1(DWARF, p001 * paru);
        }

        temp = Math.Sqrt(par);
        for (j = 0; j < n; j++)
        {
            wa1[j] = temp * diag[j];
        }

        Qrsolv(n, r, ldr, ipvt, wa1, qtb, x, sdiag, wa2);
        for (j = 0; j < n; j++)
        {
            wa2[j] = diag[j] * x[j];
        }

        dxnorm = Enorm(n, wa2);
        temp = fp;
        fp = dxnorm - delta;
        /*
        *    if the function is small enough, accept the current value
        *    of par. also test for the exceptional cases where parl
        *    is zero or the number of iterations has reached 10.
        */
        if ((Math.Abs(fp) <= p1 * delta)
            || ((parl == zero) && (fp <= temp) && (temp < zero))
            || (iter == 10))
        {
            goto L220;
        }

        /*
        *    compute the newton correction.
        */
        for (j = 0; j < n; j++)
        {
            l = ipvt[j];
            wa1[j] = diag[l] * (wa2[l] / dxnorm);
        }

        jj = 0;
        for (j = 0; j < n; j++)
        {
            wa1[j] = wa1[j] / sdiag[j];
            temp = wa1[j];
            jp1 = j + 1;
            if (jp1 < n)
            {
                ij = jp1 + jj;
                for (i = jp1; i < n; i++)
                {
                    wa1[i] -= r[ij] * temp;
                    ij += 1; /* [i+ldr*j] */
                }
            }

            jj += ldr; /* ldr*j */
        }

        temp = Enorm(n, wa1);
        parc = ((fp / delta) / temp) / temp;
        /*
        *    depending on the sign of the function, update parl or paru.
        */
        if (fp > zero)
        {
            parl = Dmax1(parl, par);
        }

        if (fp < zero)
        {
            paru = Dmin1(paru, par);
        }

        /*
        *    compute an improved estimate for par.
        */
        par = Dmax1(parl, par + parc);
        /*
        *    end of an iteration.
        */
        goto L150;

    L220:
        /*
        *     termination.
        */
        if (iter == 0)
        {
            par = zero;
        }

        /*
        *     last card of subroutine lmpar.
        */
    }
}
