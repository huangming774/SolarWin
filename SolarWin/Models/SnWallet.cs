using System.Text.Json.Serialization;

namespace SolarWin.Models;

public sealed class SnWallet
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("pockets")]
    public List<SnWalletPocket> Pockets { get; set; } = [];

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }

    [JsonPropertyName("realm_id")]
    public Guid? RealmId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("is_primary")]
    public bool IsPrimary { get; set; }

    [JsonPropertyName("public_id")]
    public string? PublicId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class SnWalletPocket
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("held_amount")]
    public decimal HeldAmount { get; set; }

    [JsonPropertyName("wallet_id")]
    public Guid WalletId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonIgnore]
    public decimal AvailableAmount => Amount - HeldAmount;
}

public sealed class SnWalletTransaction
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("is_frozen")]
    public bool IsFrozen { get; set; }

    [JsonPropertyName("require_confirmation")]
    public bool RequireConfirmation { get; set; }

    [JsonPropertyName("frozen_at")]
    public DateTimeOffset? FrozenAt { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("confirmed_at")]
    public DateTimeOffset? ConfirmedAt { get; set; }

    [JsonPropertyName("payer_wallet_id")]
    public Guid? PayerWalletId { get; set; }

    [JsonPropertyName("payer_wallet")]
    public SnWallet? PayerWallet { get; set; }

    [JsonPropertyName("payee_wallet_id")]
    public Guid? PayeeWalletId { get; set; }

    [JsonPropertyName("payee_wallet")]
    public SnWallet? PayeeWallet { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class SnWalletStats
{
    [JsonPropertyName("period_begin")]
    public DateTimeOffset? PeriodBegin { get; set; }

    [JsonPropertyName("period_end")]
    public DateTimeOffset? PeriodEnd { get; set; }

    [JsonPropertyName("total_transactions")]
    public int TotalTransactions { get; set; }

    [JsonPropertyName("total_orders")]
    public int TotalOrders { get; set; }

    [JsonPropertyName("total_income")]
    public decimal TotalIncome { get; set; }

    [JsonPropertyName("total_outgoing")]
    public decimal TotalOutgoing { get; set; }

    [JsonPropertyName("sum")]
    public decimal Sum { get; set; }

    [JsonPropertyName("income_categories")]
    public Dictionary<string, decimal> IncomeCategories { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("outgoing_categories")]
    public Dictionary<string, decimal> OutgoingCategories { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}