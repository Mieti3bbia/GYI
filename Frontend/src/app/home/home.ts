import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DemoSession } from '../auth/demo-session';

@Component({
  imports: [ReactiveFormsModule],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home {
  private readonly session = inject(DemoSession);
  private readonly router = inject(Router);
  protected readonly form = inject(NonNullableFormBuilder).group({ username: '', password: '' });
  protected readonly failed = signal(false);

  protected login(): void {
    const { username, password } = this.form.getRawValue();
    const authenticated = this.session.login(username, password);
    this.failed.set(!authenticated);
    if (authenticated) {
      void this.router.navigate(['/editor', this.session.id]);
    }
  }
}
