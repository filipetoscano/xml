namespace Lefty.Xml.Tests;

/// <summary>
/// End-to-end tests of the info command, running against files on disk.
/// </summary>
public sealed class InfoCommandTests : IDisposable
{
    private readonly TempDirectory _dir = new TempDirectory();


    /// <summary />
    public void Dispose()
    {
        _dir.Dispose();
    }


    /// <summary />
    [Fact]
    public async Task Succeeds_for_well_formed_files()
    {
        _dir.Write( "a.xml", "<?xml version=\"1.0\"?>\n<a xmlns=\"urn:a\">\n  <b />\n</a>\n" );
        _dir.Write( "sub/b.xml", "<!DOCTYPE b SYSTEM \"b.dtd\"><b>&ent;</b>" );

        Assert.Equal( 0, await Run( _dir.Root ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_when_not_well_formed()
    {
        string good = _dir.Write( "good.xml", "<a />" );
        string bad = _dir.Write( "bad.xml", "<a>" );

        Assert.Equal( 1, await Run( good, bad ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_when_nothing_matches()
    {
        Assert.Equal( 1, await Run( Path.Combine( _dir.Root, "missing.xml" ) ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_on_unmatched_pattern_even_when_matches_are_fine()
    {
        string file = _dir.Write( "a.xml", "<a />" );

        Assert.Equal( 1, await Run( file, Path.Combine( _dir.Root, "missing.xml" ) ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_on_unreadable_file()
    {
        string locked = _dir.Write( "locked.xml", "<a />" );

        using ( new FileStream( locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None ) )
            Assert.Equal( 1, await Run( locked ) );
    }


    /// <summary />
    private static Task<int> Run( params string[] patterns )
    {
        var command = new InfoCommand()
        {
            Patterns = patterns,
        };

        return command.OnExecuteAsync();
    }
}
