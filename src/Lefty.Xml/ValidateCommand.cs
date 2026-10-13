using McMaster.Extensions.CommandLineUtils;
using Spectre.Console;
using System.ComponentModel.DataAnnotations;
using System.Xml.Schema;

namespace Lefty.Xml;

/// <summary />
[Command( "validate", Description = "Validates XML file against an XSD schema" )]
public class ValidateCommand
{
    /// <summary />
    [Argument( 0, Description = "Input files" )]
    [Required]
    public string[] Patterns { get; set; } = default!;

    /// <summary />
    [Option( "-s|--schema", CommandOptionType.SingleValue, Description = "XSD schema, instead of the one named by each file's xsi:schemaLocation" )]
    [FileExists]
    public string? Schema { get; set; }

    /// <summary />
    [Option( "-e|--errors", CommandOptionType.NoValue, Description = "Print every validation error, rather than only a count" )]
    public bool EmitErrors { get; set; }


    /// <summary />
    public async Task<int> OnExecuteAsync()
    {
        IReadOnlyList<string> files = FileGlob.Resolve( Patterns, out IReadOnlyList<string> unmatched );

        foreach ( string pattern in unmatched )
            AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: no files matched '{pattern}'" );

        if ( files.Count == 0 )
            return 1;


        /*
         * An explicit schema is loaded once, up front: if it is broken, then no file
         * can be validated against it and there is no point in trying.
         */
        XmlSchemaSet? schemas = null;

        if ( Schema != null )
        {
            try
            {
                schemas = XmlValidator.LoadSchemas( [ Schema ] );
            }
            catch ( Exception ex ) when ( XmlValidator.IsSchemaLoadFailure( ex ) == true )
            {
                AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: schema {Schema} - {ex.Message}" );
                return 1;
            }
        }


        int valid = 0;
        int wellFormed = 0;
        int failed = 0;

        foreach ( string file in files )
        {
            string name = Path.GetRelativePath( Directory.GetCurrentDirectory(), file );

            /*
             * A file which cannot be read fails on its own, rather than aborting the
             * run and leaving the remaining files unchecked.
             */
            byte[] source;

            try
            {
                source = await File.ReadAllBytesAsync( file );
            }
            catch ( Exception ex ) when ( ex is IOException || ex is UnauthorizedAccessException )
            {
                AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: {name} - cannot read: {ex.Message}" );
                failed++;
                continue;
            }

            ValidationResult result = XmlValidator.Validate( source, file, schemas );

            switch ( result.Status )
            {
                case ValidationStatus.Valid:
                    AnsiConsole.MarkupLineInterpolated( $"[green]ok [/]: {name} - valid" );
                    valid++;
                    continue;

                case ValidationStatus.WellFormed:
                    AnsiConsole.MarkupLineInterpolated( $"[green]ok [/]: {name} - well formed" );
                    wellFormed++;
                    continue;

                case ValidationStatus.NotWellFormed:
                    AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: {name} - not well formed: {result.Errors[ 0 ].Message}" );
                    break;

                case ValidationStatus.SchemaError:
                    AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: {name} - schema: {result.Errors[ 0 ].Message}" );
                    break;

                case ValidationStatus.Invalid:
                    AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: {name} - invalid, {result.Errors.Count} error(s)" );

                    if ( EmitErrors == true )
                    {
                        foreach ( ValidationError error in result.Errors )
                            AnsiConsole.MarkupLineInterpolated( $"       {name}({error.Line},{error.Column}): {error.Message}" );
                    }
                    break;
            }

            failed++;
        }


        AnsiConsole.MarkupLineInterpolated(
            $"     {files.Count} file(s), {valid} valid, {wellFormed} well formed, {failed} failed" );

        if ( failed > 0 || unmatched.Count > 0 )
            return 1;

        return 0;
    }
}