import { Component, ChangeDetectorRef } from '@angular/core';
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
  notifiedPspStatus?: number | null; // optional (your API returns it)
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

  constructor(
    private route: ActivatedRoute,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    const pid = this.route.snapshot.paramMap.get('paymentId');
    if (!pid) {
      this.error = 'Missing paymentId in route. Expected /payments/:paymentId';
      this.loading = false;
      this.cdr.detectChanges();
      return;
    }

    this.paymentId = pid;
    this.refresh(true);
  }

  refresh(firstLoad = false) {
    if (firstLoad) this.loading = true;
    this.error = null;

    console.log('[BankUI] GET payment', this.paymentId);

    this.http.get<PaymentView>(`/api/bank/payments/${this.paymentId}`).subscribe({
      next: (x) => {
        console.log('[BankUI] GET ok', x);
        this.p = x;
        if (firstLoad) this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.log('[BankUI] GET error', err);
        this.error = err?.error?.message ?? 'Payment not found.';
        if (firstLoad) this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  submit() {
    this.submitting = true;
    this.error = null;
    this.doneMessage = null;
    this.cdr.detectChanges();

    console.log('[BankUI] POST card submit', this.paymentId, this.form);

    this.http.post<any>(`/api/bank/payments/${this.paymentId}/card/submit`, this.form).subscribe({
      next: (res) => {
        console.log('[BankUI] POST ok', res);
        this.doneMessage = res?.message ?? 'Payment completed.';
        this.submitting = false;
        this.cdr.detectChanges();

        // refresh status after submit
        this.refresh(false);
      },
      error: (err) => {
        console.log('[BankUI] POST error', err);
        this.error = err?.error?.message ?? 'Submit failed.';
        this.submitting = false;
        this.cdr.detectChanges();
      }
    });
  }
}
