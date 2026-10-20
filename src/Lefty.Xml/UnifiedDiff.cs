namespace Lefty.Xml;

/// <summary>
/// Line-based diff of two texts, in the unified format which diff -u and git
/// produce -- so the output can be read, and applied, with the usual tools.
/// </summary>
public static class UnifiedDiff
{
    /// <summary>
    /// Unchanged lines shown around each change.
    /// </summary>
    private const int Context = 3;


    /// <summary />
    private enum EditKind
    {
        Equal,
        Delete,
        Insert,
    }


    /// <summary>
    /// A single line of the edit script. <c>A</c> and <c>B</c> are positions in the
    /// before and after texts: the line itself where the edit has one on that side,
    /// otherwise the number of lines which precede it there.
    /// </summary>
    private readonly record struct Edit( EditKind Kind, int A, int B );


    /// <summary>
    /// Returns the lines of the unified diff from <paramref name="before" /> to
    /// <paramref name="after" />, each with its line ending; empty when the texts are
    /// the same. Lines compare including their line ending, so a line whose ending
    /// changes is reported as changed.
    /// </summary>
    /// <remarks>
    /// Lines quoted from either text keep the ending they have there -- without it, a
    /// diff of a CRLF file would not apply to it. Every other line ends in LF.
    /// </remarks>
    public static IReadOnlyList<string> Create( string before, string after, string beforeName, string afterName )
    {
        string[] a = SplitLines( before );
        string[] b = SplitLines( after );

        List<Edit> edits = Compare( a, b );

        var changes = new List<int>();

        for ( int i = 0; i < edits.Count; i++ )
        {
            if ( edits[ i ].Kind != EditKind.Equal )
                changes.Add( i );
        }

        if ( changes.Count == 0 )
            return [];

        var lines = new List<string>
        {
            "--- " + beforeName + "\n",
            "+++ " + afterName + "\n",
        };


        /*
         * Changes close enough that their context would touch or overlap share a
         * hunk, as in diff -u.
         */
        int next = 0;

        while ( next < changes.Count )
        {
            int first = changes[ next ];
            int last = first;

            next++;

            while ( next < changes.Count && changes[ next ] - last <= 2 * Context + 1 )
            {
                last = changes[ next ];
                next++;
            }

            int start = Math.Max( 0, first - Context );
            int end = Math.Min( edits.Count, last + Context + 1 );

            WriteHunk( lines, edits, start, end, a, b );
        }

        return lines;
    }


    /// <summary />
    private static void WriteHunk( List<string> lines, List<Edit> edits, int start, int end, string[] a, string[] b )
    {
        int countA = 0;
        int countB = 0;

        for ( int i = start; i < end; i++ )
        {
            if ( edits[ i ].Kind != EditKind.Insert )
                countA++;

            if ( edits[ i ].Kind != EditKind.Delete )
                countB++;
        }

        lines.Add( $"@@ -{Range( edits[ start ].A, countA )} +{Range( edits[ start ].B, countB )} @@\n" );

        for ( int i = start; i < end; i++ )
        {
            Edit edit = edits[ i ];

            string line = edit.Kind switch
            {
                EditKind.Insert => b[ edit.B ],
                _ => a[ edit.A ],
            };

            char prefix = edit.Kind switch
            {
                EditKind.Delete => '-',
                EditKind.Insert => '+',
                _ => ' ',
            };

            if ( line.EndsWith( '\n' ) == true || line.EndsWith( '\r' ) == true )
            {
                lines.Add( prefix + line );
            }
            else
            {
                lines.Add( prefix + line + "\n" );
                lines.Add( "\\ No newline at end of file\n" );
            }
        }
    }


    /// <summary>
    /// Start and length of one side of a hunk. An empty side is numbered by the line
    /// it follows, and a length of one is left implicit -- both as diff -u does.
    /// </summary>
    private static string Range( int start, int count )
    {
        if ( count == 0 )
            return $"{start},0";

        if ( count == 1 )
            return $"{start + 1}";

        return $"{start + 1},{count}";
    }


