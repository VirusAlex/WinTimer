namespace WinTimer;

using System;
using System.Drawing;
using System.Windows.Forms;

public class MainForm : Form
{
    private Timer timerClock;
    private Timer timerCountdown;
    private Timer timerStopwatch;
    
    private Label lblTime;
    private Button btnTimer;
    private Button btnStopwatch;
    private Button btnTopMost;
    private Button btnClock;
    private Button btnClose;
    
    private Panel pnlMainControls;
    private Panel pnlTimerControls;
    private Panel pnlStopwatchControls;
    
    private Button btnStartTimer;
    private Button btnPauseTimer;
    private Button btnResetTimer;
    
    private Button btnStartStopwatch;
    private Button btnPauseStopwatch;
    private Button btnResetStopwatch;
    
    private TimeSpan countdownTime = TimeSpan.Zero;
    private TimeSpan stopwatchTime = TimeSpan.Zero;
    
    // Базовые размеры для масштабирования
    private const int BASE_WIDTH = 250;
    private const int BASE_HEIGHT = 150;
    private const int BASE_FONT_SIZE = 25;
    private const int BASE_BUTTON_FONT_SIZE = 14;
    private const int RESIZE_BORDER = 5;
    
    
    // Общий стиль для всех кнопок
    private static readonly Font BUTTON_FONT = new Font("Segoe UI", BASE_BUTTON_FONT_SIZE, FontStyle.Regular);

    private const float ASPECT_RATIO = BASE_WIDTH / (float)BASE_HEIGHT;
    private bool isResizing = false;
    
    private enum Mode
    {
        Clock,
        Timer,
        Stopwatch
    }
    
    private Mode currentMode = Mode.Clock;
    
    // Переменные для перетаскивания окна
    private bool isDragging = false;
    private Point dragStartPoint;
    
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
    
    public MainForm()
    {
        InitializeComponent();
        InitializeTimers();
        UpdateDisplay();
        
        // Добавляем обработчик изменения размера
        this.Resize += MainForm_Resize;
        
        // Устанавливаем иконку приложения
        this.Icon = AppIcon.GetAppIcon();
    }
    
    private void InitializeComponent()
    {
        // Настройка формы
        this.Text = "WinTimer";
        this.Size = new Size(BASE_WIDTH, BASE_HEIGHT);
        this.FormBorderStyle = FormBorderStyle.None;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(0, 0, 0);
        this.ForeColor = Color.White;
        this.MinimumSize = new Size(200, 120);
        this.Padding = new Padding(RESIZE_BORDER);

        // Добавляем обработчик для перетаскивания окна
        AddHandlers(this);
        
        // Создание элементов управления
        lblTime = new Label
        {
            Text = "00:00:00",
            Font = new Font("Segoe UI", BASE_FONT_SIZE, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = (int)(50),
            ForeColor = Color.White
        };
        AddHandlers(lblTime);
        
        pnlMainControls = new Panel
        {
            Dock = DockStyle.Top,
            Height = (int)(40),
            Padding = new Padding(1)
        };
        AddHandlers(pnlMainControls);
        
        btnTimer = CreateButton("⏱️");
        btnStopwatch = CreateButton("⏲️");
        btnClock = CreateButton("🕒");
        btnClock.Visible = false;
        btnTopMost = CreateButton("📌");
        
        btnClose = CreateButton("✖");
        btnClose.Click += (s, e) => {
            this.Close();
        };
        
        // Добавление элементов на форму
        pnlMainControls.Controls.Add(btnTimer);
        pnlMainControls.Controls.Add(btnStopwatch);
        pnlMainControls.Controls.Add(btnClock);
        pnlMainControls.Controls.Add(btnTopMost);
        pnlMainControls.Controls.Add(btnClose);
        
        // Расположение кнопок в панели
        btnTimer.Location = new Point((int)(10), (int)(5));
        btnStopwatch.Location = new Point((int)(60), (int)(5));
        btnClock.Location = new Point((int)(110), (int)(5));
        btnTopMost.Location = new Point((int)(155), (int)(5));
        btnClose.Location = new Point((int)(200), (int)(5));
        
        // Панель для контролов таймера и секундомера
        pnlTimerControls = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = (int)(40),
            Visible = false
        };
        AddHandlers(pnlTimerControls);
        
        pnlStopwatchControls = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = (int)(40),
            Visible = false
        };
        AddHandlers(pnlStopwatchControls);
        
