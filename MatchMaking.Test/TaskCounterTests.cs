using MatchMaking.Common;
using Constant = MatchMaking.Common.Constant;

namespace MatchMaking.Tests;

public enum TestTaskType
{
    Type1,
    Type2
}

public class TaskCounterTests
{
    private object _param = new();

    [Fact]
    public async Task IncreaseTask_ShouldIncreaseTaskCount()
    {
        // Arrange
        var taskCounter = new TaskCounter<TestTaskType>();
        Func<object, CancellationToken, Task> taskAction = async (param, token) => await Task.Delay(100, token);

        // Act
        await taskCounter.IncreaseTask(TestTaskType.Type1, taskAction, _param);

        // Assert
        Assert.Equal(1, taskCounter.GetTaskCount(TestTaskType.Type1));
    }

    [Fact]
    public async Task DecreaseTask_ShouldDecreaseTaskCount()
    {
        // Arrange
        var taskCounter = new TaskCounter<TestTaskType>();
        Func<object, CancellationToken, Task> taskAction = async (param, token) => await Task.Delay(100, token);

        // Act
        await taskCounter.IncreaseTask(TestTaskType.Type1, taskAction, _param);
        await Task.Delay(2000); // 2초 대기
        await taskCounter.IncreaseTask(TestTaskType.Type1, taskAction, _param);
        await Task.Delay(2000); // 2초 대기
        await taskCounter.DecreaseTask(TestTaskType.Type1);

        // Assert
        Assert.Equal(1, taskCounter.GetTaskCount(TestTaskType.Type1));
    }

    [Fact]
    public async Task GetTaskCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var taskCounter = new TaskCounter<TestTaskType>();
        Func<object, CancellationToken, Task> taskAction = async (param, token) => await Task.Delay(100, token);

        // Act
        await taskCounter.IncreaseTask(TestTaskType.Type1, taskAction, _param);
        await Task.Delay(2000); // 2초 대기
        await taskCounter.IncreaseTask(TestTaskType.Type1, taskAction, _param);

        // Assert
        Assert.Equal(2, taskCounter.GetTaskCount(TestTaskType.Type1));
    }

    [Fact]
    public async Task IncreaseTask_ShouldNotIncreaseTaskCount_WhenMaxTaskCountReached()
    {
        // Arrange
        var taskCounter = new TaskCounter<TestTaskType>();
        Func<object, CancellationToken, Task> taskAction = async (param, token) => await Task.Delay(100, token);

        // Act
        for (int i = 0; i < Constant.MaxTaskCount + 1; i++)
        {
            await taskCounter.IncreaseTask(TestTaskType.Type1, taskAction, _param);
            await Task.Delay(2000); // 2초 대기
        }

        // Assert
        Assert.Equal(Constant.MaxTaskCount, taskCounter.GetTaskCount(TestTaskType.Type1));
    }

    [Fact]
    public async Task IncreaseTask_ShouldNotIncreaseTaskCount_WhenInCooldownPeriod()
    {
        // Arrange
        var taskCounter = new TaskCounter<TestTaskType>();
        Func<object, CancellationToken, Task> taskAction = async (param, token) => await Task.Delay(100, token);

        // Act
        await taskCounter.IncreaseTask(TestTaskType.Type1, taskAction, _param);
        await Task.Delay(1000); // 1초 대기 (쿨다운 기간 내)
        await taskCounter.IncreaseTask(TestTaskType.Type1, taskAction, _param);

        // Assert
        Assert.Equal(1, taskCounter.GetTaskCount(TestTaskType.Type1));
    }
}
