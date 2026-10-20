using EditorConfig.Core;
using McMaster.Extensions.CommandLineUtils;
using Spectre.Console;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Xml;
using AllowedValues = McMaster.Extensions.CommandLineUtils.AllowedValuesAttribute;

namespace Lefty.Xml;

/// <summary />
[Command( "format", Description = "Formats XML file based on editorconfig" )]
public class FormatCommand
{
    private const string Title = "lxml format";

    private readonly IAnsiConsole _console;


    /// <summary />
    [Argument( 0, Description = "Input files" )]
    [Required]
    public string[] Patterns { get; set; } = default!;

    /// <summary />
    [Option( "-k|--check", CommandOptionType.NoValue, Description = "Report files which need formatting, without writing them" )]
    public bool Check { get; set; }

    /// <summary />
    [Option( "-d|--diff", CommandOptionType.NoValue, Description = "Show the changes formatting makes to each file, as a unified diff" )]
    public bool Diff { get; set; }

    /// <summary />
    [Option( "-o|--output", CommandOptionType.SingleValue, Description = "Output format: text, or github to also annotate problems in GitHub Actions" )]
    [AllowedValues( "text", "github", IgnoreCase = true )]
    public string Output { get; set; } = "text";


    /// <summary />
    public FormatCommand( IAnsiConsole console )
    {
        _console = console;
    }


    /// <summary />
    private bool GitHub => string.Equals( Output, "github", StringComparison.OrdinalIgnoreCase );


    /// <summary />
    public async Task<int> OnExecuteAsync()
    {
        IReadOnlyList<string> files = FileGlob.Resolve( Patterns, out IReadOnlyList<string> unmatched );

        foreach ( string pattern in unmatched )
        {
            _console.MarkupLineInterpolated( $"[red]err[/]: no files matched '{pattern}'" );
            Annotate( null, $"No files matched '{pattern}'." );
        }

        if ( files.Count == 0 )
            return 1;


        /*
         * The parser walks up to the nearest root=true .editorconfig, caches the files
         * it reads, and picks the section whose glob matches -- so resolving the
         * configuration for a file is a single call.
         */
        var parser = new EditorConfigParser();

        int changed = 0;
        int skipped = 0;
        int failed = 0;

        foreach ( string file in files )
        {
            string name = Path.GetRelativePath( Directory.GetCurrentDirectory(), file );

            FileConfiguration config = parser.Parse( file );
            XmlFormatOptions? options = XmlFormatOptions.TryCreate( config, out string? missing );

            if ( options == null )
            {
                _console.MarkupLineInterpolated( $"[yellow]skp[/]: {name} - no .editorconfig section sets '{missing}'" );
                Annotate( name, $"Not formatted: no .editorconfig section sets '{missing}'." );
                skipped++;
                continue;
            }

            /*
             * A file which cannot be read or written fails on its own, rather than
             * aborting the run and leaving the remaining files untouched.
             */
            byte[] source;

            try
            {
                source = await File.ReadAllBytesAsync( file );
            }
            catch ( Exception ex ) when ( ex is IOException || ex is UnauthorizedAccessException )
            {
                _console.MarkupLineInterpolated( $"[red]err[/]: {name} - cannot read: {ex.Message}" );
                Annotate( name, $"Cannot read: {ex.Message}" );
                failed++;
                continue;
            }

            byte[] formatted;

            try
            {
                formatted = XmlFormatter.Format( source, options );
            }
            catch ( XmlException ex )
            {
                _console.MarkupLineInterpolated( $"[red]err[/]: {name} - not well formed: {ex.Message}" );
                Annotate( name, $"Not well formed: {ex.Message}", ex.LineNumber, ex.LinePosition );
                failed++;
                continue;
            }

            if ( source.AsSpan().SequenceEqual( formatted ) == true )
                continue;

            if ( Check == true )
            {
                _console.MarkupLineInterpolated( $"[yellow]chg[/]: {name}" );
                Annotate( name, "File is not formatted; run 'lxml format' on it." );
                WriteDiff( name, source, formatted, options );
                changed++;
                continue;
            }

            try
            {
                await File.WriteAllBytesAsync( file, formatted );
            }
            catch ( Exception ex ) when ( ex is IOException || ex is UnauthorizedAccessException )
            {
                _console.MarkupLineInterpolated( $"[red]err[/]: {name} - cannot write: {ex.Message}" );
                Annotate( name, $"Cannot write: {ex.Message}" );
                failed++;
                continue;
            }

            _console.MarkupLineInterpolated( $"[green]fmt[/]: {name}" );
            WriteDiff( name, source, formatted, options );
            changed++;
        }


        /*
         * A skipped file is a failure to do what was asked: without it, running over a
         * tree which declares no XML section would report success having done nothing.
         */
        _console.MarkupLineInterpolated(
            $"     {files.Count} file(s), {changed} {( Check == true ? "to format" : "formatted" )}, {skipped} skipped, {failed} failed" );

        if ( changed > 0 && Check == true )
            return 1;

        if ( skipped > 0 || failed > 0 || unmatched.Count > 0 )
            return 1;

        return 0;
    }


