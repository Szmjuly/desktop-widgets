#!/usr/bin/env python3
"""
Interactive Portfolio Analysis Dashboard
========================================

A Streamlit-based interactive dashboard for portfolio analysis with:
- Interactive input forms for portfolio positions
- Real-time predictions and visualizations
- Multiple prediction scenarios
- Portfolio growth charts over time
"""

import streamlit as st
import pandas as pd
import plotly.graph_objects as go
import plotly.express as px
from plotly.subplots import make_subplots
import numpy as np
import json
import os
from datetime import datetime, timedelta
from typing import List, Dict, Optional, Tuple
import yfinance as yf
from main import (
    Position,
    WatchItem,
    analyse_portfolio,
    analyse_watchlist,
    simulate_gbm_price,
    portfolio_summary,
    predict_position,
    add_positions,
)

# Page configuration
st.set_page_config(
    page_title="Portfolio Analysis Dashboard",
    page_icon="📈",
    layout="wide",
    initial_sidebar_state="expanded"
)

# Custom CSS for better styling
st.markdown("""
    <style>
    .main-header {
        font-size: 2.5rem;
        font-weight: bold;
        color: #1f77b4;
        margin-bottom: 1rem;
    }
    .metric-card {
        background-color: #f0f2f6;
        padding: 1rem;
        border-radius: 0.5rem;
        margin: 0.5rem 0;
    }
    </style>
""", unsafe_allow_html=True)


def normalize_ticker_for_yahoo(ticker: str) -> str:
    """Convert ticker symbols to Yahoo Finance format.
    
    Some tickers need special handling:
    - BRKB -> BRK-B (Berkshire Hathaway Class B)
    - BRKA -> BRK-A (Berkshire Hathaway Class A)
    """
    ticker_map = {
        "BRKB": "BRK-B",
        "BRKA": "BRK-A",
        "BRK.B": "BRK-B",
        "BRK.A": "BRK-A",
    }
    return ticker_map.get(ticker.upper(), ticker.upper())


@st.cache_data(ttl=60, show_spinner=False)  # Cache for 60 seconds
def fetch_stock_data(ticker: str) -> Optional[Dict]:
    """Fetch real-time stock data from Yahoo Finance."""
    try:
        # Normalize ticker for Yahoo Finance
        yahoo_ticker = normalize_ticker_for_yahoo(ticker)
        stock = yf.Ticker(yahoo_ticker)
        
        # Get historical data first (more reliable)
        hist = stock.history(period="1y")
        
        if hist.empty:
            # Try to get info to see if ticker exists but has no history
            try:
                info = stock.info
                if info and 'currentPrice' in info:
                    # Use current price from info if available
                    current_price = info.get('currentPrice', 0)
                    if current_price > 0:
                        name = info.get("longName", info.get("shortName", ticker))
                        return {
                            "current_price": float(current_price),
                            "today_return_pct": 0.0,  # Can't calculate without history
                            "ytd_return_pct": 0.0,
                            "name": name,
                        }
            except:
                pass
            return None
        
        current_price = hist['Close'].iloc[-1]
        prev_close = hist['Close'].iloc[-2] if len(hist) > 1 else current_price
        today_return_pct = ((current_price - prev_close) / prev_close) * 100 if prev_close > 0 else 0
        
        # Calculate YTD return
        if len(hist) > 0:
            year_start_price = hist['Close'].iloc[0]
            ytd_return_pct = ((current_price - year_start_price) / year_start_price) * 100 if year_start_price > 0 else 0
        else:
            ytd_return_pct = 0
        
        # Try to get company name, but don't fail if it's unavailable
        try:
            info = stock.info
            name = info.get("longName", info.get("shortName", ticker))
        except:
            name = ticker
        
        return {
            "current_price": float(current_price),
            "today_return_pct": float(today_return_pct),
            "ytd_return_pct": float(ytd_return_pct),
            "name": name,
        }
    except Exception as e:
        # Silently return None on error - errors are handled gracefully in the UI
        # This prevents console spam from delisted/invalid tickers
        return None


@st.cache_data(ttl=60, show_spinner=False)
def fetch_multiple_stocks(tickers: List[str]) -> Dict[str, Dict]:
    """Fetch data for multiple stocks efficiently."""
    results = {}
    for ticker in tickers:
        data = fetch_stock_data(ticker)
        if data:
            results[ticker] = data
        else:
            # Try with normalized ticker if original failed
            normalized = normalize_ticker_for_yahoo(ticker)
            if normalized != ticker.upper():
                data = fetch_stock_data(normalized)
                if data:
                    results[ticker] = data
    return results


def load_portfolio_data(json_path: str = "portfolio_data.json") -> Tuple[List[Dict], List[Dict]]:
    """Load portfolio and watchlist from JSON file."""
    if os.path.exists(json_path):
        with open(json_path, 'r') as f:
            data = json.load(f)
            return data.get("portfolio", []), data.get("watchlist", [])
    return [], []


def update_positions_with_realtime(positions: List[Dict], use_realtime: bool = True) -> List[Dict]:
    """Update positions with real-time data from Yahoo Finance."""
    if not use_realtime:
        return positions
    
    tickers = [pos["ticker"] for pos in positions]
    realtime_data = fetch_multiple_stocks(tickers)
    
    updated_positions = []
    for pos in positions:
        ticker = pos["ticker"]
        if ticker in realtime_data:
            data = realtime_data[ticker]
            # Calculate shares from original position
            original_price = pos.get("current_price", 1.0)
            if original_price > 0:
                shares = pos["value"] / original_price
            else:
                shares = 0
            
            # Update price and return, recalculate value based on shares
            updated_pos = pos.copy()
            updated_pos["current_price"] = data["current_price"]
            updated_pos["return_pct"] = data["ytd_return_pct"]
            # Recalculate value based on shares and new price
            updated_pos["value"] = shares * data["current_price"]
            updated_positions.append(updated_pos)
        else:
            updated_positions.append(pos)
    
    return updated_positions


@st.cache_data(ttl=60, show_spinner=False)
def update_watchlist_with_realtime_cached(
    watchlist_tickers: Tuple[str],
    use_realtime: bool = True,
) -> Dict[str, Dict]:
    """Cached watchlist update - returns dict of ticker -> updated data."""
    if not use_realtime:
        return {}
    
    tickers = list(watchlist_tickers)
    realtime_data = fetch_multiple_stocks(tickers)
    return realtime_data


def update_watchlist_with_realtime(watchlist: List[Dict], use_realtime: bool = True) -> List[Dict]:
    """Update watchlist with real-time data from Yahoo Finance."""
    if not use_realtime:
        return watchlist
    
    # Use cached function
    tickers_tuple = tuple(item["ticker"] for item in watchlist)
    realtime_data = update_watchlist_with_realtime_cached(tickers_tuple, use_realtime)
    
    updated_watchlist = []
    for item in watchlist:
        ticker = item["ticker"]
        if ticker in realtime_data:
            data = realtime_data[ticker]
            updated_item = item.copy()
            updated_item["current_price"] = data["current_price"]
            updated_item["today_return_pct"] = data["today_return_pct"]
            updated_item["name"] = data["name"]
            updated_watchlist.append(updated_item)
        else:
            updated_watchlist.append(item)
    
    return updated_watchlist


