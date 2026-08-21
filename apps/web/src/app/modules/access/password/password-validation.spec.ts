import { FormControl } from '@angular/forms';

import { PASSWORD_VALIDATORS, passwordValidationMessage } from './password-validation';

describe('password validation', () => {
  it('reports passwords shorter than 12 characters', () => {
    const control = new FormControl('Aa1!short', { nonNullable: true, validators: PASSWORD_VALIDATORS });

    expect(control.invalid).toBe(true);
    expect(passwordValidationMessage(control)).toBe('La contraseña debe contener entre 12 y 128 caracteres.');
  });

  it('reports every missing character category in a stable order', () => {
    const control = new FormControl('abcdefghijkl', { nonNullable: true, validators: PASSWORD_VALIDATORS });

    expect(control.hasError('passwordUppercase')).toBe(true);
    expect(control.hasError('passwordDigit')).toBe(true);
    expect(control.hasError('passwordSymbol')).toBe(true);
    expect(passwordValidationMessage(control)).toBe('La contraseña debe incluir una letra mayúscula.');
  });

  it('accepts a password that satisfies the complete policy', () => {
    const control = new FormControl('A-valid-password-123!', { nonNullable: true, validators: PASSWORD_VALIDATORS });

    expect(control.valid).toBe(true);
    expect(passwordValidationMessage(control)).toBeNull();
  });
});
