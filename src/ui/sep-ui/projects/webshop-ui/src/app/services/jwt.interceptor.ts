import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpHandler, HttpRequest } from '@angular/common/http';
import { AuthService } from './auth.service';

@Injectable()
export class JwtInterceptor implements HttpInterceptor {
  constructor(private auth: AuthService) {}

  intercept(req: HttpRequest<any>, next: HttpHandler) {
    const token = this.auth.getToken();

    // attach only for backend calls
    if (!token || !req.url.startsWith('/api/')) return next.handle(req);

    return next.handle(
      req.clone({
        setHeaders: { Authorization: `Bearer ${token}` }
      })
    );
  }
}
