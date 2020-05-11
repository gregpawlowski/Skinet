import { Component, OnInit } from '@angular/core';
import { FormGroup, FormBuilder, Validators, AsyncValidatorFn } from '@angular/forms';
import { AccountService } from '../account.service';
import { Router } from '@angular/router';
import { timer, of } from 'rxjs';
import { switchMap, tap, map } from 'rxjs/operators';

@Component({
  selector: 'app-register',
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss']
})
export class RegisterComponent implements OnInit {
  registerForm: FormGroup;
  errors: string[];

  constructor(private fb: FormBuilder, private accountService: AccountService, private router: Router) { }

  ngOnInit(): void {
    this.createRegisterForm();
  }

  createRegisterForm() {
    this.registerForm = this.fb.group({
      displayName: [null, [Validators.required]],
      email: [null, [Validators.required, Validators.pattern(/^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$/)], [this.validateEmailNotTaken()]],
      password: [null, [Validators.required]]
    });
  }

  onSubmit() {
    this.accountService.register(this.registerForm.value)
      .subscribe(() => this.router.navigateByUrl('/shop'), (err) => this.errors = err.errors);
  }

  // THis is an async validator, it returns a function that returnsa promis.
  // Use timer to debounce the validator every half second
  // Async validators are only called if we passed all synchronous validators have passed.
  validateEmailNotTaken(): AsyncValidatorFn {
    return control => {
      return timer(500)
        .pipe(
          switchMap(() => {
            if (!control.value) {
              return of(null);
            }
            return this.accountService.checkEmailExists(control.value)
              .pipe(
                map( res => {
                  return res ? { emailExists: true } : null;
                })
              );
          })
        );
    };
  }

}
