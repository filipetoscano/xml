using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace Lefty.Xml.Tests;

/// <summary />
public sealed class XmlValidatorTests : IDisposable
{
    /*
     * <order> holds one or more <item>, each with a positive integer quantity.
     */
    private const string OrderSchema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                   targetNamespace="urn:order" xmlns="urn:order" elementFormDefault="qualified">
          <xs:element name="order">
            <xs:complexType>
              <xs:sequence>
                <xs:element name="item" maxOccurs="unbounded">
                  <xs:complexType>
                    <xs:attribute name="qty" type="xs:positiveInteger" use="required" />
                  </xs:complexType>
                </xs:element>
              </xs:sequence>
            </xs:complexType>
          </xs:element>
        </xs:schema>
        """;

    private const string NoNamespaceSchema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="note" type="xs:string" />
        </xs:schema>
        """;

    private const string ValidOrder = """<order xmlns="urn:order"><item qty="1" /><item qty="2" /></order>""";

    private readonly TempDirectory _dir = new TempDirectory();


    /// <summary />
    public void Dispose()
    {
        _dir.Dispose();
    }


    /// <summary />
    [Fact]
    public void Well_formed_without_schema()
    {
        ValidationResult result = Validate( "<a><b /></a>" );

        Assert.Equal( ValidationStatus.WellFormed, result.Status );
        Assert.Empty( result.Errors );
    }


    /// <summary />
    [Fact]
    public void Well_formed_with_internally_declared_entity()
    {
        ValidationResult result = Validate( "<!DOCTYPE a [<!ENTITY e \"x\">]><a>&e;</a>" );

        Assert.Equal( ValidationStatus.WellFormed, result.Status );
    }


    /// <summary />
    [Theory]
    [InlineData( "", 0 )]
    [InlineData( "<a>", 1 )]
    [InlineData( "<a>\n  <b>\n</a>", 3 )]
    [InlineData( "<a /><b />", 1 )]
    [InlineData( "<a>&undeclared;</a>", 1 )]
    public void Not_well_formed( string input, int line )
    {
        ValidationResult result = Validate( input );

        Assert.Equal( ValidationStatus.NotWellFormed, result.Status );
        ValidationError error = Assert.Single( result.Errors );
        Assert.Equal( line, error.Line );
    }


    /// <summary />
    [Fact]
    public void Not_well_formed_takes_precedence_over_schema()
    {
        ValidationResult result = Validate( "<order xmlns=\"urn:order\">", Schemas( OrderSchema ) );

        Assert.Equal( ValidationStatus.NotWellFormed, result.Status );
    }


    /// <summary />
    [Fact]
    public void Valid_against_explicit_schema()
    {
        ValidationResult result = Validate( ValidOrder, Schemas( OrderSchema ) );

        Assert.Equal( ValidationStatus.Valid, result.Status );
        Assert.Empty( result.Errors );
    }


    /// <summary />
    [Fact]
    public void Invalid_against_explicit_schema_reports_every_error()
    {
        const string input = "<order xmlns=\"urn:order\">\n  <item qty=\"0\" />\n  <item />\n  <other />\n</order>";

        ValidationResult result = Validate( input, Schemas( OrderSchema ) );

        Assert.Equal( ValidationStatus.Invalid, result.Status );
        Assert.Equal( [ 2, 3, 4 ], result.Errors.Select( e => e.Line ) );
        Assert.All( result.Errors, e => Assert.True( e.Column > 0 ) );
    }


    /// <summary />
    [Fact]
    public void Invalid_when_schema_does_not_declare_root()
    {
        ValidationResult result = Validate( "<order xmlns=\"urn:other\" />", Schemas( OrderSchema ) );

        Assert.Equal( ValidationStatus.Invalid, result.Status );
        ValidationError error = Assert.Single( result.Errors );
        Assert.Contains( "urn:other:order", error.Message );
    }


    /// <summary />
    [Fact]
    public void Explicit_schema_overrides_schema_location()
    {
        const string input = """
            <order xmlns="urn:order" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                   xsi:schemaLocation="urn:order missing.xsd"><item qty="1" /></order>
            """;

        ValidationResult result = Validate( input, Schemas( OrderSchema ) );

        Assert.Equal( ValidationStatus.Valid, result.Status );
    }


    /// <summary />
    [Theory]
    [InlineData( "<item qty=\"3\" />", ValidationStatus.Valid )]
    [InlineData( "<item qty=\"-3\" />", ValidationStatus.Invalid )]
    public void Uses_schema_location_relative_to_document( string item, ValidationStatus expected )
    {
        _dir.Write( "schemas/order.xsd", OrderSchema );

        string input = $"""
            <order xmlns="urn:order" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                   xsi:schemaLocation="urn:order ../schemas/order.xsd">{item}</order>
            """;

        ValidationResult result = Validate( input, path: "docs/order.xml" );

        Assert.Equal( expected, result.Status );
    }


