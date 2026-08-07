using EditorConfig.Core;
using McMaster.Extensions.CommandLineUtils;
using Spectre.Console;
using System.ComponentModel.DataAnnotations;
using System.Xml;

namespace Lefty.Xml;

/// <summary />
[Command( "format", Description = "Formats XML file based on editorconfig" )]
public class FormatCommand
{
    /// <summary />
    [Argument( 0, Description = "Input files" )]
    [Required]
    public string[] Patterns { get; set; } = default!;

    /// <summary />
    [Option( "-k|--check", CommandOptionType.NoValue, Description = "Report files which need formatting, without writing them" )]
    public bool Check { get; set; }


    /// <summary />
    public async Task<int> OnExecuteAsync()
    {
        IReadOnlyList<string> files = FileGlob.Resolve( Patterns, out IReadOnlyList<string> unmatched );

        foreach ( string pattern in unmatched )
            AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: no files matched '{pattern}'" );

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
                AnsiConsole.MarkupLineInterpolated( $"[yellow]skp[/]: {name} - no .editorconfig section sets '{missing}'" );
                skipped++;
                continue;
            }

            byte[] source = await File.ReadAllBytesAsync( file );
            byte[] formatted;

            try
            {
                formatted = XmlFormatter.Format( source, options );
            }
            catch ( XmlException ex )
            {
                AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: {name} - not well formed: {ex.Message}" );
                failed++;
                continue;
            }

            if ( source.AsSpan().SequenceEqual( formatted ) == true )
                continue;

            changed++;

            if ( Check == true )
            {
                AnsiConsole.MarkupLineInterpolated( $"[yellow]chg[/]: {name}" );
            }
            else
            {
                await File.WriteAllBytesAsync( file, formatted );
                AnsiConsole.MarkupLineInterpolated( $"[green]fmt[/]: {name}" );
            }
        }


        /*
         * A skipped file is a failure to do what was asked: without it, running over a
         * tree which declares no XML section would report success having done nothing.
         */
        AnsiConsole.MarkupLineInterpolated(
            $"     {files.Count} file(s), {changed} {( Check == true ? "to format" : "formatted" )}, {skipped} skipped, {failed} failed" );

        if ( changed > 0 && Check == true )
            return 1;

        if ( skipped > 0 || failed > 0 || unmatched.Count > 0 )
            return 1;

        return 0;
    }
}
