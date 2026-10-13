using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace Lefty.Xml;

/// <summary>
/// An element or attribute name, as written in the document.
/// </summary>
public sealed record XmlName( string Prefix, string LocalName, string NamespaceUri )
{
    /// <summary>
    /// Qualified name as written, such as "xs:schema".
    /// </summary>
    public string QualifiedName => Prefix.Length == 0 ? LocalName : Prefix + ":" + LocalName;
}


/// <summary>
/// The XML declaration, as written.
/// </summary>
public sealed record XmlDeclarationInfo( string Version, string? Encoding, string? Standalone );


/// <summary>
/// The document type declaration.
/// </summary>
public sealed record XmlDocTypeInfo( string Name, string? PublicId, string? SystemId, bool HasInternalSubset );


/// <summary>
/// A namespace declaration; the prefix is empty for the default namespace.
/// </summary>
public sealed record XmlNamespaceInfo( string Prefix, string Uri );


/// <summary>
/// A schema location hint on the root element; the namespace is null for
/// xsi:noNamespaceSchemaLocation.
/// </summary>
public sealed record XmlSchemaLocationInfo( string? Namespace, string Location );


/// <summary>
/// Facts about an XML document, as reported by the info command.
/// </summary>
public sealed record XmlInfo
{
    /// <summary>
    /// Size of the file, in bytes.
    /// </summary>
    public required int Size { get; init; }

    /// <summary>
    /// Number of lines, counting a final line without a line ending.
    /// </summary>
    public required int Lines { get; init; }

    /// <summary>
    /// Encoding the document was actually read with.
    /// </summary>
    public required string Encoding { get; init; }

    /// <summary>
    /// Encoding named by the byte order mark: "utf-8", "utf-16le" or "utf-16be";
    /// null when there is none.
    /// </summary>
    public string? ByteOrderMark { get; init; }

    /// <summary>
    /// The XML declaration; null when there is none.
    /// </summary>
    public XmlDeclarationInfo? Declaration { get; init; }

    /// <summary>
    /// The document type declaration; null when there is none.
    /// </summary>
    public XmlDocTypeInfo? DocType { get; init; }

    /// <summary>
    /// The root element.
    /// </summary>
    public required XmlName Root { get; init; }

    /// <summary>
    /// Every distinct namespace declaration in the document, in order of appearance.
    /// </summary>
    public required IReadOnlyList<XmlNamespaceInfo> Namespaces { get; init; }

    /// <summary>
    /// Schema location hints on the root element.
    /// </summary>
    public required IReadOnlyList<XmlSchemaLocationInfo> SchemaLocations { get; init; }

    /// <summary>
    /// "LF", "CRLF", "CR", "none", or "mixed" followed by the count of each.
    /// </summary>
    public required string LineEndings { get; init; }

    /// <summary>
    /// Whether the file ends with a line ending.
    /// </summary>
    public required bool FinalNewline { get; init; }

    /// <summary>
    /// Best guess at the indentation: "tabs", "N spaces", "mixed" or "none".
    /// </summary>
    public required string Indentation { get; init; }

    /// <summary>
    /// Number of elements.
    /// </summary>
    public required int Elements { get; init; }

    /// <summary>
    /// Number of attributes, not counting namespace declarations.
    /// </summary>
    public required int Attributes { get; init; }

    /// <summary>
    /// Deepest element nesting; the root alone is depth 1.
    /// </summary>
    public required int Depth { get; init; }
}


/// <summary>
/// Gathers <see cref="XmlInfo" /> about a document.
/// </summary>
public static class XmlInspector
{
    private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";


