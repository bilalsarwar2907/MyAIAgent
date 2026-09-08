# MyAIAgent

**Status: work in progress — a personal "vibe coding" test case.**

I built this project to test how far AI-assisted ("vibe") coding — working with Claude as a pair-programmer — could take a real C#/.NET application, not as a finished product. It's still under active development, and I'm currently waiting on live paper-trading results before drawing any conclusions about whether the trading logic actually works.

## What it is

A full-stack AI-assisted stock research and paper-trading platform: C# .NET 8 backend, Vue 3 + TypeScript frontend (`frontend/chat-ui`).

## What's built so far

- AI chat agent for natural-language queries (`AIService`, `ChatIntentRouter`)
- Stock screener using technical indicators, including RSI (`ScreenerService`, `TechnicalIndicators`)
- Backtest engine to test strategies against historical data (`BacktestEngine`, `BacktestTool`)
- Paper-trading portfolio tracking — simulated only, no real money (`PaperPortfolioService`)
- Sector/factor and volatility research services (`ResearchService`, `VolatilityFactorService`)
- Alpaca API integration for market and historical data (`alpaca_trader.py`)
- Password hashing with BCrypt

## What's not done yet

- Paper-trading results are still coming in — too early to say whether the RSI-based strategy is actually good
- Not production-hardened; this is a learning project, not a deployed product
- No automated test suite yet

## Tech stack

.NET 8, C#, EF Core, Vue 3, TypeScript, Alpaca API

## Setup

```
dotnet restore
dotnet run
```

Frontend:

```
cd frontend/chat-ui
npm install
npm run dev
```
