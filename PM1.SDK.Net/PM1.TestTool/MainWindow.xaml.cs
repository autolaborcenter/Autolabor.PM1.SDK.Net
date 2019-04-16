﻿using System;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Autolabor.PM1.TestTool {
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window {
        private const string AutoSelectString = "自动选择";
        private const string UITestString = "界面测试";

        private readonly MainWindowContext _context;
        private CancellationTokenSource _connecting;

        public MainWindow() {
            InitializeComponent();
            _context = (MainWindowContext)DataContext;
            SerialPortCombo.Items.Add(AutoSelectString);
            SerialPortCombo.SelectedIndex = 0;
        }

        public void RefreshCombo() {
            SerialPortCombo.Items.Clear();
            SerialPortCombo.Items.Add(AutoSelectString);
            SerialPortCombo.Items.Add(new Separator());
            foreach (var port in SerialPort.GetPortNames())
                SerialPortCombo.Items.Add(port);
#if DEBUG
            if (SerialPortCombo.Items.Count > 2)
                SerialPortCombo.Items.Add(new Separator());
            SerialPortCombo.Items.Add(UITestString);
#endif
        }

        private void ComboBox_DropDownOpened(object sender, System.EventArgs e)
            => RefreshCombo();

        private void ComboBox_DropDownClosed(object sender, EventArgs e) {
            if (!(sender is ComboBox combo)) return;
            if (combo.SelectedIndex == -1)
                combo.SelectedIndex = 0;
        }

        private async void CheckBox_Click(object sender, RoutedEventArgs e) {
            if (_context.Connected)
                _connecting.Cancel();
            else
                await Connect((CheckBox)sender).ConfigureAwait(false);
        }

        private async Task Connect(CheckBox box) {
            box.IsEnabled = false;
            _context.Connected = true;
            _context.ErrorInfo = "";

            var flag = true;
            var progress = .0;
            _ = Task.Run(async () => {
                while (flag) {
                    _context.Progress = progress;
                    await Task.Delay(20).ConfigureAwait(false);
                }
                await Task.Delay(100).ConfigureAwait(false);
                _context.Progress = progress;
            });

            var port = SerialPortCombo.SelectedItem.ToString();
            {
                var temp = SerialPortCombo.SelectedItem;
                RefreshCombo();
                SerialPortCombo.SelectedItem = temp;
            }
            await Task.Run(async () => {
                try {
#if DEBUG
                    if (port != UITestString)
#endif
                        port = Methods.Initialize(port == AutoSelectString ? "" : port, null, out progress);

                    SerialPortCombo.Dispatch((it) => it.SelectedItem = port);
                } catch (Exception exception) {
                    _context.Connected = false;
                    _context.ErrorInfo = exception.Message;
                    return;
                } finally {
                    flag = false;
                    box.Dispatch((it) => it.IsEnabled = true);
                }

                _connecting = new CancellationTokenSource();
                await ToolFunctions.Handle(_context, _connecting).ConfigureAwait(false);
            }).ConfigureAwait(true);
        }

        private void Odometry_Reset_Button_Click(object sender, RoutedEventArgs e) {
            try {
                Methods.ResetOdometry();
            } catch (Exception exception) {
                _context.ErrorInfo = exception.Message;
            }
        }

        private void Clear_Error_Info(object sender, RoutedEventArgs e)
            => _context.ErrorInfo = "";

        private StateEnum ChassisState {
            set {
                try {
                    _context.State = null;
                    Methods.State = value;
                } catch (Exception exception) {
                    _context.ErrorInfo = exception.Message;
                }
            }
        }

        private void Unlock_Click(object sender, RoutedEventArgs e)
            => ChassisState = StateEnum.Unlocked;

        private void Lock_Click(object sender, RoutedEventArgs e)
            => ChassisState = StateEnum.Locked;

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            try {
                Methods.Shutdown();
            } catch (Exception) {
                // ignore
            }
        }
    }
}