﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.MarketMaker
{
    public partial class MarketMakerBotUi
    {
        private MarketMakerBot _strategy;

        public MarketMakerBotUi(MarketMakerBot strategy)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _strategy = strategy;

            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyClosePosition);

            ComboBoxVolumeType.Items.Add("Deposit percent");
            ComboBoxVolumeType.Items.Add("Contracts");
            ComboBoxVolumeType.Items.Add("Contract currency");
            ComboBoxVolumeType.SelectedItem = _strategy.VolumeType;

            ComboBoxRegime.SelectedItem = _strategy.Regime;
            TextBoxVolumeOne.Text = _strategy.Volume.ToString();
            TextBoxSpreadBeetwenLine.Text = _strategy.PersentToSpreadLines.ToString();
            TextBoxAssetInPortfolio.Text = "Prime";

            CheckBoxNeedToPaint.IsChecked = _strategy.PaintOn;

            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelVolume.Content = OsLocalization.Trader.Label30;
            LabelBetweenLines.Content = OsLocalization.Trader.Label130;
            CheckBoxNeedToPaint.Content = OsLocalization.Trader.Label131;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
            LabelVolumeType.Content = OsLocalization.Trader.Label554;
            LabelAssetInPortfolio.Content = OsLocalization.Trader.Label555;

            this.Activate();
            this.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TextBoxVolumeOne.Text.ToDecimal() <= 0||
                    TextBoxSpreadBeetwenLine.Text.ToDecimal() <= 0)
                {
                    throw new Exception("");
                }
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Trader.Label13);
                return;
            }

            _strategy.VolumeType = Convert.ToString(ComboBoxVolumeType.Text);
            _strategy.TradeAssetInPortfolio = Convert.ToString(TextBoxAssetInPortfolio.Text);
            _strategy.Volume = TextBoxVolumeOne.Text.ToDecimal();
            _strategy.PersentToSpreadLines = TextBoxSpreadBeetwenLine.Text.ToDecimal();
            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);

            if (CheckBoxNeedToPaint.IsChecked.HasValue)
            {
                _strategy.PaintOn = CheckBoxNeedToPaint.IsChecked.Value;
            }

            _strategy.Save();
            Close();
        }

        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(OsLocalization.Trader.Label113);
        }
    }
}
