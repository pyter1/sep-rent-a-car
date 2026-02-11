import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { HttpHeaders } from '@angular/common/http';
type Car = { id: string; name: string; specs: string; priceEur: number; };
type InitResponse = { transactionId: string; redirectUrl?: string; };

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  loadingCarId: string | null = null;
  error: string | null = null;

  email = 'test@example.com';
  password = 'Test123!';

  cars: Car[] = [
    { id: 'c1', name: 'VW Golf 7', specs: '1.6 TDI • Manual • 2017', priceEur: 35 },
    { id: 'c2', name: 'Audi A4', specs: '2.0 TDI • Automatic • 2018', priceEur: 55 },
    { id: 'c3', name: 'Škoda Octavia', specs: '1.6 TDI • Manual • 2019', priceEur: 40 }
  ];

  constructor(private http: HttpClient, public auth: AuthService) {}

  register() {
    this.error = null;
    this.auth.register(this.email, this.password).subscribe({
      next: () => {},
      error: (err) => this.error = err?.error?.message ?? 'Register failed.'
    });
  }

  login() {
    this.error = null;
    this.auth.login(this.email, this.password).subscribe({
      next: () => {},
      error: (err) => this.error = err?.error?.message ?? 'Login failed.'

    });
  }

  logout() {
    this.auth.clear();
  }

  rent(car: Car) {
    this.error = null;

    const token = this.auth.getToken();
    if (!token) {
      this.error = 'Login first.';
      return;
    }

    this.loadingCarId = car.id;

    const body = {
      merchantOrderId: `order-${Date.now()}`,
      amount: car.priceEur,
      currency: 'EUR'
    };

    const headers = new HttpHeaders({ Authorization: `Bearer ${token}` });

    this.http.post<InitResponse>('/api/payments/init', body, { headers }).subscribe({
      next: (res) => {
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
