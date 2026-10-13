using System.Xml;
using System.Xml.Schema;

namespace Lefty.Xml;

/// <summary>
/// Outcome of validating a single document.
/// </summary>
public enum ValidationStatus
{
    /// <summary>
    /// The document could not be parsed.
    /// </summary>
    NotWellFormed,

    /// <summary>
    /// The document parsed, and no schema was available to validate it against.
    /// </summary>
    WellFormed,

    /// <summary>
    /// The document parsed and conforms to its schema.
    /// </summary>
    Valid,

    /// <summary>
    /// The document parsed, but does not conform to its schema.
    /// </summary>
    Invalid,

    /// <summary>
    /// The document parsed, but the schema it references could not be loaded.
    /// </summary>
    SchemaError,
}


/// <summary>
/// A single problem found in a document. Line and column are 1-based, and 0 when
/// the problem has no position (such as a schema which failed to load).
/// </summary>
public sealed record ValidationError( int Line, int Column, string Message );


/// <summary>
/// Outcome of validating a single document, with every problem found.
/// </summary>
public sealed record ValidationResult( ValidationStatus Status, IReadOnlyList<ValidationError> Errors );


/// <summary>
/// Checks XML documents for well-formedness and, when a schema is available,
/// validity against it.
/// </summary>
public static class XmlValidator
{
    /// <summary>
    /// Loads the XSD schemas at <paramref name="paths" />, together with every
    /// schema they include or import, and compiles them into a set. Throws
    /// <see cref="XmlSchemaException" />, <see cref="XmlException" /> or
    /// <see cref="IOException" /> when a schema cannot be loaded.
    /// </summary>
    public static XmlSchemaSet LoadSchemas( IEnumerable<string> paths )
    {
        var set = new XmlSchemaSet()
        {
            XmlResolver = new LocalResolver(),
        };

        /*
         * Without a handler, errors throw but warnings are dropped -- and an
         * xs:include which cannot be resolved is only a warning. Silently missing
         * part of a schema would make documents fail or pass for the wrong reason.
         */
        set.ValidationEventHandler += ( _, e ) => throw e.Exception;

        foreach ( string path in paths )
        {
            using XmlReader reader = XmlReader.Create( Path.GetFullPath( path ), CreateSettings() );
            set.Add( null, reader );
        }

        set.Compile();

        return set;
    }


    /// <summary>
    /// Whether <paramref name="ex" /> is one which <see cref="LoadSchemas" /> raises
    /// for a schema that cannot be loaded.
    /// </summary>
    public static bool IsSchemaLoadFailure( Exception ex )
    {
        return ex is XmlSchemaException
            || ex is XmlException
            || ex is IOException
            || ex is UnauthorizedAccessException;
    }


    /// <summary>
    /// Validates the document in <paramref name="source" />, which was read from
    /// <paramref name="path" />. When <paramref name="schemas" /> is null, the
    /// schemas named by the root element's xsi:schemaLocation or
    /// xsi:noNamespaceSchemaLocation are used, resolved relative to the document.
    /// </summary>
    public static ValidationResult Validate( byte[] source, string path, XmlSchemaSet? schemas )
    {
        string baseUri = new Uri( Path.GetFullPath( path ) ).AbsoluteUri;


        /*
         * First pass: well-formedness, picking up the schema hints on the root
         * element along the way.
         */
        RootElement root;

        try
        {
            root = ReadRoot( source, baseUri );
        }
        catch ( XmlException ex )
        {
            return Fail( ValidationStatus.NotWellFormed, ex.LineNumber, ex.LinePosition, ex.Message );
        }


        /*
         * An explicit schema takes precedence over whatever the document points at.
         */
        if ( schemas == null )
        {
            if ( root.HasSchemaLocation == false )
                return new ValidationResult( ValidationStatus.WellFormed, [] );

            try
            {
                schemas = LoadSchemas( ResolveLocations( root, baseUri ) );
            }
            catch ( Exception ex ) when ( IsSchemaLoadFailure( ex ) == true )
            {
                return Fail( ValidationStatus.SchemaError, 0, 0, ex.Message );
            }
        }


        /*
         * The validating reader stays silent about elements in a namespace which no
         * schema covers, so a document of the wrong type entirely would come out as
         * valid. Insist that the root, at least, is declared.
         */
        if ( schemas.GlobalElements.Contains( root.Name ) == false )
            return Fail( ValidationStatus.Invalid, root.Line, root.Column, $"No schema declares the root element '{root.Name}'." );


        /*
         * Second pass: validation, collecting every error rather than stopping at
         * the first.
         */
        var errors = new List<ValidationError>();

        XmlReaderSettings settings = CreateSettings();
        settings.ValidationType = ValidationType.Schema;
        settings.Schemas = schemas;
        settings.ValidationEventHandler += ( _, e ) =>
        {
            if ( e.Severity == XmlSeverityType.Error )
                errors.Add( new ValidationError( e.Exception.LineNumber, e.Exception.LinePosition, e.Message ) );
        };

        using ( var stream = new MemoryStream( source, false ) )
        using ( XmlReader reader = XmlReader.Create( stream, settings, baseUri ) )
        {
            while ( reader.Read() )
            {
            }
        }

        return new ValidationResult( errors.Count == 0 ? ValidationStatus.Valid : ValidationStatus.Invalid, errors );
    }


