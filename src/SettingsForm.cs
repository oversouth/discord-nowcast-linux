using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Strings = Nowcast.Text;

namespace Nowcast
{
    // The settings window from the tray menu. Every change is saved to
    // config.json at once, and the engine picks it up from there.
    class SettingsForm : Form
    {
        // What the member list shows after "Listening to", in the order of the list.
        static readonly string[] inListValues = { "source", "artist", "title", "app" };

        class SourceRow
        {
            public string Name;
            public string App;

            public override string ToString()
            {
                return Name;
            }
        }

        readonly Engine engine;
        readonly ComboBox inList = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
        readonly CheckBox covers = new CheckBox { Text = Strings.CoversOption, AutoSize = true };
        readonly CheckBox buttons = new CheckBox { Text = Strings.ButtonsOption, AutoSize = true };
        readonly CheckBox showPaused = new CheckBox { Text = Strings.ShowPausedOption, AutoSize = true };
        readonly CheckedListBox sources = new CheckedListBox { CheckOnClick = true, Width = 320, Height = 110, IntegralHeight = false };
        readonly CheckBox autostart = new CheckBox { Text = Strings.Autostart, AutoSize = true };
        readonly CheckBox checkUpdates = new CheckBox { Text = Strings.CheckUpdatesOption, AutoSize = true };
        bool isLoading;

        public SettingsForm(Engine engine)
        {
            this.engine = engine;
            Text = "discord-nowcast";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            inList.Items.AddRange(new object[] { Strings.InListSource, Strings.InListArtist, Strings.InListTitle, Strings.InListApp });

            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            layout.Controls.Add(Heading(Strings.StatusHeading));
            layout.Controls.Add(Note(Strings.InListLabel));
            layout.Controls.Add(inList);
            layout.Controls.Add(covers);
            layout.Controls.Add(buttons);
            layout.Controls.Add(showPaused);
            layout.Controls.Add(Heading(Strings.Sources));
            layout.Controls.Add(Note(Strings.SourcesNote));
            layout.Controls.Add(sources);
            layout.Controls.Add(Heading(Strings.ProgramHeading));
            layout.Controls.Add(autostart);
            layout.Controls.Add(checkUpdates);

            var links = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            links.Controls.Add(Link(Strings.OpenLog, delegate { Open(Log.FilePath); }));
            links.Controls.Add(Link(Strings.Advanced, delegate { Open(Settings.FilePath); }));
            links.Controls.Add(new Label { Text = "v" + Program.Version, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(12, 3, 0, 0) });
            layout.Controls.Add(links);

            var close = new Button { Text = Strings.Close, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0) };
            close.Click += delegate { Close(); };
            CancelButton = close;
            layout.Controls.Add(close);
            Controls.Add(layout);

            Fill();

            inList.SelectedIndexChanged += delegate { Change(s => s.InList = inListValues[inList.SelectedIndex]); };
            covers.CheckedChanged += delegate { Change(s => s.Covers = covers.Checked); };
            buttons.CheckedChanged += delegate { Change(s => s.Buttons = buttons.Checked); };
            showPaused.CheckedChanged += delegate { Change(s => s.ShowPaused = showPaused.Checked); };
            checkUpdates.CheckedChanged += delegate { Change(s => s.CheckUpdates = checkUpdates.Checked); };
            sources.ItemCheck += (sender, e) =>
            {
                var row = (SourceRow)sources.Items[e.Index];
                var isShown = e.NewValue == CheckState.Checked;
                Change(s => s.SetHidden(row.App, row.Name, !isShown));
            };
            autostart.CheckedChanged += delegate
            {
                if (isLoading) return;
                if (autostart.Checked) Startup.Enable();
                else Startup.Disable();
            };
        }

        // The current settings into the controls, without saving them back.
        void Fill()
        {
            isLoading = true;
            var settings = Settings.Load();
            var index = Array.IndexOf(inListValues, settings.InList);
            inList.SelectedIndex = index >= 0 ? index : 0;
            covers.Checked = settings.Covers;
            buttons.Checked = settings.Buttons;
            showPaused.Checked = settings.ShowPaused;
            checkUpdates.Checked = settings.CheckUpdates;
            autostart.Checked = Startup.IsEnabled;

            // The apps playing now, plus the hidden ones, so they can come back.
            sources.Items.Clear();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var track in engine.Sessions)
            {
                if (!seen.Add(track.Source.Name)) continue;
                var row = new SourceRow { Name = track.Source.Name, App = track.App };
                sources.Items.Add(row, !settings.Hides(row.App, row.Name));
            }
            foreach (var word in settings.Hide)
            {
                if (seen.Add(word)) sources.Items.Add(new SourceRow { Name = word, App = word }, false);
            }
            isLoading = false;
        }

        public void ShowAgain()
        {
            Fill();
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        // From the file as it is now, so edits made by hand are kept.
        void Change(Action<Settings> change)
        {
            if (isLoading) return;
            var settings = Settings.Load();
            change(settings);
            settings.Save();
        }

        static Label Heading(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                Margin = new Padding(0, 10, 0, 4),
            };
        }

        static Label Note(string text)
        {
            return new Label { Text = text, AutoSize = true, MaximumSize = new Size(330, 0), ForeColor = SystemColors.GrayText };
        }

        static LinkLabel Link(string text, EventHandler click)
        {
            var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 3, 12, 0) };
            link.LinkClicked += (sender, e) => click(sender, e);
            return link;
        }

        static void Open(string file)
        {
            try
            {
                Process.Start("notepad.exe", "\"" + file + "\"");
            }
            catch (Exception e)
            {
                Log.Write("Could not open " + file + ": " + e.Message);
            }
        }
    }
}
