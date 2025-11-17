#!/usr/bin/env python3
"""
portfolio_platform.py
=====================

This module provides tools to analyse a stock portfolio and a watchlist of
potential investments using a simple geometric Brownian motion model.  The
functions in this file allow you to compute summary statistics for an existing
portfolio, estimate the distribution of future prices for each holding, and
evaluate potential additions from a watchlist.  Everything is contained in
Python functions so you can import the module into a notebook or run it as
a standalone script from the command line.

The underlying price simulation uses a geometric Brownian motion (GBM) model.
GBM assumes that the logarithmic returns of an asset follow a normal
distribution characterised by two parameters: the mean return (µ) and the
volatility (σ).  According to the Analytics Vidhya article on creating a
dummy stock market using GBM, the parameters µ and σ represent the average
return (positive µ denotes a bullish trend, negative µ a bearish trend) and
the degree of price variability【227366342806898†L163-L173】.  GBM is widely
used in finance because it is mathematically tractable, though one should
remember that real markets are not perfectly normally distributed or
stationary【227366342806898†L187-L192】.

For volatility we adopt a typical annualised standard deviation of 15 %.
This number falls within the “common” range for annual stock market
volatility, where fluctuations of about 15 % around the average return are
considered normal【609012483912910†L234-L239】.  The module's default
volatility parameter can be adjusted if you have better estimates for
individual securities.

**Disclaimer**: This tool is provided for educational purposes only.  The
simulations are simplistic and do not account for factors such as changing
interest rates, dividends, economic cycles or company‑specific events.  Past
returns do not guarantee future performance.  Always do your own research
before making investment decisions.
"""

from __future__ import annotations

import json
import math
from dataclasses import dataclass, field
from typing import Dict, Iterable, List, Optional, Tuple

import numpy as np


@dataclass
class Position:
    """Represents a single holding in the portfolio."""
    ticker: str
    value: float  # current market value of the position
    return_pct: float  # historical return percentage (e.g. year‑to‑date)
    current_price: float  # current share price

    @property
    def shares(self) -> float:
        """Compute the number of shares held based on value and price."""
        if self.current_price > 0:
            return self.value / self.current_price
        return 0.0


@dataclass
class WatchItem:
    """Represents an item on the investment watchlist."""
    ticker: str
    name: str
    current_price: float
    today_return_pct: float  # today's return percentage
    total_return_pct: Optional[float] = None


def simulate_gbm_price(
    current_price: float,
    mu: float,
    sigma: float,
    days: int = 252,
    n_sims: int = 1000,
    random_seed: Optional[int] = None,
) -> np.ndarray:
    """Simulate future end‑of‑period prices using geometric Brownian motion.

    Args:
        current_price: The starting price of the asset.
        mu: Annualised drift (mean return).  Positive values represent
            expected growth, negative values represent expected decline.
        sigma: Annualised volatility (standard deviation of returns).
        days: Number of trading days to simulate.
        n_sims: Number of Monte Carlo simulation paths.
        random_seed: Optional seed for reproducibility.

    Returns:
        An array of simulated prices at the end of the period for each path.
    """
    if random_seed is not None:
        np.random.seed(random_seed)

    dt = 1.0 / 252.0  # assume 252 trading days per year
    # Pre‑compute drift and diffusion constants
    drift = (mu - 0.5 * sigma ** 2) * dt
    diffusion_std = sigma * math.sqrt(dt)
    # Simulate log returns
    prices_end = np.empty(n_sims)
    for i in range(n_sims):
        increments = np.random.normal(loc=0.0, scale=1.0, size=days)
        # cumulative log return
        log_returns = drift + diffusion_std * increments
        # accumulate
        price = current_price * np.exp(log_returns.sum())
        prices_end[i] = price
    return prices_end


def portfolio_summary(positions: Iterable[Position]) -> Dict[str, float]:
    """Calculate total value and weighted historical return of a portfolio.

    Args:
        positions: Iterable of Position objects.

    Returns:
        A dictionary with total_value and weighted_return_pct keys.
    """
    total_value = sum(pos.value for pos in positions)
    total_return_value = sum(pos.value * (pos.return_pct / 100.0) for pos in positions)
    weighted_return_pct = 0.0
    if total_value > 0:
        weighted_return_pct = (total_return_value / total_value) * 100.0
    return {
        "total_value": total_value,
        "weighted_return_pct": weighted_return_pct,
    }


