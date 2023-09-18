using System;
using System.Windows.Forms;
using DiscordRPC;
using IniParser;
using IniParser.Model;
using System.IO;
using System.Timers;
using Microsoft.Win32;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;

public class Form1 : Form
{
    private DiscordRpcClient _client;
    private static readonly string ConfigDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stargazer", "RP");
    private const string ConfigFileName = "rpcconfig.ini";
    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectoryPath, ConfigFileName);
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;
    private const string AppName = "Stargazer's Rich Presence";

    protected override void SetVisibleCore(bool value)
    {
        if (!IsHandleCreated)
        {
            this.CreateHandle();
            value = false;
        }
        base.SetVisibleCore(value);
    }

    private bool IsDiscordRunning()
    {
        return Process.GetProcessesByName("Discord").Length > 0;
    }


    public Form1()
    {
        _trayMenu = new ContextMenuStrip
        {
            BackColor = Color.FromArgb(54, 57, 62),
            ForeColor = Color.White,
            Renderer = new DiscordThemeRenderer()
        };

        var openConfigMenuItem = new ToolStripMenuItem("Open Config");
        openConfigMenuItem.Click += (sender, args) => OpenConfigFile();
        _trayMenu.Items.Add(openConfigMenuItem);

        var refreshMenuItem = new ToolStripMenuItem("Refresh Config");
        refreshMenuItem.Click += (sender, args) => RefreshPresence();
        _trayMenu.Items.Add(refreshMenuItem);

        _trayMenu.Items.Add(new ToolStripSeparator());

        var startWithWindowsItem = new ToolStripMenuItem("Start with Windows");
        startWithWindowsItem.Click += (sender, args) => ToggleStartWithWindows(startWithWindowsItem);
        startWithWindowsItem.Checked = CheckIfStartsWithWindows();
        _trayMenu.Items.Add(startWithWindowsItem);

        _trayMenu.Items.Add(new ToolStripSeparator());

        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (sender, args) => ExitApplication();
        _trayMenu.Items.Add(exitMenuItem);
        openConfigMenuItem.Enabled = true;
        refreshMenuItem.Enabled = true;
        startWithWindowsItem.Enabled = true;
        exitMenuItem.Enabled = true;

        _trayIcon = new NotifyIcon
        {
            Text = "Stargazer's Rich Presence",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath),
            ContextMenuStrip = _trayMenu,
            Visible = true
        };


        InitializePresence();
    }

    private void OpenConfigFile()
    {
        try
        {
            System.Diagnostics.Process.Start("notepad.exe", ConfigFilePath);
        }
        catch (Exception ex)
        {
            NotifyUser($"Error opening config: {ex.Message}");
        }
    }


    private bool CheckIfStartsWithWindows()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
        {
            return key.GetValue(AppName) != null;
        }
    }

    private void ToggleStartWithWindows(ToolStripMenuItem item)
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
        {
            if (key.GetValue(AppName) != null)
            {
                key.DeleteValue(AppName, false);
                item.Checked = false;
            }
            else
            {
                key.SetValue(AppName, Application.ExecutablePath.ToString());
                item.Checked = true;
            }
        }
    }


    private async void InitializePresence()
    {
        if (!Directory.Exists(ConfigDirectoryPath))
        {
            Directory.CreateDirectory(ConfigDirectoryPath);
        }

        if (!File.Exists(ConfigFilePath))
        {
            CreateDefaultConfig();
            NotifyUser("Config not found. Default created.");
            OpenConfigFile();
            return;
        }

        NotifyUser("Waiting for Discord...");
        while (!IsDiscordRunning())
        {
            await Task.Delay(1000);
        }

        await Task.Delay(3000);

        SetPresenceFromConfig();
    }


    private void SetPresenceFromConfig()
    {
        var config = LoadConfiguration();
        if (config == null) return;

        try
        {
            ulong clientId = ulong.Parse(config["Identifiers"]["ClientID"]);
            _client = new DiscordRpcClient(clientId.ToString());
            _client.Initialize();

            var presence = new RichPresence
            {
                Details = config["State"]["Details"],
                State = config["State"]["State"],
                Assets = new Assets
                {
                    LargeImageKey = config["Images"]["LargeImage"],
                    LargeImageText = config["Images"]["LargeImageTooltip"],
                    SmallImageKey = config["Images"]["SmallImage"],
                    SmallImageText = config["Images"]["SmallImageTooltip"]
                },
                Timestamps = new Timestamps
                {
                    StartUnixMilliseconds = GetDateTimeValue(config, "State", "StartTimestamp"),
                    EndUnixMilliseconds = GetDateTimeValue(config, "State", "EndTimestamp")
                }
            };

            if (GetButton(config, "Buttons", "Button1Text", "Button1Link", out var button1))
                presence.Buttons = new[] { button1 };

            if (GetButton(config, "Buttons", "Button2Text", "Button2Link", out var button2))
                presence.Buttons = presence.Buttons == null ? new[] { button2 } : new[] { presence.Buttons[0], button2 };

            if (int.TryParse(config["Party"]["Size"], out int partySize) && int.TryParse(config["Party"]["Max"], out int partyMax))
            {
                presence.Party = new Party
                {
                    Size = partySize,
                    Max = partyMax,
                    ID = "ae488379-351d-4a4f-ad32-2b9b01c91657"
                };
            }

            _client.SetPresence(presence);
            NotifyUser("Presence set successfully?");
        }
        catch (Exception ex)
        {
            NotifyUser($"Error setting presence: {ex.Message}");
        }
    }

    private IniData LoadConfiguration()
    {
        var parser = new FileIniDataParser();
        try
        {
            return parser.ReadFile(ConfigFilePath);
        }
        catch (Exception e)
        {
            NotifyUser($"Error reading config: {e.Message}");
            return null;
        }
    }

    private void RefreshPresence()
    {
        _client?.ClearPresence();
        InitializePresence();
    }

    private void NotifyUser(string message)
    {
        new ToastContentBuilder()
            .AddText("Stargazer's Rich Presence")
            .AddText(message)
            .Show();
    }


    private void CreateDefaultConfig()
    {
        var defaultConfig = @"
[Identifiers]
ClientID=

[State]
Details=
State=
StartTimestamp=
EndTimestamp=

[Images]
LargeImage=
LargeImageTooltip=
SmallImage=
SmallImageTooltip=

[Buttons]
Button1Text=
Button1Link=
Button2Text=
Button2Link=

[Party]
Size=
Max=";

        File.WriteAllText(ConfigFilePath, defaultConfig);
    }
    private ulong? GetDateTimeValue(IniData config, string section, string key)
    {
        var value = config[section][key];
        if (string.IsNullOrEmpty(value)) return null;
        if (value.ToLower() == "now") return (ulong)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        return DateTime.TryParse(value, out var dateTime) ? (ulong?)new DateTimeOffset(dateTime).ToUnixTimeMilliseconds() : null;
    }

    private bool GetButton(IniData config, string section, string textKey, string linkKey, out DiscordRPC.Button button)
    {
        var text = config[section][textKey];
        var url = config[section][linkKey];
        if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(url))
        {
            button = new DiscordRPC. Button { Label = text, Url = url };
            return true;
        }
        button = null;
        return false;
    }

    private void ExitApplication()
    {
        _client?.Dispose();
        System.Windows.Forms.Application.Exit();
    }

    [STAThread]
    public static void Main()
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        System.Windows.Forms.Application.Run(new Form1());
    }
}

