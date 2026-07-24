using AntiAfk.Core.Abstractions;

namespace AntiAfk.App.Logging;

public sealed class LogConsoleForm : Form
{
    private const int MaxDocumentLines = 2500;

    private static readonly Color InfoColor = Color.FromArgb(0xC9, 0xD1, 0xD9);
    private static readonly Color WarnColor = Color.FromArgb(0xD2, 0x99, 0x22);
    private static readonly Color ErrorColor = Color.FromArgb(0xF8, 0x51, 0x49);
    private static readonly Color MutedColor = Color.FromArgb(0x8B, 0x94, 0x9E);

    private readonly IAppLogger _logger;
    private readonly RichTextBox _logView;
    private int _lineCount;

    public LogConsoleForm(IAppLogger logger)
    {
        _logger = logger;

        Text = "AntiAFK — Logs";
        Width = 820;
        Height = 520;
        MinimumSize = new Size(480, 280);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(0x0D, 0x11, 0x17);

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // ignored - icon is cosmetic only
        }

        _logView = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0x0D, 0x11, 0x17),
            ForeColor = InfoColor,
            Font = new Font("Consolas", 10f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both
        };
        Controls.Add(_logView);

        foreach (var line in _logger.Buffer)
        {
            AppendLine(line);
        }

        _logger.LineLogged += OnLineLogged;
        FormClosed += (_, _) => _logger.LineLogged -= OnLineLogged;
    }

    private void OnLineLogged(string line)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLine(line));
            return;
        }

        AppendLine(line);
    }

    private void AppendLine(string line)
    {
        _logView.SelectionStart = _logView.TextLength;
        _logView.SelectionLength = 0;
        _logView.SelectionColor = GetLineColor(line);
        _logView.AppendText(line + Environment.NewLine);
        _lineCount++;

        TrimIfNeeded();

        _logView.SelectionStart = _logView.TextLength;
        _logView.ScrollToCaret();
    }

    private void TrimIfNeeded()
    {
        if (_lineCount <= MaxDocumentLines)
        {
            return;
        }

        var firstLineEnd = _logView.Text.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return;
        }

        _logView.Select(0, firstLineEnd + 1);
        _logView.SelectedText = string.Empty;
        _lineCount--;
    }

    private static Color GetLineColor(string line)
    {
        if (line.Contains("[ERROR]", StringComparison.Ordinal))
        {
            return ErrorColor;
        }

        if (line.Contains("[WARN]", StringComparison.Ordinal))
        {
            return WarnColor;
        }

        if (line.StartsWith("--- Session", StringComparison.Ordinal))
        {
            return MutedColor;
        }

        return InfoColor;
    }
}
