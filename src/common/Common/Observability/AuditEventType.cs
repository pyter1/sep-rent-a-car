namespace Common.Observability;

public enum AuditEventType
{
    // Auth (later, for WebShop)
    AuthLoginSuccess = 1000,
    AuthLoginFail = 1001,
    AuthLockout = 1002,

    // WebShop (later)
    WebShopOrderCreated = 2000,
    WebShopOrderStatusChanged = 2001,

    // PSP
    PaymentInit = 3000,
    PaymentStartCard = 3001,
    PaymentStartQr = 3002,
    PspBankNotifyReceived = 3100,
    PspMerchantCallbackAttempt = 3200,
    PspMerchantCallbackSuccess = 3201,
    PspMerchantCallbackFail = 3202,

    // PSP reconciliation
    PspReconcileAttempt = 3300,
    PspReconcileUpdated = 3301,
    PspReconcileNoChange = 3302,
    PspReconcileFail = 3303,


    // Bank
    BankPaymentCreated = 4000,
    BankPaymentExpired = 4001,
    BankCardSubmit = 4100,
    BankQrConfirm = 4200,

    // Security / policy
    SecurityPolicyViolation = 9000
}
