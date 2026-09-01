"""
Alpaca Paper Trading Script
----------------------------
Uses the Alpaca API to manage paper trades.
Integrates with TradingAgents output to place trades automatically.

KNOWN ISSUES FIXED:
  - Orders now use GTC (Good Till Cancelled) so they don't expire after hours
  - buy_limit() no longer auto-runs on startup — call it explicitly
  - cancel_pending(symbol) cancels only that symbol's orders (safe with multiple trades)
  - cancel_all_pending() still available as nuclear option (wipes everything)
  - exit_trade() added to close position + cancel any open orders for that symbol
  - status() added as a safe daily monitoring function (no side effects)

SESSION 13 ADDITIONS:
  - record_open_in_app()         — call after any buy fills to record in Paper Portfolio DB
  - get_open_trade_id()          — looks up the DB trade ID for a symbol (needed to close)
  - record_close_in_app()        — call after any sell to close the record in Paper Portfolio DB
  - backfill_existing_trades()   — ONE-TIME: populates all 3 historical trades into app.db
    (Closed IBM Jul16→Jul17, Open IBM Jul21, Open INTC Jul21)
    Run once to fix the gap where Alpaca and the local DB were not connected.

WORKFLOW GOING FORWARD (every new trade):
  Step 1 — Place order in Alpaca:     buy_limit_gtc("SLB", 5, 42.00)
  Step 2 — After order fills, record: record_open_in_app("SLB", "Energy", 42.00, "2026-08-20", rsi_at_entry=28.4)
  Step 3 — When RSI > 60, exit:       sell_market("SLB", 5)
  Step 4 — Record the close:           record_close_in_app("SLB", exit_price=48.50, exit_date="2026-09-15", rsi_at_exit=62.1)
"""

import os

import requests as _requests

try:
    from dotenv import load_dotenv
    load_dotenv(os.path.join(os.path.dirname(__file__), ".env"))
except ImportError:
    # python-dotenv is optional — fall back to real environment variables.
    pass

from alpaca.trading.client import TradingClient
from alpaca.trading.requests import (
    MarketOrderRequest, LimitOrderRequest,
    GetOrdersRequest, CancelOrderResponse
)
from alpaca.trading.enums import OrderSide, TimeInForce, QueryOrderStatus

# ── ALPACA CONFIG ────────────────────────────────────────────────────────────
# Credentials are read from the environment (see .env / .env.example).
# Never hard-code keys in this file — it is tracked in git.
API_KEY    = os.environ.get("ALPACA_API_KEY", "")
SECRET_KEY = os.environ.get("ALPACA_SECRET_KEY", "")
PAPER      = os.environ.get("ALPACA_PAPER", "true").lower() != "false"  # default True

if not API_KEY or not SECRET_KEY:
    raise SystemExit(
        "Missing Alpaca credentials. Create a .env file next to alpaca_trader.py with:\n"
        "  ALPACA_API_KEY=your_key\n"
        "  ALPACA_SECRET_KEY=your_secret\n"
        "  ALPACA_PAPER=true\n"
        "(copy .env.example to .env and fill it in)"
    )
# ────────────────────────────────────────────────────────────────────────────

# ── APP CONFIG ───────────────────────────────────────────────────────────────
# To verify your username: open app in browser → F12 → Console → localStorage.getItem('userName')
BACKEND_URL = os.environ.get("BACKEND_URL", "http://localhost:60363")
APP_USER    = os.environ.get("APP_USER", "bilal")   # ← your login username
# ────────────────────────────────────────────────────────────────────────────

client = TradingClient(API_KEY, SECRET_KEY, paper=PAPER)


# ════════════════════════════════════════════════════════════════════════════
# ALPACA TRADE FUNCTIONS
# ════════════════════════════════════════════════════════════════════════════

