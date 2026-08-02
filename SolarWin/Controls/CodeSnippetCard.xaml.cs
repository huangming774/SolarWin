using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace SolarWin.Controls;

public sealed partial class CodeSnippetCard : UserControl
{
    private const int MaxRenderedCharacters = 100_000;
    private const int MaxHighlightedCharacters = 24_000;
    private const int MaxHighlightedTokens = 1_200;

    private static readonly Regex TokenRegex = new(
        @"(?<comment>//[^\r\n]*|/\*[\s\S]*?\*/|#[^\r\n]*)"
        + @"|(?<string>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`)"
        + @"|(?<number>\b(?:0[xX][0-9a-fA-F]+|\d+(?:\.\d+)?)\b)"
        + @"|(?<keyword>\b(?:abstract|async|await|base|bool|break|case|catch|class|const|continue|def|delete|do|double|else|enum|export|extends|false|finally|float|for|foreach|from|function|if|implements|import|in|int|interface|internal|is|let|namespace|new|null|object|of|override|package|private|protected|public|readonly|record|return|sealed|select|static|string|struct|switch|this|throw|true|try|typeof|using|var|virtual|void|while|with|yield)\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Brush PlainBrush = BrushFromArgb(0xFF, 0xD7, 0xDE, 0xE9);
    private static readonly Brush KeywordBrush = BrushFromArgb(0xFF, 0xC6, 0x78, 0xDD);
    private static readonly Brush StringBrush = BrushFromArgb(0xFF, 0x98, 0xC3, 0x79);
    private static readonly Brush NumberBrush = BrushFromArgb(0xFF, 0xD1, 0x9A, 0x66);
    private static readonly Brush CommentBrush = BrushFromArgb(0xFF, 0x7F, 0x89, 0x98);

    public CodeSnippetCard()
    {
        InitializeComponent();
        RenderCode();
    }

    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(
        nameof(Code),
        typeof(string),
        typeof(CodeSnippetCard),
        new PropertyMetadata(string.Empty, OnSnippetChanged));

    public static readonly DependencyProperty CodeLanguageProperty = DependencyProperty.Register(
        nameof(CodeLanguage),
        typeof(string),
        typeof(CodeSnippetCard),
        new PropertyMetadata(string.Empty, OnSnippetChanged));

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public string CodeLanguage
    {
        get => (string)GetValue(CodeLanguageProperty);
        set => SetValue(CodeLanguageProperty, value);
    }

    private static void OnSnippetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is CodeSnippetCard card && card.CodeText is not null)
        {
            card.RenderCode();
        }
    }

    private void RenderCode()
    {
        if (CodeText is null || LanguageLabel is null)
        {
            return;
        }

        LanguageLabel.Text = FormatLanguage(CodeLanguage);
        CopyButton.Content = "复制";
        CodeText.Blocks.Clear();

        var paragraph = new Paragraph { Margin = new Thickness(0) };
        var rawSource = string.IsNullOrEmpty(Code) ? " " : Code;
        var wasTruncated = rawSource.Length > MaxRenderedCharacters;
        var source = (wasTruncated ? rawSource[..MaxRenderedCharacters] : rawSource)
            .Replace("\t", "    ");
        if (wasTruncated)
        {
            source += "\n\n… 代码过长，卡片仅显示前 100,000 个字符；复制按钮仍会复制完整代码。";
        }

        if (source.Length > MaxHighlightedCharacters)
        {
            // Keep the full visible text selectable, but avoid thousands of Run elements.
            paragraph.Inlines.Add(CreateRun(source, PlainBrush));
            CodeText.Blocks.Add(paragraph);
            return;
        }

        var cursor = 0;
        var tokenCount = 0;
        foreach (Match match in TokenRegex.Matches(source))
        {
            if (tokenCount++ >= MaxHighlightedTokens)
            {
                paragraph.Inlines.Add(CreateRun(source[cursor..], PlainBrush));
                cursor = source.Length;
                break;
            }

            if (match.Index > cursor)
            {
                paragraph.Inlines.Add(CreateRun(source[cursor..match.Index], PlainBrush));
            }

            var brush = match.Groups["comment"].Success
                ? CommentBrush
                : match.Groups["string"].Success
                    ? StringBrush
                    : match.Groups["number"].Success
                        ? NumberBrush
                        : KeywordBrush;
            paragraph.Inlines.Add(CreateRun(match.Value, brush));
            cursor = match.Index + match.Length;
        }

        if (cursor < source.Length)
        {
            paragraph.Inlines.Add(CreateRun(source[cursor..], PlainBrush));
        }

        CodeText.Blocks.Add(paragraph);
    }

    private void CopyButton_OnClick(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(Code ?? string.Empty);
        Clipboard.SetContent(package);
        Clipboard.Flush();
        CopyButton.Content = "已复制";
    }

    private static Run CreateRun(string text, Brush foreground) => new()
    {
        Text = text,
        Foreground = foreground,
    };

    private static string FormatLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "CODE";
        }

        return language.Trim().ToLowerInvariant() switch
        {
            "cs" or "csharp" => "C#",
            "js" or "javascript" => "JAVASCRIPT",
            "ts" or "typescript" => "TYPESCRIPT",
            "py" or "python" => "PYTHON",
            "ps1" or "powershell" => "POWERSHELL",
            "sh" or "bash" or "shell" => "SHELL",
            "html" => "HTML",
            "xml" or "xaml" => "XAML",
            "json" => "JSON",
            "sql" => "SQL",
            var value => value.ToUpperInvariant(),
        };
    }

    private static Brush BrushFromArgb(byte a, byte r, byte g, byte b) =>
        new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(a, r, g, b));
}
