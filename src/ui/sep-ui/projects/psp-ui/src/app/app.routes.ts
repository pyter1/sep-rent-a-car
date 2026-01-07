import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home.component';
import { TxComponent } from './pages/tx/tx.component';
import { CheckoutComponent } from './pages/checkout/checkout.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'tx/:id', component: TxComponent },
  { path: 'checkout/:txId', component: CheckoutComponent },
  { path: '**', redirectTo: '' }
];
