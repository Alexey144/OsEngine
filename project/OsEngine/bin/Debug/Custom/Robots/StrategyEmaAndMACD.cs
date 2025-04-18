﻿using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Linq;
using OsEngine.Logging;
using System.Windows.Media.Animation;
using System.Security.Cryptography;

/* Description
trading robot for osengine

The trend robot on Strategy Ema And MACD.

Buy:
1. The price is above the Ema.
2. Macd is growing for at least 5 candles.
Sell:
1. The price is below the Ema.
2. Macd is falling for at least 5 candles.
Exit:
Exit from buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyEmaAndMACD")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyEmaAndMACD : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Indicator setting 
        private StrategyParameterInt _EmaPeriod;
        private StrategyParameterInt FastLineLengthMACD;
        private StrategyParameterInt SlowLineLengthMACD;
        private StrategyParameterInt SignalLineLengthMACD;

        // Indicator
        Aindicator _Ema;
        Aindicator _MACD;

        // The last value of the indicator
        private decimal _lastEma;
        private decimal _OnelastMACD;
        private decimal _TwolastMACD;
        private decimal _ThreelastMACD;
        private decimal _FourlastMACD;
        private decimal _FivelastMACD;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        public StrategyEmaAndMACD(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 1, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            StartTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            EndTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator setting
            _EmaPeriod = CreateParameter("Ema Length", 21, 7, 48, 7, "Indicator");
            FastLineLengthMACD = CreateParameter("MACD Fast Length", 16, 10, 300, 7, "Indicator");
            SlowLineLengthMACD = CreateParameter("MACD Slow Length", 32, 10, 300, 10, "Indicator");
            SignalLineLengthMACD = CreateParameter("MACD Signal Length", 8, 10, 300, 10, "Indicator");

            // Create indicator Ema
            _Ema = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA", false);
            _Ema = (Aindicator)_tab.CreateCandleIndicator(_Ema, "Prime");
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = _EmaPeriod.ValueInt;
            _Ema.Save();

            // Create indicator MACD
            _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", name + "MACD", false);
            _MACD = (Aindicator)_tab.CreateCandleIndicator(_MACD, "NewArea");
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = FastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = SlowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = SignalLineLengthMACD.ValueInt;
            _MACD.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyEmaAndMACD_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Ema And MACD. " +
                "Buy: " +
                "1. The price is above the Ema. " +
                "2. Macd is growing for at least 5 candles. " +
                "Sell: " +
                "1. The price is below the Ema. " +
                "2. Macd is falling for at least 5 candles. " +
                "Exit: " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        private void StrategyEmaAndMACD_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = _EmaPeriod.ValueInt;
            _Ema.Save();
            _Ema.Reload();
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = FastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = SlowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = SignalLineLengthMACD.ValueInt;
            _MACD.Save();
            _MACD.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyEmaAndMACD";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _EmaPeriod.ValueInt ||
                candles.Count < FastLineLengthMACD.ValueInt ||
                candles.Count < SlowLineLengthMACD.ValueInt + 6 ||
                candles.Count < SignalLineLengthMACD.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (StartTradeTime.Value > _tab.TimeServerCurrent ||
                EndTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            // If there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // If the position closing mode, then exit the method
            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }
            // If there are no positions, then go to the position opening method
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicator
            _lastEma = _Ema.DataSeries[0].Last;
            _OnelastMACD = _MACD.DataSeries[0].Last;
            _TwolastMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 2];
            _ThreelastMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 3];
            _FourlastMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 4];
            _FivelastMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 5];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastEma &&
                        _OnelastMACD > _TwolastMACD && 
                        _TwolastMACD > _ThreelastMACD &&
                        _ThreelastMACD > _FourlastMACD &&
                        _FourlastMACD > _FivelastMACD)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < _lastEma &&
                        _OnelastMACD < _TwolastMACD &&
                        _TwolastMACD < _ThreelastMACD &&
                        _ThreelastMACD < _FourlastMACD &&
                        _FourlastMACD < _FivelastMACD)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * TrailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume()
        {
            decimal volume = 0;

            if (VolumeRegime.ValueString == "Contract currency")
            {
                decimal contractPrice = _tab.PriceBestAsk;
                volume = VolumeOnPosition.ValueDecimal / contractPrice;
            }
            else if (VolumeRegime.ValueString == "Number of contracts")
            {
                volume = VolumeOnPosition.ValueDecimal;
            }

            // If the robot is running in the tester
            if (StartProgram == StartProgram.IsTester)
            {
                volume = Math.Round(volume, 6);
            }
            else
            {
                volume = Math.Round(volume, _tab.Securiti.DecimalsVolume);
            }
            return volume;
        }
    }
}