def status():
    """Daily monitoring — safe to run anytime, no side effects."""
    account = client.get_account()
    print("\n── Account Balance ──────────────────────")
    print(f"  Portfolio Value : ${float(account.portfolio_value):,.2f}")
    print(f"  Cash            : ${float(account.cash):,.2f}")
    print(f"  Buying Power    : ${float(account.buying_power):,.2f}")
    print(f"  P&L Today       : ${float(account.equity) - float(account.last_equity):+,.2f}")
    print("─────────────────────────────────────────")

    positions = client.get_all_positions()
    if positions:
        print("\n── Open Positions ───────────────────────")
        for p in positions:
            print(f"  {p.symbol:6s}  qty={p.qty:>6s}  avg=${float(p.avg_entry_price):>8.2f}"
                  f"  now=${float(p.current_price):>8.2f}"
                  f"  P&L=${float(p.unrealized_pl):>+8.2f}")
        print("─────────────────────────────────────────")
    else:
        print("\n  No open positions.")

    orders = client.get_orders(GetOrdersRequest(status=QueryOrderStatus.OPEN))
    if orders:
        print("\n── Pending Orders ───────────────────────")
        for o in orders:
            print(f"  {o.symbol:6s}  {str(o.side).split('.')[1]:4s}"
                  f"  qty={o.qty}  limit=${o.limit_price}  [{o.time_in_force}]")
        print("─────────────────────────────────────────")
    else:
        print("  No pending orders.\n")


def cancel_pending(symbol: str):
    """Cancel pending orders for ONE symbol only. Safe to use when you have multiple trades open."""
    orders = client.get_orders(GetOrdersRequest(status=QueryOrderStatus.OPEN))
    cancelled = [o for o in orders if o.symbol == symbol.upper()]
    if not cancelled:
        print(f"\n  No pending orders for {symbol}.\n")
        return
    for o in cancelled:
        client.cancel_order_by_id(o.id)
    print(f"\n✅ Cancelled {len(cancelled)} pending order(s) for {symbol}.\n")


def cancel_all_pending():
    """Cancel ALL open orders across every symbol. Nuclear option — use with care."""
    orders = client.get_orders(GetOrdersRequest(status=QueryOrderStatus.OPEN))
    if not orders:
        print("\n  No pending orders to cancel.\n")
        return
    client.cancel_orders()
    print(f"\n✅ Cancelled {len(orders)} pending order(s).\n")


def buy_limit_gtc(symbol: str, qty: int, limit_price: float):
    """
    Place a GTC limit buy order.
    GTC = Good Till Cancelled — stays open until filled or manually cancelled.

    IMPORTANT: After this order fills, call record_open_in_app() to register it
    in the Paper Portfolio DB.
    """
    order = client.submit_order(LimitOrderRequest(
        symbol=symbol,
        qty=qty,
        side=OrderSide.BUY,
        time_in_force=TimeInForce.GTC,
        limit_price=limit_price
    ))
    print(f"\n✅ GTC Limit BUY submitted: {qty} x {symbol} @ ${limit_price}")
    print(f"   Order ID: {order.id}")
    print(f"   Will stay open until filled or manually cancelled.")
    print(f"\n⚠️  NEXT STEP: after this fills, run:")
    print(f"   record_open_in_app(\"{symbol}\", \"Sector\", avg_fill_price, \"YYYY-MM-DD\", rsi_at_entry=XX.X)\n")
    return order


def sell_market(symbol: str, qty: int):
    """
    Place a market sell order. Use when RSI > 60 exit rule is triggered.

    IMPORTANT: After calling this, call record_close_in_app() to close the
    record in the Paper Portfolio DB (backend auto-fetches B&H benchmark).
    """
    order = client.submit_order(MarketOrderRequest(
        symbol=symbol,
        qty=qty,
        side=OrderSide.SELL,
        time_in_force=TimeInForce.DAY
    ))
    print(f"\n✅ Market SELL submitted: {qty} x {symbol} | Order ID: {order.id}")
    print(f"\n⚠️  NEXT STEP: after fill confirmation, run:")
    print(f"   record_close_in_app(\"{symbol}\", exit_price=XX.XX, exit_date=\"YYYY-MM-DD\", rsi_at_exit=XX.X)\n")
    return order


def exit_trade(symbol: str):
    """
    Full exit: cancel any pending orders for this symbol, then close the position.
    Use when stop loss is hit OR when taking profit.

    IMPORTANT: After calling this, call record_close_in_app() to close the
    record in the Paper Portfolio DB (backend auto-fetches B&H benchmark).
    """
    open_orders = client.get_orders(GetOrdersRequest(status=QueryOrderStatus.OPEN))
    cancelled = 0
    for o in open_orders:
        if o.symbol == symbol:
            client.cancel_order_by_id(o.id)
            cancelled += 1
    if cancelled:
        print(f"\n  Cancelled {cancelled} pending order(s) for {symbol}.")

    try:
        client.close_position(symbol)
        print(f"✅ Position closed: {symbol}")
        print(f"\n⚠️  NEXT STEP: run:")
        print(f"   record_close_in_app(\"{symbol}\", exit_price=XX.XX, exit_date=\"YYYY-MM-DD\", rsi_at_exit=XX.X)\n")
    except Exception as e:
        print(f"  No open position for {symbol} (or already closed): {e}\n")


