using System.Text;
using System.Xml;

namespace Lefty.Xml;

/// <summary>
/// Reformats XML documents. This is a whitespace-only formatter: it re-indents,
/// normalizes line endings and applies the encoding, but never rewrites markup.
/// Attribute order and quoting, self-closing versus paired empty elements, entity
/// references, CDATA sections and comments all survive unchanged.
/// </summary>
public static class XmlFormatter
{
    /*
     * Stand-in for an author's blank line while the document is being written. It
     * is drawn fresh per run so it cannot collide with a comment already in the file:
     * no file can contain a guid minted after it was written.
     */
    private static readonly string BlankMarker = "lxml-blank-" + Guid.NewGuid().ToString( "N" );
    private static readonly string BlankComment = "<!--" + BlankMarker + "-->";


    /// <summary>
    /// Formats <paramref name="source" /> and returns the result. Throws
    /// <see cref="XmlException" /> when the input is not well formed.
    /// </summary>
    public static byte[] Format( byte[] source, XmlFormatOptions options )
    {
        Declaration declaration = ReadDeclaration( source );
        Encoding encoding = options.Charset ?? DetectEncoding( source, declaration.Encoding );

        string text = Write( source, options, encoding, declaration );

        if ( options.TrimTrailingWhitespace == true )
            text = TrimLines( text, options.NewLine );

        text = ApplyFinalNewline( text, options, source, encoding );

        return [ .. encoding.GetPreamble(), .. encoding.GetBytes( text ) ];
    }


    /// <summary>
    /// The XML declaration of the source document, if it had one. Whether to emit a
    /// declaration has to be decided before the writer is created, so the document is
    /// probed for its first node up front.
    /// </summary>
    private readonly record struct Declaration( bool Present, string? Encoding, bool? Standalone );


    /// <summary />
    private static Declaration ReadDeclaration( byte[] source )
    {
        using var stream = new MemoryStream( source, false );
        using XmlTextReader reader = CreateReader( stream );

        if ( reader.Read() == false || reader.NodeType != XmlNodeType.XmlDeclaration )
            return new Declaration( false, null, null );

        string? standalone = reader.GetAttribute( "standalone" );

        return new Declaration(
            true,
            reader.GetAttribute( "encoding" ),
            standalone == null ? null : standalone == "yes" );
    }


    /// <summary>
    /// XmlTextReader rather than XmlReader.Create: it is the only reader which can
    /// hand back general entity references as nodes instead of silently expanding
    /// them, and expanding them would rewrite the document's content.
    /// </summary>
    private static XmlTextReader CreateReader( Stream stream )
    {
        return new XmlTextReader( stream )
        {
            /*
             * Insignificant whitespace is reported rather than dropped, so that
             * xml:space="preserve" regions -- which the reader surfaces as
             * SignificantWhitespace -- can be passed through untouched while
             * everything else gets re-indented.
             */
            WhitespaceHandling = WhitespaceHandling.All,

            /*
             * Character and predefined entities are expanded, since the writer
             * re-escapes them anyway; entities declared in a DTD are left alone.
             */
            EntityHandling = EntityHandling.ExpandCharEntities,
            Normalization = true,

            /*
             * DOCTYPE is parsed so it can be written back out, but no external
             * resource is ever fetched to do it.
             */
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
        };
    }


    /// <summary />
    private static string Write( byte[] source, XmlFormatOptions options, Encoding encoding, Declaration declaration )
    {
        using var input = new MemoryStream( source, false );
        using XmlTextReader reader = CreateReader( input );

        using var output = new EncodedStringWriter( encoding );

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = options.IndentChars,
            NewLineChars = options.NewLine,
            NewLineHandling = NewLineHandling.Replace,
            Encoding = encoding,
            OmitXmlDeclaration = declaration.Present == false,
            ConformanceLevel = ConformanceLevel.Document,
            CloseOutput = false,
        };

