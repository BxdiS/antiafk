using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Media = System.Windows.Media;

namespace AntiAfk.App.Logging;

public partial class LogConsoleWindow : Window
{
    private const int MaxDocumentLines = 2500;

    private readonly string _logFilePath;
    private readonly DispatcherTimer _tailTimer;
    private long _filePosition;
    private int _lineCount;

    private static readonly Media.Brush InfoBrush = new Media.SolidColorBrush(Media.Color.FromRgb(0xC9, 0xD1, 0xD9));
    private static readonly Media.Brush WarnBrush = new Media.SolidColorBrush(Media.Color.FromRgb(0xD2, 0x99, 0x22));
    private static readonly Media.Brush ErrorBrush = new Media.SolidColorBrush(Media.Color.FromRgb(0xF8, 0x51, 0x49));
    private static readonly Media.Brush MutedBrush = new Media.SolidColorBrush(Media.Color.FromRgb(0x8B, 0x94, 0x9E));

    static LogConsoleWindow()
    {
        InfoBrush.Freeze();
        WarnBrush.Freeze();
        ErrorBrush.Freeze();
        MutedBrush.Freeze();
    }

    public LogConsoleWindow(string logFilePath)
    {
        InitializeComponent();
        _logFilePath = logFilePath;

        LoadExistingContent();

        _tailTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _tailTimer.Tick += (_, _) => TailNewContent();
        _tailTimer.Start();

        Closed += (_, _) => _tailTimer.Stop();
    }

    private void LoadExistingContent()
    {
        if (!File.Exists(_logFilePath))
        {
            return;
        }

        using var stream = OpenSharedReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        AppendTextBlock(reader.ReadToEnd());
        _filePosition = stream.Length;
    }

    private void TailNewContent()
    {
        if (!File.Exists(_logFilePath))
        {
            return;
        }

        using var stream = OpenSharedReadStream();
        if (stream.Length < _filePosition)
        {
            LogView.Document.Blocks.Clear();
            _lineCount = 0;
            _filePosition = 0;
        }

        stream.Seek(_filePosition, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var chunk = reader.ReadToEnd();
        _filePosition = stream.Length;

        if (chunk.Length > 0)
        {
            AppendTextBlock(chunk);
        }
    }

    private FileStream OpenSharedReadStream() =>
        new(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

    private void AppendTextBlock(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var normalized = text.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var isLastPartialLine = i == lines.Length - 1 && !normalized.EndsWith('\n');
            if (isLastPartialLine && string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (i < lines.Length - 1 || normalized.EndsWith('\n'))
            {
                AppendLine(line);
            }
            else if (!string.IsNullOrEmpty(line))
            {
                AppendPartialLine(line);
            }
        }

        LogView.ScrollToEnd();
        TrimIfNeeded();
    }

    private void AppendLine(string line)
    {
        var paragraph = new Paragraph(new Run(string.IsNullOrEmpty(line) ? " " : line))
        {
            Margin = new Thickness(0),
            LineHeight = 18,
            Foreground = GetLineBrush(line)
        };

        LogView.Document.Blocks.Add(paragraph);
        _lineCount++;
    }

    private void AppendPartialLine(string line)
    {
        if (LogView.Document.Blocks.LastBlock is Paragraph { Inlines.Count: > 0 } paragraph
            && paragraph.Inlines.LastInline is Run run)
        {
            run.Text += line;
            paragraph.Foreground = GetLineBrush(run.Text);
            return;
        }

        AppendLine(line);
    }

    private static Media.Brush GetLineBrush(string line)
    {
        if (line.Contains("[ERROR]", StringComparison.Ordinal))
        {
            return ErrorBrush;
        }

        if (line.Contains("[WARN]", StringComparison.Ordinal))
        {
            return WarnBrush;
        }

        if (line.StartsWith("--- Session", StringComparison.Ordinal))
        {
            return MutedBrush;
        }

        return InfoBrush;
    }

    private void TrimIfNeeded()
    {
        while (_lineCount > MaxDocumentLines && LogView.Document.Blocks.Count > 0)
        {
            LogView.Document.Blocks.Remove(LogView.Document.Blocks.FirstBlock);
            _lineCount--;
        }
    }
}
