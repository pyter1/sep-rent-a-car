import { Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import QRCode from 'qrcode';

type PaymentView = {
  paymentId: string;
  pspTransactionId: string;
  amount: number;
  currency: string;
  status: number;
  attempted: boolean;
  expiresAtUtc: string;
  notifiedPspStatus?: number | null;
  cardBrand?: string | null;
  panFirst6?: string | null;
  panLast4?: string | null;
};

type DetectedBrand = 'VISA' | 'MASTERCARD' | 'AMEX' | null;

@Component({
  selector: 'app-payment',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './payment.component.html',
  styleUrl: './payment.component.scss'
})
export class PaymentComponent {
  paymentId = '';
  paymentToken: string | null = null;
  lanHostOverride: string | null = null;

  p: PaymentView | null = null;
  loading = true;
  submitting = false;
  error: string | null = null;
  doneMessage: string | null = null;

  detectedBrand: DetectedBrand = null;

  form = {
    pan: '4242424242424242',
    expiryMonth: 12,
    expiryYear: 2030,
    cvv: '123',
    cardholderName: 'Test User'
  };

  // hardcoded dev URLs (your docker-compose uses these)
  pspUiBaseUrl = 'http://localhost:4201';
  webshopUiBaseUrl = 'http://localhost:4200';

  // --- QR additions ---
  mode: 'card' | 'qr' = 'card';

  qrLoading = false;
  qrError: string | null = null;

  // QR #1: IPS payload
  qrDataUrl: string | null = null;
  qrPayload: string | null = null;

  // QR #2: URL that opens /scan on phone with payment context
  scanPayload: string | null = null;
  scanQrDataUrl: string | null = null;

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

    this.paymentToken = this.route.snapshot.queryParamMap.get('t');

    const m = this.route.snapshot.queryParamMap.get('m');
    this.mode = m === 'qr' ? 'qr' : 'card';

    this.paymentId = pid;
    this.onPanInput(this.form.pan);
    this.refresh(true);

    // IMPORTANT: resolve host override first, then load QR
    this.resolveLanHostOverride().then(() => {
      if (this.mode === 'qr') {
        this.loadQr();
      }
      this.cdr.detectChanges();
    });
  }

  private buildHeaders(): HttpHeaders {
    let h = new HttpHeaders();
    if (this.paymentToken) {
      h = h.set('X-Payment-Token', this.paymentToken);
    }
    return h;
  }

  refresh(firstLoad = false) {
    if (firstLoad) this.loading = true;
    this.error = null;

    this.http.get<PaymentView>(
      `/api/bank/payments/${this.paymentId}`,
      { headers: this.buildHeaders() }
    ).subscribe({
      next: (x) => {
        this.p = x;
        if (firstLoad) this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        const msg = err?.error?.message ?? 'Payment not found.';
        this.error = msg;
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

    this.http.post<any>(
      `/api/bank/payments/${this.paymentId}/card/submit`,
      this.form,
      { headers: this.buildHeaders() }
    ).subscribe({
      next: (res) => {
        this.doneMessage = res?.message ?? 'Payment completed.';
        this.submitting = false;
        this.cdr.detectChanges();
        this.refresh(false);
      },
      error: (err) => {
        const msg = err?.error?.message ?? 'Submit failed.';
        this.error = msg;
        this.submitting = false;
        this.cdr.detectChanges();
      }
    });
  }

  // --- QR additions ---
  setMode(m: 'card' | 'qr') {
    this.mode = m;
    this.doneMessage = null;
    this.error = null;

    if (m === 'qr' && !this.qrDataUrl && !this.qrLoading) {
      this.loadQr();
    }

    this.cdr.detectChanges();
  }

  private buildScanUrl(): string {
    const protocol = window.location.protocol; // http:
    const port = window.location.port;         // 4202
    const host = (this.lanHostOverride && this.lanHostOverride.trim().length > 0)
      ? this.lanHostOverride.trim()
      : window.location.hostname;

    const base = `${protocol}//${host}${port ? `:${port}` : ''}`;

    const qs = new URLSearchParams();
    qs.set('paymentId', this.paymentId);
    if (this.paymentToken) qs.set('t', this.paymentToken);

    return `${base}/scan?${qs.toString()}`;
  }

  get scanUrl(): string {
    return this.buildScanUrl();
  }
  get hostDebug(): string {
    const hn = window.location.hostname;
    return `hostname=${hn} lanHostOverride=${this.lanHostOverride ?? '(null)'}`;
  }

  get brandIconPath(): string | null {
    if (!this.detectedBrand) return null;
    const map: Record<Exclude<DetectedBrand, null>, string> = {
      VISA: 'assets/brands/visa.svg',
      MASTERCARD: 'assets/brands/mastercard.svg',
      AMEX: 'assets/brands/amex.svg',
    };
    return map[this.detectedBrand];
  }

  loadQr() {
    this.qrLoading = true;
    this.qrError = null;

    this.qrDataUrl = null;
    this.qrPayload = null;

    this.scanPayload = null;
    this.scanQrDataUrl = null;

    this.cdr.detectChanges();

    this.http.get<any>(
      `/api/bank/payments/${this.paymentId}/qr/payload`,
      { headers: this.buildHeaders() }
    ).subscribe({
      next: async (res) => {
        const payload = res?.payload as string | undefined;
        if (!payload) {
          this.qrError = 'Missing QR payload.';
          this.qrLoading = false;
          this.cdr.detectChanges();
          return;
        }

        this.qrPayload = payload;

        try {
          // QR #1: IPS payload
          this.qrDataUrl = await QRCode.toDataURL(payload, { errorCorrectionLevel: 'M' });

          // QR #2: Open scanner page on phone
          this.scanPayload = this.buildScanUrl();
          this.scanQrDataUrl = await QRCode.toDataURL(this.scanPayload, { errorCorrectionLevel: 'M' });
        } catch (e: any) {
          this.qrError = e?.message ?? 'QR render failed.';
        }

        this.qrLoading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.qrError = err?.error?.message ?? 'Unable to load QR payload.';
        this.qrLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  onPanInput(value: string) {
    const digitsOnly = (value ?? '').replace(/\D/g, '');
    if (digitsOnly !== this.form.pan) this.form.pan = digitsOnly;
    this.detectedBrand = this.detectBrand(digitsOnly);
  }

  private detectBrand(panDigits: string): DetectedBrand {
    if (!panDigits) return null;

    if (panDigits.startsWith('4')) return 'VISA';

    const first2 = panDigits.length >= 2 ? parseInt(panDigits.slice(0, 2), 10) : NaN;
    const first4 = panDigits.length >= 4 ? parseInt(panDigits.slice(0, 4), 10) : NaN;

    // AMEX: 34 or 37
    if (first2 === 34 || first2 === 37) return 'AMEX';

    // MasterCard: 51-55 or 2221-2720
    if (first2 >= 51 && first2 <= 55) return 'MASTERCARD';
    if (first4 >= 2221 && first4 <= 2720) return 'MASTERCARD';

    return null;
  }

  private async resolveLanHostOverride(): Promise<void> {
  // 1) Manual override if present
  const qpLan = this.route.snapshot.queryParamMap.get('lan');
  if (qpLan && qpLan.trim().length > 0) {
    this.lanHostOverride = qpLan.trim();
    return;
  }

  // Only attempt auto-detect when browsing localhost
  const hn = window.location.hostname;
  if (hn !== 'localhost' && hn !== '127.0.0.1') return;

  // 2) Try WebRTC
  const ip = await this.tryGetLanIpViaWebRtc();
  if (ip) this.lanHostOverride = ip;
}

  private async tryGetLanIpViaWebRtc(): Promise<string | null> {
    try {
      const pc = new RTCPeerConnection({ iceServers: [] });
      pc.createDataChannel('x');

      const ips = new Set<string>();

      pc.onicecandidate = (e) => {
        const cand = e.candidate?.candidate;
        if (!cand) return;

        const m = cand.match(/(\d{1,3}(?:\.\d{1,3}){3})/);
        if (!m) return;

        const ip = m[1];
        if (ip.startsWith('192.168.') || ip.startsWith('10.') || this.isPrivate172(ip)) {
          ips.add(ip);
        }
      };

      const offer = await pc.createOffer({ offerToReceiveAudio: false, offerToReceiveVideo: false });
      await pc.setLocalDescription(offer);

      await new Promise((r) => setTimeout(r, 1200));
      pc.close();

      const list = Array.from(ips);
      const best =
        list.find(x => x.startsWith('192.168.')) ??
        list.find(x => x.startsWith('10.')) ??
        list.find(x => this.isPrivate172(x)) ??
        null;

      return best;
    } catch {
      return null;
    }
  }

  private isPrivate172(ip: string): boolean {
    const parts = ip.split('.').map(x => parseInt(x, 10));
    return parts.length === 4 && parts[0] === 172 && parts[1] >= 16 && parts[1] <= 31;
  }


  private async rebuildScanQrOnly() {
    try {
      this.scanPayload = this.buildScanUrl();
      this.scanQrDataUrl = await QRCode.toDataURL(this.scanPayload, { errorCorrectionLevel: 'M' });
    } catch {
      // ignore
    }
  }
    private async detectLanHostOverrideViaWebRtc(): Promise<void> {
    if (window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1') return;

    try {
      const pc = new RTCPeerConnection({ iceServers: [] });
      pc.createDataChannel('x');

      const ips = new Set<string>();

      pc.onicecandidate = (e) => {
        const cand = e.candidate?.candidate;
        if (!cand) return;

        // candidate:... <ip> <port> typ ...
        const m = cand.match(/(\d{1,3}(?:\.\d{1,3}){3})/);
        if (!m) return;

        const ip = m[1];
        if (ip.startsWith('192.168.') || ip.startsWith('10.') || this.isPrivate172(ip)) {
          ips.add(ip);
        }
      };

      const offer = await pc.createOffer({ offerToReceiveAudio: false, offerToReceiveVideo: false });
      await pc.setLocalDescription(offer);

      // wait a bit for ICE gathering
      await new Promise((r) => setTimeout(r, 800));
      pc.close();

      // Prefer 192.168 then 10 then 172.16-31
      const list = Array.from(ips);
      const best =
        list.find(x => x.startsWith('192.168.')) ??
        list.find(x => x.startsWith('10.')) ??
        list.find(x => this.isPrivate172(x)) ??
        null;

      if (best) this.lanHostOverride = best;
    } catch {
      // ignore
    }
  }

  get maskedPanForDisplay(): string {
    const digits = (this.form.pan ?? '').replace(/\D/g, '');
    if (digits.length <= 4) return digits;
    const last4 = digits.slice(-4);
    return `•••• •••• •••• ${last4}`;
  }

  get backToPspUrl(): string | null {
    if (!this.p?.pspTransactionId) return null;
    return `${this.pspUiBaseUrl}/tx/${this.p.pspTransactionId}`;
  }

  get backToWebShopUrl(): string | null {
    return this.webshopUiBaseUrl;
  }

}

