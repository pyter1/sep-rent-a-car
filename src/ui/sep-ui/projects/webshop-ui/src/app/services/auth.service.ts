import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { tap } from 'rxjs/operators';

type AuthResponse = { token: string };

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly key = 'jwt';

  constructor(private http: HttpClient) {}

  getToken(): string | null {
    return localStorage.getItem(this.key);
  }

  setToken(token: string) {
    localStorage.setItem(this.key, token);
  }

  clear() {
    localStorage.removeItem(this.key);
  }

  register(email: string, password: string) {
    return this.http.post<AuthResponse>('/api/auth/register', { email, password }).pipe(
      tap(res => this.setToken(res.token))
    );
  }

  login(email: string, password: string) {
    return this.http.post<AuthResponse>('/api/auth/login', { email, password }).pipe(
      tap(res => this.setToken(res.token))
    );
  }
}
