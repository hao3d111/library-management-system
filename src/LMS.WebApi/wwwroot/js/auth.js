import { apiRequest, getSession, setSession } from './api.js';

const existing = getSession();
if (existing?.token && existing?.redirectUrl) {
  window.location.href = existing.redirectUrl;
}

const form = document.getElementById('login-form');
const alertBox = document.getElementById('login-alert');
const usernameInput = document.getElementById('username');
const passwordInput = document.getElementById('password');
const usernameErr = document.getElementById('username-error');
const passwordErr = document.getElementById('password-error');
const loginBtn = document.getElementById('login-btn');

document.querySelectorAll('[data-demo-user]').forEach((btn) => {
  btn.addEventListener('click', () => {
    usernameInput.value = btn.dataset.demoUser;
    passwordInput.value = btn.dataset.demoPass;
    clearErrors();
  });
});

function clearErrors() {
  alertBox.classList.add('hidden');
  alertBox.textContent = '';
  usernameErr.textContent = '';
  passwordErr.textContent = '';
}

form.addEventListener('submit', async (e) => {
  e.preventDefault();
  clearErrors();

  const username = usernameInput.value.trim();
  const password = passwordInput.value;

  let hasClientError = false;
  if (!username) {
    usernameErr.textContent = 'Username is required.';
    hasClientError = true;
  }
  if (!password) {
    passwordErr.textContent = 'Password is required.';
    hasClientError = true;
  }
  if (hasClientError) {
    alertBox.textContent = 'Please enter both your username and password.';
    alertBox.classList.remove('hidden');
    return;
  }

  loginBtn.disabled = true;
  loginBtn.textContent = 'Signing in...';

  try {
    const response = await apiRequest('/api/auth/login', {
      method: 'POST',
      skipAuth: true,
      body: JSON.stringify({ username, password })
    });

    setSession(response);
    window.location.href = response.redirectUrl || '/app.html#catalog';
  } catch (err) {
    alertBox.textContent = err.message || 'Invalid username or password.';
    alertBox.classList.remove('hidden');
    if (err.errors?.username) usernameErr.textContent = err.errors.username[0];
    if (err.errors?.password) passwordErr.textContent = err.errors.password[0];
  } finally {
    loginBtn.disabled = false;
    loginBtn.textContent = 'Sign In';
  }
});
