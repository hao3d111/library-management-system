import { apiRequest, escapeHtml, formatDate } from '../api.js';

export async function renderDashboardView(container) {
  container.innerHTML = `
    <div class="card">
      <div class="toolbar" style="margin-bottom:0;">
        <form id="report-filter-form" class="search-bar">
          <div style="display:flex;align-items:center;gap:8px;">
            <label for="rep-start" style="font-size:13px;font-weight:600;">From</label>
            <input id="rep-start" type="date" class="form-control" style="max-width:170px;" />
          </div>
          <div style="display:flex;align-items:center;gap:8px;">
            <label for="rep-end" style="font-size:13px;font-weight:600;">To</label>
            <input id="rep-end" type="date" class="form-control" style="max-width:170px;" />
          </div>
          <button type="submit" class="btn btn-primary">Generate Report</button>
          <button type="button" id="rep-clear" class="btn btn-secondary">All Time</button>
        </form>
        <div id="rep-timing" style="font-size:12.5px;color:var(--ink-muted);"></div>
      </div>
    </div>
    <div id="report-body">Loading statistical dashboard...</div>
  `;

  const startInput = container.querySelector('#rep-start');
  const endInput = container.querySelector('#rep-end');
  const timingEl = container.querySelector('#rep-timing');
  const reportBody = container.querySelector('#report-body');

  container.querySelector('#report-filter-form').addEventListener('submit', (e) => {
    e.preventDefault();
    loadReport();
  });

  container.querySelector('#rep-clear').addEventListener('click', () => {
    startInput.value = '';
    endInput.value = '';
    loadReport();
  });

  async function loadReport() {
    const params = new URLSearchParams();
    if (startInput.value) params.set('startDate', startInput.value);
    if (endInput.value) params.set('endDate', endInput.value);

    const t0 = performance.now();
    try {
      const data = await apiRequest(`/api/reports/overview${params.toString() ? `?${params}` : ''}`);
      const elapsed = Math.round(performance.now() - t0);
      timingEl.textContent = `Generated in ${elapsed} ms`;

      const maxBorrows = Math.max(1, ...data.categoryBreakdown.map((c) => c.borrowCount));

      reportBody.innerHTML = `
        <div class="stats-grid">
          <div class="stat-card">
            <div class="stat-label">Total Books (Titles)</div>
            <div class="stat-value">${data.totalBooks}</div>
            <div style="font-size:12px;color:var(--ink-muted);margin-top:4px;">${data.availableCopies} / ${data.totalCopies} copies available</div>
          </div>
          <div class="stat-card">
            <div class="stat-label">Registered Members</div>
            <div class="stat-value accent">${data.totalRegisteredMembers}</div>
          </div>
          <div class="stat-card">
            <div class="stat-label">Active Borrowings</div>
            <div class="stat-value accent">${data.activeBorrowings}</div>
          </div>
          <div class="stat-card">
            <div class="stat-label">Overdue Transactions</div>
            <div class="stat-value ${data.overdueTransactions > 0 ? 'danger' : ''}">${data.overdueTransactions}</div>
          </div>
          <div class="stat-card">
            <div class="stat-label">Returned (In Range)</div>
            <div class="stat-value">${data.returnedTransactions}</div>
          </div>
          <div class="stat-card">
            <div class="stat-label">Fines Collected / Pending</div>
            <div class="stat-value" style="font-size:24px;">$${Number(data.totalFinesCollected).toFixed(2)}</div>
            <div style="font-size:12px;color:var(--danger);margin-top:4px;">Pending overdue: $${Number(data.estimatedPendingFines).toFixed(2)}</div>
          </div>
        </div>

        <div class="card">
          <h2 style="font-family:var(--font-serif);font-size:18px;margin-bottom:14px;">Category Activity Breakdown</h2>
          <div style="display:flex;flex-direction:column;gap:12px;">
            ${data.categoryBreakdown.map((c) => {
              const pct = Math.round((c.borrowCount / maxBorrows) * 100);
              return `
                <div>
                  <div style="display:flex;justify-content:space-between;font-size:13.5px;margin-bottom:4px;">
                    <strong>${escapeHtml(c.categoryName)}</strong>
                    <span style="color:var(--ink-muted);">${c.bookCount} book(s) • ${c.borrowCount} borrowing(s)</span>
                  </div>
                  <div style="height:10px;background:#eceae2;border-radius:999px;overflow:hidden;">
                    <div style="height:100%;width:${c.borrowCount > 0 ? Math.max(pct, 6) : 0}%;background:var(--accent);border-radius:999px;"></div>
                  </div>
                </div>
              `;
            }).join('')}
          </div>
        </div>

        <div class="card">
          <h2 style="font-family:var(--font-serif);font-size:18px;margin-bottom:14px;">Recent Transactions (${data.totalTransactionsInRange} total in range)</h2>
          ${data.recentTransactions.length === 0 ? `
            <div class="empty-state">
              <h3>No transactions in selected date range</h3>
            </div>
          ` : `
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Book</th>
                    <th>Member</th>
                    <th>Borrow Date</th>
                    <th>Due Date</th>
                    <th>Status</th>
                    <th>Fine</th>
                  </tr>
                </thead>
                <tbody>
                  ${data.recentTransactions.map((t) => `
                    <tr>
                      <td>#${t.id}</td>
                      <td><strong>${escapeHtml(t.bookTitle)}</strong></td>
                      <td>${escapeHtml(t.memberName)} (${escapeHtml(t.memberCode)})</td>
                      <td>${formatDate(t.borrowDate)}</td>
                      <td>${formatDate(t.dueDate)}</td>
                      <td>
                        ${t.status === 'Returned'
                          ? '<span class="badge badge-success">Returned</span>'
                          : t.status === 'Overdue'
                            ? '<span class="badge badge-danger">Overdue</span>'
                            : '<span class="badge badge-info">Borrowed</span>'}
                      </td>
                      <td>$${Number(t.status === 'Returned' ? t.fineAmount : t.estimatedFine).toFixed(2)}</td>
                    </tr>
                  `).join('')}
                </tbody>
              </table>
            </div>
          `}
        </div>
      `;
    } catch (err) {
      reportBody.innerHTML = `<div class="alert alert-error">${escapeHtml(err.message)}</div>`;
    }
  }

  await loadReport();
}
