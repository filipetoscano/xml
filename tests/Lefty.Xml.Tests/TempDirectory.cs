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
        string path = Path.Combine( Root, relativePath );

        Directory.CreateDirectory( Path.GetDirectoryName( path )! );
        File.WriteAllText( path, content, new UTF8Encoding( false ) );

        return path;
    }


    /// <summary />
    public void Dispose()
    {
        try
        {
            Directory.Delete( Root, true );
        }
        catch ( IOException )
        {
        }
    }
}