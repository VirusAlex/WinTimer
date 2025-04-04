namespace WinTimer;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class MainForm : Form
{
    #region Constants
    private const int BASE_WIDTH = 320;
    private const int BASE_HEIGHT = 180;
    private const int BASE_FONT_SIZE = 40;
    private const int BASE_BUTTON_FONT_SIZE = 14;
    private const int RESIZE_BORDER = 5;
    private const float ASPECT_RATIO = BASE_WIDTH / (float)BASE_HEIGHT;
    private static readonly Font BUTTON_FONT = new Font("Segoe UI", BASE_BUTTON_FONT_SIZE, FontStyle.Regular);
    #endregion

    #region Enums
    private enum Mode
    {
        Clock,
        Timer,
        Stopwatch
    }
    #endregion

    #region Timers
    private Timer timerClock;
    private Timer timerCountdown;
    private Timer timerStopwatch;
    #endregion

    #region Flip Display Controls
    private Panel pnlTimeDisplay;
    private FlipDigit[] hourDigits;
    private FlipDigit[] minuteDigits;
    private FlipDigit[] secondDigits;
    private Label[] separators;
    #endregion

    #region Control Buttons
    private Panel pnlControls;
    private Button btnClock;
    private Button btnTimer;
    private Button btnStopwatch;
    private Button btnStart;
    private Button btnPause;
    private Button btnReset;
    private Button btnTopMost;
    private Button btnClose;
    #endregion

    #region State Variables
    private bool isResizing = false;
    private Mode currentMode = Mode.Clock;
    private TimeSpan countdownTime = TimeSpan.Zero;
    private TimeSpan stopwatchTime = TimeSpan.Zero;
    
    // Variables for window dragging
    private bool isDragging = false;
    private Point dragStartPoint;
    #endregion

    public MainForm()
    {
        InitializeComponent();
        InitializeTimers();
        UpdateDisplay();
        
        this.Resize += MainForm_Resize;
        this.Icon = AppIcon.GetAppIcon();
    }

    #region Window Handling
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084;
        
        if (m.Msg == WM_NCHITTEST)
        {
            Point pos = new Point(m.LParam.ToInt32());
            pos = this.PointToClient(pos);
            
            if (pos.X <= RESIZE_BORDER && pos.Y <= RESIZE_BORDER)
                m.Result = (IntPtr)13; // HTTOPLEFT
            else if (pos.X >= ClientSize.Width - RESIZE_BORDER && pos.Y <= RESIZE_BORDER)
                m.Result = (IntPtr)14; // HTTOPRIGHT
            else if (pos.X <= RESIZE_BORDER && pos.Y >= ClientSize.Height - RESIZE_BORDER)
                m.Result = (IntPtr)16; // HTBOTTOMLEFT
            else if (pos.X >= ClientSize.Width - RESIZE_BORDER && pos.Y >= ClientSize.Height - RESIZE_BORDER)
                m.Result = (IntPtr)17; // HTBOTTOMRIGHT
            else if (pos.X <= RESIZE_BORDER)
                m.Result = (IntPtr)10; // HTLEFT
            else if (pos.Y <= RESIZE_BORDER)
                m.Result = (IntPtr)12; // HTTOP
            else if (pos.X >= ClientSize.Width - RESIZE_BORDER)
                m.Result = (IntPtr)11; // HTRIGHT
            else if (pos.Y >= ClientSize.Height - RESIZE_BORDER)
                m.Result = (IntPtr)15; // HTBOTTOM
            else
                base.WndProc(ref m);
            
            return;
        }
        
        base.WndProc(ref m);
    }

    private void MainForm_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            isDragging = true;
            Point screenPoint = (sender as Control)?.PointToScreen(new Point(e.X, e.Y)) ?? Point.Empty;
            dragStartPoint = PointToClient(screenPoint);
        }
    }
    
    private void MainForm_MouseMove(object? sender, MouseEventArgs e)
    {
        if (isDragging)
        {
            Point screenPoint = (sender as Control)?.PointToScreen(new Point(e.X, e.Y)) ?? Point.Empty;
            Point clientPoint = PointToClient(screenPoint);
            Location = new Point(
                Location.X + (clientPoint.X - dragStartPoint.X),
                Location.Y + (clientPoint.Y - dragStartPoint.Y)
            );
        }
    }
    
    private void MainForm_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            isDragging = false;
        }
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (isResizing) return;
        
        try
        {
            isResizing = true;
            
            // Maintain aspect ratio
            float currentRatio = this.Width / (float)this.Height;
            
            if (Math.Abs(currentRatio - ASPECT_RATIO) > 0.01f)
            {
                if (this.Width != this.RestoreBounds.Width)
                {
                    this.Height = (int)(this.Width / ASPECT_RATIO);
                }
                else
                {
                    this.Width = (int)(this.Height * ASPECT_RATIO);
                }
            }
            
            float scale = this.Width / (float)BASE_WIDTH;
            
            // Scale all controls
            if (pnlTimeDisplay != null && pnlControls != null)
            {
                // Обновляем размеры панелей
                pnlControls.Height = (int)(40 * scale);
                
                // Масштабируем элементы
                ScaleFlipClockDisplay(scale);
                ScaleControlPanel(scale);
                
                // Обновляем размещение на форме
                this.Controls.Remove(pnlControls);
                this.Controls.Remove(pnlTimeDisplay);
                
                this.Controls.Add(pnlControls);
                this.Controls.Add(pnlTimeDisplay);
                
                // Обновляем отображение времени
                UpdateDisplay();
            }
        }
        finally
        {
            isResizing = false;
        }
    }

    public void AddHandlers(Control control)
    {
        control.MouseDown += MainForm_MouseDown;
        control.MouseMove += MainForm_MouseMove;
        control.MouseUp += MainForm_MouseUp;
    }

    private void FlashWindow()
    {
        for (int i = 0; i < 10; i++)
        {
            this.BackColor = Color.Red;
            Application.DoEvents();
            System.Threading.Thread.Sleep(200);
            this.BackColor = Color.FromArgb(0, 0, 0);
            Application.DoEvents();
            System.Threading.Thread.Sleep(200);
        }
    }
    #endregion

    #region Controls Initialization
    private void InitializeComponent()
    {
        // Form setup
        this.Text = "WinTimer";
        this.Size = new Size(BASE_WIDTH, BASE_HEIGHT);
        this.FormBorderStyle = FormBorderStyle.None;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(0, 0, 0);
        this.ForeColor = Color.White;
        this.MinimumSize = new Size(240, 135);
        this.Padding = new Padding(RESIZE_BORDER);

        // Add handlers for window dragging
        AddHandlers(this);
        
        // Инициализируем сначала панель управления, а затем дисплей времени
        InitializeControlPanel();
        InitializeTimeDisplay();
        
        // Add panels to form
        this.Controls.Add(pnlControls);
        this.Controls.Add(pnlTimeDisplay);
    }

    private void InitializeTimeDisplay()
    {
        // Main panel for time display
        pnlTimeDisplay = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        AddHandlers(pnlTimeDisplay);
        
        // Create flip digits for HH:MM:SS
        hourDigits = new FlipDigit[2];
        minuteDigits = new FlipDigit[2]; 
        secondDigits = new FlipDigit[2];
        separators = new Label[2];
        
        // Create individual digits
        for (int i = 0; i < 2; i++)
        {
            hourDigits[i] = new FlipDigit();
            minuteDigits[i] = new FlipDigit();
            secondDigits[i] = new FlipDigit();
            
            pnlTimeDisplay.Controls.Add(hourDigits[i]);
            pnlTimeDisplay.Controls.Add(minuteDigits[i]);
            pnlTimeDisplay.Controls.Add(secondDigits[i]);
        }
        
        // Create separators (:)
        for (int i = 0; i < 2; i++)
        {
            separators[i] = new Label
            {
                Text = ":",
                Font = new Font("Segoe UI", BASE_FONT_SIZE, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false
            };
            pnlTimeDisplay.Controls.Add(separators[i]);
            AddHandlers(separators[i]);
        }
        
        // Position all display elements
        LayoutTimeDisplay(1.0f);
    }

    private void InitializeControlPanel()
    {
        // Control panel at the bottom
        pnlControls = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            BackColor = Color.FromArgb(20, 20, 20)
        };
        AddHandlers(pnlControls);
        
        // Create all buttons
        btnClock = CreateButton("🕒");
        btnTimer = CreateButton("⏱️");
        btnStopwatch = CreateButton("⏲️");
        btnStart = CreateButton("▶️");
        btnPause = CreateButton("⏸️");
        btnReset = CreateButton("🔄");
        btnTopMost = CreateButton("📌");
        btnClose = CreateButton("✖");
        
        // Add all buttons to panel
        pnlControls.Controls.Add(btnClock);
        pnlControls.Controls.Add(btnTimer);
        pnlControls.Controls.Add(btnStopwatch);
        pnlControls.Controls.Add(btnStart);
        pnlControls.Controls.Add(btnPause);
        pnlControls.Controls.Add(btnReset);
        pnlControls.Controls.Add(btnTopMost);
        pnlControls.Controls.Add(btnClose);
        
        // Position buttons
        LayoutControlPanel(1.0f);
        
        // Initial button state
        UpdateButtonStates();
        
        // Attach event handlers
        btnClock.Click += (s, e) => SwitchToClockMode();
        btnTimer.Click += BtnTimer_Click;
        btnStopwatch.Click += (s, e) => SwitchToStopwatchMode();
        btnStart.Click += BtnStart_Click;
        btnPause.Click += BtnPause_Click;
        btnReset.Click += BtnReset_Click;
        btnTopMost.Click += BtnTopMost_Click;
        btnClose.Click += (s, e) => this.Close();
        
        // Create timer context menu
        InitializeTimerContextMenu();
    }

    private void InitializeTimerContextMenu()
    {
        ContextMenuStrip timerMenu = new ContextMenuStrip();
        timerMenu.Items.Add("1 минута", null, TimerMenuItem_Click);
        timerMenu.Items.Add("5 минут", null, TimerMenuItem_Click);
        timerMenu.Items.Add("10 минут", null, TimerMenuItem_Click);
        timerMenu.Items.Add("15 минут", null, TimerMenuItem_Click);
        timerMenu.Items.Add("30 минут", null, TimerMenuItem_Click);
        timerMenu.Items.Add("1 час", null, TimerMenuItem_Click);
        btnTimer.ContextMenuStrip = timerMenu;
    }

    private void InitializeTimers()
    {
        // Clock timer
        timerClock = new Timer
        {
            Interval = 1000
        };
        timerClock.Tick += TimerClock_Tick;
        timerClock.Start();
        
        // Countdown timer
        timerCountdown = new Timer
        {
            Interval = 1000
        };
        timerCountdown.Tick += TimerCountdown_Tick;
        
        // Stopwatch timer
        timerStopwatch = new Timer
        {
            Interval = 1000
        };
        timerStopwatch.Tick += TimerStopwatch_Tick;
    }
    #endregion

    #region UI Helpers
    public Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Width = (int)(40),
            Height = (int)(30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            Margin = new Padding(0),
            Font = BUTTON_FONT,
            FlatAppearance =
            {
                BorderSize = 0
            }
        };
        AddHandlers(button);
        return button;
    }

    private void ScaleButton(Button? button, float scale)
    {
        if (button == null) return;
        
        button.Width = (int)(40 * scale);
        button.Height = (int)(30 * scale);
        button.Font = new Font(button.Font.FontFamily, BASE_BUTTON_FONT_SIZE * scale, FontStyle.Regular);
    }
    
    private void LayoutTimeDisplay(float scale)
    {
        if (pnlTimeDisplay.Width <= 0 || pnlTimeDisplay.Height <= 0)
        {
            // В случае, если панель еще не размещена, используем размеры формы для приблизительного расчета
            pnlTimeDisplay.Width = ClientSize.Width;
            pnlTimeDisplay.Height = ClientSize.Height - (pnlControls?.Height ?? 40);
        }
        
        int digitWidth = (int)(40 * scale);
        int digitHeight = (int)(60 * scale);
        int separatorWidth = (int)(15 * scale);
        int totalWidth = 6 * digitWidth + 2 * separatorWidth;
        
        int startX = Math.Max(5, (pnlTimeDisplay.Width - totalWidth) / 2);
        int controlsHeight = pnlControls?.Height ?? 40;
        int y = Math.Max(5, (pnlTimeDisplay.Height - controlsHeight - digitHeight) / 2);
        
        int x = startX;
        
        // Position hour digits
        for (int i = 0; i < 2; i++)
        {
            hourDigits[i].Width = digitWidth;
            hourDigits[i].Height = digitHeight;
            hourDigits[i].Location = new Point(x, y);
            x += digitWidth;
        }
        
        // First separator
        separators[0].Width = separatorWidth;
        separators[0].Height = digitHeight;
        separators[0].Location = new Point(x, y);
        separators[0].Font = new Font(separators[0].Font.FontFamily, BASE_FONT_SIZE * scale, FontStyle.Bold);
        x += separatorWidth;
        
        // Position minute digits
        for (int i = 0; i < 2; i++)
        {
            minuteDigits[i].Width = digitWidth;
            minuteDigits[i].Height = digitHeight;
            minuteDigits[i].Location = new Point(x, y);
            x += digitWidth;
        }
        
        // Second separator
        separators[1].Width = separatorWidth;
        separators[1].Height = digitHeight;
        separators[1].Location = new Point(x, y);
        separators[1].Font = new Font(separators[1].Font.FontFamily, BASE_FONT_SIZE * scale, FontStyle.Bold);
        x += separatorWidth;
        
        // Position second digits
        for (int i = 0; i < 2; i++)
        {
            secondDigits[i].Width = digitWidth;
            secondDigits[i].Height = digitHeight;
            secondDigits[i].Location = new Point(x, y);
            x += digitWidth;
        }
    }
    
    private void LayoutControlPanel(float scale)
    {
        if (pnlControls.Width <= 0)
        {
            // В случае, если панель еще не размещена, используем ширину формы для приблизительного расчета
            pnlControls.Width = ClientSize.Width;
        }
        
        int buttonWidth = (int)(40 * scale);
        int buttonHeight = (int)(30 * scale);
        int gap = (int)(5 * scale);
        
        int totalWidth = 8 * buttonWidth + 7 * gap;
        int startX = Math.Max(5, (pnlControls.Width - totalWidth) / 2);
        int y = Math.Max(5, (pnlControls.Height - buttonHeight) / 2);
        
        int x = startX;
        
        // Position all buttons
        btnClock.Location = new Point(x, y);
        btnClock.Width = buttonWidth;
        btnClock.Height = buttonHeight;
        x += buttonWidth + gap;
        
        btnTimer.Location = new Point(x, y);
        btnTimer.Width = buttonWidth;
        btnTimer.Height = buttonHeight;
        x += buttonWidth + gap;
        
        btnStopwatch.Location = new Point(x, y);
        btnStopwatch.Width = buttonWidth;
        btnStopwatch.Height = buttonHeight;
        x += buttonWidth + gap;
        
        btnStart.Location = new Point(x, y);
        btnStart.Width = buttonWidth;
        btnStart.Height = buttonHeight;
        x += buttonWidth + gap;
        
        btnPause.Location = new Point(x, y);
        btnPause.Width = buttonWidth;
        btnPause.Height = buttonHeight;
        x += buttonWidth + gap;
        
        btnReset.Location = new Point(x, y);
        btnReset.Width = buttonWidth;
        btnReset.Height = buttonHeight;
        x += buttonWidth + gap;
        
        btnTopMost.Location = new Point(x, y);
        btnTopMost.Width = buttonWidth;
        btnTopMost.Height = buttonHeight;
        x += buttonWidth + gap;
        
        btnClose.Location = new Point(x, y);
        btnClose.Width = buttonWidth;
        btnClose.Height = buttonHeight;
    }
    
    private void ScaleFlipClockDisplay(float scale)
    {
        pnlTimeDisplay.Padding = new Padding((int)(10 * scale));
        
        // Resize and reposition all digits and separators
        LayoutTimeDisplay(scale);
    }
    
    private void ScaleControlPanel(float scale)
    {
        pnlControls.Height = (int)(40 * scale);
        
        // Scale buttons
        ScaleButton(btnClock, scale);
        ScaleButton(btnTimer, scale);
        ScaleButton(btnStopwatch, scale);
        ScaleButton(btnStart, scale);
        ScaleButton(btnPause, scale);
        ScaleButton(btnReset, scale);
        ScaleButton(btnTopMost, scale);
        ScaleButton(btnClose, scale);
        
        // Reposition buttons
        LayoutControlPanel(scale);
    }
    #endregion

    #region Timer Event Handlers
    private void TimerClock_Tick(object sender, EventArgs e)
    {
        if (currentMode == Mode.Clock)
        {
            UpdateDisplay();
        }
    }
    
    private void TimerCountdown_Tick(object sender, EventArgs e)
    {
        countdownTime = countdownTime.Subtract(TimeSpan.FromSeconds(1));
        
        if (countdownTime <= TimeSpan.Zero)
        {
            timerCountdown?.Stop();
            countdownTime = TimeSpan.Zero;
            UpdateButtonStates();
            
            UpdateDisplay();
            
            // Notify when timer completes
            this.FlashWindow();
            MessageBox.Show("Время истекло!", "WinTimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        UpdateDisplay();
    }
    
    private void TimerStopwatch_Tick(object sender, EventArgs e)
    {
        stopwatchTime = stopwatchTime.Add(TimeSpan.FromSeconds(1));
        UpdateDisplay();
    }
    #endregion

    #region Mode Switching
    private void SwitchToClockMode()
    {
        currentMode = Mode.Clock;
        timerCountdown?.Stop();
        timerStopwatch?.Stop();
        timerClock?.Start();
        
        UpdateButtonStates();
        UpdateDisplay();
    }
    
    private void SwitchToTimerMode()
    {
        currentMode = Mode.Timer;
        timerClock?.Stop();
        timerStopwatch?.Stop();
        
        UpdateButtonStates();
        UpdateDisplay();
    }
    
    private void SwitchToStopwatchMode()
    {
        currentMode = Mode.Stopwatch;
        timerClock?.Stop();
        timerCountdown?.Stop();
        
        UpdateButtonStates();
        UpdateDisplay();
    }
    
    private void UpdateButtonStates()
    {
        // Basic mode buttons are always visible
        btnClock.Visible = true;
        btnTimer.Visible = true;
        btnStopwatch.Visible = true;
        btnTopMost.Visible = true;
        btnClose.Visible = true;
        
        // Control buttons visibility based on mode
        switch (currentMode)
        {
            case Mode.Clock:
                btnStart.Visible = false;
                btnPause.Visible = false;
                btnReset.Visible = false;
                break;
                
            case Mode.Timer:
            case Mode.Stopwatch:
                btnStart.Visible = true;
                btnPause.Visible = true;
                btnReset.Visible = true;
                
                bool isRunning = currentMode == Mode.Timer ? 
                    timerCountdown.Enabled : timerStopwatch.Enabled;
                
                btnStart.Enabled = !isRunning;
                btnPause.Enabled = isRunning;
                btnReset.Enabled = true;
                break;
        }
        
        // Highlight active mode button
        btnClock.BackColor = currentMode == Mode.Clock ? Color.FromArgb(80, 80, 80) : Color.FromArgb(50, 50, 50);
        btnTimer.BackColor = currentMode == Mode.Timer ? Color.FromArgb(80, 80, 80) : Color.FromArgb(50, 50, 50);
        btnStopwatch.BackColor = currentMode == Mode.Stopwatch ? Color.FromArgb(80, 80, 80) : Color.FromArgb(50, 50, 50);
    }
    #endregion

    #region Button Event Handlers
    private void BtnTimer_Click(object sender, EventArgs e)
    {
        btnTimer?.ContextMenuStrip?.Show(btnTimer, new Point(0, btnTimer.Height));
    }
    
    private void BtnStart_Click(object sender, EventArgs e)
    {
        if (currentMode == Mode.Timer)
        {
            timerCountdown?.Start();
        }
        else if (currentMode == Mode.Stopwatch)
        {
            timerStopwatch?.Start();
        }
        
        UpdateButtonStates();
    }
    
    private void BtnPause_Click(object sender, EventArgs e)
    {
        if (currentMode == Mode.Timer)
        {
            timerCountdown?.Stop();
        }
        else if (currentMode == Mode.Stopwatch)
        {
            timerStopwatch?.Stop();
        }
        
        UpdateButtonStates();
    }
    
    private void BtnReset_Click(object sender, EventArgs e)
    {
        if (currentMode == Mode.Timer)
        {
            timerCountdown?.Stop();
            countdownTime = TimeSpan.Zero;
        }
        else if (currentMode == Mode.Stopwatch)
        {
            timerStopwatch?.Stop();
            stopwatchTime = TimeSpan.Zero;
        }
        
        UpdateButtonStates();
        UpdateDisplay();
    }
    
    private void BtnTopMost_Click(object sender, EventArgs e)
    {
        this.TopMost = !this.TopMost;
        btnTopMost.BackColor = this.TopMost ? Color.FromArgb(100, 100, 100) : Color.FromArgb(50, 50, 50);
    }
    
    private void TimerMenuItem_Click(object sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item) return;
        string text = item.Text;
        
        switch (text)
        {
            case "1 минута":
                countdownTime = TimeSpan.FromMinutes(1);
                break;
            case "5 минут":
                countdownTime = TimeSpan.FromMinutes(5);
                break;
            case "10 минут":
                countdownTime = TimeSpan.FromMinutes(10);
                break;
            case "15 минут":
                countdownTime = TimeSpan.FromMinutes(15);
                break;
            case "30 минут":
                countdownTime = TimeSpan.FromMinutes(30);
                break;
            case "1 час":
                countdownTime = TimeSpan.FromHours(1);
                break;
        }
        
        SwitchToTimerMode();
    }
    #endregion

    #region Display Update
    private void UpdateDisplay()
    {
        string timeText;
        
        switch (currentMode)
        {
            case Mode.Clock:
                timeText = DateTime.Now.ToString("HH:mm:ss");
                break;
            case Mode.Timer:
                timeText = countdownTime.ToString(@"hh\:mm\:ss");
                break;
            case Mode.Stopwatch:
                timeText = stopwatchTime.ToString(@"hh\:mm\:ss");
                break;
            default:
                timeText = "00:00:00";
                break;
        }
        
        UpdateFlipDigits(timeText);
    }
    
    private void UpdateFlipDigits(string timeText)
    {
        // Format should be "HH:MM:SS"
        if (timeText.Length < 8) return;
        
        // Update hours
        hourDigits[0].Value = timeText[0] - '0';
        hourDigits[1].Value = timeText[1] - '0';
        
        // Update minutes
        minuteDigits[0].Value = timeText[3] - '0';
        minuteDigits[1].Value = timeText[4] - '0';
        
        // Update seconds
        secondDigits[0].Value = timeText[6] - '0';
        secondDigits[1].Value = timeText[7] - '0';
    }
    #endregion
}

