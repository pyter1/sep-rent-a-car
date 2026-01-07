// import { Component } from '@angular/core';

// @Component({
//   selector: 'app-home',
//   standalone: true,
//   templateUrl: './home.component.html',
//   styleUrl: './home.component.scss',
// })
// export class HomeComponent {}
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';

type Car = {
  id: string;
  name: string;
  specs: string;
  priceEur: number;
};

type InitResponse = {
  transactionId: string;
  redirectUrl?: string;
};

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  loadingCarId: string | null = null;
  error: string | null = null;

  cars: Car[] = [
    { id: 'c1', name: 'VW Golf 7', specs: '1.6 TDI • Manual • 2017', priceEur: 35 },
    { id: 'c2', name: 'Audi A4', specs: '2.0 TDI • Automatic • 2018', priceEur: 55 },
    { id: 'c3', name: 'Škoda Octavia', specs: '1.6 TDI • Manual • 2019', priceEur: 40 }
  ];

  constructor(private http: HttpClient) {}

  rent(car: Car) {
    this.error = null;
    this.loadingCarId = car.id;

    // Minimal request; must match what your WebShop.Api expects in PspInitRequest.
    // If your backend requires different field names, change ONLY this object.
    const body: any = {
      merchantOrderId: `order-${Date.now()}`,
      amount: car.priceEur,
      currency: 'EUR',

      // These are server-to-server callbacks; your PSP will POST to these
      successUrl: 'http://webshop-api:7003/payment/success',
      failUrl: 'http://webshop-api:7003/payment/fail',
      errorUrl: 'http://webshop-api:7003/payment/error'
    };

    this.http.post<InitResponse>('/api/payments/init', body).subscribe({
    next: (res) => {
      console.log('INIT RESPONSE:', res);
      // debugger;
      const url = res.redirectUrl ?? `http://localhost:4201/checkout/${res.transactionId}`;
      window.location.href = url;
    },


      error: (err) => {
        this.error = err?.error?.message ?? 'Init failed.';
        this.loadingCarId = null;
      }
    });
  }
}
