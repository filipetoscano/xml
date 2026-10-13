using System.Text;
using System.Xml;

namespace Lefty.Xml.Tests;

/// <summary />
public class XmlInspectorTests
{
    /// <summary />
    [Fact]
    public void Reports_size_lines_and_content_counts()
    {
        XmlInfo info = Inspect( "<a x=\"1\">\n  <b y=\"2\" z=\"3\">\n    <c />\n  </b>\n  <b />\n</a>\n" );

        Assert.Equal( 58, info.Size );
        Assert.Equal( 6, info.Lines );
        Assert.Equal( 4, info.Elements );
        Assert.Equal( 3, info.Attributes );
        Assert.Equal( 3, info.Depth );
    }


    /// <summary />
    [Fact]
    public void Counts_final_line_without_newline()
    {
        XmlInfo info = Inspect( "<a>\n</a>" );

        Assert.Equal( 2, info.Lines );
        Assert.False( info.FinalNewline );
    }


    /// <summary />
    [Fact]
    public void Reports_declaration()
    {
        XmlInfo info = Inspect( "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><a />" );

        Assert.Equal( new XmlDeclarationInfo( "1.0", "UTF-8", "yes" ), info.Declaration );
    }


    /// <summary />
    [Fact]
    public void Reports_missing_declaration_and_doctype()
    {
        XmlInfo info = Inspect( "<a />" );

        Assert.Null( info.Declaration );
        Assert.Null( info.DocType );
        Assert.Equal( "utf-8", info.Encoding );
        Assert.Null( info.ByteOrderMark );
    }


    /// <summary />
    [Theory]
    [InlineData( "<!DOCTYPE a [<!ENTITY e \"x\">]><a>&e;</a>", "a", null, null, true )]
    [InlineData( "<!DOCTYPE a SYSTEM \"a.dtd\"><a />", "a", null, "a.dtd", false )]
    [InlineData( "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"x.dtd\"><html>&nbsp;</html>", "html", "-//W3C//DTD XHTML 1.0 Strict//EN", "x.dtd", false )]
    public void Reports_doctype( string input, string name, string? publicId, string? systemId, bool internalSubset )
    {
        XmlInfo info = Inspect( input );

        Assert.Equal( new XmlDocTypeInfo( name, publicId, systemId, internalSubset ), info.DocType );
    }


    /// <summary />
    [Fact]
    public void Reports_root_and_every_distinct_namespace()
    {
        const string input = """
            <o:order xmlns:o="urn:order" xmlns="urn:default">
              <item xmlns:m="urn:meta" m:x="1" />
              <item xmlns:m="urn:meta" />
              <inner xmlns="urn:inner" />
            </o:order>
            """;

        XmlInfo info = Inspect( input );

        Assert.Equal( new XmlName( "o", "order", "urn:order" ), info.Root );
        Assert.Equal( "o:order", info.Root.QualifiedName );
        Assert.Equal(
            [
                new XmlNamespaceInfo( "o", "urn:order" ),
                new XmlNamespaceInfo( "", "urn:default" ),
                new XmlNamespaceInfo( "m", "urn:meta" ),
                new XmlNamespaceInfo( "", "urn:inner" ),
            ],
            info.Namespaces );
        Assert.Equal( 1, info.Attributes );
    }


    /// <summary />
    [Fact]
    public void Reports_schema_locations_of_root()
    {
        const string input = """
            <order xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                   xsi:schemaLocation="urn:a a.xsd
                                       urn:b b.xsd urn:dangling"
                   xsi:noNamespaceSchemaLocation=" plain.xsd ">
              <child xsi:noNamespaceSchemaLocation="ignored.xsd" />
            </order>
            """;

        XmlInfo info = Inspect( input );

        Assert.Equal(
            [
                new XmlSchemaLocationInfo( "urn:a", "a.xsd" ),
                new XmlSchemaLocationInfo( "urn:b", "b.xsd" ),
                new XmlSchemaLocationInfo( "urn:dangling", "" ),
                new XmlSchemaLocationInfo( null, "plain.xsd" ),
            ],
            info.SchemaLocations );
    }


