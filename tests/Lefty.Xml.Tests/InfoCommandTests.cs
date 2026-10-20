using System.Text.Json;

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
    [Fact]
    public async Task Json_output_reports_facts_and_failures()
    {
        string good = _dir.Write( "a.xml", "<a xmlns=\"urn:a\">\n  <b />\n</a>\n" );
        string bad = _dir.Write( "b.xml", "<a>" );
        var console = new CapturedConsole();

        int exit = await Run( [ good, bad ], console, "json" );

        Assert.Equal( 1, exit );

        using JsonDocument json = JsonDocument.Parse( console.Text );
        JsonElement[] files = [ .. json.RootElement.EnumerateArray() ];

        Assert.Equal( 2, files.Length );

        JsonElement info = files[ 0 ].GetProperty( "info" );
        Assert.False( files[ 0 ].TryGetProperty( "error", out _ ) );
        Assert.Equal( "a", info.GetProperty( "root" ).GetProperty( "localName" ).GetString() );
        Assert.Equal( "urn:a", info.GetProperty( "namespaces" )[ 0 ].GetProperty( "uri" ).GetString() );
        Assert.Equal( "2 spaces", info.GetProperty( "indentation" ).GetString() );
        Assert.True( info.GetProperty( "finalNewline" ).GetBoolean() );

        Assert.False( files[ 1 ].TryGetProperty( "info", out _ ) );
        Assert.StartsWith( "not well formed: ", files[ 1 ].GetProperty( "error" ).GetString() );
    }


    /// <summary />
    private static Task<int> Run( params string[] patterns )
    {
        return Run( patterns, null, "text" );
    }


    /// <summary />
    private static Task<int> Run( string[] patterns, CapturedConsole? console, string output )
    {
        var command = new InfoCommand( ( console ?? new CapturedConsole() ).Console )
        {
            Patterns = patterns,
            Output = output,
        };

        return command.OnExecuteAsync();
    }
}
