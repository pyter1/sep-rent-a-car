import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TxComponent } from './tx.component';

describe('TxComponent', () => {
  let component: TxComponent;
  let fixture: ComponentFixture<TxComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TxComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TxComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
