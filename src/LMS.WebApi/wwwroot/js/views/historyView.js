import { apiRequest, escapeHtml, formatDate } from '../api.js';

export async function renderHistoryView(container) {
  try {
    const records = await apiRequest('/api/borrowings/my-history');
    const activeCount = records.filter((r) => r.status === 'Borrowed' || r.status === 'Overdue').length;
    const overdueCount = records.filter((r) => r.status === 'Overdue').length;
    const returnedCount = records.filter((r) => r.status === 'Returned').length;

    container.innerHTML = `
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-label">Active Borrowings</div>
          <div class="stat-value accent">${activeCount}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Overdue</div>
          <div class="stat-value ${overdueCount > 0 ? 'danger' : ''}">${overdueCount}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Returned</div>
          <div class="stat-value">${returnedCount}</div>
        </div>
      </div>

      <div class="card">
        ${records.length === 0 ? `
          <div class="empty-state">
            <h3>No borrowing history yet</h3>
            <p>Books you borrow from the library will appear here with due dates and return records.</p>
          </div>
        ` : `
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Book Title</th>
                  <th>Author</th>
                  <th>Borrow Date</th>
                  <th>Due Date</th>
                  <th>Return Date</th>
                  <th>Status</th>
                  <th>Fine</th>
                </tr>
              </thead>
              <tbody>
                ${records.map((r) => `
                  <tr>
                    <td>
                      <strong>${escapeHtml(r.bookTitle)}</strong>
                      <div style="font-size:12px;color:var(--ink-muted);">${escapeHtml(r.bookISBN)}</div>
                    </td>
                    <td>${escapeHtml(r.bookAuthor)}</td>
                    <td>${formatDate(r.borrowDate)}</td>
                    <td>${formatDate(r.dueDate)}</td>
                    <td>${formatDate(r.returnDate)}</td>
                    <td>
                      ${r.status === 'Returned'
                        ? '<span class="badge badge-success">Returned</span>'
                        : r.status === 'Overdue'
                          ? `<span class="badge badge-danger">Overdue (${r.daysOverdue}d)</span>`
                          : '<span class="badge badge-info">Borrowed</span>'}
                    </td>
                    <td>$${Number(r.status === 'Returned' ? r.fineAmount : r.estimatedFine).toFixed(2)}</td>
                  </tr>
                `).join('')}
              </tbody>
            </table>
          </div>
        `}
      </div>
    `;
  } catch (err) {
    container.innerHTML = `<div class="alert alert-error">${escapeHtml(err.message)}</div>`;
  }
}
