@echo off
chcp 65001 >nul
"C:\Users\biges\AppData\Local\Python\pythoncore-3.14-64\python.exe" "C:\Users\biges\Claude\Projects\MyAIAgent — Stock Trading Bot\alpaca_trader.py" > "C:\Users\biges\Claude\Projects\MyAIAgent — Stock Trading Bot\trading_output.txt" 2>&1
echo Done. Output saved to trading_output.txt
