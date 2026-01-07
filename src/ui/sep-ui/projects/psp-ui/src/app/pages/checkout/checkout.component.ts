import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';

type Tx = {
  id: string;
  merchantOrderId: string;
  amount: number;
  currency: string;
  status: number;
  bankPaymentId?: string;
};

type StartCardResponse = {
  bankPaymentId: string;
  bankPaymentUrl: string;
};

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.scss'
})
export class CheckoutComponent {
  txId = '';
  tx: Tx | null = null;
  loading = true;
  paying = false;
  error: string | null = null;

  constructor(private route: ActivatedRoute, private http: HttpClient) {
    this.txId = this.route.snapshot.paramMap.get('txId') ?? '';
    this.load();
  }

  load() {
    this.loading = true;
    this.error = null;

    this.http.get<Tx>(`/api/psp/transactions/${this.txId}`).subscribe({
      next: (t) => { this.tx = t; this.loading = false; },
      error: () => { this.error = 'Transaction not found.'; this.loading = false; }
    });
  }

  payByCard() {
    if (!this.txId) return;

    this.paying = true;
    this.error = null;

    // If your backend route differs, change ONLY this line:
    this.http.post<StartCardResponse>(`/api/psp/checkout/${this.txId}/card`, {}).subscribe({
      next: (res) => {
        // Redirect browser to bank UI
        window.location.href = `http://localhost:4202/payments/${res.bankPaymentId}`;
      },
      error: (err) => {
        this.error = err?.error?.message ?? 'Unable to start card payment.';
        this.paying = false;
      }
    });
  }
}
