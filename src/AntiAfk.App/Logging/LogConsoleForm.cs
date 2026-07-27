using AntiAfk.Core.Abstractions;

namespace AntiAfk.App.Logging;

public sealed class LogConsoleForm : Form
{
    private const int MaxDocumentLines = 2500;
    private const int LoadBatchSize = 100;

    private static readonly Color InfoColor = Color.FromArgb(0xC9, 0xD1, 0xD9);
    private static readonly Color WarnColor = Color.FromArgb(0xD2, 0x99, 0x22);
    private static readonly Color ErrorColor = Color.FromArgb(0xF8, 0x51, 0x49);
    private static readonly Color MutedColor = Color.FromArgb(0x8B, 0x94, 0x9E);

    private readonly IAppLogger _logger;
    private readonly RichTextBox _logView;
    private Font? _consoleFont;
    private int _lineCount;
    private bool _initialLoadComplete;

    public LogConsoleForm(IAppLogger logger)
    {
        try
        {
            _logger = logger;
            _initialLoadComplete = false;

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

            _consoleFont = new Font("Consolas", 10f);

            _logView = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(0x0D, 0x11, 0x17),
                ForeColor = InfoColor,
                Font = _consoleFont,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };
            Controls.Add(_logView);

            // Load buffer asynchronously after form is shown
            this.Shown += async (_, _) => await LoadBufferAsync();

            _logger.LineLogged += OnLineLogged;
            FormClosed += (_, _) =>
            {
                try
                {
                    _logger.LineLogged -= OnLineLogged;
                }
                catch { }

                _consoleFont?.Dispose();
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LogConsoleForm constructor error: {ex.Message}");
            throw;
        }
    }

    private async Task LoadBufferAsync()
    {
        try
        {
            var buffer = _logger.Buffer;
            if (buffer == null || buffer.Count == 0)
            {
                _initialLoadComplete = true;
                return;
            }

            // Load in batches to avoid blocking UI thread
            for (int i = 0; i < buffer.Count; i += LoadBatchSize)
            {
                if (_logView.IsDisposed)
                    return;

                try
                {
                    int end = Math.Min(i + LoadBatchSize, buffer.Count);
                    AppendRun(buffer, i, end);

                    // Yields to the message loop on its own - awaiting here returns control to the
                    // pump and resumes after it. Application.DoEvents() used to follow this call
                    // and was the reentrancy hazard: it pumped messages from *inside* the load
                    // loop, so a log line arriving mid-load ran OnLineLogged -> AppendLine and
                    // modified the RichTextBox underneath the loop that was still filling it.
                    await Task.Delay(10);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading batch: {ex.Message}");
                }
            }

            _initialLoadComplete = true;

            if (!_logView.IsDisposed)
            {
                _logView.SelectionStart = _logView.TextLength;
                _logView.ScrollToCaret();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading log buffer: {ex.Message}");
            _initialLoadComplete = true;
        }
    }

    private void OnLineLogged(string line)
    {
        try
        {
            if (IsDisposed || _logView?.IsDisposed == true)
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
        catch (ObjectDisposedException)
        {
            // Form was disposed
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LogConsoleForm.OnLineLogged error: {ex.Message}");
        }
    }

    private void AppendLine(string line)
    {
        try
        {
            if (_logView?.IsDisposed == true || IsDisposed)
            {
                return;
            }

            if (_logView?.Created != true)
            {
                return;
            }

            AppendLineInternal(line);
        }
        catch (ObjectDisposedException)
        {
            // Form or RichTextBox was disposed
        }
        catch (InvalidOperationException)
        {
            // Control accessed from non-UI thread
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LogConsoleForm.AppendLine error: {ex.Message}");
        }
    }

    /// <summary>
    /// Appends buffer[start..end) grouping consecutive same-coloured lines into one AppendText.
    /// The initial load is the one place that writes thousands of lines in a row, and one
    /// RichTextBox operation per line is what made opening the console slow.
    /// </summary>
    private void AppendRun(IReadOnlyList<string> buffer, int start, int end)
    {
        var runStart = start;

        while (runStart < end)
        {
            var color = GetLineColor(buffer[runStart]);
            var runEnd = runStart + 1;
            while (runEnd < end && GetLineColor(buffer[runEnd]) == color)
            {
                runEnd++;
            }

            var text = new System.Text.StringBuilder();
            for (var i = runStart; i < runEnd; i++)
            {
                text.Append(buffer[i]).Append(Environment.NewLine);
            }

            if (_logView.IsDisposed)
            {
                return;
            }

            _logView.SelectionStart = _logView.TextLength;
            _logView.SelectionLength = 0;
            _logView.SelectionColor = color;
            _logView.AppendText(text.ToString());
            _lineCount += runEnd - runStart;

            runStart = runEnd;
        }

        TrimIfNeeded();
    }

    private void AppendLineInternal(string line)
    {
        if (_logView == null || _logView.IsDisposed)
            return;

        _logView.SelectionStart = _logView.TextLength;
        _logView.SelectionLength = 0;
        _logView.SelectionColor = GetLineColor(line);
        _logView.AppendText(line + Environment.NewLine);
        _lineCount++;

        TrimIfNeeded();

        // Only scroll to end if initial load is complete and we're accepting new logs
        if (_initialLoadComplete && _logView.Created)
        {
            _logView.SelectionStart = _logView.TextLength;
            _logView.ScrollToCaret();
        }
    }

    private void TrimIfNeeded()
    {
        try
        {
            if (_lineCount <= MaxDocumentLines || _logView?.IsDisposed == true || _logView == null)
            {
                return;
            }

            // Trims down to the cap in one edit rather than dropping a single line per append.
            // AppendRun can add a whole batch at once, so removing one line per call would not keep
            // up with it.
            var excess = _lineCount - MaxDocumentLines;

            // GetFirstCharIndexFromLine returns the offset of that line directly, so the cut point
            // costs nothing to find. Reading _logView.Text instead, as this used to, materialised
            // the entire document into a string on every append past the cap - the whole log copied
            // per line, which is what made heavy logging drag the UI down.
            var cutTo = _logView.GetFirstCharIndexFromLine(excess);
            if (cutTo <= 0)
            {
                return;
            }

            _logView.Select(0, cutTo);
            _logView.SelectedText = string.Empty;
            _lineCount -= excess;
        }
        catch (ObjectDisposedException)
        {
            // RichTextBox was disposed
        }
        catch (InvalidOperationException)
        {
            // Control accessed from wrong thread
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LogConsoleForm.TrimIfNeeded error: {ex.Message}");
        }
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
