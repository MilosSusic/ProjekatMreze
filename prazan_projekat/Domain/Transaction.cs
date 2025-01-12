using System;

namespace Domain
{
    [Serializable]
    public class Transaction
    {
        public string Id { get; private set; }
        public string Username { get; set; }
        public decimal Amount { get; set; }
        public TransactionType Type { get; set; }
        public DateTime Timestamp { get; private set; }

        public Transaction(string username, decimal amount, TransactionType type)
        {
            Username = username;
            Amount = amount;
            Type = type;
        }
    }

    [Serializable]
    public enum TransactionType
    {
        Deposit,
        Withdrawal
    }
}
