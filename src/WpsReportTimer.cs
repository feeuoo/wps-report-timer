using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WpsReportTimer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool demoMode = false;
            int demoSeconds = 5;
            if (args.Length > 0 && args[0] == "--demo")
            {
                demoMode = true;
                if (args.Length > 1)
                    int.TryParse(args[1], out demoSeconds);
                if (demoSeconds < 1)
                    demoSeconds = 5;
            }

            bool createdNew;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "Local\\WpsReportTimer_SingleInstance", out createdNew))
            {
                if (!createdNew)
                    return;
                Application.Run(new TimerForm(demoMode, demoSeconds));
            }
        }
    }

    public sealed class TimerForm : Form
    {
        private static readonly Color TimerColor = Color.FromArgb(74, 222, 190);
        private static readonly Color OvertimeColor = Color.FromArgb(255, 107, 107);

        private readonly Label timerLabel;
        private readonly Label closeLabel;
        private readonly ContextMenuStrip contextMenu;
        private readonly ToolStripMenuItem settingsItem;
        private readonly ToolStripMenuItem pauseItem;
        private readonly ToolStripMenuItem resetItem;
        private readonly System.Windows.Forms.Timer pollTimer;
        private readonly System.Windows.Forms.Timer displayTimer;
        private readonly Stopwatch clock;
        private readonly bool demoMode;
        private readonly int demoSeconds;
        private readonly ToolTip toolTip;

        private int durationSeconds;
        private bool sessionActive;
        private bool paused;
        private int comFailureCount;

        public TimerForm(bool demoMode, int demoSeconds)
        {
            this.demoMode = demoMode;
            this.demoSeconds = demoSeconds;
            durationSeconds = 600;
            clock = new Stopwatch();

            Text = "WPS 报告计时器";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
            TopMost = true;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            timerLabel = new Label();
            timerLabel.Font = new Font("Consolas", 42F, FontStyle.Bold);
            timerLabel.ForeColor = TimerColor;
            timerLabel.BackColor = Color.Transparent;
            timerLabel.TextAlign = ContentAlignment.MiddleLeft;
            timerLabel.Margin = Padding.Empty;
            timerLabel.Padding = Padding.Empty;
            Controls.Add(timerLabel);

            closeLabel = new Label();
            closeLabel.Text = "×";
            closeLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            closeLabel.ForeColor = Color.FromArgb(180, 180, 180);
            closeLabel.BackColor = Color.Transparent;
            closeLabel.Margin = Padding.Empty;
            closeLabel.Padding = Padding.Empty;
            closeLabel.Cursor = Cursors.Hand;
            closeLabel.Click += delegate { Close(); };
            closeLabel.MouseEnter += delegate { closeLabel.ForeColor = OvertimeColor; };
            closeLabel.MouseLeave += delegate { closeLabel.ForeColor = Color.FromArgb(180, 180, 180); };
            Controls.Add(closeLabel);

            contextMenu = new ContextMenuStrip();
            settingsItem = new ToolStripMenuItem("设置报告时长…");
            settingsItem.Click += delegate { ShowDurationSettings(); };
            contextMenu.Items.Add(settingsItem);
            pauseItem = new ToolStripMenuItem("暂停");
            pauseItem.Click += PauseClick;
            contextMenu.Items.Add(pauseItem);
            resetItem = new ToolStripMenuItem("重新计时");
            resetItem.Click += ResetClick;
            contextMenu.Items.Add(resetItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { Close(); };
            contextMenu.Items.Add(exitItem);
            contextMenu.Opening += ContextMenuOpening;
            ContextMenuStrip = contextMenu;
            timerLabel.ContextMenuStrip = contextMenu;

            toolTip = new ToolTip();
            toolTip.SetToolTip(timerLabel, "等待放映时：双击设置时长，滚轮调整分钟\n放映计时中：右键暂停或重新计时");

            pollTimer = new System.Windows.Forms.Timer();
            pollTimer.Interval = 300;
            pollTimer.Tick += PollWps;

            displayTimer = new System.Windows.Forms.Timer();
            displayTimer.Interval = 100;
            displayTimer.Tick += DisplayTimerTick;

            Load += TimerFormLoad;
            Shown += TimerFormShown;
            FormClosing += TimerFormClosing;
            timerLabel.MouseDown += DragForm;
            timerLabel.DoubleClick += delegate { if (!sessionActive) ShowDurationSettings(); };
            timerLabel.MouseWheel += TimerLabelMouseWheel;
            MouseDown += DragForm;

            UpdateIdleDisplay();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        private void TimerFormLoad(object sender, EventArgs e)
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 24, area.Bottom - Height - 24);
        }

        private void TimerFormShown(object sender, EventArgs e)
        {
            displayTimer.Start();
            if (demoMode)
            {
                durationSeconds = demoSeconds;
                StartSession(IntPtr.Zero);
            }
            else
            {
                pollTimer.Start();
            }
        }

        private void TimerFormClosing(object sender, FormClosingEventArgs e)
        {
            pollTimer.Stop();
            displayTimer.Stop();
            clock.Stop();
        }

        private void ContextMenuOpening(object sender, EventArgs e)
        {
            settingsItem.Enabled = !sessionActive;
            pauseItem.Enabled = sessionActive;
            resetItem.Enabled = sessionActive;
            pauseItem.Text = paused ? "继续" : "暂停";
        }

        private void TimerLabelMouseWheel(object sender, MouseEventArgs e)
        {
            if (sessionActive)
                return;
            int change = e.Delta > 0 ? 60 : -60;
            durationSeconds = Math.Max(60, Math.Min(10800, durationSeconds + change));
            UpdateIdleDisplay();
        }

        private void ShowDurationSettings()
        {
            if (sessionActive)
                return;
            using (DurationForm dialog = new DurationForm(durationSeconds))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    durationSeconds = dialog.DurationSeconds;
                    UpdateIdleDisplay();
                }
            }
        }

        private void PollWps(object sender, EventArgs e)
        {
            int windowCount;
            IntPtr slideShowHandle;
            bool querySucceeded = TryGetSlideShowInfo(out windowCount, out slideShowHandle);

            if (!querySucceeded)
            {
                comFailureCount++;
                if (sessionActive && comFailureCount >= 15)
                    EndSession();
                return;
            }

            comFailureCount = 0;
            if (windowCount > 0)
            {
                if (!sessionActive)
                    StartSession(slideShowHandle);
            }
            else if (sessionActive)
            {
                EndSession();
            }
        }

        private bool TryGetSlideShowInfo(out int windowCount, out IntPtr slideShowHandle)
        {
            windowCount = 0;
            slideShowHandle = IntPtr.Zero;
            object app = null;
            object windows = null;
            object window = null;

            try
            {
                app = Marshal.GetActiveObject("Kwpp.Application");
                dynamic wpsApplication = app;
                windows = wpsApplication.SlideShowWindows;
                dynamic slideShowWindows = windows;
                windowCount = (int)slideShowWindows.Count;

                if (windowCount > 0)
                {
                    try
                    {
                        window = slideShowWindows.Item(1);
                        dynamic slideShowWindow = window;
                        slideShowHandle = new IntPtr((int)slideShowWindow.HWND);
                    }
                    catch
                    {
                        slideShowHandle = IntPtr.Zero;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                ReleaseComObject(window);
                ReleaseComObject(windows);
                ReleaseComObject(app);
            }
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                try { Marshal.ReleaseComObject(value); }
                catch { }
            }
        }

        private void StartSession(IntPtr slideShowHandle)
        {
            sessionActive = true;
            paused = false;
            comFailureCount = 0;
            clock.Reset();
            clock.Start();

            UpdateSessionDisplay();
            TopMost = false;
            TopMost = true;
            MoveToSlideShowScreen(slideShowHandle);
        }

        private void EndSession()
        {
            sessionActive = false;
            paused = false;
            clock.Reset();
            UpdateIdleDisplay();
        }

        private void MoveToSlideShowScreen(IntPtr slideShowHandle)
        {
            Screen screen = slideShowHandle != IntPtr.Zero
                ? Screen.FromHandle(slideShowHandle)
                : Screen.FromPoint(Cursor.Position);
            Rectangle area = screen.Bounds;
            Location = new Point(area.Right - Width - 24, area.Bottom - Height - 24);
        }

        private void PauseClick(object sender, EventArgs e)
        {
            if (!sessionActive)
                return;

            if (paused)
            {
                clock.Start();
                paused = false;
            }
            else
            {
                clock.Stop();
                paused = true;
            }
        }

        private void ResetClick(object sender, EventArgs e)
        {
            if (!sessionActive)
                return;
            clock.Reset();
            clock.Start();
            paused = false;
            UpdateSessionDisplay();
        }

        private void DisplayTimerTick(object sender, EventArgs e)
        {
            if (sessionActive)
                UpdateSessionDisplay();
        }

        private int GetRemainingSeconds()
        {
            return TimerLogic.GetRemainingSeconds(durationSeconds, clock.Elapsed.TotalSeconds);
        }

        private void UpdateSessionDisplay()
        {
            int remainingSeconds = GetRemainingSeconds();
            SetTimeText(TimerLogic.FormatTime(remainingSeconds));
            timerLabel.ForeColor = TimerLogic.IsOvertime(remainingSeconds) ? OvertimeColor : TimerColor;
        }

        private void UpdateIdleDisplay()
        {
            SetTimeText(TimerLogic.FormatTime(Math.Max(1, durationSeconds)));
            timerLabel.ForeColor = TimerColor;
        }

        private void SetTimeText(string value)
        {
            if (timerLabel.Text == value)
                return;
            int previousRight = Right;
            timerLabel.Text = value;
            UpdateCompactLayout();
            if (sessionActive && previousRight > 0)
                Left = previousRight - Width;
        }

        private void UpdateCompactLayout()
        {
            Size textSize = TextRenderer.MeasureText(
                timerLabel.Text,
                timerLabel.Font,
                new Size(1000, 200),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            textSize.Width += 2;
            textSize.Height += 2;
            timerLabel.SetBounds(0, 0, textSize.Width, textSize.Height);

            Size closeSize = TextRenderer.MeasureText(
                closeLabel.Text,
                closeLabel.Font,
                new Size(50, 50),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            closeLabel.SetBounds(timerLabel.Right + 2, 0, closeSize.Width + 2, closeSize.Height + 2);
            ClientSize = new Size(closeLabel.Right, Math.Max(timerLabel.Bottom, closeLabel.Bottom));
        }

        private void DragForm(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WmNcLButtonDown, NativeMethods.HtCaption, 0);
        }

        private static class NativeMethods
        {
            public const int WmNcLButtonDown = 0xA1;
            public const int HtCaption = 0x2;

            [DllImport("user32.dll")]
            public static extern bool ReleaseCapture();

            [DllImport("user32.dll")]
            public static extern IntPtr SendMessage(IntPtr hWnd, int message, int wParam, int lParam);
        }
    }

    public sealed class DurationForm : Form
    {
        private readonly NumericUpDown minutesInput;
        private readonly NumericUpDown secondsInput;

        public int DurationSeconds
        {
            get { return (int)minutesInput.Value * 60 + (int)secondsInput.Value; }
        }

        public DurationForm(int durationSeconds)
        {
            Text = "设置报告时长";
            ClientSize = new Size(230, 132);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Microsoft YaHei UI", 9F);

            Label minutesLabel = new Label();
            minutesLabel.Text = "分钟";
            minutesLabel.Location = new Point(24, 23);
            minutesLabel.Size = new Size(50, 24);
            Controls.Add(minutesLabel);

            minutesInput = new NumericUpDown();
            minutesInput.Location = new Point(92, 20);
            minutesInput.Size = new Size(105, 25);
            minutesInput.Minimum = 0;
            minutesInput.Maximum = 180;
            minutesInput.Value = durationSeconds / 60;
            Controls.Add(minutesInput);

            Label secondsLabel = new Label();
            secondsLabel.Text = "秒";
            secondsLabel.Location = new Point(24, 58);
            secondsLabel.Size = new Size(50, 24);
            Controls.Add(secondsLabel);

            secondsInput = new NumericUpDown();
            secondsInput.Location = new Point(92, 55);
            secondsInput.Size = new Size(105, 25);
            secondsInput.Minimum = 0;
            secondsInput.Maximum = 59;
            secondsInput.Value = durationSeconds % 60;
            Controls.Add(secondsInput);

            Button okButton = new Button();
            okButton.Text = "确定";
            okButton.Location = new Point(44, 94);
            okButton.Size = new Size(65, 28);
            okButton.DialogResult = DialogResult.OK;
            okButton.Click += OkClick;
            Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.Location = new Point(123, 94);
            cancelButton.Size = new Size(65, 28);
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void OkClick(object sender, EventArgs e)
        {
            if (minutesInput.Value == 0 && secondsInput.Value == 0)
                secondsInput.Value = 1;
        }
    }
}
