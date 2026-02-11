import { Routes } from '@angular/router';
import { PaymentComponent } from './pages/payment/payment.component';
import { ScanComponent } from './pages/scan/scan.component';

export const routes: Routes = [
  { path: 'payments/:paymentId', component: PaymentComponent },
  { path: 'scan', component: ScanComponent },
  { path: '**', redirectTo: 'payments/invalid' }
];
