namespace Lefty.Xml.Tests;

/// <summary />
public class UnifiedDiffTests
{
    /// <summary />
    [Fact]
    public void Returns_nothing_when_texts_match()
    {
        Assert.Empty( Diff( "a\nb\n", "a\nb\n" ) );
    }


    /// <summary />
    [Fact]
    public void Shows_change_with_three_lines_of_context()
    {
        string before = "1\n2\n3\n4\n5\n6\n7\n8\n9\n";
        string after = "1\n2\n3\n4\nfive\n6\n7\n8\n9\n";

        string[] expected =
        [
            "--- a/x",
            "+++ b/x",
            "@@ -2,7 +2,7 @@",
            " 2",
            " 3",
            " 4",
            "-5",
            "+five",
            " 6",
            " 7",
            " 8",
        ];

        Assert.Equal( expected, Diff( before, after ) );
    }


    /// <summary />
    [Fact]
    public void Splits_distant_changes_into_separate_hunks()
    {
        string before = Numbered( 20 );
        string after = Numbered( 20, 2, 19 );

        string[] hunks = [ .. Diff( before, after ).Where( line => line.StartsWith( "@@" ) ) ];

        Assert.Equal( [ "@@ -1,5 +1,5 @@", "@@ -16,5 +16,5 @@" ], hunks );
    }


    /// <summary />
    [Fact]
    public void Joins_changes_whose_context_would_touch()
    {
        string before = Numbered( 20 );
        string after = Numbered( 20, 5, 12 );

        string[] hunks = [ .. Diff( before, after ).Where( line => line.StartsWith( "@@" ) ) ];

        Assert.Equal( [ "@@ -2,14 +2,14 @@" ], hunks );
    }


    /// <summary />
    [Fact]
    public void Numbers_an_empty_side_by_the_line_it_follows()
    {
        string[] expected =
        [
            "--- a/x",
            "+++ b/x",
            "@@ -0,0 +1,2 @@",
            "+a",
            "+b",
        ];

        Assert.Equal( expected, Diff( "", "a\nb\n" ) );
    }


    /// <summary />
    [Fact]
    public void Marks_a_missing_final_newline()
    {
        string[] expected =
        [
            "--- a/x",
            "+++ b/x",
            "@@ -1,2 +1,2 @@",
            " a",
            "-b",
            "\\ No newline at end of file",
            "+b",
        ];

        Assert.Equal( expected, Diff( "a\nb", "a\nb\n" ) );
    }


    /// <summary />
    [Fact]
    public void Reports_a_changed_line_ending_as_a_change()
    {
        string[] expected =
        [
            "--- a/x",
            "+++ b/x",
            "@@ -1,2 +1,2 @@",
            "-a\r",
            "+a",
            " b",
        ];

        Assert.Equal( expected, Diff( "a\r\nb\n", "a\nb\n" ) );
    }


    /// <summary>
    /// Quoted lines keep their own ending, so the diff applies to the file as it
    /// is; the diff's own lines always end in LF.
    /// </summary>
    [Fact]
    public void Keeps_line_endings_of_quoted_lines()
    {
        string[] expected =
        [
            "--- a/x\n",
            "+++ b/x\n",
            "@@ -1,3 +1,3 @@\n",
            " a\r\n",
            "-b\r\n",
            "+B\r\n",
            " c\n",
            "\\ No newline at end of file\n",
        ];

        Assert.Equal( expected, UnifiedDiff.Create( "a\r\nb\r\nc", "a\r\nB\r\nc", "a/x", "b/x" ) );
    }


    /// <summary>
    /// Applying the diff to the before text must give the after text, and the diff
    /// must change no more lines than a longest common subsequence leaves over.
    /// </summary>
    [Theory]
    [InlineData( 1 )]
    [InlineData( 2 )]
    [InlineData( 3 )]
    public void Produces_minimal_diffs_which_apply( int seed )
    {
        var random = new Random( seed );

        for ( int run = 0; run < 500; run++ )
        {
            string[] a = RandomLines( random );
            string[] b = RandomLines( random );

            string before = string.Concat( a.Select( line => line + "\n" ) );
            string after = string.Concat( b.Select( line => line + "\n" ) );

            IReadOnlyList<string> diff = Diff( before, after );

            Assert.Equal( b, Apply( a, diff ) );

            int common = LongestCommonSubsequence( a, b );

            Assert.Equal( a.Length - common, diff.Skip( 2 ).Count( line => line.StartsWith( '-' ) ) );
            Assert.Equal( b.Length - common, diff.Skip( 2 ).Count( line => line.StartsWith( '+' ) ) );
        }
    }


    /// <summary>
    /// The diff with the LF which ends each line taken off -- but not a CR before it,
    /// which belongs to the quoted line.
    /// </summary>
    private static string[] Diff( string before, string after )
    {
        return [ .. UnifiedDiff.Create( before, after, "a/x", "b/x" ).Select( line => line[ ..^1 ] ) ];
    }


    /// <summary>
    /// Lines "1" to <paramref name="count" />, with those in <paramref name="changed" />
    /// replaced.
    /// </summary>
    private static string Numbered( int count, params int[] changed )
    {
        return string.Concat( Enumerable.Range( 1, count ).Select( i => ( changed.Contains( i ) ? "changed " + i : i.ToString() ) + "\n" ) );
    }


    /// <summary>
    /// Few distinct lines, so that sequences share a lot and the search has to
    /// choose between many equally plausible matches.
    /// </summary>
    private static string[] RandomLines( Random random )
    {
        string[] alphabet = [ "a", "b", "c", "d" ];

        return [ .. Enumerable.Range( 0, random.Next( 0, 30 ) ).Select( _ => alphabet[ random.Next( alphabet.Length ) ] ) ];
    }


    /// <summary>
    /// Applies the diff the way patch does, trusting the hunk headers for where
    /// each hunk goes.
    /// </summary>
    private static string[] Apply( string[] before, IReadOnlyList<string> diff )
    {
        var result = new List<string>();
        int position = 0;

        foreach ( string line in diff.Skip( 2 ) )
        {
            if ( line.StartsWith( "@@" ) == true )
            {
                string range = line.Split( ' ' )[ 1 ][ 1.. ];
                string[] parts = range.Split( ',' );

                int start = int.Parse( parts[ 0 ] );
                int count = parts.Length > 1 ? int.Parse( parts[ 1 ] ) : 1;
                int index = count == 0 ? start : start - 1;

                while ( position < index )
                    result.Add( before[ position++ ] );

                continue;
            }

            switch ( line[ 0 ] )
            {
                case ' ':
                    Assert.Equal( before[ position ], line[ 1.. ] );
                    result.Add( before[ position++ ] );
                    break;

                case '-':
                    Assert.Equal( before[ position ], line[ 1.. ] );
                    position++;
                    break;

                case '+':
                    result.Add( line[ 1.. ] );
                    break;
            }
        }

        while ( position < before.Length )
            result.Add( before[ position++ ] );

        return [ .. result ];
    }


    /// <summary />
    private static int LongestCommonSubsequence( string[] a, string[] b )
    {
        int[,] table = new int[ a.Length + 1, b.Length + 1 ];

        for ( int i = 1; i <= a.Length; i++ )
        {
            for ( int j = 1; j <= b.Length; j++ )
            {
                if ( a[ i - 1 ] == b[ j - 1 ] )
                    table[ i, j ] = table[ i - 1, j - 1 ] + 1;
                else
                    table[ i, j ] = Math.Max( table[ i - 1, j ], table[ i, j - 1 ] );
            }
        }

        return table[ a.Length, b.Length ];
    }
}