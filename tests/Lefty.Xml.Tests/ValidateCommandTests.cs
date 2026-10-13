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
    public async Task Validates_every_xml_file_beneath_a_directory()
    {
        _dir.Write( "docs/a.xml", Located.Replace( "count.xsd", "../count.xsd" ) + "1</count>" );
        _dir.Write( "docs/sub/b.xml", "<count>oops</count>" );

        Assert.Equal( 0, await Run( [ Path.Combine( _dir.Root, "docs" ) ] ) );
        Assert.Equal( 1, await Run( [ Path.Combine( _dir.Root, "docs" ) ], _schema ) );
    }


    /// <summary />
    private static Task<int> Run( string[] patterns, string? schema = null, bool emitErrors = false )
    {
        var command = new ValidateCommand()
        {
            Patterns = patterns,
            Schema = schema,
            EmitErrors = emitErrors,
        };

        return command.OnExecuteAsync();
    }
}