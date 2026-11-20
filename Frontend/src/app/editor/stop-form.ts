import { Component, DestroyRef, effect, inject, input, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { FixedStop, StopDetails, StopPosition } from '../api/itineraries-api';

const requiredText = [Validators.required, Validators.pattern(/\S/)];

function validPrice(control: AbstractControl<number | null>) {
  const price = control.value;
  return price === null ||
    (Number.isFinite(price) && price >= 0 && Number(price.toFixed(2)) === price)
    ? null
    : { price: true };
}

function validSource(control: AbstractControl<string>) {
  const value = control.value.trim();
  if (!value) return null;
  try {
    return /^https?:\/\//i.test(value) && new URL(value).hostname ? null : { sourceUrl: true };
  } catch {
    return { sourceUrl: true };
  }
}

@Component({
  selector: 'app-stop-form',
  imports: [ReactiveFormsModule],
  templateUrl: './stop-form.html',
  styleUrl: './stop-form.css',
})
export class StopForm {
  readonly initial = input<FixedStop | null>(null);
  readonly submitLabel = input('Applica tappa');
  readonly busy = input(false);
  readonly submitted = output<StopDetails>();
  readonly cancelled = output<void>();
  readonly preview = output<StopPosition | null>();

  private readonly builder = inject(FormBuilder);
  protected readonly form = this.builder.group({
    name: this.builder.nonNullable.control('', requiredText),
    description: this.builder.nonNullable.control('', requiredText),
    municipality: this.builder.nonNullable.control('', requiredText),
    province: this.builder.nonNullable.control('', requiredText),
    cap: this.builder.nonNullable.control('', [
      Validators.required,
      Validators.pattern(/^(?!00000$)[0-9]{5}$/),
    ]),
    price: this.builder.control<number | null>(null, [Validators.required, validPrice]),
    sourceUrl: this.builder.nonNullable.control('', validSource),
    latitude: this.builder.control<number | null>(null, [
      Validators.required,
      Validators.min(-90),
      Validators.max(90),
    ]),
    longitude: this.builder.control<number | null>(null, [
      Validators.required,
      Validators.min(-180),
      Validators.max(180),
    ]),
  });

  constructor() {
    effect(() => {
      const stop = this.initial();
      this.form.reset(
        stop
          ? {
              ...stop,
              municipality: stop.municipality ?? '',
              province: stop.province ?? '',
              cap: stop.cap ?? '',
              sourceUrl: stop.sourceUrl ?? '',
            }
          : undefined,
        { emitEvent: false },
      );
    });
    this.form.valueChanges
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.preview.emit(this.readPosition()));
  }

  protected submit(): void {
    if (this.busy()) return;
    this.form.markAllAsTouched();
    const position = this.readPosition();
    const { municipality, province, cap, price, sourceUrl } = this.form.getRawValue();
    if (position && this.form.valid && price !== null) {
      this.submitted.emit({
        ...position,
        municipality: municipality.trim(),
        province: province.trim(),
        cap,
        price,
        sourceUrl: sourceUrl.trim() || null,
      });
    }
  }

  private readPosition(): StopPosition | null {
    const { name, description, latitude, longitude } = this.form.getRawValue();
    if (
      latitude === null ||
      longitude === null ||
      this.form.controls.latitude.invalid ||
      this.form.controls.longitude.invalid
    )
      return null;
    return { name: name.trim(), description: description.trim(), latitude, longitude };
  }
}
