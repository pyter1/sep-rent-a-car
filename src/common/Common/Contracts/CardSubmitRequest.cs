namespace Common.Contracts;

public sealed record CardSubmitRequest(
    string Pan,
    int ExpiryMonth,
    int ExpiryYear,
    string Cvv,
    string CardholderName
);
