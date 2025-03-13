using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SMSChallenge.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class MessageController: ControllerBase {
    private const int Phone_Limit = 5;
    private const int Account_Limit = 10;
    private const int Cleanup_Interval = 60000;

    private static readonly HttpClient httpClient = new();
    private static readonly ConcurrentQueue<Message> messageQueue = new();
    public static readonly Dictionary<int, int> phoneCounts = [];
    public static readonly Dictionary<int, int> accountCounts = [];
    public static readonly Dictionary<int, DateTime> phoneLastUpdated = [];
    public static readonly Dictionary<int, DateTime> accountLastUpdated = [];
    public static readonly Dictionary<int, AccountStats> accountStats = [];

    static MessageController() {
        Timer processTimer = new(ProcessQueue, null, 0, 1000);
        Timer cleanupTimer = new(CleanupOldEntries, null, Cleanup_Interval, Cleanup_Interval);
    }

    [HttpPost("send")]
    public MessageResponse SendMessage([FromBody] Message[] requests) {
        int queuedMessages = 0;
        bool isExceeded = false;

        lock (phoneCounts) {
            foreach (Message request in requests) {
                System.Diagnostics.Debug.WriteLine("Acct number: " + request.account_number);
                System.Diagnostics.Debug.WriteLine("Phone number: " + request.phone_number);

                if (!phoneCounts.ContainsKey(request.phone_number))
                    phoneCounts[request.phone_number] = 0;
                
                if (!accountCounts.ContainsKey(request.account_number))
                    accountCounts[request.account_number] = 0;

                if (phoneCounts[request.phone_number] < Phone_Limit && accountCounts[request.account_number] < Account_Limit) {
                    messageQueue.Enqueue(request);
                    phoneCounts[request.phone_number]++;
                    accountCounts[request.account_number]++;
                    System.Diagnostics.Debug.WriteLine($"Successfully queued message from account number {request.account_number} and phone number {request.phone_number}.");
                    queuedMessages++;
                } else {
                    isExceeded = true;
                    if (phoneCounts[request.phone_number] >= Phone_Limit)
                        System.Diagnostics.Debug.WriteLine($"Account limit reached for account number {request.account_number}.");
                    if (accountCounts[request.account_number] >= Account_Limit)
                        System.Diagnostics.Debug.WriteLine($"Phone limit reached for phone number {request.phone_number}.");
                }
            }
        }

        return new MessageResponse {
            message = isExceeded
                ? $"{queuedMessages} messages queued successfully; however, {requests.Length - queuedMessages} exceeded the limit."
                : $"{queuedMessages} messages queued successfully."
        };
    }

    public static async void ProcessQueue(object state) {
        List<Message> messages = [];
        while (messageQueue.TryDequeue(out var message))
            messages.Add(message);

        if (messages.Count > 0) {
            StringContent content = new(JsonSerializer.Serialize(messages), System.Text.Encoding.UTF8, "application/json");

            // This would be the endpoint we'd be sending the messages to
            //HttpResponseMessage response = await httpClient.PostAsync("https://someplace/api/something", content);

            lock (accountStats) {
                foreach (Message msg in messages) {
                    if (!accountStats.ContainsKey(msg.account_number)) {
                        accountStats[msg.account_number] = new AccountStats {
                            account_number = msg.account_number,
                            phone_stats = [],
                            success_count = 0,
                            failure_count = 0,
                            last_updated = DateTime.UtcNow
                        };
                    }

                    AccountStats stats = accountStats[msg.account_number];
                    stats.last_updated = DateTime.UtcNow;
                    if (!stats.phone_stats.ContainsKey(msg.phone_number))
                        stats.phone_stats.Add(msg.phone_number, new PhoneStats {
                            phone_number = msg.phone_number,
                            success_count = 0,
                            failure_count = 0,
                            last_updated = DateTime.UtcNow,
                        });

                    //if (response.IsSuccessStatusCode) {
                    if (true) {
                        stats.success_count++;
                        stats.phone_stats[msg.phone_number].success_count++;
                        System.Diagnostics.Debug.WriteLine($"Sent message from account number {msg.account_number} and phone number {msg.phone_number}.");
                    } else {
                        stats.failure_count++;
                        stats.phone_stats[msg.phone_number].failure_count++;
                        System.Diagnostics.Debug.WriteLine($"Failed to send message from account number {msg.account_number} and phone number {msg.phone_number}.");
                    }
                }
            }

            lock (phoneCounts) {
                foreach (var msg in messages) {
                    phoneCounts[msg.phone_number]--;
                    accountCounts[msg.account_number]--;
                }
            }
        }
    }

    public static void CleanupOldEntries(object state) {
        DateTime cutoffTime = DateTime.UtcNow.AddMilliseconds(-1 * Cleanup_Interval);

        System.Diagnostics.Debug.WriteLine($"{cutoffTime}, {DateTime.UtcNow}");
        lock (phoneCounts) {
            List<int> oldAccountNumbers = accountStats.Where(stat => stat.Value.last_updated < cutoffTime).Select(stat => stat.Key).ToList();
            foreach (int accountNumber in oldAccountNumbers) {
                accountCounts.Remove(accountNumber);
                accountLastUpdated.Remove(accountNumber);

                foreach (int phoneNumber in accountStats[accountNumber].phone_stats.Keys) {
                    phoneCounts.Remove(phoneNumber);
                    phoneLastUpdated.Remove(phoneNumber);
                }

                accountStats.Remove(accountNumber);
            }

            foreach (AccountStats acct in accountStats.Values) {
                List<int> oldPhoneNumbers = acct.phone_stats.Where(phoneStat => phoneStat.Value.last_updated < cutoffTime).Select(phoneStat => phoneStat.Key).ToList();
                foreach (int phoneNumber in oldPhoneNumbers) {
                    phoneCounts.Remove(phoneNumber);
                    phoneLastUpdated.Remove(phoneNumber);
                    accountStats[acct.account_number].phone_stats.Remove(phoneNumber);
                }
            }
        }
    }

    // I would refactor either GetStats or this to use commonalities so as to not call each one every second (although in a large system both would likely be implemented and with different intervals)
    [HttpGet("Heartbeat")]
    public Heartbeat GetHeartbeat() {
        return new Heartbeat {
            msg_count = messageQueue.Count // Should show how many messages are being processed per second as that function does a batch call with those queued messages every second
        };
    }

    [HttpGet("stats")]
    public IEnumerable<AccountStats> GetStats([FromQuery] int? phoneNumber, [FromQuery] int? accountNumber, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate) {
        lock (accountStats) {
            System.Diagnostics.Debug.WriteLine($"{phoneNumber}, {accountNumber}, {startDate}, {endDate}. Current account count: {accountCounts.Count}");
            return accountStats.Values.Where(stats =>
                (!phoneNumber.HasValue || (stats.phone_stats.TryGetValue(phoneNumber.Value, out var phoneStats) && phoneStats != null)) &&
                (!accountNumber.HasValue || stats.account_number == accountNumber.Value) &&
                (!startDate.HasValue || stats.last_updated >= startDate.Value) &&
                (!endDate.HasValue || stats.last_updated <= endDate.Value)
            ).ToList();
        }
    }
}
