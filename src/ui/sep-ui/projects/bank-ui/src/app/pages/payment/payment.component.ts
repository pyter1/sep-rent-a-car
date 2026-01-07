import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

type PaymentView = {
  paymentId: string;
  pspTransactionId: string;
  amount: number;
  currency: string;
  status: number;
  attempted: boolean;
  expiresAtUtc: string;
};

@Component({
  selector: 'app-payment',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './payment.component.html',
  styleUrl: './payment.component.scss'
})
export class PaymentComponent {
  paymentId = '';
  p: PaymentView | null = null;
  loading = true;
  submitting = false;
  error: string | null = null;
  doneMessage: string | null = null;

  form = {
    pan: '4242424242424242',
    expiryMonth: 12,
    expiryYear: 2030,
    cvv: '123',
    cardholderName: 'Test User'
  };

  constructor(private route: ActivatedRoute, private http: HttpClient) {
    this.paymentId = this.route.snapshot.paramMap.get('paymentId') ?? '';
    this.refresh();
  }

  refresh() {
    this.loading = true;
    this.error = null;

    this.http.get<PaymentView>(`/api/bank/payments/${this.paymentId}`).subscribe({
      next: (x) => { this.p = x; this.loading = false; },
      error: () => { this.error = 'Payment not found.'; this.loading = false; }
    });
  }

  submit() {
    this.submitting = true;
    this.error = null;
    this.doneMessage = null;

    this.http.post<any>(`/api/bank/payments/${this.paymentId}/card/submit`, this.form).subscribe({
      next: (res) => {
        this.doneMessage = res?.message ?? 'Payment completed.';
        this.submitting = false;
        this.refresh();
      },
      error: (err) => {
        this.error = err?.error?.message ?? 'Submit failed.';
        this.submitting = false;
      }
    });
  }
}
