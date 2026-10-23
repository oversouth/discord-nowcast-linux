using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Nowcast
{
    // Discord's dark colors for the tray menu and the settings window, so
    // they look like part of the app the status goes to.
    static class Theme
    {
        public static readonly Color Back = Color.FromArgb(0x31, 0x33, 0x38);
        public static readonly Color Menu = Color.FromArgb(0x11, 0x12, 0x14);
        public static readonly Color Field = Color.FromArgb(0x1E, 0x1F, 0x22);
        public static readonly Color Hover = Color.FromArgb(0x40, 0x42, 0x49);
        public static readonly Color Border = Color.FromArgb(0x4E, 0x50, 0x58);
        public static readonly Color Text = Color.FromArgb(0xDB, 0xDE, 0xE1);
        public static readonly Color Muted = Color.FromArgb(0x94, 0x9B, 0xA4);
        public static readonly Color Accent = Color.FromArgb(0x58, 0x65, 0xF2);
        public static readonly Color AccentHover = Color.FromArgb(0x47, 0x52, 0xC4);
        public static readonly Color Link = Color.FromArgb(0x00, 0xA8, 0xFC);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        // A dark title bar: attribute 20 on Windows 10 20H1 and 11, 19 before.
        public static void DarkTitleBar(Form form)
        {
            try
            {
                var on = 1;
                if (DwmSetWindowAttribute(form.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(form.Handle, 19, ref on, 4);
            }
            catch (Exception)
            {
                // Older Windows: the title bar stays light.
            }
        }

        // A check mark in a rounded square: filled with the accent when checked.
        public static void DrawCheck(Graphics graphics, Rectangle box, bool isChecked)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Rounded(box, Math.Max(2, box.Width / 5)))
            {
                if (isChecked)
                {
                    using (var fill = new SolidBrush(Accent)) graphics.FillPath(fill, path);
                    using (var pen = new Pen(Color.White, Math.Max(1.5f, box.Width / 8f)))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        graphics.DrawLines(pen, new[]
                        {
                            new PointF(box.Left + box.Width * 0.25f, box.Top + box.Height * 0.52f),
                            new PointF(box.Left + box.Width * 0.43f, box.Top + box.Height * 0.70f),
                            new PointF(box.Left + box.Width * 0.76f, box.Top + box.Height * 0.32f),
                        });
                    }
                }
                else
                {
                    using (var fill = new SolidBrush(Field)) graphics.FillPath(fill, path);
                    using (var pen = new Pen(Border, 1.5f)) graphics.DrawPath(pen, path);
                }
            }
            graphics.SmoothingMode = SmoothingMode.Default;
        }

        static GraphicsPath Rounded(Rectangle box, int radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            path.AddArc(box.Left, box.Top, d, d, 180, 90);
            path.AddArc(box.Right - d, box.Top, d, d, 270, 90);
            path.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90);
            path.AddArc(box.Left, box.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // The tray menu and its submenus.
        public class MenuRenderer : ToolStripProfessionalRenderer
        {
            public MenuRenderer() : base(new MenuColors())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Text : Muted;
                TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, e.TextColor, e.TextFormat);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Text;
                base.OnRenderArrow(e);
            }

            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                var r = e.ImageRectangle;
                var size = Math.Min(r.Width, r.Height);
                DrawCheck(e.Graphics, new Rectangle(r.Left + (r.Width - size) / 2, r.Top + (r.Height - size) / 2, size - 1, size - 1), true);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                var y = e.Item.Height / 2;
                using (var pen = new Pen(Hover)) e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
            }
        }

        class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Menu; } }
            public override Color ImageMarginGradientBegin { get { return Menu; } }
            public override Color ImageMarginGradientMiddle { get { return Menu; } }
            public override Color ImageMarginGradientEnd { get { return Menu; } }
            public override Color MenuBorder { get { return Field; } }
            public override Color MenuItemBorder { get { return Hover; } }
            public override Color MenuItemSelected { get { return Hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
            public override Color MenuItemPressedGradientBegin { get { return Hover; } }
            public override Color MenuItemPressedGradientEnd { get { return Hover; } }
            public override Color SeparatorDark { get { return Hover; } }
            public override Color SeparatorLight { get { return Menu; } }
            public override Color CheckBackground { get { return Menu; } }
            public override Color CheckSelectedBackground { get { return Hover; } }
            public override Color CheckPressedBackground { get { return Hover; } }
        }

        // A checkbox drawn in the same colors: the system one stays white.
        public class CheckBox : System.Windows.Forms.CheckBox
        {
            public CheckBox()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                AutoSize = true;
                Cursor = Cursors.Hand;
            }

            public override Size GetPreferredSize(Size proposed)
            {
                var text = TextRenderer.MeasureText(Text, Font);
                var box = Font.Height;
                return new Size(box + 8 + text.Width, Math.Max(box, text.Height) + 4);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent != null ? Parent.BackColor : BackColor);
                var box = Font.Height - 2;
                var top = (Height - box) / 2;
                DrawCheck(e.Graphics, new Rectangle(1, top, box, box), Checked);
                var textRect = new Rectangle(box + 8, 0, Width - box - 8, Height);
                TextRenderer.DrawText(e.Graphics, Text, Font, textRect, Enabled ? Theme.Text : Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            }
        }
    }
}
