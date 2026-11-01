// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

/**
 * A loss model's distribution: its normalized buckets' edges, widths, densities, cumulative and excess probabilities
 * and averages.
 *
 * @param x the edges
 * @param dx the widths
 * @param density the densities
 * @param cumulative the cumulative probabilities
 * @param excess the excess probabilities
 * @param average the averages
 */
public record Distribution(double[] x, double[] dx, double[] density, double[] cumulative, double[] excess, double[] average) {
}
