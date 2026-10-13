namespace Lefty.Xml.Tests;

/// <summary />
public sealed class FileGlobTests : IDisposable
{
    private readonly TempDirectory _dir = new TempDirectory();
    private readonly string _a;
    private readonly string _b;


    /// <summary />
    public FileGlobTests()
    {
        _a = _dir.Write( "a.xml", "<a />" );
        _b = _dir.Write( "sub/b.xml", "<b />" );
        _dir.Write( "sub/c.txt", "c" );
    }


    /// <summary />
    public void Dispose()
    {
        _dir.Dispose();
    }


    /// <summary />
    [Fact]
    public void Resolves_file_directory_and_glob()
    {
        Assert.Equal( [ _a ], FileGlob.Resolve( [ _a ], out _ ) );
        Assert.Equal( [ _a, _b ], FileGlob.Resolve( [ _dir.Root ], out _ ) );
        Assert.Equal( [ _b ], FileGlob.Resolve( [ Path.Combine( _dir.Root, "sub", "*.xml" ) ], out _ ) );
        Assert.Equal( [ _a, _b ], FileGlob.Resolve( [ Path.Combine( _dir.Root, "**", "*.xml" ) ], out _ ) );
    }


    /// <summary />
    [Fact]
    public void Reports_patterns_which_match_nothing()
    {
        string missingFile = Path.Combine( _dir.Root, "missing.xml" );
        string missingGlob = Path.Combine( _dir.Root, "sub", "*.json" );
        string missingDirectory = Path.Combine( _dir.Root, "nope", "*.xml" );

        IReadOnlyList<string> files = FileGlob.Resolve( [ _a, missingFile, missingGlob, missingDirectory ], out IReadOnlyList<string> unmatched );

        Assert.Equal( [ _a ], files );
        Assert.Equal( [ missingFile, missingGlob, missingDirectory ], unmatched );
    }


    /// <summary />
    [Fact]
    public void Does_not_report_directory_without_xml_files_as_matched()
    {
        string empty = Path.Combine( _dir.Root, "empty" );
        Directory.CreateDirectory( empty );

        FileGlob.Resolve( [ empty ], out IReadOnlyList<string> unmatched );

        Assert.Equal( [ empty ], unmatched );
    }


    /// <summary />
    [Fact]
    public void Repeated_pattern_is_not_reported_as_unmatched()
    {
        IReadOnlyList<string> files = FileGlob.Resolve( [ _a, _a ], out IReadOnlyList<string> unmatched );

        Assert.Equal( [ _a ], files );
        Assert.Empty( unmatched );
    }


    /// <summary />
    [Fact]
    public void Overlapping_patterns_are_not_reported_as_unmatched()
    {
        string glob = Path.Combine( _dir.Root, "sub", "*.xml" );

        IReadOnlyList<string> files = FileGlob.Resolve( [ _dir.Root, glob, _b ], out IReadOnlyList<string> unmatched );

        Assert.Equal( [ _a, _b ], files );
        Assert.Empty( unmatched );
    }
}
