namespace SMSChallenge.Server {
    public class Message { // This would be expanded on with whatever the service would use as message metadata
        public int account_number { get; set; }
        public int phone_number { get; set; }
        public string message { get; set; }
    }

    public class MessageResponse { // Similar to the Message class, you'd certainly want more than just a message string
        public string message { get; set; }
    }

    public class AccountStats {
        public Dictionary<int, PhoneStats> phone_stats { get; set; }
        public DateTime last_updated { get; set; }
        public int account_number { get; set; }
        public int success_count { get; set; }
        public int failure_count { get; set; }
    }

    public class PhoneStats {
        public DateTime last_updated { get; set; }
        public int phone_number { get; set; }
        public int success_count { get; set; }
        public int failure_count { get; set; }
    }
}
