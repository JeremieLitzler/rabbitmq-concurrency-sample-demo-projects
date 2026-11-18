using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ConcurrencyTesting.Contracts;

namespace ConcurrencyTesting.WinForms
{
    public class MainForm : Form
    {
        private readonly TextBox _host = new TextBox { Text = "localhost" };
        private readonly NumericUpDown _port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 5672 };
        private readonly TextBox _vhost = new TextBox { Text = "winform-rabbit-tests" };
        private readonly TextBox _user = new TextBox { Text = "guest" };
        private readonly TextBox _password = new TextBox { Text = "guest", UseSystemPasswordChar = true };
        private readonly TextBox _exchange = new TextBox { Text = "test-exchange" };
        private readonly TextBox _routingKey = new TextBox { Text = "test-routing-key" };
        private readonly NumericUpDown _count = new NumericUpDown { Minimum = 1, Maximum = 5000, Value = 100 };
        private readonly NumericUpDown _timeout = new NumericUpDown { Minimum = 1, Maximum = 600, Value = 30 };
        private readonly NumericUpDown _spread = new NumericUpDown { Minimum = 0, Maximum = 60000, Value = 0 };
        private readonly CheckBox _preConnect = new CheckBox { Text = "Connect before publishing (otherwise the first calls also race through the lazy connect)", AutoSize = true };
        private readonly Button _runV1 = new Button { Text = "Run V1 (no publish lock)", AutoSize = true };
        private readonly Button _runV2 = new Button { Text = "Run V2 (publish lock)", AutoSize = true };
        private readonly Button _runBoth = new Button { Text = "Run V1 and V2 simultaneously", AutoSize = true };
        private readonly Button _clear = new Button { Text = "Clear", AutoSize = true };
        private readonly TextBox _output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9F) };

        public MainForm()
        {
            Text = "RabbitMQ publish concurrency test (.NET Framework 4.7.2)";
            Width = 900;
            Height = 700;

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(8) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            AddRow(grid, "Host", _host, "Port", _port);
            AddRow(grid, "Virtual host", _vhost, "Exchange", _exchange);
            AddRow(grid, "User", _user, "Password", _password);
            AddRow(grid, "Routing key", _routingKey, "Messages per run", _count);
            AddRow(grid, "Timeout (s)", _timeout, "Spread (ms, 0 = all at once)", _spread);

            _preConnect.Margin = new Padding(3, 6, 3, 3);
            grid.Controls.Add(_preConnect, 0, grid.RowCount);
            grid.SetColumnSpan(_preConnect, 4);
            grid.RowCount++;

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 0, 8, 8) };
            buttons.Controls.AddRange(new Control[] { _runV1, _runV2, _runBoth, _clear });

            Controls.Add(_output);
            Controls.Add(buttons);
            Controls.Add(grid);

            _runV1.Click += async (s, e) => await RunAsync(runV1: true, runV2: false);
            _runV2.Click += async (s, e) => await RunAsync(runV1: false, runV2: true);
            _runBoth.Click += async (s, e) => await RunAsync(runV1: true, runV2: true);
            _clear.Click += (s, e) => _output.Clear();
        }

        private static void AddRow(TableLayoutPanel grid, string label1, Control c1, string label2, Control c2)
        {
            int row = grid.RowCount;
            grid.RowCount++;
            grid.Controls.Add(new Label { Text = label1, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) }, 0, row);
            c1.Dock = DockStyle.Fill;
            grid.Controls.Add(c1, 1, row);
            if (c2 != null)
            {
                grid.Controls.Add(new Label { Text = label2, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) }, 2, row);
                c2.Dock = DockStyle.Fill;
                grid.Controls.Add(c2, 3, row);
            }
        }

        private async Task RunAsync(bool runV1, bool runV2)
        {
            var settings = new PublisherSettings
            {
                HostName = _host.Text,
                Port = (int)_port.Value,
                VirtualHost = _vhost.Text,
                UserName = _user.Text,
                Password = _password.Text,
                Exchange = _exchange.Text,
                RoutingKey = _routingKey.Text
            };
            int count = (int)_count.Value;
            var timeout = TimeSpan.FromSeconds((double)_timeout.Value);
            bool preConnect = _preConnect.Checked;
            int spreadMs = (int)_spread.Value;

            SetBusy(true);
            Log($"--- {DateTime.Now:HH:mm:ss} start: {count} concurrent publishes per library, preConnect={preConnect}, spread={spreadMs} ms ---");
            try
            {
                var runs = new List<Task<RunResult>>();
                if (runV1)
                    runs.Add(ConcurrencyRunner.RunAsync("V1", () => Publisher.V1.ClientPublisherFactory.Create(settings), count, timeout, preConnect, spreadMs));
                if (runV2)
                    runs.Add(ConcurrencyRunner.RunAsync("V2", () => Publisher.V2.ClientPublisherFactory.Create(settings), count, timeout, preConnect, spreadMs));

                RunResult[] results = await Task.WhenAll(runs);
                foreach (RunResult result in results)
                    Log(result.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                Log("Run aborted: " + ex.GetBaseException().GetType().Name + ": " + ex.GetBaseException().Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            foreach (Button b in new[] { _runV1, _runV2, _runBoth })
                b.Enabled = !busy;
        }

        private void Log(string text)
        {
            _output.AppendText(text.Replace("\n", Environment.NewLine) + Environment.NewLine);
        }
    }
}