    /// <summary>
    /// Inspects the document in <paramref name="source" />. Throws
    /// <see cref="XmlException" /> when it is not well formed.
    /// </summary>
    public static XmlInfo Inspect( byte[] source )
    {
        using var stream = new MemoryStream( source, false );

        /*
         * Read the same way the formatter does: entities declared in an external
         * DTD -- which is never fetched -- are reported, not rejected.
         */
        using var reader = new XmlTextReader( stream )
        {
            WhitespaceHandling = WhitespaceHandling.None,
            EntityHandling = EntityHandling.ExpandCharEntities,
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
        };

        XmlDeclarationInfo? declaration = null;
        XmlDocTypeInfo? docType = null;
        XmlName? root = null;
        var schemaLocations = new List<XmlSchemaLocationInfo>();
        var namespaces = new List<XmlNamespaceInfo>();
        int elements = 0;
        int attributes = 0;
        int depth = 0;

        /*
         * The reader forgets its encoding once it reaches the end of the input.
         */
        Encoding? encoding = null;

        while ( reader.Read() )
        {
            encoding ??= reader.Encoding;

            switch ( reader.NodeType )
            {
                case XmlNodeType.XmlDeclaration:
                    declaration = new XmlDeclarationInfo(
                        reader.GetAttribute( "version" ) ?? "1.0",
                        reader.GetAttribute( "encoding" ),
                        reader.GetAttribute( "standalone" ) );
                    break;

                case XmlNodeType.DocumentType:
                    docType = new XmlDocTypeInfo(
                        reader.Name,
                        reader.GetAttribute( "PUBLIC" ),
                        reader.GetAttribute( "SYSTEM" ),
                        string.IsNullOrEmpty( reader.Value ) == false );
                    break;

                case XmlNodeType.Element:
                    elements++;
                    depth = Math.Max( depth, reader.Depth + 1 );

                    if ( root == null )
                    {
                        root = new XmlName( reader.Prefix, reader.LocalName, reader.NamespaceURI );
                        schemaLocations.AddRange( ReadSchemaLocations( reader ) );
                    }

                    while ( reader.MoveToNextAttribute() == true )
                    {
                        if ( reader.NamespaceURI != XmlnsNamespace )
                        {
                            attributes++;
                            continue;
                        }

                        string prefix = reader.Prefix.Length == 0 ? string.Empty : reader.LocalName;
                        var ns = new XmlNamespaceInfo( prefix, reader.Value );

                        if ( namespaces.Contains( ns ) == false )
                            namespaces.Add( ns );
                    }

                    reader.MoveToElement();
                    break;
            }
        }

        if ( root == null || encoding == null )
            throw new XmlException( "Root element is missing." );

        string text = encoding.GetString( source );

        return new XmlInfo
        {
            Size = source.Length,
            Lines = CountLines( text ),
            Encoding = encoding.WebName,
            ByteOrderMark = DetectByteOrderMark( source ),
            Declaration = declaration,
            DocType = docType,
            Root = root,
            Namespaces = namespaces,
            SchemaLocations = schemaLocations,
            LineEndings = DescribeLineEndings( text ),
            FinalNewline = text.EndsWith( '\n' ) || text.EndsWith( '\r' ),
            Indentation = DescribeIndentation( text ),
            Elements = elements,
            Attributes = attributes,
            Depth = depth,
        };
    }


    /// <summary />
    private static IEnumerable<XmlSchemaLocationInfo> ReadSchemaLocations( XmlReader reader )
    {
        string? pairs = reader.GetAttribute( "schemaLocation", XmlSchema.InstanceNamespace );

        if ( pairs != null )
        {
            string[] tokens = pairs.Split( (char[]?) null, StringSplitOptions.RemoveEmptyEntries );

            /*
             * Reported as written: a dangling namespace without a location is shown
             * with an empty location, rather than hidden.
             */
            for ( int i = 0; i < tokens.Length; i += 2 )
                yield return new XmlSchemaLocationInfo( tokens[ i ], i + 1 < tokens.Length ? tokens[ i + 1 ] : string.Empty );
        }

        string? single = reader.GetAttribute( "noNamespaceSchemaLocation", XmlSchema.InstanceNamespace );

        if ( single != null )
            yield return new XmlSchemaLocationInfo( null, single.Trim() );
    }