    /// <summary />
    [Theory]
    [InlineData( "<a>\n<b />\n</a>\n", "LF" )]
    [InlineData( "<a>\r\n<b />\r\n</a>\r\n", "CRLF" )]
    [InlineData( "<a>\r<b />\r</a>", "CR" )]
    [InlineData( "<a />", "none" )]
    [InlineData( "<a>\r\n<b />\n<c />\n</a>\r\n", "mixed (LF 2, CRLF 2)" )]
    public void Reports_line_endings( string input, string expected )
    {
        Assert.Equal( expected, Inspect( input ).LineEndings );
    }


    /// <summary />
    [Theory]
    [InlineData( "<a />", "none" )]
    [InlineData( "<a>\n<b />\n</a>", "none" )]
    [InlineData( "<a>\n\t<b>\n\t\t<c />\n\t</b>\n</a>", "tabs" )]
    [InlineData( "<a>\n  <b>\n    <c />\n  </b>\n</a>", "2 spaces" )]
    [InlineData( "<a>\n    <b>\n        <c />\n    </b>\n</a>", "4 spaces" )]
    [InlineData( "<a>\n\t<b />\n  <c />\n</a>", "mixed" )]
    public void Reports_indentation( string input, string expected )
    {
        Assert.Equal( expected, Inspect( input ).Indentation );
    }


    /// <summary />
    [Fact]
    public void Indentation_ignores_hand_aligned_attributes()
    {
        const string input = """
            <a>
              <b x="1"
                 y="2">
                <c />
                <c />
              </b>
              <d />
            </a>
            """;

        Assert.Equal( "2 spaces", Inspect( input ).Indentation );
    }


    /// <summary />
    [Fact]
    public void Reports_utf8_bom()
    {
        byte[] source = [ .. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes( "<a />\n" ) ];

        XmlInfo info = XmlInspector.Inspect( source );

        Assert.Equal( "utf-8", info.ByteOrderMark );
        Assert.Equal( "utf-8", info.Encoding );
        Assert.Equal( 1, info.Lines );
    }


    /// <summary />
    [Theory]
    [InlineData( false, "utf-16le" )]
    [InlineData( true, "utf-16be" )]
    public void Reports_utf16_bom( bool bigEndian, string bom )
    {
        var encoding = new UnicodeEncoding( bigEndian, true );
        byte[] source = [ .. encoding.GetPreamble(), .. encoding.GetBytes( "<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n<a />\r\n" ) ];

        XmlInfo info = XmlInspector.Inspect( source );

        Assert.Equal( bom, info.ByteOrderMark );
        Assert.Equal( "utf-16", info.Declaration?.Encoding );
        Assert.Equal( "CRLF", info.LineEndings );
        Assert.Equal( 2, info.Lines );
    }


    /// <summary />
    [Fact]
    public void Reports_encoding_named_in_declaration()
    {
        byte[] source = Encoding.Latin1.GetBytes( "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?><a>é</a>" );

        XmlInfo info = XmlInspector.Inspect( source );

        Assert.Equal( "iso-8859-1", info.Encoding );
        Assert.Equal( "iso-8859-1", info.Declaration?.Encoding );
    }


    /// <summary />
    [Theory]
    [InlineData( "" )]
    [InlineData( "<a>" )]
    [InlineData( "<a><b></a>" )]
    [InlineData( "<a /><b />" )]
    public void Throws_on_malformed_input( string input )
    {
        Assert.ThrowsAny<XmlException>( () => Inspect( input ) );
    }


    /// <summary />
    private static XmlInfo Inspect( string input )
    {
        return XmlInspector.Inspect( Encoding.UTF8.GetBytes( input ) );
    }
}
