using System.Text;

namespace Lefty.Xml;

/// <summary>
/// GitHub Actions workflow commands which, written to the log, become annotations
/// on the run -- and on the lines of a pull request's diff.
/// </summary>
public static class GitHubAnnotation
{
    /// <summary>
    /// An error annotation. <paramref name="file" /> is relative to the repository
    /// root; null for a problem which concerns no file. Line and column are
    /// 1-based, and left out when 0.
    /// </summary>
    public static string Error( string title, string? file, int line, int column, string message )
    {
        var properties = new List<string>();

        if ( file != null )
            properties.Add( "file=" + EscapeProperty( file.Replace( '\\', '/' ) ) );

        if ( file != null && line > 0 )
        {
            properties.Add( "line=" + line );

            if ( column > 0 )
                properties.Add( "col=" + column );
        }

        properties.Add( "title=" + EscapeProperty( title ) );

        return "::error " + string.Join( ',', properties ) + "::" + EscapeData( message );
    }


    /// <summary>
    /// The message runs to the end of the line, so only newlines -- and the escape
    /// character itself -- need escaping.
    /// </summary>
    private static string EscapeData( string value )
    {
        return new StringBuilder( value )
            .Replace( "%", "%25" )
            .Replace( "\r", "%0D" )
            .Replace( "\n", "%0A" )
            .ToString();
    }


    /// <summary>
    /// Properties are additionally delimited by ':' and ','.
    /// </summary>
    private static string EscapeProperty( string value )
    {
        return new StringBuilder( EscapeData( value ) )
            .Replace( ":", "%3A" )
            .Replace( ",", "%2C" )
            .ToString();
    }
}