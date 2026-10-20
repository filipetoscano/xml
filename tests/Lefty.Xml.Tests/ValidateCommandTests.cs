using System.Text.Json;

namespace Lefty.Xml.Tests;

/// <summary>
/// End-to-end tests of the validate command, running against files on disk.
/// </summary>
public sealed class ValidateCommandTests : IDisposable
{
    private const string Schema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="count" type="xs:int" />
        </xs:schema>
        """;

    private const string Located = """<count xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="count.xsd">""";

    private readonly TempDirectory _dir = new TempDirectory();
    private readonly string _schema;


    /// <summary />
    public ValidateCommandTests()
    {
        _schema = _dir.Write( "count.xsd", Schema );
    }


    /// <summary />
    public void Dispose()
    {
        _dir.Dispose();
    }


    /// <summary />
    [Fact]
    public async Task Succeeds_when_well_formed_without_schema()
    {
        string file = _dir.Write( "a.xml", "<a><b /></a>" );

        Assert.Equal( 0, await Run( [ file ] ) );
    }


    /// <summary />
    [Fact]
    public async Task Succeeds_when_valid_against_explicit_schema()
    {
        string file = _dir.Write( "a.xml", "<count>1</count>" );

        Assert.Equal( 0, await Run( [ file ], _schema ) );
    }


    /// <summary />
    [Fact]
    public async Task Succeeds_when_valid_against_schema_location()
    {
        string file = _dir.Write( "a.xml", Located + "1</count>" );

        Assert.Equal( 0, await Run( [ file ] ) );
    }


    /// <summary />
    [Theory]
    [InlineData( false )]
    [InlineData( true )]
    public async Task Fails_when_invalid( bool emitErrors )
    {
        string file = _dir.Write( "a.xml", "<count>many</count>" );

        Assert.Equal( 1, await Run( [ file ], _schema, emitErrors ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_when_not_well_formed()
    {
        string good = _dir.Write( "good.xml", "<a />" );
        string bad = _dir.Write( "bad.xml", "<a>" );

        Assert.Equal( 1, await Run( [ good, bad ] ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_when_schema_location_cannot_be_loaded()
    {
        string file = _dir.Write( "a.xml", Located.Replace( "count.xsd", "missing.xsd" ) + "1</count>" );

        Assert.Equal( 1, await Run( [ file ] ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_when_explicit_schema_cannot_be_loaded()
    {
        string schema = _dir.Write( "broken.xsd", "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"><xs:element name=\"a\" type=\"undefined\" /></xs:schema>" );
        string file = _dir.Write( "a.xml", "<a />" );

        Assert.Equal( 1, await Run( [ file ], schema ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_when_nothing_matches()
    {
        Assert.Equal( 1, await Run( [ Path.Combine( _dir.Root, "missing.xml" ) ] ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_on_unmatched_pattern_even_when_matches_are_valid()
    {
        string file = _dir.Write( "a.xml", "<a />" );

        Assert.Equal( 1, await Run( [ file, Path.Combine( _dir.Root, "missing.xml" ) ] ) );
    }


    /// <summary />
    [Fact]
    public async Task Succeeds_when_patterns_overlap()
    {
        string file = _dir.Write( "a.xml", "<a />" );

        Assert.Equal( 0, await Run( [ _dir.Root, file, Path.Combine( _dir.Root, "*.xml" ) ] ) );
    }


    /// <summary />
    [Fact]
    public async Task Fails_on_unreadable_file()
    {
        string locked = _dir.Write( "locked.xml", "<a />" );
        string good = _dir.Write( "good.xml", "<a />" );

        using ( new FileStream( locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None ) )
            Assert.Equal( 1, await Run( [ locked, good ] ) );
    }


    /// <summary />
    [Fact]
    public async Task Validates_every_xml_file_beneath_a_directory()
    {
        _dir.Write( "docs/a.xml", Located.Replace( "count.xsd", "../count.xsd" ) + "1</count>" );
        _dir.Write( "docs/sub/b.xml", "<count>oops</count>" );

        Assert.Equal( 0, await Run( [ Path.Combine( _dir.Root, "docs" ) ] ) );
        Assert.Equal( 1, await Run( [ Path.Combine( _dir.Root, "docs" ) ], _schema ) );
    }


    /// <summary />
    [Fact]
    public async Task Json_output_reports_every_file()
    {
        string valid = _dir.Write( "a.xml", "<count>1</count>" );
        string invalid = _dir.Write( "b.xml", "<count>many</count>" );
        string locked = _dir.Write( "c.xml", "<count>1</count>" );
        var console = new CapturedConsole();

        int exit;

        using ( new FileStream( locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None ) )
            exit = await Run( [ valid, invalid, locked ], _schema, console: console, output: "json" );

        Assert.Equal( 1, exit );

        using JsonDocument json = JsonDocument.Parse( console.Text );
        JsonElement[] files = [ .. json.RootElement.EnumerateArray() ];

        Assert.Equal( 3, files.Length );

        Assert.EndsWith( "a.xml", files[ 0 ].GetProperty( "file" ).GetString() );
        Assert.Equal( "valid", files[ 0 ].GetProperty( "status" ).GetString() );
        Assert.Equal( 0, files[ 0 ].GetProperty( "errors" ).GetArrayLength() );

        Assert.Equal( "invalid", files[ 1 ].GetProperty( "status" ).GetString() );
        JsonElement error = Assert.Single( files[ 1 ].GetProperty( "errors" ).EnumerateArray() );
        Assert.Equal( 1, error.GetProperty( "line" ).GetInt32() );
        Assert.True( error.GetProperty( "column" ).GetInt32() > 0 );
        Assert.Contains( "many", error.GetProperty( "message" ).GetString() );

        Assert.Equal( "unreadable", files[ 2 ].GetProperty( "status" ).GetString() );
    }


    /// <summary />
    [Fact]
    public async Task Json_output_reports_not_well_formed_file()
    {
        string file = _dir.Write( "a.xml", "<a>" );
        var console = new CapturedConsole();

        await Run( [ file ], console: console, output: "json" );

        using JsonDocument json = JsonDocument.Parse( console.Text );

        Assert.Equal( "notWellFormed", json.RootElement[ 0 ].GetProperty( "status" ).GetString() );
    }


    /// <summary />
    [Fact]
    public async Task GitHub_output_annotates_each_error()
    {
        string schema = _dir.Write( "root.xsd", """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:element name="root">
                <xs:complexType>
                  <xs:attribute name="a" type="xs:int" />
                  <xs:attribute name="b" type="xs:int" />
                </xs:complexType>
              </xs:element>
            </xs:schema>
            """ );
        string file = _dir.Write( "a.xml", "<root a=\"x\" b=\"y\" />" );
        var console = new CapturedConsole();

        int exit = await Run( [ file ], schema, console: console, output: "github" );

        Assert.Equal( 1, exit );

        string[] annotations = [ .. console.Lines.Where( line => line.StartsWith( "::error " ) ) ];

        Assert.Equal( 2, annotations.Length );
        Assert.All( annotations, annotation => Assert.Matches( @"^::error file=[^,]*a\.xml,line=1,col=\d+,title=lxml validate::", annotation ) );
    }


    /// <summary />
    [Fact]
    public async Task Text_output_has_no_annotations()
    {
        string file = _dir.Write( "a.xml", "<count>many</count>" );
        var console = new CapturedConsole();

        await Run( [ file ], _schema, console: console );

        Assert.DoesNotContain( console.Lines, line => line.StartsWith( "::" ) );
    }


    /// <summary />
    private static Task<int> Run( string[] patterns, string? schema = null, bool emitErrors = false, CapturedConsole? console = null, string output = "text" )
    {
        var command = new ValidateCommand( ( console ?? new CapturedConsole() ).Console )
        {
            Patterns = patterns,
            Schema = schema,
            EmitErrors = emitErrors,
            Output = output,
        };

        return command.OnExecuteAsync();
    }
}