def _simulate_portfolio_growth_over_time_internal(
    positions_data: List[Dict],  # Use dict representation for caching
    sigma: float = 0.15,
    days: int = 252,
    n_sims: int = 100,
    random_seed: Optional[int] = None,
    dca_schedule: Optional[Dict[str, Dict]] = None,
) -> tuple:
    """Internal simulation function that works with hashable data."""
    # Convert dicts to Position objects
    positions = [Position(**p) for p in positions_data]
    
    if random_seed is not None:
        np.random.seed(random_seed)
    
    dt = 1.0 / 252.0
    drift_constants = {}
    diffusion_stds = {}
    
    # Pre-compute constants for each position
    for pos in positions:
        mu = pos.return_pct / 100.0
        drift_constants[pos.ticker] = (mu - 0.5 * sigma ** 2) * dt
        diffusion_stds[pos.ticker] = sigma * np.sqrt(dt)
    
    # Simulate paths over time - sample every 10 days
    sample_interval = max(1, days // 50)  # Sample ~50 points
    time_points = list(range(0, days + 1, sample_interval))
    if time_points[-1] != days:
        time_points.append(days)
    time_points = np.array(time_points)
    
    portfolio_paths = np.zeros((n_sims, len(time_points)))
    
    # Initialize DCA tracking
    dca_schedule = dca_schedule or {}
    position_shares = {pos.ticker: pos.shares for pos in positions}
    
    for sim_idx in range(n_sims):
        portfolio_value_over_time = []
        current_total = sum(pos.value for pos in positions)
        portfolio_value_over_time.append(current_total)
        
        # Track cumulative log returns and shares for each position
        cumulative_log_returns = {pos.ticker: 0.0 for pos in positions}
        current_shares = position_shares.copy()
        
        for day in range(1, days + 1):
            total_value = 0.0
            
            # Process DCA contributions
            for ticker, dca_config in dca_schedule.items():
                if day % dca_config['frequency_days'] == 0:
                    # Find the position
                    pos = next((p for p in positions if p.ticker == ticker), None)
                    if pos:
                        # Calculate current price
                        current_price = pos.current_price * np.exp(cumulative_log_returns[ticker])
                        # Buy shares with DCA amount
                        dca_amount = dca_config['amount']
                        shares_to_add = dca_amount / current_price if current_price > 0 else 0
                        current_shares[ticker] += shares_to_add
            
            # Calculate portfolio value
            for pos in positions:
                # Generate daily random increment
                increment = np.random.normal(loc=0.0, scale=1.0)
                daily_log_return = drift_constants[pos.ticker] + diffusion_stds[pos.ticker] * increment
                cumulative_log_returns[pos.ticker] += daily_log_return
                
                # Calculate price at this day
                price = pos.current_price * np.exp(cumulative_log_returns[pos.ticker])
                shares = current_shares[pos.ticker]
                total_value += shares * price
            
            # Store value if this is a sample point
            if day in time_points[1:]:
                portfolio_value_over_time.append(total_value)
        
        portfolio_paths[sim_idx] = portfolio_value_over_time
    
    return portfolio_paths, time_points


@st.cache_data(ttl=3600, show_spinner=False)  # Cache for 1 hour - simulations are expensive
def simulate_portfolio_growth_over_time_cached(
    positions_data: Tuple[Dict],  # Tuple of dicts for hashability
    sigma: float = 0.15,
    days: int = 252,
    n_sims: int = 100,
    random_seed: Optional[int] = None,
    dca_schedule: Optional[Tuple] = None,  # Tuple for hashability
) -> tuple:
    """Cached wrapper for simulation."""
    positions_list = list(positions_data)
    dca_dict = dict(dca_schedule) if dca_schedule else None
    return _simulate_portfolio_growth_over_time_internal(
        positions_list, sigma, days, n_sims, random_seed, dca_dict
    )


def simulate_portfolio_growth_over_time(
    positions: List[Position],
    sigma: float = 0.15,
    days: int = 252,
    n_sims: int = 100,
    random_seed: Optional[int] = None,
    dca_schedule: Optional[Dict[str, Dict]] = None,
) -> tuple:
    """Simulate portfolio value over time for multiple paths (with caching)."""
    # Convert to hashable format for caching
    positions_data = tuple({
        'ticker': pos.ticker,
        'value': pos.value,
        'return_pct': pos.return_pct,
        'current_price': pos.current_price,
    } for pos in positions)
    
    dca_tuple = tuple(sorted(dca_schedule.items())) if dca_schedule else None
    
    return simulate_portfolio_growth_over_time_cached(
        positions_data, sigma, days, n_sims, random_seed, dca_tuple
    )


def create_portfolio_growth_chart(
    portfolio_paths: np.ndarray,
    time_points: np.ndarray,
    current_value: float,
    scenario_name: str = "Current Portfolio",
    color: str = "#1f77b4",
) -> go.Figure:
    """Create an interactive chart showing portfolio growth over time."""
    fig = go.Figure()
    
    # Calculate percentiles for confidence bands
    p5_values = np.percentile(portfolio_paths, 5, axis=0)
    p25_values = np.percentile(portfolio_paths, 25, axis=0)
    p75_values = np.percentile(portfolio_paths, 75, axis=0)
    p95_values = np.percentile(portfolio_paths, 95, axis=0)
    median_values = np.percentile(portfolio_paths, 50, axis=0)
    
    # Convert hex color to RGB for better visibility
    r, g, b = int(color[1:3], 16), int(color[3:5], 16), int(color[5:7], 16)
    
    # Add 95% confidence band (5th to 95th percentile) - lightest shade
    fig.add_trace(go.Scatter(
        x=np.concatenate([time_points, time_points[::-1]]),
        y=np.concatenate([p95_values, p5_values[::-1]]),
        fill='toself',
        fillcolor=f'rgba({r}, {g}, {b}, 0.15)',
        line=dict(color='rgba(255,255,255,0)'),
        name=f'{scenario_name} - 90% Range',
        showlegend=True,
        hoverinfo='skip',
    ))
    
    # Add 50% confidence band (25th to 75th percentile) - medium shade
    fig.add_trace(go.Scatter(
        x=np.concatenate([time_points, time_points[::-1]]),
        y=np.concatenate([p75_values, p25_values[::-1]]),
        fill='toself',
        fillcolor=f'rgba({r}, {g}, {b}, 0.3)',
        line=dict(color='rgba(255,255,255,0)'),
        name=f'{scenario_name} - 50% Range',
        showlegend=True,
        hoverinfo='skip',
    ))
    
    # Add median line (most likely outcome) - use darker, more visible color
    darker_color = f'rgb({max(0, r-30)}, {max(0, g-30)}, {max(0, b-30)})'
    fig.add_trace(go.Scatter(
        x=time_points,
        y=median_values,
        mode='lines',
        name=f'{scenario_name} - Expected',
        line=dict(color=darker_color, width=4),
        hovertemplate='<b>%{fullData.name}</b><br>' +
                      'Days: %{x}<br>' +
                      'Value: $%{y:,.2f}<br>' +
                      '<extra></extra>',
    ))
    
    # Add current value marker (starting point)
    fig.add_trace(go.Scatter(
        x=[0],
        y=[current_value],
        mode='markers+text',
        name='Starting Value',
        marker=dict(size=12, color='#2ecc71', symbol='circle', line=dict(width=2, color='white')),
        text=[f'${current_value:,.0f}'],
        textposition='top center',
        textfont=dict(size=12, color='#2ecc71', family='Arial Black'),
        showlegend=False,
        hovertemplate='<b>Starting Value</b><br>$%{y:,.2f}<extra></extra>',
    ))
    
    # Add final value annotations
    final_median = median_values[-1]
    final_p5 = p5_values[-1]
    final_p95 = p95_values[-1]
    
    fig.update_layout(
        title=dict(
            text=f'<b>Portfolio Growth Projections</b><br><sub>{scenario_name}</sub>',
            x=0.5,
            xanchor='center',
            font=dict(size=20, color='#2c3e50')
        ),
        xaxis=dict(
            title=dict(text='Time (Trading Days)', font=dict(size=14, color='#34495e')),
            showgrid=True,
            gridcolor='rgba(0,0,0,0.1)',
        ),
        yaxis=dict(
            title=dict(text='Portfolio Value ($)', font=dict(size=14, color='#34495e')),
            tickformat='$,.0f',
            showgrid=True,
            gridcolor='rgba(0,0,0,0.1)',
        ),
        hovermode='x unified',
        height=600,
        template='plotly_white',
        plot_bgcolor='white',
        paper_bgcolor='white',
        legend=dict(
            orientation="h",
            yanchor="bottom",
            y=1.02,
            xanchor="right",
            x=1,
            font=dict(size=12, color='#2c3e50', family='Arial'),
            bgcolor='rgba(255,255,255,0.9)',
            bordercolor='#34495e',
            borderwidth=1,
        ),
        annotations=[
            dict(
                x=time_points[-1],
                y=final_median,
                xref="x",
                yref="y",
                text=f"Expected: ${final_median:,.0f}",
                showarrow=True,
                arrowhead=2,
                arrowcolor=darker_color,
                ax=0,
                ay=-40,
                font=dict(size=12, color=darker_color, family='Arial'),
            ),
        ],
    )
    
    return fig


def create_multi_scenario_growth_chart(
    scenarios: Dict[str, tuple],  # Dict of scenario_name: (portfolio_paths, time_points, current_value, color)
    base_current_value: float,
) -> go.Figure:
    """Create a chart comparing multiple portfolio scenarios."""
    fig = go.Figure()
    
    colors = ['#3498db', '#e74c3c', '#2ecc71', '#f39c12', '#9b59b6', '#1abc9c']
    
    for idx, (scenario_name, (portfolio_paths, time_points, current_value, scenario_color)) in enumerate(scenarios.items()):
        color = scenario_color if scenario_color else colors[idx % len(colors)]
        
        # Calculate median and confidence bands
        median_values = np.percentile(portfolio_paths, 50, axis=0)
        p5_values = np.percentile(portfolio_paths, 5, axis=0)
        p95_values = np.percentile(portfolio_paths, 95, axis=0)
        
        # Add confidence band
        fig.add_trace(go.Scatter(
            x=np.concatenate([time_points, time_points[::-1]]),
            y=np.concatenate([p95_values, p5_values[::-1]]),
            fill='toself',
            fillcolor=f'rgba({int(color[1:3], 16)}, {int(color[3:5], 16)}, {int(color[5:7], 16)}, 0.15)',
            line=dict(color='rgba(255,255,255,0)'),
            name=f'{scenario_name} - Range',
            showlegend=False,
            hoverinfo='skip',
        ))
        
        # Add median line - use darker color for visibility
        r, g, b = int(color[1:3], 16), int(color[3:5], 16), int(color[5:7], 16)
        darker_color = f'rgb({max(0, r-40)}, {max(0, g-40)}, {max(0, b-40)})'
        fig.add_trace(go.Scatter(
            x=time_points,
            y=median_values,
            mode='lines',
            name=scenario_name,
            line=dict(color=darker_color, width=4),
            hovertemplate=f'<b>{scenario_name}</b><br>' +
                          'Days: %{x}<br>' +
                          'Value: $%{y:,.2f}<br>' +
                          '<extra></extra>',
        ))
    
    # Add starting value marker
    fig.add_trace(go.Scatter(
        x=[0],
        y=[base_current_value],
        mode='markers+text',
        name='Starting Value',
        marker=dict(size=12, color='#2ecc71', symbol='circle', line=dict(width=2, color='white')),
        text=[f'${base_current_value:,.0f}'],
        textposition='top center',
        textfont=dict(size=12, color='#2ecc71', family='Arial Black'),
        showlegend=False,
        hovertemplate='<b>Starting Value</b><br>$%{y:,.2f}<extra></extra>',
    ))
    
    fig.update_layout(
        title=dict(
            text='<b>Portfolio Growth Comparison</b><br><sub>Multiple Scenarios Side-by-Side</sub>',
            x=0.5,
            xanchor='center',
            font=dict(size=20, color='#2c3e50')
        ),
        xaxis=dict(
            title=dict(text='Time (Trading Days)', font=dict(size=14, color='#34495e')),
            showgrid=True,
            gridcolor='rgba(0,0,0,0.1)',
        ),
        yaxis=dict(
            title=dict(text='Portfolio Value ($)', font=dict(size=14, color='#34495e')),
            tickformat='$,.0f',
            showgrid=True,
            gridcolor='rgba(0,0,0,0.1)',
        ),
        hovermode='x unified',
        height=600,
        template='plotly_white',
        plot_bgcolor='white',
        paper_bgcolor='white',
        legend=dict(
            orientation="h",
            yanchor="bottom",
            y=1.02,
            xanchor="right",
            x=1,
            font=dict(size=12, color='#2c3e50', family='Arial'),
            bgcolor='rgba(255,255,255,0.9)',
            bordercolor='#34495e',
            borderwidth=1,
        ),
    )
    
    return fig


def create_prediction_comparison_chart(
    scenarios: Dict[str, Dict],
    current_value: float,
) -> go.Figure:
    """Create a chart comparing different prediction scenarios."""
    fig = go.Figure()
    
    scenario_names = list(scenarios.keys())
    predicted_values = [scenarios[s]['predicted_portfolio_value'] for s in scenario_names]
    returns = [scenarios[s]['predicted_portfolio_return_pct'] for s in scenario_names]
    
    # Create subplots
    fig = make_subplots(
        rows=1, cols=2,
        subplot_titles=('Predicted Portfolio Value', 'Predicted Return %'),
        specs=[[{"type": "bar"}, {"type": "bar"}]]
    )
    
    # Value chart
    fig.add_trace(
        go.Bar(
            x=scenario_names,
            y=predicted_values,
            name='Predicted Value',
            marker_color='lightblue',
            text=[f'${v:,.2f}' for v in predicted_values],
            textposition='outside',
        ),
        row=1, col=1
    )
    
    # Add current value line
    fig.add_hline(
        y=current_value,
        line_dash="dash",
        line_color="red",
        annotation_text="Current Value",
        row=1, col=1
    )
    
    # Return chart
    fig.add_trace(
        go.Bar(
            x=scenario_names,
            y=returns,
            name='Predicted Return %',
            marker_color='lightgreen',
            text=[f'{r:.2f}%' for r in returns],
            textposition='outside',
        ),
        row=1, col=2
    )
    
    fig.update_layout(
        title='Prediction Scenarios Comparison',
        height=400,
        showlegend=False,
        template='plotly_white',
    )
    
    fig.update_xaxes(title_text="Scenario", row=1, col=1)
    fig.update_xaxes(title_text="Scenario", row=1, col=2)
    fig.update_yaxes(title_text="Value ($)", row=1, col=1)
    fig.update_yaxes(title_text="Return (%)", row=1, col=2)
    
    return fig


def main():
    st.title("📈 Interactive Portfolio Analysis Dashboard")
    st.markdown("---")
    
    # Sidebar for inputs
    with st.sidebar:
        st.header("⚙️ Configuration")
        
        # Simulation parameters
        st.subheader("Simulation Parameters")
        sigma = st.slider(
            "Volatility (σ)",
            min_value=0.05,
            max_value=0.50,
            value=0.15,
            step=0.01,
            help="Annualized volatility (standard deviation of returns)"
        )
        
        days = st.slider(
            "Time Horizon (days)",
            min_value=30,
            max_value=1000,
            value=252,
            step=30,
            help="Number of trading days to simulate"
        )
        
        n_sims = st.slider(
            "Number of Simulations",
            min_value=50,
            max_value=1000,
            value=100,  # Reduced default for faster initial load
            step=50,
            help="More simulations = more accurate but slower. Start with 100 for faster results."
        )
        
        random_seed = st.number_input(
            "Random Seed (for reproducibility)",
            min_value=0,
            value=42,
            step=1
        )
        
        st.markdown("---")
        
        # Data source configuration
        st.subheader("Data Source")
        use_realtime = st.checkbox(
            "Use Real-time Data (Yahoo Finance)",
            value=True,
            help="Fetch current prices and returns from Yahoo Finance. Uncheck to use JSON file data only."
        )
        
        if st.button("🔄 Refresh Data", help="Refresh real-time data from Yahoo Finance"):
            st.cache_data.clear()
            # Clear watchlist cache in session state
            keys_to_remove = [k for k in st.session_state.keys() if k.startswith('watchlist_')]
            for key in keys_to_remove:
                del st.session_state[key]
            st.rerun()
        
        # Show data status
        if use_realtime:
            st.info("📡 Using real-time data from Yahoo Finance (cached for 60 seconds)")
        else:
            st.info("📄 Using data from portfolio_data.json file")
        
        st.markdown("---")
        
        # Portfolio management
        st.subheader("Portfolio Management")
        
        # Initialize session state from JSON file
        if 'positions' not in st.session_state or 'watchlist' not in st.session_state:
            portfolio_data, watchlist_data = load_portfolio_data()
            st.session_state.positions = portfolio_data
            st.session_state.watchlist = watchlist_data
        
        # Load button to reload from JSON
        if st.button("📂 Reload from JSON"):
            portfolio_data, watchlist_data = load_portfolio_data()
            st.session_state.positions = portfolio_data
            st.session_state.watchlist = watchlist_data
            st.success("Data reloaded from portfolio_data.json")
            st.rerun()
        
        # Add new position form
        with st.expander("➕ Add New Position"):
            new_ticker = st.text_input("Ticker Symbol", key="new_ticker")
            new_value = st.number_input("Position Value ($)", min_value=0.0, value=10000.0, key="new_value")
            new_return = st.number_input("Historical Return (%)", value=10.0, key="new_return")
            new_price = st.number_input("Current Price ($)", min_value=0.01, value=100.0, key="new_price")
            
            if st.button("Add Position"):
                st.session_state.positions.append({
                    "ticker": new_ticker.upper(),
                    "value": new_value,
                    "return_pct": new_return,
                    "current_price": new_price,
                })
                st.success(f"Added {new_ticker.upper()}")
                st.rerun()
        
        # Edit existing positions
        with st.expander("✏️ Edit Positions"):
            for idx, pos in enumerate(st.session_state.positions):
                st.markdown(f"**{pos['ticker']}**")
                col1, col2 = st.columns(2)
                with col1:
                    new_value = st.number_input(
                        "Value ($)",
                        min_value=0.0,
                        value=pos['value'],
                        key=f"val_{idx}"
                    )
                    new_return = st.number_input(
                        "Return (%)",
                        value=pos['return_pct'],
                        key=f"ret_{idx}"
                    )
                with col2:
                    new_price = st.number_input(
                        "Price ($)",
                        min_value=0.01,
                        value=pos['current_price'],
                        key=f"price_{idx}"
                    )
                    if st.button("Update", key=f"upd_{idx}"):
                        st.session_state.positions[idx] = {
                            "ticker": pos['ticker'],
                            "value": new_value,
                            "return_pct": new_return,
                            "current_price": new_price,
                        }
                        st.rerun()
                
                if st.button("Delete", key=f"del_{idx}"):
                    st.session_state.positions.pop(idx)
                    st.rerun()
                
                st.markdown("---")
        
        st.markdown("---")
    
    # Main content area
    if not st.session_state.positions:
        st.warning("Please add at least one position to your portfolio.")
        return
    
    # Update positions with real-time data if enabled (optimized to avoid blocking)
    if use_realtime and st.session_state.positions:
        tickers = [pos["ticker"] for pos in st.session_state.positions]
        # Use cached fetch - won't block if already cached
        realtime_data = fetch_multiple_stocks(tickers)
        failed_tickers = [t for t in tickers if t not in realtime_data]
        if failed_tickers:
            st.warning(f"⚠️ Could not fetch real-time data for: {', '.join(failed_tickers)}. Using JSON data for these tickers.")
        
        # Update positions efficiently
        updated_positions = []
        for pos in st.session_state.positions:
            ticker = pos["ticker"]
            if ticker in realtime_data:
                data = realtime_data[ticker]
                original_price = pos.get("current_price", 1.0)
                if original_price > 0:
                    shares = pos["value"] / original_price
                else:
                    shares = 0
                updated_pos = pos.copy()
                updated_pos["current_price"] = data["current_price"]
                updated_pos["return_pct"] = data["ytd_return_pct"]
                updated_pos["value"] = shares * data["current_price"]
                updated_positions.append(updated_pos)
            else:
                updated_positions.append(pos)
    else:
        updated_positions = st.session_state.positions
    
    # Convert to Position objects
    positions = [Position(**p) for p in updated_positions]
    
    # DCA (Dollar Cost Averaging) Configuration - moved after positions are defined
    with st.sidebar:
        st.subheader("💰 Dollar Cost Averaging (DCA)")
        enable_dca = st.checkbox("Enable DCA", help="Add regular contributions to specific positions")
        
        if enable_dca:
            if 'dca_configs' not in st.session_state:
                st.session_state.dca_configs = []
            
            st.markdown("**Configure DCA Plans:**")
            ticker_options = [pos.ticker for pos in positions]
            
            for idx, dca in enumerate(st.session_state.dca_configs):
                col1, col2, col3 = st.columns([2, 2, 1])
                with col1:
                    ticker = st.selectbox(
                        "Ticker",
                        options=ticker_options,
                        index=ticker_options.index(dca['ticker']) if dca['ticker'] in ticker_options else 0,
                        key=f"dca_ticker_{idx}"
                    )
                with col2:
                    amount = st.number_input(
                        "Amount ($)",
                        min_value=0.0,
                        value=dca['amount'],
                        step=10.0,
                        key=f"dca_amount_{idx}"
                    )
                with col3:
                    freq_options = [7, 14, 21, 30, 60, 90]
                    freq_index = freq_options.index(dca['frequency_days']) if dca['frequency_days'] in freq_options else 2
                    frequency = st.selectbox(
                        "Frequency",
                        options=freq_options,
                        format_func=lambda x: f"{x}d",
                        index=freq_index,
                        key=f"dca_freq_{idx}"
                    )
                
                col1, col2 = st.columns([1, 4])
                with col1:
                    if st.button("Remove", key=f"dca_remove_{idx}"):
                        st.session_state.dca_configs.pop(idx)
                        st.rerun()
                
                st.session_state.dca_configs[idx] = {
                    'ticker': ticker,
                    'amount': amount,
                    'frequency_days': frequency
                }
            
            # Add new DCA plan
            if st.button("➕ Add DCA Plan"):
                st.session_state.dca_configs.append({
                    'ticker': ticker_options[0] if ticker_options else '',
                    'amount': 100.0,
                    'frequency_days': 30
                })
                st.rerun()
    
    # Calculate current portfolio summary
    summary = portfolio_summary(positions)
    current_value = summary["total_value"]
    weighted_return = summary["weighted_return_pct"]
    
    # Display key metrics
    col1, col2, col3, col4 = st.columns(4)
    with col1:
        st.metric("Total Portfolio Value", f"${current_value:,.2f}")
    with col2:
        st.metric("Weighted Return", f"{weighted_return:.2f}%")
    
    # Run analysis
    analysis = analyse_portfolio(
        positions,
        sigma=sigma,
        days=days,
        n_sims=n_sims,
        random_seed=random_seed,
    )
    
    with col3:
        st.metric(
            "Predicted Value",
            f"${analysis['predicted_portfolio_value']:,.2f}",
            delta=f"{analysis['predicted_portfolio_return_pct']:.2f}%"
        )
    with col4:
        expected_gain = analysis['predicted_portfolio_value'] - current_value
        st.metric("Expected Gain", f"${expected_gain:,.2f}")
    
    st.markdown("---")
    
    # Tabs for different views
    tab1, tab2, tab3, tab4, tab5 = st.tabs([
        "📊 Growth Over Time",
        "🔮 Prediction Scenarios",
        "📋 Position Details",
        "📈 Individual Predictions",
        "👀 Watchlist"
    ])
    
    with tab1:
        st.header("📊 Portfolio Growth Predictions Over Time")
        
        # Explanation
        with st.expander("ℹ️ How to Read This Chart & Prediction Methodology", expanded=False):
            st.markdown("""
            **Understanding the Chart:**
            - **Green Circle**: Your starting portfolio value
            - **Bold Colored Line**: The most likely (expected) growth path
            - **Light Shaded Areas**: Possible outcomes (90% of simulations fall within the outer range)
            - **Medium Shaded Areas**: More likely outcomes (50% of simulations fall within the inner range)
            
            **How Predictions Work:**
            The predictions use **Geometric Brownian Motion (GBM)**, a mathematical model that simulates stock price movements:
            - **Drift (μ)**: Based on each position's historical return percentage (from your portfolio data)
            - **Volatility (σ)**: The uncertainty/risk level (adjustable in sidebar)
            - **Monte Carlo Simulation**: Runs thousands of random scenarios to show possible outcomes
            
            **What This Means:**
            The chart shows thousands of possible future scenarios. The wider the shaded areas, the more uncertainty. 
            The bold line shows the most likely outcome based on historical performance patterns.
            
            **Note**: Predictions assume historical returns continue. Real markets are unpredictable and past performance 
            doesn't guarantee future results. This is for educational purposes only.
            """)
        
        # Scenario selection
        col1, col2 = st.columns([2, 1])
        with col1:
            view_mode = st.radio(
                "View Mode",
                ["Single Portfolio", "Compare Scenarios", "What-If Analysis"],
                horizontal=True,
                help="Choose how you want to view the growth predictions"
            )
        
        with col2:
            show_explanation = st.checkbox("Show Detailed Stats", value=True)
        
        # Get DCA schedule from session state
        dca_schedule = {}
        if 'dca_configs' in st.session_state and st.session_state.dca_configs:
            for dca in st.session_state.dca_configs:
                if dca.get('ticker') and dca.get('amount', 0) > 0:
                    dca_schedule[dca['ticker']] = {
                        'amount': dca['amount'],
                        'frequency_days': dca.get('frequency_days', 30)
                    }
        
        # Show DCA summary if enabled
        if dca_schedule:
            st.info(f"💰 DCA Active: {len(dca_schedule)} position(s) receiving regular contributions")
            for ticker, config in dca_schedule.items():
                st.caption(f"  • {ticker}: ${config['amount']:.2f} every {config['frequency_days']} days")
        
        # Simulate base portfolio with loading indicator
        # Check if we have cached results first
        positions_tuple = tuple((p.ticker, p.value, p.return_pct, p.current_price) for p in positions)
        dca_tuple = tuple(sorted(dca_schedule.items())) if dca_schedule else None
        cache_hash = hash((positions_tuple, sigma, days, n_sims, random_seed, dca_tuple))
        cache_key = f"base_sim_{cache_hash}"
        
        if cache_key in st.session_state:
            base_portfolio_paths, time_points = st.session_state[cache_key]
        else:
            with st.status("🔄 Running simulations... Please wait.", expanded=True) as status:
                status.update(label="🔄 Running simulations... This may take a moment.", state="running")
                base_portfolio_paths, time_points = simulate_portfolio_growth_over_time(
                    positions,
                    sigma=sigma,
                    days=days,
                    n_sims=n_sims,
                    random_seed=random_seed,
                    dca_schedule=dca_schedule,
                )
                status.update(label="✅ Simulations complete!", state="complete")
                # Cache the result
                st.session_state[cache_key] = (base_portfolio_paths, time_points)
        
        if view_mode == "Single Portfolio":
            # Single portfolio view with enhanced chart
            growth_chart = create_portfolio_growth_chart(
                base_portfolio_paths,
                time_points,
                current_value,
                scenario_name="Current Portfolio",
                color="#3498db",
            )
            st.plotly_chart(growth_chart, width='stretch', key='growth_chart')
            
            if show_explanation:
                final_values = base_portfolio_paths[:, -1]
                st.subheader("📈 Projection Statistics")
                col1, col2, col3, col4 = st.columns(4)
                with col1:
                    st.metric("Expected Final Value", f"${np.median(final_values):,.2f}")
                with col2:
                    st.metric("Best Case (95th %ile)", f"${np.percentile(final_values, 95):,.2f}")
                with col3:
                    st.metric("Worst Case (5th %ile)", f"${np.percentile(final_values, 5):,.2f}")
                with col4:
                    expected_return = (np.median(final_values) / current_value - 1) * 100
                    st.metric("Expected Return", f"{expected_return:.2f}%")
        
        elif view_mode == "Compare Scenarios":
            # Compare different volatility scenarios
            st.subheader("Compare Different Market Conditions")
            
            scenarios_to_compare = {}
            
            # Check cache for scenario simulations
            positions_tuple = tuple((p.ticker, p.value, p.return_pct, p.current_price) for p in positions)
            dca_tuple = tuple(sorted(dca_schedule.items())) if dca_schedule else None
            conservative_hash = hash((positions_tuple, 0.10, days, n_sims, random_seed, dca_tuple))
            aggressive_hash = hash((positions_tuple, 0.25, days, n_sims, random_seed, dca_tuple))
            conservative_cache_key = f"conservative_sim_{conservative_hash}"
            aggressive_cache_key = f"aggressive_sim_{aggressive_hash}"
            
            with st.status("🔄 Loading scenario comparisons...", expanded=False) as status:
                if conservative_cache_key in st.session_state:
                    status.update(label="✅ Using cached conservative scenario", state="complete")
                    conservative_paths, _ = st.session_state[conservative_cache_key]
                else:
                    status.update(label="🔄 Running conservative scenario...", state="running")
                    conservative_paths, _ = simulate_portfolio_growth_over_time(
                        positions, sigma=0.10, days=days, n_sims=n_sims, random_seed=random_seed, dca_schedule=dca_schedule
                    )
                    st.session_state[conservative_cache_key] = (conservative_paths, time_points)
                
                scenarios_to_compare["Conservative (Low Volatility)"] = (
                    conservative_paths, time_points, current_value, "#2ecc71"
                )
                
                # Moderate scenario (current) - already computed
                scenarios_to_compare["Moderate (Current)"] = (
                    base_portfolio_paths, time_points, current_value, "#3498db"
                )
                
                if aggressive_cache_key in st.session_state:
                    status.update(label="✅ Using cached aggressive scenario", state="complete")
                    aggressive_paths, _ = st.session_state[aggressive_cache_key]
                else:
                    status.update(label="🔄 Running aggressive scenario...", state="running")
                    aggressive_paths, _ = simulate_portfolio_growth_over_time(
                        positions, sigma=0.25, days=days, n_sims=n_sims, random_seed=random_seed, dca_schedule=dca_schedule
                    )
                    st.session_state[aggressive_cache_key] = (aggressive_paths, time_points)
                
                scenarios_to_compare["Aggressive (High Volatility)"] = (
                    aggressive_paths, time_points, current_value, "#e74c3c"
                )
                status.update(label="✅ All scenarios ready!", state="complete")
            
            # Create comparison chart
            comparison_chart = create_multi_scenario_growth_chart(
                scenarios_to_compare,
                current_value,
            )
            st.plotly_chart(comparison_chart, width='stretch', key='scenario_comparison_chart')
            
            if show_explanation:
                st.subheader("📊 Scenario Comparison")
                scenario_stats = []
                for name, (paths, _, _, _) in scenarios_to_compare.items():
                    final_vals = paths[:, -1]
                    scenario_stats.append({
                        "Scenario": name,
                        "Expected Value": f"${np.median(final_vals):,.2f}",
                        "Best Case": f"${np.percentile(final_vals, 95):,.2f}",
                        "Worst Case": f"${np.percentile(final_vals, 5):,.2f}",
                        "Expected Return": f"{((np.median(final_vals) / current_value - 1) * 100):.2f}%",
                    })
                st.dataframe(pd.DataFrame(scenario_stats), width='stretch', hide_index=True)
        
        else:  # What-If Analysis
            st.subheader("What-If Portfolio Analysis")
            st.markdown("**Modify your portfolio and see how changes would impact growth projections.**")
            
            # Initialize what-if portfolio state
            if 'whatif_positions' not in st.session_state:
                # Start with a copy of current positions
                st.session_state.whatif_positions = [
                    {
                        'ticker': pos.ticker,
                        'shares': pos.shares,
                        'value': pos.value,
                        'return_pct': pos.return_pct,
                        'current_price': pos.current_price,
                    }
                    for pos in positions
                ]
            
            if 'whatif_dca_schedule' not in st.session_state:
                st.session_state.whatif_dca_schedule = dca_schedule.copy() if dca_schedule else {}
            
            # Ultra-compact layout: Portfolio table + Add positions side-by-side
            col_left, col_right = st.columns([1.5, 1])
            
            with col_left:
                st.subheader("📊 Portfolio")
                
                if not st.session_state.whatif_positions:
                    st.info("No positions")
                else:
                    # Ultra-compact single-row format with all controls inline
                    # Header row
                    header_cols = st.columns([1.2, 0.9, 0.9, 0.9, 0.7, 0.7, 0.7, 0.5, 0.3])
                    with header_cols[0]: st.caption("**Ticker**")
                    with header_cols[1]: st.caption("**Shares**")
                    with header_cols[2]: st.caption("**Return%**")
                    with header_cols[3]: st.caption("**Value**")
                    with header_cols[4]: st.caption("**DCA$**")
                    with header_cols[5]: st.caption("**Freq**")
                    with header_cols[6]: st.caption("**DCA**")
                    with header_cols[7]: st.caption("**Edit**")
                    with header_cols[8]: st.caption("**Del**")
                    
                    # Position rows - ultra compact
                    for idx, pos_dict in enumerate(st.session_state.whatif_positions):
                        ticker = pos_dict['ticker']
                        has_dca = ticker in st.session_state.whatif_dca_schedule
                        
                        row_cols = st.columns([1.2, 0.9, 0.9, 0.9, 0.7, 0.7, 0.7, 0.5, 0.3])
                        
                        with row_cols[0]:
                            st.text(ticker)
                            if has_dca:
                                st.caption("💰")
                        
                        with row_cols[1]:
                            new_shares = st.number_input(
                                "",
                                min_value=0.0,
                                value=float(pos_dict['shares']),
                                step=0.1,
                                format="%.2f",
                                label_visibility="collapsed",
                                key=f"edit_shares_{idx}"
                            )
                        
                        with row_cols[2]:
                            new_return = st.number_input(
                                "",
                                value=float(pos_dict['return_pct']),
                                step=0.1,
                                format="%.1f",
                                label_visibility="collapsed",
                                key=f"edit_return_{idx}"
                            )
                        
                        with row_cols[3]:
                            st.caption(f"${pos_dict['value']:,.0f}")
                        
                        with row_cols[4]:
                            dca_amount = st.number_input(
                                "",
                                min_value=0.0,
                                value=st.session_state.whatif_dca_schedule[ticker]['amount'] if has_dca else 0.0,
                                step=10.0,
                                format="%.0f",
                                key=f"whatif_dca_amount_{idx}_{ticker}",
                                label_visibility="collapsed"
                            )
                        
                        with row_cols[5]:
                            dca_freq = st.selectbox(
                                "",
                                options=[7, 14, 21, 30, 60, 90],
                                format_func=lambda x: f"{x}d",
                                index=[7, 14, 21, 30, 60, 90].index(st.session_state.whatif_dca_schedule[ticker]['frequency_days']) if has_dca else 3,
                                key=f"whatif_dca_freq_{idx}_{ticker}",
                                label_visibility="collapsed"
                            )
                        
                        with row_cols[6]:
                            if has_dca:
                                if st.button("❌", key=f"whatif_remove_dca_{idx}_{ticker}", help="Remove DCA"):
                                    del st.session_state.whatif_dca_schedule[ticker]
                                    st.rerun()
                            else:
                                if st.button("✅", key=f"whatif_add_dca_{idx}_{ticker}", help="Add DCA"):
                                    if dca_amount > 0:
                                        st.session_state.whatif_dca_schedule[ticker] = {
                                            'amount': dca_amount,
                                            'frequency_days': dca_freq
                                        }
                                        st.rerun()
                        
                        with row_cols[7]:
                            if st.button("✏️", key=f"update_pos_{idx}", help="Update"):
                                st.session_state.whatif_positions[idx]['shares'] = new_shares
                                st.session_state.whatif_positions[idx]['value'] = new_shares * pos_dict['current_price']
                                st.session_state.whatif_positions[idx]['return_pct'] = new_return
                                st.rerun()
                        
                        with row_cols[8]:
                            if st.button("🗑️", key=f"remove_pos_{idx}", help="Delete"):
                                st.session_state.whatif_positions.pop(idx)
                                if ticker in st.session_state.whatif_dca_schedule:
                                    del st.session_state.whatif_dca_schedule[ticker]
                                st.rerun()
            
            with col_right:
                st.subheader("➕ Add Positions")
                
                if st.session_state.watchlist:
                    # Load watchlist data (non-blocking)
                    cache_key = f"watchlist_cache_{use_realtime}_{hash(tuple(w.get('ticker', '') for w in st.session_state.watchlist))}"
                    
                    if cache_key not in st.session_state:
                        with st.spinner("📡 Loading..."):
                            st.session_state[cache_key] = update_watchlist_with_realtime(
                                st.session_state.watchlist, use_realtime
                            )
                    
                    updated_watchlist = st.session_state[cache_key]
                    
                    # Compact watchlist selector
                    selected_tickers = st.multiselect(
                        "Select tickers",
                        options=[w["ticker"] for w in updated_watchlist],
                        help="Choose tickers to add",
                        key="whatif_ticker_select"
                    )
                    
                    # Compact watchlist preview (collapsible)
                    if st.checkbox("📋 Show watchlist", value=False, key="show_watchlist"):
                        watchlist_display = []
                        for w in updated_watchlist[:20]:  # Limit to 20 for performance
                            watchlist_display.append({
                                "Ticker": w["ticker"],
                                "Price": f"${w['current_price']:.2f}",
                                "Return": f"{w['today_return_pct']:.2f}%",
                            })
                        st.dataframe(pd.DataFrame(watchlist_display), width='stretch', hide_index=True, height=200)
                    
                    if selected_tickers:
                        # Ultra-compact configuration
                        config_col1, config_col2, config_col3 = st.columns(3)
                        
                        with config_col1:
                            allocation_method = st.radio(
                                "Add by:",
                                ["$ Amount", "Shares"],
                                horizontal=True,
                                key="whatif_allocation_method"
                            )
                            if allocation_method == "$ Amount":
                                allocation_value = st.number_input(
                                    "$ per ticker",
                                    min_value=0.0,
                                    value=1000.0,
                                    step=100.0,
                                    key="whatif_allocation_value"
                                )
                            else:
                                shares_per_ticker = st.number_input(
                                    "Shares",
                                    min_value=0.0,
                                    value=10.0,
                                    step=0.1,
                                    format="%.2f",
                                    key="whatif_shares_value"
                                )
                        
                        with config_col2:
                            use_watchlist_return = st.checkbox(
                                "Use watchlist return",
                                value=True,
                                key="whatif_use_return"
                            )
                            if not use_watchlist_return:
                                manual_return = st.number_input(
                                    "Return %",
                                    value=10.0,
                                    step=0.1,
                                    key="whatif_manual_return"
                                )
                        
                        with config_col3:
                            enable_dca_new = st.checkbox(
                                "Add DCA",
                                value=False,
                                key="whatif_enable_dca_new"
                            )
                            dca_amount_new = 0.0
                            dca_freq_new = 30
                            if enable_dca_new:
                                dca_amount_new = st.number_input(
                                    "DCA $",
                                    min_value=0.0,
                                    value=100.0,
                                    step=10.0,
                                    key="whatif_dca_amount_new"
                                )
                                dca_freq_new = st.selectbox(
                                    "Freq",
                                    options=[7, 14, 21, 30, 60, 90],
                                    format_func=lambda x: f"{x}d",
                                    index=3,
                                    key="whatif_dca_freq_new"
                                )
                        
                        if st.button("➕ Add Positions", type="primary", use_container_width=True):
                            watch_map = {w["ticker"]: WatchItem(
                                ticker=w["ticker"],
                                name=w.get("name", w["ticker"]),
                                current_price=w["current_price"],
                                today_return_pct=w["today_return_pct"],
                                total_return_pct=w.get("total_return_pct", w.get("ytd_return_pct", 0.0)),
                            ) for w in updated_watchlist}
                            
                            for ticker in selected_tickers:
                                watch_item = watch_map.get(ticker)
                                if not watch_item:
                                    continue
                                
                                # Determine return
                                if use_watchlist_return and watch_item.total_return_pct is not None:
                                    return_pct = watch_item.total_return_pct
                                elif use_watchlist_return:
                                    return_pct = watch_item.today_return_pct * 252
                                else:
                                    return_pct = manual_return
                                
                                # Calculate shares and value
                                if allocation_method == "Dollar Amount ($)":
                                    position_value = allocation_value
                                    shares = position_value / watch_item.current_price if watch_item.current_price > 0 else 0
                                else:
                                    shares = shares_per_ticker
                                    position_value = shares * watch_item.current_price
                                
                                # Check if exists
                                existing_idx = next((i for i, p in enumerate(st.session_state.whatif_positions) if p['ticker'] == ticker), None)
                                if existing_idx is not None:
                                    # Add to existing
                                    st.session_state.whatif_positions[existing_idx]['shares'] += shares
                                    st.session_state.whatif_positions[existing_idx]['value'] += position_value
                                else:
                                    # Add new
                                    st.session_state.whatif_positions.append({
                                        'ticker': ticker,
                                        'shares': shares,
                                        'value': position_value,
                                        'return_pct': return_pct,
                                        'current_price': watch_item.current_price,
                                    })
                                
                                # Add DCA if enabled
                                if enable_dca_new and dca_amount_new > 0:
                                    st.session_state.whatif_dca_schedule[ticker] = {
                                        'amount': dca_amount_new,
                                        'frequency_days': dca_freq_new
                                    }
                            
                            st.rerun()
                else:
                    st.info("No watchlist available")
            
            # Convert what-if positions to Position objects for simulation
            whatif_position_objects = []
            for pos_dict in st.session_state.whatif_positions:
                whatif_position_objects.append(Position(
                    ticker=pos_dict['ticker'],
                    value=pos_dict['value'],
                    return_pct=pos_dict['return_pct'],
                    current_price=pos_dict['current_price'],
                ))
            
            if whatif_position_objects:
                whatif_total_value = sum(pos.value for pos in whatif_position_objects)
                
                # Compact summary row
                summary_col1, summary_col2, summary_col3 = st.columns(3)
                with summary_col1:
                    st.metric("Original", f"${current_value:,.0f}")
                with summary_col2:
                    st.metric("What-If", f"${whatif_total_value:,.0f}", 
                             delta=f"{whatif_total_value - current_value:,.0f}")
                with summary_col3:
                    diff_pct = ((whatif_total_value - current_value) / current_value * 100) if current_value > 0 else 0
                    st.metric("Change", f"{diff_pct:+.1f}%")
                
                # Run simulation
                st.markdown("---")
                st.subheader("📈 Growth Comparison")
                
                # Check cache
                whatif_positions_tuple = tuple((p.ticker, p.value, p.return_pct, p.current_price) for p in whatif_position_objects)
                # Convert DCA schedule to hashable format (tuple of tuples)
                if st.session_state.whatif_dca_schedule:
                    whatif_dca_tuple = tuple(sorted(
                        (ticker, config['amount'], config['frequency_days'])
                        for ticker, config in st.session_state.whatif_dca_schedule.items()
                    ))
                else:
                    whatif_dca_tuple = None
                whatif_hash = hash((whatif_positions_tuple, sigma, days, n_sims, random_seed, whatif_dca_tuple))
                whatif_cache_key = f"whatif_sim_{whatif_hash}"
                
                if whatif_cache_key in st.session_state:
                    whatif_paths, whatif_time_points = st.session_state[whatif_cache_key]
                else:
                    with st.status("🔄 Running simulation...", expanded=True) as status:
                        status.update(label="🔄 Simulating portfolio growth...", state="running")
                        whatif_paths, whatif_time_points = simulate_portfolio_growth_over_time(
                            whatif_position_objects,
                            sigma=sigma,
                            days=days,
                            n_sims=n_sims,
                            random_seed=random_seed,
                            dca_schedule=st.session_state.whatif_dca_schedule,
                        )
                        st.session_state[whatif_cache_key] = (whatif_paths, whatif_time_points)
                        status.update(label="✅ Simulation complete!", state="complete")
                
                # Compare scenarios
                comparison_scenarios = {
                    "Original Portfolio": (
                        base_portfolio_paths, time_points, current_value, "#3498db"
                    ),
                    "What-If Portfolio": (
                        whatif_paths, whatif_time_points, whatif_total_value, "#e74c3c"
                    ),
                }
                
                whatif_chart = create_multi_scenario_growth_chart(
                    comparison_scenarios,
                    current_value,
                )
                st.plotly_chart(whatif_chart, width='stretch', key='whatif_chart', height=500)
                
                if show_explanation:
                    # Compact comparison metrics
                    original_final = base_portfolio_paths[:, -1]
                    whatif_final = whatif_paths[:, -1]
                    
                    comp_col1, comp_col2, comp_col3, comp_col4 = st.columns(4)
                    with comp_col1:
                        st.metric("Original Final", f"${np.median(original_final):,.0f}")
                        st.caption(f"Return: {((np.median(original_final) / current_value - 1) * 100):.1f}%")
                    with comp_col2:
                        st.metric("What-If Final", f"${np.median(whatif_final):,.0f}")
                        st.caption(f"Return: {((np.median(whatif_final) / whatif_total_value - 1) * 100):.1f}%")
                    with comp_col3:
                        diff = np.median(whatif_final) - np.median(original_final)
                        diff_pct = ((diff / np.median(original_final)) * 100) if np.median(original_final) > 0 else 0
                        st.metric("Difference", f"${diff:,.0f}", delta=f"{diff_pct:.1f}%")
                    with comp_col4:
                        st.metric("Best Case", f"${np.percentile(whatif_final, 95):,.0f}")
                        st.caption(f"Worst: ${np.percentile(whatif_final, 5):,.0f}")
            else:
                st.info("Add positions to see projections")
    
    with tab2:
        st.header("Compare Different Prediction Scenarios")
        
        # Create different scenarios
        scenarios = {
            "Conservative (σ=0.10)": analyse_portfolio(
                positions, sigma=0.10, days=days, n_sims=n_sims, random_seed=random_seed
            ),
            "Moderate (σ=0.15)": analyse_portfolio(
                positions, sigma=0.15, days=days, n_sims=n_sims, random_seed=random_seed
            ),
            "Aggressive (σ=0.25)": analyse_portfolio(
                positions, sigma=0.25, days=days, n_sims=n_sims, random_seed=random_seed
            ),
        }
        
        # Add time horizon scenarios
        scenarios["Short-term (90 days)"] = analyse_portfolio(
            positions, sigma=sigma, days=90, n_sims=n_sims, random_seed=random_seed
        )
        scenarios["Long-term (500 days)"] = analyse_portfolio(
            positions, sigma=sigma, days=500, n_sims=n_sims, random_seed=random_seed
        )
        
        # Display comparison chart
        comparison_chart = create_prediction_comparison_chart(scenarios, current_value)
        st.plotly_chart(comparison_chart, width='stretch', key='comparison_chart')
        
        # Scenario details table
        st.subheader("Scenario Details")
        scenario_data = []
        for name, data in scenarios.items():
            scenario_data.append({
                "Scenario": name,
                "Predicted Value": f"${data['predicted_portfolio_value']:,.2f}",
                "Predicted Return %": f"{data['predicted_portfolio_return_pct']:.2f}%",
                "Expected Gain": f"${data['predicted_portfolio_value'] - current_value:,.2f}",
            })
        st.dataframe(pd.DataFrame(scenario_data), width='stretch')
    
    with tab3:
        st.header("Portfolio Position Details")
        
        # Create positions dataframe (with loading indicator for many positions)
        pos_data = []
        if len(positions) > 10:
            progress_bar = st.progress(0)
            status_text = st.empty()
        
        for idx, pos in enumerate(positions):
            if len(positions) > 10:
                status_text.text(f"Analyzing {pos.ticker}... ({idx+1}/{len(positions)})")
                progress_bar.progress((idx + 1) / len(positions))
            
            pred = predict_position(pos, sigma=sigma, days=days, n_sims=n_sims, random_seed=random_seed)
            pos_data.append({
                "Ticker": pos.ticker,
                "Current Value": f"${pos.value:,.2f}",
                "Shares": f"{pos.shares:.2f}",
                "Current Price": f"${pos.current_price:.2f}",
                "Historical Return %": f"{pos.return_pct:.2f}%",
                "Predicted Price": f"${pred['expected_price']:.2f}",
                "Predicted Return %": f"{pred['expected_return_pct']:.2f}%",
                "Weight %": f"{(pos.value / current_value * 100):.2f}%",
            })
        
        if len(positions) > 10:
            progress_bar.empty()
            status_text.empty()
        
        df = pd.DataFrame(pos_data)
        st.dataframe(df, width='stretch')
        
        # Portfolio allocation pie chart
        st.subheader("Portfolio Allocation")
        allocation_fig = px.pie(
            values=[pos.value for pos in positions],
            names=[pos.ticker for pos in positions],
            title="Portfolio Value Allocation by Position"
        )
        st.plotly_chart(allocation_fig, width='stretch', key='allocation_chart')
    
    with tab4:
        st.header("Individual Position Predictions")
        
        # Create charts for each position
        for idx, pos in enumerate(positions):
            with st.expander(f"📊 {pos.ticker} - {pos.shares:.2f} shares @ ${pos.current_price:.2f} (Value: ${pos.value:,.2f})"):
                # Simulate individual position
                mu = pos.return_pct / 100.0
                prices_end = simulate_gbm_price(
                    current_price=pos.current_price,
                    mu=mu,
                    sigma=sigma,
                    days=days,
                    n_sims=n_sims,
                    random_seed=random_seed,
                )
                
                # Create histogram of predicted prices
                fig = go.Figure()
                fig.add_trace(go.Histogram(
                    x=prices_end,
                    nbinsx=50,
                    name='Price Distribution',
                    marker_color='lightblue',
                ))
                
                # Add current price line
                fig.add_vline(
                    x=pos.current_price,
                    line_dash="dash",
                    line_color="red",
                    annotation_text=f"Current: ${pos.current_price:.2f}",
                )
                
                # Add expected price line
                expected_price = float(prices_end.mean())
                fig.add_vline(
                    x=expected_price,
                    line_dash="dash",
                    line_color="green",
                    annotation_text=f"Expected: ${expected_price:.2f}",
                )
                
                fig.update_layout(
                    title=f'{pos.ticker} Price Distribution After {days} Days',
                    xaxis_title='Price ($)',
                    yaxis_title='Frequency',
                    height=400,
                    template='plotly_white',
                )
                
                st.plotly_chart(fig, width='stretch', key=f'position_chart_{idx}_{pos.ticker}')
                
                # Statistics
                col1, col2, col3, col4 = st.columns(4)
                with col1:
                    st.metric("Expected Price", f"${expected_price:.2f}")
                with col2:
                    st.metric("Expected Return", f"{((expected_price / pos.current_price - 1) * 100):.2f}%")
                with col3:
                    st.metric("5th Percentile", f"${np.percentile(prices_end, 5):.2f}")
                with col4:
                    st.metric("95th Percentile", f"${np.percentile(prices_end, 95):.2f}")
    
    with tab5:
        st.header("Watchlist Analysis")
        
        if not st.session_state.watchlist:
            st.info("No items in watchlist. Add items to portfolio_data.json to see them here.")
        else:
            # Cache watchlist updates in session state to avoid refetching
            cache_key = f"watchlist_tab5_cache_{use_realtime}_{hash(tuple(w.get('ticker', '') for w in st.session_state.watchlist))}"
            
            if cache_key not in st.session_state:
                st.session_state[cache_key] = update_watchlist_with_realtime(
                    st.session_state.watchlist, use_realtime
                )
            
            updated_watchlist = st.session_state[cache_key]
            
            # Convert to WatchItem objects
            watchlist_items = []
            for item in updated_watchlist:
                watchlist_items.append(WatchItem(
                    ticker=item["ticker"],
                    name=item.get("name", item["ticker"]),
                    current_price=item["current_price"],
                    today_return_pct=item["today_return_pct"],
                    total_return_pct=item.get("total_return_pct"),
                ))
            
            # Analyze watchlist
            watch_results = analyse_watchlist(
                watchlist_items,
                sigma=sigma,
                days=days,
                n_sims=n_sims,
                random_seed=random_seed,
            )
            
            # Display watchlist table
            st.subheader("Watchlist Items")
            watchlist_data = []
            for ticker, exp_ret, exp_price in watch_results:
                item = next((w for w in updated_watchlist if w["ticker"] == ticker), None)
                if item:
                    watchlist_data.append({
                        "Ticker": ticker,
                        "Name": item.get("name", ticker),
                        "Current Price": f"${item['current_price']:.2f}",
                        "Today Return %": f"{item['today_return_pct']:.2f}%",
                        "Predicted Return %": f"{exp_ret:.2f}%",
                        "Predicted Price": f"${exp_price:.2f}",
                    })
            
            df_watchlist = pd.DataFrame(watchlist_data)
            st.dataframe(df_watchlist, width='stretch', hide_index=True)
            
            # Add to portfolio section
            st.subheader("Add to Portfolio")
            selected_tickers = st.multiselect(
                "Select tickers to add to portfolio",
                options=[w["ticker"] for w in updated_watchlist],
                help="Select one or more tickers from your watchlist to add to your portfolio"
            )
            
            if selected_tickers:
                allocation_value = st.number_input(
                    "Allocation per ticker ($)",
                    min_value=0.0,
                    value=1000.0,
                    step=100.0,
                    help="Amount to invest in each selected ticker"
                )
                
                if st.button("➕ Add Selected to Portfolio"):
                    watch_map = {w["ticker"]: WatchItem(
                        ticker=w["ticker"],
                        name=w.get("name", w["ticker"]),
                        current_price=w["current_price"],
                        today_return_pct=w["today_return_pct"],
                        total_return_pct=w.get("total_return_pct"),
                    ) for w in updated_watchlist}
                    
                    current_positions = [Position(**p) for p in updated_positions]
                    new_positions = add_positions(
                        current_positions,
                        watch_map,
                        selected_tickers,
                        allocation_value,
                    )
                    
                    # Convert back to dict format and update session state
                    st.session_state.positions = [
                        {
                            "ticker": pos.ticker,
                            "value": pos.value,
                            "return_pct": pos.return_pct,
                            "current_price": pos.current_price,
                        }
                        for pos in new_positions
                    ]
                    st.success(f"Added {len(selected_tickers)} position(s) to portfolio!")
                    st.rerun()
            
            # Watchlist predictions chart
            st.subheader("Watchlist Predictions Comparison")
            if watch_results:
                tickers = [r[0] for r in watch_results]
                exp_returns = [r[1] for r in watch_results]
                exp_prices = [r[2] for r in watch_results]
                
                fig = make_subplots(
                    rows=1, cols=2,
                    subplot_titles=('Predicted Returns (%)', 'Predicted Prices ($)'),
                    specs=[[{"type": "bar"}, {"type": "bar"}]]
                )
                
                # Returns chart
                colors = ['green' if r > 0 else 'red' for r in exp_returns]
                fig.add_trace(
                    go.Bar(
                        x=tickers[:20],  # Limit to top 20 for readability
                        y=exp_returns[:20],
                        name='Predicted Return %',
                        marker_color=colors[:20],
                        text=[f'{r:.2f}%' for r in exp_returns[:20]],
                        textposition='outside',
                    ),
                    row=1, col=1
                )
                
                # Prices chart
                fig.add_trace(
                    go.Bar(
                        x=tickers[:20],
                        y=exp_prices[:20],
                        name='Predicted Price',
                        marker_color='lightblue',
                        text=[f'${p:.2f}' for p in exp_prices[:20]],
                        textposition='outside',
                    ),
                    row=1, col=2
                )
                
                fig.update_layout(
                    title='Top 20 Watchlist Predictions',
                    height=500,
                    showlegend=False,
                    template='plotly_white',
                )
                
                fig.update_xaxes(title_text="Ticker", row=1, col=1)
                fig.update_xaxes(title_text="Ticker", row=1, col=2)
                fig.update_yaxes(title_text="Return (%)", row=1, col=1)
                fig.update_yaxes(title_text="Price ($)", row=1, col=2)
                
                st.plotly_chart(fig, width='stretch', key='watchlist_chart')


if __name__ == "__main__":
    main()

