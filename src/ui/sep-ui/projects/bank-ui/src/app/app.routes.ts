import { Routes } from '@angular/router';
import { PaymentComponent } from './pages/payment/payment.component';

export const routes: Routes = [
  { path: 'payments/:paymentId', component: PaymentComponent },
  { path: '**', redirectTo: 'payments/invalid' }
];