        // Кнопки управления с тем же шрифтом
        btnStartTimer = CreateButton("▶️");
        btnPauseTimer = CreateButton("⏸️");
        btnResetTimer = CreateButton("🔄");
        
        // Добавление кнопок управления на панели
        pnlTimerControls.Controls.Add(btnStartTimer);
        pnlTimerControls.Controls.Add(btnPauseTimer);
        pnlTimerControls.Controls.Add(btnResetTimer);
        
        // Расположение кнопок управления
        btnStartTimer.Location = new Point((int)(50), (int)(5));
        btnPauseTimer.Location = new Point((int)(100), (int)(5));
        btnResetTimer.Location = new Point((int)(150), (int)(5));
        
        // Создаем отдельные кнопки для секундомера с тем же шрифтом
        btnStartStopwatch = CreateButton("▶️");
        btnPauseStopwatch = CreateButton("⏸️");
        btnResetStopwatch = CreateButton("🔄");
        
        pnlStopwatchControls.Controls.Add(btnStartStopwatch);
        pnlStopwatchControls.Controls.Add(btnPauseStopwatch);
        pnlStopwatchControls.Controls.Add(btnResetStopwatch);
        
        // Расположение кнопок управления
        btnStartStopwatch.Location = new Point((int)(50), (int)(5));
        btnPauseStopwatch.Location = new Point((int)(100), (int)(5));
        btnResetStopwatch.Location = new Point((int)(150), (int)(5));
        
        // Добавление панелей на форму
        this.Controls.Add(pnlTimerControls);
        this.Controls.Add(pnlStopwatchControls);
        this.Controls.Add(pnlMainControls);
        this.Controls.Add(lblTime);
        
        // Привязка обработчиков событий
        btnTimer.Click += BtnTimer_Click;
        btnStopwatch.Click += BtnStopwatch_Click;
        btnClock.Click += BtnClock_Click;
        btnTopMost.Click += BtnTopMost_Click;
        btnStartTimer.Click += BtnStartTimerClick;
        btnPauseTimer.Click += BtnPauseTimerClick;
        btnResetTimer.Click += BtnResetTimerClick;
        
        btnStartStopwatch.Click += (s, e) => {
            timerStopwatch?.Start();
            btnStartStopwatch.Enabled = false;
            btnPauseStopwatch.Enabled = true;
        };
        
        btnPauseStopwatch.Click += (s, e) => {
            timerStopwatch?.Stop();
            btnStartStopwatch.Enabled = true;
            btnPauseStopwatch.Enabled = false;
        };
        
        btnResetStopwatch.Click += (s, e) => {
            timerStopwatch?.Stop();
            stopwatchTime = TimeSpan.Zero;
            btnStartStopwatch.Enabled = true;
            btnPauseStopwatch.Enabled = false;
            UpdateDisplay();
        };
        
        // Создание контекстного меню для таймера
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
        // Таймер для часов
        timerClock = new Timer
        {
            Interval = 1000
        };
        timerClock.Tick += TimerClock_Tick;
        timerClock.Start();
        
        // Таймер для обратного отсчета
        timerCountdown = new Timer
        {
            Interval = 1000
        };
        timerCountdown.Tick += TimerCountdown_Tick;
        