    /// <summary />
    private void Annotate( string? name, string message, int line = 0, int column = 0 )
    {
        if ( GitHub == true )
            _console.Profile.Out.Writer.WriteLine( GitHubAnnotation.Error( Title, name, line, column, message ) );
    }


    /// <summary>
    /// Writes the diff from <paramref name="source" /> to <paramref name="formatted" />,
    /// when asked for. Lines are written exactly as the diff has them, line endings
    /// included, so that the output can be fed to git apply -- not as markup, which
    /// would wrap them at the console width.
    /// </summary>
    private void WriteDiff( string name, byte[] source, byte[] formatted, XmlFormatOptions options )
    {
        if ( Diff == false )
            return;

        Encoding encoding = XmlFormatter.DetectEncoding( source );

        string before = Decode( source, encoding );
        string after = Decode( formatted, options.Charset ?? encoding );


        /*
         * A change no line of the diff could show is described instead: the diff
         * would be empty, or else every line would look the same on both sides.
         */
        if ( before == after )
        {
            _console.MarkupLine( "[grey]     only the encoding or byte order mark changes[/]" );
            return;
        }

        if ( before.ReplaceLineEndings( "\n" ) == after.ReplaceLineEndings( "\n" ) )
        {
            _console.MarkupLine( "[grey]     only line endings change[/]" );
            return;
        }

        string path = name.Replace( '\\', '/' );
        bool ansi = _console.Profile.Capabilities.Ansi;
        TextWriter writer = _console.Profile.Out.Writer;

        IReadOnlyList<string> lines = UnifiedDiff.Create( before, after, "a/" + path, "b/" + path );

        for ( int i = 0; i < lines.Count; i++ )
        {
            string line = lines[ i ];

            /*
             * The first two lines are the ---/+++ header, not a removal and an addition.
             */
            string? color = line[ 0 ] switch
            {
                _ when i < 2 => "1",
                '-' => "31",
                '+' => "32",
                '@' => "36",
                _ => null,
            };

            if ( ansi == true && color != null )
            {
                string content = line.TrimEnd( '\r', '\n' );
                writer.Write( "\x1b[" + color + "m" + content + "\x1b[0m" + line[ content.Length.. ] );
            }
            else
            {
                writer.Write( line );
            }
        }
    }


    /// <summary />
    private static string Decode( byte[] bytes, Encoding encoding )
    {
        ReadOnlySpan<byte> content = bytes;
        ReadOnlySpan<byte> preamble = encoding.GetPreamble();

        if ( content.StartsWith( preamble ) == true )
            content = content[ preamble.Length.. ];

        return encoding.GetString( content );
    }
}