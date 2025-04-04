namespace WinTimer;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class MainForm : Form
{
    #region Constants
    private const int BASE_WIDTH = 420;
    private const int BASE_HEIGHT = 220;
    private const int BASE_BUTTON_FONT_SIZE = 14;
    private const int RESIZE_BORDER = 5;
    private const float ASPECT_RATIO = BASE_WIDTH / (float)BASE_HEIGHT;
    private static readonly Font BUTTON_FONT = new Font("Segoe UI Emoji", BASE_BUTTON_FONT_SIZE, FontStyle.Regular);
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

    // Стрелки для настройки таймера
    private Button[] upArrows;
    private Button[] downArrows;
    private bool isTimerSetupMode = false;
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
            
            // Ensure minimum size 
            if (this.Width < MinimumSize.Width)
                this.Width = MinimumSize.Width;
            if (this.Height < MinimumSize.Height)
                this.Height = MinimumSize.Height;
            
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
                pnlControls.Height = (int)(50 * scale);
                
                // Масштабируем элементы
                ScaleFlipClockDisplay(scale);
                ScaleControlPanel(scale);
                
                // Проверяем, все ли кнопки видимы
                EnsureButtonsVisible();
                
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

    private void EnsureButtonsVisible()
    {
        // Проверяем, все ли кнопки полностью видимы в панели управления
        if (btnClose.Right > pnlControls.Width || btnTopMost.Right > pnlControls.Width)
        {
            // Если кнопки выходят за границы, перепозиционируем их заново
            LayoutControlPanel(this.Width / (float)BASE_WIDTH);
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
        this.BackColor = Color.FromArgb(0, 0, 0); // Полностью черный фон
        this.ForeColor = Color.White;
        this.MinimumSize = new Size(300, 160); // Уменьшаем минимальный размер
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
            Padding = new Padding(10),
            BackColor = Color.Black // Полностью черный фон
        };
        AddHandlers(pnlTimeDisplay);
        
        // Create flip digits for HH:MM:SS
        hourDigits = new FlipDigit[2];
        minuteDigits = new FlipDigit[2]; 
        secondDigits = new FlipDigit[2];
        separators = new Label[2];
        
        // Создаем стрелки для настройки таймера
        upArrows = new Button[6]; // По стрелке для каждой цифры
        downArrows = new Button[6];
        
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
        
        // Create separators with custom painting instead of labels
        for (int i = 0; i < 2; i++)
        {
            separators[i] = new Label
            {
                Text = "",  // Пустой текст, так как будем рисовать точки вручную
                ForeColor = Color.FromArgb(255, 255, 255), // Яркий белый цвет для разделителей
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                BackColor = Color.Transparent
            };
            
            // Добавляем кастомную отрисовку разделителей
            separators[i].Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                
                // Рисуем две точки вместо двоеточия
                int diameter = (int)(separators[0].Height * 0.12f);
                int x = separators[0].Width / 2 - diameter / 2;
                int y1 = separators[0].Height / 3 - diameter / 2;
                int y2 = separators[0].Height * 2 / 3 - diameter / 2;
                
                // Тень для каждой точки
                using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                {
                    g.FillEllipse(brush, x + 1, y1 + 1, diameter, diameter);
                    g.FillEllipse(brush, x + 1, y2 + 1, diameter, diameter);
                }
                
                // Основная точка - яркий белый цвет
                using (var brush = new SolidBrush(Color.FromArgb(255, 255, 255)))
                {
                    g.FillEllipse(brush, x, y1, diameter, diameter);
                    g.FillEllipse(brush, x, y2, diameter, diameter);
                }
                
                // Подсветка точки (блик)
                using (var brush = new SolidBrush(Color.FromArgb(100, 255, 255, 255)))
                {
                    g.FillEllipse(brush, x + diameter/4, y1 + diameter/4, diameter/2, diameter/2);
                    g.FillEllipse(brush, x + diameter/4, y2 + diameter/4, diameter/2, diameter/2);
                }
            };
            
            pnlTimeDisplay.Controls.Add(separators[i]);
            AddHandlers(separators[i]);
        }
        
        // Создаем стрелки для изменения значений таймера
        CreateTimerArrows();
        
        // Position all display elements
        LayoutTimeDisplay(1.0f);
    }

    private void CreateTimerArrows()
    {
        // Создаем стрелки для увеличения значений (вверх)
        for (int i = 0; i < 6; i++)
        {
            upArrows[i] = new Button
            {
                Text = "▲",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Segoe UI Symbol", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(35, 20),
                Visible = false, // Изначально скрыты
                TabStop = false
            };
            
            upArrows[i].FlatAppearance.BorderSize = 0;
            upArrows[i].FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 60);
            
            int index = i; // Для использования в лямбда-выражении
            upArrows[i].Click += (s, e) => {
                if (isTimerSetupMode)
                {
                    IncreaseTimerDigit(index);
                }
            };
            
            pnlTimeDisplay.Controls.Add(upArrows[i]);
        }
        
        // Создаем стрелки для уменьшения значений (вниз)
        for (int i = 0; i < 6; i++)
        {
            downArrows[i] = new Button
            {
                Text = "▼",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Segoe UI Symbol", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(35, 20),
                Visible = false, // Изначально скрыты
                TabStop = false
            };
            
            downArrows[i].FlatAppearance.BorderSize = 0;
            downArrows[i].FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 60);
            
            int index = i; // Для использования в лямбда-выражении
            downArrows[i].Click += (s, e) => {
                if (isTimerSetupMode)
                {
                    DecreaseTimerDigit(index);
                }
            };
            
            pnlTimeDisplay.Controls.Add(downArrows[i]);
        }
    }

    private void IncreaseTimerDigit(int index)
    {
        // Определяем, какую цифру изменяем
        switch (index)
        {
            case 0: // Десятки часов
                if (countdownTime.Hours < 90) // Ограничение на 99 часов
                    countdownTime = countdownTime.Add(TimeSpan.FromHours(10));
                break;
            case 1: // Единицы часов
                if (countdownTime.Hours % 10 < 9)
                    countdownTime = countdownTime.Add(TimeSpan.FromHours(1));
                break;
            case 2: // Десятки минут
                if (countdownTime.Minutes < 50)
                    countdownTime = countdownTime.Add(TimeSpan.FromMinutes(10));
                break;
            case 3: // Единицы минут
                if (countdownTime.Minutes % 10 < 9)
                    countdownTime = countdownTime.Add(TimeSpan.FromMinutes(1));
                break;
            case 4: // Десятки секунд
                if (countdownTime.Seconds < 50)
                    countdownTime = countdownTime.Add(TimeSpan.FromSeconds(10));
                break;
            case 5: // Единицы секунд
                if (countdownTime.Seconds % 10 < 9)
                    countdownTime = countdownTime.Add(TimeSpan.FromSeconds(1));
                break;
        }
        
        UpdateDisplay();
    }

    private void DecreaseTimerDigit(int index)
    {
        // Определяем, какую цифру изменяем
        switch (index)
        {
            case 0: // Десятки часов
                if (countdownTime.Hours >= 10)
                    countdownTime = countdownTime.Subtract(TimeSpan.FromHours(10));
                break;
            case 1: // Единицы часов
                if (countdownTime.Hours % 10 > 0 || countdownTime.Hours > 0)
                    countdownTime = countdownTime.Subtract(TimeSpan.FromHours(1));
                break;
            case 2: // Десятки минут
                if (countdownTime.Minutes >= 10)
                    countdownTime = countdownTime.Subtract(TimeSpan.FromMinutes(10));
                break;
            case 3: // Единицы минут
                if (countdownTime.Minutes % 10 > 0 || countdownTime.Minutes > 0)
                    countdownTime = countdownTime.Subtract(TimeSpan.FromMinutes(1));
                break;
            case 4: // Десятки секунд
                if (countdownTime.Seconds >= 10)
                    countdownTime = countdownTime.Subtract(TimeSpan.FromSeconds(10));
                break;
            case 5: // Единицы секунд
                if (countdownTime.Seconds % 10 > 0 || countdownTime.Seconds > 0)
                    countdownTime = countdownTime.Subtract(TimeSpan.FromSeconds(1));
                break;
        }
        
        UpdateDisplay();
    }

    private void LayoutTimeDisplay(float scale)
    {
        if (pnlTimeDisplay.Width <= 0 || pnlTimeDisplay.Height <= 0)
        {
            // В случае, если панель еще не размещена, используем размеры формы для приблизительного расчета
            pnlTimeDisplay.Width = ClientSize.Width;
            pnlTimeDisplay.Height = ClientSize.Height - (pnlControls?.Height ?? 50);
        }
        
        // Оптимизируем размер цифр для новой ширины
        int digitWidth = (int)(60 * scale);
        int digitHeight = (int)(90 * scale);
        int separatorWidth = (int)(18 * scale);
        int totalWidth = 6 * digitWidth + 2 * separatorWidth;
        
        int startX = Math.Max(5, (pnlTimeDisplay.Width - totalWidth) / 2);
        int controlsHeight = pnlControls?.Height ?? 50;
        int y = Math.Max(5, (pnlTimeDisplay.Height - controlsHeight - digitHeight) / 2);
        
        int x = startX;
        
        // Размер стрелок
        int arrowWidth = (int)(35 * scale);
        int arrowHeight = (int)(20 * scale);
        
        // Текущий индекс для стрелок
        int arrowIndex = 0;
        
        // Position hour digits
        for (int i = 0; i < 2; i++)
        {
            hourDigits[i].Width = digitWidth;
            hourDigits[i].Height = digitHeight;
            hourDigits[i].Location = new Point(x, y);
            
            // Позиционируем стрелки над и под цифрами
            upArrows[arrowIndex].Location = new Point(x + (digitWidth - arrowWidth) / 2, y - arrowHeight - 5);
            upArrows[arrowIndex].Width = arrowWidth;
            upArrows[arrowIndex].Height = arrowHeight;
            
            downArrows[arrowIndex].Location = new Point(x + (digitWidth - arrowWidth) / 2, y + digitHeight + 5);
            downArrows[arrowIndex].Width = arrowWidth;
            downArrows[arrowIndex].Height = arrowHeight;
            
            x += digitWidth;
            arrowIndex++;
        }
        
        // First separator
        separators[0].Width = separatorWidth;
        separators[0].Height = digitHeight;
        separators[0].Location = new Point(x, y);
        x += separatorWidth;
        
        // Position minute digits
        for (int i = 0; i < 2; i++)
        {
            minuteDigits[i].Width = digitWidth;
            minuteDigits[i].Height = digitHeight;
            minuteDigits[i].Location = new Point(x, y);
            
            // Позиционируем стрелки над и под цифрами
            upArrows[arrowIndex].Location = new Point(x + (digitWidth - arrowWidth) / 2, y - arrowHeight - 5);
            upArrows[arrowIndex].Width = arrowWidth;
            upArrows[arrowIndex].Height = arrowHeight;
            
            downArrows[arrowIndex].Location = new Point(x + (digitWidth - arrowWidth) / 2, y + digitHeight + 5);
            downArrows[arrowIndex].Width = arrowWidth;
            downArrows[arrowIndex].Height = arrowHeight;
            
            x += digitWidth;
            arrowIndex++;
        }
        
        // Second separator
        separators[1].Width = separatorWidth;
        separators[1].Height = digitHeight;
        separators[1].Location = new Point(x, y);
        x += separatorWidth;
        
        // Position second digits
        for (int i = 0; i < 2; i++)
        {
            secondDigits[i].Width = digitWidth;
            secondDigits[i].Height = digitHeight;
            secondDigits[i].Location = new Point(x, y);
            
            // Позиционируем стрелки над и под цифрами
            upArrows[arrowIndex].Location = new Point(x + (digitWidth - arrowWidth) / 2, y - arrowHeight - 5);
            upArrows[arrowIndex].Width = arrowWidth;
            upArrows[arrowIndex].Height = arrowHeight;
            
            downArrows[arrowIndex].Location = new Point(x + (digitWidth - arrowWidth) / 2, y + digitHeight + 5);
            downArrows[arrowIndex].Width = arrowWidth;
            downArrows[arrowIndex].Height = arrowHeight;
            
            x += digitWidth;
            arrowIndex++;
        }
        
        // После изменения размеров принудительно перерисовываем разделители
        separators[0].Invalidate();
        separators[1].Invalidate();
    }
    
    private void LayoutControlPanel(float scale)
    {
        if (pnlControls.Width <= 0)
        {
            // В случае, если панель еще не размещена, используем ширину формы для приблизительного расчета
            pnlControls.Width = ClientSize.Width;
        }
        
        // Оптимизируем размер кнопок и расстояние между ними
        int buttonWidth = (int)(42 * scale);  // Уменьшаем с 45 для экономии места
        int buttonHeight = (int)(35 * scale); 
        int gap = (int)(8 * scale);          // Уменьшаем с 10 для экономии места
        
        int totalWidth = 8 * buttonWidth + 7 * gap;
        
        // Гарантируем, что у нас достаточно места по ширине
        int availableWidth = pnlControls.Width - 20; // 10 пикселей отступ с каждой стороны
        
        // Если кнопки не помещаются, уменьшим их размер и отступы
        if (totalWidth > availableWidth)
        {
            float reductionFactor = availableWidth / (float)totalWidth;
            buttonWidth = (int)(buttonWidth * reductionFactor);
            gap = (int)(gap * reductionFactor);
            totalWidth = 8 * buttonWidth + 7 * gap;
        }
        
        int startX = Math.Max(10, (pnlControls.Width - totalWidth) / 2);
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
        pnlTimeDisplay.Padding = new Padding((int)(5 * scale)); // Уменьшаем отступы с 10 до 5
        
        // Resize and reposition all digits and separators
        LayoutTimeDisplay(scale);
        
        // Scale the timer arrows if they exist
        if (upArrows != null && downArrows != null)
        {
            foreach (var arrow in upArrows)
            {
                if (arrow != null)
                {
                    arrow.Font = new Font("Segoe UI Symbol", 8 * scale, FontStyle.Bold);
                }
            }
            
            foreach (var arrow in downArrows)
            {
                if (arrow != null)
                {
                    arrow.Font = new Font("Segoe UI Symbol", 8 * scale, FontStyle.Bold);
                }
            }
        }
    }
    
    private void ScaleControlPanel(float scale)
    {
        // Увеличим высоту панели управления
        pnlControls.Height = (int)(50 * scale);  // Увеличено с 40
        
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
            
            // Возвращаемся в режим настройки таймера
            SetTimerSetupMode(true);
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
        
        // Скрываем стрелки настройки таймера при выходе из режима таймера
        SetTimerSetupMode(false);
        
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
        
        // Скрываем стрелки настройки таймера при выходе из режима таймера
        SetTimerSetupMode(false);
        
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
                
                // Скрываем стрелки настройки таймера в режиме часов
                if (upArrows != null && downArrows != null)
                {
                    foreach (var arrow in upArrows)
                    {
                        if (arrow != null) arrow.Visible = false;
                    }
                    
                    foreach (var arrow in downArrows)
                    {
                        if (arrow != null) arrow.Visible = false;
                    }
                }
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
                
                // В режиме секундомера стрелки тоже должны быть скрыты
                if (currentMode == Mode.Stopwatch && upArrows != null && downArrows != null)
                {
                    foreach (var arrow in upArrows)
                    {
                        if (arrow != null) arrow.Visible = false;
                    }
                    
                    foreach (var arrow in downArrows)
                    {
                        if (arrow != null) arrow.Visible = false;
                    }
                }
                break;
        }
        
        // Устанавливаем фоновые цвета для активных кнопок с легкими цветными акцентами
        btnClock.BackColor = currentMode == Mode.Clock ? 
            Color.FromArgb(35, 35, 45) : Color.FromArgb(20, 20, 20); // Синеватый оттенок для часов
        
        btnTimer.BackColor = currentMode == Mode.Timer ? 
            Color.FromArgb(40, 30, 30) : Color.FromArgb(20, 20, 20); // Красноватый оттенок для таймера
        
        btnStopwatch.BackColor = currentMode == Mode.Stopwatch ? 
            Color.FromArgb(30, 40, 30) : Color.FromArgb(20, 20, 20); // Зеленоватый оттенок для секундомера
        
        // Кнопки управления тоже должны иметь акценты когда активны
        btnStart.BackColor = btnStart.Enabled && !btnPause.Enabled ? 
            Color.FromArgb(30, 40, 30) : Color.FromArgb(20, 20, 20); // Зеленоватый оттенок
        
        btnPause.BackColor = btnPause.Enabled ? 
            Color.FromArgb(40, 35, 25) : Color.FromArgb(20, 20, 20); // Желтоватый оттенок
        
        btnReset.BackColor = Color.FromArgb(20, 20, 20);
        
        // Кнопка TopMost тоже должна выделяться, если активна
        btnTopMost.BackColor = this.TopMost ? 
            Color.FromArgb(40, 30, 40) : Color.FromArgb(20, 20, 20); // Пурпурный оттенок
        
        btnClose.BackColor = Color.FromArgb(20, 20, 20);
        
        // Подсвечиваем текст активных кнопок
        btnClock.ForeColor = currentMode == Mode.Clock ? Color.White : Color.FromArgb(200, 200, 200);
        btnTimer.ForeColor = currentMode == Mode.Timer ? Color.White : Color.FromArgb(200, 200, 200);
        btnStopwatch.ForeColor = currentMode == Mode.Stopwatch ? Color.White : Color.FromArgb(200, 200, 200);
        btnTopMost.ForeColor = this.TopMost ? Color.White : Color.FromArgb(200, 200, 200);
    }
    #endregion
    
    #region Button Event Handlers
    private void BtnTimer_Click(object sender, EventArgs e)
    {
        // Переключаемся в режим таймера
        SwitchToTimerMode();
        
        // Включаем режим настройки таймера
        SetTimerSetupMode(true);
    }
    
    private void BtnStart_Click(object sender, EventArgs e)
    {
        if (currentMode == Mode.Timer)
        {
            if (isTimerSetupMode)
            {
                // Если в режиме настройки, выключаем его и запускаем таймер
                SetTimerSetupMode(false);
            }
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
            if (!isTimerSetupMode)
            {
                // Если сбрасываем во время работы таймера, включаем режим настройки
                SetTimerSetupMode(true);
            }
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
        UpdateButtonStates();
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
    private string previousTimeText = "";
    
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
        
        // Проверяем, изменились ли цифры и запускаем анимацию только для изменившихся
        UpdateFlipDigits(timeText);
        previousTimeText = timeText;
    }
    
    private void UpdateFlipDigits(string timeText)
    {
        // Format should be "HH:MM:SS"
        if (timeText.Length < 8) return;
        
        // Обновление часов (только если изменились)
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[0] != timeText[0])
            hourDigits[0].Value = timeText[0] - '0';
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[1] != timeText[1])
            hourDigits[1].Value = timeText[1] - '0';
        
        // Обновление минут (только если изменились)
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[3] != timeText[3])
            minuteDigits[0].Value = timeText[3] - '0';
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[4] != timeText[4])
            minuteDigits[1].Value = timeText[4] - '0';
        
        // Обновление секунд (только если изменились)
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[6] != timeText[6])
            secondDigits[0].Value = timeText[6] - '0';
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[7] != timeText[7])
            secondDigits[1].Value = timeText[7] - '0';
    }
    #endregion

    // Новый метод для настройки режима установки таймера
    private void SetTimerSetupMode(bool enabled)
    {
        isTimerSetupMode = enabled;
        
        // Показываем/скрываем стрелки настройки, только если в режиме таймера
        if (currentMode == Mode.Timer && upArrows != null && downArrows != null) 
        {
            foreach (var arrow in upArrows)
            {
                if (arrow != null) arrow.Visible = enabled;
            }
            
            foreach (var arrow in downArrows)
            {
                if (arrow != null) arrow.Visible = enabled;
            }
            
            // Изменяем доступность кнопок управления
            btnStart.Enabled = enabled;
            btnPause.Enabled = !enabled;
            btnReset.Enabled = true;
            
            // Если включаем режим настройки и время не установлено, устанавливаем его в 0
            if (enabled && countdownTime == TimeSpan.Zero)
            {
                countdownTime = TimeSpan.Zero;
            }
            
            // Обновляем отображение
            UpdateDisplay();
        }
        else 
        {
            // Если не в режиме таймера, скрываем стрелки в любом случае
            if (upArrows != null && downArrows != null)
            {
                foreach (var arrow in upArrows)
                {
                    if (arrow != null) arrow.Visible = false;
                }
                
                foreach (var arrow in downArrows)
                {
                    if (arrow != null) arrow.Visible = false;
                }
            }
        }
    }

    private void InitializeControlPanel()
    {
        // Control panel at the bottom
        pnlControls = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = Color.FromArgb(15, 15, 15)
        };
        
        pnlControls.Paint += (s, e) => {
            using (var brush = new LinearGradientBrush(
                pnlControls.ClientRectangle,
                Color.FromArgb(20, 20, 20), 
                Color.FromArgb(5, 5, 5),
                LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, pnlControls.ClientRectangle);
            }
            
            using (var pen = new Pen(Color.FromArgb(30, 30, 30), 1))
            {
                e.Graphics.DrawLine(pen, 0, 0, pnlControls.Width, 0);
            }
        };
        
        AddHandlers(pnlControls);
        
        // Create all buttons with centered icons
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
        
        // Больше не используем контекстное меню для таймера
        // InitializeTimerContextMenu();
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

    #region UI Helpers
    private void ScaleButton(Button? button, float scale)
    {
        if (button == null) return;
        
        // Приведем в соответствие с размерами из LayoutControlPanel
        button.Width = (int)(42 * scale);   // Было 45
        button.Height = (int)(35 * scale);
        
        // Размер шрифта кнопок
        float fontSize = BASE_BUTTON_FONT_SIZE * scale * 1.1f;
        button.Font = new Font(button.Font.FontFamily, fontSize, FontStyle.Regular);
    }

    public Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Width = (int)(42),
            Height = (int)(35),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = Color.FromArgb(200, 200, 200),
            Margin = new Padding(0),
            Font = BUTTON_FONT,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(2, 0, 0, 1),
            FlatAppearance =
            {
                BorderSize = 0,
                MouseOverBackColor = Color.FromArgb(40, 40, 40),
                MouseDownBackColor = Color.FromArgb(60, 60, 60)
            }
        };
        
        button.Paint += (s, e) => {
            e.Graphics.Clear(button.BackColor);
            
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                
                RectangleF textRect = new RectangleF(1, -1, button.Width - 1, button.Height - 1);
                e.Graphics.DrawString(button.Text, button.Font, new SolidBrush(button.ForeColor), textRect, sf);
            }
        };
        
        button.MouseEnter += (s, e) => {
            button.ForeColor = Color.FromArgb(255, 255, 255);
            button.Invalidate();
        };
        
        button.MouseLeave += (s, e) => {
            if (button == btnClock && currentMode == Mode.Clock ||
                button == btnTimer && currentMode == Mode.Timer ||
                button == btnStopwatch && currentMode == Mode.Stopwatch ||
                button == btnTopMost && this.TopMost)
            {
                button.ForeColor = Color.White;
            }
            else
            {
                button.ForeColor = Color.FromArgb(200, 200, 200);
            }
            button.Invalidate();
        };
        
        AddHandlers(button);
        return button;
    }
    #endregion
}

