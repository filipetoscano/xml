using EditorConfig.Core;
using System.Text;

namespace Lefty.Xml.Tests;

/// <summary />
public class XmlFormatOptionsTests
{
    /// <summary />
    [Theory]
    [InlineData( "end_of_line = lf", "indent_style" )]
    [InlineData( "indent_style = space\nend_of_line = lf", "indent_size" )]
    [InlineData( "indent_style = space\nindent_size = tab\nend_of_line = lf", "indent_size" )]
    [InlineData( "indent_style = space\nindent_size = 0\nend_of_line = lf", "indent_size" )]
    [InlineData( "indent_style = space\nindent_size = 2", "end_of_line" )]
    [InlineData( "indent_style = tab", "end_of_line" )]
    public void Reports_first_missing_key( string section, string expected )
    {
        XmlFormatOptions? options = Create( section, out string? missing );

        Assert.Null( options );
        Assert.Equal( expected, missing );
    }


    /// <summary />
    [Theory]
    [InlineData( "indent_style = tab", "\t" )]
    [InlineData( "indent_style = tab\nindent_size = 4", "\t" )]
    [InlineData( "indent_style = space\nindent_size = 4", "    " )]
    [InlineData( "indent_style = space\nindent_size = 4\ntab_width = 8", "    " )]
    [InlineData( "indent_style = space\ntab_width = 3", "   " )]
    [InlineData( "indent_style = space\nindent_size = tab\ntab_width = 3", "   " )]
    public void Resolves_indentation( string section, string expected )
    {
        XmlFormatOptions options = CreateValid( section + "\nend_of_line = lf" );

        Assert.Equal( expected, options.IndentChars );
    }


    /// <summary />
    [Theory]
    [InlineData( "lf", "\n" )]
    [InlineData( "crlf", "\r\n" )]
    [InlineData( "cr", "\r" )]
    public void Resolves_end_of_line( string value, string expected )
    {
        XmlFormatOptions options = CreateValid( "indent_style = tab\nend_of_line = " + value );

        Assert.Equal( expected, options.NewLine );
    }


    /// <summary />
    [Theory]
    [InlineData( "utf-8", "utf-8", false )]
    [InlineData( "utf-8-bom", "utf-8", true )]
    [InlineData( "latin1", "iso-8859-1", false )]
    [InlineData( "utf-16le", "utf-16", true )]
    [InlineData( "utf-16be", "utf-16BE", true )]
    public void Resolves_charset( string value, string webName, bool preamble )
    {
        XmlFormatOptions options = CreateValid( "indent_style = tab\nend_of_line = lf\ncharset = " + value );

        Assert.NotNull( options.Charset );
        Assert.Equal( webName, options.Charset.WebName );
        Assert.Equal( preamble, options.Charset.GetPreamble().Length > 0 );
    }


    /// <summary />
    [Theory]
    [InlineData( "", null )]
    [InlineData( "insert_final_newline = true", true )]
    [InlineData( "insert_final_newline = false", false )]
    public void Resolves_insert_final_newline( string line, bool? expected )
    {
        XmlFormatOptions options = CreateValid( "indent_style = tab\nend_of_line = lf\n" + line );

        Assert.Equal( expected, options.InsertFinalNewline );
    }


    /// <summary />
    [Theory]
    [InlineData( "", false )]
    [InlineData( "trim_trailing_whitespace = true", true )]
    [InlineData( "trim_trailing_whitespace = false", false )]
    public void Resolves_trim_trailing_whitespace( string line, bool expected )
    {
        XmlFormatOptions options = CreateValid( "indent_style = tab\nend_of_line = lf\n" + line );

        Assert.Equal( expected, options.TrimTrailingWhitespace );
    }


    /// <summary />
    [Fact]
    public void Leaves_charset_unset_when_not_declared()
    {
        XmlFormatOptions options = CreateValid( "indent_style = tab\nend_of_line = lf" );

        Assert.Null( options.Charset );
    }


    /// <summary />
    private static XmlFormatOptions CreateValid( string section )
    {
        XmlFormatOptions? options = Create( section, out string? missing );

        Assert.Null( missing );
        Assert.NotNull( options );

        return options;
    }


    /// <summary>
    /// Resolves <paramref name="section" /> as the [*.xml] section of a root
    /// .editorconfig, the same way FormatCommand does.
    /// </summary>
    private static XmlFormatOptions? Create( string section, out string? missing )
    {
        using var dir = new TempDirectory();

        dir.Write( ".editorconfig", "root = true\n\n[*.xml]\n" + section + "\n" );

        FileConfiguration config = new EditorConfigParser().Parse( Path.Combine( dir.Root, "file.xml" ) );

        return XmlFormatOptions.TryCreate( config, out missing );
    }
}