    /// <summary>
    /// Splits <paramref name="text" /> into lines, each keeping its own line ending;
    /// only the last line can be without one.
    /// </summary>
    private static string[] SplitLines( string text )
    {
        var lines = new List<string>();
        int start = 0;

        for ( int i = 0; i < text.Length; i++ )
        {
            if ( text[ i ] == '\r' && i + 1 < text.Length && text[ i + 1 ] == '\n' )
                i++;
            else if ( text[ i ] != '\n' && text[ i ] != '\r' )
                continue;

            lines.Add( text[ start..( i + 1 ) ] );
            start = i + 1;
        }

        if ( start < text.Length )
            lines.Add( text[ start.. ] );

        return [ .. lines ];
    }


    /// <summary>
    /// Computes the edit script from <paramref name="a" /> to <paramref name="b" />.
    /// </summary>
    private static List<Edit> Compare( string[] a, string[] b )
    {
        /*
         * Lines are swapped for numbers, so the inner loops compare integers rather
         * than strings.
         */
        var ids = new Dictionary<string, int>( StringComparer.Ordinal );

        int[] x = [ .. a.Select( line => Identify( ids, line ) ) ];
        int[] y = [ .. b.Select( line => Identify( ids, line ) ) ];

        var matches = new List<(int A, int B)>();
        Match( x, 0, x.Length, y, 0, y.Length, matches );


        /*
         * Between two matched lines, whatever is left on either side was removed
         * or added.
         */
        var edits = new List<Edit>();
        int i = 0;
        int j = 0;

        foreach ( (int ma, int mb) in matches )
        {
            for ( ; i < ma; i++ )
                edits.Add( new Edit( EditKind.Delete, i, j ) );

            for ( ; j < mb; j++ )
                edits.Add( new Edit( EditKind.Insert, i, j ) );

            edits.Add( new Edit( EditKind.Equal, i, j ) );
            i++;
            j++;
        }

        for ( ; i < a.Length; i++ )
            edits.Add( new Edit( EditKind.Delete, i, j ) );

        for ( ; j < b.Length; j++ )
            edits.Add( new Edit( EditKind.Insert, i, j ) );

        return edits;
    }


    /// <summary />
    private static int Identify( Dictionary<string, int> ids, string line )
    {
        if ( ids.TryGetValue( line, out int id ) == false )
        {
            id = ids.Count;
            ids.Add( line, id );
        }

        return id;
    }


    /// <summary>
    /// Appends, in order, the pairs of lines which <paramref name="a" />[aLo..aHi) and
    /// <paramref name="b" />[bLo..bHi) have in common -- a longest common subsequence.
    /// </summary>
    private static void Match( int[] a, int aLo, int aHi, int[] b, int bLo, int bHi, List<(int A, int B)> matches )
    {
        /*
         * Common head and tail are matched outright: cheap, and usually most of a
         * file.
         */
        while ( aLo < aHi && bLo < bHi && a[ aLo ] == b[ bLo ] )
        {
            matches.Add( (aLo, bLo) );
            aLo++;
            bLo++;
        }

        int tail = 0;

        while ( aLo < aHi - tail && bLo < bHi - tail && a[ aHi - tail - 1 ] == b[ bHi - tail - 1 ] )
            tail++;

        aHi -= tail;
        bHi -= tail;

        if ( aLo < aHi && bLo < bHi )
        {
            /*
             * The split always lies strictly inside the range, but should it ever
             * not, recursing on it would never end: report no common lines instead.
             */
            if ( Bisect( a, aLo, aHi, b, bLo, bHi, out int splitA, out int splitB ) == true
                && ( splitA != aLo || splitB != bLo )
                && ( splitA != aHi || splitB != bHi ) )
            {
                Match( a, aLo, splitA, b, bLo, splitB, matches );
                Match( a, splitA, aHi, b, splitB, bHi, matches );
            }
        }

        for ( int i = 0; i < tail; i++ )
            matches.Add( (aHi + i, bHi + i) );
    }