# ════════════════════════════════════════════════════════════════════════════
# PAPER PORTFOLIO RECORDING FUNCTIONS (Session 13)
# These call the MyAIAgent backend to keep the local DB in sync with Alpaca.
# ════════════════════════════════════════════════════════════════════════════

def record_open_in_app(symbol: str, sector: str, entry_price: float,
                        entry_date: str, rsi_at_entry: float,
                        target_exit_rsi: float = 60.0, notes: str = None):
    """
    Record a new open trade in the MyAIAgent Paper Portfolio (local SQLite DB).
    Call this after a buy order fills in Alpaca.

    Args:
        symbol          : Stock ticker, e.g. "IBM"
        sector          : Sector name, e.g. "Technology"
        entry_price     : Avg fill price from Alpaca
        entry_date      : Fill date as "YYYY-MM-DD"
        rsi_at_entry    : RSI value that triggered the trade (from daily_agent output)
        target_exit_rsi : Exit RSI threshold (default 60 per Frozen Rulebook)
        notes           : Optional one-sentence reason for the trade

    Returns:
        trade_id (int) if successful, None if failed.
    """
    payload = {
        "userName":       APP_USER,
        "symbol":         symbol.upper(),
        "sector":         sector,
        "entryPrice":     entry_price,
        "entryDate":      entry_date,
        "rsiAtEntry":     rsi_at_entry,
        "targetExitRsi":  target_exit_rsi,
        "notes":          notes
    }
    try:
        r = _requests.post(f"{BACKEND_URL}/api/paper/open", json=payload, timeout=5)
        if r.ok:
            data = r.json()
            trade_id = data.get("trade", {}).get("id", "?")
            print(f"  ✅ Recorded: {symbol.upper()} | DB id={trade_id} | "
                  f"Entry=${entry_price} | RSI={rsi_at_entry}")
            return trade_id
        else:
            print(f"  ⚠️  Record failed [{r.status_code}]: {r.text}")
    except _requests.exceptions.ConnectionError:
        print(f"  ⚠️  Cannot reach backend at {BACKEND_URL} — is the backend running? (dotnet run)")
    except Exception as e:
        print(f"  ⚠️  record_open_in_app error: {e}")
    return None


def get_open_trade_id(symbol: str):
    """
    Look up the Paper Portfolio DB trade ID for an open position by symbol.
    Used internally by record_close_in_app().

    Returns:
        trade_id (int) if found, None otherwise.
    """
    try:
        r = _requests.get(f"{BACKEND_URL}/api/paper/{APP_USER}", timeout=5)
        if r.ok:
            data = r.json()
            for t in data.get("openTrades", []):
                if t["symbol"].upper() == symbol.upper():
                    return t["id"]
            print(f"  ⚠️  No open trade found for {symbol} in Paper Portfolio DB.")
        else:
            print(f"  ⚠️  Could not fetch trades [{r.status_code}]: {r.text}")
    except _requests.exceptions.ConnectionError:
        print(f"  ⚠️  Cannot reach backend at {BACKEND_URL} — is the backend running? (dotnet run)")
    except Exception as e:
        print(f"  ⚠️  get_open_trade_id error: {e}")
    return None