class DiscordThemeRenderer : ToolStripProfessionalRenderer
{
    private Color CheckBackgroundColor = Color.FromArgb(88, 101, 242); // #5865F2
    private Color MenuItemBackgroundColor = Color.FromArgb(54, 57, 62); // #36393e
    private Color HoverBackgroundColor = Color.FromArgb(66, 69, 73); // #424549
    private Color SeparatorColor = Color.FromArgb(40, 43, 48); // #282b30


    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        e.Graphics.FillRectangle(new SolidBrush(CheckBackgroundColor), e.ImageRectangle);

        if (((ToolStripMenuItem)e.Item).Checked)
        {
            Point[] checkPoints =
            {
                new Point(e.ImageRectangle.Left + 4, e.ImageRectangle.Top + e.ImageRectangle.Height / 2),
                new Point(e.ImageRectangle.Left + e.ImageRectangle.Width / 3, e.ImageRectangle.Bottom - 4),
                new Point(e.ImageRectangle.Right - 4, e.ImageRectangle.Top + 4)
            };
            e.Graphics.DrawLines(new Pen(Brushes.White, 2), checkPoints);
        }
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            Rectangle rectangle = new Rectangle(Point.Empty, e.Item.Size);
            e.Graphics.FillRectangle(new SolidBrush(HoverBackgroundColor), rectangle);
        }
        else
        {
            Rectangle rectangle = new Rectangle(Point.Empty, e.Item.Size);
            e.Graphics.FillRectangle(new SolidBrush(MenuItemBackgroundColor), rectangle);
        }
    }


    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.FillRectangle(new SolidBrush(MenuItemBackgroundColor), e.AffectedBounds);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var rectangle = new Rectangle(Point.Empty, e.Item.Size);
        rectangle.Y += (rectangle.Height / 2) - 1;
        rectangle.Height = 1;
        e.Graphics.FillRectangle(new SolidBrush(SeparatorColor), rectangle);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        e.Graphics.FillRectangle(new SolidBrush(MenuItemBackgroundColor), e.AffectedBounds);
    }
}