def predict_position(
    position: Position,
    sigma: float = 0.15,
    days: int = 252,
    n_sims: int = 1000,
    random_seed: Optional[int] = None,
) -> Dict[str, float]:
    """Estimate the expected future price and return of a single position.

    Uses geometric Brownian motion with the mean drift equal to the
    historical return percentage provided on the position.

    Args:
        position: The position to simulate.
        sigma: Annualised volatility used for all assets.
        days: Number of trading days in the forecast horizon.
        n_sims: Number of Monte Carlo simulations.
        random_seed: Optional seed for reproducibility.

    Returns:
        A dictionary containing the expected price and expected return percentage.
    """
    mu = position.return_pct / 100.0  # convert percent to fraction
    prices_end = simulate_gbm_price(
        current_price=position.current_price,
        mu=mu,
        sigma=sigma,
        days=days,
        n_sims=n_sims,
        random_seed=random_seed,
    )
    expected_price = float(prices_end.mean())
    expected_return_pct = 0.0
    if position.current_price > 0:
        expected_return_pct = (expected_price / position.current_price - 1.0) * 100.0
    return {
        "expected_price": expected_price,
        "expected_return_pct": expected_return_pct,
    }


def analyse_portfolio(
    positions: Iterable[Position],
    sigma: float = 0.15,
    days: int = 252,
    n_sims: int = 1000,
    random_seed: Optional[int] = None,
) -> Dict[str, object]:
    """Analyse a list of positions and predict future portfolio performance.

    Returns summary statistics including current value, weighted return, predicted
    future value and predicted return.  Also includes lists of the top
    performers and laggards based on historical return percentage.
    """
    positions_list: List[Position] = list(positions)
    summary = portfolio_summary(positions_list)
    total_value = summary["total_value"]

    # Determine top and bottom performers by historical returns
    sorted_by_return = sorted(positions_list, key=lambda p: p.return_pct, reverse=True)
    top_performers = sorted_by_return[:5]
    laggards = sorted_by_return[-5:]

    # Predict each position's future price
    predictions: Dict[str, Dict[str, float]] = {}
    for pos in positions_list:
        predictions[pos.ticker] = predict_position(
            pos, sigma=sigma, days=days, n_sims=n_sims, random_seed=random_seed
        )

    # Aggregate predicted portfolio value
    predicted_portfolio_value = 0.0
    for pos in positions_list:
        shares = pos.shares
        expected_price = predictions[pos.ticker]["expected_price"]
        predicted_portfolio_value += shares * expected_price

    predicted_return_pct = 0.0
    if total_value > 0:
        predicted_return_pct = (predicted_portfolio_value / total_value - 1.0) * 100.0

    return {
        "total_value": total_value,
        "weighted_return_pct": summary["weighted_return_pct"],
        "predicted_portfolio_value": predicted_portfolio_value,
        "predicted_portfolio_return_pct": predicted_return_pct,
        "predictions": predictions,
        "top_performers": [(p.ticker, p.return_pct) for p in top_performers],
        "laggards": [(p.ticker, p.return_pct) for p in laggards],
    }


def compute_mu_from_today_return(today_return_pct: float) -> float:
    """Convert a one‑day return percentage into an annualised drift estimate.

    Because the watchlist only includes a single day's return, this function uses
    a heuristic to infer a modest annual growth rate.  It multiplies the
    percentage by a factor of two and bounds the result to within ±5 % to
    ±20 % per year.  Positive daily gains therefore translate into a small
    positive annualised drift, while negative daily moves translate into
    moderate negative drift.

    Args:
        today_return_pct: Percentage change on the most recent trading day.

    Returns:
        A bounded annualised drift parameter µ for GBM.
    """
    # Convert percentage to fraction and apply scaling
    mu_estimate = (today_return_pct / 100.0) * 2.0
    # Bound between -0.05 and +0.20
    mu_estimate = max(min(mu_estimate, 0.20), -0.05)
    return mu_estimate