        // Таймер для секундомера
        timerStopwatch = new Timer
        {
            Interval = 1000
        };
        timerStopwatch.Tick += TimerStopwatch_Tick;
    }
    
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
            btnStartTimer!.Enabled = true;
            btnPauseTimer!.Enabled = false;
            
            UpdateDisplay();
            
            // Уведомление о завершении таймера
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
    
    private void UpdateDisplay()
    {
        switch (currentMode)
        {
            case Mode.Clock:
                lblTime.Text = DateTime.Now.ToString("HH:mm:ss");
                break;
            case Mode.Timer:
                lblTime.Text = countdownTime.ToString(@"hh\:mm\:ss");
                break;
            case Mode.Stopwatch:
                lblTime.Text = stopwatchTime.ToString(@"hh\:mm\:ss");
                break;
        }
    }
    
    private void BtnTimer_Click(object sender, EventArgs e)
    {
        btnTimer?.ContextMenuStrip?.Show(btnTimer, new Point(0, btnTimer.Height));
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
    
    private void BtnClock_Click(object? sender, EventArgs e)
    {
        SwitchToClockMode();
    }
    
    private void SwitchToClockMode()
    {
        currentMode = Mode.Clock;
        timerCountdown?.Stop();
        timerStopwatch?.Stop();
        timerClock?.Start();
        
        pnlTimerControls!.Visible = false;
        pnlStopwatchControls!.Visible = false;
        
        btnClock!.Visible = false;
        btnTimer!.Visible = true;
        btnStopwatch!.Visible = true;
        btnTopMost!.Visible = true;
        
        UpdateDisplay();
    }
    
    private void SwitchToTimerMode()
    {
        currentMode = Mode.Timer;
        timerClock?.Stop();
        timerStopwatch?.Stop();
        
        pnlTimerControls!.Visible = true;
        pnlStopwatchControls!.Visible = false;
        
        btnClock!.Visible = true;
        btnTimer!.Visible = false;
        btnStopwatch!.Visible = false;
        btnTopMost!.Visible = true;
        
        btnStartTimer!.Enabled = true;
        btnPauseTimer!.Enabled = false;
        btnResetTimer!.Enabled = true;
        
        UpdateDisplay();
    }
    
    private void BtnStopwatch_Click(object? sender, EventArgs e)
    {
        currentMode = Mode.Stopwatch;
        timerClock?.Stop();
        timerCountdown?.Stop();
        
        pnlTimerControls!.Visible = false;
        pnlStopwatchControls!.Visible = true;
        
        btnClock!.Visible = true;
        btnTimer!.Visible = false;
        btnStopwatch!.Visible = false;
        btnTopMost!.Visible = true;
        
        btnStartStopwatch!.Enabled = true;
        btnPauseStopwatch!.Enabled = false;
        btnResetStopwatch!.Enabled = true;
        
        UpdateDisplay();
    }
    
    private void BtnTopMost_Click(object sender, EventArgs e)
    {
        this.TopMost = !this.TopMost;
        btnTopMost.BackColor = this.TopMost ? Color.FromArgb(100, 100, 100) : Color.FromArgb(50, 50, 50);
    }
    
    private void BtnStartTimerClick(object sender, EventArgs e)
    {
        if (currentMode == Mode.Timer)
        {
            timerCountdown?.Start();
        }
        else if (currentMode == Mode.Stopwatch)
        {
            timerStopwatch?.Start();
        }
        
        btnStartTimer!.Enabled = false;
        btnPauseTimer!.Enabled = true;
    }
    
    private void BtnPauseTimerClick(object sender, EventArgs e)
    {
        if (currentMode == Mode.Timer)
        {
            timerCountdown?.Stop();
        }
        else if (currentMode == Mode.Stopwatch)
        {
            timerStopwatch?.Stop();
        }
        
        btnStartTimer!.Enabled = true;
        btnPauseTimer!.Enabled = false;
    }
    
    private void BtnResetTimerClick(object sender, EventArgs e)
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
        
        btnStartTimer!.Enabled = true;
        btnPauseTimer!.Enabled = false;
        UpdateDisplay();
    }
    
    // Метод для мигания окна при завершении таймера
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
    
    // Изменяем метод MainForm_MouseDown для работы с любым контролом
    private void MainForm_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            isDragging = true;
            // Получаем координаты относительно формы
            Point screenPoint = (sender as Control)?.PointToScreen(new Point(e.X, e.Y)) ?? Point.Empty;
            dragStartPoint = PointToClient(screenPoint);
        }
    }
    
    // Изменяем метод MainForm_MouseMove для работы с любым контролом
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
        if (lblTime == null || isResizing) return;
        
        try
        {
            isResizing = true;
            
            // Сохраняем пропорции
            float currentRatio = this.Width / (float)this.Height;
            
            if (Math.Abs(currentRatio - ASPECT_RATIO) > 0.01f) // Допуск на погрешность
            {
                // Определяем, какая сторона изменилась последней
                if (this.Width != this.RestoreBounds.Width)
                {
                    // Изменилась ширина - подстраиваем высоту
                    this.Height = (int)(this.Width / ASPECT_RATIO);
                }
                else
                {
                    // Изменилась высота - подстраиваем ширину
                    this.Width = (int)(this.Height * ASPECT_RATIO);
                }
            }
            
            // Вычисляем масштаб
            float scale = this.Width / (float)BASE_WIDTH;
            
            // Масштабируем шрифт метки времени
            lblTime.Font = new Font(lblTime.Font.FontFamily, BASE_FONT_SIZE * scale, FontStyle.Bold);
            lblTime.Height = (int)(50 * scale);
            
            // Масштабируем все кнопки
            ScaleButton(btnTimer, scale);
            ScaleButton(btnStopwatch, scale);
            ScaleButton(btnClock, scale);
            ScaleButton(btnTopMost, scale);
            ScaleButton(btnClose, scale);
            
            ScaleButton(btnStartTimer, scale);
            ScaleButton(btnPauseTimer, scale);
            ScaleButton(btnResetTimer, scale);
            
            ScaleButton(btnStartStopwatch, scale);
            ScaleButton(btnPauseStopwatch, scale);
            ScaleButton(btnResetStopwatch, scale);
            
            // Обновляем размеры панелей
            pnlMainControls.Height = (int)(40 * scale);
            UpdateButtonPositions(scale);

            pnlTimerControls.Height = (int)(40 * scale);
            UpdateTimerControlsPositions(scale);

            pnlStopwatchControls.Height = (int)(40 * scale);
            UpdateStopwatchControlsPositions(scale);
        }
        finally
        {
            isResizing = false;
        }
    }
    
    private void ScaleButton(Button? button, float scale)
    {
        if (button == null) return;
        
        button.Width = (int)(40 * scale);
        button.Height = (int)(30 * scale);
        button.Font = new Font(button.Font.FontFamily, BASE_BUTTON_FONT_SIZE * scale, FontStyle.Regular);
    }
    
    private void UpdateButtonPositions(float scale)
    {
        btnTimer.Location = new Point((int)(10 * scale), (int)(5 * scale));
        btnStopwatch.Location = new Point((int)(60 * scale), (int)(5 * scale));
        btnClock.Location = new Point((int)(110 * scale), (int)(5 * scale));
        btnTopMost.Location = new Point((int)(155 * scale), (int)(5 * scale));
        btnClose.Location = new Point((int)(200 * scale), (int)(5 * scale));
    }
    
    private void UpdateTimerControlsPositions(float scale)
    {
        btnStartTimer.Location = new Point((int)(50 * scale), (int)(5 * scale));
        btnPauseTimer.Location = new Point((int)(100 * scale), (int)(5 * scale));
        btnResetTimer.Location = new Point((int)(150 * scale), (int)(5 * scale));
    }
    
    private void UpdateStopwatchControlsPositions(float scale)
    {
        btnStartStopwatch.Location = new Point((int)(50 * scale), (int)(5 * scale));
        btnPauseStopwatch.Location = new Point((int)(100 * scale), (int)(5 * scale));
        btnResetStopwatch.Location = new Point((int)(150 * scale), (int)(5 * scale));
    }

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

    public void AddHandlers(Control control)
    {
        control.MouseDown += MainForm_MouseDown;
        control.MouseMove += MainForm_MouseMove;
        control.MouseUp += MainForm_MouseUp;
    }
} 