// FlipDigit class to create the flip-clock effect
public class FlipDigit : Panel
{
    private int _value = 0;
    private Color _backColor = Color.FromArgb(40, 40, 40);
    private Color _foreColor = Color.White;
    private Color _lineColor = Color.FromArgb(30, 30, 30);
    private static readonly Font DEFAULT_FONT = new Font("Consolas", 24, FontStyle.Bold);
    
    public int Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value % 10; // Ensure it's 0-9
                this.Invalidate();
            }
        }
    }
    
    public FlipDigit()
    {
        this.DoubleBuffered = true;
    }
    
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        
        // Draw background with gradient effect
        Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);
        using (var brush = new LinearGradientBrush(
            rect, 
            Color.FromArgb(50, 50, 50),
            Color.FromArgb(30, 30, 30),
            LinearGradientMode.Vertical))
        {
            g.FillRectangle(brush, rect);
        }
        
        // Draw border
        using (var pen = new Pen(_lineColor, 1))
        {
            g.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
        }
        
        // Draw middle line
        int middle = this.Height / 2;
        using (var pen = new Pen(Color.FromArgb(20, 20, 20), 1))
        {
            g.DrawLine(pen, 0, middle, this.Width, middle);
        }
        
        // Upper half gradient (slightly lighter)
        Rectangle upperRect = new Rectangle(1, 1, this.Width - 2, middle - 1);
        using (var brush = new LinearGradientBrush(
            upperRect, 
            Color.FromArgb(60, 60, 60),
            Color.FromArgb(40, 40, 40),
            LinearGradientMode.Vertical))
        {
            g.FillRectangle(brush, upperRect);
        }
        
        // Lower half gradient (slightly darker)
        Rectangle lowerRect = new Rectangle(1, middle + 1, this.Width - 2, this.Height - middle - 2);
        using (var brush = new LinearGradientBrush(
            lowerRect, 
            Color.FromArgb(35, 35, 35),
            Color.FromArgb(25, 25, 25),
            LinearGradientMode.Vertical))
        {
            g.FillRectangle(brush, lowerRect);
        }
        
        // Определяем размер шрифта в зависимости от размера контрола
        float fontSize = Math.Min(this.Width * 0.7f, this.Height * 0.6f);
        
        // Draw digit with some shadow for 3D effect
        StringFormat format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        
        // Shadow
        using (var brush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(_value.ToString(), font, brush, 
                new RectangleF(2, 2, this.Width, this.Height), 
                format);
        }
        
        // Main digit
        using (var brush = new SolidBrush(_foreColor))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(_value.ToString(), font, brush, 
                new RectangleF(0, 0, this.Width, this.Height), 
                format);
        }
        
        // Add reflections for glass-like effect
        int reflectionHeight = this.Height / 8;
        Rectangle reflectionRect = new Rectangle(2, 2, this.Width - 4, reflectionHeight);
        using (var brush = new LinearGradientBrush(
            reflectionRect,
            Color.FromArgb(60, 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.Vertical))
        {
            g.FillRectangle(brush, reflectionRect);
        }
    }
} 