    /// <summary />
    private static string? DetectByteOrderMark( byte[] source )
    {
        if ( source.Length >= 3 && source[ 0 ] == 0xEF && source[ 1 ] == 0xBB && source[ 2 ] == 0xBF )
            return "utf-8";

        if ( source.Length >= 2 && source[ 0 ] == 0xFF && source[ 1 ] == 0xFE )
            return "utf-16le";

        if ( source.Length >= 2 && source[ 0 ] == 0xFE && source[ 1 ] == 0xFF )
            return "utf-16be";

        return null;
    }


    /// <summary />
    private static int CountLines( string text )
    {
        if ( text.Length == 0 )
            return 0;

        (int lf, int crlf, int cr) = CountLineEndings( text );
        int breaks = lf + crlf + cr;

        bool final = text.EndsWith( '\n' ) || text.EndsWith( '\r' );

        return final == true ? breaks : breaks + 1;
    }


    /// <summary />
    private static (int Lf, int CrLf, int Cr) CountLineEndings( string text )
    {
        int lf = 0;
        int crlf = 0;
        int cr = 0;

        for ( int i = 0; i < text.Length; i++ )
        {
            if ( text[ i ] == '\n' )
            {
                lf++;
            }
            else if ( text[ i ] == '\r' )
            {
                if ( i + 1 < text.Length && text[ i + 1 ] == '\n' )
                {
                    crlf++;
                    i++;
                }
                else
                {
                    cr++;
                }
            }
        }

        return (lf, crlf, cr);
    }


    /// <summary />
    private static string DescribeLineEndings( string text )
    {
        (int lf, int crlf, int cr) = CountLineEndings( text );

        var kinds = new List<string>();

        if ( lf > 0 )
            kinds.Add( $"LF {lf}" );

        if ( crlf > 0 )
            kinds.Add( $"CRLF {crlf}" );

        if ( cr > 0 )
            kinds.Add( $"CR {cr}" );

        return kinds.Count switch
        {
            0 => "none",
            1 => kinds[ 0 ][ ..kinds[ 0 ].IndexOf( ' ' ) ],
            _ => "mixed (" + string.Join( ", ", kinds ) + ")",
        };
    }


    /// <summary>
    /// Looks at the leading whitespace of every line. Space indentation is taken
    /// to be the most common step between consecutive lines, which is robust to
    /// the odd line aligned by hand -- such as wrapped attributes.
    /// </summary>
    private static string DescribeIndentation( string text )
    {
        string[] lines = text.Split( [ "\r\n", "\n", "\r" ], StringSplitOptions.None );

        int tabbed = 0;
        int spaced = 0;
        var steps = new Dictionary<int, int>();
        int previous = 0;

        foreach ( string line in lines )
        {
            if ( line.Trim().Length == 0 )
                continue;

            if ( line[ 0 ] == '\t' )
            {
                tabbed++;
                previous = 0;
                continue;
            }

            int width = 0;
            while ( width < line.Length && line[ width ] == ' ' )
                width++;

            if ( width > 0 )
                spaced++;

            int step = Math.Abs( width - previous );

            if ( step > 0 )
                steps[ step ] = steps.GetValueOrDefault( step ) + 1;

            previous = width;
        }

        if ( tabbed > 0 && spaced > 0 )
            return "mixed";

        if ( tabbed > 0 )
            return "tabs";

        if ( spaced == 0 )
            return "none";

        /*
         * Ties go to the smaller step: 2 and 4 equally common means 2-space
         * indentation which sometimes skips a level.
         */
        int unit = steps
            .OrderByDescending( kv => kv.Value )
            .ThenBy( kv => kv.Key )
            .First()
            .Key;

        return unit == 1 ? "1 space" : $"{unit} spaces";
    }
}
