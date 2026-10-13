using System.Text;

namespace Lefty.Xml.Tests;

/// <summary>
/// Golden file tests: each Format/TestN-in.xml must format to Format/TestN-out.xml.
/// </summary>
public class FormatTests
{
    /*
     * Fixtures are compared with LF line endings regardless of how git checked them
     * out; line ending conversion itself is covered by XmlFormatterTests.
     */
    private static readonly XmlFormatOptions Options = new XmlFormatOptions()
    {
        IndentChars = "  ",
        NewLine = "\n",
        InsertFinalNewline = true,
        TrimTrailingWhitespace = true,
    };


    /// <summary />
    [Fact]
    public void Test1()
    {
        var input = Load( "Test1-in.xml" );
        var output = Load( "Test1-out.xml" );

        var actual = Format( input );

        Assert.Equal( output, actual );
    }


    /// <summary />
    [Fact]
    public void Test2()
    {
        var input = Load( "Test2-in.xml" );
        var output = Load( "Test2-out.xml" );

        var actual = Format( input );

        Assert.Equal( output, actual );
    }


    /// <summary />
    [Fact]
    public void Test3()
    {
        var input = Load( "Test3-in.xml" );
        var output = Load( "Test3-out.xml" );

        var actual = Format( input );

        Assert.Equal( output, actual );
    }


    /// <summary />
    private string Load( string filename )
    {
        string name = typeof( FormatTests ).Namespace + ".Format." + filename;

        using Stream? stream = typeof( FormatTests ).Assembly.GetManifestResourceStream( name );

        if ( stream == null )
            throw new FileNotFoundException( $"Embedded resource '{name}' not found" );

        using var reader = new StreamReader( stream, Encoding.UTF8 );

        return reader.ReadToEnd().Replace( "\r\n", "\n" );
    }

    /// <summary />
    private string Format( string input )
    {
        byte[] source = Encoding.UTF8.GetBytes( input );
        byte[] formatted = XmlFormatter.Format( source, Options );

        return Encoding.UTF8.GetString( formatted );
    }
}