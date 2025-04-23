namespace WinTimer;

using System;
using System.Drawing;
using System.Windows.Forms;

public class DimmerForm : Form
{
    private int currentOpacity = 50; // Start at 50% opacity

    public DimmerForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        // Form setup
        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar = false;
        this.TopMost = true;
        this.BackColor = Color.Black;
        this.Opacity = currentOpacity / 100.0;
        this.TransparencyKey = Color.Empty;
        this.StartPosition = FormStartPosition.Manual;

        // Enable click-through
        int initialStyle = GetWindowLong(this.Handle, -20);
        SetWindowLong(this.Handle, -20, initialStyle | 0x80000 | 0x20 | 0x80);

        // Add keyboard shortcuts
        this.KeyPreview = true;
        this.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Escape)
            {
                this.Hide();
            }
        };
    }

    public void SetOpacity(int opacity)
    {
        currentOpacity = Math.Max(10, Math.Min(90, opacity));
        this.Opacity = currentOpacity / 100.0;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80000 | 0x20 | 0x80; // WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW
            return cp;
        }
    }

    // Import necessary Win32 functions
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
} 