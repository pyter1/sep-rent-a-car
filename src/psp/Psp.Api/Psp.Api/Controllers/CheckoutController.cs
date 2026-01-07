﻿using Common.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Psp.Api.Data;
using Psp.Api.Data.Entities;
using Psp.Api.Services;

namespace Psp.Api.Controllers;

[ApiController]
[Route("api/psp/transactions")]
public sealed class CheckoutController : ControllerBase
{
    private readonly PspDbContext _db;
    private readonly BankClient _bank;
    private readonly IConfiguration _config;

    public CheckoutController(PspDbContext db, BankClient bank, IConfiguration config)
    {
        _db = db;
        _bank = bank;
        _config = config;
    }

    // IMPORTANT: this type MUST exist (otherwise StartCard/StartQr will be red).
    public sealed record StartPaymentResponse(Guid BankPaymentId, string RedirectUrl);

    [HttpPost("init")]
    public async Task<ActionResult<PspInitResponse>> Init([FromBody] PspInitRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0) return BadRequest("Amount must be > 0.");
        if (string.IsNullOrWhiteSpace(request.Currency)) return BadRequest("Currency is required.");
        if (string.IsNullOrWhiteSpace(request.MerchantOrderId)) return BadRequest("MerchantOrderId is required.");
        if (string.IsNullOrWhiteSpace(request.SuccessUrl)) return BadRequest("SuccessUrl is required.");
        if (string.IsNullOrWhiteSpace(request.FailUrl)) return BadRequest("FailUrl is required.");
        if (string.IsNullOrWhiteSpace(request.ErrorUrl)) return BadRequest("ErrorUrl is required.");

        var tx = new PspTransaction
        {
            Id = Guid.NewGuid(),
            MerchantOrderId = request.MerchantOrderId,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Status = TransactionStatus.Created,
            CreatedAtUtc = DateTime.UtcNow,
            SuccessUrl = request.SuccessUrl,
            FailUrl = request.FailUrl,
            ErrorUrl = request.ErrorUrl,
            BankPaymentId = null
        };

        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        // PSP should redirect to PSP UI checkout page (method selection happens there)
        var pspUiBase = _config["Ui:PublicBaseUrl"] ?? "http://localhost:4201";
        var checkoutUrl = $"{pspUiBase.TrimEnd('/')}/checkout/{tx.Id}";

        return Ok(new PspInitResponse(tx.Id, checkoutUrl));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PspTransaction>> Get(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return tx is null ? NotFound() : Ok(tx);
    }

    [HttpPost("{id:guid}/start-card")]
    public async Task<ActionResult<StartPaymentResponse>> StartCard(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (tx is null) return NotFound();

        if (tx.BankPaymentId is not null)
            return Conflict(new { message = "Bank payment session already created for this transaction." });

        BankInitResponse bankResp;
        try
        {
            bankResp = await _bank.InitPaymentAsync(
                new BankInitRequest(tx.Id, tx.Amount, tx.Currency),
                ct
            );
        }
        catch (HttpRequestException ex)
        {
            tx.Status = TransactionStatus.Error;
            await _db.SaveChangesAsync(ct);

            return StatusCode(StatusCodes.Status502BadGateway, new { message = $"Bank init failed: {ex.Message}" });
        }

        tx.BankPaymentId = bankResp.PaymentId;
        tx.Status = TransactionStatus.Redirected;
        await _db.SaveChangesAsync(ct);

        return Ok(new StartPaymentResponse(bankResp.PaymentId, bankResp.PaymentUrl));
    }

    [HttpPost("{id:guid}/start-qr")]
    public async Task<ActionResult<StartPaymentResponse>> StartQr(Guid id, CancellationToken ct)
    {
        // Separate endpoint so you can branch QR behavior later
        return await StartCard(id, ct);
    }
}