    /// <summary />
    [Fact]
    public void Uses_no_namespace_schema_location()
    {
        _dir.Write( "note.xsd", NoNamespaceSchema );

        const string input = """
            <note xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                  xsi:noNamespaceSchemaLocation="note.xsd">text<b /></note>
            """;

        ValidationResult result = Validate( input );

        Assert.Equal( ValidationStatus.Invalid, result.Status );
    }


    /// <summary />
    [Theory]
    [InlineData( "1", ValidationStatus.Valid )]
    [InlineData( "one", ValidationStatus.Invalid )]
    public void Uses_every_schema_location_pair( string value, ValidationStatus expected )
    {
        /*
         * The wildcard is strict, so <m:tag> only validates if meta.xsd -- named by
         * the second pair -- was loaded too.
         */
        _dir.Write( "wrap.xsd", """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:wrap">
              <xs:element name="wrap">
                <xs:complexType>
                  <xs:sequence><xs:any namespace="urn:meta" processContents="strict" /></xs:sequence>
                </xs:complexType>
              </xs:element>
            </xs:schema>
            """ );
        _dir.Write( "meta.xsd", """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:meta">
              <xs:element name="tag" type="xs:int" />
            </xs:schema>
            """ );

        string input = $"""
            <wrap xmlns="urn:wrap" xmlns:m="urn:meta" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                  xsi:schemaLocation="urn:wrap wrap.xsd
                                      urn:meta meta.xsd"><m:tag>{value}</m:tag></wrap>
            """;

        ValidationResult result = Validate( input );

        Assert.Equal( expected, result.Status );
    }


    /// <summary />
    [Theory]
    [InlineData( "xsi:schemaLocation=\"urn:order missing.xsd\"" )]
    [InlineData( "xsi:schemaLocation=\"urn:order\"" )]
    [InlineData( "xsi:schemaLocation=\"\"" )]
    [InlineData( "xsi:schemaLocation=\"urn:order http://invalid.example/order.xsd\"" )]
    [InlineData( "xsi:noNamespaceSchemaLocation=\"broken.xsd\"" )]
    public void Schema_error_when_schema_location_cannot_be_loaded( string attribute )
    {
        _dir.Write( "broken.xsd", "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"><xs:element name=\"a\" type=\"undefined\" /></xs:schema>" );

        string input = $"""<order xmlns="urn:order" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" {attribute} />""";

        ValidationResult result = Validate( input );

        Assert.Equal( ValidationStatus.SchemaError, result.Status );
        Assert.Single( result.Errors );
    }


    /// <summary />
    [Fact]
    public void Load_follows_includes_relative_to_the_including_schema()
    {
        _dir.Write( "main/main.xsd", """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:include schemaLocation="../types/types.xsd" />
              <xs:element name="size" type="Size" />
            </xs:schema>
            """ );
        _dir.Write( "types/types.xsd", """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:simpleType name="Size">
                <xs:restriction base="xs:int"><xs:maxInclusive value="10" /></xs:restriction>
              </xs:simpleType>
            </xs:schema>
            """ );

        XmlSchemaSet set = XmlValidator.LoadSchemas( [ Path.Combine( _dir.Root, "main", "main.xsd" ) ] );

        Assert.Equal( ValidationStatus.Valid, Validate( "<size>10</size>", set ).Status );
        Assert.Equal( ValidationStatus.Invalid, Validate( "<size>11</size>", set ).Status );
    }


    /// <summary />
    [Theory]
    [InlineData( "<xs:include schemaLocation=\"missing.xsd\" />" )]
    [InlineData( "<xs:include schemaLocation=\"http://invalid.example/types.xsd\" />" )]
    [InlineData( "<xs:element name=\"a\" type=\"undefined\" />" )]
    [InlineData( "<xs:element" )]
    public void Load_fails_on_broken_schema( string content )
    {
        string path = _dir.Write( "broken.xsd", "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\">" + content + "</xs:schema>" );

        Exception ex = Assert.ThrowsAny<Exception>( () => XmlValidator.LoadSchemas( [ path ] ) );

        Assert.True( XmlValidator.IsSchemaLoadFailure( ex ), ex.ToString() );
    }


    /// <summary />
    [Fact]
    public void Load_fails_on_missing_file()
    {
        Exception ex = Assert.ThrowsAny<Exception>( () => XmlValidator.LoadSchemas( [ Path.Combine( _dir.Root, "missing.xsd" ) ] ) );

        Assert.True( XmlValidator.IsSchemaLoadFailure( ex ), ex.ToString() );
    }


    /// <summary />
    private XmlSchemaSet Schemas( string content )
    {
        return XmlValidator.LoadSchemas( [ _dir.Write( "explicit/schema.xsd", content ) ] );
    }


    /// <summary />
    private ValidationResult Validate( string input, XmlSchemaSet? schemas = null, string path = "doc.xml" )
    {
        return XmlValidator.Validate( Encoding.UTF8.GetBytes( input ), Path.Combine( _dir.Root, path ), schemas );
    }
}