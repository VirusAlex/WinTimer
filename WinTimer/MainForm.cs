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

    // Arrows for timer setup
    private Button[] upArrows;
    private Button[] downArrows;
    private bool isTimerSetupMode = false;
    #endregion

    #region State Variables
    private bool isResizing = false;
    private Mode currentMode = Mode.Clock;
    private TimeSpan countdownTime = TimeSpan.Zero;
    // Add a dedicated field to track hours for timer mode without day normalization
    private int timerDisplayHours = 0;
    private int timerDisplayMinutes = 0;
    private int timerDisplaySeconds = 0;
    private TimeSpan stopwatchTime = TimeSpan.Zero;
    
    // Store a reference to the notification window for possible closing
    private Form? notificationForm = null;
    
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
                // Update panel sizes
                pnlControls.Height = (int)(50 * scale);
                
                // Scale elements
                ScaleFlipClockDisplay(scale);
                ScaleControlPanel(scale);
                
                // Check if all buttons are visible
                EnsureButtonsVisible();
                
                // Update form placement
                this.Controls.Remove(pnlControls);
                this.Controls.Remove(pnlTimeDisplay);
                
                this.Controls.Add(pnlControls);
                this.Controls.Add(pnlTimeDisplay);
                
                // Update time display
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
        // Check if all buttons are fully visible in the control panel
        if (btnClose.Right > pnlControls.Width || btnTopMost.Right > pnlControls.Width)
        {
            // If buttons extend beyond the boundaries, reposition them
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
        // Start flashing in a separate thread
        Thread flashThread = new Thread(() => {
            for (int i = 0; i < 5; i++)
            {
                BeginInvoke(() =>
                {
                    BackColor = Color.Red;
                    Application.DoEvents();
                });
                Thread.Sleep(300);
                
                BeginInvoke(() => {
                    BackColor = Color.FromArgb(0, 0, 0);
                    Application.DoEvents();
                });
                Thread.Sleep(200);
            }
        });
        
        flashThread.Start();
    }

    // New method for showing a custom notification
    private void ShowTimerCompleteNotification()
    {
        // Close any existing notification window
        if (notificationForm != null && !notificationForm.IsDisposed)
        {
            notificationForm.Close();
        }

        // Create a custom notification window
        notificationForm = new Form
        {
            Text = "",
            Size = new Size(300, 150),
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.FromArgb(25, 25, 25),
            ForeColor = Color.Black,
            ShowInTaskbar = false,
            TopMost = true,
            Opacity = 0.95
        };
        
        // Create elements of the window
        Label titleLabel = new Label
        {
            Text = "Timer is up!",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(0, 15, 0, 0)
        };
        
        Button okButton = new Button
        {
            Text = "OK",
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 40, 45),
            ForeColor = Color.White,
            Size = new Size(80, 35),
            TabIndex = 0, // Focus on this button
        };
        
        okButton.FlatAppearance.BorderSize = 0;
        okButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 65);
        
        // Position the button in the center
        okButton.Location = new Point((notificationForm.ClientSize.Width - okButton.Width) / 2, 
                                     notificationForm.ClientSize.Height - okButton.Height);
        
        // Add event handlers
        okButton.Click += (s, e) => {
            notificationForm.Close();
            notificationForm = null;
        };
        
        notificationForm.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space || e.KeyCode == Keys.Escape)
            {
                notificationForm.Close();
                notificationForm = null;
            }
        };
        
        notificationForm.FormClosed += (s, e) => {
            notificationForm = null;
        };
        
        // Add elements to the form
        notificationForm.Controls.Add(okButton);
        notificationForm.Controls.Add(titleLabel);
        
        // Show the form
        notificationForm.Show(this);
        
        // Start flashing the notification window in a separate thread
        Thread flashNotificationThread = new Thread(() => {
            Color originalBackColor = notificationForm.BackColor;
            for (int i = 0; i < 5; i++)
            {
                // Sometimes the window may already be closed
                if (notificationForm.IsDisposed) break;
                
                BeginInvoke(() => {
                    if (!notificationForm.IsDisposed)
                    {
                        notificationForm.BackColor = Color.Red;
                        titleLabel.BackColor = Color.Red;
                        Application.DoEvents();
                    }
                });
                Thread.Sleep(300);
                
                BeginInvoke(() => {
                    if (!notificationForm.IsDisposed)
                    {
                        notificationForm.BackColor = originalBackColor;
                        titleLabel.BackColor = originalBackColor;
                        Application.DoEvents();
                    }
                });
                Thread.Sleep(200);
            }
        });
        
        flashNotificationThread.Start();
        
        // Play the system notification sound
        try
        {
            System.Media.SystemSounds.Exclamation.Play();
        }
        catch
        {
            // Ignore errors when playing the sound
        }
    }

    // Method for closing the notification window
    private void CloseNotificationIfOpen()
    {
        if (notificationForm != null && !notificationForm.IsDisposed)
        {
            notificationForm.Close();
            notificationForm = null;
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
        this.BackColor = Color.FromArgb(0, 0, 0); // Fully black background
        this.ForeColor = Color.White;
        this.MinimumSize = new Size(300, 160); // Decrease minimum size
        this.Padding = new Padding(RESIZE_BORDER);

        // Add handlers for window dragging
        AddHandlers(this);
        
        // Initialize the control panel first, then the time display
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
            BackColor = Color.Black // Fully black background
        };
        AddHandlers(pnlTimeDisplay);
        
        // Create flip digits for HH:MM:SS
        hourDigits = new FlipDigit[2];
        minuteDigits = new FlipDigit[2]; 
        secondDigits = new FlipDigit[2];
        separators = new Label[2];
        
        // Create arrows for timer setup
        upArrows = new Button[6]; // One arrow for each digit
        downArrows = new Button[6];
        
        // Create individual digits
        for (int i = 0; i < 2; i++)
        {
            hourDigits[i] = new FlipDigit();
            AddHandlers(hourDigits[i]);
            minuteDigits[i] = new FlipDigit();
            AddHandlers(minuteDigits[i]);
            secondDigits[i] = new FlipDigit();
            AddHandlers(secondDigits[i]);
            
            pnlTimeDisplay.Controls.Add(hourDigits[i]);
            pnlTimeDisplay.Controls.Add(minuteDigits[i]);
            pnlTimeDisplay.Controls.Add(secondDigits[i]);
        }
        
        // Create separators with custom painting instead of labels
        for (int i = 0; i < 2; i++)
        {
            separators[i] = new Label
            {
                Text = "",  // Empty text, since we'll draw points manually
                ForeColor = Color.FromArgb(255, 255, 255), // Bright white color for separators
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                BackColor = Color.Transparent
            };
            
            // Add custom drawing of separators
            separators[i].Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                
                // Draw two points instead of a colon
                int diameter = (int)(separators[0].Height * 0.12f);
                int x = separators[0].Width / 2 - diameter / 2;
                int y1 = separators[0].Height / 3 - diameter / 2;
                int y2 = separators[0].Height * 2 / 3 - diameter / 2;
                
                // Shadow for each point
                using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                {
                    g.FillEllipse(brush, x + 1, y1 + 1, diameter, diameter);
                    g.FillEllipse(brush, x + 1, y2 + 1, diameter, diameter);
                }
                
                // Main point - bright white color
                using (var brush = new SolidBrush(Color.FromArgb(255, 255, 255)))
                {
                    g.FillEllipse(brush, x, y1, diameter, diameter);
                    g.FillEllipse(brush, x, y2, diameter, diameter);
                }
                
                // Highlight point (glow)
                using (var brush = new SolidBrush(Color.FromArgb(100, 255, 255, 255)))
                {
                    g.FillEllipse(brush, x + diameter/4, y1 + diameter/4, diameter/2, diameter/2);
                    g.FillEllipse(brush, x + diameter/4, y2 + diameter/4, diameter/2, diameter/2);
                }
            };
            
            pnlTimeDisplay.Controls.Add(separators[i]);
            AddHandlers(separators[i]);
        }
        
        // Create arrows for changing timer values
        CreateTimerArrows();
        
        // Position all display elements
        LayoutTimeDisplay(1.0f);
    }

    private void CreateTimerArrows()
    {
        // Create arrows for increasing values (up)
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
                Visible = false, // Initially hidden
                TabStop = false
            };
            
            upArrows[i].FlatAppearance.BorderSize = 0;
            upArrows[i].FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 60);
            
            int index = i; // For use in lambda expression
            upArrows[i].Click += (s, e) => {
                if (isTimerSetupMode)
                {
                    IncreaseTimerDigit(index);
                }
            };
            
            pnlTimeDisplay.Controls.Add(upArrows[i]);
        }
        
        // Create arrows for decreasing values (down)
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
                Visible = false, // Initially hidden
                TabStop = false
            };
            
            downArrows[i].FlatAppearance.BorderSize = 0;
            downArrows[i].FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 60);
            
            int index = i; // For use in lambda expression
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
        // Determine which digit to change
        switch (index)
        {
            case 0: // Tens of hours
                // Add 10 hours directly
                if (timerDisplayHours < 90) // Limit to 99 hours
                    timerDisplayHours += 10;
                else
                    timerDisplayHours -= 90; // Reset to 0 from 90
                break;
            case 1: // Units of hours
                // Add 1 hour directly
                if (timerDisplayHours % 10 < 9)
                    timerDisplayHours += 1;
                else
                    timerDisplayHours -= 9; // Reset units digit to 0
                break;
            case 2: // Tens of minutes
                if (timerDisplayMinutes < 50)
                    timerDisplayMinutes += 10;
                else
                    timerDisplayMinutes -= 50; // Reset to 0 from 50
                break;
            case 3: // Units of minutes
                if (timerDisplayMinutes % 10 < 9)
                    timerDisplayMinutes += 1;
                else
                    timerDisplayMinutes -= 9; // Reset units digit to 0
                break;
            case 4: // Tens of seconds
                if (timerDisplaySeconds < 50)
                    timerDisplaySeconds += 10;
                else
                    timerDisplaySeconds -= 50; // Reset to 0 from 50
                break;
            case 5: // Units of seconds
                if (timerDisplaySeconds % 10 < 9)
                    timerDisplaySeconds += 1;
                else
                    timerDisplaySeconds -= 9; // Reset units digit to 0
                break;
        }
        
        // Update TimeSpan for compatibility with existing code
        countdownTime = new TimeSpan(0, timerDisplayHours, timerDisplayMinutes, timerDisplaySeconds);
        UpdateDisplay();
    }

    private void DecreaseTimerDigit(int index)
    {
        // Determine which digit to change
        switch (index)
        {
            case 0: // Tens of hours
                if (timerDisplayHours >= 10)
                    timerDisplayHours -= 10;
                else
                    timerDisplayHours += 90; // Wrap around to 90
                break;
            case 1: // Units of hours
                if (timerDisplayHours % 10 > 0)
                    timerDisplayHours -= 1;
                else
                    timerDisplayHours += 9; // Wrap around to x9
                break;
            case 2: // Tens of minutes
                if (timerDisplayMinutes >= 10)
                    timerDisplayMinutes -= 10;
                else
                    timerDisplayMinutes += 50; // Wrap around to 50
                break;
            case 3: // Units of minutes
                if (timerDisplayMinutes % 10 > 0)
                    timerDisplayMinutes -= 1;
                else
                    timerDisplayMinutes += 9; // Wrap around to x9
                break;
            case 4: // Tens of seconds
                if (timerDisplaySeconds >= 10)
                    timerDisplaySeconds -= 10;
                else
                    timerDisplaySeconds += 50; // Wrap around to 50
                break;
            case 5: // Units of seconds
                if (timerDisplaySeconds % 10 > 0)
                    timerDisplaySeconds -= 1;
                else
                    timerDisplaySeconds += 9; // Wrap around to x9
                break;
        }
        
        // Update TimeSpan for compatibility with existing code
        countdownTime = new TimeSpan(0, timerDisplayHours, timerDisplayMinutes, timerDisplaySeconds);
        
        UpdateDisplay();
    }

    private void LayoutTimeDisplay(float scale)
    {
        if (pnlTimeDisplay.Width <= 0 || pnlTimeDisplay.Height <= 0)
        {
            // In case the panel is not yet placed, use the form size for approximate calculation
            pnlTimeDisplay.Width = ClientSize.Width;
            pnlTimeDisplay.Height = ClientSize.Height - (pnlControls?.Height ?? 50);
        }
        
        // Optimize the size of digits for the new width
        int digitWidth = (int)(60 * scale);
        int digitHeight = (int)(90 * scale);
        int separatorWidth = (int)(18 * scale);
        int totalWidth = 6 * digitWidth + 2 * separatorWidth;
        
        int startX = Math.Max(5, (pnlTimeDisplay.Width - totalWidth) / 2);
        int controlsHeight = pnlControls?.Height ?? 50;
        int y = Math.Max(5, (pnlTimeDisplay.Height - controlsHeight - digitHeight) / 2);
        
        int x = startX;
        
        // Size of arrows
        int arrowWidth = (int)(35 * scale);
        int arrowHeight = (int)(20 * scale);
        
        // Current index for arrows
        int arrowIndex = 0;
        
        // Position hour digits
        for (int i = 0; i < 2; i++)
        {
            hourDigits[i].Width = digitWidth;
            hourDigits[i].Height = digitHeight;
            hourDigits[i].Location = new Point(x, y);
            
            // Position arrows above and below digits
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
            
            // Position arrows above and below digits
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
            
            // Position arrows above and below digits
            upArrows[arrowIndex].Location = new Point(x + (digitWidth - arrowWidth) / 2, y - arrowHeight - 5);
            upArrows[arrowIndex].Width = arrowWidth;
            upArrows[arrowIndex].Height = arrowHeight;
            
            downArrows[arrowIndex].Location = new Point(x + (digitWidth - arrowWidth) / 2, y + digitHeight + 5);
            downArrows[arrowIndex].Width = arrowWidth;
            downArrows[arrowIndex].Height = arrowHeight;
            
            x += digitWidth;
            arrowIndex++;
        }
        
        // After changing sizes, forcefully redraw separators
        separators[0].Invalidate();
        separators[1].Invalidate();
    }
    
    private void LayoutControlPanel(float scale)
    {
        if (pnlControls.Width <= 0)
        {
            // In case the panel is not yet placed, use the form width for approximate calculation
            pnlControls.Width = ClientSize.Width;
        }
        
        // Optimize the size of buttons and the distance between them
        int buttonWidth = (int)(42 * scale);  // Reduced from 45 for space saving
        int buttonHeight = (int)(35 * scale); 
        int gap = (int)(8 * scale);          // Reduced from 10 for space saving
        
        int totalWidth = 8 * buttonWidth + 7 * gap;
        
        // Ensure we have enough width
        int availableWidth = pnlControls.Width - 20; // 10 pixels offset on each side
        
        // If buttons don't fit, reduce their size and gaps
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
        pnlTimeDisplay.Padding = new Padding((int)(5 * scale));
        
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
        // Increase the height of the control panel
        pnlControls.Height = (int)(50 * scale);
        
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
        // Decrease seconds
        timerDisplaySeconds--;
        
        // Handle underflow
        if (timerDisplaySeconds < 0) {
            timerDisplaySeconds = 59;
            timerDisplayMinutes--;
            
            if (timerDisplayMinutes < 0) {
                timerDisplayMinutes = 59;
                timerDisplayHours--;
                
                if (timerDisplayHours < 0) {
                    // Timer completed
                    timerDisplayHours = 0;
                    timerDisplayMinutes = 0;
                    timerDisplaySeconds = 0;
                    timerCountdown?.Stop();
                    UpdateButtonStates();
                    
                    // Notify when timer completes
                    FlashWindow();
                    ShowTimerCompleteNotification();
                    
                    // Return to timer setup mode
                    SetTimerSetupMode(true);
                }
            }
        }
        
        // Update TimeSpan for compatibility
        countdownTime = new TimeSpan(0, timerDisplayHours, timerDisplayMinutes, timerDisplaySeconds);
        
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
        
        // Hide timer setup arrows when exiting timer mode
        SetTimerSetupMode(false);
        
        // Close notification if it's open
        CloseNotificationIfOpen();
        
        UpdateButtonStates();
        UpdateDisplay();
    }
    
    private void SwitchToTimerMode()
    {
        currentMode = Mode.Timer;
        timerClock?.Stop();
        timerStopwatch?.Stop();
        
        // Initialize timer display fields
        if (countdownTime == TimeSpan.Zero) {
            timerDisplayHours = 0;
            timerDisplayMinutes = 0;
            timerDisplaySeconds = 0;
        } else {
            // If there was a previous time set, restore it
            timerDisplayHours = countdownTime.Hours + countdownTime.Days * 24;
            timerDisplayMinutes = countdownTime.Minutes;
            timerDisplaySeconds = countdownTime.Seconds;
        }
        
        // Close notification if it's open
        CloseNotificationIfOpen();
        
        UpdateButtonStates();
        UpdateDisplay();
    }
    
    private void SwitchToStopwatchMode()
    {
        currentMode = Mode.Stopwatch;
        timerClock?.Stop();
        timerCountdown?.Stop();
        
        // Hide timer setup arrows when exiting timer mode
        SetTimerSetupMode(false);
        
        // Close notification if it's open
        CloseNotificationIfOpen();
        
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
                
                // Hide timer setup arrows in clock mode
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
                
                // In stopwatch mode, arrows should also be hidden
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
        
        // Set background colors for active buttons with light color accents
        btnClock.BackColor = currentMode == Mode.Clock ? 
            Color.FromArgb(35, 35, 45) : Color.FromArgb(20, 20, 20); // Light blue accent for clock
        
        btnTimer.BackColor = currentMode == Mode.Timer ? 
            Color.FromArgb(40, 30, 30) : Color.FromArgb(20, 20, 20); // Reddish accent for timer
        
        btnStopwatch.BackColor = currentMode == Mode.Stopwatch ? 
            Color.FromArgb(30, 40, 30) : Color.FromArgb(20, 20, 20); // Greenish accent for stopwatch
        
        // Control buttons should also have accents when active
        btnStart.BackColor = btnStart.Enabled && !btnPause.Enabled ? 
            Color.FromArgb(30, 40, 30) : Color.FromArgb(20, 20, 20); // Greenish accent
        
        btnPause.BackColor = btnPause.Enabled ? 
            Color.FromArgb(40, 35, 25) : Color.FromArgb(20, 20, 20); // Yellowish accent
        
        btnReset.BackColor = Color.FromArgb(20, 20, 20);
        
        // TopMost button should also have an accent if active
        btnTopMost.BackColor = this.TopMost ? 
            Color.FromArgb(40, 30, 40) : Color.FromArgb(20, 20, 20); // Purple accent
        
        btnClose.BackColor = Color.FromArgb(20, 20, 20);
        
        // Highlight the text of active buttons
        btnClock.ForeColor = currentMode == Mode.Clock ? Color.White : Color.FromArgb(200, 200, 200);
        btnTimer.ForeColor = currentMode == Mode.Timer ? Color.White : Color.FromArgb(200, 200, 200);
        btnStopwatch.ForeColor = currentMode == Mode.Stopwatch ? Color.White : Color.FromArgb(200, 200, 200);
        btnTopMost.ForeColor = this.TopMost ? Color.White : Color.FromArgb(200, 200, 200);
    }
    #endregion
    
    #region Button Event Handlers
    private void BtnTimer_Click(object sender, EventArgs e)
    {
        // Switch to timer mode
        SwitchToTimerMode();
        
        // Enable timer setup mode
        SetTimerSetupMode(true);
    }
    
    private void BtnStart_Click(object sender, EventArgs e)
    {
        if (currentMode == Mode.Timer)
        {
            if (isTimerSetupMode)
            {
                // If in setup mode, disable it and start the timer
                SetTimerSetupMode(false);
            }
            timerCountdown?.Start();
            
            // Close notification if it's open
            CloseNotificationIfOpen();
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
                // If resetting while the timer is running, enable setup mode
                SetTimerSetupMode(true);
            }
            // Reset all display fields
            timerDisplayHours = 0;
            timerDisplayMinutes = 0;
            timerDisplaySeconds = 0;
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
                // Use our custom fields directly
                timeText = $"{timerDisplayHours:D2}:{timerDisplayMinutes:D2}:{timerDisplaySeconds:D2}";
                break;
            case Mode.Stopwatch:
                // Custom format to ensure hours over 24 are shown properly
                int hours = stopwatchTime.Hours;
                int minutes = stopwatchTime.Minutes;
                int seconds = stopwatchTime.Seconds;
                timeText = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
                break;
            default:
                timeText = "00:00:00";
                break;
        }
        
        // Check if digits have changed and start animation only for changed ones
        UpdateFlipDigits(timeText);
        previousTimeText = timeText;
    }
    
    private void UpdateFlipDigits(string timeText)
    {
        // Format should be "HH:MM:SS"
        if (timeText.Length < 8) return;
        
        // Update hours (only if changed)
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[0] != timeText[0])
            hourDigits[0].Value = timeText[0] - '0';
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[1] != timeText[1])
            hourDigits[1].Value = timeText[1] - '0';
        
        // Update minutes (only if changed)
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[3] != timeText[3])
            minuteDigits[0].Value = timeText[3] - '0';
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[4] != timeText[4])
            minuteDigits[1].Value = timeText[4] - '0';
        
        // Update seconds (only if changed)
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[6] != timeText[6])
            secondDigits[0].Value = timeText[6] - '0';
        if (string.IsNullOrEmpty(previousTimeText) || previousTimeText[7] != timeText[7])
            secondDigits[1].Value = timeText[7] - '0';
    }
    #endregion

    // New method for setting timer setup mode
    private void SetTimerSetupMode(bool enabled)
    {
        isTimerSetupMode = enabled;
        
        // Show/hide setup arrows only if in timer mode
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
            
            // Change button accessibility
            btnStart.Enabled = enabled;
            btnPause.Enabled = !enabled;
            btnReset.Enabled = true;
            
            // If enabling setup mode and time is not set, set it to 0
            if (enabled && countdownTime == TimeSpan.Zero)
            {
                countdownTime = TimeSpan.Zero;
            }
            
            // Update display
            UpdateDisplay();
        }
        else 
        {
            // If not in timer mode, hide arrows in any case
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
        
        // We don't use context menu for timer anymore
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
        
        // Adjust to match sizes from LayoutControlPanel
        button.Width = (int)(42 * scale);   // Was 45
        button.Height = (int)(35 * scale);
        
        // Button font size
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
    private const double FLIP_DURATION_MS = 150;
    
    // Dark-black background
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
                
                // Start flipping animation
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
        
        // Timer for animation - increase refresh rate for smoother animation
        flipTimer = new Timer
        {
            Interval = 10
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
        
        // Rounded corners for digits
        GraphicsPath path = CreateRoundedRectangle(rect, 6);
        
        // Main background (dark background)
        using (var brush = new SolidBrush(_backColor))
        {
            g.FillPath(brush, path);
        }
        
        // Gradient for upper half of digit - darker
        Rectangle upperHalf = new Rectangle(0, 0, this.Width, this.Height / 2);
        using (var brush = new LinearGradientBrush(
            upperHalf,
            Color.FromArgb(30, 30, 30),
            Color.FromArgb(20, 20, 20),
            LinearGradientMode.Vertical))
        {
            // Create path for upper half with rounded top corners
            GraphicsPath upperPath = new GraphicsPath();
            upperPath.AddArc(0, 0, 12, 12, 180, 90); // Top left corner
            upperPath.AddArc(rect.Width - 12, 0, 12, 12, 270, 90); // Top right corner
            upperPath.AddLine(rect.Width, rect.Height / 2, 0, rect.Height / 2);
            upperPath.AddLine(0, rect.Height / 2, 0, 6);
            
            g.FillPath(brush, upperPath);
        }
        
        // Gradient for lower half of digit
        Rectangle lowerHalf = new Rectangle(0, this.Height / 2, this.Width, this.Height / 2);
        using (var brush = new LinearGradientBrush(
            lowerHalf,
            Color.FromArgb(20, 20, 20),
            Color.FromArgb(15, 15, 15),
            LinearGradientMode.Vertical))
        {
            // Create path for lower half with rounded bottom corners
            GraphicsPath lowerPath = new GraphicsPath();
            lowerPath.AddLine(0, rect.Height / 2, rect.Width, rect.Height / 2);
            lowerPath.AddLine(rect.Width, rect.Height / 2, rect.Width, rect.Height - 6);
            lowerPath.AddArc(rect.Width - 12, rect.Height - 12, 12, 12, 0, 90); // Bottom right corner
            lowerPath.AddArc(0, rect.Height - 12, 12, 12, 90, 90); // Bottom left corner
            lowerPath.AddLine(0, rect.Height - 6, 0, rect.Height / 2);
            
            g.FillPath(brush, lowerPath);
        }
        
        // Frame around the entire digit
        using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
        {
            g.DrawPath(pen, path);
        }
        
        // Separator line in the middle with shadow
        using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
        {
            g.DrawLine(pen, 0, this.Height / 2, this.Width, this.Height / 2);
        }
        
        // Shadow for the line
        using (var pen = new Pen(Color.FromArgb(40, 40, 40), 1))
        {
            g.DrawLine(pen, 0, this.Height / 2 + 1, this.Width, this.Height / 2 + 1);
        }
        
        // Drawing digit separately for upper and lower halves
        float fontSize = Math.Min(this.Width * 0.85f, this.Height * 0.85f);
        StringFormat format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        
        // Setting up better text rendering
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        
        // Saving graphics state
        GraphicsState state = g.Save();
        
        // Drawing upper half of digit
        g.SetClip(upperHalf);
        
        // Shadow for upper half of digit
        using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, new RectangleF(3, 6, this.Width, this.Height), format);
        }
        
        // Upper half of digit itself
        using (var brush = new SolidBrush(Color.FromArgb(255, 255, 255)))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, new RectangleF(0, 3, this.Width, this.Height), format);
        }
        
        // Restore graphics state and draw lower half
        g.Restore(state);
        g.SetClip(lowerHalf);
        
        // Shadow for lower half of digit
        using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, new RectangleF(3, 6, this.Width, this.Height), format);
        }
        
        // Lower half of digit itself
        using (var brush = new SolidBrush(Color.FromArgb(255, 255, 255)))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, new RectangleF(0, 3, this.Width, this.Height), format);
        }
        
        // Reset clip
        g.ResetClip();
        
        // Create upper path for correct clipping of glow
        GraphicsPath upperClipPath = new GraphicsPath();
        upperClipPath.AddArc(0, 0, 12, 12, 180, 90);
        upperClipPath.AddArc(rect.Width - 12, 0, 12, 12, 270, 90);
        upperClipPath.AddLine(rect.Width, rect.Height / 2, 0, rect.Height / 2);
        upperClipPath.AddLine(0, rect.Height / 2, 0, 6);
        
        // Save state before clipping
        state = g.Save();
        g.SetClip(upperClipPath);
        
        // Bright horizontal band at the top (thin and precise)
        Rectangle reflectionRect = new Rectangle(5, 5, this.Width - 10, this.Height / 30);
        using (var brush = new LinearGradientBrush(
            reflectionRect,
            Color.FromArgb(70, 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.Vertical))
        {
            g.FillRectangle(brush, reflectionRect);
        }
        
        // Restore graphics state
        g.Restore(state);
        
        // Redraw separator line to ensure it's above digits
        using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
        {
            g.DrawLine(pen, 0, this.Height / 2, this.Width, this.Height / 2);
        }
        
        // Shadow for the line
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
        
        // Determine animation phase
        bool isFirstPhase = progress < 0.5;
        double phaseProgress = isFirstPhase ? progress * 2 : (progress - 0.5) * 2;
        
        // Angle for current phase (from 0 to 90 degrees)
        double angle = phaseProgress * 90.0;
        
        // Determine visibility of panels depending on phase
        if (isFirstPhase)
        {
            // PHASE 1: Upper panel of old digit folds down
            
            // 1. Draw lower half of OLD digit (stationary)
            Rectangle lowerRect = new Rectangle(0, halfHeight, this.Width, halfHeight);
            PaintStaticHalfDigit(g, lowerRect, _previousValue, false);
            
            // 2. Draw upper half of NEW digit (visible under folding panel)
            Rectangle upperNewRect = new Rectangle(0, 0, this.Width, halfHeight);
            PaintStaticHalfDigit(g, upperNewRect, _value, true);
            
            // 3. Draw folding top half of OLD digit
            PaintFoldingTopDown(g, angle);
        }
        else
        {
            // PHASE 2: Bottom panel of NEW digit folds down

            // 1. Draw upper half of NEW digit (already fully visible)
            Rectangle upperRect = new Rectangle(0, 0, this.Width, halfHeight);
            PaintStaticHalfDigit(g, upperRect, _value, true);
            
            // 2. Draw lower half of OLD digit (will be covered by folding panel)
            Rectangle lowerOldRect = new Rectangle(0, halfHeight, this.Width, halfHeight);
            PaintStaticHalfDigit(g, lowerOldRect, _previousValue, false);
            
            // 3. Draw folding bottom half of NEW digit
            PaintFoldingBottomDown(g, angle);
        }
    }
    
    // Draws bottom panel folding down
    private void PaintFoldingBottomDown(Graphics g, double angle)
    {
        int halfHeight = this.Height / 2;
        
        // Create temporary bitmap for lower half of NEW digit
        using (Bitmap bmp = new Bitmap(this.Width, halfHeight))
        using (Graphics tempG = Graphics.FromImage(bmp))
        {
            tempG.SmoothingMode = SmoothingMode.AntiAlias;
            tempG.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            tempG.InterpolationMode = InterpolationMode.HighQualityBicubic;
            tempG.PixelOffsetMode = PixelOffsetMode.HighQuality;
            
            // Draw lower half of NEW digit
            Rectangle tempRect = new Rectangle(0, 0, this.Width, halfHeight);
            
            // Create path for lower half
            GraphicsPath path = new GraphicsPath();
            path.AddLine(0, 0, tempRect.Width, 0);
            path.AddLine(tempRect.Width, 0, tempRect.Width, tempRect.Height - 6);
            path.AddArc(tempRect.Width - 12, tempRect.Height - 12, 12, 12, 0, 90); // Bottom right corner
            path.AddArc(0, tempRect.Height - 12, 12, 12, 90, 90); // Bottom left corner
            path.AddLine(0, tempRect.Height - 6, 0, 0);
            
            // Fill background
            using (var brush = new LinearGradientBrush(
                tempRect,
                Color.FromArgb(20, 20, 20),
                Color.FromArgb(15, 15, 15),
                LinearGradientMode.Vertical))
            {
                tempG.FillPath(brush, path);
            }
            
            // Draw border
            using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
            {
                tempG.DrawPath(pen, path);
            }
            
            // Draw digit
            float fontSize = Math.Min(this.Width * 0.85f, this.Height * 0.85f); // Increased font size
            StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            
            // Set clip and draw digit
            tempG.SetClip(path);
            
            // Shift for lower half of digit - add offset to bottom for better centering
            float yOffset = -halfHeight + 3; // Add offset to bottom
            
            // Draw digit shadow
            using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
            {
                tempG.DrawString(_value.ToString(), font, brush, 
                    new RectangleF(3, yOffset + 3, this.Width, this.Height), format);
            }
            
            // Draw digit
            using (var brush = new SolidBrush(_foreColor))
            using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
            {
                tempG.DrawString(_value.ToString(), font, brush, 
                    new RectangleF(0, yOffset, this.Width, this.Height), format);
            }
            
            // Invert angle for folding (0 -> fully folded, 90 -> fully unfolded)
            float scaleHeight = (float)Math.Sin(angle * Math.PI / 180.0);
            
            // Determine points for drawing folding panel
            Point[] destPoints = new Point[3];
            
            // Create transformation where panel is attached at the top and folds down
            destPoints[0] = new Point(0, halfHeight); // Left top corner fixed at center
            destPoints[1] = new Point(this.Width, halfHeight); // Right top corner fixed at center
            destPoints[2] = new Point(0, halfHeight + (int)(halfHeight * scaleHeight)); // Left bottom corner moves down as it folds
            
            // Draw transformed image
            g.DrawImage(bmp, destPoints);
            
            // Add shadow for 3D effect (less shadow as it folds)
            int shadowAlpha = (int)(150 * (1.0 - Math.Sin(angle * Math.PI / 180.0)));
            using (var brush = new SolidBrush(Color.FromArgb(shadowAlpha, 0, 0, 0)))
            {
                g.FillPolygon(brush, destPoints);
            }
            
            // Add line at bottom edge of folding panel
            using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
            {
                g.DrawLine(pen, 
                    0, halfHeight + (int)(halfHeight * scaleHeight),
                    this.Width, halfHeight + (int)(halfHeight * scaleHeight));
            }
        }
    }
    
    // Draws static half of digit (upper or lower)
    private void PaintStaticHalfDigit(Graphics g, Rectangle rect, int digit, bool isTopHalf)
    {
        // Create path with rounded corners for half
        GraphicsPath path = new GraphicsPath();
        
        if (isTopHalf)
        {
            // Upper half of digit with rounded top corners
            path.AddArc(rect.X, rect.Y, 12, 12, 180, 90); // Top left corner
            path.AddArc(rect.X + rect.Width - 12, rect.Y, 12, 12, 270, 90); // Top right corner
            path.AddLine(rect.X + rect.Width, rect.Y + rect.Height, rect.X, rect.Y + rect.Height);
            path.AddLine(rect.X, rect.Y + rect.Height, rect.X, rect.Y + 6);
        }
        else
        {
            // Lower half of digit with rounded bottom corners
            path.AddLine(rect.X, rect.Y, rect.X + rect.Width, rect.Y);
            path.AddLine(rect.X + rect.Width, rect.Y, rect.X + rect.Width, rect.Y + rect.Height - 6);
            path.AddArc(rect.X + rect.Width - 12, rect.Y + rect.Height - 12, 12, 12, 0, 90); // Bottom right corner
            path.AddArc(rect.X, rect.Y + rect.Height - 12, 12, 12, 90, 90); // Bottom left corner
            path.AddLine(rect.X, rect.Y + rect.Height - 6, rect.X, rect.Y);
        }
        
        // Background for half with gradient
        using (var brush = new LinearGradientBrush(
            rect,
            isTopHalf ? Color.FromArgb(30, 30, 30) : Color.FromArgb(20, 20, 20),
            isTopHalf ? Color.FromArgb(20, 20, 20) : Color.FromArgb(15, 15, 15),
            LinearGradientMode.Vertical))
        {
            g.FillPath(brush, path);
        }
        
        // Frame
        using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
        {
            g.DrawPath(pen, path);
        }
        
        // Drawing digit with clipping to limit it inside half
        float fontSize = Math.Min(this.Width * 0.85f, this.Height * 0.85f);
        
        // Save graphics state and set clip
        GraphicsState state = g.Save();
        g.SetClip(path);
        
        // Full rectangle for text
        RectangleF fullRect = new RectangleF(0, 3, this.Width, this.Height); // Shift text down
        
        // Formatting text
        StringFormat format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        
        // Setting up better text rendering
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        
        // Drawing digit shadow
        using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, new RectangleF(3, 6, this.Width, this.Height), format);
        }
        
        // Drawing digit
        using (var brush = new SolidBrush(_foreColor))
        using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
        {
            g.DrawString(digit.ToString(), font, brush, fullRect, format);
        }
        
        g.Restore(state);
        
        // Adding glow for upper half
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
        
        // Separator line for upper half
        if (isTopHalf)
        {
            using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
            {
                g.DrawLine(pen, rect.X, rect.Y + rect.Height, rect.X + rect.Width, rect.Y + rect.Height);
            }
            
            // Shadow for line
            using (var pen = new Pen(Color.FromArgb(40, 40, 40), 1))
            {
                g.DrawLine(pen, rect.X, rect.Y + rect.Height + 1, rect.X + rect.Width, rect.Y + rect.Height + 1);
            }
        }
    }
    
    // Draws upper panel folding down
    private void PaintFoldingTopDown(Graphics g, double angle)
    {
        int halfHeight = this.Height / 2;
        
        // Create temporary bitmap for upper half of old digit
        using (Bitmap bmp = new Bitmap(this.Width, halfHeight))
        using (Graphics tempG = Graphics.FromImage(bmp))
        {
            tempG.SmoothingMode = SmoothingMode.AntiAlias;
            tempG.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            tempG.InterpolationMode = InterpolationMode.HighQualityBicubic;
            tempG.PixelOffsetMode = PixelOffsetMode.HighQuality;
            
            // Draw upper half of OLD digit
            Rectangle tempRect = new Rectangle(0, 0, this.Width, halfHeight);
            
            // Create path for upper half
            GraphicsPath path = new GraphicsPath();
            path.AddArc(0, 0, 12, 12, 180, 90); // Top left corner
            path.AddArc(tempRect.Width - 12, 0, 12, 12, 270, 90); // Top right corner
            path.AddLine(tempRect.Width, tempRect.Height, 0, tempRect.Height);
            path.AddLine(0, tempRect.Height, 0, 6);
            
            // Fill background
            using (var brush = new LinearGradientBrush(
                tempRect,
                Color.FromArgb(30, 30, 30),
                Color.FromArgb(20, 20, 20),
                LinearGradientMode.Vertical))
            {
                tempG.FillPath(brush, path);
            }
            
            // Draw frame
            using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
            {
                tempG.DrawPath(pen, path);
            }
            
            // Draw digit
            float fontSize = Math.Min(this.Width * 0.85f, this.Height * 0.85f); // Increased font size
            StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            
            // Set clip and draw digit
            tempG.SetClip(path);
            
            // Drawing digit shadow - shift down
            using (var brush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
            {
                tempG.DrawString(_previousValue.ToString(), font, brush, 
                    new RectangleF(3, 6, this.Width, this.Height), format);
            }
            
            // Drawing digit - shift down
            using (var brush = new SolidBrush(_foreColor))
            using (var font = new Font(DEFAULT_FONT.FontFamily, fontSize, FontStyle.Bold))
            {
                tempG.DrawString(_previousValue.ToString(), font, brush, 
                    new RectangleF(0, 3, this.Width, this.Height), format);
            }
            
            // Adding glow - identical to glow in static upper half
            Rectangle reflectionRect = new Rectangle(5, 5, tempRect.Width - 10, tempRect.Height / 15);
            using (var brush = new LinearGradientBrush(
                reflectionRect,
                Color.FromArgb(70, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                LinearGradientMode.Vertical))
            {
                tempG.FillRectangle(brush, reflectionRect);
            }
            
            // Calculate vertical compression due to rotation
            float scaleHeight = (float)Math.Cos(angle * Math.PI / 180.0);
            
            // Create points for drawing folded panel - KEY CHANGE
            Point[] destPoints = new Point[3];
            
            // Top corners move down (folding goes from top)
            destPoints[0] = new Point(0, (int)(halfHeight * (1 - scaleHeight))); // Left top corner - moves down
            destPoints[1] = new Point(this.Width, (int)(halfHeight * (1 - scaleHeight))); // Right top corner - moves down
            destPoints[2] = new Point(0, halfHeight); // Left bottom corner - remains stationary
            
            // Draw transformed image
            g.DrawImage(bmp, destPoints);
            
            // Add shadow depending on rotation angle
            int shadowAlpha = (int)(150 * (1.0 - Math.Cos(angle * Math.PI / 180.0)));
            using (var brush = new SolidBrush(Color.FromArgb(shadowAlpha, 0, 0, 0)))
            {
                g.FillPolygon(brush, destPoints);
            }
            
            // Add line at top of folding panel
            using (var pen = new Pen(Color.FromArgb(5, 5, 5), 1))
            {
                g.DrawLine(pen, 
                    0, (int)(halfHeight * (1 - scaleHeight)),
                    this.Width, (int)(halfHeight * (1 - scaleHeight)));
            }
        }
    }
    
    // Helper method to create rounded rectangle
    private GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        
        // Top left corner
        path.AddArc(bounds.X, bounds.Y, radius * 2, radius * 2, 180, 90);
        
        // Top side and top right corner
        path.AddArc(bounds.X + bounds.Width - radius * 2, bounds.Y, radius * 2, radius * 2, 270, 90);
        
        // Right side and bottom right corner
        path.AddArc(bounds.X + bounds.Width - radius * 2, bounds.Y + bounds.Height - radius * 2, radius * 2, radius * 2, 0, 90);
        
        // Bottom side and bottom left corner
        path.AddArc(bounds.X, bounds.Y + bounds.Height - radius * 2, radius * 2, radius * 2, 90, 90);
        
        // Close path
        path.CloseFigure();
        
        return path;
    }
} 