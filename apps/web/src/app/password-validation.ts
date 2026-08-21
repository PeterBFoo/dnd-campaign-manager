import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

const passwordPolicyValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const password = typeof control.value === 'string' ? control.value : '';
  if (!password) {
    return null;
  }

  if (password.length < 12 || password.length > 128) {
    return { passwordLength: true };
  }

  const errors: ValidationErrors = {};
  if (!/\p{Lu}/u.test(password)) {
    errors['passwordUppercase'] = true;
  }
  if (!/\p{Ll}/u.test(password)) {
    errors['passwordLowercase'] = true;
  }
  if (!/\p{Nd}/u.test(password)) {
    errors['passwordDigit'] = true;
  }
  if (!/[^\p{L}\p{N}]/u.test(password)) {
    errors['passwordSymbol'] = true;
  }

  return Object.keys(errors).length > 0 ? errors : null;
};

export const PASSWORD_VALIDATORS: ValidatorFn[] = [Validators.required, passwordPolicyValidator];

export function passwordValidationMessage(control: AbstractControl): string | null {
  if (control.hasError('required')) {
    return 'Introduce una contraseña.';
  }
  if (control.hasError('passwordLength')) {
    return 'La contraseña debe contener entre 12 y 128 caracteres.';
  }
  if (control.hasError('passwordUppercase')) {
    return 'La contraseña debe incluir una letra mayúscula.';
  }
  if (control.hasError('passwordLowercase')) {
    return 'La contraseña debe incluir una letra minúscula.';
  }
  if (control.hasError('passwordDigit')) {
    return 'La contraseña debe incluir un número.';
  }
  if (control.hasError('passwordSymbol')) {
    return 'La contraseña debe incluir un símbolo.';
  }

  return null;
}
