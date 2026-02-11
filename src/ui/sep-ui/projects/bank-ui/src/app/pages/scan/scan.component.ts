import { Component, ChangeDetectorRef, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { BrowserQRCodeReader, IScannerControls } from '@zxing/browser';

type ValidateResponse = {
  ok: boolean;
  errors: string[];
  fields: any;
  embeddedPaymentId?: string | null;
};

@Component({
  selector: 'app-scan',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './scan.component.html',
  styleUrl: './scan.component.scss'
})
export class ScanComponent {
  @ViewChild('video', { static: true }) video!: ElementRef<HTMLVideoElement>;

  private reader = new BrowserQRCodeReader();
  private controls: IScannerControls | null = null;

  scanning = false;
  payload: string | null = null;

  validation: ValidateResponse | null = null;
  error: string | null = null;

  confirming = false;
  confirmMsg: string | null = null;

  constructor(private http: HttpClient, private cdr: ChangeDetectorRef) {}

  ngOnInit() {
    this.start();
  }

  ngOnDestroy() {
    this.controls?.stop();
    this.controls = null;
  }

  async start() {
    this.error = null;
    this.validation = null;
    this.confirmMsg = null;
    this.payload = null;

    try {
      this.scanning = true;
      this.cdr.detectChanges();

      this.controls = await this.reader.decodeFromVideoDevice(
        undefined,
        this.video.nativeElement,
        (result, err, controls) => {
          if (result) {
            this.payload = result.getText();
            this.scanning = false;
            controls.stop();
            this.controls = null;
            this.cdr.detectChanges();
            this.validate();
          }
        }
      );
    } catch (e: any) {
      this.scanning = false;
      this.error = e?.message ?? 'Unable to access camera.';
      this.cdr.detectChanges();
    }
  }

  validate() {
    if (!this.payload) return;

    this.http.post<ValidateResponse>('/api/bank/ips/validate', { payload: this.payload }).subscribe({
      next: (res) => {
        this.validation = res;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = err?.error?.message ?? 'Validation failed.';
        this.cdr.detectChanges();
      }
    });
  }

  confirm() {
    if (!this.payload) return;

    this.confirming = true;
    this.confirmMsg = null;
    this.error = null;
    this.cdr.detectChanges();

    this.http.post<any>('/api/bank/ips/confirm', { payload: this.payload }).subscribe({
      next: (res) => {
        this.confirmMsg = res?.message ?? 'Confirmed.';
        this.confirming = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = err?.error?.message ?? 'Confirm failed.';
        this.confirming = false;
        this.cdr.detectChanges();
      }
    });
  }
    startCamera() {
    this.start();
    }

    async onFileSelected(evt: Event) {
    this.error = null;
    this.validation = null;
    this.confirmMsg = null;

    const input = evt.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    try {
        // Stop camera if it is running
        this.controls?.stop();
        this.controls = null;
        this.scanning = false;

        const url = URL.createObjectURL(file);
        const result = await this.reader.decodeFromImageUrl(url);
        URL.revokeObjectURL(url);

        this.payload = result.getText();
        this.cdr.detectChanges();

        this.validate();
    } catch (e: any) {
        this.error = e?.message ?? 'Could not read QR from the selected image.';
        this.cdr.detectChanges();
    } finally {
        // allow selecting the same file again
        input.value = '';
    }
    }

}
