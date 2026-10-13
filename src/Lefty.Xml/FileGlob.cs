using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Lefty.Xml;

/// <summary>
/// Expands command-line file patterns into absolute file paths. Shells on Windows
/// do not expand globs, so the tool has to do it itself.
/// </summary>
public static class FileGlob
{
    private static readonly char[] Wildcards = [ '*', '?', '[' ];


    /// <summary>
    /// Expands the patterns, returning the distinct set of matching files, sorted.
    /// Patterns which matched no file at all are reported through <paramref name="unmatched" />.
    /// A pattern whose files were all matched by an earlier pattern still counts as
    /// matched.
    /// </summary>
    public static IReadOnlyList<string> Resolve( IEnumerable<string> patterns, out IReadOnlyList<string> unmatched )
    {
        var files = new SortedSet<string>( StringComparer.OrdinalIgnoreCase );
        var missed = new List<string>();

        foreach ( string pattern in patterns )
        {
            if ( ResolveOne( pattern, files ) == false )
                missed.Add( pattern );
        }

        unmatched = missed;
        return [ .. files ];
    }


    /// <summary>
    /// Adds the files matching <paramref name="pattern" /> to <paramref name="files" />,
    /// returning whether there were any.
    /// </summary>
    private static bool ResolveOne( string pattern, SortedSet<string> files )
    {
        /*
         * A pattern without wildcards is a plain path: either a file, or a
         * directory which stands for every XML file underneath it.
         */
        if ( pattern.IndexOfAny( Wildcards ) < 0 )
        {
            if ( File.Exists( pattern ) )
            {
                files.Add( Path.GetFullPath( pattern ) );
                return true;
            }

            if ( Directory.Exists( pattern ) )
                return Match( pattern, "**/*.xml", files );

            return false;
        }


        /*
         * Otherwise, split into the literal directory prefix -- which is what
         * Matcher needs as its base -- and the glob which follows it.
         */
        string[] parts = pattern.Replace( '\\', '/' ).Split( '/' );

        int split = 0;
        while ( split < parts.Length && parts[ split ].IndexOfAny( Wildcards ) < 0 )
            split++;

        string directory = split == 0 ? "." : string.Join( '/', parts[ ..split ] );
        string glob = string.Join( '/', parts[ split.. ] );

        if ( directory.Length == 0 )
            directory = "/";

        if ( Directory.Exists( directory ) == false )
            return false;

        return Match( directory, glob, files );
    }


    /// <summary />
    private static bool Match( string directory, string glob, SortedSet<string> files )
    {
        var matcher = new Matcher( StringComparison.OrdinalIgnoreCase );
        matcher.AddInclude( glob );

        PatternMatchingResult result = matcher.Execute( new DirectoryInfoWrapper( new DirectoryInfo( directory ) ) );

        foreach ( FilePatternMatch match in result.Files )
            files.Add( Path.GetFullPath( Path.Combine( directory, match.Path ) ) );

        return result.HasMatches;
    }
}