    /// <summary />
    private readonly record struct RootElement( XmlQualifiedName Name, int Line, int Column, string? SchemaLocation, string? NoNamespaceSchemaLocation )
    {
        /// <summary />
        public bool HasSchemaLocation => SchemaLocation != null || NoNamespaceSchemaLocation != null;
    }


    /// <summary>
    /// Reads the whole document, so that any well-formedness error surfaces, and
    /// returns its root element along with the schema locations it names.
    /// </summary>
    private static RootElement ReadRoot( byte[] source, string baseUri )
    {
        using var stream = new MemoryStream( source, false );
        using XmlReader reader = XmlReader.Create( stream, CreateSettings(), baseUri );

        RootElement? root = null;

        while ( reader.Read() )
        {
            if ( root != null || reader.NodeType != XmlNodeType.Element )
                continue;

            var info = (IXmlLineInfo) reader;

            root = new RootElement(
                new XmlQualifiedName( reader.LocalName, reader.NamespaceURI ),
                info.LineNumber,
                info.LinePosition,
                reader.GetAttribute( "schemaLocation", XmlSchema.InstanceNamespace ),
                reader.GetAttribute( "noNamespaceSchemaLocation", XmlSchema.InstanceNamespace ) );
        }

        return root ?? throw new XmlException( "Root element is missing." );
    }


    /// <summary>
    /// Turns the root element's schema hints, which are URIs relative to the
    /// document, into local file paths. Schemas are never fetched over the network.
    /// </summary>
    private static List<string> ResolveLocations( RootElement root, string baseUri )
    {
        var locations = new List<string>();

        if ( root.SchemaLocation != null )
        {
            /*
             * Namespace and location alternate; only the locations are needed,
             * since each schema declares its own target namespace.
             */
            string[] tokens = root.SchemaLocation.Split( (char[]?) null, StringSplitOptions.RemoveEmptyEntries );

            if ( tokens.Length % 2 != 0 )
                throw new XmlSchemaException( "xsi:schemaLocation must hold pairs of namespace and location." );

            for ( int i = 1; i < tokens.Length; i += 2 )
                locations.Add( tokens[ i ] );
        }

        if ( string.IsNullOrWhiteSpace( root.NoNamespaceSchemaLocation ) == false )
            locations.Add( root.NoNamespaceSchemaLocation.Trim() );

        var paths = new List<string>();
        var seen = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

        foreach ( string location in locations )
        {
            if ( Uri.TryCreate( new Uri( baseUri ), location, out Uri? uri ) == false )
                throw new XmlSchemaException( $"Schema location '{location}' is not a valid URI." );

            if ( uri.IsFile == false )
                throw new XmlSchemaException( $"Schema location '{location}' is not a local file; remote schemas are not fetched." );

            if ( seen.Add( uri.LocalPath ) == true )
                paths.Add( uri.LocalPath );
        }

        if ( paths.Count == 0 )
            throw new XmlSchemaException( "The schema location attributes name no schema." );

        return paths;
    }


    /// <summary />
    private static ValidationResult Fail( ValidationStatus status, int line, int column, string message )
    {
        return new ValidationResult( status, [ new ValidationError( line, column, message ) ] );
    }


    /// <summary>
    /// DTDs are processed so that internally declared entities resolve, but no
    /// external resource is fetched to do it.
    /// </summary>
    private static XmlReaderSettings CreateSettings()
    {
        return new XmlReaderSettings()
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
        };
    }


    /// <summary>
    /// Resolves xs:include and xs:import against the local file system only.
    /// </summary>
    private sealed class LocalResolver : XmlUrlResolver
    {
        /// <summary />
        public override object? GetEntity( Uri absoluteUri, string? role, Type? ofObjectToReturn )
        {
            if ( absoluteUri.IsFile == false )
                throw new XmlSchemaException( $"Schema '{absoluteUri}' is not a local file; remote schemas are not fetched." );

            return base.GetEntity( absoluteUri, role, ofObjectToReturn );
        }
    }
}