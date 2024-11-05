//using MatchMaking.Common;

//public class QueueItem : IQueueItem
//{
//    public int Id { get; set; }
//    public int Score { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//}

//[TestFixture]
//public class LookupQueueTests
//{
//    [Test]
//    public void Del_ShouldRemoveSpecifiedItems_And_Pop_ShouldReturnRemainingItems()
//    {
//        // Arrange
//        var queue = new LookupQueue<QueueItem>();
//        var items = new List<QueueItem>
//        {
//            new QueueItem { Id = 1 },
//            new QueueItem { Id = 2 },
//            new QueueItem { Id = 3 },
//            new QueueItem { Id = 4 },
//            new QueueItem { Id = 5 },
//            new QueueItem { Id = 6 }
//        };

//        foreach (var item in items)
//        {
//            queue.Push(item);
//        }

//        // Act
//        queue.Del(2);
//        queue.Del(4);
//        queue.Del(6);

//        // Assert
//        //Assert.That(queue.Count, Is.EqualTo(3));

//        // Pop and check remaining items
//        Assert.IsTrue(queue.Pop(out var item1));
//        Assert.That(item1.Id, Is.EqualTo(1));

//        Assert.IsFalse(queue.Pop(out var item2));
//        Assert.IsTrue(queue.Pop(out var item3));
//        Assert.That(item3.Id, Is.EqualTo(3));

//        Assert.IsFalse(queue.Pop(out var item4));
//        Assert.IsTrue(queue.Pop(out var item5));
//        Assert.That(item5.Id, Is.EqualTo(5));

//        // Ensure queue is empty
//        Assert.IsFalse(queue.Pop(out var item6));
//        Assert.IsFalse(queue.Pop(out var _));
//    }
//}

