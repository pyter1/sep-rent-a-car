import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { finalize } from 'rxjs/operators';

type TxView = {
  id: string;
  amount: number;
  currency: string;
  status: number;
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
    const p = this.route.snapshot.paramMap.get('txId');
    if (!p) {
      this.error = 'Missing txId in route. Expected /checkout/:txId';
      this.loading = false;
      return;
    }
    this.txId = p;
    this.loadTx(true); // first load only
  }


  loadTx(firstLoad = false) {
    if (firstLoad) this.loading = true;
    this.error = null;

    this.http
      .get<TxView>(`/api/psp/transactions/${this.txId}`)
      .pipe(finalize(() => { if (firstLoad) this.loading = false; }))
      .subscribe({
        next: (tx) => { this.tx = tx; },
        error: (err) => {
          this.error = err?.error?.message ?? 'Failed to load transaction.';
          if (firstLoad) this.loading = false;
        }
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