    /// <summary>
    /// Finds the middle snake of the shortest edit script -- Myers' linear space
    /// refinement, running the search forwards from the start and backwards from
    /// the end until the two meet. Returns false when the ranges have nothing in
    /// common.
    /// </summary>
    private static bool Bisect( int[] a, int aLo, int aHi, int[] b, int bLo, int bHi, out int splitA, out int splitB )
    {
        int n = aHi - aLo;
        int m = bHi - bLo;

        int maxD = ( n + m + 1 ) / 2;
        int offset = maxD;
        int length = 2 * maxD + 2;

        /*
         * Furthest x reached on each diagonal k = x - y, going forwards (v1) and
         * backwards (v2), with x counted from the respective end.
         */
        int[] v1 = new int[ length ];
        int[] v2 = new int[ length ];

        Array.Fill( v1, -1 );
        Array.Fill( v2, -1 );

        v1[ offset + 1 ] = 0;
        v2[ offset + 1 ] = 0;

        int delta = n - m;

        /*
         * With an odd delta the paths can only meet on a forward step, and with an
         * even one only on a backward step.
         */
        bool front = delta % 2 != 0;

        /*
         * Diagonals which have run off the edge of the grid are not explored again.
         */
        int k1Start = 0;
        int k1End = 0;
        int k2Start = 0;
        int k2End = 0;

        for ( int d = 0; d < maxD; d++ )
        {
            for ( int k1 = -d + k1Start; k1 <= d - k1End; k1 += 2 )
            {
                int k1Offset = offset + k1;
                int x1;

                if ( k1 == -d || ( k1 != d && v1[ k1Offset - 1 ] < v1[ k1Offset + 1 ] ) )
                    x1 = v1[ k1Offset + 1 ];
                else
                    x1 = v1[ k1Offset - 1 ] + 1;

                int y1 = x1 - k1;

                while ( x1 < n && y1 < m && a[ aLo + x1 ] == b[ bLo + y1 ] )
                {
                    x1++;
                    y1++;
                }

                v1[ k1Offset ] = x1;

                if ( x1 > n )
                {
                    k1End += 2;
                }
                else if ( y1 > m )
                {
                    k1Start += 2;
                }
                else if ( front == true )
                {
                    int k2Offset = offset + delta - k1;

                    if ( k2Offset >= 0 && k2Offset < length && v2[ k2Offset ] != -1 && x1 >= n - v2[ k2Offset ] )
                    {
                        splitA = aLo + x1;
                        splitB = bLo + y1;
                        return true;
                    }
                }
            }

            for ( int k2 = -d + k2Start; k2 <= d - k2End; k2 += 2 )
            {
                int k2Offset = offset + k2;
                int x2;

                if ( k2 == -d || ( k2 != d && v2[ k2Offset - 1 ] < v2[ k2Offset + 1 ] ) )
                    x2 = v2[ k2Offset + 1 ];
                else
                    x2 = v2[ k2Offset - 1 ] + 1;

                int y2 = x2 - k2;

                while ( x2 < n && y2 < m && a[ aHi - x2 - 1 ] == b[ bHi - y2 - 1 ] )
                {
                    x2++;
                    y2++;
                }

                v2[ k2Offset ] = x2;

                if ( x2 > n )
                {
                    k2End += 2;
                }
                else if ( y2 > m )
                {
                    k2Start += 2;
                }
                else if ( front == false )
                {
                    int k1Offset = offset + delta - k2;

                    if ( k1Offset >= 0 && k1Offset < length && v1[ k1Offset ] != -1 )
                    {
                        int x1 = v1[ k1Offset ];
                        int y1 = offset + x1 - k1Offset;

                        if ( x1 >= n - x2 )
                        {
                            splitA = aLo + x1;
                            splitB = bLo + y1;
                            return true;
                        }
                    }
                }
            }
        }

        splitA = 0;
        splitB = 0;
        return false;
    }
}