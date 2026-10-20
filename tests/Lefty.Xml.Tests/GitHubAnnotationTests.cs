namespace Lefty.Xml.Tests;

/// <summary />
public class GitHubAnnotationTests
{
    /// <summary />
    [Fact]
    public void Includes_position_when_known()
    {
        string actual = GitHubAnnotation.Error( "lxml validate", "docs/a.xml", 3, 7, "Bad value." );

        Assert.Equal( "::error file=docs/a.xml,line=3,col=7,title=lxml validate::Bad value.", actual );
    }


    /// <summary />
    [Fact]
    public void Leaves_out_unknown_position_and_file()
    {
        Assert.Equal( "::error file=a.xml,title=t::m", GitHubAnnotation.Error( "t", "a.xml", 0, 0, "m" ) );
        Assert.Equal( "::error title=t::m", GitHubAnnotation.Error( "t", null, 0, 0, "m" ) );
    }


    /// <summary />
    [Fact]
    public void Uses_forward_slashes_in_file()
    {
        Assert.Equal( "::error file=docs/sub/a.xml,title=t::m", GitHubAnnotation.Error( "t", "docs\\sub\\a.xml", 0, 0, "m" ) );
    }


    /// <summary />
    [Fact]
    public void Escapes_message_and_properties()
    {
        string actual = GitHubAnnotation.Error( "a:b,c", "x,y.xml", 0, 0, "100% wrong\r\nreally: yes, wrong" );

        Assert.Equal( "::error file=x%2Cy.xml,title=a%3Ab%2Cc::100%25 wrong%0D%0Areally: yes, wrong", actual );
    }
}