using EditorConfig.Core;
using System.Text;

namespace Lefty.Xml;

/// <summary>
/// The subset of .editorconfig settings which the XML formatter acts upon.
/// </summary>
public sealed record XmlFormatOptions
{
    /// <summary>
    /// String used for a single level of indentation.
    /// </summary>
    public required string IndentChars { get; init; }

    /// <summary>
    /// Line ending to emit.
    /// </summary>
    public required string NewLine { get; init; }

    /// <summary>
    /// Encoding to write the file in. When null, the encoding detected on the
    /// input file is kept.
    /// </summary>
    public Encoding? Charset { get; init; }

    /// <summary>
    /// Whether the file should end in a newline. When null, whatever the input
    /// file did is kept.
    /// </summary>
    public bool? InsertFinalNewline { get; init; }

    /// <summary>
    /// Whether trailing whitespace should be stripped from every line.
    /// </summary>
    public bool TrimTrailingWhitespace { get; init; }


    /// <summary>
    /// Builds the options from a resolved .editorconfig configuration. Returns null
    /// -- naming the missing key in <paramref name="missing" /> -- when the settings
    /// which decide how the file gets rewritten were not declared: the formatter
    /// refuses to guess those.
    /// </summary>
    public static XmlFormatOptions? TryCreate( FileConfiguration config, out string? missing )
    {
        missing = null;


        /*
         * indent_style decides between tabs and spaces; for spaces we also need to
         * know how many. indent_size = tab means "use tab_width".
         */
        if ( config.IndentStyle.HasValue == false )
        {
            missing = "indent_style";
            return null;
        }

        string indentChars;

        if ( config.IndentStyle.Value == IndentStyle.Tab )
        {
            indentChars = "\t";
        }
        else
        {
            IndentSize? size = config.IndentSize;
            int? columns = null;

            if ( size != null && size.IsUnset == false && size.UseTabWidth == false )
                columns = size.NumberOfColumns;

            columns ??= config.TabWidth;

            if ( columns.HasValue == false || columns.Value < 1 )
            {
                missing = "indent_size";
                return null;
            }

            indentChars = new string( ' ', columns.Value );
        }


        /*
         * end_of_line has no defensible default: it differs per platform and per
         * repository, and getting it wrong rewrites every line of every file.
         */
        if ( config.EndOfLine.HasValue == false )
        {
            missing = "end_of_line";
            return null;
        }

        string newLine = config.EndOfLine.Value switch
        {
            EndOfLine.LF => "\n",
            EndOfLine.CR => "\r",
            EndOfLine.CRLF => "\r\n",
            _ => "\n",
        };


        /*
         * The remainder are optional: when they are absent we can preserve what the
         * input file already does, which is not a guess.
         */
        return new XmlFormatOptions
        {
            IndentChars = indentChars,
            NewLine = newLine,
            Charset = ToEncoding( config.Charset ),
            InsertFinalNewline = config.InsertFinalNewline,
            TrimTrailingWhitespace = config.TrimTrailingWhitespace ?? false,
        };
    }


    /// <summary />
    private static Encoding? ToEncoding( Charset? charset )
    {
        return charset switch
        {
            EditorConfig.Core.Charset.Latin1 => Encoding.Latin1,
            EditorConfig.Core.Charset.UTF8 => new UTF8Encoding( false ),
            EditorConfig.Core.Charset.UTF8BOM => new UTF8Encoding( true ),
            EditorConfig.Core.Charset.UTF16LE => new UnicodeEncoding( false, true ),
            EditorConfig.Core.Charset.UTF16BE => new UnicodeEncoding( true, true ),
            _ => null,
        };
    }
}
