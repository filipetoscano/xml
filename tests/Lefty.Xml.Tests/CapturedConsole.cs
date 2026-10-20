using Spectre.Console;

namespace Lefty.Xml.Tests;

/// <summary>
/// Console which records what a command writes, without colour and without
/// wrapping lines.
/// </summary>
internal sealed class CapturedConsole
{
    private readonly StringWriter _writer = new StringWriter();


    /// <summary />
    public IAnsiConsole Console { get; }


    /// <summary />
    public CapturedConsole()
    {
        Console = AnsiConsole.Create( new AnsiConsoleSettings()
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput( _writer ),
        } );

        Console.Profile.Width = 10_000;
    }


    /// <summary>
    /// Everything written so far, with line endings normalized to LF.
    /// </summary>
    public string Text => _writer.ToString().ReplaceLineEndings( "\n" );


    /// <summary />
    public string[] Lines => Text.Split( '\n', StringSplitOptions.RemoveEmptyEntries );
}