// FlipDigit class to create the flip-clock effect
public class FlipDigit : Panel
{
    private int _value = 0;
    private int _previousValue = 0;
    private bool _isFlipping = false;
    private DateTime _flipStartTime;
    private const double FLIP_DURATION_MS = 150; // Было 300
    
    // Темно-черный фон
    private Color _backColor = Color.FromArgb(15, 15, 15);
    private Color _foreColor = Color.White;
    private static readonly Font DEFAULT_FONT = new Font("Segoe UI", 36, FontStyle.Bold);
    
    private Timer flipTimer;
    
    public int Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _previousValue = _value;
                _value = value % 10; // Ensure it's 0-9
                
                // Запускаем анимацию переворачивания
                _isFlipping = true;
                _flipStartTime = DateTime.Now;
                flipTimer.Start();
                
                this.Invalidate();
            }
        }
    }
    
    public FlipDigit()
    {
        this.DoubleBuffered = true;
        
        // Таймер для анимации - увеличиваем частоту обновления для более плавной анимации
        flipTimer = new Timer
        {
            Interval = 10 // ~100 FPS (было 16 - ~60 FPS)
        };
        flipTimer.Tick += (s, e) => {
            TimeSpan elapsed = DateTime.Now - _flipStartTime;
            if (elapsed.TotalMilliseconds >= FLIP_DURATION_MS)
            {
                _isFlipping = false;
                flipTimer.Stop();
            }
            this.Invalidate();
        };
    }
    
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        
        if (_isFlipping)
        {
            PaintFlippingDigit(g);
        }
        else
        {
            PaintNormalDigit(g, _value);
        }
    }
    
    private void PaintNormalDigit(Graphics g, int digit)
    {
        Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);
        
        // Скругленные углы для цифр
        GraphicsPath path = CreateRoundedRectangle(rect, 6);
        
        // Основная подложка (темный фон)
        using (var brush = new SolidBrush(_backColor))
        {
            g.FillPath(brush, path);
        }
        
        // Градиент для верхней половины цифры - более темный
        Rectangle upperHalf = new Rectangle(0, 0, this.Width, this.Height / 2);
        using (var brush = new LinearGradientBrush(
            upperHalf,
            Color.FromArgb(30, 30, 30),
            Color.FromArgb(20, 20, 20),
            LinearGradientMode.Vertical))
        {
            // Создаем путь для верхней половины со скругленными верхними углами
            GraphicsPath upperPath = new GraphicsPath();
            upperPath.AddArc(0, 0, 12, 12, 180, 90); // Верхний левый угол
            upperPath.AddArc(rect.Width - 12, 0, 12, 12, 270, 90); // Верхний правый угол
            upperPath.AddLine(rect.Width, rect.Height / 2, 0, rect.Height / 2);
            upperPath.AddLine(0, rect.Height / 2, 0, 6);
            
            g.FillPath(brush, upperPath);
        }
        
        // Градиент для нижней половины цифры
        Rectangle lowerHalf = new Rectangle(0, this.Height / 2, this.Width, this.Height / 2);
        using (var brush = new LinearGradientBrush(
            lowerHalf,
            Color.FromArgb(20, 20, 20),
            Color.FromArgb(15, 15, 15),
            LinearGradientMode.Vertical))
        {
            // Создаем путь для нижней половины со скругленными нижними углами
            GraphicsPath lowerPath = new GraphicsPath();
            lowerPath.AddLine(0, rect.Height / 2, rect.Width, rect.Height / 2);
            lowerPath.AddLine(rect.Width, rect.Height / 2, rect.Width, rect.Height - 6);
            lowerPath.AddArc(rect.Width - 12, rect.Height - 12, 12, 12, 0, 90); // Нижний правый угол
            lowerPath.AddArc(0, rect.Height - 12, 12, 12, 90, 90); // Нижний левый угол
            lowerPath.AddLine(0, rect.Height - 6, 0, rect.Height / 2);
            
            g.FillPath(brush, lowerPath);
        }
        
        // Рамка вокруг всей цифры
        using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
        {
            g.DrawPath(pen, path);
        }
        
        // Разделительная линия посередине с тенью
        using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
        {
            g.DrawLine(pen, 0, this.Height / 2, this.Width, this.Height / 2);
        }
        
        // Тень для линии
        using (var pen = new Pen(Color.FromArgb(40, 40, 40), 1))
        {
            g.DrawLine(pen, 0, this.Height / 2 + 1, this.Width, this.Height / 2 + 1);
        }
        
        // Отрисовка цифры отдельно для верхней и нижней половин
        float fontSize = Math.Min(this.Width * 0.85f, this.Height * 0.85f);
        StringFormat format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        
        // Настройка более качественного рендеринга текста
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        
        // Сохраняем состояние графики
        GraphicsState state = g.Save();
        
        // Отрисовка верхней половины цифры
        g.SetClip(upperHalf);
        
        // Тень верхней половины цифры
        using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, new RectangleF(3, 6, this.Width, this.Height), format);
        }
        
        // Сама верхняя половина цифры
        using (var brush = new SolidBrush(Color.FromArgb(255, 255, 255)))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, new RectangleF(0, 3, this.Width, this.Height), format);
        }
        
        // Восстанавливаем состояние и отрисовываем нижнюю половину
        g.Restore(state);
        g.SetClip(lowerHalf);
        
        // Тень нижней половины цифры
        using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, new RectangleF(3, 6, this.Width, this.Height), format);
        }
        
        // Сама нижняя половина цифры
        using (var brush = new SolidBrush(Color.FromArgb(255, 255, 255)))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, new RectangleF(0, 3, this.Width, this.Height), format);
        }
        
        // Сбрасываем клип
        g.ResetClip();
        
        // Создаем верхний путь для корректного клиппинга блика
        GraphicsPath upperClipPath = new GraphicsPath();
        upperClipPath.AddArc(0, 0, 12, 12, 180, 90);
        upperClipPath.AddArc(rect.Width - 12, 0, 12, 12, 270, 90);
        upperClipPath.AddLine(rect.Width, rect.Height / 2, 0, rect.Height / 2);
        upperClipPath.AddLine(0, rect.Height / 2, 0, 6);
        
        // Сохраняем состояние перед клиппингом
        state = g.Save();
        g.SetClip(upperClipPath);
        
        // Бликующая горизонтальная полоса сверху (тонкая и аккуратная)
        Rectangle reflectionRect = new Rectangle(5, 5, this.Width - 10, this.Height / 30);
        using (var brush = new LinearGradientBrush(
            reflectionRect,
            Color.FromArgb(70, 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.Vertical))
        {
            g.FillRectangle(brush, reflectionRect);
        }
        
        // Восстанавливаем состояние графики
        g.Restore(state);
        
        // Перерисовываем разделительную линию, чтобы быть уверенными, что она поверх цифр
        using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
        {
            g.DrawLine(pen, 0, this.Height / 2, this.Width, this.Height / 2);
        }
        
        // Тень для линии
        using (var pen = new Pen(Color.FromArgb(40, 40, 40), 1))
        {
            g.DrawLine(pen, 0, this.Height / 2 + 1, this.Width, this.Height / 2 + 1);
        }
    }
    
    private void PaintFlippingDigit(Graphics g)
    {
        TimeSpan elapsed = DateTime.Now - _flipStartTime;
        double progress = Math.Min(1.0, elapsed.TotalMilliseconds / FLIP_DURATION_MS);
        
        int halfHeight = this.Height / 2;
        
        // Определяем фазу анимации
        bool isFirstPhase = progress < 0.5;
        double phaseProgress = isFirstPhase ? progress * 2 : (progress - 0.5) * 2;
        
        // Угол поворота для текущей фазы (от 0 до 90 градусов)
        double angle = phaseProgress * 90.0;
        
        // Определяем видимость панелей в зависимости от фазы
        if (isFirstPhase)
        {
            // ФАЗА 1: Верхняя панель старой цифры складывается ВНИЗ
            
            // 1. Рисуем нижнюю половину СТАРОЙ цифры (неподвижна)
            Rectangle lowerRect = new Rectangle(0, halfHeight, this.Width, halfHeight);
            PaintStaticHalfDigit(g, lowerRect, _previousValue, false);
            
            // 2. Рисуем верхнюю половину НОВОЙ цифры (видна под складывающейся панелью)
            Rectangle upperNewRect = new Rectangle(0, 0, this.Width, halfHeight);
            PaintStaticHalfDigit(g, upperNewRect, _value, true);
            
            // 3. Рисуем складывающуюся ВНИЗ верхнюю половину СТАРОЙ цифры
            PaintFoldingTopDown(g, angle);
        }
        else
        {
            // ФАЗА 2: Нижняя панель НОВОЙ цифры раскладывается ВНИЗ

            // 1. Рисуем верхнюю половину НОВОЙ цифры (уже полностью видна)
            Rectangle upperRect = new Rectangle(0, 0, this.Width, halfHeight);
            PaintStaticHalfDigit(g, upperRect, _value, true);
            
            // 2. Рисуем нижнюю половину СТАРОЙ цифры (будет закрыта раскладывающейся панелью)
            Rectangle lowerOldRect = new Rectangle(0, halfHeight, this.Width, halfHeight);
            PaintStaticHalfDigit(g, lowerOldRect, _previousValue, false);
            
            // 3. Рисуем раскладывающуюся ВНИЗ нижнюю половину НОВОЙ цифры
            PaintFoldingBottomDown(g, angle);
        }
    }
    
    // Рисует нижнюю панель раскладывающуюся вниз
    private void PaintFoldingBottomDown(Graphics g, double angle)
    {
        int halfHeight = this.Height / 2;
        
        // Создаем временную битмапу для нижней половины НОВОЙ цифры
        using (Bitmap bmp = new Bitmap(this.Width, halfHeight))
        using (Graphics tempG = Graphics.FromImage(bmp))
        {
            tempG.SmoothingMode = SmoothingMode.AntiAlias;
            tempG.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            tempG.InterpolationMode = InterpolationMode.HighQualityBicubic;
            tempG.PixelOffsetMode = PixelOffsetMode.HighQuality;
            
            // Рисуем нижнюю половину НОВОЙ цифры
            Rectangle tempRect = new Rectangle(0, 0, this.Width, halfHeight);
            
            // Создаем путь для нижней половины
            GraphicsPath path = new GraphicsPath();
            path.AddLine(0, 0, tempRect.Width, 0);
            path.AddLine(tempRect.Width, 0, tempRect.Width, tempRect.Height - 6);
            path.AddArc(tempRect.Width - 12, tempRect.Height - 12, 12, 12, 0, 90); // Нижний правый угол
            path.AddArc(0, tempRect.Height - 12, 12, 12, 90, 90); // Нижний левый угол
            path.AddLine(0, tempRect.Height - 6, 0, 0);
            
            // Заполняем фон
            using (var brush = new LinearGradientBrush(
                tempRect,
                Color.FromArgb(20, 20, 20),
                Color.FromArgb(15, 15, 15),
                LinearGradientMode.Vertical))
            {
                tempG.FillPath(brush, path);
            }
            
            // Рисуем рамку
            using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
            {
                tempG.DrawPath(pen, path);
            }
            
            // Рисуем цифру
            float fontSize = Math.Min(this.Width * 0.85f, this.Height * 0.85f); // Увеличенный размер шрифта
            StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            
            // Устанавливаем клип и рисуем цифру
            tempG.SetClip(path);
            
            // Сдвиг для нижней половины цифры - добавляем смещение вниз для лучшего центрирования
            float yOffset = -halfHeight + 3; // Добавляем смещение вниз
            
            // Отрисовка тени цифры
            using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
            {
                tempG.DrawString(_value.ToString(), font, brush, 
                    new RectangleF(3, yOffset + 3, this.Width, this.Height), format);
            }
            
            // Отрисовка цифры
            using (var brush = new SolidBrush(_foreColor))
            using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
            {
                tempG.DrawString(_value.ToString(), font, brush, 
                    new RectangleF(0, yOffset, this.Width, this.Height), format);
            }
            
            // Инвертируем угол для раскладывания (0 -> полностью сложено, 90 -> полностью раскрыто)
            float scaleHeight = (float)Math.Sin(angle * Math.PI / 180.0);
            
            // Определяем точки для отрисовки раскладывающейся панели
            Point[] destPoints = new Point[3];
            
            // Создаем трансформацию, где панель прикреплена сверху и раскладывается вниз
            destPoints[0] = new Point(0, halfHeight); // Левый верхний угол закреплен на середине
            destPoints[1] = new Point(this.Width, halfHeight); // Правый верхний угол закреплен на середине
            destPoints[2] = new Point(0, halfHeight + (int)(halfHeight * scaleHeight)); // Левый нижний угол движется вниз по мере раскладывания
            
            // Рисуем трансформированное изображение
            g.DrawImage(bmp, destPoints);
            
            // Добавляем затенение для 3D-эффекта (меньше затенения по мере раскладывания)
            int shadowAlpha = (int)(150 * (1.0 - Math.Sin(angle * Math.PI / 180.0)));
            using (var brush = new SolidBrush(Color.FromArgb(shadowAlpha, 0, 0, 0)))
            {
                g.FillPolygon(brush, destPoints);
            }
            
            // Добавляем линию на нижнем крае раскладывающейся панели
            using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
            {
                g.DrawLine(pen, 
                    0, halfHeight + (int)(halfHeight * scaleHeight),
                    this.Width, halfHeight + (int)(halfHeight * scaleHeight));
            }
        }
    }
    
    // Рисует статичную половину цифры (верхнюю или нижнюю)
    private void PaintStaticHalfDigit(Graphics g, Rectangle rect, int digit, bool isTopHalf)
    {
        // Создаем путь со скругленными углами для половинки
        GraphicsPath path = new GraphicsPath();
        
        if (isTopHalf)
        {
            // Верхняя половина цифры со скругленными верхними углами
            path.AddArc(rect.X, rect.Y, 12, 12, 180, 90); // Верхний левый угол
            path.AddArc(rect.X + rect.Width - 12, rect.Y, 12, 12, 270, 90); // Верхний правый угол
            path.AddLine(rect.X + rect.Width, rect.Y + rect.Height, rect.X, rect.Y + rect.Height);
            path.AddLine(rect.X, rect.Y + rect.Height, rect.X, rect.Y + 6);
        }
        else
        {
            // Нижняя половина цифры со скругленными нижними углами
            path.AddLine(rect.X, rect.Y, rect.X + rect.Width, rect.Y);
            path.AddLine(rect.X + rect.Width, rect.Y, rect.X + rect.Width, rect.Y + rect.Height - 6);
            path.AddArc(rect.X + rect.Width - 12, rect.Y + rect.Height - 12, 12, 12, 0, 90); // Нижний правый угол
            path.AddArc(rect.X, rect.Y + rect.Height - 12, 12, 12, 90, 90); // Нижний левый угол
            path.AddLine(rect.X, rect.Y + rect.Height - 6, rect.X, rect.Y);
        }
        
        // Фон для половины с градиентом
        using (var brush = new LinearGradientBrush(
            rect,
            isTopHalf ? Color.FromArgb(30, 30, 30) : Color.FromArgb(20, 20, 20),
            isTopHalf ? Color.FromArgb(20, 20, 20) : Color.FromArgb(15, 15, 15),
            LinearGradientMode.Vertical))
        {
            g.FillPath(brush, path);
        }
        
        // Рамка
        using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
        {
            g.DrawPath(pen, path);
        }
        
        // Отрисовка цифры с клиппингом для ограничения внутри половины
        float fontSize = Math.Min(this.Width * 0.85f, this.Height * 0.85f);
        
        // Сохраняем состояние графики и устанавливаем клип
        GraphicsState state = g.Save();
        g.SetClip(path);
        
        // Полный прямоугольник для текста
        RectangleF fullRect = new RectangleF(0, 3, this.Width, this.Height); // Сдвигаем вниз текст
        
        // Форматирование текста
        StringFormat format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        
        // Настройка более качественного рендеринга текста
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        
        // Отрисовка тени цифры
        using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, new RectangleF(3, 6, this.Width, this.Height), format);
        }
        
        // Отрисовка цифры
        using (var brush = new SolidBrush(_foreColor))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, fullRect, format);
        }
        
        g.Restore(state);
        
        // Добавляем блик для верхней половины
        if (isTopHalf)
        {
            Rectangle reflectionRect = new Rectangle(rect.X + 5, rect.Y + 5, rect.Width - 10, rect.Height / 15);
            using (var brush = new LinearGradientBrush(
                reflectionRect,
                Color.FromArgb(70, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                LinearGradientMode.Vertical))
            {
                g.SetClip(path);
                g.FillRectangle(brush, reflectionRect);
                g.ResetClip();
            }
        }
        
        // Разделительная линия для верхней половины
        if (isTopHalf)
        {
            using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
            {
                g.DrawLine(pen, rect.X, rect.Y + rect.Height, rect.X + rect.Width, rect.Y + rect.Height);
            }
            
            // Тень для линии
            using (var pen = new Pen(Color.FromArgb(40, 40, 40), 1))
            {
                g.DrawLine(pen, rect.X, rect.Y + rect.Height + 1, rect.X + rect.Width, rect.Y + rect.Height + 1);
            }
        }
    }
    
    // Рисует верхнюю панель складывающуюся вниз
    private void PaintFoldingTopDown(Graphics g, double angle)
    {
        int halfHeight = this.Height / 2;
        
        // Создаем временную битмапу для верхней половины старой цифры
        using (Bitmap bmp = new Bitmap(this.Width, halfHeight))
        using (Graphics tempG = Graphics.FromImage(bmp))
        {
            tempG.SmoothingMode = SmoothingMode.AntiAlias;
            tempG.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            tempG.InterpolationMode = InterpolationMode.HighQualityBicubic;
            tempG.PixelOffsetMode = PixelOffsetMode.HighQuality;
            
            // Рисуем верхнюю половину СТАРОЙ цифры
            Rectangle tempRect = new Rectangle(0, 0, this.Width, halfHeight);
            
            // Создаем путь для верхней половины
            GraphicsPath path = new GraphicsPath();
            path.AddArc(0, 0, 12, 12, 180, 90); // Верхний левый угол
            path.AddArc(tempRect.Width - 12, 0, 12, 12, 270, 90); // Верхний правый угол
            path.AddLine(tempRect.Width, tempRect.Height, 0, tempRect.Height);
            path.AddLine(0, tempRect.Height, 0, 6);
            
            // Заполняем фон
            using (var brush = new LinearGradientBrush(
                tempRect,
                Color.FromArgb(30, 30, 30),
                Color.FromArgb(20, 20, 20),
                LinearGradientMode.Vertical))
            {
                tempG.FillPath(brush, path);
            }
            
            // Рисуем рамку
            using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
            {
                tempG.DrawPath(pen, path);
            }
            
            // Рисуем цифру
            float fontSize = Math.Min(this.Width * 0.85f, this.Height * 0.85f); // Увеличенный размер шрифта
            StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            
            // Устанавливаем клип и рисуем цифру
            tempG.SetClip(path);
            
            // Отрисовка тени цифры - сдвигаем вниз
            using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
            {
                tempG.DrawString(_previousValue.ToString(), font, brush, 
                    new RectangleF(3, 6, this.Width, this.Height), format);
            }
            
            // Отрисовка цифры - сдвигаем вниз
            using (var brush = new SolidBrush(_foreColor))
            using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
            {
                tempG.DrawString(_previousValue.ToString(), font, brush, 
                    new RectangleF(0, 3, this.Width, this.Height), format);
            }
            
            // Добавляем блик - идентичный блику в статичной верхней половине
            Rectangle reflectionRect = new Rectangle(5, 5, tempRect.Width - 10, tempRect.Height / 15);
            using (var brush = new LinearGradientBrush(
                reflectionRect,
                Color.FromArgb(70, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                LinearGradientMode.Vertical))
            {
                tempG.FillRectangle(brush, reflectionRect);
            }
            
            // Вычисляем "сжатие" по вертикали из-за поворота
            float scaleHeight = (float)Math.Cos(angle * Math.PI / 180.0);
            
            // Создаем точки для отрисовки сложенной панели - КЛЮЧЕВОЕ ИЗМЕНЕНИЕ
            Point[] destPoints = new Point[3];
            
            // Верхние углы двигаются вниз (сложение идет сверху)
            destPoints[0] = new Point(0, (int)(halfHeight * (1 - scaleHeight))); // Левый верхний угол - двигается вниз
            destPoints[1] = new Point(this.Width, (int)(halfHeight * (1 - scaleHeight))); // Правый верхний угол - двигается вниз
            destPoints[2] = new Point(0, halfHeight); // Левый нижний угол - остается неподвижным
            
            // Рисуем трансформированное изображение
            g.DrawImage(bmp, destPoints);
            
            // Добавляем затенение в зависимости от угла поворота
            int shadowAlpha = (int)(150 * (1.0 - Math.Cos(angle * Math.PI / 180.0)));
            using (var brush = new SolidBrush(Color.FromArgb(shadowAlpha, 0, 0, 0)))
            {
                g.FillPolygon(brush, destPoints);
            }
            
            // Добавляем линию сверху складывающейся панели
            using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
            {
                g.DrawLine(pen, 
                    0, (int)(halfHeight * (1 - scaleHeight)),
                    this.Width, (int)(halfHeight * (1 - scaleHeight)));
            }
        }
    }
    
    // Вспомогательный метод для создания скругленного прямоугольника
    private GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        
        // Верхний левый угол
        path.AddArc(bounds.X, bounds.Y, radius * 2, radius * 2, 180, 90);
        
        // Верхняя сторона и верхний правый угол
        path.AddArc(bounds.X + bounds.Width - radius * 2, bounds.Y, radius * 2, radius * 2, 270, 90);
        
        // Правая сторона и нижний правый угол
        path.AddArc(bounds.X + bounds.Width - radius * 2, bounds.Y + bounds.Height - radius * 2, radius * 2, radius * 2, 0, 90);
        
        // Нижняя сторона и нижний левый угол
        path.AddArc(bounds.X, bounds.Y + bounds.Height - radius * 2, radius * 2, radius * 2, 90, 90);
        
        // Закрываем путь
        path.CloseFigure();
        
        return path;
    }
} 