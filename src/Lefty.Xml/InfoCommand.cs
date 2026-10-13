using McMaster.Extensions.CommandLineUtils;
using Spectre.Console;
using System.ComponentModel.DataAnnotations;
using System.Xml;

namespace Lefty.Xml;

/// <summary />
[Command( "info", Description = "Shows encoding, declaration, namespaces, layout and other facts about XML files" )]
public class InfoCommand
{
    /// <summary />
    [Argument( 0, Description = "Input files" )]
    [Required]
    public string[] Patterns { get; set; } = default!;


    /// <summary />
    public async Task<int> OnExecuteAsync()
    {
        IReadOnlyList<string> files = FileGlob.Resolve( Patterns, out IReadOnlyList<string> unmatched );

        foreach ( string pattern in unmatched )
            AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: no files matched '{pattern}'" );

        if ( files.Count == 0 )
            return 1;

        int failed = 0;

        foreach ( string file in files )
        {
            string name = Path.GetRelativePath( Directory.GetCurrentDirectory(), file );

            XmlInfo info;

            try
            {
                info = XmlInspector.Inspect( await File.ReadAllBytesAsync( file ) );
            }
            catch ( Exception ex ) when ( ex is IOException || ex is UnauthorizedAccessException )
            {
                AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: {name} - cannot read: {ex.Message}" );
                failed++;
                continue;
            }
            catch ( XmlException ex )
            {
                AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: {name} - not well formed: {ex.Message}" );
                failed++;
                continue;
            }

            Print( name, info );
        }

        if ( failed > 0 || unmatched.Count > 0 )
            return 1;

        return 0;
    }


    /// <summary />
    private static void Print( string name, XmlInfo info )
    {
        AnsiConsole.MarkupLineInterpolated( $"[bold]{name}[/]" );

        Field( "size", $"{info.Size:N0} bytes, {info.Lines:N0} line(s)" );

        string bom = info.ByteOrderMark == null ? "no BOM" : $"{info.ByteOrderMark} BOM";
        string declared = info.Declaration?.Encoding == null ? "none declared" : $"declared {info.Declaration.Encoding}";
        Field( "encoding", $"{info.Encoding} ({bom}, {declared})" );

        if ( info.Declaration == null )
        {
            Field( "declaration", "none" );
        }
        else
        {
            string standalone = info.Declaration.Standalone == null ? "" : $", standalone {info.Declaration.Standalone}";
            Field( "declaration", $"version {info.Declaration.Version}{standalone}" );
        }

        if ( info.DocType != null )
        {
            var parts = new List<string> { info.DocType.Name };

            if ( info.DocType.PublicId != null )
                parts.Add( $"PUBLIC \"{info.DocType.PublicId}\"" );

            if ( info.DocType.SystemId != null )
                parts.Add( $"SYSTEM \"{info.DocType.SystemId}\"" );

            if ( info.DocType.HasInternalSubset == true )
                parts.Add( "[internal subset]" );

            Field( "doctype", string.Join( ' ', parts ) );
        }

        string rootNamespace = info.Root.NamespaceUri.Length == 0 ? "no namespace" : info.Root.NamespaceUri;
        Field( "root", $"{info.Root.QualifiedName} ({rootNamespace})" );

        Fields( "namespaces", info.Namespaces.Select( ns => $"{( ns.Prefix.Length == 0 ? "(default)" : ns.Prefix )} = {ns.Uri}" ) );
        Fields( "schema", info.SchemaLocations.Select( s => s.Namespace == null ? $"(no namespace) {s.Location}" : $"{s.Namespace} {s.Location}" ) );

        Field( "line endings", $"{info.LineEndings}, {( info.FinalNewline == true ? "final newline" : "no final newline" )}" );
        Field( "indentation", info.Indentation );
        Field( "content", $"{info.Elements:N0} element(s), {info.Attributes:N0} attribute(s), depth {info.Depth}" );

        AnsiConsole.WriteLine();
    }


    /// <summary />
    private static void Field( string label, string value )
    {
        AnsiConsole.MarkupLineInterpolated( $"  [grey]{label,-13}[/] {value}" );
    }


    /// <summary>
    /// Writes one value per line, aligned under the first; "none" when empty.
    /// </summary>
    private static void Fields( string label, IEnumerable<string> values )
    {
        bool first = true;

        foreach ( string value in values )
        {
            Field( first == true ? label : string.Empty, value );
            first = false;
        }

        if ( first == true )
            Field( label, "none" );
    }
}
