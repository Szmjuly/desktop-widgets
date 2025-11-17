# Portfolio Analysis Dashboard

An interactive web-based dashboard for analyzing stock portfolios with predictive modeling using Geometric Brownian Motion (GBM).

## Features

- **Interactive Portfolio Management**: Add, edit, and delete portfolio positions
- **Real-time Predictions**: See predicted portfolio values and returns based on historical performance
- **Growth Visualization**: Interactive charts showing portfolio growth over time with confidence intervals
- **Multiple Scenarios**: Compare different volatility and time horizon scenarios
- **Individual Position Analysis**: Detailed predictions for each position in your portfolio
- **Parameter Controls**: Adjust volatility, time horizon, and simulation parameters in real-time

## Installation

1. Install dependencies:
```bash
pip install -r requirements.txt
```

## Running the Dashboard

### Option 1: Using the run script
```bash
# On Windows PowerShell
.\run_app.ps1

# Or on Linux/Mac
python run_app.py
```

### Option 2: Direct Streamlit command
```bash
streamlit run app.py
```

The dashboard will open in your default web browser at `http://localhost:8501`

## Usage

1. **Configure Parameters**: Use the sidebar to adjust:
   - Volatility (σ): Annualized volatility of returns
   - Time Horizon: Number of trading days to simulate
   - Number of Simulations: More simulations = more accurate but slower

2. **Manage Portfolio**: 
   - Add new positions using the "Add New Position" form
   - Edit existing positions using the "Edit Positions" section
   - Delete positions you no longer want to track

3. **View Predictions**:
   - **Growth Over Time**: See how your portfolio might grow with confidence bands
   - **Prediction Scenarios**: Compare conservative, moderate, and aggressive scenarios
   - **Position Details**: View detailed breakdown of each position
   - **Individual Predictions**: See price distribution predictions for each stock

## How It Works

The dashboard uses Geometric Brownian Motion (GBM) to simulate future stock prices. GBM models stock prices as:

- **Drift (μ)**: Based on historical return percentage
- **Volatility (σ)**: Standard deviation of returns (adjustable)
- **Monte Carlo Simulation**: Runs multiple simulation paths to generate predictions

## Disclaimer

This tool is for educational purposes only. The simulations are simplistic and do not account for factors such as changing interest rates, dividends, economic cycles, or company-specific events. Past returns do not guarantee future performance. Always do your own research before making investment decisions.

