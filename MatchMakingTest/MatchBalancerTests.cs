using MatchMaking.Match;

namespace MatchMakingTest;

public class MatchBalancerTests
{
    [Fact]
    public void GetAverageMatchTime_ShouldReturnZero_WhenNoMatchTimesAdded()
    {
        // Arrange
        var balancer = new MatchBalancer();

        // Act
        var averageTime = balancer.GetAverageMatchTime();

        // Assert
        Assert.Equal(0, averageTime);
    }

    [Fact]
    public void GetAverageMatchTime_ShouldReturnCorrectAverage_WhenMatchTimesAdded()
    {
        // Arrange
        var balancer = new MatchBalancer();
        balancer.AddMatchTime(10);
        balancer.AddMatchTime(20);
        balancer.AddMatchTime(30);

        // Act
        var averageTime = balancer.GetAverageMatchTime();

        // Assert
        Assert.Equal(20, averageTime);
    }

    [Fact]
    public void AddMatchTime_ShouldUpdateTotalSecondsAndIndex()
    {
        // Arrange
        var balancer = new MatchBalancer();
        balancer.AddMatchTime(10);

        // Act
        balancer.AddMatchTime(20);

        // Assert
        Assert.Equal(30, balancer.GetAverageMatchTime() * MatchBalancer.WaitTimeMaxCount);
    }

    [Fact]
    public void GetAdjustMMR_ShouldReturnCorrectMMRAdjustment()
    {
        // Arrange
        var balancer = new MatchBalancer();
        balancer.AddMatchTime(10);
        balancer.AddMatchTime(20);
        balancer.AddMatchTime(30);

        // Act
        var adjustMMR = balancer.GetAdjustMMR(25);

        // Assert
        Assert.Equal(400, adjustMMR); // 200 (processWaitWeight) + 200 (waitTimeWeight)
    }



    [Fact]
    public void AddMatchTime_ShouldReset_WhenTimeExceedsWaitTimeReset()
    {
        // Arrange
        var balancer = new MatchBalancer();
        balancer.AddMatchTime(10);

        // Act
        balancer.AddMatchTime(20); // This should trigger the reset

        Thread.Sleep(6000); // 6초 대기

        // Assert
        Assert.Equal(0, balancer.GetAverageMatchTime());
    }
}
