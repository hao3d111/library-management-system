import { apiRequest, escapeHtml, formatDate, showToast } from '../api.js';

export async function renderSettingsView(container) {
  try {
    const current = await apiRequest('/api/settings');

    container.innerHTML = `
      <div class="card" style="max-width:640px;">
        <h2 style="font-family:var(--font-serif);font-size:19px;margin-bottom:6px;">Library Policy & Fine Rules</h2>
        <p style="color:var(--ink-muted);font-size:13.5px;margin-bottom:20px;">
          Last updated: <span id="settings-updated">${formatDate(current.updatedAt)}</span>
        </p>

        <div id="settings-error" class="alert alert-error hidden"></div>

        <form id="settings-form">
          <div class="form-group">
            <label for="st-limit">Maximum Active Borrowings per Member</label>
            <input id="st-limit" type="number" min="1" max="50" class="form-control" value="${current.maxBorrowLimit}" required />
          </div>

          <div class="form-group">
            <label for="st-days">Standard Loan Duration (Days)</label>
            <input id="st-days" type="number" min="1" max="365" class="form-control" value="${current.loanDurationDays}" required />
          </div>

          <div class="form-group">
            <label for="st-fine">Daily Overdue Fine Rate ($ / day)</label>
            <input id="st-fine" type="number" min="0" max="1000" step="0.25" class="form-control" value="${Number(current.dailyFineAmount).toFixed(2)}" required />
          </div>

          <button type="submit" class="btn btn-primary">Save Library Policy</button>
        </form>
      </div>
    `;

    container.querySelector('#settings-form').addEventListener('submit', async (e) => {
      e.preventDefault();
      const errBox = container.querySelector('#settings-error');
      errBox.classList.add('hidden');

      try {
        const updated = await apiRequest('/api/settings', {
          method: 'PUT',
          body: JSON.stringify({
            maxBorrowLimit: Number(container.querySelector('#st-limit').value),
            loanDurationDays: Number(container.querySelector('#st-days').value),
            dailyFineAmount: Number(container.querySelector('#st-fine').value)
          })
        });
        container.querySelector('#settings-updated').textContent = formatDate(updated.updatedAt);
        showToast('Library policy settings saved successfully.');
      } catch (err) {
        errBox.textContent = err.message;
        errBox.classList.remove('hidden');
      }
    });
  } catch (err) {
    container.innerHTML = `<div class="alert alert-error">${escapeHtml(err.message)}</div>`;
  }
}
