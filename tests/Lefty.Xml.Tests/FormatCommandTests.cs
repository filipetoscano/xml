namespace Lefty.Xml.Tests;

/// <summary>
/// End-to-end tests of the format command, running against files on disk.
/// </summary>
public sealed class FormatCommandTests : IDisposable
{
    private const string EditorConfig = """
        root = true

        [*.xml]
        indent_style = space
        indent_size = 2
        end_of_line = lf
        insert_final_newline = true

        """;

    private const string Unformatted = "<a><b /></a>";
    private const string Formatted = "<a>\n  <b />\n</a>\n";

    private readonly TempDirectory _dir = new TempDirectory();


    /// <summary />
    public FormatCommandTests()
    {
        _dir.Write( ".editorconfig", EditorConfig );
    }


    /// <summary />
    public void Dispose()
    {
        _dir.Dispose();
    }


    /// <summary />
    [Fact]
    public async Task Formats_file_in_place()
    {
        string file = _dir.Write( "a.xml", Unformatted );

        int exit = await Run( file );

        Assert.Equal( 0, exit );
        Assert.Equal( Formatted, File.ReadAllText( file ) );
    }


    /// <summary />
    [Fact]
    public async Task Does_not_rewrite_formatted_file()
    {
        string file = _dir.Write( "a.xml", Formatted );
        DateTime stamp = new DateTime( 2000, 1, 1, 0, 0, 0, DateTimeKind.Utc );
        File.SetLastWriteTimeUtc( file, stamp );

        int exit = await Run( file );

        Assert.Equal( 0, exit );
        Assert.Equal( stamp, File.GetLastWriteTimeUtc( file ) );
    }


    /// <summary />
    [Fact]
    public async Task Check_reports_change_without_writing()
    {
        string file = _dir.Write( "a.xml", Unformatted );

        int exit = await Run( file, check: true );

        Assert.Equal( 1, exit );
        Assert.Equal( Unformatted, File.ReadAllText( file ) );
    }


    /// <summary />
    [Fact]
    public async Task Check_succeeds_when_already_formatted()
    {
        string file = _dir.Write( "a.xml", Formatted );

        int exit = await Run( file, check: true );

        Assert.Equal( 0, exit );
    }


    /// <summary />
    [Fact]
    public async Task Fails_when_nothing_matches()
    {
        int exit = await Run( Path.Combine( _dir.Root, "missing.xml" ) );

        Assert.Equal( 1, exit );
    }


    /// <summary />
    [Fact]
    public async Task Fails_on_unmatched_pattern_but_still_formats_matches()
    {
        string file = _dir.Write( "a.xml", Unformatted );

        int exit = await Run( file, Path.Combine( _dir.Root, "missing.xml" ) );

        Assert.Equal( 1, exit );
        Assert.Equal( Formatted, File.ReadAllText( file ) );
    }


    /// <summary />
    [Fact]
    public async Task Skips_file_without_editorconfig_settings()
    {
        _dir.Write( ".editorconfig", "root = true\n\n[*.xml]\nindent_style = space\nindent_size = 2\n" );
        string file = _dir.Write( "a.xml", Unformatted );

        int exit = await Run( file );

        Assert.Equal( 1, exit );
        Assert.Equal( Unformatted, File.ReadAllText( file ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_on_malformed_file_but_formats_the_others()
    {
        string bad = _dir.Write( "bad.xml", "<a><b></a>" );
        string good = _dir.Write( "good.xml", Unformatted );

        int exit = await Run( bad, good );

        Assert.Equal( 1, exit );
        Assert.Equal( "<a><b></a>", File.ReadAllText( bad ) );
        Assert.Equal( Formatted, File.ReadAllText( good ) );
    }


    /// <summary />
    [Fact]
    public async Task Expands_directory_to_xml_files_beneath_it()
    {
        string top = _dir.Write( "a.xml", Unformatted );
        string nested = _dir.Write( "sub/deeper/b.xml", Unformatted );
        string other = _dir.Write( "sub/c.txt", Unformatted );

        int exit = await Run( _dir.Root );

        Assert.Equal( 0, exit );
        Assert.Equal( Formatted, File.ReadAllText( top ) );
        Assert.Equal( Formatted, File.ReadAllText( nested ) );
        Assert.Equal( Unformatted, File.ReadAllText( other ) );
    }


    /// <summary />
    [Fact]
    public async Task Expands_glob_patterns()
    {
        string included = _dir.Write( "sub/a.xml", Unformatted );
        string excluded = _dir.Write( "b.xml", Unformatted );

        int exit = await Run( Path.Combine( _dir.Root, "sub", "*.xml" ) );

        Assert.Equal( 0, exit );
        Assert.Equal( Formatted, File.ReadAllText( included ) );
        Assert.Equal( Unformatted, File.ReadAllText( excluded ) );
    }


    /// <summary />
    [Fact]
    public async Task Applies_settings_of_the_nearest_editorconfig()
    {
        _dir.Write( "tabs/.editorconfig", "[*.xml]\nindent_style = tab\n" );
        string file = _dir.Write( "tabs/a.xml", Unformatted );

        int exit = await Run( file );

        Assert.Equal( 0, exit );
        Assert.Equal( "<a>\n\t<b />\n</a>\n", File.ReadAllText( file ) );
    }


    /// <summary />
    private static Task<int> Run( params string[] patterns )
    {
        return Run( patterns, false );
    }


    /// <summary />
    private static Task<int> Run( string pattern, bool check )
    {
        return Run( [ pattern ], check );
    }


    /// <summary />
    private static Task<int> Run( string[] patterns, bool check )
    {
        var command = new FormatCommand()
        {
            Patterns = patterns,
            Check = check,
        };

        return command.OnExecuteAsync();
    }
}