def record_close_in_app(symbol: str, exit_price: float, exit_date: str,
                         rsi_at_exit: float, trade_id: int = None):
    """
    Close an open trade record in the MyAIAgent Paper Portfolio.
    The backend automatically fetches the B&H benchmark via Yahoo Finance.
    Call this after a sell order fills in Alpaca.

    Args:
        symbol       : Stock ticker, e.g. "IBM"
        exit_price   : Avg fill price from Alpaca
        exit_date    : Fill date as "YYYY-MM-DD"
        rsi_at_exit  : RSI value at the time of exit (from daily_agent output)
        trade_id     : (optional) pass directly to skip the lookup (used by backfill)
    """
    if trade_id is None:
        trade_id = get_open_trade_id(symbol)
    if trade_id is None:
        print(f"  ⚠️  Cannot close {symbol} in Paper Portfolio — no open trade found.")
        print(f"      Check APP_USER is set correctly (currently: '{APP_USER}')")
        return

    payload = {
        "tradeId":   trade_id,
        "userName":  APP_USER,
        "exitPrice": exit_price,
        "exitDate":  exit_date,
        "rsiAtExit": rsi_at_exit
    }
    try:
        r = _requests.post(f"{BACKEND_URL}/api/paper/close", json=payload, timeout=5)
        if r.ok:
            print(f"  ✅ Closed: {symbol.upper()} @ ${exit_price} | RSI at exit={rsi_at_exit}")
            print(f"     B&H benchmark calculated automatically by backend.")
        else:
            print(f"  ⚠️  Close failed [{r.status_code}]: {r.text}")
    except _requests.exceptions.ConnectionError:
        print(f"  ⚠️  Cannot reach backend at {BACKEND_URL} — is the backend running? (dotnet run)")
    except Exception as e:
        print(f"  ⚠️  record_close_in_app error: {e}")


def backfill_existing_trades():
    """
    ONE-TIME BACKFILL — Session 13.

    Populates all 3 historical trades into the Paper Portfolio local database:
      1. IBM (CLOSED)  — bought Jul 16 2026 @ $206.982, sold Jul 17 2026 @ $215.552
      2. IBM (OPEN)    — bought Jul 21 2026 @ $213.064
      3. INTC (OPEN)   — bought Jul 21 2026 @ $103.310

    RSI values at each entry/exit date are calculated from Yahoo Finance historical
    data using the same Wilder 14-period formula as daily_agent.py.

    DO NOT run this more than once — it will create duplicate records.
    If you accidentally ran it twice, delete the duplicates from the Paper tab
    (open trades only can be deleted via the tab's delete button).
    """
    import yfinance as yf
    import pandas as pd

    def calc_rsi_on_date(symbol, date_str, fetch_start="2026-05-01"):
        """Return Wilder RSI for a symbol on a specific date."""
        end = (pd.Timestamp(date_str) + pd.Timedelta(days=1)).strftime("%Y-%m-%d")
        df = yf.download(symbol, start=fetch_start, end=end,
                         auto_adjust=True, progress=False)
        if df.empty or len(df) < 15:
            print(f"  ⚠️  Not enough data for {symbol} RSI on {date_str}.")
            return None
        closes = df["Close"].values.flatten().astype(float)
        delta    = pd.Series(closes).diff()
        avg_gain = delta.clip(lower=0).ewm(alpha=1/14, min_periods=14, adjust=False).mean()
        avg_loss = (-delta).clip(lower=0).ewm(alpha=1/14, min_periods=14, adjust=False).mean()
        rsi = 100 - (100 / (1 + avg_gain / avg_loss))
        val = round(float(rsi.iloc[-1]), 1)
        actual_date = df.index[-1].strftime("%Y-%m-%d")
        print(f"  {symbol} RSI on {actual_date}: {val}")
        return val

    print("\n══════════════════════════════════════════════════════════════")
    print("  BACKFILL — Session 13 — calculating historical RSI values")
    print("══════════════════════════════════════════════════════════════")

    print("\n── Calculating RSI values ────────────────────────────────────")
    ibm_rsi_jul16  = calc_rsi_on_date("IBM",  "2026-07-16")
    ibm_rsi_jul17  = calc_rsi_on_date("IBM",  "2026-07-17")
    ibm_rsi_jul21  = calc_rsi_on_date("IBM",  "2026-07-21")
    intc_rsi_jul21 = calc_rsi_on_date("INTC", "2026-07-21")

    if None in (ibm_rsi_jul16, ibm_rsi_jul17, ibm_rsi_jul21, intc_rsi_jul21):
        print("\n❌ One or more RSI values could not be calculated.")
        print("   Check your internet connection and try again.")
        return

    # ── TRADE 1: IBM (CLOSED) Jul 16 → Jul 17 ─────────────────────────────
    print("\n── Trade 1: IBM (CLOSED) Jul 16 → Jul 17 ────────────────────")
    print("   Opening...")
    ibm_closed_id = record_open_in_app(
        symbol          = "IBM",
        sector          = "Technology",
        entry_price     = 206.982,
        entry_date      = "2026-07-16",
        rsi_at_entry    = ibm_rsi_jul16,
        target_exit_rsi = 60.0,
        notes           = "Track A — RSI < 30 entry. First IBM cycle, Jul 16 2026. "
                          "Avg fill $206.982 (3x$207.46 + 1x$206.53 + 1x$206.00)."
    )
    if ibm_closed_id:
        print("   Closing...")
        record_close_in_app(
            symbol       = "IBM",
            exit_price   = 215.552,
            exit_date    = "2026-07-17",
            rsi_at_exit  = ibm_rsi_jul17,
            trade_id     = ibm_closed_id   # pass directly — avoids lookup ambiguity
        )

    # ── TRADE 2: IBM (OPEN) Jul 21 ────────────────────────────────────────
    print("\n── Trade 2: IBM (OPEN) Jul 21 ───────────────────────────────")
    record_open_in_app(
        symbol          = "IBM",
        sector          = "Technology",
        entry_price     = 213.064,
        entry_date      = "2026-07-21",
        rsi_at_entry    = ibm_rsi_jul21,
        target_exit_rsi = 60.0,
        notes           = "Track A — RSI < 30 entry. Jul 2026 reset cycle. "
                          "Avg fill $213.064 (3x$212.39 + 1x$213.85 + 1x$214.30)."
    )

    # ── TRADE 3: INTC (OPEN) Jul 21 ───────────────────────────────────────
    print("\n── Trade 3: INTC (OPEN) Jul 21 ──────────────────────────────")
    record_open_in_app(
        symbol          = "INTC",
        sector          = "Technology",
        entry_price     = 103.31,
        entry_date      = "2026-07-21",
        rsi_at_entry    = intc_rsi_jul21,
        target_exit_rsi = 60.0,
        notes           = "Track A — RSI < 30 entry. Jul 2026 reset cycle. "
                          "Avg fill $103.31 (5x$103.31 via Alpaca fill)."
    )

    print("\n══════════════════════════════════════════════════════════════")
    print("  BACKFILL COMPLETE")
    print("  Go to the Paper Portfolio tab → click Refresh")
    print("  You should see: 1 closed trade (IBM) + 2 open positions (IBM, INTC)")
    print("══════════════════════════════════════════════════════════════\n")


