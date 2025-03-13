using SMSChallenge.Server.Controllers;
using SMSChallenge.Server;

namespace SMSChallengeTests {
    public class MessageTests { // With a larger project and more tests, there'd be an actual folder structure. As it is, we're doing a few simple tests.
        private readonly MessageController _controller;
        public MessageTests() {
            _controller = new MessageController();
        }

        [Fact]
        public void ExceedingLimitTest() {
            List<Message> messages = [];
            for (int i = 0; i < 8; i++)
                messages.Add(new Message { account_number = 999, phone_number = 888, message = $"Test message {i + 1}" });

            MessageResponse result = _controller.SendMessage(messages.ToArray());

            Assert.IsType<MessageResponse>(result);
            Assert.Equal("5 messages queued successfully; however, 3 exceeded the limit.", result.message);
        }

        [Fact]
        public void QueueManyMessagesTest() {
            List<Message> messages = [];
            for (int i = 0; i < 10; i++)
                messages.Add(new Message { account_number = 100 + i, phone_number = 200 + i, message = $"Test message {i + 1000}" });

            MessageResponse result = _controller.SendMessage(messages.ToArray());

            Assert.IsType<MessageResponse>(result);
            Assert.Equal("10 messages queued successfully.", result.message);
        }

        [Fact]
        public void FilteredStatsTest() {
            List<AccountStats> stats = [
                new AccountStats { account_number = 10, phone_stats = new Dictionary<int, PhoneStats> { { 1230, new PhoneStats { phone_number = 1230, success_count = 10, failure_count = 0, last_updated = DateTime.UtcNow } } }, success_count = 10, failure_count = 0, last_updated = DateTime.UtcNow },
                new AccountStats { account_number = 20, phone_stats = new Dictionary<int, PhoneStats> { { 4560, new PhoneStats { phone_number = 4560, success_count = 0, failure_count = 10, last_updated = DateTime.UtcNow } } }, success_count = 0, failure_count = 10, last_updated = DateTime.UtcNow }
            ];

            foreach (AccountStats stat in stats)
                MessageController.accountStats.Add(stat.account_number, stat);

            IEnumerable<AccountStats> result = _controller.GetStats(1230, null, null, null);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(1230, result.First().phone_stats.First().Key);
        }

        [Fact]
        public void CleanupEntriesTest() {
            DateTime oldTime = DateTime.UtcNow.AddMinutes(-2);
            DateTime recentTime = DateTime.UtcNow;

            MessageController.phoneLastUpdated[123] = oldTime;
            MessageController.accountLastUpdated[1] = oldTime;
            MessageController.phoneLastUpdated[456] = recentTime;
            MessageController.accountLastUpdated[2] = recentTime;

            MessageController.phoneCounts[123] = 1;
            MessageController.accountCounts[1] = 1;
            MessageController.phoneCounts[456] = 1;
            MessageController.accountCounts[2] = 1;

            AccountStats oldStats = new() { account_number = 1, phone_stats = new Dictionary<int, PhoneStats> { { 123, new PhoneStats { phone_number = 123, success_count = 1, failure_count = 0, last_updated = DateTime.UtcNow } } }, success_count = 1, failure_count = 0, last_updated = oldTime };
            AccountStats recentStats = new() { account_number = 2, phone_stats = new Dictionary<int, PhoneStats> { { 456, new PhoneStats { phone_number = 456, success_count = 0, failure_count = 1, last_updated = DateTime.UtcNow } } }, success_count = 0, failure_count = 1, last_updated = recentTime };

            MessageController.accountStats.Add(oldStats.account_number, oldStats);
            MessageController.accountStats.Add(recentStats.account_number, recentStats);

            MessageController.CleanupOldEntries(null);

            Assert.False(MessageController.accountStats.ContainsKey(oldStats.account_number));
            Assert.True(MessageController.accountStats.ContainsKey(recentStats.account_number));
            Assert.False(MessageController.phoneCounts.ContainsKey(123));
            Assert.False(MessageController.accountCounts.ContainsKey(1));
            Assert.True(MessageController.phoneCounts.ContainsKey(456));
            Assert.True(MessageController.accountCounts.ContainsKey(2));
        }
    }
}