        using ( XmlWriter writer = XmlWriter.Create( output, settings ) )
        {
            bool blank = false;

            while ( reader.Read() )
                WriteNode( reader, writer, declaration, options, ref blank );

            writer.Flush();
        }

        return RestoreBlankLines( output.ToString(), options.NewLine );
    }


    /// <summary>
    /// Turns each marker comment back into the empty line it stood for.
    /// </summary>
    private static string RestoreBlankLines( string text, string newLine )
    {
        if ( text.Contains( BlankMarker ) == false )
            return text;

        string[] lines = text.Split( newLine );

        for ( int i = 0; i < lines.Length; i++ )
        {
            if ( lines[ i ].Trim() == BlankComment )
                lines[ i ] = string.Empty;
            else if ( lines[ i ].Contains( BlankMarker ) == true )
                lines[ i ] = lines[ i ].Replace( BlankComment, string.Empty );
        }

        return string.Join( newLine, lines );
    }


    /// <summary />
    private static void WriteNode( XmlTextReader reader, XmlWriter writer, Declaration declaration, XmlFormatOptions options, ref bool blank )
    {
        /*
         * A blank line separating two nodes is insignificant to XML but deliberate to
         * whoever wrote the file, so it is carried over: the run of whitespace which
         * held it was dropped, and one empty line is put back in its place.
         */
        if ( blank == true && reader.NodeType != XmlNodeType.Whitespace )
        {
            blank = false;

            switch ( reader.NodeType )
            {
                case XmlNodeType.Element:
                case XmlNodeType.EndElement:
                case XmlNodeType.Comment:
                case XmlNodeType.ProcessingInstruction:
                    /*
                     * Written as a comment, not as a raw newline: the writer puts a
                     * comment on its own correctly indented line, whereas raw content
                     * makes it treat the element as mixed and stop indenting it
                     * altogether. The line is emptied again once writing is done.
                     */
                    writer.WriteComment( BlankMarker );
                    break;
            }
        }

        switch ( reader.NodeType )
        {
            case XmlNodeType.XmlDeclaration:
                if ( declaration.Standalone.HasValue == true )
                    writer.WriteStartDocument( declaration.Standalone.Value );
                else
                    writer.WriteStartDocument();
                break;

            case XmlNodeType.DocumentType:
                writer.WriteDocType(
                    reader.Name,
                    reader.GetAttribute( "PUBLIC" ),
                    reader.GetAttribute( "SYSTEM" ),
                    reader.Value );
                break;

            case XmlNodeType.Element:
                writer.WriteStartElement( reader.Prefix, reader.LocalName, reader.NamespaceURI );
                writer.WriteAttributes( reader, true );

                /*
                 * Inside xml:space="preserve" the writer must not lay anything out of
                 * its own accord. It only stops indenting a level once that level has
                 * had content written to it, and a region which opens straight onto a
                 * child element has had none yet -- so give it an empty string.
                 */
                if ( reader.XmlSpace == XmlSpace.Preserve && reader.IsEmptyElement == false )
                    writer.WriteString( string.Empty );

                /*
                 * WriteEndElement on an element with no content emits the self-closing
                 * form, which is what the source used; paired empty elements reach
                 * EndElement below and stay paired.
                 */
                if ( reader.IsEmptyElement == true )
                    writer.WriteEndElement();
                break;

            case XmlNodeType.EndElement:
                writer.WriteFullEndElement();
                break;

            case XmlNodeType.Text:
                writer.WriteString( reader.Value );
                break;

            case XmlNodeType.CDATA:
                writer.WriteCData( reader.Value );
                break;

            case XmlNodeType.SignificantWhitespace:
                /*
                 * Inside xml:space="preserve". Writing it also tells the writer this
                 * level holds mixed content, which suppresses its own indentation --
                 * so the region comes out exactly as it went in.
                 */
                writer.WriteWhitespace( reader.Value );
                break;

            case XmlNodeType.Whitespace:
                /*
                 * Insignificant: dropped, and replaced by the writer's indentation --
                 * but note whether the author had left an empty line here.
                 */
                blank = CountNewlines( reader.Value ) > 1;
                break;

            case XmlNodeType.Comment:
                writer.WriteComment( reader.Value );
                break;

            case XmlNodeType.ProcessingInstruction:
                writer.WriteProcessingInstruction( reader.Name, reader.Value );
                break;

            case XmlNodeType.EntityReference:
                writer.WriteEntityRef( reader.Name );
                break;
        }
    }


    /// <summary />
    private static int CountNewlines( string value )
    {
        int count = 0;

        for ( int i = 0; i < value.Length; i++ )
        {
            if ( value[ i ] == '\n' )
                count++;
            else if ( value[ i ] == '\r' && ( i + 1 == value.Length || value[ i + 1 ] != '\n' ) )
                count++;
        }

        return count;
    }


    /// <summary />
    private static string TrimLines( string text, string newLine )
    {
        string[] lines = text.Split( newLine );

        for ( int i = 0; i < lines.Length; i++ )
            lines[ i ] = lines[ i ].TrimEnd( ' ', '\t' );

        return string.Join( newLine, lines );
    }


    /// <summary />
    private static string ApplyFinalNewline( string text, XmlFormatOptions options, byte[] source, Encoding encoding )
    {
        /*
         * When insert_final_newline is not declared, keep whatever the source did.
         */
        bool wanted;

        if ( options.InsertFinalNewline.HasValue == true )
            wanted = options.InsertFinalNewline.Value;
        else
            wanted = EndsWithNewline( source, encoding );

        string trimmed = text.TrimEnd( '\r', '\n' );

        return wanted == true ? trimmed + options.NewLine : trimmed;
    }


    /// <summary />
    private static bool EndsWithNewline( byte[] source, Encoding encoding )
    {
        if ( source.Length == 0 )
            return false;

        /*
         * Decoding the tail is enough, and avoids decoding the whole document a
         * second time just to look at its last character.
         */
        int take = Math.Min( source.Length, 8 );
        string tail = encoding.GetString( source, source.Length - take, take );

        return tail.EndsWith( '\n' ) || tail.EndsWith( '\r' );
    }


    /// <summary />
    private static Encoding DetectEncoding( byte[] source, string? declared )
    {
        /*
         * A byte order mark is authoritative.
         */
        if ( source.Length >= 3 && source[ 0 ] == 0xEF && source[ 1 ] == 0xBB && source[ 2 ] == 0xBF )
            return new UTF8Encoding( true );

        if ( source.Length >= 2 && source[ 0 ] == 0xFF && source[ 1 ] == 0xFE )
            return new UnicodeEncoding( false, true );

        if ( source.Length >= 2 && source[ 0 ] == 0xFE && source[ 1 ] == 0xFF )
            return new UnicodeEncoding( true, true );

        /*
         * Otherwise trust the declaration, falling back on the XML default when it
         * names an encoding this runtime does not carry.
         */
        if ( string.IsNullOrEmpty( declared ) == false )
        {
            try
            {
                Encoding encoding = Encoding.GetEncoding( declared );

                if ( encoding is UTF8Encoding )
                    return new UTF8Encoding( false );

                return encoding;
            }
            catch ( ArgumentException )
            {
            }
        }

        return new UTF8Encoding( false );
    }


    /// <summary>
    /// XmlWriter takes the encoding it announces in the declaration from the
    /// TextWriter it writes to, and StringWriter always reports UTF-16.
    /// </summary>
    private sealed class EncodedStringWriter : StringWriter
    {
        private readonly Encoding _encoding;

        /// <summary />
        public EncodedStringWriter( Encoding encoding )
        {
            _encoding = encoding;
        }

        /// <summary />
        public override Encoding Encoding => _encoding;
    }
}
