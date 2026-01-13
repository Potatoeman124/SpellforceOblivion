using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace SpellforceDataEditor.OblivionScripts
{
    public sealed class ProgressForm : Form
    {
        private readonly Label _lblPhase = new Label();
        private readonly Label _lblDetail = new Label();
        private readonly Label _lblPercent = new Label();
        private readonly ProgressBar _bar = new ProgressBar();
        private readonly Button _btnCancel = new Button();

        public event EventHandler? CancelRequested;

        public ProgressForm()
        {
            Text = "Progress";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 140);

            _lblPhase.AutoSize = false;
            _lblPhase.Location = new Point(12, 10);
            _lblPhase.Size = new Size(496, 20);
            _lblPhase.Font = new Font(_lblPhase.Font, FontStyle.Bold);

            _lblDetail.AutoSize = false;
            _lblDetail.Location = new Point(12, 32);
            _lblDetail.Size = new Size(496, 18);

            _bar.Location = new Point(12, 58);
            _bar.Size = new Size(410, 22);
            _bar.Minimum = 0;
            _bar.Maximum = 100;

            _lblPercent.AutoSize = false;
            _lblPercent.Location = new Point(430, 58);
            _lblPercent.Size = new Size(78, 22);
            _lblPercent.TextAlign = ContentAlignment.MiddleRight;

            _btnCancel.Text = "Cancel";
            _btnCancel.Location = new Point(413, 95);
            _btnCancel.Size = new Size(95, 28);
            _btnCancel.Click += (_, __) =>
            {
                _btnCancel.Enabled = false;
                CancelRequested?.Invoke(this, EventArgs.Empty);
            };

            Controls.Add(_lblPhase);
            Controls.Add(_lblDetail);
            Controls.Add(_bar);
            Controls.Add(_lblPercent);
            Controls.Add(_btnCancel);
        }

        public void UpdateProgress(ProgressInfo p)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateProgress(p)));
                return;
            }

            _lblPhase.Text = p.Phase ?? "";
            _lblDetail.Text = p.Detail ?? "";

            int total = p.Total <= 0 ? 1 : p.Total;
            int cur = p.Current;
            if (cur < 0) cur = 0;
            if (cur > total) cur = total;

            _bar.Minimum = 0;
            _bar.Maximum = total;
            _bar.Value = cur;

            _lblPercent.Text = $"{p.Percent}%";
        }

        public void BindCancellation(CancellationTokenSource cts)
        {
            if (cts == null) throw new ArgumentNullException(nameof(cts));

            // If user clicks Cancel, propagate cancellation to the running pipelines.
            CancelRequested += (_, __) =>
            {
                try
                {
                    if (!cts.IsCancellationRequested)
                        cts.Cancel();
                }
                catch
                {
                    // ignore
                }
            };
        }
    }
}
