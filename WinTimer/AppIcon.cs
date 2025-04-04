namespace WinTimer;

using System;
using System.Drawing;
using System.IO;
using System.Reflection;

public static class AppIcon
{
    public static Icon GetAppIcon()
    {
        // Создаем простую иконку программно
        Bitmap bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(30, 30, 30));
            
            // Рисуем циферблат часов
            g.FillEllipse(Brushes.DarkGray, 2, 2, 28, 28);
            g.DrawEllipse(new Pen(Color.White, 2), 2, 2, 28, 28);
            
            // Рисуем стрелки часов
            Point center = new Point(16, 16);
            
            // Часовая стрелка
            g.DrawLine(new Pen(Color.White, 2), 
                center, 
                new Point(
                    center.X + (int)(8 * Math.Sin(Math.PI / 3)), 
                    center.Y - (int)(8 * Math.Cos(Math.PI / 3))
                )
            );
            
            // Минутная стрелка
            g.DrawLine(new Pen(Color.White, 2), 
                center, 
                new Point(
                    center.X + (int)(12 * Math.Sin(Math.PI / 6)), 
                    center.Y - (int)(12 * Math.Cos(Math.PI / 6))
                )
            );
        }
        
        // Конвертируем Bitmap в Icon
        IntPtr hIcon = bmp.GetHicon();
        Icon icon = Icon.FromHandle(hIcon);
        
        return icon;
    }
} 