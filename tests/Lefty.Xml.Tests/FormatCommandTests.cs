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
    [Fact]
    public async Task Succeeds_when_patterns_overlap()
    {
        string file = _dir.Write( "a.xml", Unformatted );

        int exit = await Run( _dir.Root, file, Path.Combine( _dir.Root, "*.xml" ) );

        Assert.Equal( 0, exit );
        Assert.Equal( Formatted, File.ReadAllText( file ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_on_unreadable_file_but_formats_the_others()
    {
        string locked = _dir.Write( "locked.xml", Unformatted );
        string good = _dir.Write( "good.xml", Unformatted );

        int exit;

        using ( new FileStream( locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None ) )
            exit = await Run( locked, good );

        Assert.Equal( 1, exit );
        Assert.Equal( Unformatted, File.ReadAllText( locked ) );
        Assert.Equal( Formatted, File.ReadAllText( good ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_on_read_only_file_but_formats_the_others()
    {
        string readOnly = _dir.Write( "readonly.xml", Unformatted );
        string good = _dir.Write( "good.xml", Unformatted );
        File.SetAttributes( readOnly, FileAttributes.ReadOnly );

        int exit = await Run( readOnly, good );

        Assert.Equal( 1, exit );
        Assert.Equal( Unformatted, File.ReadAllText( readOnly ) );
        Assert.Equal( Formatted, File.ReadAllText( good ) );
    }


    /// <summary />
    [Fact]
    public async Task Check_does_not_fail_on_read_only_file_which_is_formatted()
    {
        string readOnly = _dir.Write( "readonly.xml", Formatted );
        File.SetAttributes( readOnly, FileAttributes.ReadOnly );

        int exit = await Run( readOnly, check: true );

        Assert.Equal( 0, exit );
    }


    /// <summary />
    [Fact]
    public async Task Diff_shows_changes_in_check_mode()
    {
        string file = _dir.Write( "a.xml", Unformatted );
        var console = new CapturedConsole();

        int exit = await Run( [ file ], true, console, diff: true );

        Assert.Equal( 1, exit );
        Assert.Equal( Unformatted, File.ReadAllText( file ) );

        string[] lines = console.Lines;
        int header = Array.FindIndex( lines, line => line.StartsWith( "--- a/" ) );

        Assert.True( header >= 0, console.Text );
        Assert.EndsWith( "/a.xml", lines[ header ] );
        Assert.StartsWith( "+++ b/", lines[ header + 1 ] );

        string[] expected =
        [
            "@@ -1 +1,3 @@",
            "-<a><b /></a>",
            "\\ No newline at end of file",
            "+<a>",
            "+  <b />",
            "+</a>",
        ];

        Assert.Equal( expected, lines[ ( header + 2 )..( header + 8 ) ] );
    }


    /// <summary />
    [Fact]
    public async Task Diff_shows_changes_when_formatting()
    {
        string file = _dir.Write( "a.xml", Unformatted );
        var console = new CapturedConsole();

        int exit = await Run( [ file ], false, console, diff: true );

        Assert.Equal( 0, exit );
        Assert.Equal( Formatted, File.ReadAllText( file ) );
        Assert.Contains( "+  <b />", console.Lines );
    }


    /// <summary />
    [Fact]
    public async Task Diff_is_only_shown_when_asked()
    {
        string file = _dir.Write( "a.xml", Unformatted );
        var console = new CapturedConsole();

        await Run( [ file ], true, console );

        Assert.DoesNotContain( console.Lines, line => line.StartsWith( "@@" ) );
    }


    /// <summary />
    [Fact]
    public async Task Diff_describes_a_change_of_line_endings_only()
    {
        string file = _dir.Write( "a.xml", Formatted.ReplaceLineEndings( "\r\n" ) );
        var console = new CapturedConsole();

        await Run( [ file ], true, console, diff: true );

        Assert.Contains( "only line endings change", console.Text );
        Assert.DoesNotContain( console.Lines, line => line.StartsWith( "@@" ) );
    }


    /// <summary />
    [Fact]
    public async Task Diff_describes_a_change_of_encoding_only()
    {
        _dir.Write( ".editorconfig", EditorConfig + "charset = utf-8-bom\n" );
        string file = _dir.Write( "a.xml", Formatted );
        var console = new CapturedConsole();

        await Run( [ file ], true, console, diff: true );

        Assert.Contains( "only the encoding or byte order mark changes", console.Text );
    }


    /// <summary />
    [Fact]
    public async Task GitHub_output_annotates_unformatted_file()
    {
        string file = _dir.Write( "a.xml", Unformatted );
        var console = new CapturedConsole();

        await Run( [ file ], true, console, output: "github" );

        string annotation = Assert.Single( console.Lines, line => line.StartsWith( "::error " ) );

        Assert.Matches( @"^::error file=[^,]*a\.xml,title=lxml format::File is not formatted", annotation );
        Assert.Contains( console.Lines, line => line.StartsWith( "chg: " ) );
    }


    /// <summary />
    [Fact]
    public async Task GitHub_output_annotates_malformed_file_with_position()
    {
        string file = _dir.Write( "bad.xml", "<a>\n<b></a>" );
        var console = new CapturedConsole();

        await Run( [ file ], false, console, output: "github" );

        string annotation = Assert.Single( console.Lines, line => line.StartsWith( "::error " ) );

        Assert.Matches( @"^::error file=[^,]*bad\.xml,line=2,col=\d+,title=lxml format::Not well formed: ", annotation );
    }


    /// <summary />
    [Fact]
    public async Task Text_output_has_no_annotations()
    {
        string file = _dir.Write( "a.xml", Unformatted );
        var console = new CapturedConsole();

        await Run( [ file, Path.Combine( _dir.Root, "missing.xml" ) ], true, console );

        Assert.DoesNotContain( console.Lines, line => line.StartsWith( "::" ) );
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
    private static Task<int> Run( string[] patterns, bool check, CapturedConsole? console = null, bool diff = false, string output = "text" )
    {
        var command = new FormatCommand( ( console ?? new CapturedConsole() ).Console )
        {
            Patterns = patterns,
            Check = check,
            Diff = diff,
            Output = output,
        };

        return command.OnExecuteAsync();
    }
}