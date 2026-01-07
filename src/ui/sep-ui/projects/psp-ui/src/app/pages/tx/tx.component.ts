import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Subscription, timer } from 'rxjs';
import { CommonModule } from '@angular/common';   // ✅ ADD THIS

type PspTransactionDto = {
  id: string;
  merchantOrderId: string;
  amount: number;
  currency: string;
  status: number;
  createdAtUtc: string;
  successUrl: string;
  failUrl: string;
  errorUrl: string;
  bankPaymentId?: string | null;
  merchantNotified?: boolean;
  merchantNotifiedAtUtc?: string | null;
  merchantNotifyAttempts?: number;
  merchantNotifyLastError?: string | null;
};

@Component({
  selector: 'app-tx',
  standalone: true,
  imports: [CommonModule],  // ✅ ADD THIS (enables *ngIf)
  templateUrl: './tx.component.html',
  styleUrl: './tx.component.scss'
})
export class TxComponent implements OnInit, OnDestroy {
  txId = '';
  loading = true;
  error: string | null = null;
  tx: PspTransactionDto | null = null;

  private sub?: Subscription;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private http: HttpClient
  ) {}

  ngOnInit(): void {
    this.txId = this.route.snapshot.paramMap.get('id') ?? '';
    this.sub = timer(0, 2000).subscribe(() => this.load());
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  back() {
    this.router.navigate(['/']);
  }

  private load() {
    if (!this.txId) return;

    this.loading = true;
    this.error = null;

    this.http.get<PspTransactionDto>(`/api/psp/transactions/${this.txId}`)
      .subscribe({
        next: (data) => { this.tx = data; this.loading = false; },
        error: (err) => {
          this.tx = null;
          this.loading = false;
          this.error = err?.error?.toString?.() ?? err?.message ?? 'Failed to load transaction.';
        }
      });
  }
}