def analyse_watchlist(
    watchlist: Iterable[WatchItem],
    sigma: float = 0.15,
    days: int = 252,
    n_sims: int = 1000,
    random_seed: Optional[int] = None,
) -> List[Tuple[str, float, float]]:
    """Analyse a watchlist and produce predicted returns for each item.

    Args:
        watchlist: Iterable of WatchItem objects.
        sigma: Annualised volatility assumed for all items.
        days: Number of trading days to simulate.
        n_sims: Number of simulation paths per item.
        random_seed: Optional seed for reproducibility.

    Returns:
        A list of tuples (ticker, expected_return_pct, expected_price) sorted
        by expected_return_pct in descending order.
    """
    results: List[Tuple[str, float, float]] = []
    for item in watchlist:
        mu = compute_mu_from_today_return(item.today_return_pct)
        prices_end = simulate_gbm_price(
            current_price=item.current_price,
            mu=mu,
            sigma=sigma,
            days=days,
            n_sims=n_sims,
            random_seed=random_seed,
        )
        expected_price = float(prices_end.mean())
        expected_return_pct = 0.0
        if item.current_price > 0:
            expected_return_pct = (expected_price / item.current_price - 1.0) * 100.0
        results.append((item.ticker, expected_return_pct, expected_price))
    # Sort by expected return descending
    results.sort(key=lambda x: x[1], reverse=True)
    return results


def add_positions(
    current_positions: List[Position],
    watchlist_map: Dict[str, WatchItem],
    tickers_to_add: Iterable[str],
    allocation_value: float,
) -> List[Position]:
    """Create a new portfolio by adding positions from the watchlist.

    Each new ticker will be purchased with the same allocation_value.  The
    number of shares is computed based on the current price from the watchlist.

    Args:
        current_positions: Existing positions in the portfolio.
        watchlist_map: Mapping from ticker to WatchItem.
        tickers_to_add: Iterable of ticker symbols to add.
        allocation_value: Dollar amount to allocate to each new position.

    Returns:
        A new list of Position objects including the original and newly added.
    """
    new_positions = list(current_positions)
    for ticker in tickers_to_add:
        item = watchlist_map.get(ticker)
        if item is None:
            continue
        # create Position: value is allocation_value, return_pct is unknown so set to 0
        new_positions.append(
            Position(
                ticker=item.ticker,
                value=allocation_value,
                return_pct=0.0,
                current_price=item.current_price,
            )
        )
    return new_positions


def example_usage() -> None:
    """Demonstrate how to use the platform with sample data."""
    # Sample portfolio and watchlist data (can be replaced with JSON input)
    portfolio_json = """
    [
        {"ticker": "IBM", "value": 28.35, "return_pct": 18.92, "current_price": 302.50},
        {"ticker": "AVAV", "value": 37.06, "return_pct": 23.54, "current_price": 287.82},
        {"ticker": "ORA", "value": 109.40, "return_pct": 23.54, "current_price": 109.21}
    ]
    """
    watchlist_json = """
    [
        {"ticker": "MRK", "name": "Merck & Co", "current_price": 94.18, "today_return_pct": 1.79},
        {"ticker": "GEV", "name": "GE Vernova Inc", "current_price": 585.82, "today_return_pct": 1.94},
        {"ticker": "MOD", "name": "Modine Manufacturing", "current_price": 134.22, "today_return_pct": 1.97}
    ]
    """
    positions = [Position(**p) for p in json.loads(portfolio_json)]
    watchlist = [WatchItem(**w) for w in json.loads(watchlist_json)]

    # Analyse portfolio
    analysis = analyse_portfolio(positions, sigma=0.15, days=252, n_sims=300, random_seed=42)
    print("Current total value:", analysis["total_value"])
    print("Weighted historical return (%):", analysis["weighted_return_pct"])
    print("Predicted future value:", analysis["predicted_portfolio_value"])
    print("Predicted portfolio return (%):", analysis["predicted_portfolio_return_pct"])

    # Analyse watchlist
    watch_results = analyse_watchlist(watchlist, sigma=0.15, days=252, n_sims=300, random_seed=42)
    print("\nWatchlist predictions (top to bottom):")
    for ticker, exp_ret, exp_price in watch_results:
        print(f"{ticker}: expected return {exp_ret:.2f}% -> expected price {exp_price:.2f}")

    # Add MRK to portfolio with $10 allocation and reanalyse
    watch_map = {w.ticker: w for w in watchlist}
    new_positions = add_positions(positions, watch_map, ["MRK"], allocation_value=10.0)
    new_analysis = analyse_portfolio(new_positions, sigma=0.15, days=252, n_sims=300, random_seed=42)
    print("\nAfter adding MRK with $10 allocation:")
    print("New total value:", new_analysis["total_value"])
    print("Predicted future value:", new_analysis["predicted_portfolio_value"])
    print("Predicted return (%):", new_analysis["predicted_portfolio_return_pct"])


if __name__ == "__main__":
    # When run as a script, demonstrate usage.  For real use cases you can
    # replace example_usage() with custom loading of JSON from a file or input.
    example_usage()