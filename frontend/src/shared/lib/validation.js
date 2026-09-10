export function validatePassword(password) {
  const errors = []

  if (password.length < 8) errors.push('min-length')
  if (!/[A-Z]/.test(password)) errors.push('uppercase')
  if (!/\d/.test(password)) errors.push('digit')

  return { isValid: errors.length === 0, errors }
}
