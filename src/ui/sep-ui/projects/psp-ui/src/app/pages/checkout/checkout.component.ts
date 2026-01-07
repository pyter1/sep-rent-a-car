import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';

type TxView = {
  id: string;
  amount: number;
  currency: string;
  status: string;
  bankPaymentId?: string | null;
};

type StartPaymentResponse = {
  bankPaymentId: string;
  redirectUrl: string;
};

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './checkout.component.html',
})
export class CheckoutComponent {
  txId!: string;
  tx: TxView | null = null;
  loading = true;
  paying = false;
  error: string | null = null;

  constructor(private route: ActivatedRoute, private http: HttpClient) {}

  ngOnInit() {
    this.txId = this.route.snapshot.paramMap.get('txId')!;
    this.loadTx();
  }

  loadTx() {
    this.loading = true;
    this.http.get<TxView>(`/api/psp/transactions/${this.txId}`).subscribe({
      next: (tx) => { this.tx = tx; this.loading = false; },
      error: () => { this.error = 'Failed to load transaction.'; this.loading = false; }
    });
  }

  startCard() {
    this.start(`/api/psp/transactions/${this.txId}/start-card`);
  }

  startQr() {
    this.start(`/api/psp/transactions/${this.txId}/start-qr`);
  }

  private start(url: string) {
    this.paying = true;
    this.error = null;

    this.http.post<StartPaymentResponse>(url, {}).subscribe({
      next: (res) => window.location.href = res.redirectUrl,
      error: (err) => {
        this.error = err?.error?.message ?? 'Unable to start payment.';
        this.paying = false;
      }
    });
  }
}
