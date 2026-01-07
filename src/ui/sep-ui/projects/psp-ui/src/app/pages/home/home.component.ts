import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  txId = '';

  constructor(private router: Router) {}

  openTx() {
    const id = (this.txId || '').trim();
    if (!id) return;
    this.router.navigate(['/tx', id]);
  }
}
