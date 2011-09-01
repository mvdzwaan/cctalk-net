﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using dk.CctalkLib.Connections;
using dk.CctalkLib.Devices;

namespace cctalk_apptest
{
	public partial class Form1 : Form
	{
		CoinAcceptor _ca;
		Decimal _coinCounter = 0;

		public Form1()
		{
			InitializeComponent();
			configWord.Text = CoinAcceptor.ConfigWord(CoinAcceptor.DefaultConfig);

		}

		private void TryCreateCoinAcceptor()
		{
			try
			{
				CreateCoinAcceptor();
			} catch(Exception ex)
			{
				MessageBox.Show(ex.ToString());
				DisposeCoinAcceptor();

				////5-10 srconds device can be "Unusable"
				//Thread.Sleep(5000);
				//CreateCoinAcceptor();
			}


		}

		private void CreateCoinAcceptor()
		{
			var con = new ConnectionRs232
						{
							PortName = GetCom(),
						};

			Dictionary<byte, CoinTypeInfo> coins;
			if (!CoinAcceptor.TryParseConfigWord(configWord.Text, out coins))
			{
				MessageBox.Show("Wrong config word, using defaults");

				coins = CoinAcceptor.DefaultConfig;
				configWord.Text = CoinAcceptor.ConfigWord(CoinAcceptor.DefaultConfig);
			}

			_ca = new CoinAcceptor(
				Convert.ToByte(deviceNumber.Value),
				con,
				coins,
					null
				);

			_ca.CoinAccepted += _ca_CoinAccepted;
			_ca.ErrorMessageAccepted += _ca_ErrorMessageAccepted;

			_ca.Init();

			groupBox1.Enabled = true;
			panel1.Enabled = true;

			initButton.Enabled = false;
			resetButton.Enabled = true;
			configWord.Enabled = false;
		}

		private void DisposeCoinAcceptor()
		{
			if (_ca == null)
				return;

			if (_ca.IsInitialized)
			{
				_ca.IsInhibiting = true;
				_ca.UnInit();
			}

			_ca.Dispose();

			_ca = null;

			groupBox1.Enabled = false;
			panel1.Enabled = false;
			initButton.Enabled = true;
			resetButton.Enabled = false;
			configWord.Enabled = true;
		}


		private string GetCom()
		{
			return string.Format("com{0:g0}", comNumber.Value);
		}

		void _ca_ErrorMessageAccepted(object sender, CoinAcceptorErrorEventArgs e)
		{
			if (InvokeRequired)
			{
				Invoke((EventHandler<CoinAcceptorErrorEventArgs>)_ca_ErrorMessageAccepted, sender, e);
				return;
			}

			listBox1.Items.Add(String.Format("Coin acceptor error: {0} ({1}, {2:X2})", e.ErrorMessage, e.Error, (Byte)e.Error));

			listBox1.SelectedIndex = listBox1.Items.Count - 1;
			//listBox1.SelectedIndex = -1;
		}

		void _ca_CoinAccepted(object sender, CoinAcceptorCoinEventArgs e)
		{
			if (InvokeRequired)
			{
				Invoke((EventHandler<CoinAcceptorCoinEventArgs>)_ca_CoinAccepted, sender, e);
				return;
			}
			_coinCounter += e.CoinValue;
			listBox1.Items.Add(String.Format("Coin accepted: {0} ({1:X2}), path {3}. Now accepted: {2:C}", e.CoinName, e.CoinCode, _coinCounter, e.RoutePath));

			listBox1.SelectedIndex = listBox1.Items.Count - 1;
			//listBox1.SelectedIndex = -1;
	
			// There is simulator of long-working event handler
			Thread.Sleep(1000);
		}

		private void button1_Click(object sender, EventArgs e)
		{
			// Attention! There we are creating new device object. But it could share connection with _ca.
			ICctalkConnection con;
			Boolean isMyConnection;

			if (_ca.Connection.IsOpen())
			{
				con = _ca.Connection;
				isMyConnection = false;
			} else
			{
				con = new ConnectionRs232
				{
					PortName = GetCom(),
				};
				con.Open();
				isMyConnection = true;
			}
			try
			{
				var c = new GenericCctalkDevice
							{
								Connection = con,
								Address = 0
							};

				if (radioButton1.Checked)
				{
					var buf = c.CmdReadEventBuffer();


					var sb = new StringBuilder();
					sb.Append("Принято: ");
					sb.AppendFormat("Cntr={0} Data:", buf.Counter);
					for (int i = 0; i < buf.Events.Length; i++)
					{
						var ev = buf.Events[i];
						sb.AppendFormat("({0:X2} {1:X2}) ", ev.CoinCode, ev.ErrorOrRouteCode);
					}

					listBox1.Items.Add(sb.ToString());
					listBox1.SelectedIndex = listBox1.Items.Count - 1;


				}
				else if (radioButton2.Checked)
				{
					var serial = c.CmdGetSerial();
					listBox1.Items.Add(String.Format("SN: {0}", serial));
					listBox1.SelectedIndex = listBox1.Items.Count - 1;


				} else if (radioButton3.Checked)
				{
					c.CmdReset();
				}
			} finally
			{
				if (isMyConnection)
					con.Close();
			}

		}


		private void clearToolStripMenuItem_Click(object sender, EventArgs e)
		{
			listBox1.Items.Clear();
		}

		private void cbPolling_CheckedChanged(object sender, EventArgs e)
		{
			if (_ca == null) return;

			if (!_ca.IsInitialized)
				_ca.Init();

			if (cbPolling.Checked)
				_ca.StartPoll();
			else
				_ca.EndPoll();

			//groupBox1.Enabled = !_ca.IsPolling;

		}

		private void clearMoneyCounterToolStripMenuItem_Click(object sender, EventArgs e)
		{
			_coinCounter = 0;
		}

		private void cbInhibit_CheckedChanged(object sender, EventArgs e)
		{
			_ca.IsInhibiting = cbInhibit.Checked;
		}

		private void initButton_Click(object sender, EventArgs e)
		{
			try
			{
				TryCreateCoinAcceptor();
			} catch (Exception ex)
			{

				DisposeCoinAcceptor();
				MessageBox.Show(ex.Message, "Error while connecting device", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void resetButton_Click(object sender, EventArgs e)
		{
			_brutimer.Stop();

			DisposeCoinAcceptor();
			cbPolling.Checked = false;

		}

		private void ready_Click(object sender, EventArgs e)
		{
			MessageBox.Show("GetStatus = " + _ca.GetStatus(), Text);
		}

		private void Form1_FormClosed(object sender, FormClosedEventArgs e)
		{
			Properties.Settings.Default.Save();
		}

		readonly System.Windows.Forms.Timer _brutimer = new System.Windows.Forms.Timer();

		private void cbBrute_CheckedChanged(object sender, EventArgs e)
		{
			if(cbBrute.Checked)
			{
				_brutimer.Interval = 1;
				_brutimer.Start();
				_brutimer.Tick += _brutimer_Tick;
			}else
			{
				_brutimer.Stop();
			}
		}

		void _brutimer_Tick(object sender, EventArgs e)
		{
			button1_Click(sender, e);
		}

		private void butPollNow_Click(object sender, EventArgs e)
		{
			if(_ca == null) return;
			_ca.PollNow();
		}
	}
}
