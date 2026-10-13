using System.Text;
using System.Xml;

namespace Lefty.Xml.Tests;

/// <summary />
public class XmlFormatterTests
{
    private static readonly XmlFormatOptions Lf = new XmlFormatOptions()
    {
        IndentChars = "  ",
        NewLine = "\n",
        InsertFinalNewline = true,
        TrimTrailingWhitespace = true,
    };


    /// <summary />
    [Fact]
    public void Reindents_nested_elements()
    {
        string actual = Format( "<a><b><c /></b><d>text</d></a>" );

        Assert.Equal( "<a>\n  <b>\n    <c />\n  </b>\n  <d>text</d>\n</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Indents_with_tabs()
    {
        string actual = Format( "<a>\n    <b>\n        <c />\n    </b>\n</a>", Lf with { IndentChars = "\t" } );

        Assert.Equal( "<a>\n\t<b>\n\t\t<c />\n\t</b>\n</a>\n", actual );
    }


    /// <summary />
    [Theory]
    [InlineData( "\n" )]
    [InlineData( "\r\n" )]
    [InlineData( "\r" )]
    public void Emits_configured_line_ending( string newLine )
    {
        string actual = Format( "<a>\r\n<b>x\ny</b>\r<c />\n</a>\n", Lf with { NewLine = newLine } );

        string expected = "<a>|  <b>x|y</b>|  <c />|</a>|".Replace( "|", newLine );
        Assert.Equal( expected, actual );
    }


    /// <summary />
    [Fact]
    public void Is_idempotent()
    {
        const string input = """
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE root [ <!ENTITY e "entity"> ]>
            <root   a="1"     b="2">
                  <!-- comment -->


              <child>text &e; &lt;</child>
                <empty></empty>
            <pre xml:space="preserve">  keep
                 this   </pre>
              <![CDATA[ <raw> ]]>
            <?pi data?>
            </root>
            """;

        string once = Format( input );
        string twice = Format( once );

        Assert.Equal( once, twice );
    }


    /// <summary />
    [Fact]
    public void Keeps_attribute_order_and_values()
    {
        string actual = Format( "<a z=\"1\" b=\"two words\" m=\"&lt;&amp;\" />" );

        Assert.Equal( "<a z=\"1\" b=\"two words\" m=\"&lt;&amp;\" />\n", actual );
    }


    /// <summary />
    [Fact]
    public void Keeps_self_closing_and_paired_empty_elements()
    {
        string actual = Format( "<a><b /><c></c></a>" );

        Assert.Equal( "<a>\n  <b />\n  <c></c>\n</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Keeps_comments_cdata_and_processing_instructions()
    {
        string actual = Format( "<a><!-- note --><b><![CDATA[ <x> & y ]]></b><?target data?></a>" );

        Assert.Equal( "<a>\n  <!-- note -->\n  <b><![CDATA[ <x> & y ]]></b>\n  <?target data?>\n</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Keeps_escaped_text()
    {
        string actual = Format( "<a>&lt;b&gt; &amp; &quot;c&quot;</a>" );

        Assert.Equal( "<a>&lt;b&gt; &amp; \"c\"</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Keeps_doctype_and_entity_references()
    {
        string actual = Format( "<!DOCTYPE a [<!ENTITY e \"expanded\">]><a><b>&e;</b></a>" );

        Assert.Equal( "<!DOCTYPE a [<!ENTITY e \"expanded\">]>\n<a>\n  <b>&e;</b>\n</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Keeps_external_doctype_without_resolving_it()
    {
        const string input = "<!DOCTYPE a SYSTEM \"http://invalid.example/a.dtd\"><a><b /></a>";

        string actual = Format( input );

        Assert.Equal( "<!DOCTYPE a SYSTEM \"http://invalid.example/a.dtd\">\n<a>\n  <b />\n</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Leaves_mixed_content_on_one_line()
    {
        string actual = Format( "<a><p>Hello <b>big</b> world</p></a>" );

        Assert.Equal( "<a>\n  <p>Hello <b>big</b> world</p>\n</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Leaves_xml_space_preserve_regions_untouched()
    {
        const string region = "<pre xml:space=\"preserve\">\n      <b>  x  </b>\n   <c />\n</pre>";

        string actual = Format( "<a>\n" + region + "\n<d /></a>" );

        Assert.Equal( "<a>\n  " + region + "\n  <d />\n</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Collapses_runs_of_blank_lines_into_one()
    {
        string actual = Format( "<a>\n  <b />\n\n\n\n  <c />\n</a>" );

        Assert.Equal( "<a>\n  <b />\n\n  <c />\n</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Keeps_blank_lines_before_comments_and_at_document_level()
    {
        const string input = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n\n<!-- head -->\n<a>\n  <b />\n\n  <!-- c -->\n  <?pi?>\n</a>";

        string actual = Format( input );

        Assert.Equal( "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n\n<!-- head -->\n<a>\n  <b />\n\n  <!-- c -->\n  <?pi?>\n</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Blank_lines_survive_crlf_input_and_output()
    {
        XmlFormatOptions crlf = Lf with { NewLine = "\r\n" };

        string actual = Format( "<a>\r\n\r\n<b />\r\n\r\n</a>\r\n", crlf );

        Assert.Equal( "<a>\r\n\r\n  <b />\r\n\r\n</a>\r\n", actual );
    }


    /// <summary />
    [Fact]
    public void Omits_declaration_when_source_has_none()
    {
        string actual = Format( "<a />" );

        Assert.Equal( "<a />\n", actual );
    }


    /// <summary />
    [Fact]
    public void Keeps_declaration_with_standalone()
    {
        string actual = Format( "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?><a />" );

        Assert.Equal( "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\n<a />\n", actual );
    }


    /// <summary />
    [Theory]
    [InlineData( true, "<a />", "<a />\n" )]
    [InlineData( true, "<a />\n\n\n", "<a />\n" )]
    [InlineData( false, "<a />\n", "<a />" )]
    [InlineData( null, "<a />", "<a />" )]
    [InlineData( null, "<a />\n", "<a />\n" )]
    [InlineData( null, "<a />\r\n", "<a />\n" )]
    public void Applies_final_newline( bool? insert, string input, string expected )
    {
        string actual = Format( input, Lf with { InsertFinalNewline = insert } );

        Assert.Equal( expected, actual );
    }


    /// <summary />
    [Fact]
    public void Trims_trailing_whitespace_when_asked()
    {
        string actual = Format( "<a><!-- one   \n  two\t --></a>" );

        Assert.Equal( "<a>\n  <!-- one\n  two\t -->\n</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Keeps_trailing_whitespace_when_not_asked()
    {
        string actual = Format( "<a><!-- one   \n  two --></a>", Lf with { TrimTrailingWhitespace = false } );

        Assert.Equal( "<a>\n  <!-- one   \n  two -->\n</a>\n", actual );
    }


    /// <summary />
    [Fact]
    public void Keeps_utf8_bom_when_charset_unset()
    {
        byte[] source = [ .. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes( "<a>é</a>" ) ];

        byte[] actual = XmlFormatter.Format( source, Lf );

        Assert.Equal( [ .. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes( "<a>é</a>\n" ) ], actual );
    }


    /// <summary />
    [Fact]
    public void Removes_utf8_bom_when_charset_is_utf8()
    {
        byte[] source = [ .. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes( "<a />" ) ];

        byte[] actual = XmlFormatter.Format( source, Lf with { Charset = new UTF8Encoding( false ) } );

        Assert.Equal( Encoding.UTF8.GetBytes( "<a />\n" ), actual );
    }


    /// <summary />
    [Fact]
    public void Adds_utf8_bom_when_charset_is_utf8_bom()
    {
        byte[] source = Encoding.UTF8.GetBytes( "<a />" );

        byte[] actual = XmlFormatter.Format( source, Lf with { Charset = new UTF8Encoding( true ) } );

        Assert.Equal( [ .. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes( "<a />\n" ) ], actual );
    }


    /// <summary />
    [Fact]
    public void Keeps_utf16_encoding_detected_from_bom()
    {
        var utf16 = new UnicodeEncoding( false, true );
        byte[] source = [ .. utf16.GetPreamble(), .. utf16.GetBytes( "<?xml version=\"1.0\" encoding=\"utf-16\"?><a>é</a>\n" ) ];

        byte[] actual = XmlFormatter.Format( source, Lf );

        Assert.Equal( [ .. utf16.GetPreamble(), .. utf16.GetBytes( "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<a>é</a>\n" ) ], actual );
    }


    /// <summary />
    [Fact]
    public void Keeps_encoding_named_in_declaration()
    {
        byte[] source = Encoding.Latin1.GetBytes( "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?><a>é</a>" );

        byte[] actual = XmlFormatter.Format( source, Lf );

        Assert.Equal( Encoding.Latin1.GetBytes( "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?>\n<a>é</a>\n" ), actual );
    }


    /// <summary />
    [Fact]
    public void Converts_to_charset_and_updates_declaration()
    {
        byte[] source = Encoding.Latin1.GetBytes( "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?><a>é</a>" );

        byte[] actual = XmlFormatter.Format( source, Lf with { Charset = new UTF8Encoding( false ) } );

        Assert.Equal( Encoding.UTF8.GetBytes( "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<a>é</a>\n" ), actual );
    }


    /// <summary />
    [Theory]
    [InlineData( "" )]
    [InlineData( "<a>" )]
    [InlineData( "<a><b></a>" )]
    [InlineData( "<a /><b />" )]
    public void Throws_on_malformed_input( string input )
    {
        Assert.ThrowsAny<XmlException>( () => Format( input ) );
    }


    /// <summary />
    private static string Format( string input, XmlFormatOptions? options = null )
    {
        byte[] formatted = XmlFormatter.Format( Encoding.UTF8.GetBytes( input ), options ?? Lf );

        return Encoding.UTF8.GetString( formatted );
    }
}