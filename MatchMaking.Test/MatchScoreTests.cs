using MatchMaking.Common;
using MatchMaking.Match;

namespace MatchMaking.Tests;

public class MatchScoreTests
{
    [Fact]
    public void EncodeScore_ShouldReturnCorrectEncodedValue()
    {
        // Arrange
        int mmr = 1500;
        long expectedTimestamp = TimeHelper.GetUnixTimestamp();
        long expectedEncodedScore = expectedTimestamp * 10000 + mmr;

        // Act
        long encodedScore = MatchScore.EncodeScore(mmr);

        // Assert
        Assert.Equal(expectedEncodedScore, encodedScore);
    }

    [Fact]
    public void DecodeScore_ShouldReturnCorrectMMRAndWaitTime()
    {
        // Arrange
        int mmr = 1500;
        long timestamp = TimeHelper.GetUnixTimestamp();
        long encodedScore = timestamp * 10000 + mmr;

        // Act
        var (decodedMMR, waitTime) = MatchScore.DecodeScore(encodedScore);

        // Assert
        Assert.Equal(mmr, decodedMMR);
        Assert.InRange(waitTime, 0, 1); // waitTime should be very small since we just encoded it
    }

    [Fact]
    public void DecodeScore_ShouldReturnZeroMMR_WhenMMROutOfRange()
    {
        // Arrange
        int invalidMMR = 10000; // MMR out of valid range
        long timestamp = TimeHelper.GetUnixTimestamp();
        long encodedScore = timestamp * 10000 + invalidMMR;

        // Act
        var (decodedMMR, waitTime) = MatchScore.DecodeScore(encodedScore);

        // Assert
        Assert.Equal(0, decodedMMR);
        Assert.Equal(0, waitTime);
    }
}
