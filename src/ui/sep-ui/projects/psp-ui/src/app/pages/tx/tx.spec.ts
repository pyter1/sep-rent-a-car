import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Tx } from './tx';

describe('Tx', () => {
  let component: Tx;
  let fixture: ComponentFixture<Tx>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Tx]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Tx);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
