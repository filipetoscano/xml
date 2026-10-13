using System.Text;

namespace Lefty.Xml.Tests;

/// <summary>
/// Scratch directory which is deleted on dispose.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary />
    public string Root { get; }


    /// <summary />
    public TempDirectory()
    {
        Root = Path.Combine( Path.GetTempPath(), "lxml-tests-" + Guid.NewGuid().ToString( "N" ) );
        Directory.CreateDirectory( Root );
    }


    /// <summary>
    /// Writes <paramref name="content" /> as UTF-8 (without BOM) to a path relative
    /// to the directory, creating intermediate directories, and returns the full path.
    /// </summary>
    public string Write( string relativePath, string content )
    {
        string path = Path.GetFullPath( Path.Combine( Root, relativePath ) );

        Directory.CreateDirectory( Path.GetDirectoryName( path )! );
        File.WriteAllText( path, content, new UTF8Encoding( false ) );

        return path;
    }


    /// <summary />
    public void Dispose()
    {
        try
        {
            /*
             * Tests may leave read-only files behind, which Windows refuses to delete.
             */
            foreach ( string file in Directory.EnumerateFiles( Root, "*", SearchOption.AllDirectories ) )
                File.SetAttributes( file, FileAttributes.Normal );

            Directory.Delete( Root, true );
        }
        catch ( Exception ex ) when ( ex is IOException || ex is UnauthorizedAccessException )
        {
        }
    }
}