# ── MAIN — runs status() only. Uncomment ONE action at a time when you mean to act. ──
if __name__ == "__main__":
    status()

    # ════════════════════════════════════════════════════════════════════════
    # ONE-TIME BACKFILL (run once to fix the Alpaca ↔ local DB gap)
    # ════════════════════════════════════════════════════════════════════════
    # backfill_existing_trades()

    # ════════════════════════════════════════════════════════════════════════
    # WORKFLOW: PLACE A NEW TRADE
    # ════════════════════════════════════════════════════════════════════════
    # Step 1 — Place the order in Alpaca (GTC limit):
    # cancel_pending("SYMBOL")
    # buy_limit_gtc("SYMBOL", qty, limit_price)
    #
    # Step 2 — AFTER the order fills, record it in Paper Portfolio:
    # record_open_in_app("SYMBOL", "Sector", avg_fill_price, "YYYY-MM-DD", rsi_at_entry=XX.X)

    # ════════════════════════════════════════════════════════════════════════
    # WORKFLOW: CLOSE A TRADE (RSI > 60 triggered)
    # ════════════════════════════════════════════════════════════════════════
    # Step 1 — Execute the exit in Alpaca:
    # sell_market("SYMBOL", qty)
    # # OR for full exit (cancels pending + closes position):
    # exit_trade("SYMBOL")
    #
    # Step 2 — Record the close in Paper Portfolio (backend auto-fetches B&H benchmark):
    # record_close_in_app("SYMBOL", exit_price=XX.XX, exit_date="YYYY-MM-DD", rsi_at_exit=XX.X)

    # ════════════════════════════════════════════════════════════════════════
    # CANCEL ORDERS
    # ════════════════════════════════════════════════════════════════════════
    # cancel_pending("SYMBOL")        # cancel duplicates for THIS symbol only
    # cancel_all_pending()            # nuclear option — wipes everything
