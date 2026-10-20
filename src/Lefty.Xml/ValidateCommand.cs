using McMaster.Extensions.CommandLineUtils;
using Spectre.Console;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Xml.Schema;
using AllowedValues = McMaster.Extensions.CommandLineUtils.AllowedValuesAttribute;

namespace Lefty.Xml;

/// <summary>
/// Outcome for one file, as written with --output json. The status is that of
/// <see cref="ValidationStatus" />, or "unreadable" when the file could not be read.
/// </summary>
public sealed record ValidateReport( string File, string Status, IReadOnlyList<ValidationError> Errors );


/// <summary />
[Command( "validate", Description = "Validates XML file against an XSD schema" )]
public class ValidateCommand
{
    private const string Title = "lxml validate";

    private readonly IAnsiConsole _console;


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
    [Option( "-o|--output", CommandOptionType.SingleValue, Description = "Output format: text, github to also annotate errors in GitHub Actions, or json" )]
    [AllowedValues( "text", "github", "json", IgnoreCase = true )]
    public string Output { get; set; } = "text";


    /// <summary />
    public ValidateCommand( IAnsiConsole console )
    {
        _console = console;
    }


    /// <summary />
    private bool GitHub => string.Equals( Output, "github", StringComparison.OrdinalIgnoreCase );

    /// <summary />
    private bool Json => string.Equals( Output, "json", StringComparison.OrdinalIgnoreCase );


    /// <summary />
    public async Task<int> OnExecuteAsync()
    {
        IReadOnlyList<string> files = FileGlob.Resolve( Patterns, out IReadOnlyList<string> unmatched );

        foreach ( string pattern in unmatched )
        {
            Error( $"no files matched '{pattern}'" );
            Annotate( null, 0, 0, $"No files matched '{pattern}'." );
        }

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
                Error( $"schema {Schema} - {ex.Message}" );
                Annotate( Path.GetRelativePath( Directory.GetCurrentDirectory(), Schema ), 0, 0, $"Cannot load schema: {ex.Message}" );
                return 1;
            }
        }


        var reports = new List<ValidateReport>();
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
                reports.Add( new ValidateReport( name, "unreadable", [ new ValidationError( 0, 0, ex.Message ) ] ) );

                if ( Json == false )
                    _console.MarkupLineInterpolated( $"[red]err[/]: {name} - cannot read: {ex.Message}" );

                Annotate( name, 0, 0, $"Cannot read: {ex.Message}" );
                failed++;
                continue;
            }

            ValidationResult result = XmlValidator.Validate( source, file, schemas );

            reports.Add( new ValidateReport( name, JsonNamingPolicy.CamelCase.ConvertName( result.Status.ToString() ), result.Errors ) );

            if ( result.Status == ValidationStatus.Valid )
                valid++;
            else if ( result.Status == ValidationStatus.WellFormed )
                wellFormed++;
            else
                failed++;

            if ( Json == false )
                Print( name, result );

            if ( result.Status == ValidationStatus.NotWellFormed || result.Status == ValidationStatus.Invalid )
            {
                foreach ( ValidationError error in result.Errors )
                    Annotate( name, error.Line, error.Column, error.Message );
            }
            else if ( result.Status == ValidationStatus.SchemaError )
            {
                Annotate( name, 0, 0, $"Cannot load schema: {result.Errors[ 0 ].Message}" );
            }
        }


        if ( Json == true )
        {
            _console.Profile.Out.Writer.WriteLine( JsonReport.Serialize( reports ) );
        }
        else
        {
            _console.MarkupLineInterpolated(
                $"     {files.Count} file(s), {valid} valid, {wellFormed} well formed, {failed} failed" );
        }

        if ( failed > 0 || unmatched.Count > 0 )
            return 1;

        return 0;
    }


    /// <summary />
    private void Print( string name, ValidationResult result )
    {
        switch ( result.Status )
        {
            case ValidationStatus.Valid:
                _console.MarkupLineInterpolated( $"[green]ok [/]: {name} - valid" );
                break;

            case ValidationStatus.WellFormed:
                _console.MarkupLineInterpolated( $"[green]ok [/]: {name} - well formed" );
                break;

            case ValidationStatus.NotWellFormed:
                _console.MarkupLineInterpolated( $"[red]err[/]: {name} - not well formed: {result.Errors[ 0 ].Message}" );
                break;

            case ValidationStatus.SchemaError:
                _console.MarkupLineInterpolated( $"[red]err[/]: {name} - schema: {result.Errors[ 0 ].Message}" );
                break;

            case ValidationStatus.Invalid:
                _console.MarkupLineInterpolated( $"[red]err[/]: {name} - invalid, {result.Errors.Count} error(s)" );

                if ( EmitErrors == true )
                {
                    foreach ( ValidationError error in result.Errors )
                        _console.MarkupLineInterpolated( $"       {name}({error.Line},{error.Column}): {error.Message}" );
                }
                break;
        }
    }


    /// <summary>
    /// Reports a problem which concerns no single file. With JSON output it goes to
    /// standard error, so that standard output stays valid JSON.
    /// </summary>
    private void Error( string message )
    {
        if ( Json == true )
            Console.Error.WriteLine( "err: " + message );
        else
            _console.MarkupLineInterpolated( $"[red]err[/]: {message}" );
    }


    /// <summary />
    private void Annotate( string? name, int line, int column, string message )
    {
        if ( GitHub == true )
            _console.Profile.Out.Writer.WriteLine( GitHubAnnotation.Error( Title, name, line, column, message ) );
    }
}